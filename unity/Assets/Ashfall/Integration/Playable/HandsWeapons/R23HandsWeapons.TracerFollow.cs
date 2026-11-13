#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
namespace Ashfall.Integration.Playable.HandsWeapons
{
    public sealed partial class R23HandsWeapons
    {
        struct FollowedTrace {public string Action;public int PhysicalHand;public float Until;}
        readonly Dictionary<LineRenderer,FollowedTrace> followedTraces=new Dictionary<LineRenderer,FollowedTrace>();
        void PresentLocalTracer(LineRenderer line,Vector3 origin,Vector3 acceptedEnd,int physicalHand=-1)
        {
            line.SetPosition(0,origin);line.SetPosition(1,acceptedEnd);line.enabled=true;
            if(IsAwp&&awpView!=null&&awpView.Scope!=null)awpView.Scope.BindTracer(line);
            followedTraces[line]=new FollowedTrace{Action=activeActionId,PhysicalHand=physicalHand,Until=Time.unscaledTime+R23WeaponFeedbackState.TracerSeconds};
        }
        // Source main._update_effect_pool / tracer_follow_090: cosmetic near end
        // follows current emitter after tracked/recoil presentation; world hit
        // remains the accepted shot endpoint. No raycast or gameplay mutation.
        void RefreshLocalTracers()
        {
            foreach(var entry in followedTraces)
            {
                var line=entry.Key;var trace=entry.Value;if(line==null)continue;
                if(Time.unscaledTime>=trace.Until||!FeedbackAllowed||trace.Action!=activeActionId){line.enabled=false;continue;}
                if(!line.enabled)continue;
                R23ShotPose pose=displayedShot;
                bool valid=trace.PhysicalHand<0?!IsDual&&displayedPoseValid:TryReadDualShotPose(trace.PhysicalHand,out pose);
                if(trace.PhysicalHand<0)pose=displayedShot;
                if(!valid){line.enabled=false;continue;}
                line.SetPosition(0,From(pose.Origin));
            }
        }
    }
}
#endif
