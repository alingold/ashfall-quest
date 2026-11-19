using System;
using System.IO;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Modules.Networking;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.Playable.Traversal;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        const string WebFeature="playable.web.state/1";
        readonly Dictionary<string,R25WebState> webStates=new Dictionary<string,R25WebState>(StringComparer.Ordinal);
        readonly Dictionary<string,Dictionary<string,byte[]>> webPending=new Dictionary<string,Dictionary<string,byte[]>>(StringComparer.Ordinal);
        R25WebState sentWeb;byte[] clientWeb;long webSequence;
        readonly Queue<byte[]> grappleShotFrames=new Queue<byte[]>();
        public bool TryReadWeb(string actor,out R25WebState state){Check();return webStates.TryGetValue(actor,out state);}
        public bool PublishWeb(R25WebState state,int shotHand=-1)
        {
            Check();if(state==null||!state.Valid||State!=R24LanLobbyState.Hosting&&State!=R24LanLobbyState.Connected)return false;
            if(shotHand< -1||shotHand>5)return false;
            if(shotHand>=0){
                if(State==R24LanLobbyState.Hosting){if(shotHand==2){EndPull(OwnActor);return true;}if(shotHand==3){ReleaseWeb(OwnActor,state);return true;}if(TryStartPull(OwnActor,shotHand))return true;}
                else if(grappleShotFrames.Count<64){byte[] bytes=new byte[26];Array.Copy(state.Encode(),bytes,25);bytes[25]=(byte)shotHand;grappleShotFrames.Enqueue(Packet(PacketKind.Command,WebFeature,OwnActor,bytes,++webSequence));sentWeb=state;return true;}else return false;
            }
            if(state.Same(sentWeb))return true;
            if(State==R24LanLobbyState.Hosting){if(!AcceptWeb(OwnActor,state))return false;}
            else clientWeb=Packet(PacketKind.Command,WebFeature,OwnActor,state.Encode(),++webSequence);
            sentWeb=state;return true;
        }
        bool AcceptWeb(string actor,R25WebState state)
        {
            var rules=SelectedMap==null?warmupRules:SelectedMap.SourceRules;
            R24LanHealthState health;
            if(pulls.ContainsKey(actor))return false;
            foreach(var row in pulls.Values)if(row.Shooter==actor)return false;
            if(rules==null||!rules.GrappleGun||healthStates.TryGetValue(actor,out health)&&health.Life!=LifeState.Alive)return false;
            if(state.LeftAttached||state.RightAttached){R24LanRawTrackingSample pose;if(!TryReadRawTracking(actor,clock.Now,out pose)||!state.Near(pose.Pose.Head.Position))return false;}
            R25WebState prior;if(webStates.TryGetValue(actor,out prior)&&state.Same(prior))return true;
            if(!state.LeftAttached&&!state.RightAttached){RemoveWeb(actor);return true;}
            webStates[actor]=state;QueueWeb(actor,state);return true;
        }
        byte[] WebFrame(string actor,R25WebState state)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){Write(w,actor,64);w.Write(state.Encode());return Packet(PacketKind.Snapshot,WebFeature,OwnActor,s.ToArray(),++webSequence);}
        }
        void PendingWeb(string peer,string actor,byte[] frame){Dictionary<string,byte[]> rows;if(!webPending.TryGetValue(peer,out rows)){rows=new Dictionary<string,byte[]>(StringComparer.Ordinal);webPending[peer]=rows;}rows[actor]=frame;}
        void QueueWeb(string actor,R25WebState state){byte[] frame=WebFrame(actor,state);foreach(var pair in peers)if(pair.Value.Admitted)PendingWeb(pair.Key,actor,frame);}
        void ReplayWeb(string peer){foreach(var row in webStates)PendingWeb(peer,row.Key,WebFrame(row.Key,row.Value));}
        void ReceiveWeb(Packet p,bool host)
        {
            if(host){
                if(p.BaseSequence!=-1||p.Sequence<1)return;
                long prior;if(grappleShots.TryGetValue(p.ActorId,out prior)&&p.Sequence<=prior)return;
                R25WebState state;byte[] bytes=p.Payload;int hand=-1;
                if(bytes.Length==26){hand=bytes[25];if(hand>5)return;bytes=new byte[25];Array.Copy(p.Payload,bytes,25);}
                if(!R25WebState.Decode(bytes,out state))return;
                grappleShots[p.ActorId]=p.Sequence;
                if(hand==2){EndPull(p.ActorId);return;}if(hand==3){ReleaseWeb(p.ActorId,state);return;}
                if(hand<0||!TryStartPull(p.ActorId,hand))AcceptWeb(p.ActorId,state);return;
            }
            try{using(var s=new MemoryStream(p.Payload))using(var r=new BinaryReader(s,Utf8)){string actor=Read(r,64);R25WebState state;if(R25WebState.Decode(r.ReadBytes((int)(s.Length-s.Position)),out state)&&HasMember(actor)){if(state.LeftAttached||state.RightAttached)webStates[actor]=state;else webStates.Remove(actor);}}}
            catch(IOException){}catch(ArgumentException){}
        }
        void TickWeb()
        {
            if(State==R24LanLobbyState.Connected){while(grappleShotFrames.Count>0){if(!Send(serverPeer,grappleShotFrames.Peek()))return;grappleShotFrames.Dequeue();}if(clientWeb!=null&&Send(serverPeer,clientWeb))clientWeb=null;return;}
            if(State!=R24LanLobbyState.Hosting)return;
            foreach(var actor in new List<string>(webStates.Keys)){
                R24LanHealthState health;
            var rules=SelectedMap==null?warmupRules:SelectedMap.SourceRules;
                if(!HasMember(actor)||rules==null||!rules.GrappleGun||healthStates.TryGetValue(actor,out health)&&health.Life!=LifeState.Alive)RemoveWeb(actor);
            }
            foreach(var peer in new List<string>(webPending.Keys)){if(!peers.ContainsKey(peer)){webPending.Remove(peer);continue;}var rows=webPending[peer];foreach(var actor in new List<string>(rows.Keys))if(Send(peer,rows[actor]))rows.Remove(actor);if(rows.Count==0)webPending.Remove(peer);}
        }
        void ReleaseWeb(string actor,R25WebState state)
        {
            ReleaseShooterPull(actor);
            // A guest coalesces this snapshot with its release command.
            if(!state.LeftAttached&&!state.RightAttached){RemoveWeb(actor);if(actor==OwnActor)sentWeb=state;}
        }
        void RemoveWeb(string actor){if(!webStates.ContainsKey(actor))return;QueueWeb(actor,new R25WebState(false,new Float3(),false,new Float3()));webStates.Remove(actor);}
        void ClearWeb(){ClearPulls();grappleShotFrames.Clear();webStates.Clear();webPending.Clear();clientWeb=null;sentWeb=null;}
    }
}
