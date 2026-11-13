#if UNITY_6000_0_OR_NEWER && UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Ashfall.Core;
using Ashfall.Modules.World;
using Ashfall.Integration.WorldRuntime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ashfall.Tools.WorldRuntime
{
    // Mutates after ordinary FixedUpdate checks; the inserted pre-native-physics barrier must catch it.
    public sealed class R4LateWorldMutation : MonoBehaviour
    {
        internal Transform Target;
        void FixedUpdate(){if(Target){Target.position=Vector3.right;Target=null;}}
    }
    public sealed class R4ContactProbe : MonoBehaviour
    {
        internal int Contacts,OtherContacts;
        internal Collider[] WorldColliders;
        void Count(Collision collision){if(WorldColliders!=null&&WorldColliders.Contains(collision.collider))Contacts++;else {OtherContacts++;Debug.Log("R4 non-world contact: "+collision.collider.name);}}
        void OnCollisionEnter(Collision collision){Count(collision);}
        void OnCollisionStay(Collision collision){Count(collision);}
    }
    public static class NativeWorldR4LifetimeFixture
    {
        sealed class Ports : IWorldMaterialResolver,INativeActorIdentity,INativeHazards
        {
            internal Material Original=new Material(Shader.Find("Hidden/InternalErrorShader"));
            public Material Resolve(ContentIdentity id,LoadedSurface surface){return Original;}
            public bool Matches(ContentIdentity id,LoadedSurface surface,Material value){return value&&Original&&value.shader==Original.shader;}
            public string ActorId(Collider collider){return null;}
            public bool IsHazard(Vector3 feet,float radius,float height){return false;}
        }
        static void Require(bool ok,string label){if(!ok)throw new Exception("WorldR4: "+label);}
        static bool Present(Collider[] exact)
        {Physics.SyncTransforms();return Physics.OverlapSphere(Vector3.zero,.1f,~0,QueryTriggerInteraction.Collide).Any(c=>exact.Contains(c));}
        static UnityEngine.Object[] Owned(NativeWorld world)
        {return new UnityEngine.Object[]{world.Root}.Concat(world.Root.GetComponentsInChildren<Transform>(true).Select(t=>(UnityEngine.Object)t.gameObject))
            .Concat(world.Root.GetComponentsInChildren<MeshFilter>(true).Select(f=>(UnityEngine.Object)f.sharedMesh))
            .Concat(world.Root.GetComponentsInChildren<MeshRenderer>(true).Select(r=>(UnityEngine.Object)r.sharedMaterial)).Distinct().ToArray();}
        static WorldSourceRegistration Register(WorldSourceLifetime source,Ports ports,out FixturePolicy policy)
        {policy=new FixturePolicy();return source.Register(WorldRuntimeFixture.Approved(policy),policy,policy,policy,ports,ports,ports);}
        static void Stage(WorldLifetimeDriver driver,WorldSourceRegistration registration){driver.StageInternal(registration,WorldRuntimeFixture.Layer);}
        static R4ContactProbe ContactProbe(GameObject host,Collider[] worldColliders)
        {
            var probe=new GameObject("R4/dynamic-contact-probe");probe.transform.SetParent(host.transform,false);probe.transform.position=new Vector3(0,.1f,0);
            probe.AddComponent<SphereCollider>().radius=.2f;
            var body=probe.AddComponent<Rigidbody>();body.useGravity=false;body.constraints=RigidbodyConstraints.FreezeRotation;
            var result=probe.AddComponent<R4ContactProbe>();result.WorldColliders=worldColliders;return result;
        }
        // Root's coroutine runner must advance every yield in PlayMode; compilation is not execution.
        public static IEnumerator RunNativeRootPlayMode()
        {
            Require(Application.isPlaying,"PlayMode required");
            var ports=new Ports();var host=new GameObject("R4/external-host");
            var source=host.AddComponent<WorldSourceLifetime>();var driver=host.AddComponent<WorldLifetimeDriver>();
            try
            {
                FixturePolicy p;var a=Register(source,ports,out p);Stage(driver,a);
                var never=Owned(driver.Current);a.RetireRoot();Require(driver.Current==null,"never activated retired synchronously");
                yield return null;Require(never.All(o=>!o),"never activated owned destruction after yield");Require(ports.Original,"borrowed survives never-active dispose");
                a=Register(source,ports,out p);Stage(driver,a);driver.Activate();
                var old=driver.Current;var oldOwned=Owned(old);var colliders=old.Root.GetComponentsInChildren<Collider>(true);
                Require(Present(colliders),"actual active colliders overlap");
                var baselineContact=ContactProbe(host,colliders);yield return new WaitForFixedUpdate();
                Require(baselineContact.Contacts>0,"actual rigidbody receives active-world native contacts");
                UnityEngine.Object.Destroy(baselineContact.gameObject);
                a.RevokeApproval(p);Require(!Present(colliders)&&colliders.All(c=>!c||!c.enabled),"typed approval synchronous physical withdrawal");
                Require(oldOwned.Any(o=>o),"PlayMode destruction is deferred");
                var withdrawnContact=ContactProbe(host,colliders);long withdrawnStep=WorldLifetimeDriver.PhysicsBarrierCount;
                yield return new WaitForFixedUpdate();
                Require(WorldLifetimeDriver.PhysicsBarrierCount>withdrawnStep&&withdrawnContact.Contacts==0,"actual next physics step has no withdrawn-world contacts");
                UnityEngine.Object.Destroy(withdrawnContact.gameObject);
                var b=Register(source,ports,out p);Stage(driver,b);driver.Activate();var replacement=driver.Current;
                a.RetireRoot();a.RevokeReceipt(a.Receipt);old.Dispose();Require(ReferenceEquals(driver.Current,replacement),"obsolete callbacks and dispose preserve replacement");
                b.RevokeReceipt(p);Require(driver.Current==null,"receipt withdrawal synchronous");
                a=Register(source,ports,out p);Stage(driver,a);driver.Activate();
                var replacedColliders=driver.Current.Root.GetComponentsInChildren<Collider>(true);
                b=Register(source,ports,out p);Require(driver.Current==null&&!Present(replacedColliders),"register replacement withdraws old root synchronously");
                Stage(driver,b);driver.Activate();replacement=driver.Current;a.RetireRoot();
                Require(ReferenceEquals(driver.Current,replacement),"retired registration cannot close registered replacement");
                a=Register(source,ports,out p);Stage(driver,a);driver.Activate();var active=driver.Current;
                var activeColliders=active.Root.GetComponentsInChildren<Collider>(true);
                source.enabled=false;Require(driver.Current==null&&!Present(activeColliders),"source disable immediately withdraws");source.enabled=true;
                a=Register(source,ports,out p);Stage(driver,a);driver.Activate();active=driver.Current;var refs=Owned(active);
                driver.enabled=false;Require(driver.Current==null,"driver disable retires");driver.enabled=true;
                yield return null;Require(refs.All(o=>!o)&&oldOwned.All(o=>!o),"deferred exact ownership destroyed");Require(ports.Original,"borrowed survives replacements");
                a=Register(source,ports,out p);Stage(driver,a);driver.Activate();active=driver.Current;
                var mutate=host.AddComponent<R4LateWorldMutation>();mutate.Target=active.Root.transform;
                activeColliders=active.Root.GetComponentsInChildren<Collider>(true);
                long before=WorldLifetimeDriver.PhysicsBarrierCount;
                yield return new WaitForFixedUpdate();
                Require(WorldLifetimeDriver.PhysicsBarrierCount>before,"actual native physics scheduling barrier advanced");
                Require(driver.Current==null&&driver.LastFailure!=null,"post-script mutation rejected before native physics");
                Require(!Present(activeColliders),"no residual collision after scheduler rejection");
                a=Register(source,ports,out p);Stage(driver,a);driver.Activate();active=driver.Current;activeColliders=active.Root.GetComponentsInChildren<Collider>(true);
                p.Attested=false;before=WorldLifetimeDriver.PhysicsBarrierCount;
                yield return new WaitForFixedUpdate();
                Require(WorldLifetimeDriver.PhysicsBarrierCount>before&&driver.Current==null&&!Present(activeColliders),"silent receipt drift caught on real physics lane");
                a=Register(source,ports,out p);Stage(driver,a);driver.Activate();active=driver.Current;activeColliders=active.Root.GetComponentsInChildren<Collider>(true);
                UnityEngine.Object.Destroy(source);yield return null;
                Require(driver.Current==null&&!Present(activeColliders),"source destroy retires without query");
                source=host.AddComponent<WorldSourceLifetime>();a=Register(source,ports,out p);Stage(driver,a);driver.Activate();active=driver.Current;refs=Owned(active);
                UnityEngine.Object.Destroy(driver);yield return null;Require(refs.All(o=>!o),"driver destroy releases owned objects");
                var scene=SceneManager.CreateScene("R4-source-lifetime-scene");var sceneHost=new GameObject("R4-scene-host");SceneManager.MoveGameObjectToScene(sceneHost,scene);
                var sceneSource=sceneHost.AddComponent<WorldSourceLifetime>();var sceneDriver=sceneHost.AddComponent<WorldLifetimeDriver>();
                a=Register(sceneSource,ports,out p);Stage(sceneDriver,a);sceneDriver.Activate();refs=Owned(sceneDriver.Current);
                yield return SceneManager.UnloadSceneAsync(scene);yield return null;
                Require(refs.All(o=>!o)&&ports.Original,"real scene unload destroys owned and preserves borrowed");
            }
            finally{if(host)UnityEngine.Object.Destroy(host);if(ports.Original)UnityEngine.Object.Destroy(ports.Original);}
            yield return null;
            Debug.Log("PASS WorldR4 concrete lifetime/revocation/pre-PhysicsFixedUpdate/deferred destruction/scene teardown synthetic PlayMode fixture; production maps unapproved");
        }
    }
}
#endif
