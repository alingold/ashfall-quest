using System;
using System.Collections.Generic;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Combat;
using Ashfall.Modules.Construction;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    public struct R26BulletSurface
    {
        public float Distance;public Float3 Normal;public byte RangeTarget;
    }
    public struct R26BulletContact
    {
        public string Actor;public long Id;public byte Pellet,RangeTarget;public bool MapSurface;public Float3 Point,Normal;public float Damage;
    }
    public sealed partial class R24LanLobby
    {
        const byte ContactLayout=11;
        sealed class Flight
        {
            public string Actor,Mount;public long Id;public R24LanBallisticProfile Gun;public int Stars,Mask,Team;
            public double Start;public float Distance;public Float3 Origin;public Float3[] Rays;
        }
        struct VehicleCover{internal string Id,Kind;internal Float3 Center;internal float Yaw;}
        struct GuardCover{internal string Owner;internal BulletGuard Guard;}
        sealed class CoverSample
        {
            internal double Time;internal PlacedPanel[] Panels;
            internal readonly List<VehicleCover> Vehicles=new List<VehicleCover>();
            internal readonly List<GuardCover> Guards=new List<GuardCover>();
        }
        readonly CoverSample[] coverHistory=new CoverSample[32];int coverNext,coverCount;
        long panelVersion=-1;PlacedPanel[] panelShapes;
        void CaptureCover()
        {
            int slot=coverNext;var last=coverCount==0?null:coverHistory[(coverNext+31)%32];
            if(last!=null&&Math.Floor(clock.Now*128)==Math.Floor(last.Time*128))slot=(coverNext+31)%32;
            else{coverNext=(coverNext+1)%32;coverCount=Math.Min(32,coverCount+1);}
            var frame=coverHistory[slot]??(coverHistory[slot]=new CoverSample());frame.Time=clock.Now;
            if(panelVersion!=constructionRevision||panelShapes==null){panelShapes=SharedPanels;panelVersion=constructionRevision;}
            frame.Panels=panelShapes;frame.Vehicles.Clear();frame.Guards.Clear();
            if(VehiclesLive)foreach(var v in vehicles.Values)if(!v.Destroyed)frame.Vehicles.Add(new VehicleCover{Id=v.Id,Kind=v.Kind,Center=VehicleRoot(v),Yaw=VehicleYaw(v)});
            foreach(var pair in equipment){BulletGuard guard;if(TryFlightGuard(pair.Key,pair.Value,out guard))frame.Guards.Add(new GuardCover{Owner=pair.Key,Guard=guard});}
        }
        bool TryFlightGuard(string actor,EquippedWeapon e,out BulletGuard guard)
        {
            guard=null;if(e.Id==null||e.Blade==null||e.Suppressed||e.Blocked||clock.Now-e.At>.5||clock.Now-e.SampleAt>.15)return false;
            var member=sessions.Get(actor);var h=member==null?null:hostHealth.Read(member.CanonicalActorId);CombatActor owner;R24LanRawTrackingSample pose;
            return h!=null&&h.Life==LifeState.Alive&&combat.TryGetValue(actor,out owner)&&owner.Weapons.ContainsKey(e.Id)&&Fresh(actor,clock.Now,out pose)&&e.Blade.TryReadBulletGuard(e.Model,true,out guard);
        }
        CoverSample CoverAt(double at)
        {
            int i=(coverNext+31)%32;for(int n=1;n<coverCount&&coverHistory[i].Time>at+.000001;n++)i=(i+31)%32;
            return coverHistory[i];
        }
        struct BodySweep{internal string Actor;internal Float3 Head,Delta;}
        readonly BodySweep[] movingBodies=new BodySweep[Capacity];
        int SampleBodies(Flight f,Ashfall.Modules.Sessions.MemberSnapshot[] roster,double t0,double t1)
        {
            int count=0;var histories=roundRules==null?inventoryTracking:rawTracking;
            foreach(var member in roster){
                if(member.ActorId==f.Actor||!member.Loaded||!member.HasPosition)continue;
                var hp=hostHealth.Read(member.CanonicalActorId);if(hp==null||!hp.Connected||hp.Life!=LifeState.Alive)continue;
                R24LanRawTrackingSample pose;if(!Fresh(member.ActorId,t1,out pose))continue;
                Float3 h0,h1;var history=histories[member.ActorId];if(!history.HeadAt(t1,out h1))continue;if(!history.HeadAt(t0,out h0))h0=h1;
                movingBodies[count++]=new BodySweep{Actor=member.ActorId,Head=h0,Delta=Sub(h1,h0)};
            }
            return count;
        }
        static bool NearSegment(Float3 from,Float3 to,Float3 p,float margin)
        {
            return p.X>=Math.Min(from.X,to.X)-margin&&p.X<=Math.Max(from.X,to.X)+margin&&p.Y>=Math.Min(from.Y,to.Y)-margin&&p.Y<=Math.Max(from.Y,to.Y)+margin&&p.Z>=Math.Min(from.Z,to.Z)-margin&&p.Z<=Math.Max(from.Z,to.Z)+margin;
        }
        readonly List<Flight> flights=new List<Flight>();
        readonly Stack<Flight> flightPool=new Stack<Flight>();
        readonly Queue<R26BulletContact> bulletContacts=new Queue<R26BulletContact>();
        readonly R26BulletContact[] contactsThisStep=new R26BulletContact[8];
        readonly string[] victimsThisStep=new string[8];readonly bool[] headsThisStep=new bool[8];readonly Float3[] anchorsThisStep=new Float3[8];
        Func<Float3,Float3,float,double,double,R26BulletSurface> firearmSurface;long contactReceived;
        public void BindFirearmSurface(Func<Float3,Float3,float,double,double,R26BulletSurface> trace){Check();firearmSurface=trace;}
        public bool TryDequeueBulletContact(out R26BulletContact value){value=default(R26BulletContact);if(bulletContacts.Count==0)return false;value=bulletContacts.Dequeue();return true;}
        void ClearBulletFlights(){flights.Clear();coverCount=coverNext=0;panelShapes=null;flightFrames.Clear();bulletContacts.Clear();contactReceived=0;}
        internal long LastLocalGameCommand{get{return gameCommandSequence;}}
        void LaunchMountedBullet(string actor,string mount,Float3 origin,Float3 direction,R24LanBallisticProfile profile)
        {
            if(actor==OwnActor)checked{gameCommandSequence++;}
            LaunchVolley(actor,new R24LanShot(profile.Id,0,clock.Now,origin,new[]{direction}),profile,1,false,mount);
        }
        void LaunchVolley(string actor,R24LanShot shot,R24LanBallisticProfile gun,int stars,bool blocked,string mount=null)
        {
            long id=gameCommandSequence;if(actor!=OwnActor)foreach(var peer in peers.Values)if(peer.Actor==actor){id=peer.LastGameCommand;break;}
            id=Math.Max(1,id);double start=Math.Max(clock.Now-.2,Math.Min(clock.Now,shot.Time));
            var history=roundRules==null?inventoryTracking:rawTracking;R24LanRawTrackingHistory self;
            if(history.TryGetValue(actor,out self))start=Math.Max(start,self.OldestTime);
            if(coverCount>0)start=Math.Max(start,coverHistory[(coverNext-coverCount+32)%32].Time);
            var rays=blocked?new Float3[0]:shot.Directions;for(int i=0;i<rays.Length;i++)rays[i]=Normalize(rays[i]);
            BroadcastRemoteVolley(actor,shot,rays,id,start);if(blocked){CancelBulletPrediction(actor,id);return;}
            var f=flightPool.Count>0?flightPool.Pop():new Flight();f.Actor=actor;f.Mount=mount;f.Id=id;f.Origin=shot.Origin;f.Rays=rays;f.Mask=(1<<rays.Length)-1;
            f.Gun=gun;f.Team=sessions.Get(actor).Team;f.Stars=stars;f.Start=start;f.Distance=0;flights.Add(f);
        }
        static Float3 Along(Float3 a,Float3 d,float length){return new Float3(a.X+d.X*length,a.Y+d.Y*length,a.Z+d.Z*length);}
        void TickBulletFlights()
        {
            if(!InventoryLive){if(flights.Count!=0||coverCount!=0)ClearBulletFlights();return;}
            CaptureCover();if(flights.Count==0)return;
            var roster=sessions.CaptureMembers();
            for(int i=0;i<flights.Count;){
                var f=flights[i];float end=f.Gun.Travel(clock.Now-f.Start);
                while(f.Mask!=0&&end>f.Distance){
                    // Resolve a backlog on the host timeline in small swept intervals.
                    float next=Math.Min(end,f.Distance+f.Gun.Speed*.02f);
                    SweepVolley(f,next,roster);f.Distance=next;
                }
                if(f.Mask!=0&&f.Distance<R24LanBallisticProfile.MaximumRange){i++;continue;}
                flights.RemoveAt(i);f.Rays=null;flightPool.Push(f);
            }
        }
        void SweepVolley(Flight f,float next,Ashfall.Modules.Sessions.MemberSnapshot[] roster)
        {
            int count=0;float length=next-f.Distance;
            double t0=f.Start+f.Distance/f.Gun.Speed,t1=f.Start+next/f.Gun.Speed;
            var cover=CoverAt(t1);int bodies=f.Mount!=null&&!CombatLive?0:SampleBodies(f,roster,t0,t1);
            for(int j=0;j<f.Rays.Length;j++){
                if((f.Mask&(1<<j))==0)continue;
                var d=f.Rays[j];var from=Along(f.Origin,d,f.Distance);
                var surface=firearmSurface!=null?firearmSurface(from,d,length,t0,t1):new R26BulletSurface{Distance=ballisticTrace(from,d,length)};
                float nearest=surface.Distance;if(!Finite(nearest)||nearest<0)nearest=0;
                bool map=nearest<=length&&(firearmSurface!=null||nearest<length);nearest=Math.Min(length,nearest);
                var end=Along(from,d,nearest);bool guarded=false;
                foreach(var g in cover.Guards){float fraction;if(g.Owner!=f.Actor&&g.Guard.Intersects(from,Along(from,d,nearest),out fraction)){nearest*=fraction;guarded=true;map=false;}}
                string panel=null,bike=null;
                var panels=cover.Time>=clock.Now-.000001&&panelVersion!=constructionRevision?SharedPanels:cover.Panels;
                foreach(var p in panels){if(!NearSegment(from,end,p.Shape.Center,8))continue;
                    float t=BoxRay(from,d,p.Shape,nearest);if(t<=nearest&&(panel==null||t<nearest||String.CompareOrdinal(p.Id,panel)<0)){panel=p.Id;nearest=t;}}
                foreach(var v in cover.Vehicles){if(v.Id==f.Mount||!NearSegment(from,end,v.Center,8))continue;
                    float t=VehicleBoxRay(from,d,v.Center,v.Yaw,nearest,0,v.Kind);if(t<nearest){bike=v.Id;panel=null;nearest=t;}}
                string victim=null;bool head=false;Float3 anchor=new Float3();
                for(int k=0;k<bodies;k++){
                    var body=movingBodies[k];var h0=body.Head;
                    var relative=Sub(Along(new Float3(),d,length),body.Delta);
                    if(!NearSegment(from,Along(from,relative,1),h0,1.3f))continue;
                    float rel=(float)Math.Sqrt(Distance2(relative,new Float3()));
                    if(rel<=0)continue;var u=Normalize(relative);
                    float h=MovingSphere(from,u,h0,.17f),c=MovingSphere(from,u,new Float3(h0.X,h0.Y-.45f,h0.Z),.29f),hip=MovingSphere(from,u,new Float3(h0.X,h0.Y-.92f,h0.Z),.27f);
                    float hit=Math.Min(h,Math.Min(c,hip))*length/rel;
                    if(hit<nearest||hit==nearest&&!map&&!guarded&&panel==null&&bike==null&&victim==null){nearest=hit;victim=body.Actor;anchor=Along(h0,body.Delta,hit/length);head=h<=Math.Min(c,hip);}
                }
                if(victim==null&&panel==null&&bike==null&&!map&&!guarded&&nearest>=length)continue;
                f.Mask&=~(1<<j);bool native=map&&victim==null&&panel==null&&bike==null;
                float damage=f.Gun.Damage(f.Stars,head,f.Distance+nearest);
                contactsThisStep[count]=new R26BulletContact{Actor=f.Actor,Id=f.Id,Pellet=(byte)j,Point=Along(from,d,nearest),Normal=native&&surface.Normal.IsFinite&&Distance2(surface.Normal,new Float3())>.1?surface.Normal:Along(new Float3(),d,-1),RangeTarget=native?surface.RangeTarget:(byte)0,MapSurface=native,Damage=damage};
                anchorsThisStep[count]=anchor;victimsThisStep[count]=victim;headsThisStep[count]=head;count++;
                if(victim==null){if(bike!=null&&CombatLive)DamageVehicle(bike,damage,true);else if(panel!=null&&(CombatLive||f.Mount!=null&&VehicleBuildDamageLive))DamageSharedPanel(panel,damage);}
            }
            if(count==0)return;
            BroadcastContacts(f,count);
            if(!CombatLive)return;
            for(int j=0;j<count;j++){
                string victim=victimsThisStep[j];if(victim==null)continue;float amount=0;bool head=false;
                for(int k=j;k<count;k++)if(victimsThisStep[k]==victim){amount+=contactsThisStep[k].Damage*(headsThisStep[k]?roundRules.Headshots:1);head|=headsThisStep[k];victimsThisStep[k]=null;}
                bool applied=ApplyCombatDamageAt(f.Actor,victim,amount,false,f.Team,head?2:1,f.Origin,null,anchorsThisStep[j]);
                if(ShotDiagnostics!=null)ShotDiagnostics("contact id="+f.Id+" shooter="+f.Actor+" victim="+victim+" launch="+f.Start.ToString("F5")+" now="+clock.Now.ToString("F5")+" applied="+applied);
            }
        }
        static float MovingSphere(Float3 from,Float3 unit,Float3 center,float radius){return Distance2(from,center)<=radius*radius?0:SphereRay(from,unit,center,radius);}
        void QueueContact(R26BulletContact contact){if(bulletContacts.Count>=512)bulletContacts.Dequeue();bulletContacts.Enqueue(contact);}
        readonly Dictionary<string,Queue<byte[]>> flightFrames=new Dictionary<string,Queue<byte[]>>(StringComparer.Ordinal);
        void SendFlight(string connection,byte[] frame)
        {
            Queue<byte[]> q;
            if(!flightFrames.TryGetValue(connection,out q)){if(Send(connection,frame))return;q=new Queue<byte[]>();flightFrames.Add(connection,q);}
            else if(q.Count==0&&Send(connection,frame))return;
            if(q.Count<128)q.Enqueue(frame);
        }
        void FlushFlights()
        {
            if(flightFrames.Count==0)return;
            foreach(var peer in peers){Queue<byte[]> q;if(!flightFrames.TryGetValue(peer.Key,out q))continue;
                int budget=16;while(q.Count!=0&&budget-->0&&Send(peer.Key,q.Peek()))q.Dequeue();}
        }
        void CancelBulletPrediction(string actor,long id)
        {
            var stop=new R26BulletContact{Actor=actor,Id=id,Pellet=255};
            if(actor==OwnActor){QueueContact(stop);return;}
            foreach(var peer in peers)if(peer.Value.Actor==actor){
                using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream,Utf8)){
                    w.Write(ContactLayout);Write(w,actor,64);w.Write(id);w.Write((byte)0);
                    SendFlight(peer.Key,Packet(PacketKind.Snapshot,ScoreFeature,OwnActor,stream.ToArray(),checked(++remoteVisualSequence)));
                }return;
            }
        }
        void BroadcastContacts(Flight f,int count)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream,Utf8)){
                w.Write(ContactLayout);Write(w,f.Actor,64);w.Write(f.Id);w.Write((byte)count);
                for(int i=0;i<count;i++){var c=contactsThisStep[i];QueueContact(c);w.Write((byte)(c.Pellet|(c.MapSurface?8:0)));w.Write(c.RangeTarget);w.Write(c.Point.X);w.Write(c.Point.Y);w.Write(c.Point.Z);w.Write(c.Normal.X);w.Write(c.Normal.Y);w.Write(c.Normal.Z);w.Write(c.Damage);}
                var frame=Packet(PacketKind.Snapshot,ScoreFeature,OwnActor,stream.ToArray(),checked(++remoteVisualSequence));foreach(var peer in peers)if(RemotePeerReady(peer.Value))SendFlight(peer.Key,frame);
            }
        }
        bool ReadBulletContacts(Packet packet)
        {
            if(packet.Payload==null||packet.Payload.Length==0||packet.Payload[0]!=ContactLayout)return false;
            if(packet.BaseSequence!=-1||packet.Sequence<=contactReceived)return true;
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){
                r.ReadByte();string actor=Read(r,64);long id=r.ReadInt64();int n=r.ReadByte();if(!HasMember(actor)||id<=0||n>8||s.Length-s.Position!=30*n)return true;
                if(n==0){if(actor==OwnActor){contactReceived=packet.Sequence;QueueContact(new R26BulletContact{Actor=actor,Id=id,Pellet=255});}return true;}
                int mask=0;for(int i=0;i<n;i++){
                    int flags=r.ReadByte();if(flags>15)return true;var c=new R26BulletContact{Actor=actor,Id=id,Pellet=(byte)(flags&7),MapSurface=(flags&8)!=0,RangeTarget=r.ReadByte(),Point=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),Normal=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),Damage=r.ReadSingle()};
                    if(c.RangeTarget!=0&&!c.MapSurface||(mask&(1<<c.Pellet))!=0||!c.Point.IsFinite||!c.Normal.IsFinite||!Finite(c.Damage)||c.Damage<0)return true;
                    mask|=1<<c.Pellet;contactsThisStep[i]=c;
                }
                contactReceived=packet.Sequence;for(int i=0;i<n;i++)QueueContact(contactsThisStep[i]);
            }}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}return true;
        }
    }
}
