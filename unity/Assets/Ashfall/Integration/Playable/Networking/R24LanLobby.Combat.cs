using System;using System.Collections.Generic;using System.IO;using System.Text;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.Playable.Networking
{
    // Host-authoritative LAN combat (net_session1000._shoot/_reload/hurt/_die_r13,
    // net_bridge1000._hit_rays/_validate revive). A client submits only rays, a
    // stamp, a reload completion or a revive target; the host owns custody,
    // ammunition, victims, damage, kills, scores and every Health mutation.
    public sealed partial class R24LanLobby
    {
        const string ShotFeature="playable.combat.shot/1",ReloadFeature="playable.combat.reload/1",ReviveFeature="playable.combat.revive/1",ScoreFeature="playable.score/1";
        sealed class CombatWeapon{internal string Profile,Family;internal int Stars,Capacity,Ammo,RocketReloads;internal int[] Hands;internal readonly double[] HandShots={Double.NegativeInfinity,Double.NegativeInfinity},HandReloads={Double.NegativeInfinity,Double.NegativeInfinity};internal double LastShot=Double.NegativeInfinity,LastReload=Double.NegativeInfinity;}
        sealed class ShotClock{internal double Last=Double.NegativeInfinity,Credit=Double.NegativeInfinity;internal readonly List<double> Arrivals=new List<double>();}
        sealed class CombatActor{internal readonly Dictionary<string,CombatWeapon> Weapons=new Dictionary<string,CombatWeapon>(StringComparer.Ordinal);internal readonly Dictionary<string,int> Reserves=R24LanLoadout.StartingReserves();internal readonly Dictionary<string,ShotClock> Clocks=new Dictionary<string,ShotClock>(StringComparer.Ordinal);internal int Kills,Score,Capacity=3,Builds=999;internal long LastBuildPose=-1;}
        readonly Dictionary<string,CombatActor> combat=new Dictionary<string,CombatActor>(StringComparer.Ordinal);
        readonly Dictionary<string,R24LanScoreRow> scores=new Dictionary<string,R24LanScoreRow>(StringComparer.Ordinal);
        // MP-HITMARKER: host-confirmed hits for their shooter. The host's own hits stay local; a remote
        // shooter gets one reliable playable.score/1 frame per hit (payload layout 2, flushed every tick).
        const byte HitConfirmLayout=2;readonly Queue<R26HitConfirm> hitConfirms=new Queue<R26HitConfirm>();readonly Dictionary<string,Queue<byte[]>> hitPending=new Dictionary<string,Queue<byte[]>>(StringComparer.Ordinal);long hitSequence,hitReceived;
        public int HitConfirmsQueued{get;private set;}public int HitConfirmsSent{get;private set;}public int HitConfirmsReceived{get;private set;}
        public bool TryDequeueHitConfirm(out R26HitConfirm hit){Check();hit=hitConfirms.Count==0?null:hitConfirms.Dequeue();return hit!=null;}
        readonly Queue<byte[]> pendingGameCommands=new Queue<byte[]>();long gameCommandSequence,ammoPredictionSequence,resourcesPredictionSequence,scoreSequence,scoreRevision=-1;bool scoreDirty;
        Func<Float3,Float3,float,float> ballisticTrace;
        public int AcceptedShots{get;private set;}public int RejectedShots{get;private set;}public string LastShotRejection{get;private set;}
        public int AcceptedReloads{get;private set;}public int Revives{get;private set;}public string LastReviveRejection{get;private set;}public int CombatDamageEvents{get;private set;}
        // Host composition only: distance to the nearest host map/build blocker
        // along a unit ray (range when clear). Never supplied by a client.
        public void BindCombatWorld(Func<Float3,Float3,float,float> trace)
        {Check();if(State!=R24LanLobbyState.Hosting||trace==null||ballisticTrace!=null)throw new InvalidOperationException("One host ballistic world required");ballisticTrace=trace;}
        Ashfall.Integration.Playable.Match.R24PlayableMatchRules GameplayRules{get{return roundRules??warmupRules;}}
        bool InventoryLive{get{return State==R24LanLobbyState.Hosting&&hostHealth!=null&&GameplayRules!=null&&SelectedMap!=null&&SelectedMap.Active&&!SelectedMap.Cancelled&&!SelectedMap.Results&&RoundCountdown<=0;}}
        bool CombatLive{get{return State==R24LanLobbyState.Hosting&&hostHealth!=null&&roundRules!=null&&SelectedMap!=null&&SelectedMap.Active&&!SelectedMap.Cancelled&&!SelectedMap.Results&&SelectedMap.Rules!=null&&sessions.Stage==SessionStage.Match&&RoundCountdown<=0;}}
        void ClearCombat(){ClearBulletFlights();ClearDeathLoot();ClearDamageDirection();ClearRemotePresentation();ClearVehicles();ClearCatalog();ClearExplosives();ClearEquipment();ClearConstruction();ownResources=null;ClearAmmo();ClearFood();ClearSharedSupplies();ClearLoot();combat.Clear();scores.Clear();hitConfirms.Clear();hitPending.Clear();scoreDirty=false;scoreRevision=-1;incomingScores=null;pendingGameCommands.Clear();gameCommandSequence=ammoPredictionSequence=resourcesPredictionSequence=0;foreach(var p in peers.Values){p.ScoreFrames=null;p.NextScore=0;p.LastGameCommand=0;}}
        void EnsureCombatActor(string actor)
        {
            if(State!=R24LanLobbyState.Hosting||GameplayRules==null||combat.ContainsKey(actor))return;var value=new CombatActor{Builds=GameplayRules.StartingBuilds};
            foreach(int catalog in GameplayRules.StartingCatalog){string profile=R24LanLoadout.Profile(catalog);if(profile==null)continue;int capacity=R24LanLoadout.Capacity(profile,1);
                var weapon=new CombatWeapon{Profile=profile,Family=R24LanLoadout.Family(profile),Stars=1,Capacity=capacity,Ammo=capacity,RocketReloads=profile=="smaw"?2:0};
                if(R24LanLoadout.Paired(profile))weapon.Hands=new[]{(capacity+1)/2,capacity/2};value.Weapons[profile]=weapon;}
            combat.Add(actor,value);scoreDirty=true;
        }
        static bool Allied(MemberSnapshot a,MemberSnapshot b,int teams){return a!=null&&b!=null&&(a.ActorId==b.ActorId||teams>0&&a.Team==b.Team);}
        bool Fresh(string actor,double at,out R24LanRawTrackingSample sample){if(PlatformProtected(actor)){sample=null;return false;}if(roundRules==null)return TryReadInventoryTracking(actor,at,out sample);return TryReadRawTracking(actor,at,out sample);}
        bool RejectShot(string why){RejectedShots++;LastShotRejection=why;return false;}
        // ---------- shots ----------
        public bool SubmitShot(string profile,int side,Float3 origin,Float3[] directions,string instance=null)
        {
            Check();if(State!=R24LanLobbyState.Hosting&&State!=R24LanLobbyState.Connected)return false;
            var shot=new R24LanShot(profile,side,EstimatedHostTime,origin,directions,instance);if(!shot.Valid)return false;
            if(State==R24LanLobbyState.Hosting){checked{gameCommandSequence++;}MarkGamePrediction(ShotFeature,gameCommandSequence);return ApplyShot(OwnActor,shot);}
            return QueueGameCommand(ShotFeature,shot.Encode());
        }
        partial void HitregTarget(string actor,bool head,float distance);partial void BeforeHitregShot(string actor,R24LanShot shot);partial void AfterHitregShot(string actor,R24LanShot shot,bool accepted);
        internal bool ApplyShot(string actor,R24LanShot shot){BeforeHitregShot(actor,shot);bool accepted=ApplyShotCore(actor,shot);AfterHitregShot(actor,shot,accepted);return accepted;}
        bool ApplyShotCore(string actor,R24LanShot shot)
        {
            if(!InventoryLive)return RejectShot("match-required");if(ballisticTrace==null)return RejectShot("host-world-required");
            var member=sessions.Get(actor);var health=member==null?null:hostHealth.Read(member.CanonicalActorId);
            if(member==null||!member.Loaded||!member.HasPosition||member.Life!=SessionLife.Alive||health==null||!health.Connected||health.Life!=LifeState.Alive)return RejectShot("not-alive-member");
            CombatActor owner;CombatWeapon weapon;R24LanBallisticProfile profile;
            if(shot==null||!shot.Valid||!combat.TryGetValue(actor,out owner)||!owner.Weapons.TryGetValue(shot.Instance,out weapon)||weapon.Profile!=shot.Profile||!R24LanBallisticProfile.TryRead(shot.Profile,out profile))return RejectShot("custody");
            bool paired=weapon.Hands!=null;int side=paired?shot.Side:0;
            if(paired?weapon.Hands[side]<=0:weapon.Ammo<=0)return RejectShot("ammo");
            double now=clock.Now;if(shot.Time>now+.05||shot.Time<now-.75)return RejectShot("stamp");
            // R18-NET cadence on the shooter timeline plus host arrival budgets.
            string key=shot.Profile+(paired?":"+side:"");ShotClock timing;if(!owner.Clocks.TryGetValue(key,out timing)){timing=new ShotClock();owner.Clocks.Add(key,timing);}
            double interval=1.0/profile.Rate,gap=interval*.65;
            if(shot.Time<timing.Last||timing.Credit>shot.Time+gap+.000001)return RejectShot("cadence");
            // Budgets count accepted shooter stamps, not host arrival times (WPN-CX4-SEMI): a legitimate backlog that
            // Relay/Wi-Fi jitter delivers at once keeps its spacing, while stamps (bounded by the window above) still
            // sustain at most ceil(rate)+2 per second. Stamps are monotonic, so expiry is from the front.
            while(timing.Arrivals.Count>0&&shot.Time-timing.Arrivals[0]>=1)timing.Arrivals.RemoveAt(0);int burst=0;foreach(double at in timing.Arrivals)if(shot.Time-at<.25)burst++;
            if(timing.Arrivals.Count>=Math.Ceiling(1/interval)+2||burst>=Math.Ceiling(.25/interval)+2)return RejectShot("arrival-budget");
            R24LanRawTrackingSample self;if(!Fresh(actor,now,out self))return RejectShot("shooter-tracking");
            if(Distance2(self.Pose.Root.Position,shot.Origin)>25)return RejectShot("origin");
            // Commit custody exactly once (source consumes the round before _hit_rays).
            if(paired)weapon.Hands[side]--;else weapon.Ammo--;if(paired)weapon.Ammo=weapon.Hands[0]+weapon.Hands[1];
            if(paired)weapon.HandShots[side]=now;weapon.LastShot=now;timing.Arrivals.Add(shot.Time);timing.Credit=Math.Max(timing.Credit,shot.Time)+gap;timing.Last=shot.Time;AcceptedShots++;LastShotRejection=null;
            LaunchVolley(actor,shot,profile,weapon.Stars,rawWorldClear==null||!rawWorldClear(self.Pose.Head.Position,shot.Origin));
            return true;
        }
        // net_session1000.hurt: friendly fire, protection, rules.damage, shield first.
        bool ApplyCombatDamage(string attacker,string target,float amount,bool direct,int launchTeam=int.MinValue,int marker=0,Float3? damageSource=null,Float3? bladeContact=null){return ApplyCombatDamageAt(attacker,target,amount,direct,launchTeam,marker,damageSource,bladeContact,null);}
        bool ApplyCombatDamageAt(string attacker,string target,float amount,bool direct,int launchTeam,int marker,Float3? damageSource,Float3? bladeContact,Float3? firearmAnchor)
        {
            if(PlatformProtected(attacker)||PlatformProtected(target))return false;
            var a=sessions.Get(attacker);var t=sessions.Get(target);if(t==null||!t.Loaded||!t.HasPosition)return false;var th=hostHealth.Read(t.CanonicalActorId);
            if(th==null||!th.Connected||th.Life!=LifeState.Alive||Single.IsNaN(amount)||Single.IsInfinity(amount)||amount<=0)return false;
            bool allied=attacker!=target&&(launchTeam==int.MinValue?Allied(a,t,roundRules.Teams):roundRules.Teams>0&&t.Team==launchTeam);if(allied&&!roundRules.FriendlyFire)return false;if(clock.Now<th.ProtectionUntil)return false;
            amount*=roundRules.Damage;if(!(amount>0))return false;float shield=direct?0:Math.Min(th.Shield,amount),hp=Math.Min(th.Health,amount-shield);if(hp<=0&&shield<=0)return false;
            if(!hostHealth.ApplyLoss(LifecycleContext(),t.CanonicalActorId,th.Revision,hp,shield,true).Accepted)return false;CombatDamageEvents++;PublishDamageDirection(attacker,target,damageSource);
            var after=hostHealth.Read(t.CanonicalActorId);CombatActor killer;
            if(after!=null&&after.Life!=LifeState.Alive&&a!=null&&attacker!=target&&!allied&&combat.TryGetValue(attacker,out killer)){killer.Kills++;killer.Score++;}
            if(marker>0&&a!=null&&attacker!=target&&after!=null)ConfirmHit(attacker,target,th.Shield-after.Shield,th.Health-after.Health,marker==2,after.Life!=LifeState.Alive,bladeContact,firearmAnchor); // marker: 1 body, 2 head
            scoreDirty=true;SyncHostHealth();return true;
        }
        partial void PublishDamageDirection(string attacker,string victim,Float3? explicitSource);
        partial void ClearDamageDirection();
        partial void FlushDamageDirection();
        partial void ReadDamageDirection(Packet packet,ref bool handled);
        // ---------- hit confirmation (MP-HITMARKER) ----------
        // Only actual applied losses are confirmed; the shooter never infers hits locally.
        void ConfirmHit(string attacker,string target,float shield,float health,bool headshot,bool kill,Float3? bladeContact,Float3? firearmAnchor=null)
        {
            if(!(shield+health>0)||Single.IsInfinity(shield+health))return;R24LanRawTrackingSample pose;bool located=Fresh(target,clock.Now,out pose)&&pose.Pose.Head.Position.IsFinite;
            bool blade=bladeContact.HasValue&&bladeContact.Value.IsFinite;
            var hit=new R26HitConfirm(target,shield,health,headshot,kill,located||blade||firearmAnchor.HasValue,blade?bladeContact.Value:firearmAnchor.HasValue?firearmAnchor.Value:located?pose.Pose.Head.Position:new Float3(),blade);HitConfirmsQueued++;
            if(attacker==OwnActor){LocalHitConfirm(hit);return;}
            foreach(var peer in peers)if(peer.Value.Admitted&&peer.Value.Actor==attacker){
                byte[] frame;using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){w.Write(HitConfirmLayout);Write(w,target,64);w.Write(shield);w.Write(health);w.Write((byte)((headshot?1:0)|(kill?2:0)|(hit.Located?4:0)|(hit.BladeContact?8:0)));w.Write(hit.Point.X);w.Write(hit.Point.Y);w.Write(hit.Point.Z);frame=Packet(PacketKind.Snapshot,ScoreFeature,OwnActor,s.ToArray(),checked(++hitSequence));}
                Queue<byte[]> q;if(!hitPending.TryGetValue(peer.Key,out q)){q=new Queue<byte[]>();hitPending[peer.Key]=q;}if(q.Count>=32)q.Dequeue();q.Enqueue(frame);return;
            }
        }
        void LocalHitConfirm(R26HitConfirm hit){if(hitConfirms.Count>=32)hitConfirms.Dequeue();hitConfirms.Enqueue(hit);}
        void FlushHitConfirms()
        {
            if(State!=R24LanLobbyState.Hosting||hitPending.Count==0)return;List<string> gone=null;
            foreach(var entry in hitPending){if(!peers.ContainsKey(entry.Key)){(gone??(gone=new List<string>())).Add(entry.Key);continue;}var q=entry.Value;int budget=8;while(q.Count>0&&budget-->0&&Send(entry.Key,q.Peek())){q.Dequeue();HitConfirmsSent++;}}
            if(gone!=null)foreach(string key in gone)hitPending.Remove(key);
        }
        void ReadHitConfirm(Packet packet)
        {
            if(packet.BaseSequence!=-1||packet.Sequence<=hitReceived)return;
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){
                if(r.ReadByte()!=HitConfirmLayout)return;string victim=Read(r,64);float shield=r.ReadSingle(),health=r.ReadSingle();int flags=r.ReadByte();var point=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                if(s.Position!=s.Length||flags>15||(flags&12)==8||!Finite(shield)||!Finite(health)||shield<0||health<0||!(shield+health>0)||shield+health>100000||!point.IsFinite||victim==OwnActor)return;
                hitReceived=packet.Sequence;HitConfirmsReceived++;LocalHitConfirm(new R26HitConfirm(victim,shield,health,(flags&1)!=0,(flags&2)!=0,(flags&4)!=0,point,(flags&8)!=0));
            }}catch(InvalidDataException){}catch(IOException){}catch(ArgumentException){}
        }
        // ---------- reloads ----------
        // Net effect of the source _reload state machine on completion: the
        // magazine is topped up from the holder's family reserve exactly once.
        public bool SubmitReload(string profile,int side)
        {
            Check();if(State!=R24LanLobbyState.Hosting&&State!=R24LanLobbyState.Connected||profile==null||Encoding.UTF8.GetByteCount(profile)>128||side< -1||side>1)return false;
            if(State==R24LanLobbyState.Hosting){checked{gameCommandSequence++;}MarkGamePrediction(ReloadFeature,gameCommandSequence);return ApplyReload(OwnActor,profile,side);}
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){byte[] id=Encoding.UTF8.GetBytes(profile);w.Write((byte)id.Length);w.Write(id);w.Write((sbyte)side);return QueueGameCommand(ReloadFeature,s.ToArray());}
        }
        internal bool ApplyReload(string actor,string profile,int side)
        {
            if(PlatformProtected(actor))return false;
            if(!InventoryLive)return false;var member=sessions.Get(actor);var health=member==null?null:hostHealth.Read(member.CanonicalActorId);CombatActor owner;CombatWeapon weapon;
            if(health==null||health.Life!=LifeState.Alive||!combat.TryGetValue(actor,out owner)||!owner.Weapons.TryGetValue(profile,out weapon)||weapon.Family==null&&weapon.Profile!="smaw")return false;
            if(weapon.Profile=="smaw"){R24LanRawTrackingSample raw;if(weapon.Ammo!=0||weapon.RocketReloads<=0||clock.Now-weapon.LastShot<.35||clock.Now-weapon.LastReload<.12||!Fresh(actor,clock.Now,out raw))return false;weapon.Ammo=1;weapon.RocketReloads--;weapon.LastReload=clock.Now;AcceptedReloads++;return true;}
            double now=clock.Now;if(weapon.Hands==null&&(now-weapon.LastShot<.25||now-weapon.LastReload<.25))return false;
            int reserve;owner.Reserves.TryGetValue(weapon.Family,out reserve);bool infinite=GameplayRules.InfiniteAmmo;bool changed=false;
            if(weapon.Hands!=null){int half=weapon.Capacity/2;for(int h=0;h<2;h++){if(side>=0&&h!=side||now-weapon.HandShots[h]<.25||now-weapon.HandReloads[h]<.25)continue;int cap=h==0?(weapon.Capacity+1)/2:half,take=Math.Max(0,cap-weapon.Hands[h]);if(!infinite)take=Math.Min(take,reserve);if(take<=0)continue;weapon.Hands[h]+=take;weapon.HandReloads[h]=now;if(!infinite)reserve-=take;changed=true;}weapon.Ammo=weapon.Hands[0]+weapon.Hands[1];}
            else{int take=Math.Max(0,weapon.Capacity-weapon.Ammo);if(!infinite)take=Math.Min(take,reserve);if(take>0){weapon.Ammo+=take;if(!infinite)reserve-=take;changed=true;}}
            if(!changed)return false;owner.Reserves[weapon.Family]=reserve;weapon.LastReload=now;AcceptedReloads++;return true;
        }
        public bool TryReadAmmo(string actor,string profile,out int loaded,out int reserve)
        {Check();loaded=reserve=0;CombatActor owner;CombatWeapon weapon;if(State!=R24LanLobbyState.Hosting||actor==null||profile==null||!combat.TryGetValue(actor,out owner)||!owner.Weapons.TryGetValue(profile,out weapon))return false;loaded=weapon.Ammo;reserve=weapon.Profile=="smaw"?weapon.RocketReloads:0;if(weapon.Family!=null)owner.Reserves.TryGetValue(weapon.Family,out reserve);return true;}
        // ---------- revive ----------
        public bool RequestRevive(string target)
        {
            Check();if(State!=R24LanLobbyState.Hosting&&State!=R24LanLobbyState.Connected||!Wire.IsText(target,64,false))return false;
            if(State==R24LanLobbyState.Hosting)return ApplyRevive(OwnActor,target);
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){Write(w,target,64);return QueueGameCommand(ReviveFeature,s.ToArray());}
        }
        internal bool ApplyRevive(string actor,string target)
        {
            LastReviveRejection=null;if(!CombatLive||roundRules.Mode!="revive"){LastReviveRejection="match-required";return false;}
            if(PlatformProtected(actor)||PlatformProtected(target)){LastReviveRejection="platform-protected";return false;}
            var a=sessions.Get(actor);var t=sessions.Get(target);if(a==null||t==null||a==t||(!roundRules.EnemyRevive&&!Allied(a,t,roundRules.Teams))||roundRules.Teams==0){LastReviveRejection="ally-required";return false;}
            var th=hostHealth.Read(t.CanonicalActorId);if(th==null||th.Life!=LifeState.Downed){LastReviveRejection="downed-required";return false;}
            var result=AsMember(a.CanonicalActorId,delegate(CommandContext c){return hostHealth.ReviveNearby(c,t.CanonicalActorId,th.Revision,roundRules.EnemyRevive,true);});
            if(!result.Accepted){LastReviveRejection=result.Reason;hostHealth.DrainTransitions();return false;}
            CompleteRescue(actor);return true;
        }
        // Evaluate the first accepted equipped contact, including if a guest request arrives before its pose.
        void SampleCombatPose(string actor,R24LanRawTrackingSample sample)
        {
            if(!PlatformProtected(actor))SamplePaddleCharge(actor,sample);
        }
        bool TryRescuePose(string canonical,out RescuePose pose)
        {
            pose=new RescuePose();if(State!=R24LanLobbyState.Hosting||sessions==null)return false;string actor=null;foreach(var m in sessions.CaptureMembers())if(m.CanonicalActorId==canonical){actor=m.ActorId;break;}
            R24LanRawTrackingSample sample;if(actor==null||!TryWarmupRecoveryPose(actor,out sample)||clock.Now-sample.Time>.5)return false;var p=sample.Pose;
            Float3 lf=Rotate(p.Left.Rotation,new Float3(0,-1,0)),rf=Rotate(p.Right.Rotation,new Float3(0,-1,0));var inverse=new R24LanRotation(-p.Root.Rotation.X,-p.Root.Rotation.Y,-p.Root.Rotation.Z,p.Root.Rotation.W);
            pose=new RescuePose{Tracked=(p.Flags&6)==6,Paused=false,PaddlesActive=p.Paddles,Desktop=false,BodyCenter=RescueBody(sample),Facing=lf.X*rf.X+lf.Y*rf.Y+lf.Z*rf.Z<=-.20f,Left=Electrode(p.Left),Right=Electrode(p.Right),
                LocalLeft=Rotate(inverse,Sub(Electrode(p.Left),p.Root.Position)),LocalRight=Rotate(inverse,Sub(Electrode(p.Right),p.Root.Position)),Chest=new Float3(p.Head.Position.X,p.Head.Position.Y-.6f,p.Head.Position.Z)};
            return true;
        }
        // ---------- scores ----------
        public bool TryReadScore(string actor,out R24LanScoreRow row){Check();row=null;return actor!=null&&scores.TryGetValue(actor,out row);}
        public R24LanScoreRow[] Scores{get{return CopySnapshot(scores.Values);}}
        // net_session1000._check_end score limit (team key, else actor key).
        bool ScoreLimitReached()
        {
            if(roundRules==null||roundRules.Mode=="revive"||roundRules.ScoreLimit<=0)return false;var totals=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var member in sessions.CaptureMembers()){CombatActor c;if(!combat.TryGetValue(member.ActorId,out c))continue;string key=roundRules.Teams>0?"team:"+member.Team:member.ActorId;int prior;totals.TryGetValue(key,out prior);totals[key]=prior+c.Score;}
            foreach(int total in totals.Values)if(total>=roundRules.ScoreLimit)return true;return false;
        }
        void PublishScores()
        {
            FlushFlights();TickBulletFlights();TickRemotePresentation();FlushHitConfirms();FlushDamageDirection();if(State!=R24LanLobbyState.Hosting||!scoreDirty||sessions==null)return;scoreDirty=false;var rows=new List<R24LanScoreRow>();
            foreach(var member in sessions.CaptureMembers()){CombatActor c;combat.TryGetValue(member.ActorId,out c);var h=hostHealth==null?null:hostHealth.Read(member.CanonicalActorId);rows.Add(new R24LanScoreRow(member.ActorId,member.Team,c==null?0:c.Kills,h==null?0:h.Deaths,c==null?0:c.Score));if(rows.Count>=Capacity)break;}
            scores.Clear();foreach(var row in rows)scores[row.Actor]=row;
            PublishScorePages(rows);
        }
        void ReadScores(Packet packet)
        {
            if(ReadBulletContacts(packet))return;
            if(ReadScorePages(packet)||ReadEquipmentPage(packet))return;
            bool damageHandled=false;ReadDamageDirection(packet,ref damageHandled);if(damageHandled)return;
            if(ReadRemotePresentation(packet))return;
            if(packet.Payload!=null&&packet.Payload.Length>0&&packet.Payload[0]==HitConfirmLayout){ReadHitConfirm(packet);return;}
            if(packet.Sequence<1||packet.BaseSequence!=-1){Reject(serverPeer);return;}if(packet.Sequence<=scoreRevision)return;var rows=new List<R24LanScoreRow>();
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){if(r.ReadByte()!=1)throw new InvalidDataException();int n=r.ReadByte();if(n>Capacity)throw new InvalidDataException();for(int i=0;i<n;i++){string actor=Read(r,64);int team=r.ReadInt32(),kills=r.ReadInt32(),deaths=r.ReadInt32(),score=r.ReadInt32();if(team< -1||team>7||kills<0||deaths<0||score<0)throw new InvalidDataException();rows.Add(new R24LanScoreRow(actor,team,kills,deaths,score));}if(s.Position!=s.Length)throw new InvalidDataException();}}
            catch(InvalidDataException){Reject(serverPeer);return;}catch(IOException){Reject(serverPeer);return;}catch(ArgumentException){Reject(serverPeer);return;}
            scoreRevision=packet.Sequence;scores.Clear();foreach(var row in rows)if(HasMember(row.Actor))scores[row.Actor]=row;
        }
        // ---------- client command channel ----------
        // One ordered reliable lane for all gameplay commands; bounded, oldest kept.
        bool QueueGameCommand(string feature,byte[] payload)
        {if(State!=R24LanLobbyState.Connected||pendingGameCommands.Count>=64)return false;long sequence=checked(++gameCommandSequence);pendingGameCommands.Enqueue(Packet(PacketKind.Command,feature,OwnActor,payload,sequence));MarkGamePrediction(feature,sequence);return true;}
        public long AmmoPredictionSequence{get{return ammoPredictionSequence;}}public long ResourcesPredictionSequence{get{return resourcesPredictionSequence;}}
        void MarkGamePrediction(string feature,long sequence){if(feature==ShotFeature||feature==ReloadFeature||feature==RocketCommand)ammoPredictionSequence=sequence;if(feature==ReloadFeature||feature==SupplyCommand||feature==SupplyDropCommand||feature==BuildCommand)resourcesPredictionSequence=sequence;}
        void TickGameCommands(){if(State!=R24LanLobbyState.Connected)return;while(pendingGameCommands.Count>0&&Send(serverPeer,pendingGameCommands.Peek()))pendingGameCommands.Dequeue();}
        bool ReadGameCommand(Peer peer,Packet packet)
        {
            if(packet.FeatureId!=VehicleFire&&packet.FeatureId!=VehicleMount&&packet.FeatureId!=VehicleDismount&&packet.FeatureId!=CatalogCommand&&packet.FeatureId!=GrenadeCommand&&packet.FeatureId!=RocketCommand&&packet.FeatureId!=EquipmentCommand&&packet.FeatureId!=BuildCommand&&packet.FeatureId!=SupplyDropCommand&&packet.FeatureId!=FoodCommand&&packet.FeatureId!=SupplyCommand&&packet.FeatureId!=LootCommand&&packet.FeatureId!=ShotFeature&&packet.FeatureId!=ReloadFeature&&packet.FeatureId!=ReviveFeature&&packet.FeatureId!=PodLaunchFeature&&packet.FeatureId!=PodEjectFeature)return false;
            // Ordered reliable lane: duplicates/replays are dropped, never applied twice.
            if(packet.BaseSequence!=-1||packet.Sequence<=peer.LastGameCommand)return true;peer.LastGameCommand=packet.Sequence;
            if(packet.FeatureId==VehicleFire||packet.FeatureId==VehicleMount||packet.FeatureId==VehicleDismount){ReadVehicleCommand(peer.Actor,packet.FeatureId,packet.Payload);return true;}
            if(packet.FeatureId==CatalogCommand){ReadCatalog(peer.Actor,packet.Payload);return true;}
            if(packet.FeatureId==GrenadeCommand||packet.FeatureId==RocketCommand){ReadExplosiveCommand(peer.Actor,packet.FeatureId,packet.Payload);return true;}
            if(packet.FeatureId==EquipmentCommand){ReadEquipment(peer.Actor,packet.Payload);return true;}
            if(packet.FeatureId==BuildCommand){if(packet.Payload.Length==4&&packet.Payload[3]<=1)ApplyBuild(peer.Actor,packet.Payload[0],packet.Payload[1],packet.Payload[2],packet.Payload[3]==1);return true;}
            if(packet.FeatureId==SupplyDropCommand){ReadSupplyDrop(peer.Actor,packet.Payload);return true;}
            if(packet.FeatureId==FoodCommand){ReadFoodCommand(peer.Actor,packet.Payload);return true;}
            if(packet.FeatureId==SupplyCommand){ReadSupplyCommand(peer.Actor,packet.Payload);return true;}
            if(packet.FeatureId==LootCommand){ReadLootCommand(peer.Actor,packet.Payload);return true;}
            if(packet.FeatureId==ShotFeature){R24LanShot shot;if(!R24LanShot.TryDecode(packet.Payload,out shot)){RejectShot("payload");return true;}if(!ApplyShot(peer.Actor,shot))CancelBulletPrediction(peer.Actor,packet.Sequence);return true;}
            if(packet.FeatureId==PodLaunchFeature){string pod;if(!TryReadAscii(packet.Payload,64,out pod))return true;ApplyPodLaunch(peer.Actor,pod);return true;}
            if(packet.FeatureId==PodEjectFeature){if(packet.Payload.Length==0)ApplyPodEject(peer.Actor);return true;}
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){
                if(packet.FeatureId==ReloadFeature){int n=r.ReadByte();if(n<1||n>128||n>s.Length-s.Position)return true;string profile=Utf8.GetString(r.ReadBytes(n));int side=r.ReadSByte();if(s.Position==s.Length&&side>=-1&&side<=1)ApplyReload(peer.Actor,profile,side);return true;}
                string target=Read(r,64);if(s.Position==s.Length)ApplyRevive(peer.Actor,target);return true;}}
            catch(InvalidDataException){return true;}catch(IOException){return true;}catch(ArgumentException){return true;}
        }
        static bool TryReadAscii(byte[] bytes,int max,out string text){text=null;if(bytes==null||bytes.Length<1||bytes.Length>max)return false;foreach(byte b in bytes)if(b<33||b>126)return false;text=Encoding.ASCII.GetString(bytes);return true;}
        // REMOTE: the existing host-authenticated score snapshot lane also carries visual
        // equipment (layout 3), launches (10) and contacts (11), with the same transport
        // reliability as scores. Transient visual queues are cleared at round boundaries.
        readonly Dictionary<string,R26RemoteEquipment> remoteEquipment=new Dictionary<string,R26RemoteEquipment>(StringComparer.Ordinal);
        readonly Queue<R26RemoteVolley> remoteVolleys=new Queue<R26RemoteVolley>();long remoteVisualSequence,remoteEquipRevision,remoteVolleyRevision;double nextRemoteEquipment,nextRemoteRefresh;byte[] lastRemoteEquipment;
        public int RemoteVolleysReceived{get;private set;}
        public bool TryReadRemoteEquipment(string actor,out R26RemoteEquipment row){Check();return remoteEquipment.TryGetValue(actor,out row);}
        public bool TryDequeueRemoteVolley(out R26RemoteVolley shot){Check();shot=remoteVolleys.Count==0?null:remoteVolleys.Dequeue();return shot!=null;}
        void ClearRemotePresentation(){remoteEquipment.Clear();remoteVolleys.Clear();remoteEquipRevision=remoteVolleyRevision=0;nextRemoteEquipment=nextRemoteRefresh=0;lastRemoteEquipment=null;}
        void EnqueueRemoteVolley(R26RemoteVolley shot){if(shot.Actor==OwnActor)return;if(remoteVolleys.Count>=32)remoteVolleys.Dequeue();remoteVolleys.Enqueue(shot);RemoteVolleysReceived++;}
        void BroadcastRemoteVolley(string actor,R24LanShot shot,Float3[] directions,long id,double launch)
        {
            EquippedWeapon e;int hand=equipment.TryGetValue(actor,out e)?e.Hand:shot.Side;if(R24LanLoadout.Paired(shot.Profile)&&shot.Side==1)hand=1-hand;
            var value=new R26RemoteVolley(actor,shot.Profile,hand,launch,shot.Origin,directions,id);byte[] payload;try{payload=R26RemoteWire.Volley(value);}catch(ArgumentException){return;}EnqueueRemoteVolley(value);
            byte[] frame=Packet(PacketKind.Snapshot,ScoreFeature,OwnActor,payload,checked(++remoteVisualSequence));foreach(var peer in peers)if(RemotePeerReady(peer.Value)&&peer.Value.Actor!=actor)SendFlight(peer.Key,frame);
        }
        void TickRemotePresentation()
        {
            if(!InventoryLive||clock.Now<nextRemoteEquipment)return;nextRemoteEquipment=clock.Now+.1;var rows=new List<R26RemoteEquipment>(Capacity);remoteEquipment.Clear();
            foreach(var member in members){EquippedWeapon e;CombatActor owner;CombatWeapon weapon;string profile=null;int hand=1;bool suppressed=true,blocked=true;
                if(equipment.TryGetValue(member.Actor,out e)){hand=e.Hand;suppressed=e.Suppressed;blocked=e.Blocked;if(e.Id!=null&&combat.TryGetValue(member.Actor,out owner)&&owner.Weapons.TryGetValue(e.Id,out weapon))profile=weapon.Profile;}
                var row=new R26RemoteEquipment(member.Actor,profile,hand,suppressed,blocked,ShowingPaddles(member.Actor));rows.Add(row);remoteEquipment[member.Actor]=row;
            }
            byte[] payload=R26RemoteWire.Equipment(rows.ToArray());bool changed=lastRemoteEquipment==null||lastRemoteEquipment.Length!=payload.Length;if(!changed)for(int i=0;i<payload.Length;i++)if(payload[i]!=lastRemoteEquipment[i]){changed=true;break;}
            if(!changed&&clock.Now<nextRemoteRefresh)return;lastRemoteEquipment=payload;nextRemoteRefresh=clock.Now+.5;PublishEquipmentPages(rows);
        }
        bool RemotePeerReady(Peer peer){var member=sessions.Get(peer.Actor);return peer.Admitted&&peer.Roster==null&&peer.MapState==null&&member!=null&&member.Loaded;}
        bool ReadRemotePresentation(Packet packet)
        {
            if(packet.Payload==null||packet.Payload.Length==0)return false;int layout=packet.Payload[0];if(layout!=R26RemoteWire.EquipmentLayout&&layout!=R26RemoteWire.VolleyLayout)return false;
            if(packet.Sequence<1||packet.BaseSequence!=-1)return true;
            if(layout==R26RemoteWire.EquipmentLayout){R26RemoteEquipment[] rows;if(packet.Sequence<=remoteEquipRevision||!R26RemoteWire.TryEquipment(packet.Payload,out rows))return true;foreach(var row in rows)if(!HasMember(row.Actor))return true;remoteEquipRevision=packet.Sequence;remoteEquipment.Clear();foreach(var row in rows)remoteEquipment[row.Actor]=row;}
            else{R26RemoteVolley shot;if(packet.Sequence<=remoteVolleyRevision||!R26RemoteWire.TryVolley(packet.Payload,out shot)||!HasMember(shot.Actor)||shot.Time>EstimatedHostTime+.1||EstimatedHostTime-shot.Time>1)return true;remoteVolleyRevision=packet.Sequence;EnqueueRemoteVolley(shot);}return true;
        }
        // ---------- math ----------
        static float SphereRay(Float3 origin,Float3 unit,Float3 center,float radius){return HandsWeapons.R26RemoteAimVolumes.SphereEntry(origin.X,origin.Y,origin.Z,unit.X,unit.Y,unit.Z,center.X,center.Y,center.Z,radius);}
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        static double Distance2(Float3 a,Float3 b){double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;return x*x+y*y+z*z;}
        static Float3 Normalize(Float3 v){double n=Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y+(double)v.Z*v.Z);return n<=0?v:new Float3((float)(v.X/n),(float)(v.Y/n),(float)(v.Z/n));}
        static Float3 Rotate(R24LanRotation q,Float3 v){float tx=2*(q.Y*v.Z-q.Z*v.Y),ty=2*(q.Z*v.X-q.X*v.Z),tz=2*(q.X*v.Y-q.Y*v.X);return new Float3(v.X+q.W*tx+q.Y*tz-q.Z*ty,v.Y+q.W*ty+q.Z*tx-q.X*tz,v.Z+q.W*tz+q.X*ty-q.Y*tx);}
    }
}
