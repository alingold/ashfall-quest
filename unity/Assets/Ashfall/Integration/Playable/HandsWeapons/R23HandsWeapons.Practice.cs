#if UNITY_6000_0_OR_NEWER
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        public void TickSourcePracticeTargets(float dt)
        {
            if(round==null)return;round.ExternalPracticeClock=true;round.AdvancePracticeTargets(dt);
            foreach(var target in targets)if(target!=null&&target.gameObject.activeInHierarchy)target.Refresh(round);
        }
    }
}
#endif
