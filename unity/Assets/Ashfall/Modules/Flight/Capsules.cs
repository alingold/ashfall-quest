using System;
using System.Collections.Generic;
using Ashfall.Core;
namespace Ashfall.Modules.Flight
{
    public sealed class CapsulePlan
    {
        public readonly string PodId;public readonly int Team,Slot;
        public readonly Float3 Start,Clear,Cruise,End,ExitTarget,RearDirection;
        public readonly double Duration;
        public CapsulePlan(string podId,int team,int slot,Float3 start,Float3 clear,Float3 cruise,Float3 end,Float3 exitTarget,Float3 rearDirection,double duration)
        {
            if(String.IsNullOrEmpty(podId)||team<0||slot<0||slot>=12||!V.Bounded(start,1000000)||!V.Bounded(clear,1000000)||!V.Bounded(cruise,1000000)||!V.Bounded(end,1000000)||!V.Bounded(exitTarget,1000000)||!V.Bounded(rearDirection,2)||Math.Abs(V.Len(rearDirection)-1)>.001||!V.Finite(duration)||duration<1.5||duration>120)throw new ArgumentException("capsule plan");
            double length=V.Len(V.Sub(clear,start))+V.Len(V.Sub(cruise,clear))+V.Len(V.Sub(end,cruise));
            if(!V.Finite(length)||length/duration>35.01)throw new ArgumentException("capsule speed");
            PodId=podId;Team=team;Slot=slot;Start=start;Clear=clear;Cruise=cruise;End=end;ExitTarget=exitTarget;RearDirection=rearDirection;Duration=duration;
        }
        public Float3 Position(double elapsed)
        {
            if(!V.Finite(elapsed))throw new ArgumentException("elapsed");
            Float3[] p={Start,Clear,Cruise,End};
            float length=V.Len(V.Sub(Clear,Start))+V.Len(V.Sub(Cruise,Clear))+V.Len(V.Sub(End,Cruise));
            float distance=length*(float)Math.Max(0,Math.Min(1,elapsed/Duration));
            for(int i=0;i<3;i++){float segment=V.Len(V.Sub(p[i+1],p[i]));if(distance<segment)return V.Add(p[i],V.Mul(V.Sub(p[i+1],p[i]),distance/segment));distance-=segment;}
            return End;
        }
    }
    public struct CapsuleEligibility
    {
        public string MapId,Mode;
        public bool Indoor, CapsulesAllowed, Match, Alive, OnDeck;
        public int Team,TeamCount;
    }
    public enum CapsuleState { None, Flying, Released, Landed }
    public struct CapsuleUpdate
    {
        public readonly CapsuleState State; public readonly Float3 RootPosition;
        public CapsuleUpdate(CapsuleState state,Float3 position){State=state;RootPosition=position;}
    }
    public interface ICapsuleRouteValidation
    {
        // Host checks immutable map provenance, route geometry, cabin/team/slot ownership, destination clearance.
        bool ValidateRoute(string actorId,CapsulePlan plan);
    }
    internal sealed class CapsuleCommandFence
    {
        readonly string session,actor; readonly long epoch; readonly IAuthorityGate gate;
        Dictionary<string,long> seen=new Dictionary<string,long>(4096,StringComparer.Ordinal);
        readonly string[] expired=new string[4096];
        long newest=-1;
        internal bool AllowsTick(long tick) { return tick >= newest; }
        internal bool HasCommand(string id) { return id!=null&&seen.ContainsKey(id); }
        internal void AdvanceFloor(long tick) { newest=Math.Max(newest,tick); }
        internal CapsuleCommandFence(string session,string actor,long epoch,IAuthorityGate gate)
        { if(String.IsNullOrEmpty(session)||String.IsNullOrEmpty(actor)||gate==null)throw new ArgumentException("authority scope"); this.session=session;this.actor=actor;this.epoch=epoch;this.gate=gate; }
        internal CapsuleCommandFence Copy()
        {
            var copy=new CapsuleCommandFence(session,actor,epoch,gate);
            copy.newest=newest;
            foreach(var entry in seen)copy.seen.Add(entry.Key,entry.Value);
            return copy;
        }
        // Validation never mutates this fence. Host gate must be an authorization/replay
        // decision, not a gameplay commit; its own external bookkeeping is host-owned.
        internal RuleResult Validate(CommandContext c)
        {
            if(!c.IsAuthority||c.SessionId!=session||c.ActorId!=actor||c.AuthorityEpoch!=epoch||c.Tick<0||c.Tick<newest||String.IsNullOrEmpty(c.CommandId))return RuleResult.Reject("authority scope/tick");
            if(seen.ContainsKey(c.CommandId))return RuleResult.Reject("replay");
            long edge=Math.Max(newest,c.Tick)-256;int retained=0;
            foreach(var p in seen)if(p.Value>=edge)retained++;
            if(retained>=4096)return RuleResult.Reject("command window full");
            return gate.Validate(c);
        }
        internal void Commit(CommandContext c)
        {
            long edge=Math.Max(newest,c.Tick)-256;int count=0;
            foreach(var p in seen)if(p.Value<edge)expired[count++]=p.Key;
            for(int i=0;i<count;i++){seen.Remove(expired[i]);expired[i]=null;}
            newest=Math.Max(newest,c.Tick);seen.Add(c.CommandId,c.Tick);
        }
        internal RuleResult Take(CommandContext c)
        {RuleResult r=Validate(c);if(r.Accepted)Commit(c);return r;}
    }

