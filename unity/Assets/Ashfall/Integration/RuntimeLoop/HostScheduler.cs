using System;
using Ashfall.Core;
namespace Ashfall.Integration.RuntimeLoop
{
    // Root supplies one host-lane source. Identity is an opaque local lifetime,
    // never a client generation or a production-content approval.
    public interface IHostFrameSource
    {
        object SourceIdentity { get; }
        HostFrameBatch Capture(long tick);
    }
    public sealed class HostFrameBatch
    {
        public readonly object SourceIdentity;
        public readonly long Tick, Sequence;
        readonly ActorFrame[] frames;
        public HostFrameBatch(object identity,long tick,long sequence,ActorFrame[] values)
        {SourceIdentity=identity;Tick=tick;Sequence=sequence;frames=values==null?null:(ActorFrame[])values.Clone();}
        internal ActorFrame[] Copy(){return frames==null?null:(ActorFrame[])frames.Clone();}
    }
    public enum ScheduleDisposition { Rejected, Draining, SourceRetired, CommittedPending, Committed, CommittedWithActionFailures }
    public sealed class ScheduleReceipt
    {
        public readonly ScheduleDisposition Disposition;
        public readonly RuleResult Result;
        public readonly long Tick;
        public readonly ActorResult[] Actors;
        internal ScheduleReceipt(ScheduleDisposition disposition,RuleResult result,long tick,ActorResult[] actors)
        {Disposition=disposition;Result=result;Tick=tick;Actors=actors??new ActorResult[0];}
    }
    // Explicit root-owned scheduler candidate: no static activation, scene or second
    // root writer. Capture/read ports are pure, stable, nonthrowing on this lane.
    public sealed class HostScheduler
    {
        readonly HostLoop loop;readonly IHostFrameSource source;readonly object identity;
        readonly string session,host;readonly long epoch;
        long tick,sequence;bool running,retired;
        public long CommittedTick {get{return tick;}}
        public HostScheduler(HostLoop loop,IHostFrameSource source,string session,string host,long epoch,long nextTick)
        {
            if(loop==null||source==null||source.SourceIdentity==null||String.IsNullOrEmpty(session)||String.IsNullOrEmpty(host)||epoch<0||nextTick<1)
                throw new ArgumentException("scheduler binding");
            this.loop=loop;this.source=source;identity=source.SourceIdentity;
            this.session=session;this.host=host;this.epoch=epoch;tick=nextTick-1;
        }
        ScheduleReceipt Receipt(ScheduleDisposition state,RuleResult result,ActorResult[] actors=null)
        {return new ScheduleReceipt(state,result,tick,actors);}
        public ScheduleReceipt Run(float dt)
        {
            if(running)return Receipt(ScheduleDisposition.Rejected,RuleResult.Reject("reentrant scheduler"));
            if(Single.IsNaN(dt)||Single.IsInfinity(dt)||dt<=0||dt>.1f)
                return Receipt(ScheduleDisposition.Rejected,RuleResult.Reject("scheduler delta"));
            running=true;
            try {
                // Delivery is independent of input lifetime, load state and connection.
                // Never let a disconnected source strand already committed Health.
                var drained=loop.Drain();if(!drained.Accepted)return Receipt(ScheduleDisposition.Draining,drained);
                if(retired||!Object.ReferenceEquals(identity,source.SourceIdentity)){
                    retired=true;return Receipt(ScheduleDisposition.SourceRetired,RuleResult.Reject("source lifetime retired"));}
                if(tick==Int64.MaxValue)return Receipt(ScheduleDisposition.Rejected,RuleResult.Reject("scheduler tick exhausted"));
                long next=tick+1;var batch=source.Capture(next);
                // Recheck after capture: disable/re-enable or replacement during sampling
                // cannot lend an old sample authority over a replacement source.
                if(!Object.ReferenceEquals(identity,source.SourceIdentity)){
                    retired=true;return Receipt(ScheduleDisposition.SourceRetired,RuleResult.Reject("source changed during capture"));}
                if(batch==null||!Object.ReferenceEquals(identity,batch.SourceIdentity)||batch.Tick!=next||batch.Sequence<=sequence)
                    return Receipt(ScheduleDisposition.Rejected,RuleResult.Reject("stale source batch"));
                ActorResult[] actors;var result=loop.Tick(new CommandContext(session,host,"scheduler/"+next,next,epoch,true),dt,batch.Copy(),out actors);
                if(loop.LastDisposition==TickDisposition.RejectedBeforeEffects)return Receipt(ScheduleDisposition.Rejected,result,actors);
                tick=next;sequence=batch.Sequence;
                if(loop.LastDisposition==TickDisposition.CommittedPending)return Receipt(ScheduleDisposition.CommittedPending,result,actors);
                bool failed=false;foreach(var actor in actors)if(!actor.Motion.Accepted||!actor.Action.Accepted)failed=true;
                return Receipt(failed?ScheduleDisposition.CommittedWithActionFailures:ScheduleDisposition.Committed,result,actors);
            }finally{running=false;}
        }
    }
}
