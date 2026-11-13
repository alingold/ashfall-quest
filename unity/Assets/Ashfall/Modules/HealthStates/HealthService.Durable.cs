using System;
using System.Collections.Generic;
using Ashfall.Core;

namespace Ashfall.Modules.HealthStates
{
    public sealed partial class HealthService
    {
        DurableBoundary durable;
        int durableDepth;
        long durableGeneration;
        bool durableWriting;
        sealed class StorageGate : IAuthorityGate
        {
            public RuleResult Validate(CommandContext c)
            {
                return c.IsAuthority ? RuleResult.Accept() : RuleResult.Reject("storage restore");
            }
        }

        sealed class DurableBoundary
        {
            internal IHealthImageStore Store;
            internal string Binding, Failure, Manifest;
            internal byte[] Committed;
            internal int MaximumKeys;
            internal long ResidentBytes;
        }

        public string StorageFailure
        {
            get
            {
                lock (sync)
                {
                    return durable == null ? null : durable.Failure;
                }
            }
        }

        public string DurableManifest
        {
            get
            {
                lock (sync)
                {
                    return durable == null ? null : StorageCall(delegate
                    {
                        return durable.Store.Manifest;
                    });
                }
            }
        }

        internal void EnableDurableImages(IHealthImageStore store, string binding, int maximumReceiptKeys = 4096, long residentStateBytes = 67108864)
        {
            lock (sync)
            {
                if (durable != null || store == null || !CheckpointWire.Binding(binding) || maximumReceiptKeys < 1 || maximumReceiptKeys > 16384)
                    throw new ArgumentException("durable configuration");
                if ((long)damage.Count + gains.Count + gainCommands.Count > maximumReceiptKeys)
                    throw new HealthStorageException("resident attach quota");
                ValidateResidentBudget(maximumReceiptKeys, residentStateBytes);
                storageRestoreBinding = binding;
                var d = new DurableBoundary
                {
                    Store = store,
                    Binding = binding,
                    MaximumKeys = maximumReceiptKeys,
                    ResidentBytes = residentStateBytes
                };
                byte[] image = StorageCall(delegate
                {
                    return store.Read();
                });
                if (image == null)
                {
                    image = Checkpoint(StorageContext(), binding, epochIngressClosed, true);
                    StorageCall(delegate
                    {
                        store.Commit(image);
                        return true;
                    });
                }
                else
                    RestoreDurableImage(image, maximumReceiptKeys);
                d.Manifest = StorageCall(delegate
                {
                    return store.Manifest;
                });
                ManifestGeneration(d.Manifest);
                d.Committed = (byte[])image.Clone();
                durable = d;
            }
        }

        static long ManifestGeneration(string manifest)
        {
            var fields = manifest == null ? new string[0] : manifest.Split(':');
            long generation;
            if (fields.Length != 3 || !CheckpointWire.Binding(fields[0]) ||
                !CheckpointWire.Binding(fields[2]) ||
                !Int64.TryParse(fields[1], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out generation) || generation < 1)
                throw new HealthStorageException("invalid storage manifest");
            return generation;
        }

        static void NoManifestRollback(string previous, string next)
        {
            long before = ManifestGeneration(previous), after = ManifestGeneration(next);
            if (previous.Split(':')[0] != next.Split(':')[0] || after < before || after == before && previous != next)
                throw new HealthStorageException("storage rollback or identity conflict");
        }

        static void ValidateResidentBudget(int keys, long bytes)
        {
            if (bytes < 33562624 || bytes > 268435456 || 33554432L + 8192L * keys > bytes)
                throw new HealthStorageException("resident state byte budget (fixed reserve plus conservative per-key allowance)");
        }

        public long ResidentStateBudgetBytes
        {
            get
            {
                lock (sync)
                {
                    return durable == null ? 0 : durable.ResidentBytes;
                }
            }
        }

        CommandContext StorageContext()
        {
            return new CommandContext(session, "host", "durable-image", Math.Max(0, latestTick), epoch, true);
        }

        void RestoreDurableImage(byte[] image, int maximumKeys = 16384)
        {
            HealthService restored;
            var c = new CommandContext(session, "host", "durable-recovery", Int64.MaxValue, epoch, true);
            string binding = durable == null ? storageRestoreBinding : durable.Binding;
            if (!TryRestoreCheckpoint(c, binding, CheckpointWire.Hash(image), image, teams, respawnMode,
                new StorageGate(), world, query, out restored, downedSeconds, respawnDelay, protection))
                throw new HealthStorageException("checkpoint identity, clock or corruption");
            if ((long)restored.damage.Count + restored.gains.Count + restored.gainCommands.Count > maximumKeys)
                throw new HealthStorageException("resident recovery quota");
            durableGeneration = checked(durableGeneration + 1);
            players.Clear();
            foreach (var pair in restored.players)
                players.Add(pair.Key, pair.Value);
            damage.Clear();
            foreach (var pair in restored.damage)
                damage.Add(pair.Key, pair.Value);
            gains.Clear();
            foreach (var pair in restored.gains)
                gains.Add(pair.Key, pair.Value);
            gainCommands.Clear();
            foreach (var pair in restored.gainCommands)
                gainCommands.Add(pair.Key, pair.Value);
            seen.Clear();
            foreach (var pair in restored.seen)
                seen.Add(pair.Key, pair.Value);
            transitions.Clear();
            foreach (var item in restored.transitions)
                transitions.Enqueue(item);
            Array.Clear(lifecycleKeys, 0, lifecycleKeys.Length);
            Array.Copy(restored.lifecycleKeys, lifecycleKeys, lifecycleKeys.Length);
            lifecycleCount = restored.lifecycleCount;
            eventSequence = restored.eventSequence;
            latestTick = restored.latestTick;
            lastTime = restored.lastTime;
            epochIngressClosed = restored.epochIngressClosed;
            epochAbortedFamily = restored.epochAbortedFamily;
            if (preparedPublication != null)
                preparedPublication.Abort();
            foreach (var reservation in respawnReservations)
                if (reservation != null)
                    reservation.Abort();
            Array.Clear(respawnReservations, 0, respawnReservations.Length);
        }

