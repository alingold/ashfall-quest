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
        const string VehicleFire="playable.vehicle.fire/1";
        void TickTankTurrets()
        {
            if(clock.Now-vehicleTick<.05)return;
            float dt=(float)Math.Min(.1,Math.Max(0,clock.Now-vehicleTick));vehicleTick=clock.Now;
            foreach(var row in new List<R25VehicleState>(vehicles.Values)){
                if(row.Kind!="tank"||row.Destroyed||row.Rider=="")continue;
                R24LanRawTrackingSample raw;if(!VehicleRiderReady(row.Rider,out raw)||!GameplayRules.Tanks){EjectVehicleRider(row.Rider);continue;}
                var q=raw.Pose.Head.Rotation;float head=R25BikeRider.Yaw(q.X,q.Y,q.Z,q.W);
                // R5: the turret follows the rider's right-hand aim when fresh.
                TankAim aim;bool aimed=FreshTankAim(row,out aim);if(aimed)head=aim.Yaw;
                // R4 steering is the left stick; the rider reports the hull yaw with its aim.
                float hull=aimed?R25VehicleKinds.Turn(row.Yaw,aim.Hull,dt,90):row.Yaw;
                float turret=R25VehicleKinds.Turn(row.TurretYaw,head,dt,90);
                if(Math.Abs(turret-row.TurretYaw)>.01f||Math.Abs(hull-row.Yaw)>.01f)
                    PublishVehicle(new R25VehicleState(row.Id,row.Home,row.HomeYaw,row.Rest,hull,row.Health,row.Rider,0,++vehicleSequence,row.Kind,turret,row.FiredAt));
            }
        }
        public bool RequestTankFire(string id)
        {
            Check();if(!Wire.IsText(id,32,false))return false;
            if(State==R24LanLobbyState.Hosting)return ApplyTankFire(OwnActor,id);
            if(State!=R24LanLobbyState.Connected)return false;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){Write(w,id,32);return QueueGameCommand(VehicleFire,s.ToArray());}
        }
        bool ApplyTankFire(string actor,string id)
        {
            R25VehicleState row;R24LanRawTrackingSample raw;
            if(!GameplayRules.Tanks||ControlsBlocked(actor)||!VehicleRiderReady(actor,out raw)||!vehicles.TryGetValue(id,out row)||row.Kind!="tank"||row.Destroyed||row.Rider!=actor||row.FiredAt>0&&clock.Now-row.FiredAt<R25VehicleKinds.TankReloadSeconds||projectileWorld==null||projectiles.Count>=64)return false;
            TankAim aim;float pitch=FreshTankAim(row,out aim)?aim.Pitch:4.6f;
            var direction=TankShellDirection(row.TurretYaw,pitch);var root=VehicleRoot(row);
            double a=row.TurretYaw*Math.PI/180;
            var origin=new Float3(root.X+(float)Math.Sin(a)*3.6f,root.Y+.65f+direction.Y*3.6f,root.Z+(float)Math.Cos(a)*3.6f);
            if(rawWorldClear==null||!rawWorldClear(new Float3(root.X,root.Y+.65f,root.Z),origin))return false;
            LaunchProjectile(actor,"shell",origin,new Float3(direction.X*45,direction.Y*45,direction.Z*45),225);
            PublishVehicle(new R25VehicleState(row.Id,row.Home,row.HomeYaw,row.Rest,row.Yaw,row.Health,actor,0,++vehicleSequence,row.Kind,row.TurretYaw,Math.Max(.000001,clock.Now)));return true;
        }
    }
}
