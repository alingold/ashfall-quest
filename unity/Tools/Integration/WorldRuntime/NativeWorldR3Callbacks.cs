#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
using System;
using System.Linq;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Integration.WorldRuntime;
using UnityEngine;
namespace Ashfall.Tools.WorldRuntime
{
    public static class NativeWorldR3Callbacks
    {
        sealed class CallbackFailure : Exception { }
        sealed class Ports : IWorldMaterialResolver, INativeActorIdentity, INativeHazards, IWorldApproval, IRuntimeAttestation, IDisposable
        {
            internal readonly FixturePolicy Policy=new FixturePolicy();
            internal Material Original=new Material(Shader.Find("Hidden/InternalErrorShader"));
            internal Action Hook;internal string Port;internal int Calls;internal bool Denied;
            void Call(string port){if(Port==port&&Hook!=null){Calls++;Hook();}}
            public bool IsApproved(ContentIdentity id){Call("approval");return Policy.IsApproved(id);}
            public bool Matches(ContentIdentity id){Call("receipt");return Policy.Matches(id);}
            public Material Resolve(ContentIdentity id,LoadedSurface s){Call("resolve");return Original;}
            public bool Matches(ContentIdentity id,LoadedSurface s,Material m){Call("material");return !Denied&&m&&Original&&m.shader==Original.shader;}
            public string ActorId(Collider c){Call("actor");return null;}
            public bool IsHazard(Vector3 p,float r,float h){Call("hazard");return false;}
            public void Dispose(){if(Original)UnityEngine.Object.DestroyImmediate(Original);}
        }
        static void Require(bool value,string label){if(!value)throw new Exception("WorldR3 callback: "+label);}
        static void Reject(Action call,string label)
        {
            try{call();}catch(InvalidOperationException){return;}catch(System.IO.InvalidDataException){return;}catch(CallbackFailure){return;}
            throw new Exception("WorldR3 callback failed rejection: "+label);
        }
        static NativeWorld Stage(Ports p)
        {
            var world=WorldRuntimeFixture.Approved(p.Policy);
            return NativeWorld.StageInternal(world,p,p.Policy,p,p,p,p,WorldRuntimeFixture.Layer);
        }
        static void Invoke(string api,NativeWorld n,FixturePolicy p)
        {
            var a=new Float3(0,1,0);var b=new Float3(0,1,2);Float3 floor,normal;
            switch(api)
            {
                case "Activate":n.Activate();break;
                case "Query":n.Query("actor",a,b);break;
                case "IsHazard":n.IsHazard(a);break;
                case "TryFloor":n.TryFloor(new Float3(0,.5f,0),1,2,out floor,out normal);break;
                case "HasStandingRoom":n.HasStandingRoom(a,.3f,1.8f);break;
                case "HasLineOfSight":n.HasLineOfSight("actor","device-owner",a,b,3);break;
                case "Trigger":n.Trigger("device-body","device-owner",b);break;
                case "Pad":n.Pad("device-body","device-owner",b);break;
                case "SafeSpawns":n.SafeSpawns("warmup","shared",1.8f,p);break;
                default:throw new Exception(api);
            }
        }
        static void Withdrawn(NativeWorld n,Collider[] colliders,string label)
        {
            Require(!n.Root.activeInHierarchy,label+" hierarchy withdrawn");
            Require(colliders.All(c=>!c||!c.enabled),label+" exact colliders disabled");
            Reject(n.Activate,label+" terminal");
        }
        public static string Run()
        {
            Require(!Application.isPlaying,"edit mode only");int cases=0;
            var apis=new[]{"Activate","Query","IsHazard","TryFloor","HasStandingRoom","HasLineOfSight","Trigger","Pad","SafeSpawns"};
            foreach(string api in apis)
            foreach(string change in new[]{"approval","receipt","mesh"})
            using(var p=new Ports())using(var n=Stage(p))
            {
                n.Activate();var colliders=n.Root.GetComponentsInChildren<Collider>(true);
                if(change=="approval")p.Policy.Approved=false;
                if(change=="receipt")p.Policy.Attested=false;
                if(change=="mesh")
                {
                    var c=n.Root.GetComponentInChildren<MeshCollider>(true);c.sharedMesh=null;
                    Require(c.sharedMesh==null,"before-query mesh mutation effective");
                }
                Reject(()=>Invoke(api,n,p.Policy),api+"/before/"+change);
                Withdrawn(n,colliders,api+"/before/"+change);cases++;
            }
            foreach(string api in apis)
            foreach(string port in new[]{"approval","receipt","resolve","material"})
            foreach(string mutation in new[]{"revoke","revoke-receipt","mutate","throw","mutate-throw","reentry"})
            using(var p=new Ports())using(var n=Stage(p))
            {
                n.Activate();var colliders=n.Root.GetComponentsInChildren<Collider>(true);
                p.Port=port;p.Hook=()=>{
                    if(mutation=="revoke"){p.Policy.Approved=false;p.Denied=true;}
                    if(mutation=="revoke-receipt")p.Policy.Attested=false;
                    if(mutation=="mutate"||mutation=="mutate-throw")n.Root.transform.position=Vector3.one;
                    if(mutation=="throw"||mutation=="mutate-throw")throw new CallbackFailure();
                    // Even if a port swallows nested rejection, the outer validation must detect terminal state.
                    if(mutation=="reentry")try{n.IsHazard(new Float3());}catch(InvalidOperationException){}
                };
                string label=api+"/"+port+"/"+mutation;Reject(()=>Invoke(api,n,p.Policy),label);
                Require(p.Calls>0,label+" actual callback reached");p.Hook=null;Withdrawn(n,colliders,label);cases++;
            }
            foreach(string port in new[]{"actor","hazard"})
            foreach(string mutation in new[]{"mutate","throw","mutate-throw","revoke","reentry"})
            using(var p=new Ports())using(var n=Stage(p))
            {
                var foreign=new GameObject("WorldR3/foreign");
                try
                {
                    foreign.transform.position=new Vector3(0,1,1);foreign.AddComponent<BoxCollider>();
                    n.Activate();Physics.SyncTransforms();var colliders=n.Root.GetComponentsInChildren<Collider>(true);
                    p.Port=port;p.Hook=()=>{
                        if(mutation=="mutate"||mutation=="mutate-throw")n.Root.transform.localScale=Vector3.one*2;
                        if(mutation=="revoke")p.Policy.Approved=false;
                        if(mutation=="throw"||mutation=="mutate-throw")throw new CallbackFailure();
                        if(mutation=="reentry")try{n.Activate();}catch(InvalidOperationException){}
                    };
                    string api=port=="actor"?"Query":"IsHazard";Reject(()=>Invoke(api,n,p.Policy),api+mutation);
                    Require(p.Calls>0,"direct port actually called");p.Hook=null;Withdrawn(n,colliders,api+mutation);cases++;
                }finally{UnityEngine.Object.DestroyImmediate(foreign);}
            }
            return "PASS "+cases+" native callback cases; all public gameplay methods, exact withdrawal and terminal state; synthetic only";
        }
        public static string RunRollback()
        {
            Require(!Application.isPlaying,"edit mode only");int cases=0,throws=0,unreached=0;
            foreach(string port in new[]{"approval","receipt","resolve","material"})
            foreach(int failureCall in new[]{1,2,3,4,5,6,7,8})
            using(var p=new Ports())using(var live=Stage(p))
            {
                live.Activate();var meshes=Resources.FindObjectsOfTypeAll<Mesh>().Select(x=>x.GetEntityId()).OrderBy(x=>x.ToString()).ToArray();
                var materials=Resources.FindObjectsOfTypeAll<Material>().Select(x=>x.GetEntityId()).OrderBy(x=>x.ToString()).ToArray();
                p.Port=port;p.Hook=()=>{if(p.Calls==failureCall)throw new CallbackFailure();};NativeWorld staged=null;
                try{staged=Stage(p);unreached++;Require(p.Calls<failureCall,"reachable callback failure cannot succeed");}catch(CallbackFailure){throws++;}
                finally{if(staged!=null)staged.Dispose();p.Hook=null;}
                Require(live.Root.activeInHierarchy,"failed replacement preserves live world");
                Require(meshes.SequenceEqual(Resources.FindObjectsOfTypeAll<Mesh>().Select(x=>x.GetEntityId()).OrderBy(x=>x.ToString())),"rollback mesh identities");
                Require(materials.SequenceEqual(Resources.FindObjectsOfTypeAll<Material>().Select(x=>x.GetEntityId()).OrderBy(x=>x.ToString())),"rollback material identities");
                Require(p.Original,"borrowed original survives rollback");Debug.Log("WorldR3 rollback port="+port+" failureCall="+failureCall+" calls="+p.Calls+" reached="+(p.Calls>=failureCall));cases++;
            }
            Require(throws>0,"actual stage failures reached");
            return "PASS "+cases+" stage probes (throws="+throws+", unreachable positions="+unreached+") with exact resource rollback and incumbent preservation; native synthetic only";
        }
    }
}
#endif
