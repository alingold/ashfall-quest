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
        void ClearCombat(){ClearVehicles();ClearCatalog();ClearExplosives();ClearEquipment();ClearConstruction();ownResources=null;ClearAmmo();ClearFood();ClearSharedSupplies();ClearLoot();combat.Clear();scores.Clear();scoreDirty=false;scoreRevision=-1;pendingGameCommands.Clear();gameCommandSequence=ammoPredictionSequence=resourcesPredictionSequence=0;foreach(var p in peers.Values){p.ScoreFrame=null;p.LastGameCommand=0;}}
        void EnsureCombatActor(string actor)
        {
            if(State!=R24LanLobbyState.Hosting||GameplayRules==null||combat.ContainsKey(actor))return;var value=new CombatActor{Builds=GameplayRules.StartingBuilds};
            foreach(int catalog in GameplayRules.StartingCatalog){string profile=R24LanLoadout.Profile(catalog);if(profile==null)continue;int capacity=R24LanLoadout.Capacity(profile,1);
                var weapon=new CombatWeapon{Profile=profile,Family=R24LanLoadout.Family(profile),Stars=1,Capacity=capacity,Ammo=capacity,RocketReloads=profile=="smaw"?2:0};
                if(R24LanLoadout.Paired(profile))weapon.Hands=new[]{(capacity+1)/2,capacity/2};value.Weapons[profile]=weapon;}
            combat.Add(actor,value);scoreDirty=true;
        }
        static bool Allied(MemberSnapshot a,MemberSnapshot b,int teams){return a!=null&&b!=null&&(a.ActorId==b.ActorId||teams>0&&a.Team==b.Team);}
        bool Fresh(string actor,double at,out R24LanRawTrackingSample sample){if(roundRules==null)return TryReadInventoryTracking(actor,at,out sample);return TryReadRawTracking(actor,at,out sample);}
        bool RejectShot(string why){RejectedShots++;LastShotRejection=why;return false;}
        // ---------- shots ----------
        public bool SubmitShot(string profile,int side,Float3 origin,Float3[] directions,string instance=null)
        {
            Check();if(State!=R24LanLobbyState.Hosting&&State!=R24LanLobbyState.Connected)return false;
            var shot=new R24LanShot(profile,side,EstimatedHostTime,origin,directions,instance);if(!shot.Valid)return false;
            if(State==R24LanLobbyState.Hosting){checked{gameCommandSequence++;}MarkGamePrediction(ShotFeature,gameCommandSequence);return ApplyShot(OwnActor,shot);}
            return QueueGameCommand(ShotFeature,shot.Encode());
        }
        internal bool ApplyShot(string actor,R24LanShot shot)
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
            timing.Arrivals.RemoveAll(delegate(double at){return now-at>=1;});int burst=0;foreach(double at in timing.Arrivals)if(now-at<.25)burst++;
            if(timing.Arrivals.Count>=Math.Ceiling(1/interval)+2||burst>=Math.Ceiling(.25/interval)+2)return RejectShot("arrival-budget");
            R24LanRawTrackingSample self;if(!Fresh(actor,now,out self))return RejectShot("shooter-tracking");
            if(Distance2(self.Pose.Root.Position,shot.Origin)>25)return RejectShot("origin");
            // Commit custody exactly once (source consumes the round before _hit_rays).
            if(paired)weapon.Hands[side]--;else weapon.Ammo--;if(paired)weapon.Ammo=weapon.Hands[0]+weapon.Hands[1];
            if(paired)weapon.HandShots[side]=now;weapon.LastShot=now;timing.Arrivals.Add(now);timing.Credit=Math.Max(timing.Credit,shot.Time)+gap;timing.Last=shot.Time;AcceptedShots++;LastShotRejection=null;
            if(rawWorldClear==null||!rawWorldClear(self.Pose.Head.Position,shot.Origin))return true; // bore inside a blocker: consumed, no hit
            if(!CombatLive)return true;
            double sampleAt=Math.Max(now-.20,Math.Min(now,shot.Time));var grouped=new Dictionary<string,float>(StringComparer.Ordinal);var heads=new Dictionary<string,bool>(StringComparer.Ordinal);var members=sessions.CaptureMembers();
            foreach(var raw in shot.Directions){
                var d=Normalize(raw);float nearest=ballisticTrace==null?180:ballisticTrace(shot.Origin,d,180);if(Single.IsNaN(nearest)||nearest<0||nearest>180)nearest=0;
                nearest=GuardDistance(actor,shot.Origin,d,nearest);float panelDistance;string panel=NearestSharedPanel(shot.Origin,d,nearest,out panelDistance);if(panel!=null)nearest=panelDistance;float bikeDistance;string bike=NearestVehicle(shot.Origin,d,nearest,out bikeDistance);if(bike!=null){nearest=bikeDistance;panel=null;}string target=null;bool headshot=false;
                foreach(var other in members){
                    if(other.ActorId==actor||!other.Loaded||!other.HasPosition)continue;var oh=hostHealth.Read(other.CanonicalActorId);if(oh==null||!oh.Connected||oh.Life!=LifeState.Alive)continue;
                    R24LanRawTrackingSample pose;if(!Fresh(other.ActorId,sampleAt,out pose))continue;var head=pose.Pose.Head.Position;
                    float h=SphereRay(shot.Origin,d,head,.17f),c=SphereRay(shot.Origin,d,new Float3(head.X,head.Y-.45f,head.Z),.29f),hip=SphereRay(shot.Origin,d,new Float3(head.X,head.Y-.92f,head.Z),.27f),t=Math.Min(h,Math.Min(c,hip));
                    if(t<nearest){nearest=t;target=other.ActorId;headshot=h<=Math.Min(c,hip);}
                }
                if(target==null){if(bike!=null)DamageVehicle(bike,profile.Damage(weapon.Stars,false,nearest),true);else if(panel!=null)DamageSharedPanel(panel,profile.Damage(weapon.Stars,false,nearest)*GameplayRules.BuildDamage);continue;}float amount=profile.Damage(weapon.Stars,headshot,nearest)*(headshot?roundRules.Headshots:1),prior;bool wasHead;
                grouped.TryGetValue(target,out prior);grouped[target]=prior+amount;heads.TryGetValue(target,out wasHead);heads[target]=wasHead||headshot;
            }
            foreach(var pair in grouped)ApplyCombatDamage(actor,pair.Key,pair.Value,false);
            return true;
        }
        // net_session1000.hurt: friendly fire, protection, rules.damage, shield first.
        bool ApplyCombatDamage(string attacker,string target,float amount,bool direct,int launchTeam=int.MinValue)
        {
            var a=sessions.Get(attacker);var t=sessions.Get(target);if(t==null||!t.Loaded||!t.HasPosition)return false;var th=hostHealth.Read(t.CanonicalActorId);
            if(th==null||!th.Connected||th.Life!=LifeState.Alive||Single.IsNaN(amount)||Single.IsInfinity(amount)||amount<=0)return false;
            bool allied=attacker!=target&&(launchTeam==int.MinValue?Allied(a,t,roundRules.Teams):roundRules.Teams>0&&t.Team==launchTeam);if(allied&&!roundRules.FriendlyFire)return false;if(clock.Now<th.ProtectionUntil)return false;
            amount*=roundRules.Damage;if(!(amount>0))return false;float shield=direct?0:Math.Min(th.Shield,amount),hp=Math.Min(th.Health,amount-shield);if(hp<=0&&shield<=0)return false;
            if(!hostHealth.ApplyLoss(LifecycleContext(),t.CanonicalActorId,th.Revision,hp,shield,true).Accepted)return false;CombatDamageEvents++;
            var after=hostHealth.Read(t.CanonicalActorId);CombatActor killer;
            if(after!=null&&after.Life!=LifeState.Alive&&a!=null&&attacker!=target&&!allied&&combat.TryGetValue(attacker,out killer)){killer.Kills++;killer.Score++;}
            scoreDirty=true;SyncHostHealth();return true;
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
            LastReviveRejection=null;if(!CombatLive){LastReviveRejection="match-required";return false;}
            var a=sessions.Get(actor);var t=sessions.Get(target);if(a==null||t==null||a==t||!Allied(a,t,roundRules.Teams)||roundRules.Teams==0){LastReviveRejection="ally-required";return false;}
            var th=hostHealth.Read(t.CanonicalActorId);if(th==null||th.Life!=LifeState.Downed){LastReviveRejection="downed-required";return false;}
            var result=AsMember(a.CanonicalActorId,delegate(CommandContext c){return hostHealth.Revive(c,t.CanonicalActorId,th.Revision);});
            if(!result.Accepted){LastReviveRejection=result.Reason;hostHealth.DrainTransitions();return false;}
            Revives++;scoreDirty=true;SyncHostHealth();return true;
        }
        // HealthService paddle charge is sampled on every accepted authority pose.
        void SampleCombatPose(string actor,R24LanRawTrackingSample sample)
        {
            if(!CombatLive||sample==null)return;var member=sessions.Get(actor);if(member==null)return;var h=hostHealth.Read(member.CanonicalActorId);if(h==null||h.Life!=LifeState.Alive)return;
            AsMember(member.CanonicalActorId,delegate(CommandContext c){return hostHealth.SamplePaddles(c);});hostHealth.DrainTransitions();
        }
        bool TryRescuePose(string canonical,out RescuePose pose)
        {
            pose=new RescuePose();if(State!=R24LanLobbyState.Hosting||sessions==null)return false;string actor=null;foreach(var m in sessions.CaptureMembers())if(m.CanonicalActorId==canonical){actor=m.ActorId;break;}
            R24LanRawTrackingSample sample;if(actor==null||!TryWarmupRecoveryPose(actor,out sample)||clock.Now-sample.Time>1)return false;var p=sample.Pose;
            Float3 lf=Rotate(p.Left.Rotation,new Float3(0,0,1)),rf=Rotate(p.Right.Rotation,new Float3(0,0,1));var inverse=new R24LanRotation(-p.Root.Rotation.X,-p.Root.Rotation.Y,-p.Root.Rotation.Z,p.Root.Rotation.W);
            pose=new RescuePose{Tracked=(p.Flags&6)==6,Paused=false,PaddlesActive=p.Paddles,Desktop=false,Facing=lf.X*rf.X+lf.Y*rf.Y+lf.Z*rf.Z<=-.20f,Left=p.Left.Position,Right=p.Right.Position,
                LocalLeft=Rotate(inverse,Sub(p.Left.Position,p.Root.Position)),LocalRight=Rotate(inverse,Sub(p.Right.Position,p.Root.Position)),Chest=new Float3(p.Head.Position.X,p.Head.Position.Y-.6f,p.Head.Position.Z)};
            return true;
        }
        // ---------- scores ----------
        public bool TryReadScore(string actor,out R24LanScoreRow row){Check();row=null;return actor!=null&&scores.TryGetValue(actor,out row);}
        public R24LanScoreRow[] Scores{get{return CopySnapshot(scores.Values);}}
        // net_session1000._check_end score limit (team key, else actor key).
        bool ScoreLimitReached()
        {
            if(roundRules==null||roundRules.ScoreLimit<=0)return false;var totals=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var member in sessions.CaptureMembers()){CombatActor c;if(!combat.TryGetValue(member.ActorId,out c))continue;string key=roundRules.Teams>0?"team:"+member.Team:member.ActorId;int prior;totals.TryGetValue(key,out prior);totals[key]=prior+c.Score;}
            foreach(int total in totals.Values)if(total>=roundRules.ScoreLimit)return true;return false;
        }
        void PublishScores()
        {
            if(State!=R24LanLobbyState.Hosting||!scoreDirty||sessions==null)return;scoreDirty=false;var rows=new List<R24LanScoreRow>();
            foreach(var member in sessions.CaptureMembers()){CombatActor c;combat.TryGetValue(member.ActorId,out c);var h=hostHealth==null?null:hostHealth.Read(member.CanonicalActorId);rows.Add(new R24LanScoreRow(member.ActorId,member.Team,c==null?0:c.Kills,h==null?0:h.Deaths,c==null?0:c.Score));if(rows.Count>=Capacity)break;}
            scores.Clear();foreach(var row in rows)scores[row.Actor]=row;
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){w.Write((byte)1);w.Write((byte)rows.Count);foreach(var row in rows){Write(w,row.Actor,64);w.Write(row.Team);w.Write(row.Kills);w.Write(row.Deaths);w.Write(row.Score);}
                var frame=Packet(PacketKind.Snapshot,ScoreFeature,OwnActor,s.ToArray(),checked(++scoreSequence));foreach(var p in peers.Values)if(p.Admitted)p.ScoreFrame=frame;}
        }
        void ReadScores(Packet packet)
        {
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
            if(packet.FeatureId==ShotFeature){R24LanShot shot;if(!R24LanShot.TryDecode(packet.Payload,out shot)){RejectShot("payload");return true;}ApplyShot(peer.Actor,shot);return true;}
            if(packet.FeatureId==PodLaunchFeature){string pod;if(!TryReadAscii(packet.Payload,64,out pod))return true;ApplyPodLaunch(peer.Actor,pod);return true;}
            if(packet.FeatureId==PodEjectFeature){if(packet.Payload.Length==0)ApplyPodEject(peer.Actor);return true;}
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){
                if(packet.FeatureId==ReloadFeature){int n=r.ReadByte();if(n<1||n>128||n>s.Length-s.Position)return true;string profile=Utf8.GetString(r.ReadBytes(n));int side=r.ReadSByte();if(s.Position==s.Length&&side>=-1&&side<=1)ApplyReload(peer.Actor,profile,side);return true;}
                string target=Read(r,64);if(s.Position==s.Length)ApplyRevive(peer.Actor,target);return true;}}
            catch(InvalidDataException){return true;}catch(IOException){return true;}catch(ArgumentException){return true;}
        }
        static bool TryReadAscii(byte[] bytes,int max,out string text){text=null;if(bytes==null||bytes.Length<1||bytes.Length>max)return false;foreach(byte b in bytes)if(b<33||b>126)return false;text=Encoding.ASCII.GetString(bytes);return true;}
        // ---------- math ----------
        static float SphereRay(Float3 origin,Float3 unit,Float3 center,float radius){var o=Sub(origin,center);float b=o.X*unit.X+o.Y*unit.Y+o.Z*unit.Z,c=o.X*o.X+o.Y*o.Y+o.Z*o.Z-radius*radius,d=b*b-c;if(d<0)return Single.PositiveInfinity;float t=-b-(float)Math.Sqrt(d);return t>=0?t:Single.PositiveInfinity;}
        static Float3 Sub(Float3 a,Float3 b){return new Float3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
        static double Distance2(Float3 a,Float3 b){double x=(double)a.X-b.X,y=(double)a.Y-b.Y,z=(double)a.Z-b.Z;return x*x+y*y+z*z;}
        static Float3 Normalize(Float3 v){double n=Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y+(double)v.Z*v.Z);return n<=0?v:new Float3((float)(v.X/n),(float)(v.Y/n),(float)(v.Z/n));}
        static Float3 Rotate(R24LanRotation q,Float3 v){float tx=2*(q.Y*v.Z-q.Z*v.Y),ty=2*(q.Z*v.X-q.X*v.Z),tz=2*(q.X*v.Y-q.Y*v.X);return new Float3(v.X+q.W*tx+q.Y*tz-q.Z*ty,v.Y+q.W*ty+q.Z*tx-q.X*tz,v.Z+q.W*tz+q.X*ty-q.Y*tx);}
    }
}
