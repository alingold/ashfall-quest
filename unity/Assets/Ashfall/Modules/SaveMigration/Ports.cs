using System;
using System.Collections.Generic;
namespace Ashfall.Modules.SaveMigration
{
    // Caller supplies explicit paths; source access has no mutation capability.
    public interface IMigrationSources { byte[] ReadIfPresent(string absolutePath, int maxBytes); }
    // Installation/playability is supplied centrally, never inferred from a saved map ID.
    public interface IMapAvailability { bool IsPlayable(string exactMapId); }
    public enum SourceProfile { Quest, SkinLibrary }
    public enum MigrationState { Ready, Missing, Invalid, Blocked }
    public sealed class MigrationPlan
    {
        // A public empty plan must never masquerade as enum-zero Ready.
        public MigrationPlan() { State=MigrationState.Invalid; }
        public MigrationState State { get; internal set; }
        public string Reason { get; internal set; }
        public string Kind { get; internal set; }
        public string SelectedSource { get; internal set; }
        public SourceProfile? Profile { get; internal set; }
        internal string Data, Originals;
        public string SemanticJson { get { return Data; } }
    }
}
