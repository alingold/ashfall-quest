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
    public static partial class R26MoveVehicleSmoke
    {
        const string Key="Ashfall.R26MoveVehicle.Smoke";
        static R26MoveVehicleSmoke(){EditorApplication.update+=Pump;}
        public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);R23PlayableBuild.Xr().InitManagerOnStart=false;SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
        static string report="";static int failures;
        static void Need(bool value,string message){Debug.Log((value?"R26 PASS ":"R26 FAIL ")+message);report+=(value?"PASS ":"FAIL ")+message+"\n";if(!value)failures++;}
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
        static void Step(R23VRRig rig,HandSample left,HandSample right,int frames){for(int i=0;i<frames;i++)Sample(rig,left,right,Dt,Jitter());}
        static void Sample(R23VRRig rig,HandSample left,HandSample right,float dt,Vector3 head){rig.FixtureTick(dt);if(locomotionFrames!=null)LocomotionSample(rig,left,right,dt,head);else rig.VehicleSmokeSample(left,right,dt,head);}
        static GameObject Box(string name,Vector3 center,Vector3 size){var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;b.transform.position=center;b.transform.localScale=size;Physics.SyncTransforms();return b;}
        static void Pump()
        {
            if(exitCode>=0){if(!EditorApplication.isPlayingOrWillChangePlaymode){R23PlayableBuild.Xr().InitManagerOnStart=true;EditorApplication.Exit(exitCode);}return;}
            if(!SessionState.GetBool(Key,false)||!Application.isPlaying)return;SessionState.SetBool(Key,false);
            int code=0;
            try{Test();code=failures==0?0:3;}catch(Exception e){Debug.LogException(e);report+="EXCEPTION "+e+"\n";code=2;}
            try{File.WriteAllText(Path.Combine(R23PlayableBuild.Argument("-ashfallR4Output"),"receipt.txt"),report+"failures="+failures+" exit="+code+"\n");}catch(Exception e){Debug.LogException(e);}
            exitCode=code;EditorApplication.isPlaying=false;
        }
        static void RubWall(HandSample left,HandSample right,out int pushes,out int reversals,out float worst,out float travelled)
        {
            var wall=Box("Rub wall",new Vector3(320.55f,1.5f,10),new Vector3(.3f,3,30));
            // Map walls are mesh colliders: swap the primitive box collider for its mesh.
            UnityEngine.Object.DestroyImmediate(wall.GetComponent<BoxCollider>());var meshCollider=wall.AddComponent<MeshCollider>();meshCollider.sharedMesh=wall.GetComponent<MeshFilter>().sharedMesh;Physics.SyncTransforms();
            var rig=Rig(new Vector3(320,0,0));rig.FixtureRunsRecovery=true;rig.CaptureAdvanceTrace=true;Step(rig,left,right,40);
            var stick=left;stick.StickX=.5f;stick.StickY=.87f;int recoveries=rig.HeadRecoveries;
            float lastX=0,lastDelta=0;worst=0;reversals=0;bool touching=false;
            for(int i=0;i<360;i++){
                var swayHead=Jitter();swayHead.x+=Mathf.Min(.1f,i*.002f)+.04f*Mathf.Sin(i*Dt*9.4f);
                Sample(rig,stick,right,Dt,swayHead);
                float x=rig.transform.position.x;
                if(!touching&&x>320.15f)touching=true;
                if(touching&&i>0&&rig.transform.position.z<23){float d=x-lastX;if(lastDelta*d<0&&Mathf.Abs(d)>.01f){reversals++;worst=Mathf.Max(worst,Mathf.Abs(d));}if(Mathf.Abs(d)>.0005f)lastDelta=d;if(Mathf.Abs(d)>.02f)report+="  rub frame "+i+" dx="+d.ToString("F3")+" blocked="+rig.HeadClearanceBlocked+" crouch="+rig.RampCrouchOffset.ToString("F3")+" eye="+rig.Head.transform.position.ToString("F3")+" | "+rig.AdvanceTrace+" | "+rig.VerifiedPath+"\n";}
                lastX=x;
            }
            pushes=rig.HeadRecoveries-recoveries;travelled=rig.transform.position.z;
            UnityEngine.Object.DestroyImmediate(rig.gameObject);UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();
        }
        // R6 overhang matrix: a sloped underside (thin slab) above a floor that
        // is flat or slopes up/down toward it, approached at several headings.
        // Walk in 3 s, walk back out 3 s, stand 1.5 s. Requirements: crouch while
        // under it, stand fully again after leaving, no eye-height glitching, eye
        // never closer than the near clip to the underside, feet stay on the floor.
        static bool traceCase;
        static int OverhangCase(float overhangAngle,float floorSlope,float heading,float x,out string line)
        {
            var pivot=new Vector3(x,0,0);var yaw=Quaternion.Euler(0,heading,0);
            Vector3 floorDir=Quaternion.Euler(-floorSlope,0,0)*Vector3.forward,floorUp=Quaternion.Euler(-floorSlope,0,0)*Vector3.up;
            var floor=Box("Overhang floor",pivot+yaw*(floorDir*14-floorUp*.25f),new Vector3(80,.5f,60));floor.transform.rotation=yaw*Quaternion.Euler(-floorSlope,0,0);
            float floorAtStart=0,floorRise=Mathf.Tan(floorSlope*Mathf.Deg2Rad);
            Vector3 p0=new Vector3(0,2.6f+floorRise*1.5f,1.5f);Vector3 dir=Quaternion.Euler(overhangAngle,0,0)*Vector3.forward,up=Quaternion.Euler(overhangAngle,0,0)*Vector3.up;
            var slab=Box("Overhang underside",pivot+yaw*(p0+dir*5+up*.03f),new Vector3(60,.06f,10));slab.transform.rotation=yaw*Quaternion.Euler(overhangAngle,0,0);
            Physics.SyncTransforms();
            var left=Hand(new Vector3(-.2f,1.1f,.3f));var right=Hand(new Vector3(.2f,1.1f,.3f));
            var rig=Rig(pivot+Vector3.up*(floorAtStart+.02f));rig.FixtureRunsRecovery=true;
            // R7 (playtest 6): the headset runs the standing-height calibration
            // every frame. Earlier fixtures had none and passed while the headset
            // failed, so every case now carries the production calibration.
            var calibration=new GameObject("R7 fixture calibration").AddComponent<Ashfall.Integration.Playable.Calibration.R23VerticalCalibration>();
            calibration.SuppressPersistence=true;calibration.Initialize(rig.TrackingOrigin,rig.Head,rig.Body);rig.Calibration=calibration;
            for(int i=0;i<60;i++)Sample(rig,left,right,Dt,new Vector3(0,1.71f,0));
            var slabCollider=slab.GetComponent<Collider>();float maxOffset=0,minClear=9,maxJump=0;int jitter=0;float lastEye=float.NaN,lastDelta=0;
            float standEye=locomotionFrames==null?rig.Head.transform.position.y-rig.transform.position.y:caseHeadHeight+calibration.Receipt.BaseOriginY;
            Vector3 lean=Vector3.zero;float phaseTop=float.NegativeInfinity;int standFrame=-1,freeFrame=-1,frameIndex=0;bool trackStand=false;
            System.Action<HandSample,int> run=delegate(HandSample stick,int frames){
                for(int i=0;i<frames;i++){
                    Sample(rig,stick,right,Dt,Jitter()+Vector3.up*(caseHeadHeight-1.7f)+lean);
                    Vector3 eye=rig.Head.transform.position;float height=eye.y-rig.transform.position.y;
                    maxOffset=Mathf.Max(maxOffset,rig.RampCrouchOffset);phaseTop=Mathf.Max(phaseTop,height);
                    minClear=Mathf.Min(minClear,Vector3.Distance(eye,slabCollider.ClosestPoint(eye)));
                    if(!float.IsNaN(lastEye)){float d=height-lastEye;maxJump=Mathf.Max(maxJump,Mathf.Abs(d));if(Mathf.Abs(d)>.015f){if(lastDelta*d<0)jitter++;lastDelta=d;if(traceCase)report+="    !jump frame "+frameIndex+" d="+d.ToString("F3")+" eyeH "+height.ToString("F3")+" off "+rig.RampCrouchOffset.ToString("F3")+" root "+rig.transform.position.ToString("F3")+" grounded "+rig.Supported+" unfocused "+rig.FixtureUnfocused+" stickY "+stick.StickY+" lean "+lean.z.ToString("F2")+"\n";}}
                    lastEye=height;frameIndex++;
                    if(trackStand)
                    {
                        // Stand-up latency: from the first frame the full standing column
                        // above the head is free to the first frame back at full height.
                        Vector3 root=rig.transform.position;float r=rig.HeadCollision.EyeSafetyRadius+.015f;
                        bool free=Array.IndexOf(Physics.OverlapCapsule(new Vector3(eye.x,root.y+.6f+r,eye.z),new Vector3(eye.x,root.y+standEye,eye.z),r,~0,QueryTriggerInteraction.Ignore),slabCollider)<0;
                        if(!free){freeFrame=-1;standFrame=-1;}else if(freeFrame<0)freeFrame=frameIndex;
                        if(freeFrame>=0&&standFrame<0&&Mathf.Abs(height-standEye)<.03f)standFrame=frameIndex;
                    }
                    if(traceCase&&(i%12==0||trackStand&&i%3==0))report+="    t"+i+" f"+frameIndex+" free "+freeFrame+" root "+rig.transform.position.ToString("F2")+" eyeH "+height.ToString("F3")+" off "+rig.RampCrouchOffset.ToString("F3")+" grounded "+rig.Supported+" headBlocked "+rig.HeadClearanceBlocked+" crouchBlocked "+rig.RampCrouchBlocked+" unfocused "+rig.FixtureUnfocused+" stickY "+stick.StickY+" lean "+lean.z.ToString("F2")+"\n";
                }
            };
            var forward=left;forward.StickY=1;var back=left;back.StickY=-1;
            run(forward,216);float deepest=maxOffset;
            // Meta/system menu while crouched under it (session unfocused, no input).
            float beforeMenu=rig.Head.transform.position.y-rig.transform.position.y;phaseTop=float.NegativeInfinity;
            rig.FixtureUnfocused=true;run(left,72);rig.FixtureUnfocused=false;float menuUnderRise=phaseTop-beforeMenu;
            if(locomotionFrames!=null){rig.LocomotionProbePaused=true;run(left,36);rig.LocomotionProbePaused=false;run(left,36);for(int i=0;i<72;i++){lean.y=-.3f*Mathf.Sin(Mathf.PI*i/71);run(left,1);}lean=Vector3.zero;}
            // Physical lean forward into it and back (room-scale head motion).
            for(int i=1;i<=36;i++){lean.z=.25f*i/36;run(left,1);}run(left,36);for(int i=35;i>=0;i--){lean.z=.25f*i/36;run(left,1);}
            var slowBack=left;slowBack.StickY=-.35f;trackStand=true;run(slowBack,150);run(left,108);trackStand=false;
            float standSeconds=standFrame<0||freeFrame<0?99:(standFrame-freeFrame)*Dt;
            float finalOffset=rig.RampCrouchOffset,finalEye=rig.Head.transform.position.y-rig.transform.position.y;
            // Meta/system menu in the open: the view must never float above standing height.
            phaseTop=float.NegativeInfinity;rig.FixtureUnfocused=true;run(left,72);rig.FixtureUnfocused=false;float menuOpenTop=phaseTop-standEye;run(left,36);
            RaycastHit under;bool onFloor=Physics.Raycast(rig.transform.position+Vector3.up*.5f,Vector3.down,out under,2f,1,QueryTriggerInteraction.Ignore)&&Mathf.Abs(under.point.y-rig.transform.position.y)<.06f;
            bool crouched=deepest>Mathf.Min(.2f,Mathf.Max(.01f,standEye-Ashfall.Integration.Playable.Exclusion.R25RampCrouch.MinimumEyeHeight)*.5f),stood=finalOffset<.005f&&Mathf.Abs(finalEye-standEye)<.03f&&standSeconds<=.5f,clear=minClear>=.045f,smooth=jitter<=(locomotionFrames==null?2:8),noFloat=menuOpenTop<=.02f&&menuUnderRise<=.05f;
            line=string.Format("overhang {0,2}deg floor {1,3}deg heading {2,2}deg: crouch max {3:F2} m, final {4:F3} m, eye {5:F2}/{6:F2} m, stand-up {11:F2} s, min clear {7:F3} m, height jitter {8}, max frame jump {9:F3} m, menu rise under {12:F3} m / open {13:F3} m, on floor {10}",
                overhangAngle,floorSlope,heading,deepest,finalOffset,finalEye,standEye,minClear,jitter,maxJump,onFloor,standSeconds,menuUnderRise,menuOpenTop);
            UnityEngine.Object.DestroyImmediate(rig.gameObject);UnityEngine.Object.DestroyImmediate(calibration.gameObject);UnityEngine.Object.DestroyImmediate(slab);UnityEngine.Object.DestroyImmediate(floor);Physics.SyncTransforms();
            return (crouched?0:1)|(stood?0:2)|(clear?0:4)|(smooth?0:8)|(onFloor?0:16)|(noFloat?0:32);
        }
        static void OverhangMatrix()
        {
            Directory.CreateDirectory(R23PlayableBuild.Argument("-ashfallR4Output"));
            int cases=0,noCrouch=0,noStand=0,tooClose=0,glitch=0,offFloor=0,floated=0;float x=2000;
            foreach(float overhang in new[]{30f,45f})foreach(float floor in new[]{0f,10f,20f,-10f,-20f})foreach(float heading in new[]{0f,30f,60f})
            {
                string line;int f=OverhangCase(overhang,floor,heading,x,out line);x+=40;cases++;
                if((f&1)!=0)noCrouch++;if((f&2)!=0)noStand++;if((f&4)!=0)tooClose++;if((f&8)!=0)glitch++;if((f&16)!=0)offFloor++;if((f&32)!=0)floated++;
                report+=(f==0?"  ok   ":"  BAD"+f+" ")+line+"\n";
            }
            Need(noCrouch==0,"overhang matrix: crouches under the overhang in every case ("+(cases-noCrouch)+"/"+cases+")");
            Need(noStand==0,"overhang matrix: stands fully within 0.5 s of the space above the head being free ("+(cases-noStand)+"/"+cases+")");
            Need(floated==0,"overhang matrix: system menu never raises the view (open: <= 2 cm; under it: <= 5 cm settling into free space) ("+(cases-floated)+"/"+cases+")");
            Need(tooClose==0,"overhang matrix: eye never within 4.5 cm of the underside ("+(cases-tooClose)+"/"+cases+")");
            Need(glitch==0,"overhang matrix: no eye-height glitching ("+(cases-glitch)+"/"+cases+")");
            Need(offFloor==0,"overhang matrix: feet end on the floor ("+(cases-offFloor)+"/"+cases+")");
        }
        static void Test()
        {
            if(R23PlayableBuild.Argument("-ashfallLocomotion")=="true"){LocomotionMatrix();return;}
            if(R23PlayableBuild.Argument("-ashfallR6Overhang")=="only"){OverhangMatrix();return;}
            string overhangArg=R23PlayableBuild.Argument("-ashfallR6Overhang")??"";
            if(overhangArg.StartsWith("trace")){Directory.CreateDirectory(R23PlayableBuild.Argument("-ashfallR4Output"));traceCase=true;float a=30,b=10,h=0;if(overhangArg.StartsWith("trace:")){var p=overhangArg.Substring(6).Split(',');a=float.Parse(p[0],System.Globalization.CultureInfo.InvariantCulture);b=float.Parse(p[1],System.Globalization.CultureInfo.InvariantCulture);h=float.Parse(p[2],System.Globalization.CultureInfo.InvariantCulture);}string l;int r=OverhangCase(a,b,h,2000,out l);report+=r+" "+l+"\n";return;}
            string output=R23PlayableBuild.Argument("-ashfallR4Output");Directory.CreateDirectory(output);
            report="R26 move/vehicle native smoke. Production rig, native PhysX, synthetic tracking (head jitter 1.2 cm), XR off; not headset acceptance.\n";
            Box("Floor",new Vector3(0,-.5f,0),new Vector3(800,1,800));
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(35,-35,0);RenderSettings.ambientLight=Color.gray;
            var camera=new GameObject("Evidence camera").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.03f;
            var left=Hand(new Vector3(-.2f,1.1f,.3f));var right=Hand(new Vector3(.2f,1.1f,.3f));

            // 1. Gravity while standing still: support removed under a player
            //    whose real head keeps moving slightly, thumbstick untouched.
            {
                var platform=Box("Platform",new Vector3(60,.5f,0),new Vector3(2,1,2));
                var rig=Rig(new Vector3(60,1,0));Step(rig,left,right,72);
                float before=rig.transform.position.y;
                Need(Mathf.Abs(before-1)<.05f,"gravity: settles standing on the 1 m platform (y="+before.ToString("F3")+")");
                UnityEngine.Object.DestroyImmediate(platform);Physics.SyncTransforms();
                Step(rig,left,right,90);
                float after=rig.transform.position.y;
                Need(after<.1f,"gravity: falls to the floor within 1.25 s with no stick input (y "+before.ToString("F3")+" -> "+after.ToString("F3")+")");
                UnityEngine.Object.DestroyImmediate(rig.gameObject);
            }

            // 2. Walking step-up: anything shorter than the chin (eye 1.7 m,
            //    chin 1.58 m) is mounted; taller is refused.
            foreach(float h in new[]{.12f,.3f,.45f,.9f,1.4f,1.55f,1.66f,2.2f})
            {
                float x=100+h*20;var box=Box("Step "+h,new Vector3(x,h*.5f,2.2f),new Vector3(2.4f,h,2.4f));
                var rig=Rig(new Vector3(x,0,0));rig.CaptureAdvanceTrace=true;Step(rig,left,right,40);
                var stick=left;stick.StickY=1;float top=0;
                string trace="";Vector3 lastRoot=rig.transform.position,lastEye=rig.Head.transform.position;
                for(int i=0;i<216;i++){Vector3 pre=rig.transform.position;Sample(rig,stick,right,Dt,Jitter());top=Mathf.Max(top,rig.transform.position.y);
                    if(h>1.6f&&h<2&&Mathf.Abs(rig.transform.position.y-pre.y)>.001f)report+="  frame "+i+" pre "+pre.ToString("F3")+" post "+rig.transform.position.ToString("F3")+" | "+rig.AdvanceTrace+"\n";
                    if(trace==""&&rig.transform.position.y>.1f)trace=" first lift frame "+i+": root "+lastRoot.ToString("F3")+" -> "+rig.transform.position.ToString("F3")+", eye "+lastEye.ToString("F3")+" -> "+rig.Head.transform.position.ToString("F3")+", eye radius "+rig.HeadCollision.EyeSafetyRadius.ToString("F3")+", grounded "+rig.Supported;
                    lastRoot=rig.transform.position;lastEye=rig.Head.transform.position;}
                if(h>1.6f&&h<2)report+="TRACE"+trace+"\n";
                bool mounted=top>h-.03f;bool expect=h<=1.58f;
                Need(mounted==expect,"step-up: "+(h*100).ToString("F0")+" cm obstacle "+(expect?"mounted":"refused")+" (max root y "+top.ToString("F3")+")");
                UnityEngine.Object.DestroyImmediate(rig.gameObject);UnityEngine.Object.DestroyImmediate(box);Physics.SyncTransforms();
            }

            // 2b. Leaning the real head over a ledge between chin and eye height
            //     neither pops the player on top nor sinks the feet.
            {
                var ledge=Box("Ledge 1.66",new Vector3(150,.83f,1.5f),new Vector3(2.4f,1.66f,2.4f));
                var rig=Rig(new Vector3(150,0,0));Step(rig,left,right,40);
                float lo=9,hi=-9;
                for(int i=0;i<90;i++){var head=Jitter();head.z+=Mathf.Min(.55f,i*.02f);Sample(rig,left,right,Dt,head);lo=Mathf.Min(lo,rig.transform.position.y);hi=Mathf.Max(hi,rig.transform.position.y);}
                Need(lo>-.03f&&hi<.03f,"lean: head over a 166 cm ledge keeps the feet on the floor (root y "+lo.ToString("F3")+".."+hi.ToString("F3")+")");
                UnityEngine.Object.DestroyImmediate(rig.gameObject);UnityEngine.Object.DestroyImmediate(ledge);Physics.SyncTransforms();
            }

            // 2c. Floor removed while the real head presses a wall: still falls.
            {
                var platform=Box("Wall-side platform",new Vector3(250,1.5f,0),new Vector3(2,3,2));
                var wall=Box("Side wall",new Vector3(250.75f,5,0),new Vector3(.3f,10,6));
                var rig=Rig(new Vector3(250.3f,3,0));rig.FixtureRunsRecovery=true;Step(rig,left,right,72);
                for(int i=0;i<30;i++){var head=Jitter();head.x+=.25f;Sample(rig,left,right,Dt,head);}
                float before=rig.transform.position.y;
                UnityEngine.Object.DestroyImmediate(platform);Physics.SyncTransforms();
                for(int i=0;i<108;i++){var head=Jitter();head.x+=.25f;Sample(rig,left,right,Dt,head);}
                float after=rig.transform.position.y;
                Need(before>2.9f&&after<.1f,"float: head pressed against a wall does not suspend a falling player (y "+before.ToString("F2")+" -> "+after.ToString("F3")+")");
                UnityEngine.Object.DestroyImmediate(rig.gameObject);UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();
            }

            // 2d. Rubbing along a wall at 30 degrees: no stuck push-outs, no shaking.
            //     Before control: the r4 stuck trigger (12 cm eye sphere).
            {
                int pushes,reversals;float worst,travelled;
                float r5=R23VRRig.StuckEyeRadius,n5=R23VRRig.StuckNudge;R23VRRig.StuckEyeRadius=.12f;R23VRRig.StuckNudge=100;
                RubWall(left,right,out pushes,out reversals,out worst,out travelled);
                report+="BEFORE (r4 12 cm stuck trigger): wall rub push-outs="+pushes+" reversals>1cm="+reversals+" worst="+worst.ToString("F3")+" m travelled="+travelled.ToString("F2")+" m\n";
                R23VRRig.StuckEyeRadius=r5;R23VRRig.StuckNudge=n5;
                RubWall(left,right,out pushes,out reversals,out worst,out travelled);
                Need(pushes==0,"wall rub: no stuck push-outs while sliding along a wall ("+pushes+")");
                Need(reversals<=2&&worst<.02f,"wall rub: no back-and-forth vibration ("+reversals+" reversals > 1 cm, worst "+worst.ToString("F3")+" m)");
                Need(travelled>3.5f,"wall rub: keeps sliding along the wall ("+travelled.ToString("F2")+" m in 5 s)");
            }

            // 3. Walking into a vertical wall never blacks the view.
            {
                var wall=Box("Wall",new Vector3(200,1.5f,2),new Vector3(6,3,.3f));
                var rig=Rig(new Vector3(200,0,0));Step(rig,left,right,40);
                var stick=left;stick.StickY=1;float maxAlpha=0;
                for(int i=0;i<216;i++){Sample(rig,stick,right,Dt,Jitter());if(rig.ViewFade!=null)maxAlpha=Mathf.Max(maxAlpha,rig.ViewFade.Alpha);}
                float gap=1.85f-rig.Head.transform.position.z;
                Need(maxAlpha<=.001f,"wall: view never darkens walking into a wall (max fade alpha "+maxAlpha.ToString("F3")+")");
                Need(gap>.04f&&gap<.6f,"wall: walking stops before the wall (eye gap "+gap.ToString("F3")+" m)");
                camera.transform.SetPositionAndRotation(rig.Head.transform.position,rig.Head.transform.rotation);Capture(camera,Path.Combine(output,"wall-eye-view.png"));
                UnityEngine.Object.DestroyImmediate(rig.gameObject);UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();
            }

            // 4/5. Tank and helicopter: board, move, fire/fly, exit, not sunk.
            foreach(string kind in new[]{"tank","helicopter","hoverbike"})
            {
                float baseX=kind=="tank"?300:kind=="helicopter"?-200:-300;
                Vector3 rest=new Vector3(baseX,kind=="helicopter"?.65f:.55f,0),start=new Vector3(baseX+(kind=="tank"?2.1f:kind=="helicopter"?1.5f:.6f),0,.15f);
                var rig=Rig(start);var row=Row(kind+"/smoke",F(rest),F(rest),kind=="tank"?1800:600,"",1,kind);int fired=0;
                rig.ReadVehicles=delegate{return new[]{row};};rig.ReadVehicleActor=delegate{return "local";};
                rig.MountVehicle=delegate(string id,int hand){row=Row(row.Id,row.Home,row.Rest,row.Health,"local",2,kind);return true;};
                rig.DismountVehicle=delegate(string id,Float3 at,float yaw){row=Row(row.Id,row.Home,at,row.Health,"",3,kind);};
                rig.FireTank=delegate(string id){fired++;return true;};int gunShots=0;rig.FireVehicleGun=delegate(string id,Vector3 o,Vector3 d){gunShots++;return true;};
                MonoBehaviour view=kind=="tank"?(MonoBehaviour)new GameObject("Tank view").AddComponent<R25ArmourPresentation>():kind=="helicopter"?(MonoBehaviour)new GameObject("Helicopter view").AddComponent<R25HelicopterPresentation>():new GameObject("Bike view").AddComponent<R25BikePresentation>();
                if(view is R25ArmourPresentation)((R25ArmourPresentation)view).Initialize();else if(view is R25HelicopterPresentation)((R25HelicopterPresentation)view).Initialize();
                var l=left;var r=Hand(U(R25BikeRider.MountPoint(F(rest),0,kind,1))-start);
                Step(rig,l,r,20);r.Grip=1;Step(rig,l,r,40);r.Grip=0;
                Need(rig.Riding,kind+": mounts from the side handle");
                Step(rig,l,r,90);
                Vector3 seated=rig.transform.position;
                if(kind=="hoverbike")
                {
                    r.Trigger=1;Step(rig,l,r,72);r.Trigger=0;Step(rig,l,r,4);
                    Need(gunShots>=8&&gunShots<=11,"bike: right trigger fires the machine gun at ~10/s ("+gunShots+" in 1 s)");
                    Need(rig.Riding,"bike: the mounting grip does not immediately exit");
                    r.Grip=1;Step(rig,l,r,48);r.Grip=0;
                    Need(!rig.Riding,"bike: hold right grip exits");
                    UnityEngine.Object.DestroyImmediate(view.gameObject);UnityEngine.Object.DestroyImmediate(rig.gameObject);continue;
                }
                Need(Vector3.Distance(new Vector3(seated.x,0,seated.z),new Vector3(rest.x,0,rest.z))<.15f&&Mathf.Abs(seated.y-rest.y)<.2f,kind+": rider glides onto the hull rest pose (root "+seated.ToString("F2")+", rest "+rest.ToString("F2")+")");
                if(kind=="tank")
                {
                    l.StickY=1;Step(rig,l,r,216);l.StickY=0;
                    Vector3 driven=rig.transform.position;float moved=new Vector2(driven.x-seated.x,driven.z-seated.z).magnitude;
                    Need(moved>8,"tank: left stick drives forward (moved "+moved.ToString("F2")+" m in 3 s)");
                    Need(driven.y>.35f&&driven.y<.8f,"tank: rides above the ground, not sunk (root y "+driven.y.ToString("F3")+")");
                    float yaw0=rig.VehicleHullYaw;l.StickX=1;Step(rig,l,r,144);l.StickX=0;
                    float turned=Mathf.Abs(Mathf.DeltaAngle(yaw0,rig.VehicleHullYaw));
                    Need(turned>40,"tank: left stick X steers the hull ("+turned.ToString("F1")+" deg in 2 s)");
                    ((R25ArmourPresentation)view).Show(rig.transform.position,rig.VehicleHullYaw,0,0,1800,0,true,true,-1);
                    camera.transform.position=rig.transform.position+new Vector3(6,2.5f,-6);camera.transform.LookAt(rig.transform.position+Vector3.up*.5f);Capture(camera,Path.Combine(output,"tank-driving.png"));
                    r.Trigger=1;Step(rig,l,r,4);r.Trigger=0;Step(rig,l,r,4);
                    Need(fired==1,"tank: right trigger fires the gun once per pull (fired "+fired+")");
                    l.Click=true;Step(rig,l,r,4);l.Click=false;Step(rig,l,r,4);
                    Need(fired==1,"tank: left stick click no longer fires (fired "+fired+")");
                    Vector3 hull=rig.transform.position;
                    l.Primary=true;Step(rig,l,r,48);l.Primary=false;
                    Need(rig.Riding,"tank: X no longer exits (grip exit only)");
                    r.Grip=1;Step(rig,l,r,48);r.Grip=0;
                    Need(!rig.Riding,"tank: hold right grip exits");
                    Step(rig,l,r,144);
                    Vector3 off=rig.transform.position;
                    Need(new Vector2(off.x-hull.x,off.z-hull.z).magnitude>2,"tank: exit steps the rider off the hull ("+new Vector2(off.x-hull.x,off.z-hull.z).magnitude.ToString("F2")+" m)");
                    Need(off.y<.05f,"tank: rider lands on the ground after exit (y "+off.y.ToString("F3")+")");
                    Need(row.Rest.Y>.4f,"tank: parked hull rest stays above ground, not sunk (rest y "+row.Rest.Y.ToString("F3")+")");
                    // Board again and exit with both grips.
                    Vector3 marker=U(R25BikeRider.MountPoint(row.Rest,0,kind,Vector3.Dot(rig.Head.transform.position-U(row.Rest),Vector3.right)<0?-1:1));
                    var r2=Hand(rig.TrackingOrigin.InverseTransformPoint(marker));
                    Step(rig,l,r2,10);r2.Grip=1;Step(rig,l,r2,40);r2.Grip=0;Step(rig,l,r2,90);
                    Need(rig.Riding,"tank: can board again after exiting");
                    Need(rig.Riding,"tank: the mounting grip does not immediately exit");
                    r2.Grip=1;Step(rig,l,r2,48);r2.Grip=0;
                    Need(!rig.Riding,"tank: release then hold right grip exits again");
                }
                else
                {
                    Step(rig,l,r,110); // rotor spin-up 1.5 s
                    l.Grip=1;Step(rig,l,r,216);l.Grip=0;
                    float climbed=rig.transform.position.y;
                    Need(climbed>8,"helicopter: left grip climbs (root y "+climbed.ToString("F2")+" after 3 s)");
                    Vector3 hover=rig.transform.position;l.StickY=1;Step(rig,l,r,216);l.StickY=0;
                    float moved=new Vector2(rig.transform.position.x-hover.x,rig.transform.position.z-hover.z).magnitude;
                    Need(moved>20,"helicopter: left stick flies forward (moved "+moved.ToString("F2")+" m in 3 s)");
                    ((R25HelicopterPresentation)view).Show(rig.transform.position,rig.transform.eulerAngles.y,0,1,600,true,true,-1,false);
                    camera.transform.position=rig.transform.position+new Vector3(8,1,-8);camera.transform.LookAt(rig.transform.position);Capture(camera,Path.Combine(output,"helicopter-flying.png"));
                    l.Click=true;Step(rig,l,r,600);l.Click=false;Step(rig,l,r,72);
                    float landed=rig.transform.position.y;
                    Need(landed>.45f&&landed<.8f,"helicopter: descends and lands on its skids, not sunk (root y "+landed.ToString("F3")+")");
                    Vector3 hull=rig.transform.position;
                    r.Trigger=1;Step(rig,l,r,72);r.Trigger=0;Step(rig,l,r,4);
                    Need(gunShots>=8&&gunShots<=11,"helicopter: right trigger fires the machine gun at ~10/s ("+gunShots+" in 1 s)");
                    r.Grip=1;Step(rig,l,r,60);r.Grip=0;
                    Need(!rig.Riding,"helicopter: landed + hold right grip exits");
                    Step(rig,l,r,144);
                    Vector3 off=rig.transform.position;
                    Need(new Vector2(off.x-hull.x,off.z-hull.z).magnitude>1.6f&&off.y<.05f,"helicopter: exit steps off and lands on the ground (offset "+new Vector2(off.x-hull.x,off.z-hull.z).magnitude.ToString("F2")+" m, y "+off.y.ToString("F3")+")");
                    Need(row.Rest.Y>.45f,"helicopter: parked rest stays above ground, not sunk (rest y "+row.Rest.Y.ToString("F3")+")");
                    ((R25HelicopterPresentation)view).Show(U(row.Rest),0,0,0,600,false,true,-1,false);
                    camera.transform.position=U(row.Rest)+new Vector3(7,2,-7);camera.transform.LookAt(U(row.Rest));Capture(camera,Path.Combine(output,"helicopter-parked.png"));
                }
                UnityEngine.Object.DestroyImmediate(view.gameObject);UnityEngine.Object.DestroyImmediate(rig.gameObject);
            }
        }
    }
}
#endif
