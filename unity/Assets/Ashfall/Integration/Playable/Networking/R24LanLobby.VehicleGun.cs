using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Integration.Playable.Vehicles;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        const byte VehicleModeAim=1,VehicleModeAimFire=2,VehicleModeGun=3;
        public const float TankPitchMinimum=-10,TankPitchMaximum=35,VehicleGunInterval=.1f,VehicleGunRange=R24LanBallisticProfile.MaximumRange;
        const float VehicleShellSeconds=8;
        readonly Dictionary<string,double> vehicleGunShots=new Dictionary<string,double>(StringComparer.Ordinal);
        public int AcceptedVehicleGunShots{get;private set;}
        public string LastVehicleGunRejection{get;private set;}
        public static float ClampTankPitch(float pitch){return Single.IsNaN(pitch)||Single.IsInfinity(pitch)?0:Math.Max(TankPitchMinimum,Math.Min(TankPitchMaximum,pitch));}
        public static Float3 TankShellDirection(float yaw,float pitch)
        {return R25VehicleKinds.AimDirection(yaw,ClampTankPitch(pitch));}
        bool SendVehicleMode(string id,byte mode,float[] values)
        {
            Check();if(!Wire.IsText(id,32,false))return false;
            foreach(float v in values)if(Single.IsNaN(v)||Single.IsInfinity(v))return false;
            if(State!=R24LanLobbyState.Connected)return false;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){Write(w,id,32);w.Write(mode);foreach(float v in values)w.Write(v);return QueueGameCommand(VehicleFire,s.ToArray());}
        }
        public bool RequestVehicleAim(string id,float yaw,float pitch,float hull)
        {
            if(State==R24LanLobbyState.Hosting)return ApplyVehicleAim(OwnActor,id,yaw,pitch,hull,false);
            return SendVehicleMode(id,VehicleModeAim,new[]{yaw,pitch,hull});
        }
        public bool RequestTankFireAimed(string id,float yaw,float pitch,float hull)
        {
            if(State==R24LanLobbyState.Hosting)return ApplyVehicleAim(OwnActor,id,yaw,pitch,hull,true)&&ApplyTankFire(OwnActor,id);
            return SendVehicleMode(id,VehicleModeAimFire,new[]{yaw,pitch,hull});
        }
        public bool RequestVehicleGun(string id,Float3 origin,Float3 direction)
        {
            if(State==R24LanLobbyState.Hosting)return ApplyVehicleGun(OwnActor,id,origin,direction);
            return SendVehicleMode(id,VehicleModeGun,new[]{origin.X,origin.Y,origin.Z,direction.X,direction.Y,direction.Z});
        }
        void ReadVehicleMode(string actor,string id,BinaryReader r,Stream s)
        {
            byte mode=r.ReadByte();
            if(mode==VehicleModeAim||mode==VehicleModeAimFire){
                float yaw=r.ReadSingle(),pitch=r.ReadSingle(),hull=r.ReadSingle();if(s.Position!=s.Length)return;
                if(ApplyVehicleAim(actor,id,yaw,pitch,hull,mode==VehicleModeAimFire)&&mode==VehicleModeAimFire)ApplyTankFire(actor,id);
            }else if(mode==VehicleModeGun){
                var origin=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());var direction=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                if(s.Position==s.Length)ApplyVehicleGun(actor,id,origin,direction);
            }
        }
        bool ApplyVehicleAim(string actor,string id,float yaw,float pitch,float hull,bool tankOnly)
        {
            R25VehicleState row;R24LanRawTrackingSample raw;
            if(!Finite(yaw)||Math.Abs(yaw)>720||!Finite(pitch)||!Finite(hull)||Math.Abs(hull)>720||
                ControlsBlocked(actor)||!VehicleRiderReady(actor,out raw)||!vehicles.TryGetValue(id,out row)||row.Destroyed||row.Rider!=actor)return false;
            if(row.Kind=="tank"){
                if(!GameplayRules.Tanks)return false;
                yaw=((yaw%360)+360)%360;pitch=ClampTankPitch(pitch);hull=((hull%360)+360)%360;
            }else{
                // Machine gun aim snapshots are relative to the hull. Shot rays are world-space.
                if(tankOnly||row.Kind=="helicopter"&&!GameplayRules.Helicopters||
                    Math.Abs(yaw)>R25VehicleKinds.GunTraverseDegrees+.001f||Math.Abs(pitch)>R25VehicleKinds.GunTraverseDegrees+.001f||
                    !R25VehicleKinds.GunDirectionAllowed(R25VehicleKinds.AimDirection(yaw,pitch),0))return false;
                hull=row.Yaw;
            }
            if(row.TurretYaw!=yaw||row.GunPitch!=pitch||row.Yaw!=hull)
                PublishVehicle(new R25VehicleState(row.Id,row.Home,row.HomeYaw,row.Rest,hull,row.Health,actor,0,++vehicleSequence,row.Kind,yaw,row.FiredAt,pitch));
            return true;
        }
        // The sandbox can practice on builds even when room warm-up protects players.
        bool VehicleBuildDamageLive{get{return CombatLive||VehiclesLive&&MapId=="range";}}
        bool RejectVehicleGun(string why){LastVehicleGunRejection=why;return false;}
        bool ApplyVehicleGun(string actor,string id,Float3 origin,Float3 direction)
        {
            R25VehicleState row;R24LanRawTrackingSample raw;R24LanBallisticProfile profile;
            if(!VehiclesLive||ControlsBlocked(actor)||!vehicles.TryGetValue(id,out row)||row.Destroyed||row.Rider!=actor||row.Kind=="tank")return RejectVehicleGun("custody");
            if(row.Kind=="helicopter"&&!GameplayRules.Helicopters)return RejectVehicleGun("rules");
            if(!origin.IsFinite||!direction.IsFinite||!VehicleRiderReady(actor,out raw))return RejectVehicleGun("tracking");
            if(Distance2(raw.Pose.Root.Position,origin)>25)return RejectVehicleGun("origin");
            double length=Math.Sqrt((double)direction.X*direction.X+(double)direction.Y*direction.Y+(double)direction.Z*direction.Z);if(length<.5||length>1.5)return RejectVehicleGun("direction");
            var d=Normalize(direction);
            if(!R25VehicleKinds.GunDirectionAllowed(d,VehicleYaw(row)))return RejectVehicleGun("mount-angle");
            double now=clock.Now,last;if(vehicleGunShots.TryGetValue(actor,out last)&&now-last<VehicleGunInterval*.75)return RejectVehicleGun("cadence");
            vehicleGunShots[actor]=now;AcceptedVehicleGunShots++;LastVehicleGunRejection=null;
            if(!VehicleBuildDamageLive||ballisticTrace==null||!R24LanBallisticProfile.TryRead("m60",out profile))return true;
            if(rawWorldClear==null||!rawWorldClear(raw.Pose.Head.Position,origin))return true;
            LaunchMountedBullet(actor,id,origin,d,profile);
            return true;
        }
        string NearestVehicleExcept(Float3 origin,Float3 direction,float range,string except,out float distance)
        {
            distance=range;string id=null;if(!VehiclesLive)return null;
            foreach(var row in vehicles.Values){if(row.Destroyed||row.Id==except)continue;float t=VehicleBoxRay(origin,direction,VehicleRoot(row),VehicleYaw(row),range,0,row.Kind);if(t<distance){distance=t;id=row.Id;}}
            return id;
        }
    }
}
