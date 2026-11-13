#if UNITY_6000_0_OR_NEWER
// Coordinator-only execution through engine_lane.py. This invokes real neutral
// Inventory/SessionWeapons/lifetime contracts inside the Editor, using explicit
// auth/geometry fixtures. It does NOT prove native geometry, rendering or gameplay.
public static class WorldWeaponNativeFixture
{
    public static void ExecuteManagedContractFixture()
    {
        WorldTests.Run();
        UnityEngine.Debug.Log("WeaponWorldTransfersR2 managed contract fixture completed; native geometry and runtime gates OPEN");
    }
}
#endif
