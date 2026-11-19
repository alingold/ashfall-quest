using System;using System.Collections.Generic;using System.IO;using Ashfall.Modules.HealthStates;using Ashfall.Modules.Networking;using Ashfall.Modules.Sessions;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        R26MatchEndResults finalResults;double nextMatchEndSend;
        public R26MatchEndResults MatchEndResults
        {
            get
            {
                if(SelectedMap==null||!SelectedMap.Results||finalResults==null)return null;
                // An unordered clock reply can beat the final reliable Health update.
                // Show a living winner the celebration once that exact revision has arrived.
                var row=finalResults.ForActor(OwnActor);R24LanHealthState health;
                if(State==R24LanLobbyState.Connected&&row!=null&&row.Outcome>0&&row.AliveHealthRevision>0&&
                    (!TryReadHealth(OwnActor,out health)||health.Revision<row.AliveHealthRevision))return null;
                return finalResults;
            }
        }
        public bool HostFinishMatchWithCelebration()
        {
            Check();if(State!=R24LanLobbyState.Hosting||SelectedMap==null||!SelectedMap.Active||SelectedMap.Cancelled||roundRules==null)return false;if(roundResults)return true;
            var receipt=CaptureMatchEnd();var result=sessions.Lifecycle(LifecycleContext(),LifecycleAction.EnterResults,null,null,false);if(!result.Accepted)return false;
            finalResults=receipt;roundResults=true;PublishMapState(true,false);PublishMatchEnd();PublishRoster();Dirty("LAN MATCH RESULTS");return true;
        }
        R26MatchEndResults CaptureMatchEnd()
        {
            var rows=new List<R26MatchEndRow>();var totals=new Dictionary<int,int>();
            foreach(var member in sessions.CaptureMembers()){
                CombatActor c;combat.TryGetValue(member.ActorId,out c);var h=hostHealth==null?null:hostHealth.Read(member.CanonicalActorId);
                int kills=c==null?0:c.Kills,deaths=h==null?0:h.Deaths,score=c==null?0:c.Score;
                rows.Add(new R26MatchEndRow(member.ActorId,member.Name,member.Team,kills,deaths,score,0,0,h!=null&&h.Life==LifeState.Alive?h.Revision:0));
                if(member.Team>=0){int prior;totals.TryGetValue(member.Team,out prior);int value=roundRules.Mode=="revive"?(h!=null&&h.Connected&&h.Life==LifeState.Alive?1:0):score;totals[member.Team]=prior+value;}
            }
            return R26MatchEndResults.Rank(clock.Now+R26MatchEndResults.Duration,roundRules.Teams>0,rows,totals);
        }
        static void WriteMatchEnd(BinaryWriter w,R26MatchEndResults result,string actor)
        {
            if(result==null)throw new InvalidOperationException("Final host results missing");w.Write(result.EndsAt);w.Write(result.Teams);w.Write((byte)1);
            var row=result.ForActor(actor);if(row==null)throw new InvalidOperationException("Results actor missing");{Write(w,row.Actor,64);w.Write(row.Team);w.Write(row.Kills);w.Write(row.Deaths);w.Write(row.Score);w.Write(row.Rank);w.Write(row.Outcome);w.Write(row.AliveHealthRevision);}
        }
        static R26MatchEndResults ReadMatchEnd(BinaryReader r)
        {
            double ends=r.ReadDouble();byte teams=r.ReadByte();int count=r.ReadByte();if(!Finite(ends)||ends<0||teams>1||count<1||count>Capacity)throw new InvalidDataException();
            var rows=new R26MatchEndRow[count];var actors=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<count;i++){string actor=Read(r,64),name=actor;int team=r.ReadInt32(),kills=r.ReadInt32(),deaths=r.ReadInt32(),score=r.ReadInt32(),rank=r.ReadInt32(),outcome=r.ReadInt32();long healthRevision=r.ReadInt64();if(!actors.Add(actor)||team< -1||team>=Capacity||kills<0||deaths<0||score<0||rank<1||rank>Capacity||outcome< -1||outcome>1||healthRevision<0)throw new InvalidDataException();rows[i]=new R26MatchEndRow(actor,name,team,kills,deaths,score,rank,outcome,healthRevision);}
            return new R26MatchEndResults(ends,teams==1,rows);
        }
        // Reuses the clock reply decoder; results additionally travel on reliable Send.
        // One complete personal receipt is
        // at most 124 bytes, including clock sample, even with a 64-byte actor ID.
        byte[] MatchEndClockFrame(double sent,string actor)
        {
            using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){w.Write(sent);w.Write(clock.Now);if(roundResults&&finalResults!=null&&finalResults.ForActor(actor)!=null)WriteMatchEnd(w,finalResults,actor);return Packet(PacketKind.Snapshot,ClockFeature,OwnActor,s.ToArray(),0);}
        }
        void PublishMatchEnd(){if(retainedScoreboard==null)RetainScoreboard(finalResults);if(clock.Now<nextMatchEndSend)return;nextMatchEndSend=clock.Now+.5;foreach(var peer in peers)if(peer.Value.Admitted)Send(peer.Key,MatchEndClockFrame(-1,peer.Value.Actor));}
        void ReadMatchEndClock(Packet packet)
        {
            if(packet.Epoch!=Epoch||packet.Payload.Length<16)return;
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){double sent=r.ReadDouble(),host=r.ReadDouble();if(!Finite(sent)||!Finite(host)||host<0)throw new InvalidDataException();R26MatchEndResults receipt=s.Length>16?ReadMatchEnd(r):null;if(s.Position!=s.Length||receipt!=null&&(receipt.Rows.Length!=1||receipt.ForActor(OwnActor)==null))throw new InvalidDataException();if(sent==lastClockQuery)hostClock.Reply(sent,host,clock.Now);if(receipt!=null)finalResults=receipt;}}
            catch(InvalidDataException){Reject(serverPeer);}catch(IOException){Reject(serverPeer);}catch(ArgumentException){Reject(serverPeer);}
        }
        // SPAWNS integration seam: only the host advances the room after the celebration.
        bool ReturnMatchEndToWarmup(){return HostReturnAllPlayersToWarmupSpawns();}
    }
}
