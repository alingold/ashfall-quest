using System;
using System.IO;
using System.Security.Cryptography;
using Ashfall.Modules.Persistence;
using Ashfall.Modules.SaveMigration;

namespace Ashfall.Integration.MigrationRuntime
{
    public enum PublicationState { Unresolved, ValidatedNotPublished, CommittedReadbackVerified }
    // A process-local observation capability, not authenticated adoption or a durable journal.
    public sealed class MigrationReceipt
    {
        internal readonly SettingsMigrationRuntime Owner;
        internal readonly MigrationPreview Preview;
        internal readonly CommitState CommitState;
        public bool PublicationAttempted { get; private set; }
        public string ExpectedSha256 { get; private set; }
        internal MigrationReceipt(SettingsMigrationRuntime owner, MigrationPreview preview, bool attempted, string hash, CommitState state)
        { Owner=owner; Preview=preview; PublicationAttempted=attempted; ExpectedSha256=hash; CommitState=state; }
    }
    public sealed class MigrationReconciliation
    {
        public PublicationState State { get; private set; }
        public string Reason { get; private set; }
        internal MigrationReconciliation(PublicationState state,string reason) { State=state; Reason=reason; }
    }
    public sealed partial class SettingsMigrationRuntime
    {
        private static string Digest(byte[] bytes)
        { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""); }
        // Read-only inspection under the real cooperative family lease. Never recover, flush,
        // delete, retry, adopt gameplay settings or infer rollback from an exception.
        public MigrationReconciliation Reconcile(MigrationPreview preview, MigrationReceipt receipt)
        {
            if(preview==null||receipt==null||!Object.ReferenceEquals(preview.Owner,this)||
               !Object.ReferenceEquals(receipt.Owner,this)||!Object.ReferenceEquals(receipt.Preview,preview)||
               preview.State!=MigrationState.Ready||preview.Profile!=Profile)
                return Unknown("Exact runtime, preview and receipt required");
            try
            {
                using(files.AcquireFamily(Destination))
                {
                    // This migration only admits an empty dedicated family. A later backup is
                    // evidence of another operation, even when the primary happens to match.
                    if(files.Exists(Destination+".backup"))return Unknown("Backup present; family changed");
                    if(!files.Exists(Destination))
                    {
                        if(!receipt.PublicationAttempted&&receipt.CommitState==CommitState.IoFailure)
                            return new MigrationReconciliation(PublicationState.ValidatedNotPublished,"Ready preview; no publication call and missing primary/backup at inspection");
                        return Unknown("Missing primary cannot establish history after a publication attempt");
                    }
                    if(!receipt.PublicationAttempted||receipt.ExpectedSha256==null)return Unknown("No publication candidate bound to receipt");
                    string hash=Digest(files.Read(Destination,AtomicSettingsStore.MaxDocumentBytes));
                    if(hash!=receipt.ExpectedSha256)return Unknown("Primary bytes differ from publication candidate");
                    var store=new AtomicSettingsStore(Destination,new LeasedReadFiles(files,Destination));
                    if(!store.Load(false).Accepted||store.State!=SettingsState.Ready)return Unknown("Actual store readback invalid");
                    if(!Equal(store,"semantic.schema","1")||!Equal(store,"semantic.kind",preview.Plan.Kind)||
                       !Equal(store,"semantic.data",preview.SemanticJson)||!Equal(store,"semantic.source",preview.SelectedSource)||
                       (preview.Kind==MigrationKind.Lobby&&!Equal(store,"semantic.source-profile",Profile==SourceProfile.Quest?"quest":"skin-library")))
                        return Unknown("Semantic readback differs from immutable preview");
                    if(files.Exists(Destination+".backup")||Digest(files.Read(Destination,AtomicSettingsStore.MaxDocumentBytes))!=hash)
                        return Unknown("Family changed during inspection");
                    return new MigrationReconciliation(PublicationState.CommittedReadbackVerified,"Exact candidate and semantic archive verified under family lease");
                }
            }
            catch(Exception) { return Unknown("Family inspection failed; publication remains unresolved"); }
        }
        private static bool Equal(AtomicSettingsStore store,string key,string expected)
        { string value;return store.TryRead(key,out value)&&value==expected; }
        private static MigrationReconciliation Unknown(string reason)
        { return new MigrationReconciliation(PublicationState.Unresolved,reason); }
        // Only used inside the already-held exact family lease. Mutations are prohibited.
        private sealed class LeasedReadFiles : IAtomicSettingsFiles
        {
            private readonly IAtomicSettingsFiles inner;private readonly string destination;
            internal LeasedReadFiles(IAtomicSettingsFiles inner,string destination) { this.inner=inner;this.destination=destination; }
            private sealed class Lease : IDisposable { public void Dispose() { } }
            public IDisposable AcquireFamily(string path) { if(path!=destination)throw new IOException("Foreign lease");return new Lease(); }
            public bool Exists(string path) { return inner.Exists(path); }
            public byte[] Read(string path,int bound) { return inner.Read(path,bound); }
            public void CreateDurable(string path,byte[] bytes) { throw new IOException("Read-only reconciliation"); }
            public void Commit(string p,string d,string b,bool r) { throw new IOException("Read-only reconciliation"); }
        }
    }
}
