using System.Collections.Generic;
namespace Ashfall.Modules.Avatar
{
    // Owned boundary. Dependencies are already receipt-bound VerifiedAssets, never paths
    // resolved by a renderer. URI keys are exact glTF strings; external IO is forbidden.
    public interface ISkinDependencies { string ExpectedClosureSha256 { get; } VerifiedAsset Resolve(string exactUri); }
    public interface ISkinPose { float[][] CopyLocalMatrices(); }
}

