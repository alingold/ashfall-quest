#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Core;
namespace Ashfall.Integration.RuntimeLoop.Engine
{
    // Staged scheduler only. Coordinator injects approved world/actor/root ports and
    // Input System 1.20.1 desktop samples; no scene/bootstrap/camera/collider ownership.
    public interface IDesktopHostFrames { ActorFrame[] Capture(long tick); }
    public sealed class UnityHostLoopAdapter : MonoBehaviour
    {
        HostScheduler scheduler;
        public event Action<RuleResult,ActorResult[]> Completed;
        public ScheduleReceipt LastReceipt {get;private set;}
        public void Bind(HostLoop value,IDesktopHostFrames source,string sessionId,string hostActor,long authorityEpoch,long nextTick)
        {var attested=source as IHostFrameSource;if(attested==null)throw new ArgumentException("source lifetime binding required");BindSource(value,attested,sessionId,hostActor,authorityEpoch,nextTick);}
        public void BindSource(HostLoop value,IHostFrameSource source,string sessionId,string hostActor,long authorityEpoch,long nextTick)
        {if(scheduler!=null)throw new InvalidOperationException("scheduler already owned");scheduler=new HostScheduler(value,source,sessionId,hostActor,authorityEpoch,nextTick);}
        void FixedUpdate(){RunScheduled(Time.fixedDeltaTime);}
        // Callable by the coordinator's serialized native fixture. No engine invocation here.
        public RuleResult RunScheduled(float dt)
        {
            if(scheduler==null)return RuleResult.Reject("scheduler unbound");
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0||dt>.1f)return RuleResult.Reject("scheduler delta");
            LastReceipt=scheduler.Run(dt);
            if(Completed!=null)Completed(LastReceipt.Result,LastReceipt.Actors);
            return LastReceipt.Result;
        }
    }
}
#endif
