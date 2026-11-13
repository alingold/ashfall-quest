using System;using System.Collections.Generic;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        const string Presence="playable.presence/1";
        readonly Dictionary<string,R24LanPoseSnapshot> poses=new Dictionary<string,R24LanPoseSnapshot>(StringComparer.Ordinal);
        readonly string[] retiredPoses=new string[Capacity];R24LanPoseSnapshot localPresence;long localPoseSequence,lastLocalSent;double nextPresence;
        public int RejectedPoses{get;private set;}
        public bool TryReadPresence(string actor,out R24LanPoseSnapshot snapshot){Check();return poses.TryGetValue(actor,out snapshot);}
        public bool PublishPresence(R24LanPose pose)
        {
            Check();if(State!=R24LanLobbyState.Hosting&&State!=R24LanLobbyState.Connected)return false;
            if(pose==null||!pose.Valid){RejectedPoses++;return false;}
            long sequence=checked(++localPoseSequence);var frame=Packet(PacketKind.Delta,Presence,OwnActor,pose.Encode(),sequence);
            localPresence=new R24LanPoseSnapshot(OwnActor,sequence,clock.Now,pose,frame);if(State==R24LanLobbyState.Hosting)poses[OwnActor]=localPresence;return true;
        }
        bool HasMember(string actor){foreach(var m in members)if(m.Actor==actor)return true;return false;}
        bool StorePresence(Packet packet)
        {
            R24LanPoseSnapshot old;if(packet.Sequence<=0||packet.BaseSequence!=-1){RejectedPoses++;return false;}
            if(poses.TryGetValue(packet.ActorId,out old)&&packet.Sequence<=old.Sequence)return false;
            R24LanPose pose;if(!R24LanPose.TryDecode(packet.Payload,out pose)){RejectedPoses++;return false;}
            poses[packet.ActorId]=new R24LanPoseSnapshot(packet.ActorId,packet.Sequence,clock.Now,pose,Packet(PacketKind.Delta,Presence,packet.ActorId,packet.Payload,packet.Sequence));return true;
        }
        void ReadClientPresence(Peer peer,Packet packet)
        {
            if(packet.Sequence<=peer.LastPose)return;
            // Presentation-only traffic is bounded independently of reliable
            // session membership. Excess updates are dropped, never queued.
            if(clock.Now<peer.NextPose)return;
            if(StorePresence(packet)){peer.LastPose=packet.Sequence;peer.NextPose=clock.Now+1.0/45;}
        }
        void ReadServerPresence(Packet packet)
        {if(packet.ActorId==OwnActor||!HasMember(packet.ActorId))return;StorePresence(packet);}
        bool SendPresence(string peer,byte[] frame)
        {try{transport.Send(peer,frame,false);return true;}catch(InvalidOperationException){return false;}}
        void TickPresence()
        {
            if(State!=R24LanLobbyState.Hosting&&State!=R24LanLobbyState.Connected){poses.Clear();localPresence=null;return;}
            if(clock.Now<nextPresence)return;nextPresence=clock.Now+.04;
            if(State==R24LanLobbyState.Connected){if(localPresence!=null&&localPresence.Sequence>lastLocalSent&&SendPresence(serverPeer,localPresence.Frame))lastLocalSent=localPresence.Sequence;return;}
            foreach(var pair in peers)if(pair.Value.Admitted)foreach(var pose in poses.Values)
            {
                var peer=pair.Value;long sent;if(pose.Actor==peer.Actor||clock.Now-pose.ReceivedAt>1||peer.SentPoses.TryGetValue(pose.Actor,out sent)&&sent>=pose.Sequence)continue;
                if(SendPresence(pair.Key,pose.Frame))peer.SentPoses[pose.Actor]=pose.Sequence;
            }
        }
        void RemovePresence(string actor){RemoveWeb(actor);RemoveSpawn(actor);ForgetRawTracking(actor,true);poses.Remove(actor);foreach(var p in peers.Values)p.SentPoses.Remove(actor);}
        void PrunePresence(){int n=0;foreach(string actor in poses.Keys)if(!HasMember(actor))retiredPoses[n++]=actor;for(int i=0;i<n;i++){poses.Remove(retiredPoses[i]);retiredPoses[i]=null;}}
    }
}
