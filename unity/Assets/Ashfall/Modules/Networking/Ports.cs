using Ashfall.Core;
namespace Ashfall.Modules.Networking
{
    // Coordinator supplies semantic validation and mutation; payload is an opaque versioned module message.
    // Called only on the simulation thread after transport identity and replay validation.
    public interface ICommandSink { RuleResult Apply(CommandContext context, string featureId, byte[] payload); }
    public interface IReplicationSink { RuleResult Apply(bool fullSnapshot, long sequence, byte[] payload); }
    public interface IPumpedTransport : INetworkTransport { void Pump(); }
}
