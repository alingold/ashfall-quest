using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Vehicles;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed class R25VehicleState
    {
        public readonly string Id, Rider;
        public readonly string Kind;
        public readonly float TurretYaw;public readonly double FiredAt;
        public readonly Float3 Home, Rest;
        public readonly float HomeYaw, Yaw, Health;
        public readonly double RespawnAt;
        public readonly long Revision;
        public bool Destroyed { get { return Health<=0; } }
        internal R25VehicleState(string id,Float3 home,float homeYaw,Float3 rest,float yaw,
            float health,string rider,double respawn,long revision,string kind="hoverbike",float turretYaw=0,double firedAt=0)
        {Kind=kind;TurretYaw=turretYaw;FiredAt=firedAt;Id=id;Home=home;HomeYaw=homeYaw;Rest=rest;Yaw=yaw;Health=health;Rider=rider;RespawnAt=respawn;Revision=revision;}
    }
    public sealed partial class R24LanLobby
    {
        const string VehicleFeature="playable.vehicle.state/1";
        const string VehicleMount="playable.vehicle.mount/1",VehicleDismount="playable.vehicle.dismount/1";
        readonly Dictionary<string,R25VehicleState> vehicles=new Dictionary<string,R25VehicleState>(StringComparer.Ordinal);
        readonly Dictionary<string,Dictionary<string,byte[]>> vehiclePending=new Dictionary<string,Dictionary<string,byte[]>>(StringComparer.Ordinal);
        double vehicleTick;
        public Func<string,bool> VehicleTankSite;
        public Func<Float3,bool> VehicleTankClear;
        public Func<Float3,bool> VehicleHelicopterClear;
        public Func<float> VehicleMapTop;
        public Func<Float3,float,Float3,Float3> VehicleHelicopterSweep;
        readonly Dictionary<string,Float3> helicopterPrevious=new Dictionary<string,Float3>();
        readonly Dictionary<string,double> helicopterImpactAt=new Dictionary<string,double>();
        long vehicleSequence;bool vehicleSpawned,vehicleRuntimeBound;
        public R25VehicleState[] Vehicles { get { Check();return CopySnapshot(vehicles.Values); } }
        // The complete rider/presentation adapter opts in at composition time.
        // With that adapter absent, native play must not get invisible chassis.
        public void BindVehicleRuntime()
        {
            Check();if(State!=R24LanLobbyState.Hosting||vehicleRuntimeBound)throw new InvalidOperationException("One host vehicle runtime binding");
            vehicleRuntimeBound=true;
        }
        bool VehiclesLive { get { return vehicleRuntimeBound&&InventoryLive&&GameplayRules.Vehicles; } }
        public static int VehicleSpawnCount(string map,int players)
        {
            if(String.IsNullOrEmpty(map)||map.IndexOf("indoor",StringComparison.OrdinalIgnoreCase)>=0||map.IndexOf("paintball",StringComparison.OrdinalIgnoreCase)>=0)return 0;
            return map=="range"?2:Math.Min(8,Math.Max(2,players/2));
        }
        void ClearVehicles()
        {vehicles.Clear();vehiclePending.Clear();vehicleSpawned=false;vehicleTick=0;helicopterPrevious.Clear();helicopterImpactAt.Clear();}
        void SpawnVehicles()
        {
            if(vehicleSpawned||!VehiclesLive||supplyDropWorld==null||!HostSpawnWorldReady)return;
            vehicleSpawned=true;
            int count=VehicleSpawnCount(MapId,members.Length),made=0;
            if(count==0)return;
            using(spawnWorld.Begin(MapId,OwnActor,SelectedMap.Rules==null)){
                var sites=spawnWorld.Sites(MapId,SelectedMap.Rules==null);
                if(sites==null)return;
                foreach(var site in sites){
                    if(site==null||site.CapsulePlatform)continue;
                    for(int side=0;side<4&&made<count;side++){
                        float dx=side==0?3.2f:side==1?-3.2f:0,dz=side==2?3.2f:side==3?-3.2f:0;
                        var desired=new Float3(site.Feet.X+dx,site.Feet.Y,site.Feet.Z+dz);Float3 at;
                        // Reuse the host's ground/clearance oracle and spawn safety oracle.
                        if(!supplyDropWorld.TryPlace("builds",desired,out at)||!at.IsFinite||Distance2(desired,at)>4||
                            Distance2(site.Feet,at)<6.25||Distance2(site.Feet,at)>16||!spawnWorld.IsSafe(MapId,OwnActor,at))continue;
                        bool overlap=false;foreach(var old in vehicles.Values)if(Distance2(old.Home,at)<9)overlap=true;
                        if(overlap)continue;
                        at=new Float3(at.X,at.Y+.55f,at.Z);
                        PublishVehicle(new R25VehicleState("bike/"+made,at,0,at,0,300,"",0,++vehicleSequence));made++;
                    }
                    if(made>=count)break;
                }

            }
        }
        void SpawnTank()
        {
            if(!VehiclesLive||!HostSpawnWorldReady||supplyDropWorld==null||vehicles.ContainsKey("tank/0")||VehicleSpawnCount(MapId,members.Length)==0)return;
            using(spawnWorld.Begin(MapId,OwnActor,SelectedMap.Rules==null)){
                var sites=spawnWorld.Sites(MapId,SelectedMap.Rules==null);if(sites==null)return;
                if(GameplayRules.Tanks&&VehicleTankSite!=null&&VehicleTankSite(MapId))foreach(var site in sites){
                    if(site==null||site.CapsulePlatform)continue;
                    Float3 at;var desired=new Float3(site.Feet.X+9,site.Feet.Y,site.Feet.Z+9);
                    if(!supplyDropWorld.TryPlace("builds",desired,out at)||!at.IsFinite||Distance2(desired,at)>4||!spawnWorld.IsSafe(MapId,OwnActor,at))continue;
                    bool clear=true;foreach(var old in vehicles.Values)if(Distance2(old.Home,at)<36)clear=false;
                    // Native callback includes full hull clearance at the candidate ground point.
                    if(!clear||VehicleTankClear==null||!VehicleTankClear(at))continue;
                    at=new Float3(at.X,at.Y+.55f,at.Z);
                    PublishVehicle(new R25VehicleState("tank/0",at,0,at,0,1800,"",0,++vehicleSequence,"tank"));break;
                }
            }
        }
        void SpawnHelicopter()
        {
            if(!VehiclesLive||!GameplayRules.Helicopters||!HostSpawnWorldReady||supplyDropWorld==null||vehicles.ContainsKey("helicopter/0")||VehicleTankSite==null||!VehicleTankSite(MapId)||VehicleHelicopterClear==null||rawWorldClear==null)return;
            using(spawnWorld.Begin(MapId,OwnActor,SelectedMap.Rules==null)){
                var sites=spawnWorld.Sites(MapId,SelectedMap.Rules==null);if(sites==null)return;
                foreach(var site in sites){
                    if(site==null||site.CapsulePlatform)continue;
                    Float3 at;var desired=new Float3(site.Feet.X-12,site.Feet.Y,site.Feet.Z+9);
                    if(!supplyDropWorld.TryPlace("builds",desired,out at)||!at.IsFinite||Distance2(desired,at)>4||!spawnWorld.IsSafe(MapId,OwnActor,at))continue;
                    bool clear=true;foreach(var old in vehicles.Values)if(Distance2(old.Home,at)<100)clear=false;
                    var home=new Float3(at.X,at.Y+.65f,at.Z);
                    if(!clear||!rawWorldClear(home,new Float3(home.X,home.Y+30,home.Z))||!VehicleHelicopterClear(at))continue;
                    PublishVehicle(new R25VehicleState("helicopter/0",home,0,home,0,600,"",0,++vehicleSequence,"helicopter"));break;
                }
            }
        }
        // Custody alone grants the existing track envelope (55 horizontal, 120 rise,
        // 360 fall m/s), already wider than the 18/6 aircraft motor. Enforce ceiling too.
        bool VehicleTrackAllowed(string actor,Float3 root)
        {
            foreach(var row in vehicles.Values)if(row.Kind=="helicopter"&&row.Rider==actor&&!row.Destroyed&&VehiclesLive&&GameplayRules.Helicopters){
                float top=VehicleMapTop==null?row.Home.Y+80:VehicleMapTop();
                return root.IsFinite&&root.Y<=R25HelicopterDynamics.Ceiling(row.Home.Y-.65f,top)+.2f;
            }return true;
        }
        void TickHelicopterContact(R25VehicleState row)
        {
            if(row.Kind!="helicopter")return;
            if(row.Destroyed||row.Rider==""){helicopterPrevious.Remove(row.Id);helicopterImpactAt.Remove(row.Id);return;}
            var root=VehicleRoot(row);Float3 previous;
            if(helicopterPrevious.TryGetValue(row.Id,out previous)&&VehicleHelicopterSweep!=null){
                var motion=Sub(root,previous);double elapsed=Math.Max(.01,clock.Now-vehicleTick);
                float length=(float)Math.Sqrt(Distance2(root,previous));
                if(length>0){
                    var extended=new Float3(motion.X*(1+.08f/length),motion.Y*(1+.08f/length),motion.Z*(1+.08f/length));
                    var accepted=VehicleHelicopterSweep(previous,VehicleYaw(row),extended);double last;
                    if(Distance2(extended,accepted)>.0001&&length/elapsed>4&&(!helicopterImpactAt.TryGetValue(row.Id,out last)||clock.Now-last>.5)){
                        helicopterImpactAt[row.Id]=clock.Now;DamageVehicle(row.Id,(float)Math.Min(300,(length/elapsed-4)*20));
                    }
                }
            }
            helicopterPrevious[row.Id]=root;
        }
        void PendingVehicle(string peer,string id,byte[] frame)
        {
            Dictionary<string,byte[]> rows;
            if(!vehiclePending.TryGetValue(peer,out rows)){rows=new Dictionary<string,byte[]>(StringComparer.Ordinal);vehiclePending[peer]=rows;}
            rows[id]=frame;
        }
        void PublishVehicle(R25VehicleState row)
        {vehicles[row.Id]=row;byte[] frame=VehicleFrame(row);foreach(var p in peers)if(p.Value.Admitted)PendingVehicle(p.Key,row.Id,frame);}
        byte[] VehicleFrame(R25VehicleState row)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){
                Write(w,row.Id,32);Write(w,row.Kind,10);if(row.Rider=="")w.Write((ushort)0);else Write(w,row.Rider,64);
                foreach(var v in new[]{row.Home,row.Rest}){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}
                w.Write(row.HomeYaw);w.Write(row.Yaw);w.Write(row.Health);w.Write(row.RespawnAt);w.Write(row.TurretYaw);w.Write(row.FiredAt);
                return Packet(PacketKind.Snapshot,VehicleFeature,OwnActor,s.ToArray(),row.Revision);
            }
        }
        void ReplayVehicles(string peer)
        {foreach(var row in vehicles.Values)PendingVehicle(peer,row.Id,VehicleFrame(row));}
        void ReadVehicle(Packet packet)
        {
            if(packet.BaseSequence!=-1||packet.Sequence<1)return;
            try{using(var s=new MemoryStream(packet.Payload))using(var r=new BinaryReader(s,Utf8)){
                string id=Read(r,32),kind=Read(r,10),rider=ReadVehicleRider(r);
                var home=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                var rest=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                float hy=r.ReadSingle(),yaw=r.ReadSingle(),health=r.ReadSingle();double respawn=r.ReadDouble();float turret=r.ReadSingle();double fired=r.ReadDouble();
                if(s.Position!=s.Length||!R25VehicleKinds.Valid(kind)||!VehiclePoint(home)||!VehiclePoint(rest)||
                    !Finite(hy)||!Finite(yaw)||Math.Abs(hy)>360||Math.Abs(yaw)>360||!Finite(health)||health<0||health>R25VehicleKinds.Health(kind)||!Finite(turret)||Math.Abs(turret)>360||!Finite(fired)||fired<0||
                    !Finite(respawn)||respawn<0||health==0&&rider!=""||health>0&&respawn!=0||
                    !vehicles.ContainsKey(id)&&vehicles.Count>=16)return;
                foreach(var row in vehicles.Values)if(rider!=""&&row.Id!=id&&row.Rider==rider)return;
                R25VehicleState prior;if(vehicles.TryGetValue(id,out prior)&&prior.Revision>=packet.Sequence)return;
                vehicles[id]=new R25VehicleState(id,home,hy,rest,yaw,health,rider,respawn,packet.Sequence,kind,turret,fired);
            }}catch(IOException){}catch(ArgumentException){}
        }
        // Lobby.Read requires nonempty text; rider intentionally permits an empty seat.
        static string ReadVehicleRider(BinaryReader r)
        {int n=r.ReadUInt16();if(n>64)throw new InvalidDataException();byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();string actor=Utf8.GetString(b);if(n>0&&!Wire.IsText(actor,64,false))throw new InvalidDataException();return actor;}
        static bool VehiclePoint(Float3 p)
        {return p.IsFinite&&Math.Abs(p.X)<=100000&&Math.Abs(p.Y)<=100000&&Math.Abs(p.Z)<=100000;}
        public bool RequestVehicleMount(string id,int hand)
        {
            Check();if(!Wire.IsText(id,32,false)||hand<0||hand>1)return false;
            if(State==R24LanLobbyState.Hosting)return ApplyVehicleMount(OwnActor,id,hand);
            if(State!=R24LanLobbyState.Connected)return false;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){Write(w,id,32);w.Write((byte)hand);return QueueGameCommand(VehicleMount,s.ToArray());}
        }
        bool VehicleRiderReady(string actor,out R24LanRawTrackingSample raw)
        {
            raw=null;var member=sessions==null?null:sessions.Get(actor);R24LanHealthState health;
            return VehiclesLive&&member!=null&&member.Life==SessionLife.Alive&&!PodCarried(actor)&&
                healthStates.TryGetValue(actor,out health)&&health.Life==LifeState.Alive&&Fresh(actor,clock.Now,out raw)&&!raw.Pose.Paddles;
        }
        static Float3 VehicleMountMarker(R25VehicleState row,Float3 hand)
        {
            var left=R25BikeRider.MountPoint(row.Rest,row.Yaw,row.Kind,-1);var right=R25BikeRider.MountPoint(row.Rest,row.Yaw,row.Kind,1);
            return Distance2(hand,left)<Distance2(hand,right)?left:right;
        }
        static bool VehicleMountReach(R25VehicleState row,Float3 hand)
        {return Distance2(hand,row.Rest)<=1.44||Distance2(hand,VehicleMountMarker(row,hand))<=R25BikeRider.MountReach*R25BikeRider.MountReach;}
        bool ApplyVehicleMount(string actor,string id,int hand)
        {
            R25VehicleState row;R24LanRawTrackingSample raw;
            if(hand<0||hand>1||pulls.ContainsKey(actor)||ControlsBlocked(actor)||!VehicleRiderReady(actor,out raw)||!vehicles.TryGetValue(id,out row)||row.Destroyed||row.Rider!=""||row.Kind=="tank"&&!GameplayRules.Tanks||row.Kind=="helicopter"&&!GameplayRules.Helicopters||
                (raw.Pose.Flags&(hand==0?2:4))==0||!VehicleMountReach(row,hand==0?raw.Pose.Left.Position:raw.Pose.Right.Position)||
                rawWorldClear==null||!rawWorldClear(raw.Pose.Head.Position,VehicleMountMarker(row,hand==0?raw.Pose.Left.Position:raw.Pose.Right.Position)))return false;
            foreach(var bike in vehicles.Values)if(bike.Rider==actor)return false;
            RemoveWeb(actor);
            PublishVehicle(new R25VehicleState(row.Id,row.Home,row.HomeYaw,row.Rest,row.Yaw,row.Health,actor,0,++vehicleSequence,row.Kind,row.TurretYaw,row.FiredAt));return true;
        }
        public bool RequestVehicleDismount(string id,Float3 rest,float yaw)
        {
            Check();if(!Wire.IsText(id,32,false))return false;
            if(State==R24LanLobbyState.Hosting)return ApplyVehicleDismount(OwnActor,id,rest,yaw);
            if(State!=R24LanLobbyState.Connected)return false;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){Write(w,id,32);w.Write(rest.X);w.Write(rest.Y);w.Write(rest.Z);w.Write(yaw);return QueueGameCommand(VehicleDismount,s.ToArray());}
        }
        Float3 VehicleRoot(R25VehicleState row)
        {
            R24LanRawTrackingHistory history;
            if(row.Rider!=""&&rawTracking.TryGetValue(row.Rider,out history)&&history.Latest!=null)return history.Latest.Pose.Root.Position;
            return row.Rest;
        }
        float VehicleYaw(R25VehicleState row)
        {
            R24LanRawTrackingHistory history;
            if(row.Kind=="tank")return row.Yaw;
            if(row.Rider!=""&&rawTracking.TryGetValue(row.Rider,out history)&&history.Latest!=null){
                var q=history.Latest.Pose.Root.Rotation;
                return (float)(Math.Atan2(2*(q.W*q.Y+q.X*q.Z),1-2*(q.Y*q.Y+q.Z*q.Z))*180/Math.PI);
            }
            return row.Yaw;
        }
        bool ApplyVehicleDismount(string actor,string id,Float3 rest,float yaw)
        {
            R25VehicleState row;
            if(!VehiclesLive||!vehicles.TryGetValue(id,out row)||row.Rider!=actor)return false;
            var root=VehicleRoot(row);
            // Aircraft may park directly below an airborne rider, on clear supported ground.
            bool below=row.Kind=="helicopter"&&VehiclePoint(rest)&&Math.Abs(root.X-rest.X)<.05f&&Math.Abs(root.Z-rest.Z)<.05f&&rest.Y<=root.Y+.1f&&root.Y-rest.Y<=2000&&rawWorldClear!=null&&rawWorldClear(root,rest)&&!rawWorldClear(rest,new Float3(rest.X,rest.Y-.7f,rest.Z));
            if(!below&&(!VehiclePoint(rest)||Distance2(root,rest)>9||rawWorldClear==null||!rawWorldClear(root,rest)))rest=root;
            if(!Finite(yaw)||Math.Abs(yaw)>360)yaw=row.Yaw;
            PublishVehicle(new R25VehicleState(row.Id,row.Home,row.HomeYaw,rest,yaw,row.Health,"",0,++vehicleSequence,row.Kind,row.TurretYaw,row.FiredAt));return true;
        }
        void ReadVehicleCommand(string actor,string feature,byte[] bytes)
        {
            try{using(var s=new MemoryStream(bytes))using(var r=new BinaryReader(s,Utf8)){
                string id=Read(r,32);
                if(feature==VehicleFire){if(s.Position==s.Length)ApplyTankFire(actor,id);else ReadVehicleMode(actor,id,r,s);}
                else if(feature==VehicleMount){int hand=r.ReadByte();if(s.Position==s.Length)ApplyVehicleMount(actor,id,hand);}
                else{var rest=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());float yaw=r.ReadSingle();if(s.Position==s.Length)ApplyVehicleDismount(actor,id,rest,yaw);}
            }}catch(IOException){}catch(ArgumentException){}
        }
        void EjectVehicleRider(string actor)
        {
            foreach(var row in new List<R25VehicleState>(vehicles.Values))if(row.Rider==actor)
                PublishVehicle(new R25VehicleState(row.Id,row.Home,row.HomeYaw,VehicleRoot(row),VehicleYaw(row),row.Health,"",0,++vehicleSequence,row.Kind,row.TurretYaw,row.FiredAt));
        }
        internal bool DamageVehicle(string id,float amount,bool bullet=false)
        {
            R25VehicleState row;if(!VehiclesLive||!Finite(amount)||amount<=0||!vehicles.TryGetValue(id,out row)||row.Destroyed)return false;
            float health=Math.Max(0,row.Health-amount*(bullet?R25VehicleKinds.BulletScale(row.Kind):1));
            PublishVehicle(new R25VehicleState(row.Id,row.Home,row.HomeYaw,VehicleRoot(row),VehicleYaw(row),health,health==0?"":row.Rider,health==0?clock.Now+20:0,++vehicleSequence,row.Kind,row.TurretYaw,row.FiredAt));return true;
        }
        // Chassis OBB, centered on hover root; no rider impact/contact damage.
        internal static float VehicleBoxRay(Float3 origin,Float3 direction,Float3 center,float yaw,float range,float margin=0,string kind="hoverbike")
        {
            double a=yaw*Math.PI/180;float c=(float)Math.Cos(a),s=(float)Math.Sin(a);
            if(kind=="helicopter")center=R25HelicopterClearance.Center(center,yaw,0);
            Float3 o=Sub(origin,center);
            float[] p={o.X*c-o.Z*s,o.Y,o.X*s+o.Z*c},d={direction.X*c-direction.Z*s,direction.Y,direction.X*s+direction.Z*c},h={(kind=="tank"?1.35f:kind=="helicopter"?.85f:.45f)+margin,(kind=="tank"?.55f:kind=="helicopter"?.63f:.35f)+margin,(kind=="tank"?2.2f:kind=="helicopter"?3.25f:1.1f)+margin};
            float near=0,far=range;
            for(int i=0;i<3;i++){
                if(Math.Abs(d[i])<.000001f){if(Math.Abs(p[i])>h[i])return Single.PositiveInfinity;continue;}
                float lo=(-h[i]-p[i])/d[i],hi=(h[i]-p[i])/d[i];if(lo>hi){float t=lo;lo=hi;hi=t;}
                near=Math.Max(near,lo);far=Math.Min(far,hi);if(near>far)return Single.PositiveInfinity;
            }return near;
        }
        string NearestVehicle(Float3 origin,Float3 direction,float range,out float distance)
        {
            distance=range;string id=null;if(!VehiclesLive)return null;
            foreach(var row in vehicles.Values){if(row.Destroyed)continue;float t=VehicleBoxRay(origin,direction,VehicleRoot(row),VehicleYaw(row),range,0,row.Kind);if(t<distance){distance=t;id=row.Id;}}
            return id;
        }
        bool SweepVehicleProjectile(Float3 from,Float3 direction,float range,float radius,ref float distance,ref Float3 normal)
        {
            bool hit=false;if(!VehiclesLive)return false;
            foreach(var row in vehicles.Values){
                if(row.Destroyed)continue;var center=VehicleRoot(row);float yaw=VehicleYaw(row);
                float t=VehicleBoxRay(from,direction,center,yaw,range,radius,row.Kind);
                if(t>distance||Single.IsPositiveInfinity(t))continue;
                distance=t;hit=true;
                var at=new Float3(from.X+direction.X*t-center.X,from.Y+direction.Y*t-center.Y,from.Z+direction.Z*t-center.Z);
                double a=yaw*Math.PI/180;float c=(float)Math.Cos(a),s=(float)Math.Sin(a);
                float x=at.X*c-at.Z*s,z=at.X*s+at.Z*c;
                float dx=Math.Abs(Math.Abs(x)-(.45f+radius)),dy=Math.Abs(Math.Abs(at.Y)-(.35f+radius)),dz=Math.Abs(Math.Abs(z)-(1.1f+radius));
                normal=dy<=dx&&dy<=dz?new Float3(0,at.Y<0?-1:1,0):dx<=dz?new Float3((x<0?-1:1)*c,0,-(x<0?-1:1)*s):new Float3((z<0?-1:1)*s,0,(z<0?-1:1)*c);
            }
            return hit;
        }
        void TickVehicles()
        {
            if(State==R24LanLobbyState.Hosting){
                SpawnVehicles();SpawnTank();SpawnHelicopter();
                TickTankTurrets();
                foreach(var row in new List<R25VehicleState>(vehicles.Values)){
                    R24LanRawTrackingSample raw;
                    TickHelicopterContact(row);
                    if(row.Rider!=""&&row.Kind=="helicopter"&&!GameplayRules.Helicopters)EjectVehicleRider(row.Rider);
                    if(row.Rider!=""&&!VehicleRiderReady(row.Rider,out raw))EjectVehicleRider(row.Rider);
                    if(VehiclesLive&&row.Destroyed&&clock.Now>=row.RespawnAt)
                        PublishVehicle(new R25VehicleState(row.Id,row.Home,row.HomeYaw,row.Home,row.HomeYaw,R25VehicleKinds.Health(row.Kind),"",0,++vehicleSequence,row.Kind,row.HomeYaw,0));
                }
                vehicleTick=clock.Now;
                foreach(var peer in new List<string>(vehiclePending.Keys)){
                    if(!peers.ContainsKey(peer)){vehiclePending.Remove(peer);continue;}
                    var rows=vehiclePending[peer];foreach(var id in new List<string>(rows.Keys))if(Send(peer,rows[id]))rows.Remove(id);
                    if(rows.Count==0)vehiclePending.Remove(peer);
                }
            }
        }
    }
}
