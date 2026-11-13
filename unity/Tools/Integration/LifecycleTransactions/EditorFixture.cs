// Coordinator may compile the sealed Scenario + production inputs in its isolated
// Editor probe and invoke this entry under engine_lane.py. This runs real neutral
// modules with explicit auth/root/geometry fixtures; it does not test native motion.
#if UNITY_6000_0_OR_NEWER
public static class LifecycleTransactionsEditorFixture
{
    public static void Run() { Fixture.Main(); }
}
#endif
