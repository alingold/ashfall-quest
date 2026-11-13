using System;
using Ashfall.Core;
using Ashfall.Modules.Player;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;
using Ashfall.Modules.Construction;
using Ashfall.Integration.SessionReplication;
using Ashfall.Integration.ConstructionRuntime;
using Ashfall.Integration.LifecycleTransactions;
namespace Ashfall.Integration.ConstructionTransactions
{
    // Root owner must supply current world-space pose and its monotonic revision.
    // This explicit missing native binding cannot be replaced by client pose fields.
    public interface IConstructionRootPose
    {
        long Generation {get;}
        bool IsCurrent(string session,string actor,long epoch,PlayerFrameOwner frames);
        bool TryRead(InputFrame frame,string hand,out BuilderState pose);
    }
    // One instance per canonical actor/connection/root/frame owner. Reconnect replaces
    // the adapter; queued leases retain the old reference. No auto-remap of old callbacks.
    public sealed class CanonicalBuilder:IConstructionHostState
    {
        readonly Session session;readonly HealthService health;readonly IAuthenticatedPeerPort peers;
        readonly PlayerFrameOwner frames;readonly IConstructionRootPose root;readonly Func<long> ticks;
        readonly string connection,actor,content;readonly long generation,epoch;readonly string authority;
        readonly IAuthenticatedLifecycleIdentity lifecycle;
        readonly object incarnation;readonly long playerGeneration;readonly string admittedMember,hostActor;
        public CanonicalBuilder(Session session,HealthService health,IAuthenticatedPeerPort peers,PlayerFrameOwner frames,
            IConstructionRootPose root,Func<long> tick,string connection,string canonicalActor,string content,long connectionGeneration,IAuthenticatedLifecycleIdentity lifecycle,string hostActor=null)
        {
            if(session==null||health==null||peers==null||frames==null||root==null||tick==null||lifecycle==null||String.IsNullOrEmpty(connection)||String.IsNullOrEmpty(canonicalActor)||String.IsNullOrEmpty(content)||connectionGeneration<1)
                throw new ArgumentException("canonical construction binding");
            this.session=session;this.health=health;this.peers=peers;this.frames=frames;this.root=root;ticks=tick;
            this.connection=connection;actor=canonicalActor;this.content=content;generation=connectionGeneration;epoch=session.Epoch;
            this.lifecycle=lifecycle;this.hostActor=hostActor??canonicalActor;
            playerGeneration=frames.Generation;
            AuthenticatedPeer initial;
            if(peers.TryResolve(connection,out initial)&&initial!=null)admittedMember=initial.Actor;
            incarnation=admittedMember==null?null:session.GetConnectionIdentity(admittedMember);
            authority=BodyIdentity.Part(session.Id)+BodyIdentity.Part(connection)+BodyIdentity.Part(generation.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        public long Tick {get{return ticks();}}
        public bool TryRead(string canonicalActor,out HostBuilderStamp stamp)
        {
            stamp=null;long inputGeneration=frames.Generation;AuthenticatedPeer peer;
            if(Tick<0||frames.Generation!=playerGeneration||incarnation==null||canonicalActor!=actor||session.Epoch!=epoch||session.Stage!=SessionStage.Match||!peers.TryResolve(connection,out peer)||peer==null||
                peer.Actor!=admittedMember||!Object.ReferenceEquals(incarnation,session.GetConnectionIdentity(admittedMember))||
                peer.Session!=session.Id||peer.Content!=content||peer.Epoch!=epoch||peer.Generation!=generation)return false;
            var member=session.Get(peer.Actor);var life=health.Read(actor);var frame=frames.Current;
            if(member==null||member.CanonicalActorId!=actor||member.Life!=SessionLife.Alive||member.WaitingRound||!member.Loaded||
                life==null||!life.Connected||life.Life!=LifeState.Alive||life.Team!=member.Team||
                !lifecycle.Validate(new CommandContext(session.Id,hostActor,"construction-observe",Tick,epoch,true),admittedMember,actor,life.ConnectionGeneration)||
                !root.IsCurrent(session.Id,actor,epoch,frames)||
                frames.Generation!=inputGeneration||!frames.Valid||frame.Paused||!frame.Tracked||frame.Tick!=Tick||root.Generation<1)return false;
            stamp=new HostBuilderStamp(session.Id,epoch,actor,authority,life.LifeGeneration,life.ConnectionGeneration,root.Generation,frame.Sequence,true,inputGeneration);
            return true;
        }
        public bool Claim(RuntimeHost host,string hand,long expiry,out ConstructionLease lease)
        {
            lease=null;if(host==null||(hand!="left"&&hand!="right")||expiry<Tick||expiry-Tick>20)return false;
            HostBuilderStamp before,after;BuilderState pose;
            if(!TryRead(actor,out before)||!root.TryRead(frames.Current,hand,out pose)||pose==null||!TryRead(actor,out after)||
                before.Life!=after.Life||before.Connection!=after.Connection||before.RootRevision!=after.RootRevision||before.PoseRevision!=after.PoseRevision||before.InputGeneration!=after.InputGeneration)return false;
            return host.Claim(actor,hand,pose,expiry,out lease);
        }
    }
}
