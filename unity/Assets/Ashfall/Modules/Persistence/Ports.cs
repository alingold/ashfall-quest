namespace Ashfall.Modules.Persistence
{
    // All cooperative save-family mutations use the same canonical destination identity.
    // AcquireFamily must exclusively serialize across instances/processes through commit,
    // fail explicitly on contention, and release on Dispose. Never delete the lock file.
    // Aliased paths/hardlinks and writers ignoring this protocol are unsupported.
    // Never emulate atomic replacement by delete/move.
    public interface IAtomicSettingsFiles
    {
        System.IDisposable AcquireFamily(string destination);
        bool Exists(string path);
        // Reject oversized files before allocating their contents.
        byte[] Read(string path, int maxBytes);
        void CreateDurable(string path, byte[] bytes);
        void Commit(string pending, string destination, string backup, bool replacing);
    }
    public enum SettingsState { Missing, Ready, Corrupt, NewerSchema, IoError, Recovered }
}
