#if UNITY_6000_0_OR_NEWER
using UnityEngine;
using Ashfall.Integration.Playable.Expansion;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        float gloveRecoveryClock=-1;
        readonly string[] gloveHiddenReason=new string[2];
        void SetTrackedGloveVisible(Transform glove,bool tracked,int hand)
        {
            if(glove==null)return;
            if(glove.gameObject.activeSelf&&!tracked)Debug.Log("R26 hands hidden reason=tracking-unavailable hand="+hand);
            if(!glove.gameObject.activeSelf&&tracked)Debug.Log("R26 hands restored reason=tracking-returned hand="+hand);
            glove.gameObject.SetActive(tracked);
        }
        void RefreshHandsVisibility()
        {
            // The before-render pass follows LateUpdate at the same frame time.
            // Do not borrow its already-consumed obstruction clock for clear time.
            float now=Time.unscaledTime;
            float dt=gloveRecoveryClock<0?0:Mathf.Clamp(now-gloveRecoveryClock,0,.25f);
            gloveRecoveryClock=now;
            bool enabled=R25Features.ObstructedWeaponPresentation&&head!=null;
            StepGlove(leftGloveLatch,leftVisual,enabled,dt);
            StepGlove(rightGloveLatch,rightVisual,enabled,dt);
            LogHandsHiddenState();
        }
        void LogHandsHiddenState()
        {
            bool faded=ViewObstructed!=null&&ViewObstructed();
            LogGloveVisibility(0,leftVisual,faded,leftGloveLatch.Hidden);
            LogGloveVisibility(1,rightVisual,faded,rightGloveLatch.Hidden);
        }
        void LogGloveVisibility(int hand,Transform glove,bool faded,bool blocked)
        {
            string reason=glove==null?"uninitialized":!glove.gameObject.activeInHierarchy?"tracking-or-parent-inactive":faded?"head-view-obstructed":blocked?"hand-behind-world":null;
            if(reason==gloveHiddenReason[hand])return;
            if(reason!=null)Debug.Log("R26 hands hidden reason="+reason+" hand="+hand+" paused="+gameplayPaused+" focused="+hadFocus);
            else Debug.Log("R26 hands restored reason=clear hand="+hand);
            gloveHiddenReason[hand]=reason;
        }
    }
}
#endif