        // Only used during trusted initial recovery, never supplied by a wire peer.
        string storageRestoreBinding;
        void RequireCommittedRead()
        {
            if (durableWriting)
                throw new HealthStorageException("storage callback cannot observe unpublished Health");
        }

        T StorageCall<T>(Func<T> call)
        {
            if (durableWriting)
                throw new HealthStorageException("reentrant storage operation");
            durableWriting = true;
            try
            {
                return call();
            }
            finally
            {
                durableWriting = false;
            }
        }

        T AtomicHealth<T>(Func<T> operation)
        {
            lock (sync)
            {
                if (durableWriting)
                    throw new HealthStorageException("storage callback cannot mutate Health");
                if (durable == null || durableDepth > 0)
                    return operation();
                if (durable.Failure != null)
                    throw new HealthStorageException(durable.Failure);
                durableDepth++;
                try
                {
                    if (StorageCall(delegate
                    {
                        return durable.Store.Manifest;
                    }) != durable.Manifest)
                        throw new HealthStorageException("storage generation changed; recover current head explicitly");
                    T result = operation();
                    if ((long)damage.Count + gains.Count + gainCommands.Count > durable.MaximumKeys)
                        throw new HealthStorageException("resident receipt-key quota; raise configured quota or end the room explicitly");
                    byte[] next = Checkpoint(StorageContext(), durable.Binding, epochIngressClosed, true);
                    StorageCall(delegate
                    {
                        durable.Store.Commit(next);
                        return true;
                    });
                    durable.Committed = next;
                    durable.Manifest = StorageCall(delegate
                    {
                        return durable.Store.Manifest;
                    });
                    return result;
                }
                catch (Exception error)
                {
                    durable.Failure = error.Message;
                    byte[] committed = durable.Committed;
                    try
                    {
                        string observed = StorageCall(delegate
                        {
                            return durable.Store.Manifest;
                        });
                        NoManifestRollback(durable.Manifest, observed);
                        var onDisk = StorageCall(delegate
                        {
                            return durable.Store.Read();
                        });
                        if (onDisk != null)
                        {
                            RestoreDurableImage(onDisk, durable.MaximumKeys);
                            durable.Committed = onDisk;
                            durable.Manifest = observed;
                            committed = onDisk;
                        }
                    }
                    catch (Exception readError)
                    {
                        durable.Failure += "; recovery required: " + readError.Message;
                    }

                    try
                    {
                        RestoreDurableImage(committed, durable.MaximumKeys);
                    }
                    catch (HealthStorageException)
                    {
                        RestoreDurableImage(durable.Committed, durable.MaximumKeys);
                    }

                    throw new HealthStorageException(durable.Failure, error);
                }
                finally
                {
                    durableDepth--;
                }
            }
        }

        internal void RecoverDurableImages(string expectedManifest, int maximumReceiptKeys = 4096, long residentStateBytes = 67108864)
        {
            lock (sync)
            {
                ValidateResidentBudget(maximumReceiptKeys, residentStateBytes);
                if (durable == null || maximumReceiptKeys < 1 || maximumReceiptKeys > 16384)
                    throw new ArgumentException("durable recovery");
                NoManifestRollback(durable.Manifest, expectedManifest);
                if (StorageCall(delegate
                {
                    return durable.Store.Manifest;
                }) != expectedManifest)
                    throw new HealthStorageException("stale storage checkpoint manifest");
                byte[] image = StorageCall(delegate
                {
                    return durable.Store.Read();
                });
                if (image == null)
                    throw new HealthStorageException("missing committed image");
                RestoreDurableImage(image, maximumReceiptKeys);
                durable.MaximumKeys = maximumReceiptKeys;
                durable.ResidentBytes = residentStateBytes;
                durable.Committed = image;
                durable.Manifest = expectedManifest;
                durable.Failure = null;
            }
        }

        internal void ReopenDurableImages(Func<IHealthImageStore> reopen, string expectedManifest,
            int maximumReceiptKeys = 4096, long residentStateBytes = 67108864)
        {
            lock (sync)
            {
                ValidateResidentBudget(maximumReceiptKeys, residentStateBytes);
                if (durable == null || durable.Failure == null || reopen == null)
                    throw new HealthStorageException("reopen requires a faulted owner and explicit storage factory");
                durable.Store.Dispose();
                durable.Store = reopen();
                RecoverDurableImages(expectedManifest, maximumReceiptKeys, residentStateBytes);
            }
        }

        internal void ReleaseDurableImages()
        {
            lock (sync)
            {
                if (durable == null)
                    return;
                try
                {
                    if (durable.Failure == null)
                        AtomicHealth(delegate
                        {
                            epochIngressClosed = true;
                            if (preparedPublication != null)
                                preparedPublication.Abort();
                            foreach (var r in respawnReservations)
                                if (r != null)
                                    r.Abort();
                            return true;
                        });
                }
                finally
                {
                    durable.Store.Dispose();
                }
            }
        }
    }
}
