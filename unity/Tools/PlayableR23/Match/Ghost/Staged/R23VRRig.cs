#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Player;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using Ashfall.Integration.Playable.Activities;
using Ashfall.Integration.Playable.UI;
using Ashfall.Integration.Playable.Climbing;
using Ashfall.Integration.Playable.Calibration;
using InputDevice=UnityEngine.XR.InputDevice;
namespace Ashfall.Integration.Playable
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class R23VRRig:MonoBehaviour
    {
        public Camera Head {get;private set;}
        public Transform TrackingOrigin {get;private set;}
        public Transform Left {get;private set;}
        public Transform Right {get;private set;}
        public bool Tracking {get;private set;}
        public bool DisplayRunning {get;private set;}
        public bool Paused {get;private set;}
        public float DistanceMoved {get;private set;}
        public string TrackingStatus {get;private set;}
        public string InputStatus {get;private set;}
        public HandSample LeftInput {get;private set;}
        public HandSample RightInput {get;private set;}
        public bool SessionFocused {get;private set;}
        public Vector3 LeftAimPosition {get;private set;}public Vector3 RightAimPosition {get;private set;}
        public Quaternion LeftAimRotation {get;private set;}public Quaternion RightAimRotation {get;private set;}
        public bool LeftAimTracked {get;private set;}public bool RightAimTracked {get;private set;}
        public bool GameplayBlocked;
        public bool ReservePrimaryMarkTurn;
        public bool RightHandOccupied;
        public bool LeftHandOccupied;
        public bool LeftObjectiveGripReserved,RightObjectiveGripReserved;
        public bool LeftInputReady {get{return input.LeftReady;}}
        public bool RightInputReady {get{return input.RightReady;}}
        // Resolve the original fresh objective grip before the same hand can
        // acquire a wall anchor. The root dispatches the selected target later.
        public System.Action BeforeHandInteractions;
        // Source equipped-item movement is separate from the user's saved walk setting.
        public System.Func<float> ReadItemMovementMultiplier;
        public int PrimaryHand=1;
        // Native source-hull movement, mantle, climb/glide and authored-pad fixtures passed.
        public bool UseSourceHeadHull=true;
        public R23HeadCollision HeadCollision {get;private set;}
        public R23VRActivities Activities;
        public R23VRClimbing Climbing;
        public R23VerticalCalibration Calibration;
        public CharacterController Body {get{return body;}}
        public Vector3 MotionVelocity {get{return motor==null?Vector3.zero:U(motor.Velocity);}}
        public bool Supported {get{return grounded;}}
        public InputFrame LastFrame {get;private set;}
        public float Stamina {get{return motor==null?0:motor.SprintStamina;}}
        public float StaminaMaximum {get{return motor==null?0:motor.StaminaMaximum;}}
        public int SprintTier {get{return motor==null?0:motor.SprintTier;}}
        public Vector3 LocomotionAnchorPosition {get{return transform.position-artificialOriginOffset;}}
        public float RefillStamina(){return motor==null?0:motor.RestoreStamina(Mathf.Min(6,Mathf.Max(0,StaminaMaximum-Stamina)));}
        public event System.Action MenuPressed;
        public event System.Action PingPressed;
        public bool TryLaunchPad(Vector3 normal,float speed)
        {
            if(motor==null||Activities==null||!LastFrame.Tracked||LastFrame.Paused||!new Float3(normal.x,normal.y,normal.z).IsFinite||float.IsNaN(speed)||float.IsInfinity(speed)||speed<=0||speed>1000)return false;
            Vector3 velocity;
            if(!Activities.TryLaunchPad(normal,speed,true,Climbing!=null&&(Climbing.LeftAnchored||Climbing.RightAnchored),Climbing!=null&&Climbing.Mantling,out velocity))return false;
            motor.SetVelocity(F(velocity));grounded=false;ceiling=false;return true;
        }
        readonly Ashfall.Modules.Player.PlayerInput input=new Ashfall.Modules.Player.PlayerInput();
        readonly MovementHeading movementHeading=new MovementHeading();
        readonly List<XRDisplaySubsystem> displays=new List<XRDisplaySubsystem>();
        readonly List<XRInputSubsystem> inputs=new List<XRInputSubsystem>();
        CharacterController body;Locomotion motor;PlayerTuning tuning;long tick;bool handHeading;
        bool ceiling,grounded,floorRequested,menuHeld,menuArmed,pingHeld,pingArmed,lastLoggedBlock;float nextInputLog;float firstFocus=-1;
        Vector3 spawn;Quaternion spawnRotation;
        Vector3 artificialOriginOffset;
        Vector3 previousSourceHead;bool haveSourceHead;
        sealed class Speed:IPlayerSettingsPort
        {
            readonly R23VRRig owner;public Speed(R23VRRig owner){this.owner=owner;}
            public ForwardReference ForwardReference{get{return ForwardReference.Head;}}
            public float GroundSpeedGain(float source){float gain=owner.ReadItemMovementMultiplier==null?1:owner.ReadItemMovementMultiplier();return source*(float.IsNaN(gain)||float.IsInfinity(gain)||gain<=0||gain>4?1:gain);}
        }
        public void Initialize(Vector3 location,Vector3 lookAt)
        {
            spawn=location;Vector3 facing=lookAt-location;facing.y=0;spawnRotation=facing.sqrMagnitude>.01f?Quaternion.LookRotation(facing):Quaternion.identity;
            body=gameObject.AddComponent<CharacterController>();body.height=1.75f;body.radius=.25f;body.center=new Vector3(0,.875f,0);body.skinWidth=.025f;body.stepOffset=.25f;body.minMoveDistance=0;body.slopeLimit=65;
            gameObject.layer=10;
            Physics.IgnoreLayerCollision(10,World.R23PlayableMap.FootProbeOnlyLayer,true);Physics.IgnoreLayerCollision(2,World.R23PlayableMap.FootProbeOnlyLayer,true);
            var origin=new GameObject("Tracking origin").transform;origin.SetParent(transform,false);
            TrackingOrigin=origin;
            var camera=new GameObject("Head");camera.tag="MainCamera";camera.layer=10;camera.transform.SetParent(origin,false);camera.transform.localPosition=new Vector3(0,1.65f,0);
            Head=camera.AddComponent<Camera>();Head.nearClipPlane=.04f;Head.farClipPlane=2500;Head.backgroundColor=new Color(.36f,.44f,.56f);
            camera.AddComponent<AudioListener>();
            Left=new GameObject("Left tracked controller").transform;Left.SetParent(origin,false);Left.gameObject.layer=10;
            Right=new GameObject("Right tracked controller").transform;Right.SetParent(origin,false);Right.gameObject.layer=10;
            HeadCollision=new GameObject("R23 head collision queries").AddComponent<R23HeadCollision>();HeadCollision.transform.SetParent(transform,false);HeadCollision.Initialize(transform,Head.transform);
            tuning=new PlayerTuning();motor=new Locomotion(tuning);motor.Settings=new Speed(this);
            ResetPose();Application.onBeforeRender+=TrackPoses;
        }
        void TrackPoses()
        {
            // Application.onBeforeRender also invokes disabled subscribers.
            // A disabled rig must not overwrite owned poses between frames.
            if(!isActiveAndEnabled||Head==null)return;
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            Vector3 p;Quaternion q;bool tracked;
            Tracking=head.isValid&&head.TryGetFeatureValue(CommonUsages.isTracked,out tracked)&&tracked;
            if(Tracking)
            {
                if(head.TryGetFeatureValue(CommonUsages.devicePosition,out p))Head.transform.localPosition=p;
                if(head.TryGetFeatureValue(CommonUsages.deviceRotation,out q))Head.transform.localRotation=q;
            }
            Pose(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand),Left);
            Pose(InputDevices.GetDeviceAtXRNode(XRNode.RightHand),Right);
        }
        static void Pose(InputDevice device,Transform node)
        {Vector3 p;Quaternion q;if(device.isValid&&device.TryGetFeatureValue(CommonUsages.devicePosition,out p))node.localPosition=p;if(device.isValid&&device.TryGetFeatureValue(CommonUsages.deviceRotation,out q))node.localRotation=q;}
        HandSample Hand(InputDevice device,Transform node)
        {
            bool tracked,primary,secondary,click,menu;float grip,trigger;Vector2 axis;
            device.TryGetFeatureValue(CommonUsages.isTracked,out tracked);device.TryGetFeatureValue(CommonUsages.primaryButton,out primary);device.TryGetFeatureValue(CommonUsages.secondaryButton,out secondary);device.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out click);device.TryGetFeatureValue(CommonUsages.menuButton,out menu);
            device.TryGetFeatureValue(CommonUsages.grip,out grip);device.TryGetFeatureValue(CommonUsages.trigger,out trigger);device.TryGetFeatureValue(CommonUsages.primary2DAxis,out axis);
            return new HandSample{Tracked=device.isValid&&tracked,Primary=primary,Secondary=secondary,Click=click,Menu=menu,Grip=grip,Trigger=trigger,StickX=axis.x,StickY=axis.y,Position=F(transform.InverseTransformPoint(node.position))};
        }
        void Update()
        {
            if(body==null)return;
            displays.Clear();SubsystemManager.GetSubsystems(displays);DisplayRunning=false;foreach(var d in displays)DisplayRunning|=d.running;
            if(!floorRequested&&DisplayRunning){inputs.Clear();SubsystemManager.GetSubsystems(inputs);foreach(var i in inputs)floorRequested|=i.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);}
            TrackPoses();
            if(UseSourceHeadHull&&Tracking&&!CarrierActive)SynchronizeSourceHead();
            if(Climbing!=null)Climbing.HeadCollision=UseSourceHeadHull?HeadCollision:null;
            if(Calibration!=null)Calibration.HeadCollision=UseSourceHeadHull?HeadCollision:null;
            SessionFocused=DisplayRunning&&OpenXRUtility.IsSessionFocused;
            if(Calibration!=null&&Time.unscaledDeltaTime>0)Calibration.Tick(Tracking,SessionFocused,false,Mathf.Min(Time.unscaledDeltaTime,.1f));
            var left=Hand(InputDevices.GetDeviceAtXRNode(XRNode.LeftHand),Left);var right=Hand(InputDevices.GetDeviceAtXRNode(XRNode.RightHand),Right);
            Vector3 aimPosition;Quaternion aimRotation;
            LeftAimTracked=Ashfall.Integration.Playable.Input.R23ControllerAimInput.TryReadAim(XRNode.LeftHand,out aimPosition,out aimRotation);LeftAimPosition=TrackingOrigin.TransformPoint(aimPosition);LeftAimRotation=TrackingOrigin.rotation*aimRotation;
            RightAimTracked=Ashfall.Integration.Playable.Input.R23ControllerAimInput.TryReadAim(XRNode.RightHand,out aimPosition,out aimRotation);RightAimPosition=TrackingOrigin.TransformPoint(aimPosition);RightAimRotation=TrackingOrigin.rotation*aimRotation;
            bool menu=left.Menu||Ashfall.Integration.Playable.Input.R23ControllerAimInput.TryReadMenu(XRNode.LeftHand);
            if(!SessionFocused||!Tracking){menuArmed=false;menuHeld=true;}else if(!menu){menuArmed=true;menuHeld=false;}else{if(menuArmed&&!menuHeld&&MenuPressed!=null)MenuPressed();menuHeld=true;}
            // The desktop mirror can lose focus while the OpenXR session still
            // owns headset input. XR focus is the gameplay authority here.
            SessionFocused=DisplayRunning&&OpenXRUtility.IsSessionFocused;
            bool focused=SessionFocused&&Tracking;
            LeftInput=left;RightInput=right;
            TrackingStatus=!DisplayRunning?"Connect your headset through Quest Link / OpenXR":!Tracking?"Put on your headset":Paused?"Paused":"";
            input.Ownership.Dominant=PrimaryHand==0?DominantHand.Left:DominantHand.Right;
            var sample=R23InputProjection.Sample(focused,Tracking,F(transform.InverseTransformPoint(Head.transform.position)),left,right,Paused||GameplayBlocked,PrimaryHand,ReservePrimaryMarkTurn);
            Advance(sample,Mathf.Min(Time.unscaledDeltaTime,.1f));
            bool ping=R23InputProjection.PingHeld(left,right,PrimaryHand);bool pingReady=focused&&!GameplayBlocked&&(PrimaryHand==0?input.RightReady&&right.Tracked:input.LeftReady&&left.Tracked);
            if(!pingReady){pingArmed=false;pingHeld=true;}else if(!ping){pingArmed=true;pingHeld=false;}else{if(pingArmed&&!pingHeld&&PingPressed!=null)PingPressed();pingHeld=true;}
            InputStatus="L "+(left.Tracked?"tracked":"missing")+" "+left.StickX.ToString("F2")+","+left.StickY.ToString("F2")+" grip "+left.Grip.ToString("F2")+" | R "+(right.Tracked?"tracked":"missing")+" grip "+right.Grip.ToString("F2")+" | "+(SessionFocused?"XR focused":"XR unfocused");
            if(SessionFocused&&firstFocus<0)firstFocus=Time.realtimeSinceStartup;
            if((firstFocus>=0&&Time.realtimeSinceStartup-firstFocus<60||!GameplayBlocked||lastLoggedBlock!=GameplayBlocked)&&Time.realtimeSinceStartup>=nextInputLog)
            {
                nextInputLog=Time.realtimeSinceStartup+2;lastLoggedBlock=GameplayBlocked;
                Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,this,"R23 XR input {0} | aim {1}/{2} | armed {3}/{4} | menu {5} | move {6},{7} | root {8} rawhead {9} origin {10} bodycenter {11} grounded {12} collisions {13}",InputStatus,LeftAimTracked,RightAimTracked,input.LeftReady,input.RightReady,GameplayBlocked,LastFrame.MoveX.ToString("F2"),LastFrame.MoveY.ToString("F2"),transform.position,Head.transform.localPosition,TrackingOrigin.localPosition,body.center,grounded,body.detectCollisions);
            }
        }
        void Advance(DeviceSample sample,float seconds)
        {
            if(seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            if(Climbing!=null)Climbing.HeadCollision=UseSourceHeadHull?HeadCollision:null;
            var frame=input.Read(++tick,seconds,sample);
            LastFrame=frame;
            if(TryAdvanceCarrier())return;
            LeftObjectiveGripReserved=RightObjectiveGripReserved=false;
            if(BeforeHandInteractions!=null)BeforeHandInteractions();
            Float3 previousVelocity=motor.Velocity;
            if(!frame.Tracked||frame.Paused)movementHeading.Reset();
            float heading=movementHeading.Read(handHeading?ForwardReference.Hand:ForwardReference.Head,sample.IsXr,input.LeftReady&&sample.Left.Tracked,F(Head.transform.forward),F(Left.forward),F(Left.up),transform.eulerAngles.y*Mathf.Deg2Rad);
            var step=motor.Step(frame,new MotionEnvironment{Grounded=grounded,CeilingBlocked=ceiling,IsXr=sample.IsXr,HeadingRadians=heading},seconds);
            Vector3 displacement=U(step.Displacement),proposedVelocity=U(step.Velocity);
            var climb=new R23ClimbMotion();bool evaluatedClimb=Climbing!=null&&sample.IsXr;
            if(evaluatedClimb)
            {
                climb=Climbing.Evaluate(frame,seconds,sample.Left.Tracked,sample.Right.Tracked,!LeftHandOccupied&&!LeftObjectiveGripReserved,!RightHandOccupied&&!RightObjectiveGripReserved,U(previousVelocity));
                if(climb.OwnsMotion){displacement=climb.Displacement;proposedVelocity=climb.Velocity;}
            }
            if(Activities!=null&&sample.IsXr)
            {
                // Source applies this tick's turn before resolving both walking and glide heading.
                var travel=R23TravelHeading.Resolve(heading,step.OwnsYaw?step.YawRadians:0,frame.MoveX,frame.MoveY);
                var flight=Activities.EvaluateMotion(frame,seconds,grounded,ceiling,U(previousVelocity),U(step.Velocity),U(travel.Direction),input.LeftReady&&input.RightReady,climb.OwnsMotion,climb.Mantling,U(travel.Forward));
                if(flight.OwnsMotion&&!climb.OwnsMotion){displacement=flight.Displacement;proposedVelocity=flight.Velocity;}
                var constructionFrame=frame;if(SourceGhostMotion)constructionFrame.Paused=true;
                Activities.TickConstruction(constructionFrame,input.LeftReady&&sample.Left.Tracked&&!LeftHandOccupied&&sample.Left.Grip<.2f&&!climb.Climbing,input.RightReady&&sample.Right.Tracked&&!RightHandOccupied&&sample.Right.Grip<.2f&&!climb.Climbing,sample.Left.Primary,sample.Right.Primary);
            }
            Vector3 headLocal=transform.InverseTransformPoint(Head.transform.position);
            float height=Mathf.Clamp(headLocal.y+.18f,.6f,2.5f);body.height=height;body.center=new Vector3(headLocal.x,height*.5f,headLocal.z);
            if(!step.OwnsMotion){if(Tracking&&!UseSourceHeadHull)body.Move(Vector3.zero);if(evaluatedClimb)Climbing.AcceptMotion(Vector3.zero,seconds,grounded,ceiling);return;}
            // Turn around the measured head, leaving all tracked local poses intact.
            if(step.OwnsYaw&&step.YawRadians!=0&&!climb.Climbing&&!climb.Mantling){Vector3 prior=transform.position;transform.RotateAround(Head.transform.position,Vector3.up,step.YawRadians*Mathf.Rad2Deg);artificialOriginOffset+=transform.position-prior;}
            Vector3 before=transform.position,supportCorrection=Vector3.zero;
            if(UseSourceHeadHull)
            {
                Vector3 requested=HeadCollision.LandingMotion(transform.position,Head.transform.position,displacement);
                Vector3 safe=HeadCollision.Sweep(Head.transform.position,requested,true,out ceiling);
                body.enabled=false;transform.position+=safe;body.enabled=true;
                Vector3 correction;grounded=false;
                if(!climb.OwnsMotion&&(Activities==null||!Activities.PadLaunchActive)&&displacement.y<=0&&HeadCollision.ResolveSupport(transform.position,Head.transform.position,proposedVelocity,seconds,Mathf.Clamp(Head.transform.position.y-transform.position.y,.55f,2.2f)+.0025f,false,out correction))
                {body.enabled=false;transform.position+=correction;body.enabled=true;grounded=true;supportCorrection=correction;}
            }
            else
            {
                CollisionFlags flags=body.Move(displacement);grounded=body.isGrounded||(flags&CollisionFlags.Below)!=0;ceiling=(flags&CollisionFlags.Above)!=0;
            }
            if(Activities!=null&&Activities.PadLaunchActive&&!ceiling)grounded=false;
            // Support elevation is a positional correction, never launch energy.
            // Original player._resolve_support explicitly publishes velocity.y=0.
            Vector3 acceptedPhysical=transform.position-before-supportCorrection;
            motor.SetVelocity(R23RootVelocity.Accept(F(transform.position-before),F(supportCorrection),seconds,grounded,ceiling));DistanceMoved+=Vector3.Distance(before,transform.position);
            if(evaluatedClimb){var accepted=Climbing.AcceptMotion(acceptedPhysical,seconds,grounded,ceiling);if(climb.OwnsMotion){motor.SetVelocity(F(accepted.Velocity));grounded=accepted.Grounded;}}
            if(Activities!=null)Activities.AcceptMotion(acceptedPhysical,seconds,grounded,ceiling,false);
            if(UseSourceHeadHull){previousSourceHead=Head.transform.position;haveSourceHead=true;}
        }
        void SynchronizeSourceHead()
        {
            Vector3 desired=Head.transform.position;
            if(haveSourceHead)
            {bool hitCeiling;Vector3 actual=HeadCollision.Sweep(previousSourceHead,desired-previousSourceHead,false,out hitCeiling);TrackingOrigin.position+=previousSourceHead+actual-desired;}
            // Source recenters logical feet beneath the safe head by applying equal
            // and opposite root/origin translations. Raw tracked local poses survive.
            Vector3 recenter=Head.transform.position-transform.position;recenter.y=0;Vector3 origin=TrackingOrigin.position;
            body.enabled=false;transform.position+=recenter;artificialOriginOffset+=recenter;TrackingOrigin.position=origin;body.enabled=true;
            previousSourceHead=Head.transform.position;haveSourceHead=true;
        }
        // Focused native smoke uses the same input/motor/controller path. It does
        // not constitute a real headset/controller tracking test.
        public void SmokeMove(Vector2 axis,float seconds)
        {Advance(new DeviceSample{Focused=true,MoveX=axis.x,MoveY=axis.y,HeadPosition=F(Head.transform.localPosition)},seconds);}
        public void SmokeXrMove(Vector2 axis,float seconds)
        {
            var left=new HandSample{Tracked=true,Position=F(Left.localPosition),StickX=axis.x,StickY=axis.y};
            var right=new HandSample{Tracked=true,Position=F(Right.localPosition)};
            Advance(R23InputProjection.Sample(true,true,F(Head.transform.localPosition),left,right,false,PrimaryHand),seconds);
        }
        public void SmokeXrHands(float leftGrip,float rightTrigger,float seconds)
        {
            var left=new HandSample{Tracked=true,Position=F(transform.InverseTransformPoint(Left.position)),Grip=leftGrip};
            var right=new HandSample{Tracked=true,Position=F(transform.InverseTransformPoint(Right.position)),Trigger=rightTrigger};
            Advance(R23InputProjection.Sample(true,true,F(transform.InverseTransformPoint(Head.transform.position)),left,right,false,PrimaryHand),seconds);
        }
#if UNITY_EDITOR
        // XR-off native fixtures publish hand input through the same public
        // samples consumed by Root.Update, after the ordinary motor smoke frame.
        public void SmokeTrackedHands(HandSample left,HandSample right)
        {
            if(XRSettings.isDeviceActive)throw new System.InvalidOperationException("Simulated hands require XR disabled");
            LeftInput=left;RightInput=right;
            LeftAimTracked=left.Tracked;RightAimTracked=right.Tracked;
            LeftAimPosition=Left.position;RightAimPosition=Right.position;
            LeftAimRotation=Left.rotation;RightAimRotation=Right.rotation;
        }
#endif
        public void ResetPose()
        {
            if(body==null)return;body.enabled=false;transform.SetPositionAndRotation(spawn,spawnRotation);body.enabled=true;
            // Map spawns name physical feet. Stage-space HMD X/Z can be nonzero
            // even while standing still: move the origin, never raw tracked poses.
            if(TrackingOrigin!=null&&Head!=null){Vector3 p=TrackingOrigin.localPosition;p.x=-Head.transform.localPosition.x;p.z=-Head.transform.localPosition.z;TrackingOrigin.localPosition=p;}
            motor.ResetMotion();input.Disarm();movementHeading.Reset();grounded=false;ceiling=false;Paused=false;
            haveSourceHead=false;
            artificialOriginOffset=Vector3.zero;
        }
        public void SetSpawn(Vector3 location,Vector3 lookAt)
        {spawn=location;Vector3 forward=lookAt-location;forward.y=0;spawnRotation=forward.sqrMagnitude>.01f?Quaternion.LookRotation(forward):Quaternion.identity;ResetPose();}
        public void DisarmMotion(){if(motor!=null)motor.ResetMotion();input.Disarm();movementHeading.Reset();}
        public void ApplySettings(R23UiSettings value)
        {
            if(value==null||tuning==null)return;
            tuning.WalkMultiplier=value.WalkMultiplier;tuning.Turning=value.Turning==R23Turning.Snap?TurnStyle.Snap:TurnStyle.Smooth;
            tuning.SnapRadians=value.SnapDegrees*Mathf.Deg2Rad;tuning.SmoothRadiansPerSecond=value.SmoothDegreesPerSecond*Mathf.Deg2Rad;
            handHeading=value.Forward==R23Forward.LeftController;motor.Settings=new Speed(this);input.Disarm();movementHeading.Reset();
            AudioListener.volume=value.MasterVolume;
        }
        void OnApplicationFocus(bool focused){if(!focused&&!SessionFocused){input.Disarm();movementHeading.Reset();}}
        void OnDisable(){input.Disarm();movementHeading.Reset();}
        void OnDestroy(){Application.onBeforeRender-=TrackPoses;}
        static Float3 F(Vector3 p){return new Float3(p.x,p.y,p.z);}static Vector3 U(Float3 p){return new Vector3(p.X,p.Y,p.Z);}
    }
}
#endif

namespace Ashfall.Integration.Playable
{
    // Pure SI feedback policy shared by the actual motor and CPU regression cases.
    // Native collision still supplies accepted displacement/support/ceiling flags.
    public static class R23RootVelocity
    {
        public static Ashfall.Core.Float3 Accept(Ashfall.Core.Float3 total,Ashfall.Core.Float3 support,float seconds,bool grounded,bool ceiling)
        {
            if(!total.IsFinite||!support.IsFinite||float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0||seconds>.1f)throw new System.ArgumentException("accepted root motion");
            var velocity=new Ashfall.Core.Float3((total.X-support.X)/seconds,(total.Y-support.Y)/seconds,(total.Z-support.Z)/seconds);
            if(grounded||(ceiling&&velocity.Y>0))velocity=new Ashfall.Core.Float3(velocity.X,0,velocity.Z);
            if(!velocity.IsFinite||System.Math.Abs(velocity.X)>10000||System.Math.Abs(velocity.Y)>10000||System.Math.Abs(velocity.Z)>10000)throw new System.ArgumentException("accepted root velocity");
            return velocity;
        }
    }
}
