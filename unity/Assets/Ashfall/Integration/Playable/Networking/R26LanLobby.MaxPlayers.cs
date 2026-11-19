using System;using System.Collections.Generic;using System.IO;using Ashfall.Modules.Networking;
namespace Ashfall.Integration.Playable.Networking
{
    public sealed partial class R24LanLobby
    {
        // Layout 6: total, offset, count, then up to six ordinary score rows.
        // 4 + 6 * (2 + 64 + 16) = 496 bytes, below the 512-byte payload budget.
        const byte ScorePageLayout=6;const int ScorePageRows=6;
        R24LanScoreRow[] incomingScores;long incomingScoreRevision=-1,incomingScoreEpoch=-1;
        void PublishScorePages(List<R24LanScoreRow> rows)
        {
            long sequence=checked(++scoreSequence);var pages=new List<byte[]>();
            for(int offset=0;offset<rows.Count;offset+=ScorePageRows){int count=Math.Min(ScorePageRows,rows.Count-offset);
                using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){w.Write(ScorePageLayout);w.Write((byte)rows.Count);w.Write((byte)offset);w.Write((byte)count);
                    for(int i=offset;i<offset+count;i++){var row=rows[i];Write(w,row.Actor,64);w.Write(row.Team);w.Write(row.Kills);w.Write(row.Deaths);w.Write(row.Score);}
                    pages.Add(Packet(PacketKind.Snapshot,ScoreFeature,OwnActor,s.ToArray(),sequence));}
            }
            AppendRetainedScorePages(pages,sequence);
            var frames=pages.ToArray();foreach(var peer in peers.Values)if(peer.Admitted){peer.ScoreFrames=frames;peer.NextScore=0;}
        }
        void FlushScorePages(string id,Peer peer)
        {if(peer.ScoreFrames==null)return;while(peer.NextScore<peer.ScoreFrames.Length){if(!Send(id,peer.ScoreFrames[peer.NextScore]))return;peer.NextScore++;}peer.ScoreFrames=null;peer.NextScore=0;}
        bool ReadScorePages(Packet packet)
        {
            if(packet.Payload==null||packet.Payload.Length==0)return false;
            if(packet.Payload[0]==RetainedScorePageLayout){ReadRetainedScorePage(packet);return true;}
            if(packet.Payload[0]!=ScorePageLayout)return false;
            if(packet.Sequence<=scoreRevision)return true;
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){
                r.ReadByte();int total=r.ReadByte(),offset=r.ReadByte(),count=r.ReadByte();
                if(packet.Sequence<1||packet.BaseSequence!=-1||total<1||total>Capacity||offset%ScorePageRows!=0||offset>=total||count!=Math.Min(ScorePageRows,total-offset))throw new InvalidDataException();
                var page=new R24LanScoreRow[count];for(int i=0;i<count;i++){string actor=Read(r,64);int team=r.ReadInt32(),kills=r.ReadInt32(),deaths=r.ReadInt32(),score=r.ReadInt32();if(team< -1||team>7||kills<0||deaths<0||score<0)throw new InvalidDataException();page[i]=new R24LanScoreRow(actor,team,kills,deaths,score);}
                if(s.Position!=s.Length)throw new InvalidDataException();
                if(incomingScores==null||incomingScoreEpoch!=packet.Epoch||packet.Sequence>incomingScoreRevision){incomingScores=new R24LanScoreRow[total];incomingScoreRevision=packet.Sequence;incomingScoreEpoch=packet.Epoch;}
                if(packet.Sequence<incomingScoreRevision)return true;if(incomingScores.Length!=total)throw new InvalidDataException();
                Array.Copy(page,0,incomingScores,offset,count);var actors=new HashSet<string>(StringComparer.Ordinal);
                foreach(var row in incomingScores){if(row==null)return true;if(!actors.Add(row.Actor))throw new InvalidDataException();}
                scores.Clear();foreach(var row in incomingScores)if(HasMember(row.Actor))scores[row.Actor]=row;scoreRevision=packet.Sequence;incomingScores=null;
            }}catch(InvalidDataException){Reject(serverPeer);}catch(IOException){Reject(serverPeer);}catch(ArgumentException){Reject(serverPeer);}return true;
        }
        // Layout 8 carries final name/kill rows independently of the current roster.
        // 4 + 4 * (2 + 96 + 4) = 412 bytes, below the 512-byte payload budget.
        const byte RetainedScorePageLayout=8;const int RetainedScorePageRows=4;
        IReadOnlyList<KeyValuePair<string,int>> retainedScoreboard;
        KeyValuePair<string,int>[] incomingRetainedScores;long incomingRetainedEpoch=-1,incomingRetainedRevision=-1,retainedRevision=-1;
        public IReadOnlyList<KeyValuePair<string,int>> RetainedScoreboard{get{return State==R24LanLobbyState.Hosting||State==R24LanLobbyState.Connected?retainedScoreboard:null;}}
        void ClearRetainedScoreboard(){retainedScoreboard=null;incomingRetainedScores=null;}
        void RetainScoreboard(R26MatchEndResults result)
        {
            if(result==null)return;
            var rows=new KeyValuePair<string,int>[result.Rows.Length];
            for(int i=0;i<rows.Length;i++)rows[i]=new KeyValuePair<string,int>(result.Rows[i].Name,result.Rows[i].Kills);
            retainedScoreboard=Array.AsReadOnly(rows);scoreDirty=true;
        }
        void AppendRetainedScorePages(List<byte[]> pages,long sequence)
        {
            if(retainedScoreboard==null)return;
            for(int offset=0;offset<retainedScoreboard.Count;offset+=RetainedScorePageRows){int count=Math.Min(RetainedScorePageRows,retainedScoreboard.Count-offset);
                using(var s=new MemoryStream())using(var w=new BinaryWriter(s,Utf8)){w.Write(RetainedScorePageLayout);w.Write((byte)retainedScoreboard.Count);w.Write((byte)offset);w.Write((byte)count);
                    for(int i=offset;i<offset+count;i++){var row=retainedScoreboard[i];Write(w,row.Key,96);w.Write(row.Value);}
                    pages.Add(Packet(PacketKind.Snapshot,ScoreFeature,OwnActor,s.ToArray(),sequence));}
            }
        }
        void ReadRetainedScorePage(Packet packet)
        {
            if(packet.Sequence<=retainedRevision||SelectedMap==null||SelectedMap.Active&&SelectedMap.Rules!=null&&!SelectedMap.Results)return;
            try{using(var s=new MemoryStream(packet.Payload,false))using(var r=new BinaryReader(s,Utf8)){
                r.ReadByte();int total=r.ReadByte(),offset=r.ReadByte(),count=r.ReadByte();
                if(packet.Sequence<1||packet.BaseSequence!=-1||total<1||total>Capacity||offset%RetainedScorePageRows!=0||offset>=total||count!=Math.Min(RetainedScorePageRows,total-offset))throw new InvalidDataException();
                var page=new KeyValuePair<string,int>[count];
                for(int i=0;i<count;i++){string name=Read(r,96);int kills=r.ReadInt32();if(kills<0)throw new InvalidDataException();page[i]=new KeyValuePair<string,int>(name,kills);}
                if(s.Position!=s.Length)throw new InvalidDataException();
                if(incomingRetainedScores==null||incomingRetainedEpoch!=packet.Epoch||packet.Sequence>incomingRetainedRevision){incomingRetainedScores=new KeyValuePair<string,int>[total];incomingRetainedEpoch=packet.Epoch;incomingRetainedRevision=packet.Sequence;}
                if(packet.Sequence<incomingRetainedRevision)return;if(incomingRetainedScores.Length!=total)throw new InvalidDataException();
                Array.Copy(page,0,incomingRetainedScores,offset,count);foreach(var row in incomingRetainedScores)if(row.Key==null)return;
                retainedScoreboard=Array.AsReadOnly(incomingRetainedScores);retainedRevision=packet.Sequence;incomingRetainedScores=null;
            }}catch(InvalidDataException){Reject(serverPeer);}catch(IOException){Reject(serverPeer);}catch(ArgumentException){Reject(serverPeer);}
        }
        // Layout 7 uses layout 3's row encoding, but merges a bounded subset.
        // Periodic full refresh heals send pressure; absent actors are pruned.
        void PublishEquipmentPages(List<R26RemoteEquipment> rows)
        {
            for(int offset=0;offset<rows.Count;offset+=7){var page=rows.GetRange(offset,Math.Min(7,rows.Count-offset));byte[] payload=R26RemoteWire.Equipment(page.ToArray());payload[0]=7;
                byte[] frame=Packet(PacketKind.Snapshot,ScoreFeature,OwnActor,payload,checked(++remoteVisualSequence));foreach(var peer in peers)if(RemotePeerReady(peer.Value))Send(peer.Key,frame);}
        }
        bool ReadEquipmentPage(Packet packet)
        {
            if(packet.Payload==null||packet.Payload.Length==0||packet.Payload[0]!=7)return false;
            if(packet.Sequence<=remoteEquipRevision||packet.BaseSequence!=-1||packet.Payload.Length>471)return true;
            byte[] payload=(byte[])packet.Payload.Clone();payload[0]=R26RemoteWire.EquipmentLayout;R26RemoteEquipment[] rows;
            if(!R26RemoteWire.TryEquipment(payload,out rows)||rows.Length>7)return true;foreach(var row in rows)if(!HasMember(row.Actor))return true;
            remoteEquipRevision=packet.Sequence;foreach(string actor in new List<string>(remoteEquipment.Keys))if(!HasMember(actor))remoteEquipment.Remove(actor);foreach(var row in rows)remoteEquipment[row.Actor]=row;return true;
        }
    }
}
