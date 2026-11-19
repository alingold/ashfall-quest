using System;using System.Collections.Generic;using System.IO;using Ashfall.Core;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Networking;using Ashfall.Integration.Playable.JumpPads;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        public const double NearbyReviveSeconds=10;public const float NearbyReviveRange=1;
        sealed class RescueHold {internal string Target;internal long Life,TargetLife;internal int Spawn,TargetSpawn;internal double Began,Last;}
        readonly Dictionary<string,RescueHold> rescueHolds=new Dictionary<string,RescueHold>(StringComparer.Ordinal);
        R23SourceHealthStation[] rescueStations=new R23SourceHealthStation[0];double[] stationReady=new double[0];Func<Float3,Float3,bool> stationClear;
        double nextRescueTick;long supportSequence;string supportTarget;float supportProgress;
        public int StationRestores{get;private set;}
        public void BindHealthStations(string mapHash,R23SourceHealthStation[] rows,Func<Float3,Float3,bool> clear)
        {Check();if(State!=R24LanLobbyState.Hosting||SelectedMap==null||mapHash!=MapManifestHash||rows==null||rows.Length>128||clear==null)throw new ArgumentException("host station world");foreach(var row in rows)if(row==null)throw new ArgumentException("station");rescueStations=(R23SourceHealthStation[])rows.Clone();stationReady=new double[rows.Length];stationClear=clear;}
        public double StationCooldownRemaining(int index){Check();return index<0||index>=stationReady.Length?0:Math.Max(0,Math.Min(15,stationReady[index]-EstimatedHostTime));}
        public bool TryNearbyReviveProgress(out string target,out float fraction)
        {Check();target=supportTarget;fraction=supportProgress;if(State==R24LanLobbyState.Hosting){RescueHold hold;if(rescueHolds.TryGetValue(OwnActor,out hold)){target=hold.Target;fraction=(float)Math.Min(1,(clock.Now-hold.Began)/NearbyReviveSeconds);}else target=null;}return target!=null;}
        void ClearRescueStations(){rescueHolds.Clear();rescueCues.Clear();paddleSamples.Clear();rescueStations=new R23SourceHealthStation[0];stationReady=new double[0];stationClear=null;nextRescueTick=0;supportSequence=0;supportTarget=null;supportProgress=0;}
        static double RescueDistance(Float3 a,Float3 b){double x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return Math.Sqrt(x*x+y*y+z*z);}
        // The published rig root follows the player on the floor. The calibrated tracking origin
        // and the HMD can move independently above it. Aim the visibility ray through the body.
        static Float3 RescueBody(R24LanRawTrackingSample sample){var root=sample.Pose.Root.Position;return new Float3(root.X,root.Y+1,root.Z);}
        bool RescueSample(string actor,out R24LanRawTrackingSample sample){return TryWarmupRecoveryPose(actor,out sample)&&clock.Now-sample.Time<=.5;}
        void TickRescueStations()
        {
            if(clock.Now<nextRescueTick)return;nextRescueTick=clock.Now+.1;RetirePaddleSamples();
            bool live=SelectedMap!=null&&SelectedMap.Active&&!SelectedMap.Cancelled&&!SelectedMap.Results;
            var held=new HashSet<string>(StringComparer.Ordinal);
            if(live)foreach(var member in sessions.CaptureMembers()){
                var h=hostHealth.Read(member.CanonicalActorId);R24LanRawTrackingSample pose;
                if(!member.Loaded||!member.HasPosition||h==null||!h.Connected||h.Life!=LifeState.Alive||!RescueSample(member.ActorId,out pose))continue;
                if(clock.Now-pose.Time<=.25&&stationClear!=null&&(h.Health<100||h.Shield<100)){
                    var feet=new Float3(pose.Pose.Head.Position.X,pose.Pose.Root.Position.Y,pose.Pose.Head.Position.Z);
                    for(int i=0;i<rescueStations.Length;i++){
                        var row=rescueStations[i];if(!R23HealthContactMath.Contains(row,feet))continue;Float3 from,to;R23HealthContactMath.ContactSegment(row,feet,out from,out to);
                        if(R23HealthContactMath.NeedsContactRay(from,to)&&!stationClear(from,to))continue;
                        if(clock.Now>=stationReady[i]){
                            var result=AsMember(member.CanonicalActorId,delegate(CommandContext c){HealthService.PreparedGain gain;if(!hostHealth.PrepareGain(c,c.CommandId,c.ActorId,h.Revision,h.LifeGeneration,h.ConnectionGeneration,100-h.Health,100-h.Shield,out gain))return RuleResult.Reject("station-gain");if(!gain.Validate()){gain.Abort();return RuleResult.Reject("station-stale");}gain.CommitValidated();return RuleResult.Accept();});
                            if(result.Accepted){stationReady[i]=clock.Now+R23HealthStationRecharge.Cooldown;StationRestores++;SyncHostHealth();}
                        }break;
                    }
                }
                // Keep the separate proximity hold available with the paddles stowed.
                if(!CombatLive||roundRules.Mode!="revive"||roundRules.Teams==0||pose.Pose.Paddles)continue;
                string target=null;R24LanRawTrackingSample targetPose=null;HealthSnapshot th=null;double nearest=NearbyReviveRange;RescueHold hold;rescueHolds.TryGetValue(member.ActorId,out hold);var body=RescueBody(pose);
                foreach(var other in sessions.CaptureMembers()){
                    var health=hostHealth.Read(other.CanonicalActorId);R24LanRawTrackingSample raw;
                    if(other.ActorId==member.ActorId||(!roundRules.EnemyRevive&&other.Team!=member.Team)||!other.Loaded||!other.HasPosition||health==null||!health.Connected||health.Life!=LifeState.Downed||!RescueSample(other.ActorId,out raw))continue;
                    var otherBody=RescueBody(raw);double distance=RescueDistance(body,otherBody);bool continuing=hold!=null&&hold.Target==other.ActorId;
                    if(distance>NearbyReviveRange||(!continuing&&distance>nearest)||rawWorldClear==null||!rawWorldClear(body,otherBody))continue;
                    target=other.ActorId;targetPose=raw;th=health;nearest=distance;if(continuing)break;
                }
                if(target==null)continue;held.Add(member.ActorId);
                if(hold==null||hold.Target!=target||hold.Life!=h.LifeGeneration||hold.TargetLife!=th.LifeGeneration||hold.Spawn!=pose.Pose.SpawnRevision||hold.TargetSpawn!=targetPose.Pose.SpawnRevision||clock.Now-hold.Last>.5){
                    hold=new RescueHold{Target=target,Life=h.LifeGeneration,TargetLife=th.LifeGeneration,Spawn=pose.Pose.SpawnRevision,TargetSpawn=targetPose.Pose.SpawnRevision,Began=clock.Now};rescueHolds[member.ActorId]=hold;
                }
                hold.Last=clock.Now;if(clock.Now-hold.Began<NearbyReviveSeconds)continue;
                var revived=AsMember(member.CanonicalActorId,delegate(CommandContext c){return hostHealth.ReviveNearby(c,th.Id,th.Revision,roundRules.EnemyRevive);});rescueHolds.Remove(member.ActorId);
                if(revived.Accepted){CompleteRescue(member.ActorId);}else hostHealth.DrainTransitions();
            }
            var retired=new List<string>();foreach(var actor in rescueHolds.Keys)if(!held.Contains(actor))retired.Add(actor);foreach(var actor in retired)rescueHolds.Remove(actor);
            // Same authenticated host-only Health lane, subtype 2. Repeat complete small state so loss/rejoin converges.
            foreach(var peer in peers.Values)if(peer.Admitted){RescueHold hold;rescueHolds.TryGetValue(peer.Actor,out hold);using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){
                w.Write((byte)2);Write(w,MapManifestHash,64);w.Write(clock.Now);w.Write((byte)stationReady.Length);foreach(double ready in stationReady)w.Write((ushort)Math.Ceiling(Math.Max(0,Math.Min(15,ready-clock.Now))*100));Write(w,hold==null?"-":hold.Target,64);w.Write(hold==null?0:(float)Math.Min(1,(clock.Now-hold.Began)/NearbyReviveSeconds));
                peer.Health["/rescue-stations"]=Packet(PacketKind.Snapshot,HealthStateFeature,OwnActor,s.ToArray(),checked(++healthStateSequence));
            }}
        }
        void ReadRescueStations(Packet packet)
        {
            if(packet.Sequence<=supportSequence||packet.BaseSequence!=-1)return;
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){
                if(r.ReadByte()!=2)throw new InvalidDataException();string map=Read(r,64);double stamp=r.ReadDouble();if(!Finite(stamp)||stamp<0)throw new InvalidDataException();int count=r.ReadByte();if(count>128)throw new InvalidDataException();var ready=new double[count];for(int i=0;i<count;i++){int remaining=r.ReadUInt16();if(remaining>1500)throw new InvalidDataException();ready[i]=stamp+remaining*.01;}
                string target=Read(r,64);float progress=r.ReadSingle();if(!Finite(progress)||progress<0||progress>1||s.Position!=s.Length)throw new InvalidDataException();
                if(map!=MapManifestHash||SelectedMap==null||!SelectedMap.Active||SelectedMap.Cancelled)return;stationReady=ready;supportTarget=target=="-"?null:target;supportProgress=progress;supportSequence=packet.Sequence;
            }}catch(IOException){Reject(serverPeer);}catch(ArgumentException){Reject(serverPeer);}
        }
    }
}
