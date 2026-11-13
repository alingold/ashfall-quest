using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Integration.Playable.Vehicles;
namespace Ashfall.Integration.Playable.Networking
{
    // R5 (playtest 4, lobby-v18): tank aimed by the rider's right hand (yaw and
    // pitch) and mounted machine guns on the helicopter and hover bike. Both
    // ride the existing ordered vehicle.fire/1 command with a mode byte after
    // the vehicle id; an id-only payload is still a plain tank shot.
    public sealed partial class R24LanLobby
    {
        const byte VehicleModeAim=1,VehicleModeAimFire=2,VehicleModeGun=3;
        public const float TankPitchMinimum=-10,TankPitchMaximum=35,VehicleGunInterval=.1f,VehicleGunRange=150;
        sealed class TankAim{internal float Yaw,Pitch,Hull;internal double At;}
        readonly Dictionary<string,TankAim> tankAims=new Dictionary<string,TankAim>(StringComparer.Ordinal);
        readonly Dictionary<string,double> vehicleGunShots=new Dictionary<string,double>(StringComparer.Ordinal);
        public int AcceptedVehicleGunShots{get;private set;}
        public string LastVehicleGunRejection{get;private set;}
        public static float ClampTankPitch(float pitch){return Single.IsNaN(pitch)||Single.IsInfinity(pitch)?0:Math.Max(TankPitchMinimum,Math.Min(TankPitchMaximum,pitch));}
        public static Float3 TankShellDirection(float yaw,float pitch)
        {
            double a=yaw*Math.PI/180,p=ClampTankPitch(pitch)*Math.PI/180;
            return new Float3((float)(Math.Sin(a)*Math.Cos(p)),(float)Math.Sin(p),(float)(Math.Cos(a)*Math.Cos(p)));
        }
        bool SendVehicleMode(string id,byte mode,float[] values)
        {
            Check();if(!Wire.IsText(id,32,false))return false;
            foreach(float v in values)if(Single.IsNaN(v)||Single.IsInfinity(v))return false;
            if(State!=R24LanLobbyState.Connected)return false;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){Write(w,id,32);w.Write(mode);foreach(float v in values)w.Write(v);return QueueGameCommand(VehicleFire,s.ToArray());}
        }
        public bool RequestTankAim(string id,float yaw,float pitch,float hull)
        {
            if(State==R24LanLobbyState.Hosting)return ApplyTankAim(OwnActor,id,yaw,pitch,hull);
            return SendVehicleMode(id,VehicleModeAim,new[]{yaw,pitch,hull});
        }
        public bool RequestTankFireAimed(string id,float yaw,float pitch,float hull)
        {
            if(State==R24LanLobbyState.Hosting){ApplyTankAim(OwnActor,id,yaw,pitch,hull);return ApplyTankFire(OwnActor,id);}
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
                if(ApplyTankAim(actor,id,yaw,pitch,hull)&&mode==VehicleModeAimFire)ApplyTankFire(actor,id);
            }else if(mode==VehicleModeGun){
                var origin=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());var direction=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                if(s.Position==s.Length)ApplyVehicleGun(actor,id,origin,direction);
            }
        }
        bool ApplyTankAim(string actor,string id,float yaw,float pitch,float hull)
        {
            R25VehicleState row;
            if(Single.IsNaN(yaw)||Single.IsInfinity(yaw)||Math.Abs(yaw)>720||Single.IsNaN(pitch)||Single.IsInfinity(pitch)||Single.IsNaN(hull)||Single.IsInfinity(hull)||Math.Abs(hull)>720||!vehicles.TryGetValue(id,out row)||row.Kind!="tank"||row.Destroyed||row.Rider!=actor)return false;
            TankAim aim;if(!tankAims.TryGetValue(id,out aim)){aim=new TankAim();tankAims.Add(id,aim);}
            aim.Yaw=((yaw%360)+360)%360;aim.Pitch=ClampTankPitch(pitch);aim.Hull=((hull%360)+360)%360;aim.At=clock.Now;return true;
        }
        bool FreshTankAim(R25VehicleState row,out TankAim aim)
        {return tankAims.TryGetValue(row.Id,out aim)&&clock.Now-aim.At<1.5;}
        bool RejectVehicleGun(string why){LastVehicleGunRejection=why;return false;}
        bool ApplyVehicleGun(string actor,string id,Float3 origin,Float3 direction)
        {
            R25VehicleState row;R24LanRawTrackingSample raw;R24LanBallisticProfile profile;
            if(!VehiclesLive||ControlsBlocked(actor)||!vehicles.TryGetValue(id,out row)||row.Destroyed||row.Rider!=actor||row.Kind=="tank")return RejectVehicleGun("custody");
            if(row.Kind=="helicopter"&&!GameplayRules.Helicopters)return RejectVehicleGun("rules");
            if(!origin.IsFinite||!direction.IsFinite||!VehicleRiderReady(actor,out raw))return RejectVehicleGun("tracking");
            if(Distance2(raw.Pose.Root.Position,origin)>25)return RejectVehicleGun("origin");
            double length=Math.Sqrt((double)direction.X*direction.X+(double)direction.Y*direction.Y+(double)direction.Z*direction.Z);if(length<.5||length>1.5)return RejectVehicleGun("direction");
            double now=clock.Now,last;if(vehicleGunShots.TryGetValue(actor,out last)&&now-last<VehicleGunInterval*.75)return RejectVehicleGun("cadence");
            vehicleGunShots[actor]=now;AcceptedVehicleGunShots++;LastVehicleGunRejection=null;
            if(!CombatLive||ballisticTrace==null||!R24LanBallisticProfile.TryRead("m60",out profile))return true;
            if(rawWorldClear==null||!rawWorldClear(raw.Pose.Head.Position,origin))return true;
            var d=Normalize(direction);float nearest=ballisticTrace(origin,d,VehicleGunRange);if(Single.IsNaN(nearest)||nearest<0||nearest>VehicleGunRange)nearest=0;
            nearest=GuardDistance(actor,origin,d,nearest);float panelDistance;string panel=NearestSharedPanel(origin,d,nearest,out panelDistance);if(panel!=null)nearest=panelDistance;
            float vehicleDistance;string hitVehicle=NearestVehicleExcept(origin,d,nearest,id,out vehicleDistance);if(hitVehicle!=null){nearest=vehicleDistance;panel=null;}
            string target=null;bool headshot=false;double sampleAt=Math.Max(now-.20,now);
            foreach(var other in sessions.CaptureMembers()){
                if(other.ActorId==actor||!other.Loaded||!other.HasPosition)continue;var oh=hostHealth.Read(other.CanonicalActorId);if(oh==null||!oh.Connected||oh.Life!=Ashfall.Modules.HealthStates.LifeState.Alive)continue;
                R24LanRawTrackingSample pose;if(!Fresh(other.ActorId,sampleAt,out pose))continue;var head=pose.Pose.Head.Position;
                float h=SphereRay(origin,d,head,.17f),c=SphereRay(origin,d,new Float3(head.X,head.Y-.45f,head.Z),.29f),hip=SphereRay(origin,d,new Float3(head.X,head.Y-.92f,head.Z),.27f),t=Math.Min(h,Math.Min(c,hip));
                if(t<nearest){nearest=t;target=other.ActorId;headshot=h<=Math.Min(c,hip);}
            }
            if(target==null){if(hitVehicle!=null)DamageVehicle(hitVehicle,profile.Damage(1,false,nearest),true);else if(panel!=null)DamageSharedPanel(panel,profile.Damage(1,false,nearest)*GameplayRules.BuildDamage);return true;}
            ApplyCombatDamage(actor,target,profile.Damage(1,headshot,nearest)*(headshot?roundRules.Headshots:1),false);
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
