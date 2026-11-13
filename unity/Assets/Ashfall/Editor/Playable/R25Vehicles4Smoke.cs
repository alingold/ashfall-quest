#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Integration.Playable;
using Ashfall.Integration.Playable.Vehicles;
using Ashfall.Integration.Playable.Input;
using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Editor.Playable
{
    [InitializeOnLoad]
    public static class R25Vehicles4Smoke
    {
        const string Key="Ashfall.Vehicles4.Smoke";
        static R25Vehicles4Smoke(){EditorApplication.update+=Pump;}
        public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);R23PlayableBuild.Xr().InitManagerOnStart=false;SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static void Need(bool value,string message){if(!value)throw new Exception(message);Debug.Log("VEHICLES4 PASS "+message);}
        static R25VehicleState Row(string id,Float3 home,float hy,Float3 rest,float yaw,float health,string rider,double respawn,long revision,string kind)
        {return (R25VehicleState)Activator.CreateInstance(typeof(R25VehicleState),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{id,home,hy,rest,yaw,health,rider,respawn,revision,kind,0f,0d},null);}
        static Vector3 U(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        static void Capture(Camera camera,string path){var rt=RenderTexture.GetTemporary(1200,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(pixels);}
        static R23VRRig Rig(Vector3 at){var r=new GameObject("Synthetic production rig").AddComponent<R23VRRig>();r.Initialize(at,at+Vector3.forward);r.enabled=false;r.GameplayBlocked=false;r.TraversalInputAllowed=true;return r;}
        static HandSample Hand(Vector3 at){return new HandSample{Tracked=true,Position=F(at)};}
        static int exitCode=-1,mapIndex=-1;static float mapDeadline;
        static Ashfall.Integration.Playable.World.R23PlayableMap map;
        static readonly string[] mapIds={"range","valley","cyber_bird"};
        static void NextMap(){if(map!=null)UnityEngine.Object.DestroyImmediate(map.gameObject);mapIndex++;if(mapIndex>=mapIds.Length){exitCode=0;EditorApplication.isPlaying=false;return;}map=new GameObject("Actual map collision acceptance").AddComponent<Ashfall.Integration.Playable.World.R23PlayableMap>();map.Load(mapIds[mapIndex]);mapDeadline=Time.realtimeSinceStartup+180;}
        static void MapProbe(){
            if(map.LoadError!=null)throw new Exception(map.LoadError);
            if(!map.IsLoaded){if(Time.realtimeSinceStartup>mapDeadline)throw new Exception("Map load timeout");return;}
            var rig=Rig(map.WarmupSpawn);Physics.SyncTransforms();RaycastHit support;
            Need(rig.HeadCollision.Support(map.WarmupSpawn,map.WarmupSpawn+Vector3.up*2,out support),map.MapId+" has native support");
            Vector3 at=support.point+Vector3.up*.55f;
            Vector3 down=rig.HeadCollision.BikeMotion(at,Vector3.forward,Vector3.down*5);
            Need(at.y+down.y>=support.point.y+.28f,map.MapId+" full 5 m downward step cannot tunnel floor");
            for(int heading=0;heading<4;heading++){
                Vector3 forward=Quaternion.Euler(0,heading*90,0)*Vector3.forward;Vector3 position=at;
                for(int n=0;n<30;n++){
                    Vector3 safe=rig.HeadCollision.BikeMotion(position,forward,forward*5+Vector3.down*.05f);position+=safe;
                    Need(!float.IsNaN(position.y),map.MapId+" native finite high-speed path");
                    if(n==0||n==15||n==29){var camera=rig.Head;camera.transform.position=position+Vector3.up*1.5f;camera.transform.rotation=Quaternion.LookRotation(forward);Capture(camera,Path.Combine(R23PlayableBuild.Argument("-ashfallVehicleOutput"),map.MapId+"-"+heading+"-"+n+".png"));}
                    if(safe.magnitude<.02f)break;
                }
            }
            File.AppendAllText(Path.Combine(R23PlayableBuild.Argument("-ashfallVehicleOutput"),"map-receipts.txt"),map.MapId+" manifest "+map.ManifestSha256+" native 5m floor sweep and four headings of 5m swept advances passed; fixture uses chassis queries, not held-weapon gameplay.\n");
            UnityEngine.Object.DestroyImmediate(rig.gameObject);NextMap();
        }
        static void Pump()
        {
            if(exitCode>=0){if(!EditorApplication.isPlayingOrWillChangePlaymode){R23PlayableBuild.Xr().InitManagerOnStart=true;EditorApplication.Exit(exitCode);}return;}
            if(mapIndex>=0&&exitCode<0){try{MapProbe();}catch(Exception e){Debug.LogException(e);exitCode=2;EditorApplication.isPlaying=false;}return;}
            if(!SessionState.GetBool(Key,false)||!Application.isPlaying)return;SessionState.SetBool(Key,false);
            int code=0;
            try{Test();}catch(Exception e){Debug.LogException(e);code=2;}
            if(code==0){foreach(var c in UnityEngine.Object.FindObjectsByType<Collider>())UnityEngine.Object.DestroyImmediate(c.gameObject);NextMap();}else{exitCode=code;EditorApplication.isPlaying=false;}
        }
        static void Test()
        {
            string output=R23PlayableBuild.Argument("-ashfallVehicleOutput");Directory.CreateDirectory(output);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Synthetic seam terrain";floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(600,1,600);
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(35,-35,0);RenderSettings.ambientLight=Color.gray;
            var camera=new GameObject("Evidence camera").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.03f;
            var report="Production rig, native PhysX, synthetic tracking; not headset acceptance.\n";
            var runner=Rig(new Vector3(20,0,0));var runLeft=Hand(new Vector3(-.2f,1.1f,.3f));var runRight=Hand(new Vector3(.2f,1.1f,.3f));
            for(int i=0;i<20;i++)runner.VehicleSmokeSample(runLeft,runRight,1f/72);
            runLeft.StickY=1;runLeft.Click=true;runner.VehicleSmokeSample(runLeft,runRight,1f/72);runLeft.Click=false;runner.VehicleSmokeSample(runLeft,runRight,1f/72);runLeft.Click=true;runner.VehicleSmokeSample(runLeft,runRight,1f/72);runLeft.Click=false;
            for(int i=0;i<30;i++)runner.VehicleSmokeSample(runLeft,runRight,1f/72);
            float sprint=new Vector2(runner.MotionVelocity.x,runner.MotionVelocity.z).magnitude;Need(sprint>16&&sprint<17,"production rig tier-two sprint measured");report+="Sprint accepted speed "+sprint+" m/s\n";UnityEngine.Object.DestroyImmediate(runner.gameObject);
            foreach(string kind in new[]{"tank","helicopter","hoverbike"}){
                Vector3 rest=new Vector3(0,.55f,0),start=new Vector3(kind=="tank"?2.1f:kind=="helicopter"?1.5f:.6f,0,.15f);
                var rig=Rig(start);var row=Row("smoke",F(rest),0,F(rest),0,kind=="tank"?1800:kind=="helicopter"?600:300,"",0,1,kind);
                rig.ReadVehicles=delegate{return new[]{row};};rig.ReadVehicleActor=delegate{return "local";};rig.MountVehicle=delegate(string id,int h){row=Row(row.Id,row.Home,0,row.Rest,0,row.Health,"local",0,2,kind);return true;};
                var left=Hand(new Vector3(-.2f,1.1f,.3f));var right=Hand(U(R25BikeRider.MountPoint(F(rest),0,kind,1))-start);
                Physics.SyncTransforms();for(int i=0;i<20;i++)rig.VehicleSmokeSample(left,right,1f/72);
                right.Grip=1;for(int i=0;i<40;i++)rig.VehicleSmokeSample(left,right,1f/72);
                Need(rig.Riding,kind+" natural side reach + grip mounts through production EvaluateVehicle");
                right.Grip=0;right.Primary=true;rig.TraversalInputAllowed=false;
                for(int i=0;i<12;i++)rig.VehicleSmokeSample(left,right,1f/72);
                Need(rig.Riding,kind+" remains mounted while A/wheel disables traversal");
                bool toggled=false;var wheel=new GameObject("Production A wheel").AddComponent<R23WeaponWheel>();wheel.Initialize(rig.Head.transform,delegate(string id,int hand){return true;},null,delegate{toggled=true;});
                var aim=new Ray(rig.Right.position,Vector3.forward);wheel.Step(1f/72,false,true,aim,rig.LocomotionAnchorPosition);
                for(int i=0;i<4;i++)wheel.Step(1f/72,true,true,aim,rig.LocomotionAnchorPosition);
                wheel.Step(1f/72,false,true,aim,rig.LocomotionAnchorPosition);Need(toggled&&rig.Riding,kind+" production A tap dispatches held-item toggle while riding");UnityEngine.Object.DestroyImmediate(wheel.gameObject);
                if(kind=="tank"){var view=new GameObject("Tank").AddComponent<R25ArmourPresentation>();view.Initialize();view.Show(rest,0,0,0,1800,0,false,true,-1);}
                if(kind=="helicopter"){var view=new GameObject("Helicopter").AddComponent<R25HelicopterPresentation>();view.Initialize();view.Show(rest+Vector3.forward*8,0,0,0,600,false,true,-1,false);}
                if(kind=="hoverbike"){
                    right.Primary=false;left.StickY=1;for(int i=0;i<400;i++)rig.VehicleSmokeSample(left,right,1f/72);
                    float speed=new Vector2(rig.MotionVelocity.x,rig.MotionVelocity.z).magnitude;Need(speed>39.5f&&speed<40.5f,"actual accepted flat bike speed 40 m/s");report+="Bike accepted speed "+speed+" m/s\n";
                    left.Click=true;for(int i=0;i<80;i++)rig.VehicleSmokeSample(left,right,1f/72);
                    float boost=new Vector2(rig.MotionVelocity.x,rig.MotionVelocity.z).magnitude;Need(boost>49.5f&&boost<50.5f,"actual accepted boost 50 m/s");report+="Boost accepted speed "+boost+" m/s\n";
                }
                UnityEngine.Object.DestroyImmediate(rig.gameObject);
            }
            var probe=Rig(new Vector3(30,.55f,0));
            foreach(float angle in new[]{0f,20f,-20f}){
                var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Thin seam / ramp test";obstacle.transform.position=new Vector3(30,.9f,3);obstacle.transform.localScale=new Vector3(8,2,.02f);obstacle.transform.rotation=Quaternion.Euler(angle,0,0);Physics.SyncTransforms();
                Vector3 safe=probe.HeadCollision.BikeMotion(new Vector3(30,.55f,0),Vector3.forward,Vector3.forward*5);
                Need(safe.z>0&&safe.z<3,"50 m/s x 100 ms full-step sweep stops at thin seam angle "+angle);report+="Thin seam "+angle+" accepted step "+safe+" m\n";UnityEngine.Object.DestroyImmediate(obstacle);
            }
            UnityEngine.Object.DestroyImmediate(probe.gameObject);
            camera.transform.position=new Vector3(5,2.1f,4);camera.transform.LookAt(new Vector3(0,.8f,0));Capture(camera,Path.Combine(output,"tank-boarding.png"));
            camera.transform.position=new Vector3(4,2,11);camera.transform.LookAt(new Vector3(0,.9f,8));Capture(camera,Path.Combine(output,"helicopter-boarding.png"));
            File.WriteAllText(Path.Combine(output,"receipt.txt"),report+"Mount and A rider interlocks passed for all three kinds.\n");
        }
    }
}
#endif

