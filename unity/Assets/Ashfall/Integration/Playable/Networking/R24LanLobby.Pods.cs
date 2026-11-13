using System;using System.Collections.Generic;using System.IO;using System.Text;using Ashfall.Core;using Ashfall.Modules.Flight;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.Playable.Networking
{
    // Host's own launch runtime/world. rideKey is that runtime's player key
    // ("player" for the listen host's Root). Returns false unless the actor's
    // authenticated root is inside its own team's unused cabin (TryCabin).
    public interface IR24LanPodWorld
    {bool TryPlan(string rideKey,int team,string podId,Float3 authenticatedRoot,double roundClock,out CapsulePlan plan);}
    public enum R24LanPodPhase:byte{Flying=0,Released=1,Landed=2}
    public sealed class R24LanPodState
    {
        public readonly string Actor;public readonly CapsulePlan Plan;public readonly double Started;public readonly R24LanPodPhase Phase;public readonly Float3 Terminal;public readonly long Sequence;
        internal R24LanPodState(string actor,CapsulePlan plan,double started,R24LanPodPhase phase,Float3 terminal,long sequence){Actor=actor;Plan=plan;Started=started;Phase=phase;Terminal=terminal;Sequence=sequence;}
    }
    // net_session1000 _pod_launch_r12/_step_pods_r12/_pod_eject_r12: the host
    // plans the capsule, relocates the rider to its start (new revision), owns
    // the path while flying (client poses are refused, never continuity-skipped)
    // and relocates to its own exit/eject point. The client only adopts it.
    public sealed partial class R24LanLobby
    {
        const string PodLaunchFeature="playable.pod.launch/1",PodEjectFeature="playable.pod.eject/1",PodStateFeature="playable.pod.state/1";
        sealed class PodRide{internal CapsulePlan Plan;internal double Started;internal R24LanPodPhase Phase;internal Float3 Terminal;}
        readonly Dictionary<string,PodRide> pods=new Dictionary<string,PodRide>(StringComparer.Ordinal);readonly HashSet<string> usedPods=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,R24LanPodState> podStates=new Dictionary<string,R24LanPodState>(StringComparer.Ordinal);
        IR24LanPodWorld podWorld;long podSequence;
        public int PodLaunches{get;private set;}public int PodLandings{get;private set;}public string LastPodRejection{get;private set;}
        public void BindPodWorld(IR24LanPodWorld world){Check();if(State!=R24LanLobbyState.Hosting||world==null||podWorld!=null)throw new InvalidOperationException("One host pod world required");podWorld=world;}
        void ClearPods(){ClearVehicles();ClearWeb();pods.Clear();usedPods.Clear();podStates.Clear();foreach(var p in peers.Values)p.Pods.Clear();}
        bool PodCarried(string actor){PodRide ride;return pods.TryGetValue(actor,out ride)&&ride.Phase==R24LanPodPhase.Flying;}
        public bool TryReadPod(string actor,out R24LanPodState state){Check();state=null;return actor!=null&&podStates.TryGetValue(actor,out state);}
        public static string RideKey(string actor,string hostActor){return actor==hostActor?"player":actor;}
        bool RejectPod(string why){LastPodRejection=why;return false;}
        public bool RequestPodLaunch(string podId)
        {
            Check();if(podId==null||podId.Length<1||podId.Length>64)return false;foreach(char c in podId)if(c<33||c>126)return false;
            if(State==R24LanLobbyState.Hosting)return ApplyPodLaunch(OwnActor,podId);
            return State==R24LanLobbyState.Connected&&QueueGameCommand(PodLaunchFeature,Encoding.ASCII.GetBytes(podId));
        }
        public bool RequestPodEject(){Check();if(State==R24LanLobbyState.Hosting)return ApplyPodEject(OwnActor);return State==R24LanLobbyState.Connected&&QueueGameCommand(PodEjectFeature,new byte[0]);}
        internal bool ApplyPodLaunch(string actor,string podId)
        {
            LastPodRejection=null;if(!CombatLive||podWorld==null||roundRules.Mode!="revive"||roundRules.Teams<=0)return RejectPod("source-revive-team-match");
            var member=sessions.Get(actor);var health=member==null?null:hostHealth.Read(member.CanonicalActorId);
            if(member==null||!member.Loaded||!member.HasPosition||member.Life!=SessionLife.Alive||health==null||!health.Connected||health.Life!=LifeState.Alive)return RejectPod("living-member");
            if(pods.ContainsKey(actor)||usedPods.Contains(podId))return RejectPod("one-ride-unused-pod");
            R24LanRawTrackingSample raw;if(!TryReadRawTracking(actor,clock.Now,out raw))return RejectPod("authenticated-root");
            CapsulePlan plan;double started=RoundElapsed;
            if(!podWorld.TryPlan(RideKey(actor,OwnActor),member.Team,podId,raw.Pose.Root.Position,started,out plan)||!PlanValid(plan,podId,member.Team))return RejectPod("host-plan");
            var ride=new PodRide{Plan=plan,Started=started,Phase=R24LanPodPhase.Flying,Terminal=plan.Start};
            if(!RelocateForPod(actor,plan.Start))return RejectPod("relocation");
            pods[actor]=ride;usedPods.Add(podId);PodLaunches++;PublishPod(actor,ride);return true;
        }
        // net_session1000 _pod_launch_r12 plan bounds: finite, 1.5..120 s, <=35 m/s.
        static bool PlanValid(CapsulePlan p,string podId,int team)
        {
            if(p==null||p.PodId!=podId||p.Team!=team||p.Slot<0||p.Slot>=12||Double.IsNaN(p.Duration)||p.Duration<1.5||p.Duration>120)return false;
            foreach(var v in new[]{p.Start,p.Clear,p.Cruise,p.End,p.ExitTarget,p.RearDirection})if(!v.IsFinite||Math.Abs(v.X)>100000||Math.Abs(v.Y)>100000||Math.Abs(v.Z)>100000)return false;
            double length=Math.Sqrt(Distance2(p.Start,p.Clear))+Math.Sqrt(Distance2(p.Clear,p.Cruise))+Math.Sqrt(Distance2(p.Cruise,p.End));return length/p.Duration<=35.01;
        }
        internal bool ApplyPodEject(string actor)
        {
            PodRide ride;if(!pods.TryGetValue(actor,out ride)||ride.Phase!=R24LanPodPhase.Flying)return RejectPod("flying-ride");
            var at=ride.Plan.Position(Math.Max(0,RoundElapsed-ride.Started));var rear=ride.Plan.RearDirection;var exit=new Float3(at.X+rear.X*2,at.Y+rear.Y*2,at.Z+rear.Z*2);
            return FinishPod(actor,ride,R24LanPodPhase.Released,exit);
        }
        bool FinishPod(string actor,PodRide ride,R24LanPodPhase phase,Float3 at)
        {
            ride.Phase=phase;ride.Terminal=at;var member=sessions.Get(actor);
            // Departed/eliminated riders still leave the host path; living riders are re-placed there.
            if(member!=null&&member.Loaded&&member.HasPosition&&(member.Life==SessionLife.Alive||member.Life==SessionLife.Eliminated))RelocateForPod(actor,at);
            if(phase==R24LanPodPhase.Landed)PodLandings++;PublishPod(actor,ride);return true;
        }
        bool RelocateForPod(string actor,Float3 at)
        {
            var member=sessions.Get(actor);if(member==null)return false;long id=checked(++tick);
            var context=new CommandContext(SessionId,OwnActor,"pod-transition/"+id.ToString(System.Globalization.CultureInfo.InvariantCulture),id,Epoch,true);
            return sessions.RelocateRoot(context,actor,member.SpawnRevision,at).Accepted;
        }
        void TickPods()
        {
            if(State!=R24LanLobbyState.Hosting||pods.Count==0)return;var done=new List<KeyValuePair<string,PodRide>>();
            foreach(var pair in pods)if(pair.Value.Phase==R24LanPodPhase.Flying)done.Add(pair);
            foreach(var pair in done){
                var member=sessions==null?null:sessions.Get(pair.Key);var h=member==null||hostHealth==null?null:hostHealth.Read(member.CanonicalActorId);
                if(member==null||h==null||h.Life!=LifeState.Alive||!CombatLive){ApplyPodEject(pair.Key);continue;}
                if(RoundElapsed>=pair.Value.Started+pair.Value.Plan.Duration)FinishPod(pair.Key,pair.Value,R24LanPodPhase.Landed,pair.Value.Plan.ExitTarget);
            }
        }
        void PublishPod(string actor,PodRide ride)
        {
            var state=new R24LanPodState(actor,ride.Plan,ride.Started,ride.Phase,ride.Terminal,checked(++podSequence));podStates[actor]=state;var frame=Packet(PacketKind.Snapshot,PodStateFeature,OwnActor,EncodePod(state),state.Sequence);
            foreach(var p in peers.Values)if(p.Admitted)p.Pods[actor]=frame;
        }
        static byte[] EncodePod(R24LanPodState s)
        {
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Utf8)){var p=s.Plan;w.Write((byte)1);Write(w,s.Actor,64);Write(w,p.PodId,64);w.Write(p.Team);w.Write(p.Slot);foreach(var v in new[]{p.Start,p.Clear,p.Cruise,p.End,p.ExitTarget,p.RearDirection,s.Terminal}){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);}w.Write(p.Duration);w.Write(s.Started);w.Write((byte)s.Phase);return m.ToArray();}
        }
        void ReadPod(Packet packet)
        {
            if(packet.Sequence<1||packet.BaseSequence!=-1){Reject(serverPeer);return;}R24LanPodState state;
            try{using(var m=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(m,Utf8)){
                if(r.ReadByte()!=1)throw new InvalidDataException();string actor=Read(r,64),pod=Read(r,64);int team=r.ReadInt32(),slot=r.ReadInt32();var v=new Float3[7];for(int i=0;i<7;i++)v[i]=new Float3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());double duration=r.ReadDouble(),started=r.ReadDouble();byte phase=r.ReadByte();
                if(m.Position!=m.Length||phase>2||Double.IsNaN(started)||Double.IsInfinity(started)||started<0)throw new InvalidDataException();var plan=new CapsulePlan(pod,team,slot,v[0],v[1],v[2],v[3],v[4],v[5],duration);if(!PlanValid(plan,pod,team)||!v[6].IsFinite)throw new InvalidDataException();
                state=new R24LanPodState(actor,plan,started,(R24LanPodPhase)phase,v[6],packet.Sequence);}}
            catch(InvalidDataException){Reject(serverPeer);return;}catch(IOException){Reject(serverPeer);return;}catch(ArgumentException){Reject(serverPeer);return;}
            R24LanPodState prior;if(!HasMember(state.Actor)||podStates.TryGetValue(state.Actor,out prior)&&prior.Sequence>=state.Sequence)return;podStates[state.Actor]=state;
        }
        void FlushPods(string connection,Peer peer)
        {if(peer.Pods.Count==0)return;string sent=null;foreach(var item in peer.Pods){if(!Send(connection,item.Value))return;sent=item.Key;break;}if(sent!=null)peer.Pods.Remove(sent);}
    }
}
