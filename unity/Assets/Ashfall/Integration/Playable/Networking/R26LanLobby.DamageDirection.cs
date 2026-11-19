using System;using System.Collections.Generic;using Ashfall.Core;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        readonly Queue<R26DamageDirectionReceipt> damageDirections=new Queue<R26DamageDirectionReceipt>();long damageDirectionReceived,damageDirectionSequence;readonly Dictionary<string,Queue<byte[]>> damageDirectionPending=new Dictionary<string,Queue<byte[]>>(StringComparer.Ordinal);
        public int DamageDirectionsQueued{get;private set;}public int DamageDirectionsSent{get;private set;}public int DamageDirectionsReceived{get;private set;}
        // Called after Health accepted the loss. Only the victim gets the source;
        // health authority and the shooter's layout-2 confirmation are unchanged.
        partial void PublishDamageDirection(string attacker,string victim,Float3? explicitSource)
        {
            var member=sessions.Get(victim);if(member==null)return;var health=hostHealth.Read(member.CanonicalActorId);if(health==null)return;
            Float3 source;R24LanRawTrackingSample pose;if(explicitSource.HasValue)source=explicitSource.Value;else if(Fresh(attacker,clock.Now,out pose))source=pose.Pose.Head.Position;else return;
            if(!source.IsFinite)return;var cue=new R26DamageDirectionReceipt(member.SpawnRevision,health.LifeGeneration,source,clock.Now);DamageDirectionsQueued++;
            if(victim==OwnActor){EnqueueDamageDirection(cue);return;}
            foreach(var peer in peers)if(peer.Value.Admitted&&peer.Value.Actor==victim){
                var frame=Packet(PacketKind.Snapshot,ScoreFeature,OwnActor,cue.Encode(),checked(++damageDirectionSequence));Queue<byte[]> q;
                if(!damageDirectionPending.TryGetValue(peer.Key,out q)){q=new Queue<byte[]>();damageDirectionPending[peer.Key]=q;}if(q.Count>=32)q.Dequeue();q.Enqueue(frame);return;
            }
        }
        void EnqueueDamageDirection(R26DamageDirectionReceipt cue){if(damageDirections.Count>=32)damageDirections.Dequeue();damageDirections.Enqueue(cue);}
        public bool TryDequeueDamageDirection(out R26DamageDirectionReceipt cue)
        {
            Check();cue=null;while(damageDirections.Count>0){var next=damageDirections.Dequeue();double age=EstimatedHostTime-next.Time;if(age< -.25||age>1)continue;cue=next;return true;}return false;
        }
        partial void ClearDamageDirection(){damageDirections.Clear();damageDirectionPending.Clear();damageDirectionReceived=0;}
        partial void FlushDamageDirection()
        {
            if(State!=R24LanLobbyState.Hosting||damageDirectionPending.Count==0)return;List<string> gone=null;
            foreach(var entry in damageDirectionPending){if(!peers.ContainsKey(entry.Key)){if(gone==null)gone=new List<string>();gone.Add(entry.Key);continue;}var q=entry.Value;int budget=8;while(q.Count>0&&budget-->0&&Send(entry.Key,q.Peek())){q.Dequeue();DamageDirectionsSent++;}}
            if(gone!=null)foreach(var connection in gone)damageDirectionPending.Remove(connection);
        }
        partial void ReadDamageDirection(Packet packet,ref bool handled)
        {
            if(packet.Payload==null||packet.Payload.Length==0||packet.Payload[0]!=R26DamageDirectionReceipt.Layout)return;handled=true;
            if(packet.Sequence<=damageDirectionReceived||packet.Sequence<1||packet.BaseSequence!=-1)return;R26DamageDirectionReceipt cue;
            if(!R26DamageDirectionReceipt.TryDecode(packet.Payload,out cue))return;double age=EstimatedHostTime-cue.Time;if(age< -.25||age>1)return;
            damageDirectionReceived=packet.Sequence;DamageDirectionsReceived++;EnqueueDamageDirection(cue);
        }
    }
}