    // Host-lane owner mechanics capability. Geometry/life/connection must remain
    // reserved by the root composition; this token does not certify those domains.
    public sealed class PreparedCapsuleUpdate
    {
        internal readonly CapsuleFleet Owner,Staged;internal readonly long Revision;internal readonly CommandContext Context;internal bool Finished;
        public CapsuleUpdate Update {get;private set;}
        internal PreparedCapsuleUpdate(CapsuleFleet owner,CapsuleFleet staged,long revision,CapsuleUpdate update,CommandContext context)
        {Owner=owner;Staged=staged;Revision=revision;Update=update;Context=context;}
        public RuleResult Validate(){return Owner.ValidateAuthorization(this);}
        public void CommitValidated(){Owner.Publish(this);}
        public void Abort(){Owner.Abort(this);}
    }
    public sealed class CapsuleFleet
    {
        sealed class Ride { internal CapsulePlan Plan;internal double Start,Last;internal CapsuleState State; }
        readonly string session;readonly long epoch;readonly IAuthorityGate gate;
        Dictionary<string,CapsuleCommandFence> commands=new Dictionary<string,CapsuleCommandFence>(StringComparer.Ordinal);
        Dictionary<string,Ride> rides=new Dictionary<string,Ride>(StringComparer.Ordinal);
        HashSet<string> pods=new HashSet<string>(StringComparer.Ordinal);
        sealed class Life { internal long Generation,Tick;internal bool Inactive;internal CapsuleCommandFence Fence; }
        Dictionary<string,Life> lives=new Dictionary<string,Life>(StringComparer.Ordinal);
        long revision;
        PreparedCapsuleUpdate pending;
        static bool Fields(Type type,string names)
        {
            var fields=type.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
            var expected=names.Split(',');if(fields.Length!=expected.Length)return false;
            foreach(var field in fields)if(Array.IndexOf(expected,field.Name)<0)return false;
            return true;
        }
        static bool SupportedFields()
        {return Fields(typeof(CapsuleFleet),"session,epoch,gate,commands,rides,pods,lives,revision,pending")&&
            Fields(typeof(Ride),"Plan,Start,Last,State")&&Fields(typeof(Life),"Generation,Tick,Inactive,Fence")&&
            Fields(typeof(CapsuleCommandFence),"session,actor,epoch,gate,seen,expired,newest");}
        CapsuleFleet CopyState()
        {
            var copy=new CapsuleFleet(session,epoch,gate);
            foreach(var entry in commands)copy.commands.Add(entry.Key,entry.Value.Copy());
            foreach(var entry in lives)copy.lives.Add(entry.Key,new Life{Generation=entry.Value.Generation,Tick=entry.Value.Tick,Inactive=entry.Value.Inactive,Fence=entry.Value.Fence.Copy()});
            foreach(var entry in rides)copy.rides.Add(entry.Key,new Ride{Plan=entry.Value.Plan,Start=entry.Value.Start,Last=entry.Value.Last,State=entry.Value.State});
            foreach(var pod in pods)copy.pods.Add(pod);
            return copy;
        }
        // Local owner checkpoint readiness only; no global retirement assertion.
        public RuleResult CheckpointReady()
        {return !SupportedFields()?RuleResult.Reject("unaccounted capsule fields"):pending!=null?RuleResult.Reject("capsule reservation outstanding"):RuleResult.Accept();}
        RuleResult Reserve(CommandContext context,CapsuleFleet staged,long original,RuleResult result,CapsuleUpdate update,out PreparedCapsuleUpdate plan)
        {
            plan=null;
            if(!result.Accepted)return result;
            if(original!=revision||pending!=null)return RuleResult.Reject("capsule changed during preparation");
            plan=new PreparedCapsuleUpdate(this,staged,revision,update,context);pending=plan;return result;
        }
        public RuleResult PrepareBegin(CommandContext c,CapsuleEligibility eligibility,CapsulePlan route,double now,ICapsuleRouteValidation validation,out PreparedCapsuleUpdate plan)
        {
            plan=null;if(!SupportedFields())return RuleResult.Reject("unaccounted capsule fields");if(pending!=null)return RuleResult.Reject("capsule reservation outstanding");
            long before=revision;var staged=CopyState();CapsuleUpdate update;
            var result=staged.Begin(c,eligibility,route,now,validation,out update);
            return Reserve(c,staged,before,result,update,out plan);
        }
        public RuleResult PrepareAdvance(CommandContext c,double now,bool match,bool alive,bool eject,IFlightWorld world,out PreparedCapsuleUpdate plan)
        {
            plan=null;if(!SupportedFields())return RuleResult.Reject("unaccounted capsule fields");if(pending!=null)return RuleResult.Reject("capsule reservation outstanding");
            long before=revision;var staged=CopyState();CapsuleUpdate update;
            var result=staged.Advance(c,now,match,alive,eject,world,out update);
            return Reserve(c,staged,before,result,update,out plan);
        }
        public RuleResult PrepareResetActor(CommandContext c,long generation,FlightLifecycle reason,IFlightLifecycleAuthorization authorization,out PreparedCapsuleUpdate plan)
        {
            plan=null;if(!SupportedFields())return RuleResult.Reject("unaccounted capsule fields");if(pending!=null)return RuleResult.Reject("capsule reservation outstanding");
            long before=revision;var staged=CopyState();
            var result=staged.ResetActor(c,generation,reason,authorization);
            return Reserve(c,staged,before,result,new CapsuleUpdate(),out plan);
        }
        internal RuleResult ValidateAuthorization(PreparedCapsuleUpdate plan)
        {
            var result=ValidatePrepared(plan);if(!result.Accepted)return result;
            result=gate.Validate(plan.Context);
            return result.Accepted?ValidatePrepared(plan):result;
        }
        internal RuleResult ValidatePrepared(PreparedCapsuleUpdate plan)
        {return plan!=null&&plan==pending&&plan.Revision==revision&&!plan.Finished?RuleResult.Accept():RuleResult.Reject("capsule reservation/revision");}
        internal void Publish(PreparedCapsuleUpdate plan)
        {
            if(!ValidatePrepared(plan).Accepted)throw new InvalidOperationException("stale capsule publication");
            commands=plan.Staged.commands;rides=plan.Staged.rides;pods=plan.Staged.pods;lives=plan.Staged.lives;
            revision++;plan.Finished=true;pending=null;
        }
        internal void Abort(PreparedCapsuleUpdate plan)
        {if(plan.Finished)return;plan.Finished=true;if(pending==plan)pending=null;}
        public CapsuleFleet(string session,long epoch,IAuthorityGate gate)
        {if(String.IsNullOrEmpty(session)||gate==null)throw new ArgumentException("scope");this.session=session;this.epoch=epoch;this.gate=gate;}
        public static bool AllowsCapsules(CapsuleEligibility e)
        {return !String.IsNullOrEmpty(e.MapId)&&e.CapsulesAllowed&&!e.Indoor&&e.MapId!="archive:battlefield-paintball-gods-pitdevil"&&e.MapId!="archive:skb-paintball-gods-skblong";}
        RuleResult Validate(CommandContext c,out CapsuleCommandFence fence)
        {
            fence=null;
            if(String.IsNullOrEmpty(c.ActorId))return RuleResult.Reject("actor");
            Life life;if(lives.TryGetValue(c.ActorId,out life)&&life.Fence.HasCommand(c.CommandId))return RuleResult.Reject("lifecycle replay");
            if(!commands.TryGetValue(c.ActorId,out fence))
            {
                if(!c.IsAuthority||c.SessionId!=session||c.AuthorityEpoch!=epoch)return RuleResult.Reject("scope");
                if(commands.Count>=256)return RuleResult.Reject("fleet full");
                fence=new CapsuleCommandFence(session,c.ActorId,epoch,gate);
            }
            return fence.Validate(c);
        }
        void Commit(CommandContext c,CapsuleCommandFence fence)
        {revision++;fence.Commit(c);if(!commands.ContainsKey(c.ActorId))commands.Add(c.ActorId,fence);}
        // Retain all ordinary fences: cleanup must never resurrect old commands or
        // reclaim capacity by forgetting an authenticated actor's replay history.
        public RuleResult ResetActor(CommandContext c,long generation,FlightLifecycle reason,IFlightLifecycleAuthorization authorization)
        {
            if(String.IsNullOrEmpty(c.ActorId)||authorization==null||generation<=0||!Enum.IsDefined(typeof(FlightLifecycle),reason))return RuleResult.Reject("lifecycle input");
            Life life;CapsuleCommandFence ordinary;
            if(!lives.TryGetValue(c.ActorId,out life))life=new Life{Fence=new CapsuleCommandFence(session,c.ActorId,epoch,gate),Tick=-1};
            if(generation<=life.Generation||c.Tick<life.Tick)return RuleResult.Reject("lifecycle generation/tick");
            if(commands.TryGetValue(c.ActorId,out ordinary))
            {if(!ordinary.AllowsTick(c.Tick)||ordinary.HasCommand(c.CommandId))return RuleResult.Reject("stale lifecycle tick/replay");}
            else
            {
                if(commands.Count>=256)return RuleResult.Reject("fleet full");
                ordinary=new CapsuleCommandFence(session,c.ActorId,epoch,gate);
            }
            RuleResult r=life.Fence.Validate(c);if(!r.Accepted)return r;
            r=authorization.ValidateTransition(c,generation,reason);if(!r.Accepted)return r;
            revision++;life.Fence.Commit(c);ordinary.AdvanceFloor(c.Tick);
            if(!commands.ContainsKey(c.ActorId))commands.Add(c.ActorId,ordinary);
            life.Generation=generation;life.Tick=c.Tick;life.Inactive=reason!=FlightLifecycle.Respawn;lives[c.ActorId]=life;
            Ride ride;if(rides.TryGetValue(c.ActorId,out ride)){pods.Remove(ride.Plan.PodId);rides.Remove(c.ActorId);}
            return r;
        }
        public RuleResult Begin(CommandContext c,CapsuleEligibility e,CapsulePlan plan,double now,ICapsuleRouteValidation routes,out CapsuleUpdate update)
        {
            update=new CapsuleUpdate();
            Life life;if(!String.IsNullOrEmpty(c.ActorId)&&lives.TryGetValue(c.ActorId,out life)&&(life.Inactive||c.Tick<=life.Tick))return RuleResult.Reject("inactive lifecycle");
            if(plan==null||routes==null||!V.Finite(now)||now<0||now>1e12||!AllowsCapsules(e)||!e.Match||e.Mode!="revive"||e.TeamCount<=0||!e.Alive||!e.OnDeck||e.Team<0||e.Team>=e.TeamCount||e.Team!=plan.Team||String.IsNullOrEmpty(c.ActorId)||rides.ContainsKey(c.ActorId)||pods.Contains(plan.PodId))return RuleResult.Reject("capsule eligibility/reservation");
            CapsuleCommandFence fence;RuleResult r=Validate(c,out fence);if(!r.Accepted)return r;
            if(!routes.ValidateRoute(c.ActorId,plan))return RuleResult.Reject("capsule route");
            Commit(c,fence);
            rides.Add(c.ActorId,new Ride{Plan=plan,Start=now,Last=now,State=CapsuleState.Flying});pods.Add(plan.PodId);
            update=new CapsuleUpdate(CapsuleState.Flying,plan.Start);return r;
        }
        public RuleResult Advance(CommandContext c,double now,bool match,bool alive,bool eject,IFlightWorld world,out CapsuleUpdate update)
        {
            update=new CapsuleUpdate();Ride ride;
            if(world==null||String.IsNullOrEmpty(c.ActorId)||!rides.TryGetValue(c.ActorId,out ride)||ride.State!=CapsuleState.Flying||!match||!V.Finite(now)||now>1e12||now<ride.Last)return RuleResult.Reject("capsule state/time");
            CapsuleCommandFence fence;RuleResult r=Validate(c,out fence);if(!r.Accepted)return r;
            Float3 at=ride.Plan.Position(now-ride.Start);CapsuleState state=CapsuleState.Flying;
            if(eject||!alive){state=CapsuleState.Released;at=V.Add(at,V.Mul(ride.Plan.RearDirection,2));}
            else if(now>=ride.Start+ride.Plan.Duration){state=CapsuleState.Landed;at=ride.Plan.ExitTarget;}
            if(!at.IsFinite)return RuleResult.Reject("invalid capsule destination");
            // Fail closed if geometry changed after launch; remain carried and allow a later retry.
            if(state!=CapsuleState.Flying&&!world.ClearCapsuleExit(c.ActorId,at))return RuleResult.Reject("capsule exit blocked");
            Commit(c,fence);
            ride.State=state;ride.Last=now;update=new CapsuleUpdate(state,at);return r;
        }
    }
}


