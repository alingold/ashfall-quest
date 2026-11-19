using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.Playable.Traversal;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed class R25PullState
    {
        public readonly string Victim,Shooter;
        public readonly Float3 Target;
        public readonly double Start;
        public readonly long Revision;
        public readonly int Hand;
        public readonly bool Active;
        internal R25PullState(string victim,string shooter,Float3 target,double start,long revision,int hand,bool active)
        {Victim=victim;Shooter=shooter;Target=target;Start=start;Revision=revision;Hand=hand;Active=active;}
    }
    public sealed partial class R24LanLobby
    {
        const string PullFeature="playable.grapple.pull/1";
        readonly Dictionary<string,R25PullState> pulls=new Dictionary<string,R25PullState>(StringComparer.Ordinal);
        readonly Dictionary<string,double> pullCooldown=new Dictionary<string,double>(StringComparer.Ordinal),pullImmunity=new Dictionary<string,double>(StringComparer.Ordinal),pullAllowance=new Dictionary<string,double>(StringComparer.Ordinal);
        readonly Dictionary<string,long> pullRevisions=new Dictionary<string,long>(StringComparer.Ordinal),grappleShots=new Dictionary<string,long>(StringComparer.Ordinal);
        readonly Dictionary<string,Dictionary<string,byte[]>> pullPending=new Dictionary<string,Dictionary<string,byte[]>>(StringComparer.Ordinal);
        readonly Dictionary<string,float> pullVitals=new Dictionary<string,float>(StringComparer.Ordinal);
        readonly Dictionary<string,R24LanRawTrackingHistory> roomPullTracking=new Dictionary<string,R24LanRawTrackingHistory>(StringComparer.Ordinal);
        Float3 roomPullOrigin;bool roomPullBound,roomPullEnabled;
        long pullSequence;
        // The room toy has no inventory or damage authority. Its raw samples use revision zero.
        public bool RoomGrapple {get{return State!=R24LanLobbyState.Offline&&(SelectedMap==null||SelectedMap.Home);}}
        public void EnableRoomGrappleTracking(){Check();roomPullEnabled=RoomGrapple;}
        public void BindRoomGrappleOrigin(Float3 origin){Check();if(State!=R24LanLobbyState.Hosting||!origin.IsFinite)throw new ArgumentException("Host room origin required");roomPullOrigin=origin;roomPullBound=roomPullEnabled=true;}
        bool StoreRoomPullTracking(string actor,long sequence,R24LanRawTracking pose)
        {
            if(!RoomGrapple||!roomPullBound||!HasMember(actor)||pose.MapHash!=MapManifestHash||pose.SpawnRevision!=0||!AdmitRawArrival(actor))return false;
            R24LanRawTrackingHistory history;if(!roomPullTracking.TryGetValue(actor,out history)){if(!Anchored(roomPullOrigin,pose.Root.Position))return false;history=new R24LanRawTrackingHistory();roomPullTracking[actor]=history;}
            float slack;float multiplier=PullMotionMultiplier(actor,1,out slack);
            bool accepted=history.Accept(sequence,clock.Now,pose,true,multiplier,rawWorldClear,slack);
            if(!accepted){RejectedRawTracking++;LastRawTrackingRejection="room-"+history.Rejection;}return accepted;
        }
        bool RoomPullFresh(string actor,out R24LanRawTrackingSample sample)
        {sample=null;R24LanRawTrackingHistory history;return roomPullTracking.TryGetValue(actor,out history)&&history.Latest!=null&&clock.Now-history.Latest.Time<=.25&&history.TryNearest(clock.Now,out sample);}
        bool PullPose(string actor,out R24LanRawTrackingSample sample){return RoomGrapple?RoomPullFresh(actor,out sample):Fresh(actor,clock.Now,out sample);}
        float PullVital(string actor){R24LanHealthState h;return healthStates.TryGetValue(actor,out h)?h.Health+h.Shield:0;}
        bool PullDamaged(string actor){float prior,value=PullVital(actor);bool lost=pullVitals.TryGetValue(actor,out prior)&&value<prior-.001f;pullVitals[actor]=value;return lost;}
        bool PullShooter(string actor){foreach(var row in pulls.Values)if(row.Shooter==actor)return true;return false;}
        void ReleaseShooterPull(string actor){foreach(var row in new List<R25PullState>(pulls.Values))if(row.Shooter==actor)EndPull(row.Victim);}

        public R25PullState[] GrapplePulls {get{Check();return CopySnapshot(pulls.Values);}}
        bool PullRules {get{return GameplayRules!=null&&GameplayRules.GrappleGun&&GameplayRules.GrapplePull;}}
        bool PullReady(string actor,out R24LanRawTrackingSample raw)
        {
            raw=null;var member=sessions==null?null:sessions.Get(actor);R24LanHealthState health;
            if(RoomGrapple)return member!=null&&member.Life!=SessionLife.Eliminated&&member.Life!=SessionLife.Spectator&&RoomPullFresh(actor,out raw);
            if(member==null||member.Life!=SessionLife.Alive||!RoomGrapple&&(!member.Loaded||!member.HasPosition)||PodCarried(actor)||PlatformProtected(actor)||
                !healthStates.TryGetValue(actor,out health)||!health.Connected||health.Life!=LifeState.Alive||!PullPose(actor,out raw)||raw.Pose.Paddles)return false;
            foreach(var bike in vehicles.Values)if(bike.Rider==actor)return false;
            return true;
        }
        // Existing rope command's optional byte selects only the authenticated firing hand.
        // All hit geometry, custody and timers are reconstructed by the host.
        bool TryStartPull(string actor,int hand)
        {
            bool corrected=hand==4||hand==5;if(corrected)hand-=4;
            R24LanRawTrackingSample self;EquippedWeapon e;CombatActor owner;CombatWeapon weapon;double until;
            if(RoomGrapple||!PullRules||ballisticTrace==null||hand<0||hand>1||!PullReady(actor,out self)||ControlsBlocked(actor)||
                (!equipment.TryGetValue(actor,out e)||e.Hand!=hand||e.Suppressed||clock.Now-e.At>.5||e.Id==null||
                !combat.TryGetValue(actor,out owner)||!owner.Weapons.TryGetValue(e.Id,out weapon)||weapon.Profile!="grapple")||
                (self.Pose.Flags&(hand==0?2:4))==0||pulls.ContainsKey(actor)||pullCooldown.TryGetValue(actor,out until)&&clock.Now<until)return false;
            foreach(var active in pulls.Values)if(active.Shooter==actor)return false;
            var firing=hand==0?self.Pose.Left:self.Pose.Right;
            var direction=Rotate(firing.Rotation,corrected?new Float3(0,-1,0):new Float3(0,0,1));var origin=firing.Position;
            if(rawWorldClear==null||!rawWorldClear(self.Pose.Head.Position,origin))return false;
            float nearest=ballisticTrace(origin,direction,25);
            if(Single.IsNaN(nearest)||nearest<0||nearest>25)return false;
            float panelDistance; if(NearestSharedPanel(origin,direction,nearest,out panelDistance)!=null)nearest=panelDistance;
            string victim=null;var shooterMember=sessions.Get(actor);
            foreach(var other in sessions.CaptureMembers())
            {
                R24LanRawTrackingSample raw;
                if(other.ActorId==actor||!PullReady(other.ActorId,out raw)||pulls.ContainsKey(other.ActorId)||PullShooter(other.ActorId)||
                    pullImmunity.TryGetValue(other.ActorId,out until)&&clock.Now<until||
                    !RoomGrapple&&GameplayRules.Teams>0&&other.Team==shooterMember.Team&&!GameplayRules.GrapplePullFriends)continue;
                var head=raw.Pose.Head.Position;
                float distance=R26RemoteAimVolumes.Nearest(origin.X,origin.Y,origin.Z,direction.X,direction.Y,direction.Z,head.X,head.Y,head.Z);
                if(distance<nearest){nearest=distance;victim=other.ActorId;}
            }
            if(victim==null)return false;
            // Center the stand-off on the accepted shooter chest, on whichever side the shot landed.
            var target=R25GrapplePull.Chest(self.Pose.Head.Position);
            var state=new R25PullState(victim,actor,target,clock.Now,++pullSequence,hand,true);
            pulls[victim]=state;pullCooldown[actor]=clock.Now+6;pullAllowance[victim]=clock.Now+2;
            pullVitals[actor]=PullVital(actor);pullVitals[victim]=PullVital(victim);
            RemoveWeb(actor);RemoveWeb(victim);QueuePull(state);return true;
        }
        byte[] PullFrame(R25PullState row)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8))
            {Write(w,row.Victim,64);Write(w,row.Shooter,64);w.Write(row.Active);w.Write((byte)row.Hand);w.Write(row.Start);w.Write(row.Target.X);w.Write(row.Target.Y);w.Write(row.Target.Z);return Packet(PacketKind.Snapshot,PullFeature,OwnActor,s.ToArray(),row.Revision);}
        }
        void PendingPull(string peer,string victim,byte[] frame)
        {Dictionary<string,byte[]> rows;if(!pullPending.TryGetValue(peer,out rows)){rows=new Dictionary<string,byte[]>(StringComparer.Ordinal);pullPending[peer]=rows;}foreach(var id in new List<string>(rows.Keys))if(!HasMember(id))rows.Remove(id);if(rows.ContainsKey(victim)||rows.Count<Capacity)rows[victim]=frame;}
        void QueuePull(R25PullState row){byte[] frame=PullFrame(row);foreach(var peer in peers)if(peer.Value.Admitted)PendingPull(peer.Key,row.Victim,frame);}
        void ReplayPulls(string peer){foreach(var row in pulls.Values)if(clock.Now<row.Start+R25GrapplePull.Duration)PendingPull(peer,row.Victim,PullFrame(row));}
        void EndPull(string victim)
        {
            R25PullState row;if(!pulls.TryGetValue(victim,out row))return;
            pulls.Remove(victim);pullImmunity[victim]=clock.Now+3;pullAllowance[victim]=clock.Now+.5;
            QueuePull(new R25PullState(victim,row.Shooter,row.Target,row.Start,++pullSequence,row.Hand,false));
        }
        void ReadPull(Packet packet)
        {
            if(packet.Sequence<1||packet.BaseSequence!=-1)return;
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){
                string victim=Read(r,64),shooter=Read(r,64);bool active=r.ReadBoolean();int hand=r.ReadByte();double start=r.ReadDouble();var target=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());long prior;
                if(s.Position!=s.Length||hand>1||!target.IsFinite||Double.IsNaN(start)||Double.IsInfinity(start)||start<0||pullRevisions.TryGetValue(victim,out prior)&&packet.Sequence<=prior)return;
                if(!HasMember(victim)||!HasMember(shooter))return;
                foreach(var id in new List<string>(pullRevisions.Keys))if(!HasMember(id))pullRevisions.Remove(id);
                if(!pullRevisions.ContainsKey(victim)&&pullRevisions.Count>=Capacity)return;
                pullRevisions[victim]=packet.Sequence;if(active&&pulls.Count<Capacity)pulls[victim]=new R25PullState(victim,shooter,target,start,packet.Sequence,hand,true);else pulls.Remove(victim);
            }}catch(IOException){}catch(ArgumentException){}
        }
        void TickPulls()
        {
            if(State==R24LanLobbyState.Connected){
                foreach(var row in new List<R25PullState>(pulls.Values))if(!HasMember(row.Victim)||!HasMember(row.Shooter)||EstimatedHostTime>=row.Start+R25GrapplePull.Duration)pulls.Remove(row.Victim);
                foreach(var id in new List<string>(pullRevisions.Keys))if(!HasMember(id))pullRevisions.Remove(id);return;
            }
            if(State!=R24LanLobbyState.Hosting)return;
            foreach(var row in new List<R25PullState>(pulls.Values)){
                R24LanRawTrackingSample victim,shooter;
                if(!PullRules||clock.Now>=row.Start+R25GrapplePull.Duration||!PullReady(row.Victim,out victim)||!PullReady(row.Shooter,out shooter)||PullDamaged(row.Victim)||PullDamaged(row.Shooter))EndPull(row.Victim);
                else if(Distance2(victim.Pose.Head.Position,shooter.Pose.Head.Position)>900||rawWorldClear==null||!rawWorldClear((row.Hand==0?shooter.Pose.Left:shooter.Pose.Right).Position,R25GrapplePull.Chest(victim.Pose.Head.Position)))EndPull(row.Victim);
                else if(R25GrapplePull.Reached(R25GrapplePull.Chest(victim.Pose.Head.Position),row.Target))EndPull(row.Victim);
            }
            foreach(var peer in new List<string>(pullPending.Keys)){
                if(!peers.ContainsKey(peer)){pullPending.Remove(peer);continue;}
                var rows=pullPending[peer];foreach(var id in new List<string>(rows.Keys))if(Send(peer,rows[id]))rows.Remove(id);if(rows.Count==0)pullPending.Remove(peer);
            }
            foreach(var actor in new List<string>(roomPullTracking.Keys))if(!HasMember(actor))roomPullTracking.Remove(actor);
            foreach(var actor in new List<string>(pullVitals.Keys))if(!HasMember(actor))pullVitals.Remove(actor);
            foreach(var actor in new List<string>(pullCooldown.Keys))if(!HasMember(actor))pullCooldown.Remove(actor);
            foreach(var actor in new List<string>(pullImmunity.Keys))if(!HasMember(actor))pullImmunity.Remove(actor);
            foreach(var actor in new List<string>(pullAllowance.Keys))if(!HasMember(actor)||clock.Now>pullAllowance[actor])pullAllowance.Remove(actor);
            foreach(var actor in new List<string>(grappleShots.Keys))if(!HasMember(actor))grappleShots.Remove(actor);
        }
        float PullMotionMultiplier(string actor,float ordinary,out float slack)
        {
            double until;bool allowed=pullAllowance.TryGetValue(actor,out until)&&clock.Now<=until;
            slack=allowed?R25GrapplePull.RawTrackingSlack:0;
            return allowed?R25GrapplePull.Allowance(ordinary,clock.Now,until):ordinary;
        }
        void ClearPulls(){roomPullTracking.Clear();pullVitals.Clear();pulls.Clear();pullPending.Clear();pullCooldown.Clear();pullImmunity.Clear();pullAllowance.Clear();pullRevisions.Clear();grappleShots.Clear();}
    }
}
