using System;
using System.Collections.Generic;
using System.Threading;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Presentation;
namespace Ashfall.Integration.PersonalSettings
{
    public interface ITargetFeedbackPort
    { void Emit(string target,float amount,bool shieldHit); }

    // Trusted host/main-thread fanout only. Observation is NOT attribution. The host
    // must retain the exact published transition object until attribution completes.
    // HealthTransition contains no attacker; no client may call the authorized port.
    public sealed class TargetFeedbackBinding
    {
        private sealed class Observed
        {
            public HealthTransition Change;
            public HealthSnapshot Before;
            public bool Authorized,Consumed,Hidden;
            public object ResetIdentity;
        }
        public const int HistoryCapacity=256;
        private readonly string session,target;
        private readonly long epoch;
        private readonly HudModel hud;
        private readonly ITargetFeedbackPort output;
        private readonly int thread=Thread.CurrentThread.ManagedThreadId;
        private readonly SortedDictionary<long,Observed> events=new SortedDictionary<long,Observed>();
        private HealthSnapshot baseline,latest;
        private long retired=-1;
        private bool active;
        public TargetFeedbackBinding(HealthService health,string session,long epoch,string target,HudModel hud,ITargetFeedbackPort output)
        {
            if(health==null||String.IsNullOrEmpty(session)||String.IsNullOrEmpty(target)||epoch<0||hud==null||output==null)throw new ArgumentException("target feedback ports");
            this.session=session;this.epoch=epoch;this.target=target;this.hud=hud;this.output=output;baseline=latest=health.Read(target);
        }
        // Fan out EVERY transition, including foreign damage, gains and lifecycle.
        public bool ObserveTransition(HealthTransition change) { return Receive(change,null,false); }
        // For a transient missing observation, the trusted producer can supply the
        // actual pre-mutation Read captured on its host lane. Never use current Read
        // after publication, or fabricate a before snapshot from accumulated loss.
        public bool ObserveTransition(HealthTransition change,HealthSnapshot exactBefore)
        { return Receive(change,exactBefore,false); }
        public void OnAuthorizedOutgoingTransition(HealthTransition change)
        { Receive(change,null,true); }
        private bool Receive(HealthTransition change,HealthSnapshot before,bool authorize)
        {
            if(Thread.CurrentThread.ManagedThreadId!=thread)throw new InvalidOperationException("Host main-thread fanout required");
            if(active)return false; // callback reentry cannot alter an enumerated batch
            if(change==null||change.After==null||change.SessionId!=session||change.AuthorityEpoch!=epoch||change.After.Id!=target||change.Sequence<=retired||change.BeforeRevision==Int64.MaxValue||change.After.Revision!=change.BeforeRevision+1)return false;
            if(before!=null&&(before.Id!=target||before.Revision!=change.BeforeRevision))return false;
            active=true;
            try {
                Observed item;
                if(events.TryGetValue(change.Sequence,out item)) {
                    // Different receipt at the same sequence cannot steal authorization.
                    if(!Object.ReferenceEquals(item.Change,change))return false;
                    if(before!=null&&item.Before!=null&&!Same(before,item.Before))return false;
                    if(item.Before==null)item.Before=before;
                } else {
                    item=new Observed{Change=change,Before=before,Hidden=!hud.Visible,ResetIdentity=hud.TransientResetIdentity};events.Add(change.Sequence,item);
                    if(latest==null||change.After.Revision>latest.Revision)latest=change.After;
                }
                item.Authorized|=authorize;
                if(authorize&&!hud.Visible)item.Hidden=true;
                // Resolve exact before revisions independently of arrival/authorization
                // order. Unresolved gaps stay pending within the bounded history.
                foreach(var pair in events) {
                    var pending=pair.Value;
                    var known=FindBefore(pending.Change.BeforeRevision);
                    if(pending.Before==null)pending.Before=known;
                    else if(known!=null&&!Same(known,pending.Before)) { pending.Consumed=true;continue; }
                    Emit(pending);
                }
                while(events.Count>HistoryCapacity) {
                    long first=0;Observed old=null;foreach(var pair in events){first=pair.Key;old=pair.Value;break;}
                    retired=first;if(baseline==null||old.Change.After.Revision>baseline.Revision)baseline=old.Change.After;
                    events.Remove(first);
                }
                return true;
            } finally {active=false;}
        }
        private HealthSnapshot FindBefore(long revision)
        {
            if(baseline!=null&&baseline.Revision==revision)return baseline;
            foreach(var pair in events)if(pair.Value.Change.After.Revision==revision)return pair.Value.Change.After;
            return null;
        }
        private static bool Same(HealthSnapshot a,HealthSnapshot b)
        { return a.Id==b.Id&&a.Revision==b.Revision&&a.Health==b.Health&&a.Shield==b.Shield&&a.Connected==b.Connected&&a.Life==b.Life&&a.LifeGeneration==b.LifeGeneration&&a.ConnectionGeneration==b.ConnectionGeneration; }
        private void Emit(Observed item)
        {
            if(!Object.ReferenceEquals(item.ResetIdentity,hud.TransientResetIdentity)){item.Consumed=true;return;}
            if(!item.Authorized||item.Consumed||item.Before==null)return;
            item.Consumed=true; // once-only even if the external renderer throws
            var before=item.Before;var after=item.Change.After;
            if(item.Hidden||!hud.Visible||item.Change.Kind!="damage"||!before.Connected||!after.Connected||before.LifeGeneration!=after.LifeGeneration||before.ConnectionGeneration!=after.ConnectionGeneration||latest==null||latest.LifeGeneration!=after.LifeGeneration||latest.ConnectionGeneration!=after.ConnectionGeneration)return;
            // Promote stored components before arithmetic; summing float totals
            // first can turn an exactly representable 2.5 loss into 2.49999.
            float amount=(float)Math.Max(0,((double)before.Health-after.Health)+((double)before.Shield-after.Shield));
            if(amount>0)output.Emit(target,amount,after.Shield<before.Shield);
        }
    }
}
