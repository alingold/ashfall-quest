#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
public static class PickupSupplyNativeFixture
{
    // Compile-only leaf artifact; invocation reserved to the serialized root lane.
    public static void Run()
    {
        if(SupplyTests.Main()!=0||ActualSupplyOwnerTests.Main()!=0)throw new Exception("prepared supply CPU behavior");
        ConstructionOwnerNativeFixture.Run();
    }
}
#endif
