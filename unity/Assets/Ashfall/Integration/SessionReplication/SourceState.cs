using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Player;
using Ashfall.Integration.Sessions;
namespace Ashfall.Integration.SessionReplication
{
    // Positions are Unity SI. Head is tracking-local, feet is world-space.
    // No rotation/hand availability can be inferred from InputFrame.
    public sealed class SourceMemberState
    {
        public readonly string Actor,Canonical;
        public readonly int Team,SpawnRevision,Deaths;
        public readonly bool Loaded,Waiting,HasFeet,HasHealth,HasHead;
        public readonly SessionLife SessionLife;
        public readonly LifeState HealthLife;
        public readonly long HealthRevision,LifeGeneration,ConnectionGeneration,PoseGeneration,PoseTick;
        public readonly float Health,Shield;
        public readonly Float3 Feet,Head;
        public bool HasLeftHand { get { return false; } }
        public bool HasRightHand { get { return false; } }
        internal SourceMemberState(string actor,string canonical,int team,int spawn,int deaths,bool loaded,bool waiting,bool feet,
            SessionLife life,Float3 position,bool health,LifeState healthLife,long revision,long lg,long cg,float hp,float shield,
            bool head,long pg,long tick,Float3 headPosition)
        { Actor=actor;Canonical=canonical;Team=team;SpawnRevision=spawn;Deaths=deaths;Loaded=loaded;Waiting=waiting;HasFeet=feet;
          SessionLife=life;Feet=position;HasHealth=health;HealthLife=healthLife;HealthRevision=revision;LifeGeneration=lg;
          ConnectionGeneration=cg;Health=hp;Shield=shield;HasHead=head;PoseGeneration=pg;PoseTick=tick;Head=headPosition; }
    }
    public sealed class SourceStateView
    {
        public readonly SessionStage Stage;
        public readonly long LifecycleSequence;
        private readonly SourceMemberState[] members;
        public SourceMemberState[] Members {get {return (SourceMemberState[])members.Clone();}}
        internal SourceStateView(SessionStage stage,long lifecycle,SourceMemberState[] members)
        {Stage=stage;LifecycleSequence=lifecycle;this.members=(SourceMemberState[])members.Clone();}
    }
    // Host-owned binding attests which real PlayerFrameOwner belongs to a canonical
    // actor. It is never populated from a network payload. Root must supply it.
    public sealed class SessionSourceObservations
    {
        private sealed class PoseBinding
        {internal PlayerFrameOwner Owner;internal long Generation,Life,Connection;internal int SpawnRevision;internal string Canonical;internal object SessionConnection;}
        private readonly SessionNetworkCoordinator sessions;
        private readonly HealthService health;
        private readonly Dictionary<string,PoseBinding> poses=new Dictionary<string,PoseBinding>(StringComparer.Ordinal);
        private readonly int thread=System.Threading.Thread.CurrentThread.ManagedThreadId;
        private byte[] lastImage;
        public long Sequence {get;private set;}
        public SessionSourceObservations(SessionNetworkCoordinator sessions,HealthService health)
        {if(sessions==null||health==null)throw new ArgumentNullException("sources");this.sessions=sessions;this.health=health;Sequence=-1;}
        public RuleResult BindPlayer(CommandContext host,string actor,PlayerFrameOwner owner)
        {
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=thread||host.ActorId!="host"||!health.Authorize(host).Accepted||host.AuthorityEpoch!=sessions.Epoch)
                return RuleResult.Reject("authority");
            MemberSnapshot m=sessions.Get(actor);HealthSnapshot h=m==null?null:health.Read(m.CanonicalActorId);
            object incarnation=m==null?null:sessions.GetConnectionIdentity(actor);
            if(m==null||incarnation==null||h==null||!h.Connected||owner==null||!owner.Valid||poses.Count>=64&&!poses.ContainsKey(actor))return RuleResult.Reject("source");
            poses[actor]=new PoseBinding {Owner=owner,Generation=owner.Generation,Canonical=m.CanonicalActorId,Life=h.LifeGeneration,Connection=h.ConnectionGeneration,SpawnRevision=m.SpawnRevision,SessionConnection=incarnation};
            return RuleResult.Accept();
        }
        public RuleResult Capture(CommandContext host,out SourceStateView view)
        {
            view=null;
            if(System.Threading.Thread.CurrentThread.ManagedThreadId!=thread||host.ActorId!="host"||!health.Authorize(host).Accepted||host.AuthorityEpoch!=sessions.Epoch)return RuleResult.Reject("authority");
            MemberSnapshot[] members=sessions.CaptureMembers();if(members.Length>64)return RuleResult.Reject("roster-capacity");
            var result=new SourceMemberState[members.Length];var canonical=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<members.Length;i++)
            {
                MemberSnapshot m=members[i];if(!canonical.Add(m.CanonicalActorId))return RuleResult.Reject("canonical-alias");
                HealthSnapshot h=health.Read(m.CanonicalActorId);bool have=h!=null&&h.Connected;
                if(have&&(h.Id!=m.CanonicalActorId||h.Team!=m.Team))return RuleResult.Reject("health-identity");
                PoseBinding p;InputFrame frame=new InputFrame();bool head=false;
                if(have&&m.Loaded&&poses.TryGetValue(m.ActorId,out p)&&Object.ReferenceEquals(p.SessionConnection,sessions.GetConnectionIdentity(m.ActorId))&&p.Canonical==m.CanonicalActorId&&p.SpawnRevision==m.SpawnRevision&&p.Life==h.LifeGeneration&&p.Connection==h.ConnectionGeneration&&p.Owner.Generation==p.Generation&&p.Owner.Valid)
                {frame=p.Owner.Current;head=frame.Tick==host.Tick&&frame.Sequence>0&&frame.Tracked&&!frame.Paused;}
                result[i]=new SourceMemberState(m.ActorId,m.CanonicalActorId,m.Team,m.SpawnRevision,m.Deaths,m.Loaded,m.WaitingRound,m.HasPosition,m.Life,m.Position,
                    have,have?h.Life:LifeState.Spectator,have?h.Revision:0,have?h.LifeGeneration:0,have?h.ConnectionGeneration:0,have?h.Health:0,have?h.Shield:0,
                    head,head?poses[m.ActorId].Generation:0,head?frame.Tick:0,head?frame.HeadPosition:new Float3());
            }
            // Retire bindings absent from actual membership; bound memory, no replay ledger reset.
            var stale=new List<string>();foreach(string actor in poses.Keys)if(sessions.Get(actor)==null)stale.Add(actor);foreach(string actor in stale)poses.Remove(actor);
            var next=new SourceStateView(sessions.Stage,sessions.LifecycleSequence,result);if(!SourceStateCodec.Validate(next))return RuleResult.Reject("source-data");
            byte[] image=SourceStateCodec.Encode(next,null);
            if(lastImage==null||Convert.ToBase64String(lastImage)!=Convert.ToBase64String(image))
            {if(Sequence==Int64.MaxValue)return RuleResult.Reject("sequence-capacity");Sequence++;lastImage=image;}
            view=next;return RuleResult.Accept();
        }
    }
}
