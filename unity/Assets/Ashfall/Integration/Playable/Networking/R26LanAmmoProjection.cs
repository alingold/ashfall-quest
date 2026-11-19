using System;
namespace Ashfall.Integration.Playable.Networking
{
    // WPN-CX4-SEMI: decides when an authoritative ammo row may overwrite the local
    // magazine. ReconcileNetworkMagazine suspends the owner and forces a trigger
    // release, so it must never run against a row that predates the newest local
    // shot/reload (the host's own rows refresh only every 100 ms and were never
    // fenced), nor while a held trigger is still firing. A real correction (a host
    // rejection, a pickup refill) is applied once the trigger has settled.
    public static class R26LanAmmoProjection
    {
        // Longer than the slowest automatic interval (M1014 0.29 s) so a held burst never settles.
        public const double SettleSeconds=.35;
        public static bool ShouldApply(R24LanAmmoState prior,R24LanAmmoState state,long prediction,double sinceLocalVolley)
        {
            if(state==null||state.Acknowledged<prediction||Double.IsNaN(sinceLocalVolley)||sinceLocalVolley<SettleSeconds)return false;
            return prior==null||prior.Epoch!=state.Epoch||prior.Acknowledged<prediction||prior.Loaded!=state.Loaded||prior.Left!=state.Left||prior.Right!=state.Right||prior.Reserve!=state.Reserve;
        }
    }
}
