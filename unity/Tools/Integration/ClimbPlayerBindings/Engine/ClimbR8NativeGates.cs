#if UNITY_6000_0_OR_NEWER
using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Modules.Climbing.Engine;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Ashfall.Integration.ClimbPlayerBindings.Gates
{
    // Synthetic fixture, root-callable only. Requires centrally configured layers.
    // No InitializeOnLoad, MenuItem, editor launch or asset/import operations.
    public static class ClimbR8NativeGates
    {
        sealed class Approval:IUnityConvexApproval
        {public bool Live=true;public RuleResult ValidateSourceHull(long revision){return Live?RuleResult.Accept():RuleResult.Reject("fixture revoked");}}
        static void Require(bool b,string name){if(!b)throw new Exception(name);}
        static GameObject Make(Scene s,string name,Vector3 p)
        {var g=new GameObject(name);SceneManager.MoveGameObjectToScene(g,s);g.transform.position=p;return g;}
        public static void RunEditorFixture()
        {
            Scene source=default(Scene),query=default(Scene);Mesh mesh=null;
            try
            {
                source=SceneManager.CreateScene("R8-source-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                query=SceneManager.CreateScene("R8-query-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                var actor=Make(source,"actor",Vector3.zero);
                var a=Make(source,"wall",new Vector3(1,0,0));a.layer=8;var box=a.AddComponent<BoxCollider>();box.size=new Vector3(.1f,10,10);
                var b=Make(query,"wall proxy",a.transform.position);b.layer=8;var proxy=b.AddComponent<BoxCollider>();proxy.size=box.size;
                var head=Make(query,"168-vertex hull",Vector3.zero);head.layer=10;
                var rb=head.AddComponent<Rigidbody>();rb.isKinematic=true;rb.useGravity=false;
                var mc=head.AddComponent<MeshCollider>();mesh=ExplicitConvexContext.CreateSourceMesh();mc.sharedMesh=mesh;mc.convex=true;
                var approval=new Approval();long revision=8;
                var id=new ConvexBodyIdentity{BodyId="fixture/wall",OwnerId="fixture",Approval="synthetic-only"};
                var binding=new ConvexProxyBinding(box,proxy,id);
                var context=new ExplicitConvexContext(query,rb,mc,actor.transform,revision,approval,new[]{binding});
                var distance=new UnityBoxHullDistance(context);var pose=new ConvexHeadPose{Qw=1,WorldOffset=new Float3(.08f,.04f,.03f)};
                var safe=new SourceHeadTrajectory(new Float3(),pose,new Float3(.1f,.1f,0),.6f);
                Require(new ContinuousHeadClearance(distance).Clear(safe,.0025),"offset head + nonzero yaw/height clear");
                Require(!new ContinuousHeadClearance(distance).Clear(new SourceHeadTrajectory(new Float3(),pose,new Float3(2,0,0),.6f),.0025),"wall crossing refused");
                approval.Live=false;Require(!distance.Approved,"revoked approval");approval.Live=true;
                box.center=new Vector3(.01f,0,0);Require(!context.Validate(),"changed source box center");box.center=Vector3.zero;
                proxy.size=new Vector3(.2f,10,10);Require(!context.Validate(),"changed proxy size");proxy.size=box.size;
                // Source and proxy moving together STILL invalidate the sealed context.
                a.transform.position=new Vector3(1.2f,0,0);b.transform.position=a.transform.position;
                Require(!context.Validate(),"moving support invalidates old revision");
                context=new ExplicitConvexContext(query,rb,mc,actor.transform,++revision,approval,new[]{binding});
                distance=new UnityBoxHullDistance(context);
                Require(new ContinuousHeadClearance(distance).Clear(safe,.0025),"new geometry revision retry");
                // Floor/ceiling and corner use actual source/proxy box transforms.
                foreach(var position in new[]{new Vector3(0,1,0),new Vector3(0,-1,0),new Vector3(.7f,0,.7f)})
                {
                    a.transform.position=position;b.transform.position=position;
                    box.size=position.y!=0?new Vector3(10,.1f,10):new Vector3(.1f,10,.1f);proxy.size=box.size;
                    a.transform.rotation=position.y==0?Quaternion.Euler(0,45,0):Quaternion.identity;b.transform.rotation=a.transform.rotation;
                    context=new ExplicitConvexContext(query,rb,mc,actor.transform,++revision,approval,new[]{binding});
                    distance=new UnityBoxHullDistance(context);
                    var delta=new Float3(position.x*2,position.y*2,position.z*2);
                    Require(!new ContinuousHeadClearance(distance).Clear(new SourceHeadTrajectory(new Float3(),pose,delta,.3f),.0025),"ceiling/floor/corner crossing");
                    // Near floor tangency is conservatively unavailable with the .1m
                    // native reserve. Do not assert this candidate can slide at contact.
                    if(position.y<0){double lower;Require(distance.TryLowerBound(new SourceHeadTrajectory(new Float3(0,-.9f,0),pose,new Float3(.1f,0,0),0),0,out lower)&&lower==0,"floor tangent loses certificate");}
                }
                // Bound nonconvex AND convex meshes reach the provider's explicit
                // unsupported-type path, rather than only the unbound census guard.
                UnityEngine.Object.DestroyImmediate(box);UnityEngine.Object.DestroyImmediate(proxy);
                var sm=a.AddComponent<MeshCollider>();sm.sharedMesh=mesh;
                var pm=b.AddComponent<MeshCollider>();pm.sharedMesh=mesh;
                foreach(bool convex in new[]{false,true})
                {
                    sm.convex=pm.convex=convex;
                    var mb=new ConvexProxyBinding(sm,pm,id);
                    context=new ExplicitConvexContext(query,rb,mc,actor.transform,++revision,approval,new[]{mb});
                    Require(context.Validate(),"bound mesh context valid");double lower;
                    Require(!new UnityBoxHullDistance(context).TryLowerBound(safe,0,out lower),"bound mesh distance deliberately declined");
                }
            }
            finally
            {
                // Destroy scene roots synchronously before destroying the shared mesh.
                foreach(var scene in new[]{source,query})if(scene.IsValid())
                {foreach(var g in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(g);SceneManager.UnloadSceneAsync(scene);}
                if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
#endif
