#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Climbing.Engine;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ashfall.Integration.ClimbingMotor.Gates
{
    // Coordinator-callable fixture only. No load callbacks, live actor writes or
    // production approval. Root alone admits execution under engine_lane.py.
    public static class ClimbingR7ClearanceGates
    {
        sealed class FixtureApproval:IUnityConvexApproval
        {public RuleResult ValidateSourceHull(long r){return r==7?RuleResult.Accept():RuleResult.Reject("fixture revision");}}
        static void Require(bool b,string name){if(!b)throw new Exception(name);}
        static GameObject Make(Scene s,string name,Vector3 p)
        {var g=new GameObject(name);SceneManager.MoveGameObjectToScene(g,s);g.transform.position=p;return g;}
        public static void RunEditorFixture()
        {
            Scene source=default(Scene),query=default(Scene);Mesh mesh=null;
            try
            {
                source=SceneManager.CreateScene("R7-source-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                query=SceneManager.CreateScene("R7-query-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                var actor=Make(source,"fixture actor",Vector3.zero);
                var wall=Make(source,"fixture slab",new Vector3(.75f,0,0));wall.layer=8;var box=wall.AddComponent<BoxCollider>();box.size=new Vector3(.1f,10,10);
                var copy=Make(query,"fixture slab copy",wall.transform.position);copy.layer=8;var proxy=copy.AddComponent<BoxCollider>();proxy.size=box.size;
                var head=Make(query,"fixture exact head",Vector3.zero);head.layer=10;var body=head.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
                var collider=head.AddComponent<MeshCollider>();mesh=ExplicitConvexContext.CreateSourceMesh();collider.sharedMesh=mesh;collider.convex=true;
                var binding=new ConvexProxyBinding(box,proxy,new ConvexBodyIdentity{BodyId="fixture/box",OwnerId="fixture",Approval="R7-test-only"});
                var context=new ExplicitConvexContext(query,body,collider,actor.transform,7,new FixtureApproval(),new[]{binding});
                Require(context.Validate(),"fixture central layer and identity setup");
                var distance=new UnityBoxHullDistance(context);var clear=new ContinuousHeadClearance(distance);var pose=new ConvexHeadPose{Qw=1};
                var safe=new SourceHeadTrajectory(new Float3(),pose,new Float3(.1f,0,0),.7f);
                Require(clear.Clear(safe,.0025),"native data source hull safe trajectory");
                Require(!clear.Clear(new SourceHeadTrajectory(new Float3(),pose,new Float3(2,0,0),0),.0025),"native data translated slab crossing");
                var orbit=new SourceHeadTrajectory(new Float3(),pose,new Float3(),(float)Math.PI,new Float3(0,0,1));
                double a,b;Require(distance.TryLowerBound(orbit,0,out a)&&a>.2&&distance.TryLowerBound(orbit,2,out b)&&b>.2,"angular endpoints clear");
                Require(!clear.Clear(orbit,.0025),"continuous interior angular crossing rejected");
                // Addition of any unbound body invalidates census, even if distant.
                var unbound=Make(query,"unsupported unbound nonconvex",new Vector3(50,0,0));var mc=unbound.AddComponent<MeshCollider>();mc.sharedMesh=mesh;mc.convex=false;
                Require(!clear.Clear(safe,.0025),"unbound nonconvex never approved");
            }
            finally
            {
                if(source.IsValid())SceneManager.UnloadSceneAsync(source);
                if(query.IsValid())SceneManager.UnloadSceneAsync(query);
                if(mesh!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
    }
}
#endif
