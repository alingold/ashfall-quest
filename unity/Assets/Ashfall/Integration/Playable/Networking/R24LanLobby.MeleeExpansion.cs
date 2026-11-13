using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Expansion;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed class R25MeleeHit
    {
        public string Attacker,Target,Kind;
        public float Damage,Stagger,TargetStagger;public int TargetHand;
        public Float3 Point;
        public int Hand;
    }
    public sealed partial class R24LanLobby
    {
        const string MeleeHitFeature="playable.melee.hit/1";
        sealed class BladeMotion
        {
            internal string Id;internal long Revision;internal Float3 A,B,Velocity,Root; internal double At;internal readonly R25ParryWindow Parry=new R25ParryWindow();
            internal Float3[] Hull;internal int[] Triangles;
        }
        sealed class FistMotion { internal readonly R25FistContact Contact=new R25FistContact();internal long Revision; internal Float3 Previous;internal bool Seeded,BladeInside; }
        sealed class MeleeLoss {internal string Actor,Target,Kind;internal float Damage;internal Float3 Point;internal int Hand;internal double At;internal long Revision,TargetRevision;}
        readonly Dictionary<string,BladeMotion> bladeMotion=new Dictionary<string,BladeMotion>(StringComparer.Ordinal);
        readonly Dictionary<string,FistMotion> fists=new Dictionary<string,FistMotion>(StringComparer.Ordinal);
        readonly List<MeleeLoss> meleeLosses=new List<MeleeLoss>();
        readonly Queue<R25MeleeHit> meleeHits=new Queue<R25MeleeHit>();
        readonly Dictionary<string,Queue<byte[]>> meleePending=new Dictionary<string,Queue<byte[]>>(StringComparer.Ordinal);
        long meleeSequence,meleeReceived;
        public bool TryDequeueMeleeHit(out R25MeleeHit hit){Check();hit=meleeHits.Count==0?null:meleeHits.Dequeue();return hit!=null;}
        void LocalMeleeHit(R25MeleeHit hit){if(meleeHits.Count>=64)meleeHits.Dequeue();meleeHits.Enqueue(hit);}
        bool MeleeBlocked(string actor){BladeMotion b;return bladeMotion.TryGetValue(actor,out b)&&b.Parry.Blocked(clock.Now);}
        bool QueueMeleeDamage(string actor,string target,float damage,Float3 point,int hand,string kind)
        {
            R24LanSpawn spawn;
            R24LanSpawn targetSpawn;if(!TryReadSpawn(target,out targetSpawn))return false;
            if(!CombatLive||MeleeBlocked(actor)||meleeLosses.Count>=128||!TryReadSpawn(actor,out spawn))return false;
            R24LanRawTrackingSample victim;if(Fresh(target,clock.Now,out victim)){var chest=R24KnifeMath.Add(victim.Pose.Head.Position,new Float3(0,-.45f,0));point=R24KnifeMath.Add(chest,R24KnifeMath.Scale(R24KnifeMath.Unit(R24KnifeMath.Sub(point,chest)),.29f));}
            meleeLosses.Add(new MeleeLoss{Actor=actor,Target=target,Damage=damage,Point=point,Hand=hand,Kind=kind,At=clock.Now,Revision=spawn.Revision,TargetRevision=targetSpawn.Revision});return true;
        }
        void EmitMelee(string actor,string target,string kind,float damage,Float3 point,int hand,float stagger,int targetHand=0,float targetStagger=0)
        {
            var hit=new R25MeleeHit{Attacker=actor,Target=target,Kind=kind,Damage=damage,Point=point,Hand=hand,Stagger=stagger,TargetHand=targetHand,TargetStagger=targetStagger};
            if(OwnActor==actor||OwnActor==target)LocalMeleeHit(hit);
            byte[] frame;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){Write(w,actor,64);Write(w,target,64);Write(w,kind,8);w.Write(damage);w.Write(point.X);w.Write(point.Y);w.Write(point.Z);w.Write((byte)hand);w.Write(stagger);w.Write((byte)targetHand);w.Write(targetStagger);frame=Packet(PacketKind.Snapshot,MeleeHitFeature,OwnActor,s.ToArray(),++meleeSequence);}
            foreach(var peer in peers)if(peer.Value.Admitted&&(peer.Value.Actor==actor||peer.Value.Actor==target)){
                Queue<byte[]> q;if(!meleePending.TryGetValue(peer.Key,out q)){q=new Queue<byte[]>();meleePending[peer.Key]=q;}
                if(q.Count>=64)q.Dequeue();q.Enqueue(frame);
            }
        }
        void ReadMeleeHit(Packet packet)
        {
            if(packet.BaseSequence!=-1||packet.Sequence<=meleeReceived)return;
            try{using(var s=new MemoryStream(packet.Payload))using(var r=new BinaryReader(s,Utf8)){
                var h=new R25MeleeHit{Attacker=Read(r,64),Target=Read(r,64),Kind=Read(r,8),Damage=r.ReadSingle(),Point=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle()),Hand=r.ReadByte(),Stagger=r.ReadSingle(),TargetHand=r.ReadByte(),TargetStagger=r.ReadSingle()};
                if(s.Position!=s.Length||h.TargetHand>1||float.IsNaN(h.TargetStagger)||h.TargetStagger<0||h.TargetStagger>.9f||h.Hand>1||!h.Point.IsFinite||float.IsNaN(h.Damage)||h.Damage<0||h.Damage>10000||float.IsNaN(h.Stagger)||h.Stagger<0||h.Stagger>.9f||(h.Kind!="blade"&&h.Kind!="fist"&&h.Kind!="parry")||(h.Attacker!=OwnActor&&h.Target!=OwnActor))return;
                meleeReceived=packet.Sequence;LocalMeleeHit(h);
            }}catch(IOException){}catch(ArgumentException){}
        }
        void TickMeleeExpansion()
        {
            if(State!=R24LanLobbyState.Hosting)return;
            // Resolve after a short sampling window, so either blade's next accepted sample can cancel both contacts.
            for(int i=meleeLosses.Count-1;i>=0;i--){var hit=meleeLosses[i];if(clock.Now-hit.At<.06)continue;meleeLosses.RemoveAt(i);R24LanSpawn spawn;R24LanRawTrackingSample raw;
                R24LanSpawn targetSpawn;var attacker=sessions.Get(hit.Actor);var ah=attacker==null?null:hostHealth.Read(attacker.CanonicalActorId);var victim=sessions.Get(hit.Target);var before=victim==null?null:hostHealth.Read(victim.CanonicalActorId);
                if(CombatLive&&ah!=null&&ah.Life==LifeState.Alive&&before!=null&&!MeleeBlocked(hit.Actor)&&TryReadSpawn(hit.Actor,out spawn)&&spawn.Revision==hit.Revision&&TryReadSpawn(hit.Target,out targetSpawn)&&targetSpawn.Revision==hit.TargetRevision&&Fresh(hit.Actor,clock.Now,out raw)&&ApplyCombatDamage(hit.Actor,hit.Target,hit.Damage,false)){var after=hostHealth.Read(victim.CanonicalActorId);float actual=(before.Health+before.Shield)-(after.Health+after.Shield);EmitMelee(hit.Actor,hit.Target,hit.Kind,actual,hit.Point,hit.Hand,0);}
            }
            foreach(string peer in new List<string>(meleePending.Keys)){if(!peers.ContainsKey(peer)){meleePending.Remove(peer);continue;}var q=meleePending[peer];int budget=8;while(q.Count>0&&budget-->0){if(!Send(peer,q.Peek()))break;q.Dequeue();}if(q.Count==0)meleePending.Remove(peer);}
            foreach(string actor in new List<string>(bladeMotion.Keys))if(!HasMember(actor))bladeMotion.Remove(actor);
            foreach(string key in new List<string>(fists.Keys))if(!HasMember(key.Substring(0,key.Length-2)))fists.Remove(key);
        }
        void ClearMeleeExpansion(){fists.Clear();bladeMotion.Clear();meleeLosses.Clear();meleeHits.Clear();meleePending.Clear();}
        bool ActiveBlade(string actor,out EquippedWeapon e)
        {
            R24LanRawTrackingSample raw;var member=sessions.Get(actor);var h=member==null?null:hostHealth.Read(member.CanonicalActorId);
            return equipment.TryGetValue(actor,out e)&&e.Blade!=null&&e.Blade.HistoryValid&&!e.Suppressed&&!e.Blocked&&clock.Now-e.At<=.15&&h!=null&&h.Life==LifeState.Alive&&Fresh(actor,clock.Now,out raw);
        }
        void SampleBladeClash(string actor,EquippedWeapon e,Float3 a,Float3 b,R24LanRawTrackingSample raw)
        {
            BladeMotion m;bool prior=bladeMotion.TryGetValue(actor,out m)&&m.Id==e.Id&&m.Revision==raw.Pose.SpawnRevision;if(!prior){double until=m!=null&&m.Revision==raw.Pose.SpawnRevision?m.Parry.Until:0;m=new BladeMotion{Id=e.Id,Revision=raw.Pose.SpawnRevision};m.Parry.InheritUntil(until);bladeMotion[actor]=m;}
            float dt=(float)(raw.Time-m.At);var center=R24KnifeMath.Scale(R24KnifeMath.Add(a,b),.5f);
            m.Velocity=prior&&dt>0&&dt<=.15f?R24KnifeMath.Scale(R24KnifeMath.Sub(R24KnifeMath.Sub(center,R24KnifeMath.Scale(R24KnifeMath.Add(m.A,m.B),.5f)),R24KnifeMath.Sub(raw.Pose.Root.Position,m.Root)),1/dt):new Float3();
            m.Parry.Observe(clock.Now,m.Velocity);
            Float3[] hull;int[] triangles;
            if(!R24KnifeHull.TryBuild(R24KnifeMath.HullPoints(prior&&dt<=.15f?m.A:a,prior&&dt<=.15f?m.B:b,a,b),out hull,out triangles))return;
            m.A=a;m.B=b;m.Root=raw.Pose.Root.Position;m.At=raw.Time;m.Hull=hull;m.Triangles=triangles;
            if(!GameplayRules.Parry||MeleeBlocked(actor)||R24KnifeMath.Length(m.Velocity)<.2f)return;
            foreach(var pair in bladeMotion){EquippedWeapon other;var n=pair.Value;
                if(pair.Key==actor||clock.Now-n.At>.15||MeleeBlocked(pair.Key)||!ActiveBlade(pair.Key,out other)||n.Hull==null||R24KnifeMath.Length(n.Velocity)<.2f||R24KnifeMath.Length(R24KnifeMath.Sub(m.Velocity,n.Velocity))<1||!MeleeVisible(a,n.A,null)||!R24LanMeleeGeometry.Hulls(hull,triangles,n.Hull,n.Triangles))continue;
                bool timedOther=n.Parry.Timed(clock.Now,R24KnifeMath.Unit(R24KnifeMath.Sub(center,R24KnifeMath.Scale(R24KnifeMath.Add(n.A,n.B),.5f))));
                bool timedSelf=m.Parry.Timed(clock.Now,R24KnifeMath.Unit(R24KnifeMath.Sub(R24KnifeMath.Scale(R24KnifeMath.Add(n.A,n.B),.5f),center)));
                m.Parry.Clash(clock.Now,timedOther);n.Parry.Clash(clock.Now,timedSelf);
                EmitMelee(actor,pair.Key,"parry",0,center,e.Hand,timedOther?.9f:0,other.Hand,timedSelf?.9f:0);break;
            }
        }
        void SampleFists(string actor,R24LanRawTrackingSample sample)
        {
            if(!InventoryLive)return;var member=sessions.Get(actor);var h=member==null?null:hostHealth.Read(member.CanonicalActorId);
            for(int hand=0;hand<2;hand++){
                string key=actor+"/"+hand;FistMotion m;if(!fists.TryGetValue(key,out m)||m.Revision!=sample.Pose.SpawnRevision){m=new FistMotion{Revision=sample.Pose.SpawnRevision};fists[key]=m;}
                var at=(hand==0?sample.Pose.Left:sample.Pose.Right).Position;
                bool usable=GameplayRules.Fists&&h!=null&&h.Life==LifeState.Alive&&!PodCarried(actor)&&!ControlsBlocked(actor)&&!sample.Pose.Paddles&&(sample.Pose.Flags&(hand==0?2:4))!=0&&(hand==0?sample.Pose.LeftGrip:sample.Pose.RightGrip)>.7f&&!HandOccupied(actor,hand)&&!GrenadeHandOccupied(actor,hand);
                EquippedWeapon declared;CombatActor custody;if(equipment.TryGetValue(actor,out declared)&&declared.Id!=null&&clock.Now-declared.At<=.5&&combat.TryGetValue(actor,out custody)&&custody.Weapons.ContainsKey(declared.Id)&&(declared.Hand==hand||custody.Weapons[declared.Id].Hands!=null))usable=false;
                FoodPreparation food;if(foodPreparations.TryGetValue(actor,out food)&&food.Hand==hand)usable=false;
                usable=usable&&rawWorldClear!=null&&rawWorldClear(sample.Pose.Head.Position,at)&&MapBladeClear(sample.Pose.Head.Position,at);
                var model=Basis(hand==0?sample.Pose.Left:sample.Pose.Right);
                var before=m.Seeded?m.Previous:at;var targets=MeleeOverlaps(actor,model,before,before,at,at,.045f);m.Previous=at;m.Seeded=usable;
                var hits=m.Contact.Sample(sample.Time,R24KnifeMath.Local(Basis(sample.Pose.Root),at),usable,targets);
                bool blocked=false;
                if(usable&&GameplayRules.Parry&&m.Contact.Speed>1.6f)foreach(var pair in bladeMotion){EquippedWeapon blade;var n=pair.Value;if(pair.Key!=actor&&clock.Now-n.At<=.15&&ActiveBlade(pair.Key,out blade)&&n.Hull!=null&&R24LanMeleeGeometry.Sphere(n.Hull,n.Triangles,at,.075f)&&MeleeVisible(at,n.A,null)){blocked=true;if(!m.BladeInside)EmitMelee(actor,pair.Key,"parry",0,at,hand,0,blade.Hand);meleeLosses.RemoveAll(delegate(MeleeLoss loss){return loss.Actor==actor&&loss.Hand==hand&&loss.Kind=="fist";});break;}}
                m.BladeInside=blocked;
                if(!blocked)foreach(string target in hits)if(target.StartsWith("actor/",StringComparison.Ordinal))QueueMeleeDamage(actor,target.Substring(6),R25FistContact.Damage(m.Contact.Speed),at,hand,"fist");
            }
        }
    }
}
