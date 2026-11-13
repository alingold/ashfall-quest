using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using Ashfall.Core;
using Ashfall.Modules.World;
class BoundaryTests
{
    static int count;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static void Reject(Action action,string name){bool no=false;try{action();}catch(InvalidDataException){no=true;}Check(no,name);}
    static byte[] Bytes(MapDocument d){using(var m=new MemoryStream()){new DataContractJsonSerializer(typeof(MapDocument)).WriteObject(m,d);return m.ToArray();}}
    static MapDocument Doc(byte[] b){using(var m=new MemoryStream(b))return (MapDocument)new DataContractJsonSerializer(typeof(MapDocument)).ReadObject(m);}
    static string Layer(int i){return new[]{"WorldStructure","WorldInteraction","PlayerActor","WorldDecoration"}[i-8];}
    class Approval:IWorldApproval
    {
        public bool Allow=true;public string Hash,Version,Source,Revision;
        public bool IsApproved(ContentIdentity id){return Allow&&id.Schema==1&&id.PayloadSha256==Hash&&id.Version==Version&&id.SourceSha256==Source&&id.SourceRevision==Revision&&id.Disposition=="accepted";}
    }
    class Policy:IWorldBuildPolicy
    {
        public bool Material=true,Mutate,Bounds=true;public Float3 Final;
        public bool ApproveMaterial(ContentIdentity id,LoadedSurface s){if(Mutate){s.Indices[0]=99999;s.Color[0]=-99;s.MaterialPath="substituted";}return Material;}
        public bool AllowsSpawn(ContentIdentity id,string mode,string team,Float3 feet){Final=feet;return Bounds&&mode=="warmup"&&feet.Y>=.1f&&feet.Y<1;}
    }
    class Floor:ISpawnQuery
    {
        public bool Hazard,Blocked; public Float3 Room,Haz;
        public bool TryFloor(Float3 p,float up,float depth,out Float3 f,out Float3 n){f=new Float3(p.X,0,p.Z);n=new Float3(0,1,0);return true;}
        public bool HasStandingRoom(Float3 p,float r,float h){Room=p;return !Blocked;}
        public bool IsHazard(Float3 p){Haz=p;return Hazard;}
    }
    static ApprovedWorld Approved(MapDocument d,out Approval a){var b=Bytes(d);a=new Approval {Hash=WorldLoader.Hash(b),Version=d.version,Source=d.sourceSha256,Revision=d.sourceRevision};return ApprovedWorld.Load(b,a.Hash,a);}
    static void Main(string[] args)
    {
        foreach(var path in args){var b=File.ReadAllBytes(path);var q=QuarantinedWorld.LoadPilot(b,WorldLoader.Hash(b));Check(q.Snapshot().Surfaces.Length==2,"real sample parsed "+path);Reject(()=>q.PrepareQuarantinedPilot(new Policy(),Layer),"real unclassified sample blocked with IDs");Reject(()=>ApprovedWorld.Load(b,WorldLoader.Hash(b),new Approval()),"real unreviewed sample production denied");var snap=q.Snapshot();snap.Surfaces[0].Vertices[0]=new Float3(999,999,999);Check(q.Snapshot().Surfaces[0].Vertices[0].X!=999,"quarantine snapshot frozen");}
        // Explicit test-only authoring; never a production export approval.
        var d=Doc(File.ReadAllBytes(args[1]));d.approval="accepted";d.version="test-only-r3";d.pads=new Pad[0];d.semanticsVersion="world-semantics/1";
        var wall=d.surfaces[0];var target=d.surfaces[1];wall.id="wall-child";wall.semanticOwner="building";target.id="device-child";target.semanticOwner="fixture-parent";target.colliderRole="trigger";
        wall.matrix=new float[]{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1};wall.vertices=new float[]{-2,-2,-1,2,-2,-1,0,2,-1};wall.normals=new float[]{0,0,1,0,0,1,0,0,1};wall.indices=new[]{0,1,2};wall.uv=new float[0];
        d.semantics=new[]{new SemanticRecord {sourceId=wall.id,ownerId="building",kind="structure",evidence="analytic test wall"},new SemanticRecord {sourceId=target.id,ownerId="fixture-parent",kind="interaction",evidence="analytic authored device box",boxes=new[]{new AuthoredBox {center=new float[]{0,0,2},size=new float[]{1,1,1}}}}};
        d.spawns=new[]{new Spawn {id="spawn",mode="warmup",team="shared",position=new float[]{0,0,0},forward=new float[]{0,0,-1}}};
        Approval a;var w=Approved(d,out a);var p=new Policy();var plan=w.PrepareBuild(a,p,Layer);
        Check(w.Identity.Schema==1&&w.Identity.MapId==d.id&&w.Identity.Version==d.version&&w.Identity.SourceRevision==d.sourceRevision&&w.Identity.SourceSha256==d.sourceSha256&&w.Identity.PayloadSha256==a.Hash,"complete identity retained");
        Check(typeof(ApprovedWorld).GetConstructors().Length==0&&typeof(ContentIdentity).GetConstructors().Length==0,"no public forged-token constructor");
        a.Allow=false;Reject(()=>w.PrepareBuild(a,p,Layer),"revoked build denied");a.Allow=true;
        a.Version="wrong";Reject(()=>w.PrepareBuild(a,p,Layer),"version substitution denied");a.Version=d.version;
        a.Hash=new string('0',64);Reject(()=>w.PrepareBuild(a,p,Layer),"hash substitution denied");a.Hash=w.Identity.PayloadSha256;
        a.Source="wrong";Reject(()=>w.PrepareBuild(a,p,Layer),"source substitution denied");a.Source=d.sourceSha256;
        Reject(()=>w.PrepareBuild(a,p,i=>"Default"),"layer0/default allocation denied");
        p.Material=false;Reject(()=>w.PrepareBuild(a,p,Layer),"material policy denied");p.Material=true;p.Mutate=true;
        plan=w.PrepareBuild(a,p,Layer);Check(plan.Snapshot().Surfaces[0].Indices[0]!=99999&&plan.Snapshot().Surfaces[0].MaterialPath!="substituted","policy callback material and index mutation isolated");
        var s=plan.Snapshot();s.Id="forged";s.Surfaces[0].Indices[0]=99999;s.Surfaces[0].Vertices[0]=new Float3(999,999,999);s.Surfaces[0].Normals[0]=new Float3(9,9,9);s.Surfaces[0].Color[0]=-5;s.Spawns[0].Position=new Float3(9,9,9);s.Surfaces[1].Uv=new float[]{99};
        Check(plan.Snapshot().Id==d.id&&plan.Snapshot().Surfaces[0].Indices[0]!=99999&&plan.Snapshot().Surfaces[0].Color[0]>=0&&plan.Snapshot().Spawns[0].Position.X==0,"post-load record and nested-array mutation isolated");
        var bytes=Bytes(d);var frozen=ApprovedWorld.Load(bytes,WorldLoader.Hash(bytes),a);bytes[0]=0;Check(frozen.Snapshot().Id==d.id,"input-buffer mutation isolated");
        var r=plan.Registry();Check(r.Route("device-child","fixture-parent",false),"child collider routes to parent semantic owner");Check(!r.Route("wall-child","fixture-parent",false),"structure cannot impersonate interaction");Check(!r.Route("device-child","wrong-owner",true),"wrong owner trigger rejected");Reject(()=>r.Resolve("unregistered-child"),"unknown collider blocked");
        r.Resolve("device-child").boxes[0].size[0]=999;Check(r.Resolve("device-child").boxes[0].size[0]==1,"returned authored-volume array isolated");
        var g=new WorldGeometry(plan);Check(!g.HasLineOfSight("fixture-parent",new Float3(0,0,0),new Float3(0,0,2),5),"wall occludes intended fixture");Check(g.HasLineOfSight("fixture-parent",new Float3(0,0,1.4f),new Float3(0,0,2),5),"front approach reaches authored fixture");Check(g.HasLineOfSight("fixture-parent",new Float3(.4f,0,1.4f),new Float3(.4f,0,2),5),"off-axis approach reaches fixture");Check(!g.HasLineOfSight("fixture-parent",new Float3(2,0,1.4f),new Float3(2,0,2),5),"ray missing authored volume rejected");Check(!g.HasLineOfSight("fixture-parent",new Float3(0,0,1.4f),new Float3(0,0,2),.1f),"out of range rejected");Check(g.TriggerContains("device-child","fixture-parent",new Float3(0,0,2)),"authored trigger inside");Check(!g.TriggerContains("device-child","fixture-parent",new Float3(0,0,3)),"outside trigger rejected");Check(!g.TriggerContains("device-child","building",new Float3(0,0,2)),"trigger owner substitution rejected");
        var route=new WorldHitRouting(plan);var hash=plan.Identity.PayloadSha256;
        var hit=new WorldQueryHit(hash,"device-child",null,9,2);
        Check(route.CanInteract("actor","fixture-parent",5,new[]{hit}),"physics hit routes child to owner");
        Check(!route.CanInteract("actor","fixture-parent",5,new[]{hit,new WorldQueryHit(hash,"wall-child",null,8,1)}),"near wall blocks physics ray");
        Check(!route.CanInteract("actor","fixture-parent",5,new[]{hit,new WorldQueryHit("other-map","device-child",null,9,1)}),"foreign map blocks physics ray");
        Check(!route.CanInteract("actor","fixture-parent",5,new[]{hit,new WorldQueryHit(hash,null,"other-actor",10,1)}),"other actor occludes");
        Check(route.CanInteract("actor","fixture-parent",5,new[]{hit,new WorldQueryHit(null,null,"actor",10,1)}),"explicit requesting actor excluded");
        Check(!route.CanInteract("actor","fixture-parent",5,new[]{new WorldQueryHit(hash,"unknown",null,9,1)}),"unregistered interaction denied");
        Check(!route.CanInteract("actor","fixture-parent",5,new[]{hit,new WorldQueryHit(hash,"unknown",null,11,1)}),"unknown decoration cannot bypass ray");
        Check(!route.CanInteract("actor","fixture-parent",5,new[]{hit,new WorldQueryHit(hash,"wall-child",null,8,2)}),"ambiguous coplanar wall tie rejected");
        Check(!route.Trigger("other-map","device-child","fixture-parent",9),"foreign-map trigger denied");Check(!route.Trigger(hash,"device-child","fixture-parent",8),"wrong-layer trigger denied");Check(route.Trigger(hash,"device-child","fixture-parent",9),"bound trigger routes");
        var floor=new Floor();var spawn=plan.SafeSpawns("warmup","red",1.8f,floor,p);Check(spawn.Length==1&&spawn[0].Position.Y==p.Final.Y&&floor.Room.Y==p.Final.Y&&floor.Haz.Y==p.Final.Y,"bounds clearance hazard all use final feet");p.Bounds=false;Check(plan.SafeSpawns("warmup","red",1.8f,floor,p).Length==0,"mode bounds reject final pose");p.Bounds=true;floor.Hazard=true;Check(plan.SafeSpawns("warmup","red",1.8f,floor,p).Length==0,"final hazard rejects");floor.Hazard=false;floor.Blocked=true;Check(plan.SafeSpawns("warmup","red",1.8f,floor,p).Length==0,"final clearance rejects");
        d.semantics[1].boxes=null;var bad=Approved(d,out a);Reject(()=>bad.PrepareBuild(a,p,Layer),"no inferred convex trigger hull");d.semantics[1].boxes=new[]{new AuthoredBox {center=new float[]{0,0,0},size=new float[]{-1,1,1}}};bad=Approved(d,out a);Reject(()=>bad.PrepareBuild(a,p,Layer),"invalid authored volume rejected");
        Console.WriteLine("PASS "+count+" r3 assertions; Unity/hardware UNKNOWN");
    }
}
