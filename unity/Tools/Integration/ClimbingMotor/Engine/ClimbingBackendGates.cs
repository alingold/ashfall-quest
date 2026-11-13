#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Climbing.Engine;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ashfall.Integration.ClimbingMotor.Gates
{
    // CALLABLE SOURCE ONLY. No MenuItem, InitializeOnLoad, RuntimeInitialize or auto run.
    // Coordinator supplies the installed-editor lane and reserved layers. This fixture
    // deliberately supplies NO approval provider. Passing it cannot approve the hull.
    public static class ClimbingBackendGates
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static GameObject Create(Scene scene,string name,Vector3 at)
        {var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=at;return go;}
        public static void RunEditorFixture()
        {
            Scene source=default(Scene),query=default(Scene);Mesh mesh=null;
            try
            {
                source=SceneManager.CreateScene("ClimbSourceGate-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                query=SceneManager.CreateScene("ClimbQueryGate-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                var actor=Create(source,"actor",Vector3.zero);
                var obstacle=Create(source,"ceiling",new Vector3(0,2,0));obstacle.layer=8;
                var wall=obstacle.AddComponent<BoxCollider>();wall.size=new Vector3(4,.1f,4);
                var copy=Create(query,"ceiling proxy",obstacle.transform.position);copy.layer=8;
                var proxy=copy.AddComponent<BoxCollider>();proxy.size=wall.size;
                var go=Create(query,"owned convex head proxy",Vector3.zero);go.layer=10;
                var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
                var hull=go.AddComponent<MeshCollider>();mesh=ExplicitConvexContext.CreateSourceMesh();hull.sharedMesh=mesh;hull.convex=true;
                source.GetPhysicsScene().Simulate(.02f);query.GetPhysicsScene().Simulate(.02f);
                var binding=new ConvexProxyBinding(wall,proxy,new ConvexBodyIdentity{BodyId="ceiling/body",OwnerId="ceiling",Approval="fixture-only geometry",AuthoredClimbable=true});
                var context=new ExplicitConvexContext(query,body,hull,actor.transform,1,null,new[]{binding});
                Require(context.Validate(),"explicit context/layers/matrix/hull rejected");
                Require(!context.Approved,"fixture must never grant hull approval");
                Require(mesh.vertexCount==168&&ExplicitConvexContext.SourceMesh(mesh),"exact source hull input");
                var scene=new UnityConvexClimbScene(context);
                var pose=new UnityRootYawHeadSource(actor.transform,new Float3(.03f,0,0));
                var collision=new ConvexClimbCollision(scene,pose,new CollisionLayers{StructureMask=1<<8,SupportMask=1<<8,HeadMask=1<<8},.70710678f);
                var result=collision.Solve(new Float3(0,1.75f,0),new Float3(0,.2f,0),false,.001f);
                Require(result.Available&&!result.Complete&&result.Displacement.Y<.1f,"native convex ceiling clipping");
                Require(!collision.HeadMotionClear(new Float3(0,1.94f,0),new Float3(0,.1f,0)),"native true penetration refusal");
                wall.transform.position+=Vector3.up*.2f;
                Require(!context.Validate(),"moved source invalidates captured revision");
                wall.transform.position-=Vector3.up*.2f;
                actor.transform.localScale=new Vector3(2,1,1);ConvexHeadPose read;
                Require(!pose.TryRead(out read),"nonuniform scale refused");
                actor.transform.localScale=new Vector3(-1,1,1);Require(!pose.TryRead(out read),"reflection refused");
                actor.transform.localScale=Vector3.one;actor.transform.rotation=Quaternion.Euler(20,0,0);
                Require(!pose.TryRead(out read),"root pitch cannot become HMD pitch collider");
                actor.transform.rotation=Quaternion.Euler(0,90,0);Require(pose.TryRead(out read)&&read.WorldOffset.X==.03f,"root yaw and already-world offset");
                var rogue=Create(query,"unbound geometry",Vector3.zero);rogue.AddComponent<BoxCollider>();
                Require(!context.Validate(),"unbound query collider refuses all queries");
            }
            finally
            {
                if(source.IsValid())SceneManager.UnloadSceneAsync(source);
                if(query.IsValid())SceneManager.UnloadSceneAsync(query);
                if(mesh!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
        public static IEnumerator RunPlayModeFixture()
        {yield return null;RunEditorFixture();yield return null;}
    }
}
#endif
