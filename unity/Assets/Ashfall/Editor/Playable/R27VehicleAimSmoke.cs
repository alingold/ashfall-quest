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
using Ashfall.Integration.Playable.Networking;
namespace Ashfall.Editor.Playable
{
    // R4 (playtest 3) native acceptance: gravity while standing still with a
    // moving real head, walking step-up to chin height, no black view at walls,
    // and tank/helicopter boarding, driving, flying, firing and exit.
    // Production rig + native PhysX + synthetic tracking; not headset acceptance.
    [InitializeOnLoad]
    public static class R27VehicleAimSmoke
    {
        const string Key="Ashfall.R27VehicleAim.Smoke";
        static R27VehicleAimSmoke(){EditorApplication.update+=Pump;}
        public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);R23PlayableBuild.Xr().InitManagerOnStart=false;SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static string report="";static int failures;
        static void Need(bool value,string message){Debug.Log((value?"R27 PASS ":"R27 FAIL ")+message);report+=(value?"PASS ":"FAIL ")+message+"\n";if(!value)failures++;}
        static R25VehicleState Row(string id,Float3 home,Float3 rest,float health,string rider,long revision,string kind)
        {return (R25VehicleState)Activator.CreateInstance(typeof(R25VehicleState),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{id,home,0f,rest,0f,health,rider,0d,revision,kind,0f,0d},null);}
        static Vector3 U(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}
        static void Capture(Camera camera,string path){var rt=RenderTexture.GetTemporary(1200,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(pixels);}
        static R23VRRig Rig(Vector3 at){var r=new GameObject("Synthetic production rig").AddComponent<R23VRRig>();r.Initialize(at,at+Vector3.forward);r.enabled=false;r.GameplayBlocked=false;r.TraversalInputAllowed=true;return r;}
        static HandSample Hand(Vector3 at){return new HandSample{Tracked=true,Position=F(at)};}
        static int exitCode=-1;
        const float Dt=1f/72;
        static float clock;
        static Vector3 Jitter(){clock+=Dt;return new Vector3(.012f*Mathf.Sin(clock*7.1f),1.7f+.006f*Mathf.Sin(clock*4.3f),.012f*Mathf.Cos(clock*5.7f));}
        static void Step(R23VRRig rig,HandSample left,HandSample right,int frames){for(int i=0;i<frames;i++)rig.VehicleSmokeSample(left,right,Dt,Jitter());}
        static GameObject Box(string name,Vector3 center,Vector3 size){var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;b.transform.position=center;b.transform.localScale=size;Physics.SyncTransforms();return b;}
        static void Pump()
        {
            if(exitCode>=0){if(!EditorApplication.isPlayingOrWillChangePlaymode){R23PlayableBuild.Xr().InitManagerOnStart=true;EditorApplication.Exit(exitCode);}return;}
            if(!SessionState.GetBool(Key,false)||!Application.isPlaying)return;SessionState.SetBool(Key,false);
            int code=0;
            try{Test();code=failures==0?0:3;}catch(Exception e){Debug.LogException(e);report+="EXCEPTION "+e+"\n";code=2;}
            try{File.WriteAllText(Path.Combine(R23PlayableBuild.Argument("-ashfallVehicleAimOutput"),"receipt.txt"),report+"failures="+failures+" exit="+code+"\n");}catch(Exception e){Debug.LogException(e);}
            exitCode=code;EditorApplication.isPlaying=false;
        }
        static object Call(object target,string method,params object[] args)
        {return target.GetType().GetMethod(method,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(target,args);}
        static void Set(object target,string field,object value)
        {target.GetType().GetField(field,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(target,value);}
        static void Test()
        {
            string output=R23PlayableBuild.Argument("-ashfallVehicleAimOutput");Directory.CreateDirectory(output);
            report="Production rig, offline session and native PhysX; synthetic hands, XR off.\n";
            Box("Floor",new Vector3(0,-.5f,0),new Vector3(800,1,800));
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.ambientLight=Color.gray;
            var camera=new GameObject("Evidence camera").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.03f;camera.farClipPlane=300;
            foreach(string kind in new[]{"tank","helicopter","hoverbike"}){
                var rig=Rig(new Vector3(0,kind=="helicopter"?.65f:.55f,0));
                var loop=new GameObject("Fixture loop").AddComponent<R23PlayableLoop>();loop.enabled=false;
                typeof(R23PlayableLoop).GetProperty("Rig").GetSetMethod(true).Invoke(loop,new object[]{rig});
                var session=new GameObject("Fixture session").AddComponent<R24LanSession>();session.enabled=false;Set(session,"root",loop);
                var row=Row(kind+"/smoke",F(rig.transform.position),F(rig.transform.position),600,"local",1,kind);
                var rows=(System.Collections.Generic.List<R25VehicleState>)typeof(R24LanSession).GetField("localBikes",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(session);rows.Add(row);
                rig.ReadVehicles=delegate{return rows.ToArray();};rig.ReadVehicleActor=delegate{return "local";};
                rig.DismountVehicle=delegate(string id,Float3 at,float yaw){Call(session,"DismountBike",id,at,yaw);};
                var left=Hand(new Vector3(-.2f,1.1f,.3f));var right=Hand(new Vector3(.2f,1.1f,.3f));
                Set(rig,"vehicleExitPending",false);Step(rig,left,right,100);Need(rig.Riding,kind+" fixture riding");Set(session,"bikeRows",rows.ToArray());
                if(kind=="tank"){
                    var view=new GameObject("Tank evidence").AddComponent<R25ArmourPresentation>();view.Initialize();view.Show(rig.transform.position,0,0,0,1800,0,true,true,-1);
                    Call(session,"TickVehicleAim");
                    Need(GameObject.Find("R5 vehicle aim reticle")!=null&&GameObject.Find("R5 tank shell arc")==null,"Tank shows dot without arc object");
                    var dot=GameObject.Find("R5 vehicle aim reticle");Need(dot.transform.position.y<.1f,"Tank dot at predicted floor impact");
                    camera.transform.position=new Vector3(7,5,-7);camera.transform.LookAt(new Vector3(0,0,12));Capture(camera,Path.Combine(output,"tank-dot.png"));
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
                }else{
                    MonoBehaviour view=kind=="helicopter"?(MonoBehaviour)new GameObject("Helicopter evidence").AddComponent<R25HelicopterPresentation>():new GameObject("Bike evidence").AddComponent<R25BikePresentation>();
                    if(kind=="helicopter")((R25HelicopterPresentation)view).Initialize();else ((R25BikePresentation)view).Initialize();
                    rig.transform.rotation=Quaternion.Euler(0,30,0);
                    Ray initial=rig.VehicleGunRay;rig.Right.position+=new Vector3(2,1,-2);rig.Right.rotation=Quaternion.Euler(-40,110,0);
                    Ray moved=rig.VehicleGunRay;Need(Vector3.Distance(initial.origin,moved.origin)<.001f&&Vector3.Angle(initial.direction,moved.direction)<.001f,kind+" gun unaffected by moved/rotated hand");
                    Vector3 expected=Quaternion.Euler(0,30,0)*Quaternion.Euler(kind=="helicopter"?5:0,0,0)*Vector3.forward;
                    Need(Vector3.Angle(expected,moved.direction)<.01f&&Vector3.Distance(moved.origin,rig.transform.position)<2,kind+" muzzle on hull and direction follows hull yaw");
                    int shots=0;rig.FireVehicleGun=delegate(string id,Vector3 o,Vector3 d){shots++;return (bool)Call(session,"FireVehicleGunLocal",id,o,d);};
                    right.Trigger=1;Step(rig,left,right,72);right.Trigger=0;Need(shots>=9&&shots<=10,kind+" cadence "+shots+" shots/s");
                    if(kind=="helicopter"){
                        var helicopter=(R25HelicopterPresentation)view;
                        helicopter.Show(rig.transform.position,30,0,0,600,true,true,-1,false,1);
                        Call(session,"TickVehicleAim");
                        camera.transform.position=rig.transform.position+new Vector3(7,2,6);camera.transform.LookAt(rig.transform.position+Vector3.forward*2);Capture(camera,Path.Combine(output,"helicopter-firing.png"));
                        Call(session,"ClearVehicleAim");
                        // Destroy is deferred until end of frame; this synchronous fixture renders immediately.
                        var oldEffects=session.GetComponentsInChildren<Transform>();
                        foreach(var effect in oldEffects)if(effect!=null&&(effect.name=="R5 vehicle gun tracer"||effect.name=="R5 vehicle gun impact"))UnityEngine.Object.DestroyImmediate(effect.gameObject);
                        // Start at 15m after boarding; motor and ordinary gravity remain production paths.
                        rig.transform.position=new Vector3(0,15,0);helicopter.Show(rig.transform.position,30,0,0,600,true,true,-1,false,1);
                        right.Grip=1;Step(rig,left,right,46);right.Grip=0;
                        Need(!rig.Riding,"Empty right grip exits helicopter at 15m");
                        row=rows[0];Need(Mathf.Abs(row.Rest.Y-.65f)<.001f,"Offline rest directly below at ground + .65m");
                        float startY=helicopter.transform.position.y,midY=0,endY=0;
                        for(int i=0;i<180;i++){
                            helicopter.Show(U(row.Rest),30,0,0,600,false,true,-1,false,-1,Dt);
                            if(i==0){Need(helicopter.transform.position.y>startY-.1f,"No instant ground snap");}
                            if(i==71){midY=helicopter.transform.position.y;
                                camera.transform.position=new Vector3(12,12,12);camera.transform.LookAt(new Vector3(0,10,0));Capture(camera,Path.Combine(output,"helicopter-falling-middle.png"));
                            }
                            Step(rig,left,right,1);
                        }
                        endY=helicopter.transform.position.y;
                        Need(midY>.65f&&midY<startY-2&&Mathf.Abs(endY-.65f)<.001f,"Accelerating empty descent takes >1s and reaches ground: "+startY+" / "+midY+" / "+endY);
                        Need(rig.transform.position.y<.05f,"Airborne rider falls normally to floor y="+rig.transform.position.y);
                        camera.transform.position=U(row.Rest)+new Vector3(7,3,6);camera.transform.LookAt(U(row.Rest));Capture(camera,Path.Combine(output,"helicopter-parked.png"));
                    }
                    if(kind=="hoverbike"){
                        ((R25BikePresentation)view).Show(rig.transform.position,30,0,0,300,true,-1,true,"");
                        Call(session,"TickVehicleAim");camera.transform.position=rig.transform.position+new Vector3(3,2,4);camera.transform.LookAt(rig.transform.position);Capture(camera,Path.Combine(output,"bike-firing.png"));
                    }
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
                }
                Call(session,"ClearVehicleAim");rig.DismountVehicle=null;
                UnityEngine.Object.DestroyImmediate(session.gameObject);UnityEngine.Object.DestroyImmediate(loop.gameObject);UnityEngine.Object.DestroyImmediate(rig.gameObject);
            }
        }
    }
}
#endif
