#if UNITY_6000_0_OR_NEWER
using Ashfall.Integration.Playable.HandsWeapons;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanSession
    {
        double warmupVolley=double.NegativeInfinity;
        void OnDisable(){R23GunFeedback.PlayerDamageOff=false;if(root!=null&&root.UI!=null)root.UI.SetWarmupExploration(false);}
        void SyncWarmupFeedback()
        {
            bool warmup=R26WarmupFeedback.DamageOff(room.Stage);R23GunFeedback.PlayerDamageOff=warmup;
            if(root==null||root.UI==null)return;root.UI.SetWarmupExploration(warmup);
            if(warmup&&warmupVolley!=lastLocalVolley)root.UI.NotifyWarmupShot();warmupVolley=lastLocalVolley;
        }
    }
}
#endif
