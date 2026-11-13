#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Integration.WorldRuntime;
using UnityEngine;

namespace Ashfall.Tools.WorldRuntime
{
    // Coordinator-only callable fixtures. Local entity IDs are diagnostics, never semantic/body authority.
    public sealed class UnapprovedWorldBehaviour : MonoBehaviour { }
    public static class NativeWorldR2Fixture
    {
        sealed class Ports : IWorldMaterialResolver, INativeActorIdentity, INativeHazards, IDisposable
        {
            internal Material Original;
            internal int Calls, Deny=-1;
            internal Ports()
            {
                var shader=Shader.Find("Hidden/InternalErrorShader");
                if(!shader)throw new Exception("Missing fixture shader");
                Original=new Material(shader);Original.name="WorldR2/borrowed-fixture-original";
            }
            public Material Resolve(ContentIdentity id,LoadedSurface surface){return Original;}
            public bool Matches(ContentIdentity id,LoadedSurface surface,Material material)
            {return ++Calls!=Deny&&Original&&material&&material.shader==Original.shader;}
            public string ActorId(Collider collider){return null;}
            public bool IsHazard(Vector3 feet,float radius,float height){return false;}
            public void Dispose(){if(Original){if(Application.isPlaying)UnityEngine.Object.Destroy(Original);else UnityEngine.Object.DestroyImmediate(Original);}}
        }
        static void Require(bool ok,string label){if(!ok)throw new Exception("WorldR2: "+label);}
        static void Reject(Action action,string label)
        {try{action();}catch(InvalidOperationException){return;}throw new Exception("WorldR2 failed rejection: "+label);}
        static NativeWorld Stage(Ports ports,out FixturePolicy policy)
        {policy=new FixturePolicy();var world=WorldRuntimeFixture.Approved(policy);return NativeWorld.StageInternal(world,policy,policy,policy,ports,ports,ports,WorldRuntimeFixture.Layer);}
        static UnityEngine.Object[] Census()
        {return Resources.FindObjectsOfTypeAll<Mesh>().Cast<UnityEngine.Object>().Concat(Resources.FindObjectsOfTypeAll<Material>()).ToArray();}
        static string Id(UnityEngine.Object o){return o.GetEntityId().ToString();}
        static void Log(string phase,UnityEngine.Object[] rows)
        {foreach(var o in rows.OrderBy(Id))Debug.Log("WorldR2 census phase="+phase+" id="+Id(o)+" type="+o.GetType().Name+" name="+o.name+" flags="+o.hideFlags);}
        static void EqualCensus(string phase,UnityEngine.Object[] before)
        {
            var after=Census();Log(phase,after);
            var oldIds=before.Select(Id).OrderBy(s=>s).ToArray();var newIds=after.Select(Id).OrderBy(s=>s).ToArray();
            Require(oldIds.SequenceEqual(newIds),phase+" exact entity census differs; retain complete logs for ownership diagnosis");
        }
        static UnityEngine.Object[] Owned(NativeWorld n)
        {
            return new UnityEngine.Object[]{n.Root}.Concat(n.Root.GetComponentsInChildren<Transform>(true).Where(t=>t!=n.Root.transform).Select(t=>(UnityEngine.Object)t.gameObject))
                .Concat(n.Root.GetComponentsInChildren<MeshFilter>(true).Select(f=>(UnityEngine.Object)f.sharedMesh))
                .Concat(n.Root.GetComponentsInChildren<MeshRenderer>(true).Select(r=>(UnityEngine.Object)r.sharedMaterial)).ToArray();
        }
        static bool Present(Collider[] owned)
        {return Physics.OverlapBox(Vector3.zero,Vector3.one*.1f,Quaternion.identity,~0,QueryTriggerInteraction.Collide).Any(c=>owned.Contains(c));}
        static void Case(string label,Action<NativeWorld,Ports,FixturePolicy> mutate,bool active)
        {
            using(var ports=new Ports())
            {
                FixturePolicy policy;using(var n=Stage(ports,out policy))
                {
                    if(active)n.Activate();mutate(n,ports,policy);
                    if(active)Reject(()=>n.Trigger("device-body","device-owner",new Float3(0,1,2)),label);
                    else Reject(n.Activate,label);
                    Require(!n.Root.activeInHierarchy,label+" withdraws hierarchy");
                    Require(n.Root.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled||!c.gameObject.activeInHierarchy),label+" no remaining active collider");
                    Reject(n.Activate,label+" cannot revive");
                }
            }
        }
        public static string RunTamper()
        {
            Require(!Application.isPlaying,"Editor gate requires edit mode");
            foreach(bool active in new[]{false,true})
            {
                foreach(string type in new[]{"Collider","Camera","AudioSource","Mono","Transform"})
                {
                    string kind=type;
                    Case("inactive grandchild "+kind,(n,p,f)=>{
                        var go=new GameObject("unapproved/"+kind);go.SetActive(false);go.transform.SetParent(n.Root.transform.GetChild(0),false);
                        if(kind=="Collider")go.AddComponent<BoxCollider>();
                        if(kind=="Camera")go.AddComponent<Camera>();
                        if(kind=="AudioSource")go.AddComponent<AudioSource>();
                        if(kind=="Mono")go.AddComponent<UnapprovedWorldBehaviour>();
                    },active);
                }
                Case("root transform",(n,p,f)=>n.Root.transform.position=Vector3.one,active);
                Case("child transform",(n,p,f)=>n.Root.transform.GetChild(0).localScale=Vector3.one*2,active);
                Case("visual layer",(n,p,f)=>n.Root.GetComponentsInChildren<MeshRenderer>(true)[1].gameObject.layer=0,active);
                Case("UV",(n,p,f)=>{var m=n.Root.GetComponentInChildren<MeshFilter>(true).sharedMesh;m.uv=new Vector2[m.vertexCount];},active);
                Case("mesh replacement",(n,p,f)=>n.Root.GetComponentInChildren<MeshFilter>(true).sharedMesh=null,active);
                Case("collider mesh",(n,p,f)=>n.Root.GetComponentInChildren<MeshCollider>(true).sharedMesh=null,active);
                Case("extra renderer",(n,p,f)=>n.Root.GetComponentInChildren<MeshRenderer>(true).sharedMaterials=new[]{p.Original,p.Original},active);
                Case("material identity",(n,p,f)=>n.Root.GetComponentInChildren<MeshRenderer>(true).sharedMaterial=p.Original,active);
                Case("approval revocation",(n,p,f)=>f.Approved=false,active);
                Case("receipt revocation",(n,p,f)=>f.Attested=false,active);
                Case("root layer",(n,p,f)=>n.Root.layer=8,active);
                Case("sub-epsilon transform",(n,p,f)=>n.Root.transform.GetChild(0).localPosition=new Vector3(.0000001f,0,0),active);
                Case("extra direct collider",(n,p,f)=>n.Root.transform.GetChild(0).gameObject.AddComponent<BoxCollider>(),active);
                Case("extra direct rigidbody",(n,p,f)=>n.Root.transform.GetChild(0).gameObject.AddComponent<Rigidbody>().isKinematic=true,active);
                Case("collider routing",(n,p,f)=>n.Root.GetComponentInChildren<BoxCollider>(true).includeLayers=1,active);
            }
            return "PASS 40 native tamper cases with withdrawal and terminal activation rejection; fixture content only";
        }
        public static string RunOwnershipCensus()
        {
            Require(!Application.isPlaying,"Immediate census requires edit mode");
            var cold=Census();Log("cold-before-ports",cold);
            // Attribute builtin first-use independently, before the repeated owned-resource baseline.
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);Log("first-primitive-created",Census());
            UnityEngine.Object.DestroyImmediate(primitive);Log("first-primitive-destroyed",Census());
            var warmed=Census();
            for(int repeat=0;repeat<3;repeat++)
            {
                using(var ports=new Ports())
                {
                    var baseline=Census();Log("ports-created-"+repeat,baseline);
                    FixturePolicy policy;using(var n=Stage(ports,out policy))
                    {
                        var owned=Owned(n);Log("owned-"+repeat,owned);var colliders=n.Root.GetComponentsInChildren<Collider>(true);
                        Physics.SyncTransforms();Require(!Present(colliders),"staging has no physical overlap");n.Activate();Require(Present(colliders),"activation exposes actual floor");
                        var beforeFailure=Census();ports.Deny=ports.Calls+2;
                        Reject(()=>{var unexpected=NativeWorld.StageInternal(WorldRuntimeFixture.Approved(policy),policy,policy,policy,ports,ports,ports,WorldRuntimeFixture.Layer);unexpected.Dispose();},"late material failure");
                        ports.Deny=-1;EqualCensus("late-failure-"+repeat,beforeFailure);
                        Require(n.Root.activeSelf,"late failure preserves previous world");
                        n.Dispose();n.Dispose();Physics.SyncTransforms();Require(!Present(colliders),"dispose withdraws actual floor");
                        Require(owned.All(o=>!o),"all exact owned references destroyed");Require(ports.Original,"borrowed original survives");
                    }
                    EqualCensus("native-disposed-"+repeat,baseline);
                }
                EqualCensus("ports-disposed-"+repeat,warmed);
            }
            // Cold delta is retained separately; neither a builtin hypothesis nor warming waives repeated accumulation.
            Log("final-versus-cold-inspect-identities",Census());
            return "PASS exact owned destruction and three identical warm entity censuses; cold delta requires attribution; original R1 census remains separate";
        }
        // Advance on the coordinator's main-thread PlayMode runner. yield null allows deferred Destroy to finish.
        public static IEnumerator RunPlayModeLifetime()
        {
            Require(Application.isPlaying,"PlayMode required");
            using(var ports=new Ports())
            {
                FixturePolicy inactivePolicy;var inactive=Stage(ports,out inactivePolicy);var inactiveOwned=Owned(inactive);
                inactive.Dispose();inactive.Dispose();yield return null;
                Require(inactiveOwned.All(o=>!o),"never-activated owned resources destroyed after frame");
                FixturePolicy policy;using(var n=Stage(ports,out policy))
                {
                    var owned=Owned(n);var colliders=n.Root.GetComponentsInChildren<Collider>(true);
                    Physics.SyncTransforms();Require(!Present(colliders),"PlayMode staging inert");n.Activate();Require(Present(colliders),"PlayMode floor active");
                    n.Dispose();n.Dispose();Physics.SyncTransforms();Require(!Present(colliders),"PlayMode immediate physical withdrawal");
                    Require(colliders.All(c=>!c||!c.enabled),"all owned colliders immediately disabled");
                    yield return null;
                    Require(owned.All(o=>!o),"deferred objects destroyed after frame");Require(ports.Original,"borrowed material retained");
                }
            }
            yield return null;
            Debug.Log("PASS WorldR2 PlayMode deferred ownership lifetime; no dynamic contact or production content approval");
        }
    }
}
#endif
