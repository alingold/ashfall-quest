#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER && ASHFALL_BINDING_RETIREMENT_API
using System;
namespace Ashfall.NetworkRuntimeFixtures
{
    // Original bytes retained in NetworkNativeLifecycleR3/inputs. A callback is
    // incapable of proving a native allocation census, so this API is retired.
    public static class NativeFixtureR2
    {
        [Obsolete("R2 callback cannot attest native census. Use NativeFixtureR3.Run; global census remains UNKNOWN.",true)]
        public static void Run(Action<string> nativeCensusGate)
        { throw new NotSupportedException("R2-census-callback-disabled"); }
    }
}
#endif
