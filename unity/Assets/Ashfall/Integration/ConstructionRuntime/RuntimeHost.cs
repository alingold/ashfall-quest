using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Construction;
namespace Ashfall.Integration.ConstructionRuntime
{
    public sealed class ConstructionPublication
    {
        public readonly long Serial,Epoch; public readonly string Cause,Actor,Command,MapHash,Session,HostInstance;public readonly int Charges;
        readonly PlacedPanel[] panels;
        public PlacedPanel[] Panels {get{return (PlacedPanel[])panels.Clone();}}
        internal ConstructionPublication(long serial,string cause,string actor,string command,string hash,int charges,PlacedPanel[] state,string session,long epoch,string instance)
        {Serial=serial;Cause=cause;Actor=actor;Command=command;MapHash=hash;Charges=charges;panels=(PlacedPanel[])state.Clone();Session=session;Epoch=epoch;HostInstance=instance;}
    }
    // One shared host simulation lane only; ports must be pure, stable and non-reentrant.
    // Pull/ack outbox, no publication callbacks between resource and panel mutation.
    // In-memory exactly-once serial generation, not durable/network exactly-once delivery.
    public sealed class RuntimeHost
    {
        sealed class Actors:IConstructionActors
        {
            public readonly Dictionary<string,BuilderState> States=new Dictionary<string,BuilderState>(StringComparer.Ordinal);
            public bool TryRead(string actor,out BuilderState state){return States.TryGetValue(actor,out state);}
        }
        readonly Actors actors=new Actors();readonly Dictionary<string,string> hands=new Dictionary<string,string>(StringComparer.Ordinal);
        readonly Dictionary<string,ConstructionLease> leases=new Dictionary<string,ConstructionLease>(StringComparer.Ordinal);
        readonly IConstructionHostState hostState;long observedTick=-1;bool clockFailed;
        readonly AttestedWorld world;readonly ConstructionService service;readonly IConstructionDamageAuthorization damage;
        readonly List<ConstructionPublication> outbox=new List<ConstructionPublication>();long serial;bool disposed;
        readonly string session,instance;readonly long epoch;
        public RuntimeHost(string session,long epoch,string hostInstance,IAuthorityGate authority,AttestedWorld geometry,IConstructionDamageAuthorization damagePolicy,IConstructionHostState currentHost=null)
        {hostState=currentHost;if(geometry==null||String.IsNullOrEmpty(hostInstance))throw new ArgumentException("host_instance");world=geometry;damage=damagePolicy;service=new ConstructionService(session,epoch,authority,actors,world,false);world.Attach(session,epoch,hostInstance);this.session=session;this.epoch=epoch;instance=hostInstance;}
        // Legacy unbound ingress cannot establish lifecycle/queued-intent safety.
        public bool Claim(string actor,string hand,BuilderState state,long validThroughTick){return false;}
        bool Observe()
        {
            if(disposed||clockFailed||hostState==null)return false;
            long now=hostState.Tick;if(now<0||now<observedTick){clockFailed=true;return false;}observedTick=now;return true;
        }
        static bool Same(HostBuilderStamp a,HostBuilderStamp b)
        {return a!=null&&b!=null&&a.Active&&b.Active&&a.Session==b.Session&&a.Epoch==b.Epoch&&a.Actor==b.Actor&&a.AuthorityLease==b.AuthorityLease&&a.Life==b.Life&&a.Connection==b.Connection&&a.RootRevision==b.RootRevision&&a.PoseRevision==b.PoseRevision&&a.InputGeneration==b.InputGeneration;}
        bool Stamp(string actor,out HostBuilderStamp stamp)
        {
            stamp=null;return Observe()&&hostState.TryRead(actor,out stamp)&&stamp!=null&&stamp.Active&&stamp.Session==session&&stamp.Epoch==epoch&&stamp.Actor==actor&&!String.IsNullOrWhiteSpace(stamp.AuthorityLease)&&stamp.Life>0&&stamp.Connection>0&&stamp.RootRevision>=0&&stamp.PoseRevision>=0;
        }
        public bool Claim(string actor,string hand,BuilderState state,long validThroughTick,out ConstructionLease lease)
        {
            lease=null;HostBuilderStamp stamp;
            if(String.IsNullOrEmpty(actor)||(hand!="left"&&hand!="right"))return false;
            if(!Stamp(actor,out stamp)){Release(actor);return false;}
            if(validThroughTick<observedTick||state==null||!state.Eligible||!state.Tracked){Release(actor);return false;}
            ConstructionLease old;if(leases.TryGetValue(actor,out old)&&(!Same(old.Stamp,stamp)||old.Expires<observedTick))Release(actor);
            string held;if(hands.TryGetValue(actor,out held)&&held!=hand)return false;
            lease=new ConstructionLease(this,stamp,validThroughTick);
            hands[actor]=hand;actors.States[actor]=state;leases[actor]=lease;service.CancelPreview(actor);return true;
        }
        public void Release(string actor){if(actor==null)return;hands.Remove(actor);leases.Remove(actor);actors.States.Remove(actor);service.CancelPreview(actor);}
        public void Close(){disposed=true;foreach(var actor in new List<string>(hands.Keys))Release(actor);}
        // Pure prepared read: no Release/Observe/admission mutation. Original queued lease only.
        public bool TryReadPreparedLease(string actor,ConstructionLease lease,out BuilderState pose)
        {
            pose=null;HostBuilderStamp stamp;ConstructionLease current;
            long now=hostState==null?-1:hostState.Tick;
            return !disposed&&!clockFailed&&now>=observedTick&&now>=0&&lease!=null&&lease.Host==this&&
                leases.TryGetValue(actor,out current)&&Object.ReferenceEquals(current,lease)&&now<=lease.Expires&&
                hostState.TryRead(actor,out stamp)&&Same(lease.Stamp,stamp)&&actors.States.TryGetValue(actor,out pose);
        }
        bool Begin(){if(disposed||outbox.Count>=1024||!world.Capture())return false;world.Bind(service.Snapshot());return true;}
        bool Lease(string actor,ConstructionLease lease)
        {
            HostBuilderStamp stamp;ConstructionLease current;
            if(actor==null||!Stamp(actor,out stamp)){if(actor!=null)Release(actor);return false;}
            if(!leases.TryGetValue(actor,out current))return false;
            if(current.Expires<observedTick||!Same(current.Stamp,stamp)){Release(actor);return false;}
            return lease!=null&&lease.Host==this&&Object.ReferenceEquals(current,lease);
        }
        public BuildDecision Preview(string actor,long hostTick){return null;}
        public BuildDecision Preview(string actor,ConstructionLease lease){if(!Lease(actor,lease)||!Begin()||!Lease(actor,lease))return null;return service.Preview(actor);}
        public RuleResult Place(ConstructionCommand command){return RuleResult.Reject("bound_intent_required");}
        public RuleResult Place(ConstructionCommand command,ConstructionLease lease)
        {
            if(!Lease(command.Context.ActorId,lease))return RuleResult.Reject("actor_lease_stale");
            if(!Begin()||!Lease(command.Context.ActorId,lease))return RuleResult.Reject("runtime_unavailable");
            var result=service.Place(command);if(!result.Accepted)return RuleResult.Reject(result.Reason);
            Publish("place",command.Context);return RuleResult.Accept();
        }
        public RuleResult Damage(ConstructionCommand command,string piece,float amount,string capability)
        {
            if(!Begin()||damage==null||!damage.Validate(command.Context,instance,piece,amount,capability))return RuleResult.Reject("damage_capability_unavailable");
            var result=service.Damage(command,piece,amount);if(result.Accepted)Publish("damage",command.Context);return result;
        }
        // Supply/Inventory integration intentionally not exposed without a registered source receipt.
        public RuleResult AwardSupply(ConstructionCommand command,int amount){return RuleResult.Reject("inventory_supply_receipt_unbound");}
        public bool Advance(float seconds,bool paused)
        {
            if(!Begin())return false;var before=service.Snapshot();service.Advance(seconds,paused);var after=service.Snapshot();
            if(before.Length!=after.Length)Publish("collapse",new CommandContext("","","",0,0,true));return true;
        }
        void Publish(string cause,CommandContext c)
        {var state=service.Snapshot();world.Bind(state);outbox.Add(new ConstructionPublication(++serial,cause,c.ActorId,c.CommandId,world.Identity.PayloadSha256,String.IsNullOrEmpty(c.ActorId)?0:service.Charges(c.ActorId),state,session,epoch,instance));}
        public ConstructionPublication Pending(){return outbox.Count==0?null:outbox[0];}
        public bool Acknowledge(long publication){if(outbox.Count==0||outbox[0].Serial!=publication)return false;outbox.RemoveAt(0);return true;}
        public PlacedPanel[] Snapshot(){return service.Snapshot();}
        public int Charges(string actor){return service.Charges(actor);}
    }
}
