using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Integration.Playable.Vehicles;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        const string VehicleFire="playable.vehicle.fire/1";
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
            var direction=TankShellDirection(row.TurretYaw,row.GunPitch);var root=VehicleRoot(row);
            var origin=R25VehicleKinds.TankMuzzle(root,direction);
            if(rawWorldClear==null||!rawWorldClear(new Float3(root.X,root.Y+R25VehicleKinds.TankMuzzleHeight,root.Z),origin))return false;
            LaunchProjectile(actor,"shell",origin,new Float3(direction.X*45,direction.Y*45,direction.Z*45),225);
            PublishVehicle(new R25VehicleState(row.Id,row.Home,row.HomeYaw,row.Rest,row.Yaw,row.Health,actor,0,++vehicleSequence,row.Kind,row.TurretYaw,Math.Max(.000001,clock.Now),row.GunPitch));return true;
        }
    }
}
