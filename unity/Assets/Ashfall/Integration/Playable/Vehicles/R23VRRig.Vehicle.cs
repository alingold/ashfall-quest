#if UNITY_6000_0_OR_NEWER
using Ashfall.Core;
using Ashfall.Integration.Playable.Vehicles;
using Ashfall.Integration.Playable.Networking;
using UnityEngine;
namespace Ashfall.Integration.Playable
{
    public sealed partial class R23VRRig
    {
        readonly R25HelicopterDynamics helicopterMotor=new R25HelicopterDynamics();
        public System.Func<float> VehicleMapTop;
        readonly R25TankDynamics tankMotor=new R25TankDynamics();
        string vehicleKind="hoverbike";bool tankClick;
        public System.Func<string,bool> FireTank;
        public float VehicleHullYaw {get{return vehicleKind=="tank"?tankMotor.Yaw:transform.eulerAngles.y;}}
        readonly R25BikeDynamics bikeMotor=new R25BikeDynamics();
        readonly R25BikeHold mountHold=new R25BikeHold(),exitHold=new R25BikeHold();
        public System.Func<R25VehicleState[]> ReadVehicles;
        public System.Func<bool> VehicleEnabled;
        public System.Func<string> ReadVehicleActor;
        public System.Func<string,int,bool> MountVehicle;
        public System.Action<string,Float3,float> DismountVehicle;
        string vehicleId,mountCandidate;float mountRequestedUntil;bool vehicleExitPending;
        public bool Riding {get{return vehicleId!=null&&!vehicleExitPending;}}
        // R4 (playtest 3): tank and helicopter motors keep the rider root about
        // .55 m above the ground (their chassis boxes need that clearance). The
        // rider used to stay at ground level after mounting, so the chassis box
        // started inside the ground, every step was refused (vehicle never moved)
        // and the hull was drawn sunk into the ground. Mounting now glides the
        // rider onto the vehicle's rest pose; exiting glides them off one side.
        // Glides stay well below the host's 55 m/s track envelope.
        Vector3 vehicleGlideTarget;float vehicleGlideLeft;bool vehicleGlideBoarding,vehicleGlideArrived;
        const float VehicleGlideSpeed=9f;
        public bool VehicleBoarding {get{return vehicleGlideLeft>0&&vehicleGlideBoarding&&Riding;}}
        bool tankTrigger,exitGripArmed;float vehicleGunCooldown;
        string ExitButtonName {get{return PrimaryHand==1?"X":"A";}}
        public System.Func<string,Vector3,Vector3,bool> FireVehicleGun;
        public string RidingKind {get{return Riding?vehicleKind:null;}}
        public string RidingVehicleId {get{return Riding?vehicleId:null;}}
        Vector3 vehicleAimLocal;
        float vehicleAimStep;
        int vehicleAimHand;
        public bool VehicleAimReady {get;private set;}
        public bool VehicleAimLimited {get;private set;}
        Quaternion VehicleAimBasis {get{return Quaternion.Euler(0,vehicleKind=="tank"?transform.eulerAngles.y:VehicleHullYaw,0);}}
        public float VehicleGunYaw {get{return R25VehicleKinds.AimYaw(F(vehicleAimLocal));}}
        public float VehicleGunPitch {get{return R25VehicleKinds.AimPitch(F(vehicleAimLocal));}}
        public float TankAimYaw {get{return R25VehicleKinds.AimYaw(F(VehicleAimBasis*vehicleAimLocal));}}
        public float TankAimPitch {get{return R24LanLobby.ClampTankPitch(VehicleGunPitch);}}
        public Ray VehicleGunRay
        {
            get
            {
                Vector3 direction=VehicleAimBasis*vehicleAimLocal;
                if(vehicleKind=="tank")return new Ray(U(R25VehicleKinds.TankMuzzle(F(transform.position),F(direction))),direction);
                Vector3 pivot=transform.position+VehicleAimBasis*U(R25VehicleKinds.GunPivot(vehicleKind));
                return new Ray(pivot+direction*R25VehicleKinds.GunBarrelLength(vehicleKind),direction);
            }
        }
        void TrackVehicleAim(float dt)
        {
            bool left=PrimaryHand==0;Transform hand=left?Left:Right;
            if(hand==null||!(left?LeftInput.Tracked:RightInput.Tracked)){VehicleAimReady=false;return;}
            bool aimTracked=left?LeftAimTracked:RightAimTracked;
            Quaternion rotation=aimTracked?(left?LeftAimRotation:RightAimRotation):hand.rotation;
            var local=F((Quaternion.Inverse(VehicleAimBasis)*rotation*Vector3.forward).normalized);
            if(!local.IsFinite||local.Equals(new Float3())){VehicleAimReady=false;return;}
            Vector3 target=vehicleKind=="tank"
                ?U(R24LanLobby.TankShellDirection(R25VehicleKinds.AimYaw(local),R25VehicleKinds.AimPitch(local)))
                :U(R25VehicleKinds.ClampGunDirection(local));
            VehicleAimLimited=Vector3.Dot(target,U(local))<.99999f;
            // Filter in the turning rig's frame so snap turns don't drag the aim across the cockpit.
            Vector3 smoothed=VehicleAimReady&&vehicleAimHand==PrimaryHand
                ?Vector3.Slerp(vehicleAimLocal,target,1-Mathf.Exp(-dt/.045f)).normalized:target;
            var filtered=F(smoothed);
            vehicleAimLocal=vehicleKind=="tank"
                ?U(R24LanLobby.TankShellDirection(R25VehicleKinds.AimYaw(filtered),R25VehicleKinds.AimPitch(filtered)))
                :U(R25VehicleKinds.ClampGunDirection(filtered));
            vehicleAimHand=PrimaryHand;VehicleAimReady=true;
        }
        void TickMountedWeapon(float dt)
        {
            TrackVehicleAim(dt);
            bool trigger=RightInput.Tracked&&!RightHandOccupied&&RightInput.Trigger>.55f;
            if(vehicleKind=="tank"){
                if(VehicleAimReady&&trigger&&!tankTrigger&&FireTank!=null)FireTank(vehicleId);
                tankTrigger=trigger;return;
            }
            vehicleGunCooldown-=dt;
            if(!VehicleAimReady||!trigger||FireVehicleGun==null){vehicleGunCooldown=Mathf.Max(0,vehicleGunCooldown);return;}
            if(vehicleGunCooldown>0)return;
            var ray=VehicleGunRay;
            if(FireVehicleGun(vehicleId,ray.origin,ray.direction))vehicleGunCooldown+=R24LanLobby.VehicleGunInterval;else vehicleGunCooldown=0;
        }
        Float3 VehicleExitRest()
        {
            if(vehicleKind=="helicopter"){
                RaycastHit hit;
                if(Physics.Raycast(transform.position,Vector3.down,out hit,2100,(1<<0)|(1<<8),QueryTriggerInteraction.Ignore))
                    return F(new Vector3(transform.position.x,hit.point.y+.65f,transform.position.z));
            }
            return F(transform.position);
        }
        void StartVehicleExitGlide(string kind)
        {
            if(kind!="tank"&&kind!="helicopter"||HeadCollision==null||Head==null)return;
            float side=kind=="tank"?2.5f:2.1f;Vector3 right=Quaternion.Euler(0,VehicleHullYaw,0)*Vector3.right;
            for(int s=0;s<2;s++){
                Vector3 offset=right*(s==0?side:-side);
                if(!HeadCollision.HeadClear(Head.transform.position+offset))continue;
                bool hit;Vector3 swept=HeadCollision.Sweep(Head.transform.position,offset,false,out hit,false,true);
                if((swept-offset).sqrMagnitude>.0001f)continue;
                vehicleGlideTarget=transform.position+offset;vehicleGlideLeft=1;vehicleGlideBoarding=false;return;
            }
        }
        bool VehicleGlide(float dt,out Vector3 displacement,out Vector3 velocity)
        {
            displacement=velocity=Vector3.zero;if(vehicleGlideLeft<=0)return false;
            vehicleGlideLeft-=dt;Vector3 d=vehicleGlideTarget-transform.position;
            if(d.magnitude<.01f||vehicleGlideLeft<=0){vehicleGlideLeft=0;if(motor!=null)motor.SetVelocity(new Float3());return false;}
            displacement=Vector3.MoveTowards(Vector3.zero,d,VehicleGlideSpeed*dt);velocity=displacement/dt;
            // Final step: arrive without carrying glide speed into the next frame.
            if(d.magnitude<=VehicleGlideSpeed*dt){vehicleGlideLeft=0;vehicleGlideArrived=true;}
            return true;
        }
        public string VehicleHint {get;private set;}
        public void CancelVehicle()
        {
            if(vehicleId!=null&&!vehicleExitPending&&DismountVehicle!=null)DismountVehicle(vehicleId,VehicleExitRest(),transform.eulerAngles.y);
            vehicleExitPending=true;vehicleId=null;mountCandidate=null;mountHold.Step(false,.01f,.4f);exitHold.Step(false,.01f,.5f);
            VehicleAimReady=false;VehicleHint="";HideVehicleSeat();
        }
#if UNITY_EDITOR
        // Explicit editor fixture: same input projection and motor path, no XR device.
        public void VehicleSmokeSample(Ashfall.Modules.Player.HandSample left,Ashfall.Modules.Player.HandSample right,float dt)
        {VehicleSmokeSample(left,right,dt,new Vector3(0,1.7f,0));}
        // R4 fixture: same path with a supplied raw tracked head (real heads never stay still).
        public void VehicleSmokeSample(Ashfall.Modules.Player.HandSample left,Ashfall.Modules.Player.HandSample right,float dt,Vector3 headLocal)
        {
            if(UnityEngine.XR.XRSettings.isDeviceActive)throw new System.InvalidOperationException("Vehicle fixture requires XR disabled");
            enabled=false;Tracking=DisplayRunning=true;SessionFocused=!FixtureUnfocused;Paused=false;
            Head.transform.localPosition=headLocal;Head.transform.localRotation=Quaternion.identity;
            LeftInput=left;RightInput=right;Left.localPosition=U(left.Position);Right.localPosition=U(right.Position);
            // R7: same order as Update (sync, calibration, advance, recovery, render-time sync).
            if(UseSourceHeadHull&&!CarrierActive)SynchronizeSourceHead();
            if(Calibration!=null)Calibration.Tick(true,SessionFocused,false,dt);
            Advance(R23InputProjection.Sample(SessionFocused,true,F(transform.InverseTransformPoint(Head.transform.position)),left,right,MotionPaused,PrimaryHand),dt);
            if(FixtureRunsRecovery)TickHeadRecovery();
            SynchronizeLateSourceHead();
        }
        public bool FixtureRunsRecovery,FixtureUnfocused;
#endif
        void AcceptVehicleVelocity(Float3 velocity)
        {if(vehicleGlideArrived){vehicleGlideArrived=false;velocity=new Float3();}
         if(vehicleKind=="helicopter"){helicopterMotor.Accept(velocity);motor.SetVelocity(helicopterMotor.Velocity);}else if(vehicleKind=="tank"){tankMotor.Accept(velocity);motor.SetVelocity(tankMotor.Velocity);}else{bikeMotor.Accept(velocity);motor.SetVelocity(bikeMotor.Velocity);}
         if(Riding&&vehicleAimStep>0)TickMountedWeapon(vehicleAimStep);}
        Vector3 VehicleChassisMotion(Vector3 desired)
        {
            // Boarding/exit glides move only the rider; the common head sweep still runs.
            if(vehicleGlideLeft>0)return desired;
            if(vehicleKind=="helicopter")
            {
                // An aircraft that starts overlapping something may still lift
                // out of it; previously it stayed frozen forever.
                if(!HeadCollision.HelicopterClear(transform.position,transform.eulerAngles.y))return new Vector3(0,Mathf.Max(0,desired.y),0);
                return HeadCollision.HelicopterMotion(transform.position,transform.eulerAngles.y,desired);
            }
            if(vehicleKind!="tank")return HeadCollision.BikeMotion(transform.position,transform.forward,desired);
            var q=Quaternion.Euler(0,tankMotor.Yaw,0);RaycastHit support;if(HeadCollision.Support(transform.position,transform.position+Vector3.up*.2f,out support))q=Quaternion.FromToRotation(Vector3.up,support.normal)*q;
            // Hull box from just below the ride root to the turret ring: the tracks
            // clear kerbs and small bumps below about .45 m; walls still stop it.
            var half=new Vector3(1.35f,.35f,2.2f);Vector3 center=transform.position+q*(Vector3.up*.3f);
            // The common verified head sweep still runs afterwards.
            int mask=(1<<0)|(1<<8);
            if(Physics.CheckBox(center,half,q,mask,QueryTriggerInteraction.Ignore))return new Vector3(0,Mathf.Max(0,desired.y),0);
            RaycastHit hit;float length=desired.magnitude;
            if(length>0&&Physics.BoxCast(center,half,desired/length,out hit,q,length+.02f,mask,QueryTriggerInteraction.Ignore))desired=desired.normalized*Mathf.Max(0,hit.distance-.02f);
            if(Physics.CheckBox(center+desired,half,q,mask,QueryTriggerInteraction.Ignore))return new Vector3(0,Mathf.Max(0,desired.y),0);
            return desired;
        }
        bool EvaluateVehicle(InputFrame frame,float dt,out Vector3 displacement,out Vector3 velocity)
        {
            displacement=velocity=Vector3.zero;vehicleAimStep=0;
            var rules=ReadTraversalRules==null?null:ReadTraversalRules();
            bool allowed=R25BikeRider.Allowed((rules==null||rules.Vehicles)&&(VehicleEnabled==null||VehicleEnabled()),frame.Tracked&&!frame.Paused,
                GameplayBlocked||MenuLocomotion,SourceGhostMotion,CarrierActive);
            var rows=ReadVehicles==null?new R25VehicleState[0]:ReadVehicles();
            string actor=ReadVehicleActor==null?null:ReadVehicleActor();R25VehicleState owned=null;
            foreach(var row in rows)if(!row.Destroyed&&row.Rider==actor&&actor!=null)owned=row;
            if(owned!=null&&owned.Kind=="tank"&&rules!=null&&!rules.Tanks)allowed=false;
            if(owned!=null&&owned.Kind=="helicopter"&&rules!=null&&!rules.Helicopters)allowed=false;
            if(!allowed){vehicleGlideLeft=0;CancelVehicle();return false;}
            // Exit glide: the rider is already released; finish stepping off the hull.
            if(!vehicleGlideBoarding&&vehicleId==null&&VehicleGlide(dt,out displacement,out velocity))return true;
            if(owned==null){if(vehicleId!=null){vehicleId=null;VehicleAimReady=false;if(vehicleKind!="helicopter")RequestHeadRecovery(Exclusion.R25RecoveryReason.Respawn,false);}vehicleExitPending=false;}
            else if(!vehicleExitPending&&vehicleId!=owned.Id){
                VehicleAimReady=false;vehicleKind=owned.Kind;helicopterMotor.Reset(owned.Home.Y-.65f);tankMotor.Reset(owned.Yaw,motor.Velocity);tankClick=true;tankTrigger=true;exitGripArmed=false;vehicleGunCooldown=.3f;vehicleId=owned.Id;bikeMotor.Accept(motor.Velocity);CancelTraversal();
                // Bike: mount in place. Tank/helicopter: glide onto the hull's rest pose.
                if(Climbing!=null)Climbing.ResetMotion();
                vehicleGlideLeft=0;
                if(owned.Kind=="tank"||owned.Kind=="helicopter"){vehicleGlideTarget=U(owned.Rest);vehicleGlideLeft=1;vehicleGlideBoarding=true;}
            }
            if(!Riding){
                if(owned!=null)return false;
                string candidate=null;int hand=0;
                foreach(var row in rows)if((row.Kind!="tank"||rules==null||rules.Tanks)&&(row.Kind!="helicopter"||rules==null||rules.Helicopters)&&grounded&&!row.Destroyed&&row.Rider==""&&Vector3.Distance(transform.position,U(row.Rest))<(row.Kind=="tank"?3.4f:row.Kind=="helicopter"?2.8f:1.8f)){
                    Vector3 marker=U(R25BikeRider.MountPoint(row.Rest,row.Yaw,row.Kind,Vector3.Dot(Head.transform.position-U(row.Rest),Quaternion.Euler(0,row.Yaw,0)*Vector3.right)<0?-1:1));
                    for(int h=0;h<2;h++)if((h==0?frame.GripLeft&&LeftInput.Tracked&&!LeftHandOccupied:frame.GripRight&&RightInput.Tracked&&!RightHandOccupied)&&Vector3.Distance(h==0?Left.position:Right.position,marker)<R25BikeRider.MountReach){candidate=row.Id;hand=h;}
                }
                if(candidate!=null){if(hand==0)LeftObjectiveGripReserved=true;else RightObjectiveGripReserved=true;}
                if(candidate!=mountCandidate){mountHold.Step(false,dt,.4f);mountCandidate=candidate;}
                if(mountHold.Step(candidate!=null,dt,.4f)&&MountVehicle!=null&&MountVehicle(candidate,hand))mountRequestedUntil=Time.unscaledTime+2;
                VehicleHint=Time.unscaledTime<mountRequestedUntil?"MOUNT REQUESTED":candidate!=null?"HOLD GRIP TO MOUNT":"";
                HideVehicleSeat();return false;
            }
            string aimHint=(PrimaryHand==0?"LEFT":"RIGHT")+" HAND: AIM / RIGHT TRIGGER: FIRE / PULL OVERHEAD HANDLE: EJECT";
            VehicleHint=vehicleKind=="helicopter"?"LEFT GRIP + TILT: COLLECTIVE / STICK CLICK: DESCEND / "+aimHint:vehicleKind=="tank"?"LEFT STICK: DRIVE / "+aimHint:"JUMP: HOP / "+aimHint;
            if(vehicleGlideBoarding&&vehicleGlideLeft>0){
                if(VehicleGlide(dt,out displacement,out velocity))return true;
                // Boarded: the motors take over from the hull's rest pose.
                helicopterMotor.Accept(new Float3());tankMotor.Accept(new Float3());motor.SetVelocity(new Float3());
            }
            // r19 EJECT-HANDLE: the overhead handle is the exit. Grab it with either grip and pull it down EjectPull.
            if(TickVehicleSeat(dt)){string seatKind=vehicleKind;DismountedByHandle=true;CancelVehicle();DismountedByHandle=false;StartVehicleExitGlide(seatKind);return false;}
            bool rightGrip=frame.GripRight&&RightInput.Tracked&&!RightHandOccupied;
            // The grip that mounted the vehicle must be released before it can exit.
            if(!frame.GripRight)exitGripArmed=true;
            // Fallback exit (the pre-r19 right-grip hold): deliberate 1.5 s, and never while the right hand is on the handle.
            bool gripExit=rightGrip&&exitGripArmed&&!VehicleSeatHandBusy(1);
            if(gripExit)RightObjectiveGripReserved=true;
            if(vehicleKind=="tank"){
                if(exitHold.Step(gripExit,dt,R26VehicleSeat.FallbackExitSeconds)){CancelVehicle();StartVehicleExitGlide("tank");return false;}
            }
            else if(vehicleKind!="helicopter"&&exitHold.Step(gripExit||frame.Jump&&!VehicleSeatHandBusy(1),dt,R26VehicleSeat.FallbackExitSeconds)){CancelVehicle();return false;}
            CancelTraversal();
            RaycastHit floor;bool ground;
            // Initialize separately because short-circuit support may not execute.
            floor=new RaycastHit();ground=HeadCollision!=null&&HeadCollision.Support(transform.position,transform.position+Vector3.up*.2f,out floor);
            if(vehicleKind=="helicopter"){
                ground=HeadCollision.HelicopterGround(transform.position,out floor);
                float height=ground?transform.position.y-floor.point.y:200;
                // r19 HELI-COLLECTIVE: while the LEFT grip is held its controller pitch (relative to the press pose) sets the vertical speed.
                // The left grip no longer climbs by itself; X/A still climbs and the stick click still descends fast.
                TickCollective(dt);
                bool climb=frame.Jump;
                bool exit=R25HelicopterDynamics.CanExit(height,gripExit,true,RightInput.Tracked,climb,LeftInput.Click,frame.Paused||MenuLocomotion);
                if(exitHold.Step(exit,dt,R26VehicleSeat.FallbackExitSeconds)){CancelVehicle();StartVehicleExitGlide("helicopter");return false;}
                helicopterMotor.Step(dt,frame.MoveX,frame.MoveY,transform.eulerAngles.y,collectiveHeld,collectiveSmoothed,climb,LeftInput.Click,transform.position.y,ground?floor.point.y:helicopterMotor.LaunchGround-200,VehicleMapTop==null?helicopterMotor.LaunchGround+82.3f:VehicleMapTop());
                displacement=U(helicopterMotor.Displacement);velocity=U(helicopterMotor.Velocity);vehicleAimStep=dt;return true;
            }
            if(vehicleKind=="tank"){
                // Left stick: Y throttle, X steers the hull (35 deg/s at full deflection).
                float steer=Mathf.Abs(frame.MoveX)<.15f?0:Mathf.Clamp(frame.MoveX,-1,1);
                tankMotor.Step(dt,frame.MoveY,tankMotor.Yaw+steer*35*dt,ground,ground?transform.position.y-floor.point.y:3,ground?F(floor.normal):new Float3(0,1,0));
                tankClick=LeftInput.Click;vehicleAimStep=dt;
                displacement=U(tankMotor.Displacement);velocity=U(tankMotor.Velocity);return true;
            }
            R25BikeRider.Drive(bikeMotor,dt,frame.MoveX,frame.MoveY,transform.eulerAngles.y,LeftInput.Click,frame.Jump,ground,ground?transform.position.y-floor.point.y:3,ground?F(floor.normal):new Float3(0,1,0));
            displacement=U(bikeMotor.Displacement);velocity=U(bikeMotor.Velocity);vehicleAimStep=dt;return true;
        }
    }
}
#endif
