using System;
using System.Collections.Generic;
using System.IO;
using Ashfall.Modules.Persistence;
using Ashfall.Modules.SaveMigration;

namespace Ashfall.Integration.MigrationRuntime
{
    public enum MigrationKind { Campaign, Lobby, WeaponTiming, ExplosiveTuning }
    public enum CommitState { Committed, Refused, Conflict, IoFailure }
    public sealed class MigrationCommitResult
    {
        public CommitState State { get; private set; }
        public string Reason { get; private set; }
        public MigrationReceipt Receipt { get; internal set; }
        internal MigrationCommitResult(CommitState state, string reason) { State=state; Reason=reason; }
    }
    // No public constructor or mutable plan is exposed across the assembly boundary.
    public sealed class MigrationPreview
    {
        internal readonly SettingsMigrationRuntime Owner;
        internal readonly MigrationPlan Plan;
        private readonly Dictionary<string,byte[]> originals;
        public MigrationKind Kind { get; private set; }
        public SourceProfile Profile { get; private set; }
        public MigrationState State { get { return Plan.State; } }
        public string Reason { get { return Plan.Reason; } }
        public string SemanticJson { get { return Plan.SemanticJson; } }
        public string SelectedSource { get { return Plan.SelectedSource; } }
        public string Destination { get { return Owner.Destination; } }
        public string[] SourcePaths { get { var a=new string[originals.Count]; originals.Keys.CopyTo(a,0); return a; } }
        public byte[] ReadOriginal(string absolutePath)
        { byte[] b; if(!originals.TryGetValue(absolutePath,out b))throw new ArgumentException("Uncaptured source"); return b==null?null:(byte[])b.Clone(); }
        internal MigrationPreview(SettingsMigrationRuntime owner, MigrationKind kind, MigrationPlan plan, Dictionary<string,byte[]> bytes)
        { Owner=owner; Kind=kind; Profile=owner.Profile; Plan=plan; originals=bytes; }
    }
    // Explicitly selected files only. No startup hook, user-directory discovery or gameplay publication.
    // Ports must obey Persistence's exclusive-family and atomic-commit contract; no rollback is invented.
    public sealed partial class SettingsMigrationRuntime
    {
        public string Destination { get; private set; }
        public SourceProfile Profile { get; private set; }
        private readonly IAtomicSettingsFiles files;
        private readonly int sourceBudget;
        public SettingsMigrationRuntime(string destination, SourceProfile profile)
            : this(destination,profile,new PhysicalSettingsFiles(),AtomicSettingsStore.MaxArtifactBytes) { }
        public SettingsMigrationRuntime(string destination, SourceProfile profile, IAtomicSettingsFiles files, int sourceBudget)
        {
            Destination=Absolute(destination);
            if(!Enum.IsDefined(typeof(SourceProfile),profile)||files==null||sourceBudget<1||sourceBudget>AtomicSettingsStore.MaxArtifactBytes)throw new ArgumentException("Supported profile, files and bounded budget required");
            Profile=profile; this.files=files; this.sourceBudget=sourceBudget;
        }
        private static string Absolute(string path)
        { if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||Path.GetPathRoot(path).Length<3)throw new ArgumentException("Explicit absolute path required"); return Path.GetFullPath(path); }
        public MigrationPreview PreviewCampaign(string expanded, string legacy, bool allowSourceRecovery)
        {
            var s=new CapturedSources(sourceBudget); var m=new SemanticMigration(s);
            var p=m.Campaign(Absolute(expanded),String.IsNullOrEmpty(legacy)?null:Absolute(legacy),allowSourceRecovery);
            return new MigrationPreview(this,MigrationKind.Campaign,p,s.Bytes);
        }
        public MigrationPreview PreviewLobby(string source, IMapAvailability approvedMaps)
        {
            var s=new CapturedSources(sourceBudget);
            return new MigrationPreview(this,MigrationKind.Lobby,new SemanticMigration(s).Lobby(Absolute(source),Profile,approvedMaps),s.Bytes);
        }
        public MigrationPreview PreviewTuning(string source, MigrationKind kind)
        {
            if(kind!=MigrationKind.WeaponTiming&&kind!=MigrationKind.ExplosiveTuning)throw new ArgumentException("Explicit tuning kind required");
            var s=new CapturedSources(sourceBudget);
            return new MigrationPreview(this,kind,new SemanticMigration(s).Tuning(Absolute(source),kind==MigrationKind.WeaponTiming),s.Bytes);
        }
        public MigrationCommitResult Commit(MigrationPreview preview)
        {
            if(preview==null||!Object.ReferenceEquals(preview.Owner,this)||preview.Profile!=Profile||preview.State!=MigrationState.Ready)
                return new MigrationCommitResult(CommitState.Refused,"Ready preview from this fixed-profile runtime required");
            var observed=new ObservedFiles(files,Destination);
            var store=new AtomicSettingsStore(Destination,observed);
            MigrationCommitResult outcome;
            try
            {
                var result=SemanticMigration.Commit(preview.Plan,store);
                outcome=new MigrationCommitResult(result.Accepted?CommitState.Committed:observed.Failed?CommitState.IoFailure:observed.Occupied?CommitState.Conflict:CommitState.Refused,result.Reason);
            }
            // Accepted plans may hold a base64 archive larger than the accepted JSON parser
            // bound. That parser runs before destination access; report refusal, never retry
            // by truncating originals or omitting unknown source data.
            catch(FormatException) { outcome=new MigrationCommitResult(CommitState.Refused,"Accepted plan archive exceeds format/budget constraints"); }
            outcome.Receipt=new MigrationReceipt(this,preview,observed.PublicationAttempted,observed.ExpectedHash,outcome.State);
            return outcome;
        }
        private sealed class CapturedSources : IMigrationSources
        {
            internal readonly Dictionary<string,byte[]> Bytes=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
            private int remaining;
            internal CapturedSources(int budget) { remaining=budget; }
            public byte[] ReadIfPresent(string path,int maxBytes)
            {
                byte[] b;
                if(!Bytes.TryGetValue(path,out b))
                {
                    b=new PhysicalMigrationSources().ReadIfPresent(path,Math.Min(maxBytes,remaining));
                    if(b!=null)remaining-=b.Length;
                    Bytes.Add(path,b);
                }
                return b==null?null:(byte[])b.Clone();
            }
        }
        private sealed class ObservedFiles : IAtomicSettingsFiles
        {
            private readonly IAtomicSettingsFiles inner; private readonly string destination;
            internal bool Failed,Occupied;
            internal bool PublicationAttempted;
            internal string ExpectedHash;
            internal ObservedFiles(IAtomicSettingsFiles inner,string destination) { this.inner=inner; this.destination=destination; }
            public IDisposable AcquireFamily(string p) { try{return inner.AcquireFamily(p);}catch{Failed=true;throw;} }
            public bool Exists(string p) { try{bool b=inner.Exists(p);if(b&&(p.Equals(destination,StringComparison.OrdinalIgnoreCase)||p.Equals(destination+".backup",StringComparison.OrdinalIgnoreCase)))Occupied=true;return b;}catch{Failed=true;throw;} }
            public byte[] Read(string p,int n) { try{return inner.Read(p,n);}catch{Failed=true;throw;} }
            public void CreateDurable(string p,byte[] b) { ExpectedHash=Digest(b);try{inner.CreateDurable(p,b);}catch{Failed=true;throw;} }
            public void Commit(string p,string d,string b,bool r) { PublicationAttempted=true;try{inner.Commit(p,d,b,r);}catch{Failed=true;throw;} }
        }
    }
}
