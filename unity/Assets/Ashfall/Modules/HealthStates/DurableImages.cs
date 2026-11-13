using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace Ashfall.Modules.HealthStates
{
    public interface IHealthImageStore : IDisposable
    {
        byte[] Read();
        void Commit(byte[] image);
        string Manifest { get; }
    }

    public sealed class HealthStorageException : InvalidOperationException
    {
        public HealthStorageException(string reason) : base("Health storage unavailable: " + reason)
        {
        }

        public HealthStorageException(string reason, Exception inner) : base("Health storage unavailable: " + reason, inner)
        {
        }
    }

    // Two managed image slots and an atomic head. Every image retains ALL outcomes.
    // No log segments/archives are automatically accumulated or historical data deleted.
    public sealed class FileHealthImageStore : IHealthImageStore
    {
        readonly string root, dir, identity;
        readonly long quota;
        readonly int imageLimit;
        FileStream lease;
        bool disposed;
        public Action<string> FaultInjection; // CPU fault harness; never bound by native composition.
        public const int DefaultImageBytes = 2097152;
        public const long DefaultDiskBytes = 67108864;
        public static string Digest(byte[] bytes)
        {
            using (var h = SHA256.Create())
                return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        public FileHealthImageStore(string directory, string session, long epoch, string build,
            long diskBytes = DefaultDiskBytes, int maximumImageBytes = DefaultImageBytes, int maximumEpochs = 128)
        {
            if (String.IsNullOrEmpty(session) || session.Length > 128 || epoch < 0 ||
                build == null || build.Length != 64 || diskBytes < 4096 || maximumImageBytes < 256 ||
                maximumImageBytes > DefaultImageBytes || maximumEpochs < 1)
                throw new ArgumentException("storage configuration");
            root = Path.GetFullPath(directory);
            quota = diskBytes;
            imageLimit = maximumImageBytes;
            identity = "ashfall-health-image/1\n" + session + "\n" + epoch.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n" + build;
            Directory.CreateDirectory(root);
            RejectLinks(root);
            dir = Path.Combine(root, Digest(Encoding.UTF8.GetBytes(identity)));
            using (var quotaLease = new FileStream(Path.Combine(root, "quota.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (!Directory.Exists(dir) && Directory.GetDirectories(root).Length >= maximumEpochs)
                    throw new HealthStorageException("epoch directory quota; retain or archive explicitly");
                bool existed = Directory.Exists(dir);
                Directory.CreateDirectory(dir);
                RejectLinks(dir);
                try
                {
                    lease = new FileStream(Path.Combine(dir, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    string marker = Path.Combine(dir, "identity");
                    if (existed && !File.Exists(marker))
                        throw new InvalidDataException("missing storage identity");
                    if (File.Exists(marker))
                    {
                        if (new FileInfo(marker).Length > 2048 || File.ReadAllText(marker) != identity)
                            throw new InvalidDataException("storage identity mismatch");
                    }
                    else
                    {
                        CheckQuota(Encoding.UTF8.GetByteCount(identity));
                        WriteNew(marker, Encoding.UTF8.GetBytes(identity));
                    }

                    Read();
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }
        }

        static void RejectLinks(string path)
        {
            if ((new DirectoryInfo(path).Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("storage reparse point");
        }

        void Check()
        {
            if (disposed)
                throw new ObjectDisposedException("FileHealthImageStore");
        }

        System.Collections.Generic.IEnumerable<string> QuotaFiles()
        {
            foreach (string path in Directory.GetFiles(root))
                yield return path;
            foreach (string directory in Directory.GetDirectories(root))
            {
                RejectLinks(directory);
                if (Directory.GetDirectories(directory).Length != 0)
                    throw new InvalidDataException("unexpected nested storage directory");
                foreach (string path in Directory.GetFiles(directory))
                    yield return path;
            }
        }

        void CheckQuota(long additional)
        {
            long size = 0;
            foreach (string path in QuotaFiles())
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("storage link");
                size = checked(size + new FileInfo(path).Length);
                if (size > quota)
                    throw new HealthStorageException("disk quota; no data deleted");
            }

            if (additional > quota - size)
                throw new HealthStorageException("disk quota; no data deleted");
        }

        static void WriteNew(string path, byte[] data)
        {
            using (var f = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                f.Write(data, 0, data.Length);
                f.Flush(true);
            }
        }

        static void Replace(string temporary, string target)
        {
            if (File.Exists(target))
                File.Replace(temporary, target, null);
            else
                File.Move(temporary, target);
        }

        sealed class Head
        {
            internal long Generation;
            internal int Slot;
            internal string Hash;
        }

        Head ReadHead()
        {
            string path = Path.Combine(dir, "head");
            if (!File.Exists(path))
                return null;
            if (new FileInfo(path).Length > 2048)
                throw new InvalidDataException("head size");
            byte[] raw = File.ReadAllBytes(path);
            if (raw.Length < 32)
                throw new InvalidDataException("head checksum");
            byte[] payload = new byte[raw.Length - 32], sum = new byte[32];
            Array.Copy(raw, payload, payload.Length);
            Array.Copy(raw, payload.Length, sum, 0, 32);
            if (Digest(payload) != BitConverter.ToString(sum).Replace("-", "").ToLowerInvariant())
                throw new InvalidDataException("head checksum");
            using (var m = new MemoryStream(payload, false))
            using (var r = new BinaryReader(m, Encoding.UTF8))
            {
                if (r.ReadInt32() != 1 || r.ReadString() != Digest(Encoding.UTF8.GetBytes(identity)))
                    throw new InvalidDataException("head identity");
                var h = new Head
                {
                    Generation = r.ReadInt64(),
                    Slot = r.ReadByte(),
                    Hash = r.ReadString()
                };
                if (h.Generation < 1 || h.Slot > 1 || h.Hash.Length != 64 || m.Position != m.Length)
                    throw new InvalidDataException("head format");
                return h;
            }
        }

        public byte[] Read()
        {
            Check();
            var h = ReadHead();
            if (h == null)
            {
                if (File.Exists(Path.Combine(dir, "image0")) || File.Exists(Path.Combine(dir, "image1")))
                    throw new InvalidDataException("image without committed head");
                return null;
            }

            string path = Path.Combine(dir, "image" + h.Slot);
            if (!File.Exists(path) || new FileInfo(path).Length > imageLimit)
                throw new InvalidDataException("image size/missing");
            var data = File.ReadAllBytes(path);
            if (data.Length == 0 || Digest(data) != h.Hash)
                throw new InvalidDataException("image checksum");
            return data;
        }

        public string Manifest
        {
            get
            {
                Check();
                var h = ReadHead();
                return h == null ? "empty" : Digest(Encoding.UTF8.GetBytes(identity)) + ":" +
                    h.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + h.Hash;
            }
        }

        public void RequireManifest(string manifest)
        {
            if (manifest != Manifest)
                throw new InvalidDataException("stale storage checkpoint manifest");
            Read();
        }

        void Inject(string point)
        {
            if (FaultInjection != null)
                FaultInjection(point);
        }

        public void Commit(byte[] image)
        {
            Check();
            using (var quotaLease = new FileStream(Path.Combine(root, "quota.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (image == null || image.Length < 1 || image.Length > imageLimit)
                    throw new HealthStorageException("image quota");
                Read();
                var old = ReadHead();
                int slot = old == null ? 0 : 1 - old.Slot;
                long generation = old == null ? 1 : checked(old.Generation + 1);
                string pending = Path.Combine(dir, "image.pending"), headPending = Path.Combine(dir, "head.pending");
                if (File.Exists(pending) || File.Exists(headPending))
                    throw new HealthStorageException("incomplete write; inspect and explicitly quarantine pending files");
                CheckQuota(image.Length + 2048);
                Inject("before-image");
                WriteNew(pending, image);
                Inject("image-flushed");
                Replace(pending, Path.Combine(dir, "image" + slot));
                Inject("image-installed");
                byte[] head;
                using (var m = new MemoryStream())
                using (var w = new BinaryWriter(m, Encoding.UTF8))
                {
                    w.Write(1);
                    w.Write(Digest(Encoding.UTF8.GetBytes(identity)));
                    w.Write(generation);
                    w.Write((byte)slot);
                    w.Write(Digest(image));
                    w.Flush();
                    var payload = m.ToArray();
                    using (var hash = SHA256.Create())
                    {
                        var sum = hash.ComputeHash(payload);
                        head = new byte[payload.Length + sum.Length];
                        Array.Copy(payload, head, payload.Length);
                        Array.Copy(sum, 0, head, payload.Length, sum.Length);
                    }
                }

                WriteNew(headPending, head);
                Inject("head-flushed");
                Replace(headPending, Path.Combine(dir, "head"));
                Inject("head-installed");
            }
        }

        public string DirectoryPath
        {
            get
            {
                return dir;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (lease != null)
            {
                lease.Dispose();
                lease = null;
            }
        }
    }
}
