#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Climbing.Engine;
using Ashfall.Modules.Player;
using Ashfall.Integration.ClimbingMotor;
using Ashfall.Integration.ClimbPlayerBindings.Engine;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ashfall.Integration.ClimbPlayerBindings.Gates
{
    public static class ClimbR8PlayerGates
    {
        sealed class Approval:IUnityConvexApproval
        {public RuleResult ValidateSourceHull(long r){return RuleResult.Accept();}}
        sealed class Pose:IConvexHeadSource
        {
            public Transform Root;
            public bool TryRead(out ConvexHeadPose p)
            {var q=Root.rotation;var offset=Root.TransformVector(new Vector3(.06f,0,.03f));p=new ConvexHeadPose{Qx=q.x,Qy=q.y,Qz=q.z,Qw=q.w,WorldOffset=F(offset)};return true;}
        }
        sealed class Support:IClimbSupport
        {public bool TryComplete(Float3 r,Float3 h,Float3 p,Float3 v,float s,out Float3 c){c=new Float3();return true;}}
        static void Require(bool b,string s){if(!b)throw new Exception(s);}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        // Root invokes synchronously in a disposable isolated editor probe with no
        // Update between statements. The real Driver, motor, controller and query
        // graph execute. Empty synthetic geometry is not a production receipt.
        public static void RunEditorFixture()
        {
            Scene source=default(Scene),query=default(Scene);Mesh mesh=null;UnityClimbPlayerBinding binding=null,replacement=null;
            try
            {
                source=SceneManager.CreateScene("R8-default-physics-"+Guid.NewGuid());
                query=SceneManager.CreateScene("R8-player-query-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                var actor=new GameObject("R8 real driver");actor.SetActive(false);SceneManager.MoveGameObjectToScene(actor,source);actor.layer=10;
                var cc=actor.AddComponent<CharacterController>();cc.radius=.2f;cc.height=1.6f;cc.center=new Vector3(0,.8f,0);cc.skinWidth=.02f;
                var tracking=new GameObject("tracking");tracking.transform.SetParent(actor.transform,false);
                var head=new GameObject("physical eye");head.transform.SetParent(tracking.transform,false);head.transform.localPosition=new Vector3(.3f,1.5f,.2f);
                var driver=actor.AddComponent<UnityPlayerDriver>();driver.TrackingSpace=tracking.transform;driver.Head=head.transform;driver.BindLifecycle("fixture","actor",1);
                actor.SetActive(true);Require(driver.isActiveAndEnabled,"real driver rig enabled");
                var hg=new GameObject("head hull proxy");SceneManager.MoveGameObjectToScene(hg,query);hg.layer=10;
                var rb=hg.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
                var mc=hg.AddComponent<MeshCollider>();mesh=ExplicitConvexContext.CreateSourceMesh();mc.sharedMesh=mesh;mc.convex=true;
                var context=new ExplicitConvexContext(query,rb,mc,actor.transform,8,new Approval(),new ConvexProxyBinding[0]);
                var distance=new UnityBoxHullDistance(context);var pose=new Pose{Root=actor.transform};
                binding=new UnityClimbPlayerBinding(driver,pose,distance,new Support(),1<<8);binding.InstallOnDriver();
                Vector3 initialRoot=actor.transform.position,initialHead=head.transform.position;
                var plan=RootMotionSafety.Plan(F(initialRoot),F(initialHead),new Float3(.1f,.05f,0),.4f,true);
                var result=driver.Motor.Apply(plan,.02f);
                Require(result.Complete&&result.HasPhaseFeedback,"actual controller physical/pivot feedback");
                Require((head.transform.position-(initialHead+new Vector3(.1f,.05f,0))).sqrMagnitude<1e-8f,"offset eye preserved through actual yaw");
                Require(Math.Abs(result.Velocity.X-5)<.001&&Math.Abs(result.Velocity.Y-2.5)<.001,"physical velocity excludes pivot");
                Require(binding.ValidateObservedHeight(HeightChangeKind.Tracking,.02f,false).Accepted,"post-motor committed baseline");
                object identity=driver.PoseSourceIdentity;driver.Paused=true;
                Require(System.Object.ReferenceEquals(identity,driver.PoseSourceIdentity)&&binding.Approved,"action pause retains physical binding");
                driver.enabled=false;Require(!binding.Approved,"disable invalidates synchronously without Pump");
                driver.enabled=true;Require(!binding.Approved,"reenable cannot revive old binding");
                replacement=new UnityClimbPlayerBinding(driver,pose,distance,new Support(),1<<8);replacement.InstallOnDriver();
                binding.Dispose();Require(System.Object.ReferenceEquals(driver.Motor,replacement.Motor)&&replacement.Approved,"old dispose cannot clear replacement");
            }
            finally
            {
                if(binding!=null)binding.Dispose();if(replacement!=null)replacement.Dispose();
                foreach(var scene in new[]{source,query})if(scene.IsValid())
                {foreach(var g in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(g);SceneManager.UnloadSceneAsync(scene);}
                if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
#endif
