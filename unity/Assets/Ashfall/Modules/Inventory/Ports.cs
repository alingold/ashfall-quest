namespace Ashfall.Modules.Inventory
{
    // Owned ports only. Coordinator supplies immutable, reviewed export records.
    public interface IContentIndex
    {
        bool TryResolve(string kind, string sourceId, out ContentRecord record);
        string ContentHash { get; }
    }
    public interface IExportApproval
    {
        bool IsApproved(string kind, string sourceId, string version, string sha256);
    }
}
