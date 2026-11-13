#if UNITY_6000_0_OR_NEWER
using System;
using UnityEngine;
using Ashfall.Core;
using Ashfall.Integration.RuntimeLoop;
using Ashfall.Integration.RuntimeLoop.Engine;
// Coordinator calls under engine_lane.py with actual approved ports. No scene/bootstrap changes.
public static class RuntimeLoopNativeFixture
{
    // Root may exercise failure/retirement/pressure with its real immutable ports.
    // Result.Accepted alone cannot attest that all actor actions succeeded.
    public static ScheduleReceipt RunExpected(HostLoop loop,IHostFrameSource frames,string session,string host,long epoch,long tick,ScheduleDisposition expected)
    {
        var owner=new GameObject("RuntimeLoopR4-disposition-fixture");
        try {
            var adapter=owner.AddComponent<UnityHostLoopAdapter>();adapter.enabled=false;
            adapter.BindSource(loop,frames,session,host,epoch,tick);
            adapter.RunScheduled(.02f);
            if(adapter.LastReceipt==null||adapter.LastReceipt.Disposition!=expected)throw new Exception("unexpected native scheduler disposition");
            bool refused=false;try{adapter.BindSource(loop,frames,session,host,epoch,tick);}catch(InvalidOperationException){refused=true;}
            if(!refused)throw new Exception("native scheduler replaced owner");
            return adapter.LastReceipt;
        }finally{UnityEngine.Object.DestroyImmediate(owner);}
    }
    public static void Run(HostLoop loop,IHostFrameSource frames,string session,string host,long epoch,long tick)
    {
        var owner=new GameObject("RuntimeLoopR2-fixture");
        try {
            var adapter=owner.AddComponent<UnityHostLoopAdapter>();adapter.enabled=false;
            adapter.BindSource(loop,frames,session,host,epoch,tick);
            int callbacks=0;adapter.Completed+=delegate(RuleResult r,ActorResult[] batch){callbacks++;};
            var invalid=adapter.RunScheduled(float.NaN);if(invalid.Accepted||callbacks!=0)throw new Exception("invalid scheduler delta");
            var result=adapter.RunScheduled(.02f);
            if(!result.Accepted||callbacks!=1||loop.LastDisposition!=TickDisposition.Committed||adapter.LastReceipt.Disposition!=ScheduleDisposition.Committed)throw new Exception("native scheduler requires fully bound approved fixture ports and successful actor actions");
        }finally{UnityEngine.Object.DestroyImmediate(owner);}
    }
}
#endif
