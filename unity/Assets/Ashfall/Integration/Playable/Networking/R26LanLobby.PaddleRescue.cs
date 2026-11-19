using System;
using System.Collections.Generic;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        readonly Dictionary<string,double> rescueCues=new Dictionary<string,double>(StringComparer.Ordinal);
        readonly Dictionary<string,R24LanRawTrackingSample> paddleSamples=new Dictionary<string,R24LanRawTrackingSample>(StringComparer.Ordinal);
        void SamplePaddleCharge(string actor,R24LanRawTrackingSample sample)
        {
            if(sample==null||!CombatLive||roundRules.Mode!="revive"||roundRules.Teams==0)return;
            var member=sessions.Get(actor);if(member==null)return;
            R24LanRawTrackingSample prior;
            bool equipped=(sample.Pose.Flags&15)==15;
            if(paddleSamples.TryGetValue(actor,out prior)&&(!equipped||sample.Time-prior.Time>.5||sample.Pose.SpawnRevision!=prior.Pose.SpawnRevision))CancelPaddleCharge(actor,member.CanonicalActorId);
            if(!equipped)return;
            var health=hostHealth.Read(member.CanonicalActorId);if(health==null||!health.Connected||health.Life!=LifeState.Alive)return;
            paddleSamples[actor]=sample;
            var result=AsMember(member.CanonicalActorId,delegate(CommandContext c){return hostHealth.SamplePaddles(c);});
            if(result.Accepted&&hostHealth.Read(member.CanonicalActorId).ReadyUntil>clock.Now)TryPaddleTouch(actor,sample,member.Team);
        }
        void CancelPaddleCharge(string actor,string canonical)
        {
            paddleSamples.Remove(actor);
            AsMember(canonical,delegate(CommandContext c){return hostHealth.CancelRevive(c);});
        }
        void RetirePaddleSamples()
        {
            if(paddleSamples.Count==0)return;
            var retired=new List<string>();
            foreach(var pair in paddleSamples){
                R24LanRawTrackingSample sample;
                if(!CombatLive||!RescueSample(pair.Key,out sample)||(sample.Pose.Flags&15)!=15)retired.Add(pair.Key);
            }
            foreach(var actor in retired){
                var member=sessions.Get(actor);
                if(member!=null)CancelPaddleCharge(actor,member.CanonicalActorId);else paddleSamples.Remove(actor);
            }
        }
        // The model sits 10 cm along the corrected grip forward (raw controller -Y).
        static Float3 Electrode(R24LanTransform controller)
        {
            var offset=Rotate(controller.Rotation,new Float3(0,-.1f,0));var at=controller.Position;
            return new Float3(at.X+offset.X,at.Y+offset.Y,at.Z+offset.Z);
        }
        void TryPaddleTouch(string actor,R24LanRawTrackingSample sample,int team)
        {
            if(roundRules.Teams==0||(sample.Pose.Flags&7)!=7)return;
            var left=Electrode(sample.Pose.Left);var right=Electrode(sample.Pose.Right);
            // Preselect by distance using only current accepted poses. The Health owner
            // checks life, generation, equipment, the same geometry and occlusion at commit.
            string nearest=null;double best=double.PositiveInfinity;
            foreach(var m in members){
                R24LanHealthState health;R24LanRawTrackingSample patient;
                if(m.Actor==actor||(!roundRules.EnemyRevive&&m.Team!=team)||!healthStates.TryGetValue(m.Actor,out health)||health.Life!=LifeState.Downed||!RescueSample(m.Actor,out patient))continue;
                var head=patient.Pose.Head.Position;var chest=new Float3(head.X,head.Y-.6f,head.Z);
                double distance=Math.Min(ContactDistance(left,chest),ContactDistance(right,chest));
                if(distance<best){best=distance;nearest=m.Actor;}
            }
            if(nearest!=null)ApplyRevive(actor,nearest);
        }
        double ContactDistance(Float3 pad,Float3 chest)
        {
            if(!HealthService.TouchesTorso(pad,chest)||rawWorldClear==null)return double.PositiveInfinity;
            var point=HealthService.TorsoPoint(pad,chest);
            return rawWorldClear(pad,point)?Distance2(pad,point):double.PositiveInfinity;
        }
        internal double RescueCue(string actor)
        {
            double at;return rescueCues.TryGetValue(actor,out at)&&EstimatedHostTime-at<=1.5?at:-1;
        }
        bool ShowingPaddles(string actor)
        {
            R24LanRawTrackingSample sample;
            return rescueHolds.ContainsKey(actor)||RescueCue(actor)>=0||(RescueSample(actor,out sample)&&sample.Pose.Paddles);
        }
        void CompleteRescue(string actor)
        {
            Revives++;scoreDirty=true;SyncHostHealth();rescueCues[actor]=clock.Now;
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream,Utf8)){
                w.Write((byte)3);Write(w,MapManifestHash,64);Write(w,actor,64);w.Write(clock.Now);
                var frame=Packet(PacketKind.Snapshot,HealthStateFeature,OwnActor,stream.ToArray(),checked(++healthStateSequence));
                foreach(var peer in peers.Values)if(peer.Admitted)peer.Health["/rescue/"+actor]=frame;
            }
        }
        void ReadRescueCue(Packet packet)
        {
            try{using(var stream=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(stream,Utf8)){
                r.ReadByte();string map=Read(r,64),actor=Read(r,64);double at=r.ReadDouble();
                if(stream.Position!=stream.Length||!Finite(at)||at<0)throw new InvalidDataException();
                if(map!=MapManifestHash||!HasMember(actor)||at>EstimatedHostTime+.5||EstimatedHostTime-at>1.5)return;
                double prior;if(!rescueCues.TryGetValue(actor,out prior)||at>prior)rescueCues[actor]=at;
            }}catch(InvalidDataException){Reject(serverPeer);}catch(IOException){Reject(serverPeer);}catch(ArgumentException){Reject(serverPeer);}
        }
    }
}
