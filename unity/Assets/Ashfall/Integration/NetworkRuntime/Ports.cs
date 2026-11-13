using Ashfall.Core;
namespace Ashfall.Integration.NetworkRuntime
{
    // Trusted serialized host lane only. A successful send takes ownership of bytes;
    // failure MUST mean no send was queued. No authentication is inferred from UDP.
    public interface IFrameSender { RuleResult TrySend(byte[] bytes); }
    public enum ChannelState { Unbound, Active, Closing, Closed }
}
