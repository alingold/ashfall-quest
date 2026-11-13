using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Modules.Flight;
using Ashfall.Integration.FlightMotor;
namespace Ashfall.Integration.MotionRoutes
{
    // Isolated deterministic CPU arena only. No supplied approval bool/query callback is consumed.
    // All methods require one serialized host lane; production preparation deliberately refuses.
    public sealed class SyntheticCapsuleRoutes : IAuthorityGate, IFlightLifecycleAuthorization
    {
        sealed class Actor
        {internal string Id,Body;internal long Life,Connection,Generation;internal bool Active=true;internal ReservedCapsuleRoute Route;}
        sealed class Approval : IWorldApproval, IWorldBuildPolicy
        {
            internal string Hash;internal bool Active=true;
            public bool IsApproved(ContentIdentity i){return Active&&i.MapId=="synthetic:reserved-capsule-r4"&&i.PayloadSha256==Hash;}
            public bool ApproveMaterial(ContentIdentity i,LoadedSurface s){return IsApproved(i)&&s.Id=="far-triangle";}
            public bool AllowsSpawn(ContentIdentity i,string mode,string team,Float3 p){return false;}
        }
        readonly string session;readonly long epoch;readonly CapsuleFleet fleet;
        readonly Dictionary<string,Actor> actors=new Dictionary<string,Actor>(StringComparer.Ordinal);
        readonly ApprovedWorld world;readonly Approval approval;readonly WorldBuildPlan geometry;
        long clock,geometryRevision=1;PreparedRouteUpdate pending;
        public long Clock {get{return clock;}}
        public string WorldSha256 {get{return world.Identity.PayloadSha256;}}
        public SyntheticCapsuleRoutes(string session,long epoch)
        {
            if(String.IsNullOrEmpty(session)||epoch<0)throw new ArgumentException("scope");this.session=session;this.epoch=epoch;
            var d=new MapDocument{schema=1,id="synthetic:reserved-capsule-r4",version="1",sourcePath="C:/synthetic/reserved-capsule-r4",sourceSha256=new string('0',64),sourceRevision="synthetic-only/1",approval="accepted",capsules=true,spawns=new Spawn[0],pads=new Pad[0],semanticsVersion="world-semantics/1",semantics=new[]{new SemanticRecord{sourceId="far-triangle",ownerId="far",kind="structure",evidence="CPU fixture only"}},surfaces=new[]{new Surface{id="far-triangle",semanticOwner="far",colliderRole="structure",materialPath="synthetic",matrix=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1},vertices=new float[]{10,-1,-5,10,5,-5,10,-1,5},normals=new float[]{0,0,1,0,0,1,0,0,1},indices=new[]{0,1,2},uv=new float[0],color=new float[]{1,1,1,1},roughness=1}}};
            byte[] bytes;using(var s=new MemoryStream()){new DataContractJsonSerializer(typeof(MapDocument)).WriteObject(s,d);bytes=s.ToArray();}
            approval=new Approval{Hash=WorldLoader.Hash(bytes)};world=ApprovedWorld.Load(bytes,approval.Hash,approval);
            geometry=world.PrepareBuild(approval,approval,Layer);fleet=new CapsuleFleet(session,epoch,this);
        }
        static string Layer(int n){return n==8?"WorldStructure":n==9?"WorldInteraction":n==10?"PlayerActor":n==11?"WorldDecoration":"";}
        public static RuleResult PrepareProduction(out ReservedCapsuleRoute route)
        {route=null;return RuleResult.Reject("Missing World owner swept capsule geometry/lifetime reservation and canonical actor/clock lease");}
        public void AdvanceClock(long tick){if(tick<clock||tick==long.MaxValue)throw new ArgumentException("clock");clock=tick;}
        public void RevokeWorld(){approval.Active=false;geometryRevision++;}
        public void ReplaceGeometry(){geometryRevision++;}
        public bool RegisterActor(string id,string body,long life,long connection)
        {
            if(String.IsNullOrEmpty(id)||String.IsNullOrEmpty(body)||life<=0||connection<=0||actors.ContainsKey(id)||actors.Count>=256)return false;
            foreach(var a in actors.Values)if(a.Body==body)return false;
            actors.Add(id,new Actor{Id=id,Body=body,Life=life,Connection=connection});return true;
        }
        public bool ReplaceActor(string id,string body,long life,long connection)
        {
            Actor old;if(!actors.TryGetValue(id,out old)||String.IsNullOrEmpty(body)||life<old.Life||connection<old.Connection||(life==old.Life&&connection==old.Connection&&body==old.Body))return false;
            foreach(var a in actors.Values)if(a.Id!=id&&a.Body==body)return false;
            actors[id]=new Actor{Id=id,Body=body,Life=life,Connection=connection,Generation=old.Generation};return true;
        }
        public RuleResult Validate(CommandContext c)
        {Actor a;return c.IsAuthority&&c.SessionId==session&&c.AuthorityEpoch==epoch&&c.Tick==clock&&actors.TryGetValue(c.ActorId??"",out a)&&!String.IsNullOrEmpty(c.CommandId)?RuleResult.Accept():RuleResult.Reject("synthetic owner scope/clock");}
        public RuleResult ValidateTransition(CommandContext c,long generation,FlightLifecycle reason)
        {Actor a;return Validate(c).Accepted&&actors.TryGetValue(c.ActorId,out a)&&generation>a.Generation?RuleResult.Accept():RuleResult.Reject("synthetic lifecycle");}
        internal bool Current(ReservedCapsuleRoute r,string id)
        {
            Actor a;if(r==null||r.Owner!=this||r.Revoked||r.GeometryRevision!=geometryRevision||clock>r.Expires||!actors.TryGetValue(id??"",out a)||!Object.ReferenceEquals(a,r.Actor)||!a.Active)return false;
            try{world.Revalidate(approval);return r.WorldSha256==world.Identity.PayloadSha256;}catch(InvalidDataException){return false;}
        }
        // Conservative swept upright capsule envelope against every actual immutable triangle AABB.
        // False positives refuse. Synthetic hull radius .3, height 1.8; route feet in [-20,20].
        internal bool Clear(Float3 a,Float3 b)
        {
            if(!Bound(a)||!Bound(b))return false;
            foreach(var s in geometry.Snapshot().Surfaces)for(int i=0;i<s.Indices.Length;i+=3)
            {var x=s.Vertices[s.Indices[i]];var y=s.Vertices[s.Indices[i+1]];var z=s.Vertices[s.Indices[i+2]];
                if(Math.Min(a.X,b.X)-.3<=Math.Max(x.X,Math.Max(y.X,z.X))&&Math.Max(a.X,b.X)+.3>=Math.Min(x.X,Math.Min(y.X,z.X))&&
                   Math.Min(a.Y,b.Y)<=Math.Max(x.Y,Math.Max(y.Y,z.Y))&&Math.Max(a.Y,b.Y)+1.8>=Math.Min(x.Y,Math.Min(y.Y,z.Y))&&
                   Math.Min(a.Z,b.Z)-.3<=Math.Max(x.Z,Math.Max(y.Z,z.Z))&&Math.Max(a.Z,b.Z)+.3>=Math.Min(x.Z,Math.Min(y.Z,z.Z)))return false;
            }return true;
        }
        static bool Bound(Float3 p){return p.IsFinite&&Math.Abs(p.X)<=20&&Math.Abs(p.Y)<=20&&Math.Abs(p.Z)<=20;}
        static Float3 Rear(Float3 p,Float3 d){return new Float3(p.X+2*d.X,p.Y+2*d.Y,p.Z+2*d.Z);}
        public RuleResult Reserve(string actor,CapsulePlan plan,byte[] exactSource,long expires,out ReservedCapsuleRoute route)
        {
            route=null;Actor a;if(plan==null||exactSource==null||exactSource.Length>4096||!actors.TryGetValue(actor??"",out a)||!a.Active||expires<clock||expires-clock>256)return RuleResult.Reject("route lease");
            var bytes=(byte[])exactSource.Clone();var canonical=ReservedCapsuleRoute.Encode(plan);
            if(bytes.Length!=canonical.Length)return RuleResult.Reject("source bytes");for(int i=0;i<bytes.Length;i++)if(bytes[i]!=canonical[i])return RuleResult.Reject("source bytes");
            try{world.Revalidate(approval);}catch(InvalidDataException){return RuleResult.Reject("world revoked");}
            Float3[] points={plan.Start,plan.Clear,plan.Cruise,plan.End,plan.ExitTarget};
            for(int i=0;i<points.Length;i++)if(!Clear(points[i],points[i])||!Clear(points[i],Rear(points[i],plan.RearDirection)))return RuleResult.Reject("geometry");
            for(int i=1;i<points.Length;i++)if(!Clear(points[i-1],points[i])||!Clear(Rear(points[i-1],plan.RearDirection),Rear(points[i],plan.RearDirection)))return RuleResult.Reject("swept geometry");
            route=new ReservedCapsuleRoute(this,a,plan,bytes,a.Body,a.Life,a.Connection,epoch,expires,geometryRevision,world.Identity.PayloadSha256);return RuleResult.Accept();
        }
        public RuleResult PrepareBegin(CommandContext c,ReservedCapsuleRoute route,out PreparedRouteUpdate p)
        {
            p=null;if(pending!=null||!Validate(c).Accepted||!Current(route,c.ActorId))return RuleResult.Reject("route binding");
            var e=new CapsuleEligibility{MapId=world.Identity.MapId,Mode="revive",CapsulesAllowed=true,Match=true,Alive=true,OnDeck=true,Team=route.Plan.Team,TeamCount=route.Plan.Team+1};
            PreparedCapsuleUpdate f;var r=fleet.PrepareBegin(c,e,route.Plan,clock,route,out f);if(!r.Accepted)return r;
            p=new PreparedRouteUpdate(this,f,route,c,false,0,FlightLifecycle.Respawn);pending=p;return r;
        }
        public RuleResult PrepareAdvance(CommandContext c,bool eject,out PreparedRouteUpdate p)
        {
            p=null;Actor a;if(pending!=null||!Validate(c).Accepted||!actors.TryGetValue(c.ActorId,out a)||!Current(a.Route,c.ActorId))return RuleResult.Reject("active route");
            PreparedCapsuleUpdate f;var r=fleet.PrepareAdvance(c,clock,true,true,eject,a.Route,out f);if(!r.Accepted)return r;
            p=new PreparedRouteUpdate(this,f,a.Route,c,false,0,FlightLifecycle.Respawn);pending=p;return r;
        }
        public RuleResult PrepareResetOwner(CommandContext c,long generation,FlightLifecycle reason,out PreparedRouteUpdate p)
        {
            p=null;Actor a;if(pending!=null||!Validate(c).Accepted||!actors.TryGetValue(c.ActorId,out a))return RuleResult.Reject("reset owner");
            PreparedCapsuleUpdate f;var r=fleet.PrepareResetActor(c,generation,reason,this,out f);if(!r.Accepted)return r;
            p=new PreparedRouteUpdate(this,f,null,c,true,generation,reason);pending=p;return r;
        }
        internal RuleResult Check(PreparedRouteUpdate p)
        {
            Actor a;if(p!=pending||p.Finished||!Validate(p.Context).Accepted||!actors.TryGetValue(p.Context.ActorId,out a)||!Object.ReferenceEquals(a,p.Actor))return RuleResult.Reject("participant lifetime");
            if(!p.Reset&&!Current(p.Route,p.Context.ActorId))return RuleResult.Reject("route stale");
            return p.Fleet.Validate();
        }
        internal object ActorFor(string id){return actors[id];}
        internal void Publish(PreparedRouteUpdate p)
        {
            // Caller validates ALL participants immediately before commit under the exclusive lane.
            // No fallible external authorization/query between final validation and these owner writes.
            if(p!=pending||p.Finished||!Validate(p.Context).Accepted||!Object.ReferenceEquals(actors[p.Context.ActorId],p.Actor)||(!p.Reset&&!Current(p.Route,p.Context.ActorId)))throw new InvalidOperationException("participant consumed/stale");
            p.Fleet.CommitValidated();var a=(Actor)p.Actor;
            if(p.Reset){if(a.Route!=null)a.Route.Revoke();a.Route=null;a.Generation=p.Generation;a.Active=p.Reason==FlightLifecycle.Respawn;}
            else a.Route=p.Route;
            p.Finished=true;pending=null;
        }
        internal void Abort(PreparedRouteUpdate p){if(p.Finished)return;p.Fleet.Abort();p.Finished=true;if(pending==p)pending=null;}
    }
    public sealed class PreparedRouteUpdate
    {
        internal readonly SyntheticCapsuleRoutes Owner;internal readonly PreparedCapsuleUpdate Fleet;internal readonly ReservedCapsuleRoute Route;
        internal readonly CommandContext Context;internal readonly object Actor;internal readonly bool Reset;internal readonly long Generation;internal readonly FlightLifecycle Reason;internal bool Finished;
        internal PreparedRouteUpdate(SyntheticCapsuleRoutes o,PreparedCapsuleUpdate f,ReservedCapsuleRoute r,CommandContext c,bool reset,long g,FlightLifecycle reason)
        {Owner=o;Fleet=f;Route=r;Context=c;Reset=reset;Generation=g;Reason=reason;Actor=o.ActorFor(c.ActorId);}
        public CapsuleUpdate Update {get{return Fleet.Update;}}
        public RuleResult Validate(){return Owner.Check(this);}
        public void CommitValidated(){Owner.Publish(this);}
        public void Abort(){Owner.Abort(this);}
    }
}


