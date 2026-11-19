#if UNITY_EDITOR && UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Activities;
using Ashfall.Integration.Playable.Climbing;
using Ashfall.Integration.Playable.UI;
using Ashfall.Modules.Flight;
using UnityEngine;
namespace Ashfall.Editor.Playable
{
    // Staged for root's next coordinated native slot. Run explicitly in PlayMode;
    // creates its own actual rig/motor/head hull and native geometry, no live rig edits.
    public static class R23MotionFixSmoke
    {
        static void Need(bool okay,string message){if(!okay)throw new Exception(message);}
        static GameObject Box(Transform parent,Vector3 center,Vector3 size)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.SetParent(parent,false);go.transform.position=center;go.transform.localScale=size;return go;}
        static void CloseHands(R23VRRig rig)
        {rig.Left.localPosition=new Vector3(-.2f,1.2f,0);rig.Right.localPosition=new Vector3(.2f,1.2f,0);}
        static void SpreadHands(R23VRRig rig)
        {rig.Left.localPosition=new Vector3(-.65f,1.2f,0);rig.Right.localPosition=new Vector3(.65f,1.2f,0);}
        static void Warm(R23VRRig rig){for(int i=0;i<10;i++)rig.SmokeXrMove(Vector2.zero,.02f);}
        static void Reset(R23VRRig rig,R23VRActivities activity,Vector3 feet)
        {activity.ResetRound();rig.SetSpawn(feet,feet+Vector3.forward);rig.TrackingOrigin.localPosition=Vector3.zero;rig.Head.transform.localPosition=new Vector3(0,1.7088f,0);CloseHands(rig);Physics.SyncTransforms();Warm(rig);}
        public static void Run(R23PlayableLoop loop)
        {if(loop==null)throw new ArgumentNullException("loop");Run();}
        public static void Run()
        {
            Need(Application.isPlaying,"MotionFix native fixture requires PlayMode");
            var owned=new List<GameObject>();var meshes=new List<Mesh>();R23VRRig rig=null;float volume=AudioListener.volume;
            try
            {
                Vector3 site=new Vector3(4000,300,4000);var map=new GameObject("MotionFix native fixture world");owned.Add(map);Box(map.transform,site+new Vector3(0,-.1f,8),new Vector3(12,.2f,40));
                var actor=new GameObject("MotionFix isolated actual root");owned.Add(actor);rig=actor.AddComponent<R23VRRig>();rig.Initialize(site,site+Vector3.forward);rig.enabled=false;rig.Head.enabled=false;
                foreach(var listener in rig.Head.GetComponents<AudioListener>())listener.enabled=false;
                rig.ApplySettings(new R23UiSettings(walkMultiplier:2.5f));rig.GameplayBlocked=false;
                var adapter=new GameObject("MotionFix actual Flight adapter");owned.Add(adapter);var activity=adapter.AddComponent<R23VRActivities>();activity.Initialize(rig.transform,rig.Head.transform,rig.Left,rig.Right,rig.Body,map.transform);rig.Activities=activity;
                foreach(bool sourceHull in new[]{true,false})
                {
                    rig.UseSourceHeadHull=sourceHull;
                    foreach(float dt in new[]{.005f,.02f})foreach(float height in new[]{.004f,.025f,.12f})
                    {
                        var curb=Box(map.transform,site+new Vector3(0,height*.5f,1),new Vector3(6,height,1));Reset(rig,activity,site);float highest=rig.transform.position.y;
                        for(int i=0;i<(int)(.45f/dt);i++)
                        {
                            rig.SmokeXrMove(Vector2.up,dt);highest=Mathf.Max(highest,rig.transform.position.y);
                            Need(!rig.Supported||Mathf.Abs(rig.MotionVelocity.y)<.00001f,"Supported curb produced launch velocity: hull="+sourceHull+" dt="+dt+" curb="+height+" vy="+rig.MotionVelocity.y);
                        }
                        Need(highest<=site.y+height+.06f,"Tiny curb launched root above authored support: "+highest);
                        curb.SetActive(false);UnityEngine.Object.Destroy(curb);Physics.SyncTransforms();
                    }
                }
                rig.UseSourceHeadHull=true;
                var ramp=new GameObject("MotionFix real26.6deg native ramp");ramp.transform.SetParent(map.transform,false);ramp.transform.position=site+Vector3.forward*.5f;
                var rampMesh=new Mesh();meshes.Add(rampMesh);rampMesh.vertices=new[]{new Vector3(-3,0,0),new Vector3(3,0,0),new Vector3(-3,1.5f,3),new Vector3(3,1.5f,3)};rampMesh.triangles=new[]{0,2,1,2,3,1};rampMesh.RecalculateNormals();rampMesh.RecalculateBounds();ramp.AddComponent<MeshCollider>().sharedMesh=rampMesh;Reset(rig,activity,site);
                for(int i=0;i<32;i++){rig.SmokeXrMove(Vector2.up,.01f);Need(!rig.Supported||rig.MotionVelocity.y==0,"Supported ramp motion accumulated vertical energy");Need(rig.transform.position.y<site.y+1.6f,"Ramp launched root above its actual surface");}
                Need(rig.transform.position.y>site.y+.8f,"Root failed to follow real ramp; feature was disabled instead of fixed");ramp.SetActive(false);UnityEngine.Object.Destroy(ramp);Physics.SyncTransforms();
                Reset(rig,activity,site+Vector3.up*12);SpreadHands(rig);
                float glideY=-Ashfall.Modules.Player.PlayerTuning.WalkSpeed*2.5f*(12f/6.5f)/7;
                for(int i=0;i<90;i++){rig.SmokeXrMove(Vector2.up,.02f);Need(activity.Mode==FlightMode.Glide&&Mathf.Abs(rig.MotionVelocity.y-glideY)<.01f,"Forward spread glide unexpectedly dived or lost controlled descent: tick="+i+" mode="+activity.Mode+" vy="+rig.MotionVelocity.y+" expected="+glideY+" supported="+rig.Supported+" status="+activity.MotionStatus);}
                CloseHands(rig);for(int i=0;i<15;i++){rig.SmokeXrMove(Vector2.up,.02f);Need(activity.Mode==FlightMode.Air&&rig.MotionVelocity.y> -9,"Glide release inherited a fast-fall/dive counter");}
                SpreadHands(rig);rig.SmokeXrMove(Vector2.up,.02f);Need(activity.Mode==FlightMode.Glide&&Mathf.Abs(rig.MotionVelocity.y-glideY)<.01f,"Spread glide reentry failed to recover actual descent: "+rig.MotionVelocity.y);
                Reset(rig,activity,site+Vector3.up*100);CloseHands(rig);for(int i=0;i<150;i++)rig.SmokeXrMove(Vector2.up,.02f);
                // FLIGHT-DIVE r12: 3 s full-forward dive is drag-limited (~32 m/s, belly ~24, terminal 40), no 96 m/s2 snap.
                Need(activity.Mode==FlightMode.Dive&&rig.MotionVelocity.y< -28&&rig.MotionVelocity.y> -Ashfall.Modules.Flight.R26FreefallTuning.DiveTerminalSpeed-.6f,"Genuine arms-folded forward freefall dive was disabled or exceeded drag terminal: vy="+rig.MotionVelocity.y);
                // Back-facing single-sided wall: source hand_wall_r2 rays explicitly
                // request both faces. Proxy must stay on head side; raw pose survives.
                Reset(rig,activity,site);var wall=new GameObject("MotionFix one-sided hand wall");wall.transform.SetParent(map.transform,false);wall.transform.position=site+Vector3.forward;
                var wallMesh=new Mesh();meshes.Add(wallMesh);wallMesh.vertices=new[]{new Vector3(-3,-1,0),new Vector3(3,-1,0),new Vector3(-3,4,0),new Vector3(3,4,0)};wallMesh.triangles=new[]{0,1,2,2,1,3};wallMesh.RecalculateNormals();wallMesh.RecalculateBounds();wall.AddComponent<MeshCollider>().sharedMesh=wallMesh;
                var climbOwner=new GameObject("MotionFix free-hand proxy owner");owned.Add(climbOwner);var climbing=climbOwner.AddComponent<R23VRClimbing>();climbing.Initialize(rig.transform,rig.Head.transform,rig.Left,rig.Right,rig.Body,map.transform);climbing.HeadCollision=rig.HeadCollision;
                rig.Left.localPosition=new Vector3(0,1.2f,2);Vector3 tracked=rig.Left.localPosition;Physics.SyncTransforms();climbing.Evaluate(new InputFrame{Tick=1,Sequence=1,Tracked=true},.02f,true,true,true,true,Vector3.zero);climbing.AcceptMotion(Vector3.zero,.02f,true,false);
                Need(climbing.LeftContactProxy.z<=wall.transform.position.z-.064f,"Free-hand proxy crossed source back-facing wall");Need(rig.Left.localPosition==tracked,"Wall proxy overwrote raw tracked controller pose");
                Debug.Log("R23 MOTION FIX PASS: actual root/head hull+capsule curbs4/25/120mm at5/20ms, real ramp, repeated controlled glide/release/reentry, genuine freefall dive, and backface-safe free hand; no headset claim");
            }
            finally
            {
                foreach(var go in owned)if(go!=null){go.SetActive(false);UnityEngine.Object.Destroy(go);}foreach(var mesh in meshes)if(mesh!=null)UnityEngine.Object.Destroy(mesh);AudioListener.volume=volume;Physics.SyncTransforms();
            }
        }
    }
}
#endif
