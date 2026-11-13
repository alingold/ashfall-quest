using System;
namespace Ashfall.Modules.Avatar
{
    // These ports belong to Avatar; shared contracts remain frozen.
    public interface IAvatarAssetBytes { byte[] Read(string absolutePath); }
    // Coordinator supplies an immutable acceptance ledger. A manifest cannot approve itself.
    public interface IAvatarApproval { bool IsApproved(string manifestSha256, string receipt); }
    public enum AvatarUse { Production, UnreviewedPilot }
    public sealed class AvatarValidationException : Exception
    { public AvatarValidationException(string message) : base(message) { } }
}
