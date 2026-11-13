#if UNITY_EDITOR
using System;
using Ashfall.Core;
using Ashfall.Modules.Climbing;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Climbing;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    public static class R23HeadHullSmoke
    {
        static void Need(bool okay,string reason){if(!okay)throw new Exception(reason);}
        static GameObject Box(Transform root,string name,Vector3 local,Vector3 size)
        {var result=GameObject.CreatePrimitive(PrimitiveType.Cube);result.name=name;result.transform.SetParent(root,false);result.transform.localPosition=local;result.transform.localScale=size;return result;}
        static Float3 F(Vector3 v){return new Float3(v.x,v.y,v.z);}
        public static void Run(R23PlayableLoop loop)
        {
            var fixture=new GameObject("Editor-only source head geometry fixture");fixture.transform.position=new Vector3(3000,0,0);
            var actor=new GameObject("Editor-only source head actor");actor.transform.SetParent(fixture.transform,false);
            var eye=new GameObject("Tracked head fixture").transform;eye.SetParent(actor.transform,false);eye.localPosition=Vector3.up*1.7f;
            var query=actor.AddComponent<R23HeadCollision>();query.Initialize(actor.transform,eye);
            Box(fixture.transform,"Support floor",new Vector3(0,-.1f,0),new Vector3(12,.2f,12));
            Box(fixture.transform,"Source low ledge",new Vector3(0,.5f,1),Vector3.one);
            var ceiling=Box(fixture.transform,"Source ceiling",new Vector3(0,2.1f,0),new Vector3(8,.2f,8));
            var wall=Box(fixture.transform,"Source slide wall",new Vector3(0,2,3),new Vector3(8,4,.2f));
            bool prior=loop.Rig.UseSourceHeadHull;
            try
            {
                Physics.SyncTransforms();var shape=R23HeadCollision.CreateSourceMesh();
                Need(shape.vertexCount==168&&Mathf.Abs(shape.bounds.min.y)<.00001f&&Mathf.Abs(shape.bounds.max.y-.12f)<.00001f,"Source hull shape dimensions changed");UnityEngine.Object.Destroy(shape);
                Vector3 feet=actor.transform.position,head=eye.position;bool hitCeiling;
                Vector3 across=query.Sweep(head,Vector3.forward*2,true,out hitCeiling);
                Need(across.z>1.99f,"Invisible torso blocked a source-head-clear ledge");
                Vector3 lift=query.Sweep(head,Vector3.up*.8f,false,out hitCeiling);
                Need(hitCeiling&&lift.y>.15f&&lift.y<.185f,"Actual source convex hull failed ceiling clipping");
                Vector3 slide=query.Sweep(head,new Vector3(1,0,4),true,out hitCeiling);
                Need(slide.x>.99f&&slide.z<2.79f&&slide.z>2.7f,"Source convex wall slide failed");
                Vector3 descent=query.LandingMotion(feet,head,Vector3.down*10);Need(Mathf.Abs(descent.y)<.0001f,"Fast fall passed the standing foot plane");
                Vector3 correction;Need(query.ResolveSupport(feet,head,Vector3.zero,.02f,.02f,false,out correction)&&correction.sqrMagnitude<.000001f,"Source floor support failed");
                ceiling.SetActive(false);wall.SetActive(false);eye.localPosition=new Vector3(0,1.003f,.6f);Physics.SyncTransforms();
                var body=actor.AddComponent<CharacterController>();var left=new GameObject("left").transform;left.SetParent(actor.transform,false);var right=new GameObject("right").transform;right.SetParent(actor.transform,false);
                var climb=actor.AddComponent<R23VRClimbing>();climb.Initialize(actor.transform,eye,left,right,body,fixture.transform);climb.HeadCollision=query;
                Float3[] route;Need(MantlePlanner.TryPlan(climb,F(feet),F(eye.position),F(feet+new Vector3(0,1,.5f)),1.7f,R23HeadCollision.FloorNormal,out route),"Genuine head-only ledge mantle preflight failed");
                Need(route.Length==1&&Mathf.Abs(route[0].Y-1)<.005f,"Source ledge standing route changed");
                Need(query.Clear(eye.position,Vector3.up)&&Mathf.Abs(query.Sweep(eye.position,Vector3.up,false,out hitCeiling).y-1)<.002f,"Preflighted source mantle route could not be swept");
                fixture.SetActive(false);loop.Rig.UseSourceHeadHull=true;R23ActivitySmoke.Run(loop);
                Debug.Log("R23 SOURCE HEAD PASS: 168-point hull, no invisible torso, ceiling, wall slide, fast landing, support, actual mantle route and composed rig construction/climb/fling/glide");
            }
            finally{loop.Rig.UseSourceHeadHull=prior;fixture.SetActive(false);UnityEngine.Object.Destroy(fixture);loop.Rig.ResetPose();}
        }
    }
}
#endif
