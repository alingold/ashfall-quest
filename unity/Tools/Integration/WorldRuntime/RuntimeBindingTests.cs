using System;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Integration.WorldRuntime;
using Ashfall.Tools.WorldRuntime;
class RuntimeBindingTests
{
    sealed class Hits:IWorldExternalHits
    {public WorldQueryHit[] Rows=new WorldQueryHit[0];public Action Callback;public WorldQueryHit[] Query(string a,Float3 o,Float3 t){if(Callback!=null)Callback();return Rows;}}
    sealed class Floor:ISpawnQuery
    {
        public bool Block,Hazard;public Float3 Feet;
        public bool TryFloor(Float3 p,float u,float d,out Float3 f,out Float3 n){f=new Float3(p.X,0,p.Z);n=new Float3(0,1,0);return true;}
        public bool HasStandingRoom(Float3 f,float r,float h){Feet=f;return !Block;}
        public bool IsHazard(Float3 f){return Hazard;}
    }
    static int count;
    static void Check(bool yes,string label){if(!yes)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    static void Reject(Action action,string label){try{action();}catch(InvalidOperationException){Check(true,label);return;}catch(System.IO.InvalidDataException){Check(true,label);return;}throw new Exception(label);}
    static void Main()
    {
        Check(!Array.Exists(typeof(RuntimeBinding).Assembly.GetReferencedAssemblies(),a=>a.Name.StartsWith("Unity",StringComparison.Ordinal)),"define-OFF assembly has no Unity references");
        var p=new FixturePolicy();var w=WorldRuntimeFixture.Approved(p);var hits=new Hits();
        var r=new RuntimeBinding(w,p,p,p,WorldRuntimeFixture.Layer,hits);
        var origin=new Float3(0,1,0);var target=new Float3(0,1,2);
        Check(r.HasLineOfSight("actor","device-owner",origin,target,3),"actual accepted query selects authored box");
        Check(!r.HasLineOfSight("actor","device-body",origin,target,3),"BodyId is not semantic owner");
        Check(r.ResolveBody("device-body").ownerId=="device-owner","real registry child-to-owner");
        Check(r.Trigger("device-body","device-owner",target),"actual trigger contains");
        Check(!r.Trigger("device-body","device-owner",origin),"trigger excludes outside");
        var owner=new WorldQueryHit(w.Identity.PayloadSha256,"device-body",null,9,1);
        foreach(var distance in new[]{1f,1.000005f,1.000018f})
        {
            var blocker=new WorldQueryHit(null,null,null,8,distance);var chain=new WorldQueryHit(w.Identity.PayloadSha256,"device-body",null,9,1.000009f);
            foreach(var rows in new[]{new[]{owner,chain,blocker},new[]{blocker,owner,chain},new[]{chain,blocker,owner}})
                Check(r.RouteNative("actor","device-owner",3,rows)==(distance>1.00001f),"global-minimum tie order "+distance);
        }
        hits.Rows=new[]{new WorldQueryHit(null,"foreign-body",null,0,.5f)};
        Check(!r.HasLineOfSight("actor","device-owner",origin,target,3),"native unknown blocks accepted composed query");
        hits.Rows=new[]{new WorldQueryHit(null,null,"actor",10,.5f)};
        Check(r.HasLineOfSight("actor","device-owner",origin,target,3),"trusted exact self actor excluded");
        hits.Rows=new[]{new WorldQueryHit(null,null,"other",10,.5f)};
        Check(!r.HasLineOfSight("actor","device-owner",origin,target,3),"other actor occludes");
        hits.Rows=null;Check(!r.HasLineOfSight("actor","device-owner",origin,target,3),"incomplete native hit provider fails closed");
        hits.Rows=new WorldQueryHit[0];hits.Callback=()=>p.Attested=false;
        Reject(()=>r.HasLineOfSight("actor","device-owner",origin,target,3),"mid-query dependency revocation rejects");
        hits.Callback=null;p.Attested=true;p.Approved=false;Reject(()=>r.ResolveBody("device-body"),"approval revocation rejects");p.Approved=true;
        p.Attested=false;Reject(()=>new RuntimeBinding(w,p,p,p,WorldRuntimeFixture.Layer,hits),"missing attestation blocks construction");p.Attested=true;
        Reject(()=>new RuntimeBinding(w,p,p,p,i=>"Default",hits),"layer allocation required");
        var f=new Floor();var s=r.SafeSpawns("warmup","red",1.8f,f,p);
        Check(s.Length==1&&s[0].Position.Y==f.Feet.Y&&f.Feet.Y==p.LastFeet.Y,"accepted spawn final feet shared with clearance and bounds");
        f.Block=true;Check(r.SafeSpawns("warmup","red",1.8f,f,p).Length==0,"standing clearance rejects");f.Block=false;
        f.Hazard=true;Check(r.SafeSpawns("warmup","red",1.8f,f,p).Length==0,"hazard rejects");f.Hazard=false;
        p.Bounds=false;Check(r.SafeSpawns("warmup","red",1.8f,f,p).Length==0,"final bounds reject");
        var doc=WorldRuntimeFixture.Document();doc.approval="unreviewed";var bytes=WorldRuntimeFixture.Bytes(doc);
        Reject(()=>ApprovedWorld.Load(bytes,WorldLoader.Hash(bytes),p),"pilot cannot enter production boundary");
        Console.WriteLine("PASS "+count+" CPU assertions; native execution OPEN");
    }
}
