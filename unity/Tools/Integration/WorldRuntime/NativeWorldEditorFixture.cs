#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
using System;
using System.Linq;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Integration.WorldRuntime;
using UnityEngine;

namespace Ashfall.Tools.WorldRuntime
{
    public static class NativeWorldEditorFixture
    {
        sealed class Ports : IWorldMaterialResolver,INativeActorIdentity,INativeHazards,IDisposable
        {
            internal Material Material;internal int DenyAtCall=-1;internal int Calls;
            internal Ports(){var shader=Shader.Find("Hidden/InternalErrorShader");if(!shader)throw new Exception("Fixture shader unavailable");Material=new Material(shader);}
            public Material Resolve(ContentIdentity id,LoadedSurface s){return Material;}
            public bool Matches(ContentIdentity id,LoadedSurface s,Material m){Calls++;return Calls!=DenyAtCall&&m&&m.shader==Material.shader;}
            public string ActorId(Collider c){return null;}
            public bool IsHazard(Vector3 feet,float radius,float height){return false;}
            public void Dispose(){if(Material)UnityEngine.Object.DestroyImmediate(Material);}
        }
        static int count;
        static void Check(bool value,string label){if(!value)throw new Exception("WorldRuntime native: "+label);count++;}
        static void Reject(Action action,string label){try{action();}catch(InvalidOperationException){Check(true,label);return;}throw new Exception(label);}
        static int Meshes(){return Resources.FindObjectsOfTypeAll<Mesh>().Length;}
        static int Materials(){return Resources.FindObjectsOfTypeAll<Material>().Length;}
        // Coordinator invokes once in isolated Editor through engine_lane.py; any exception is FAIL.
        // Does not edit layer names, collision matrices, queriesHitTriggers or queriesHitBackfaces.
        public static string Run()
        {
            count=0;var names=Enumerable.Range(0,32).Select(LayerMask.LayerToName).ToArray();
            bool triggers=Physics.queriesHitTriggers,backs=Physics.queriesHitBackfaces;
            var policy=new FixturePolicy();var world=WorldRuntimeFixture.Approved(policy);
            NativeWorld native=null;GameObject foreign=null;
            using(var ports=new Ports())
            {
                int meshes=Meshes(),materials=Materials();
                try
                {
                    native=NativeWorld.StageInternal(world,policy,policy,policy,ports,ports,ports,WorldRuntimeFixture.Layer);
                    Check(!native.Root.activeSelf,"inert staging");
                    Reject(()=>native.HasLineOfSight("actor","device-owner",new Float3(0,1,0),new Float3(0,1,2),3),"inactive queries denied");
                    native.ActivateFixture();
                    var a=new Float3(0,1,0);var b=new Float3(0,1,2);
                    Check(native.Root.GetComponentsInChildren<MeshCollider>().Length==1,"real structural MeshCollider");
                    Check(native.Root.GetComponentsInChildren<BoxCollider>().Single().isTrigger,"real authored BoxCollider");
                    Check(native.HasLineOfSight("actor","device-owner",a,b,3),"actual ray to box and accepted CPU result");
                    Check(!native.HasLineOfSight("actor","device-body",a,b,3),"body distinct from owner");
                    Check(native.Trigger("device-body","device-owner",b),"actual semantic trigger routing");
                    Check(native.SafeSpawns("warmup","red",1.8f,policy).Length==1,"real native floor and standing clearance");
                    foreign=GameObject.CreatePrimitive(PrimitiveType.Cube);foreign.transform.position=new Vector3(0,1,.75f);foreign.transform.localScale=new Vector3(.5f,.5f,.1f);Physics.SyncTransforms();
                    Check(!native.HasLineOfSight("actor","device-owner",a,b,3),"unknown foreign structure blocks");
                    foreign.transform.position=new Vector3(0,1,1.55f);foreign.transform.localScale=new Vector3(.5f,.5f,.1f);Physics.SyncTransforms();
                    Check(!native.HasLineOfSight("actor","device-owner",a,b,3),"native global nearest structural tie denies");
                    foreign.transform.position=new Vector3(0,1,0);Physics.SyncTransforms();
                    Check(!native.HasLineOfSight("actor","device-owner",a,b,3),"origin containing collider blocks");
                    UnityEngine.Object.DestroyImmediate(foreign);foreign=null;Physics.SyncTransforms();
                    Check(native.HasLineOfSight("actor","device-owner",a,b,3),"foreign removal restores actual query");
                    int previousMeshes=Meshes(),previousMaterials=Materials();ports.DenyAtCall=ports.Calls+2;
                    Reject(()=>NativeWorld.StageInternal(world,policy,policy,policy,ports,ports,ports,WorldRuntimeFixture.Layer),"late second-material staging failure rejected");ports.DenyAtCall=-1;
                    Check(Meshes()==previousMeshes&&Materials()==previousMaterials,"failed staging native allocations cleaned");
                    Check(native.Root.activeSelf&&native.HasLineOfSight("actor","device-owner",a,b,3),"previous world preserved after failed staging");
                    policy.Attested=false;Reject(()=>native.HasLineOfSight("actor","device-owner",a,b,3),"dependency revision rejected");policy.Attested=true;
                    var box=native.Root.GetComponentInChildren<BoxCollider>(true);box.size=Vector3.one*4;
                    Reject(()=>native.Trigger("device-body","device-owner",b),"native volume tamper rejected");
                    native.Dispose();native.Dispose();native=null;
                    Check(Meshes()==meshes&&Materials()==materials,"full dispose returns owned resource census");
                    Check(Enumerable.Range(0,32).Select(LayerMask.LayerToName).SequenceEqual(names)&&Physics.queriesHitTriggers==triggers&&Physics.queriesHitBackfaces==backs,"global layer/query settings unchanged");
                }
                finally{if(foreign)UnityEngine.Object.DestroyImmediate(foreign);if(native!=null)native.Dispose();}
            }
            return "PASS "+count+" WorldRuntime native assertions; analytic test-only content; no runtime/content approval";
        }
        public static string RunStreamed(byte[] mapref,string maprefSha256,byte[] worldPayload,string worldPayloadSha256,int triangleBudget)
        {
            int before=Meshes();
            var world=QuarantinedWorld.LoadPilot(worldPayload,worldPayloadSha256);
            using(var stream=Ashfall.Modules.MapStreaming.MapStream.OpenPilot(mapref,maprefSha256))
            using(var staged=QuarantinedStreamGeometry.Stage(stream,world,triangleBudget))
            {
                if(staged.TriangleCount<1||staged.TriangleCount>triangleBudget||staged.MeshCount<1)throw new Exception("No bounded real streamed geometry");
                Debug.Log("Quarantined actual streamed triangles="+staged.TriangleCount+" meshes="+staged.MeshCount+"; material/semantic/content gates OPEN");
            }
            if(Meshes()!=before)throw new Exception("Streamed mesh leak");
            return "PASS bounded real stream materialization/disposal only; production content OPEN";
        }
    }
}
#endif
