using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Ashfall.RecoveryContracts;

namespace Ashfall.Modules.HealthStates
{
    // Historical records only. Authenticity, freshness and coherent root adoption are external.
    public static class MatchCheckpointRecords
    {
        public static long Sequence(HealthService service){if(service==null)throw new ArgumentNullException("service");return service.CheckpointSequence;}
        public const string ApiVersion="HealthArchive/1";
        public const int MaximumRecords=8192, MaximumBytes=2097152;
        private sealed class StrictReader : BinaryReader
        {
            internal StrictReader(Stream s):base(s,new UTF8Encoding(false,true)){}
            public override bool ReadBoolean(){return RecoveryWire.Bool(this);}
        }
        private static void Need(bool ok){RecoveryWire.Require(ok);}
        private static void Fields()
        {
            RecoveryWire.Fields(typeof(HealthSnapshot),"Charge:System.Single\nConnected:System.Boolean\nConnectionGeneration:System.Int64\nDeaths:System.Int32\nDownedUntil:System.Double\nHealth:System.Single\nId:System.String\nLife:Ashfall.Modules.HealthStates.LifeState\nLifeGeneration:System.Int64\nProtectionUntil:System.Double\nReadyUntil:System.Double\nRespawnAt:System.Double\nRevision:System.Int64\nShield:System.Single\nTeam:System.Int32");
            RecoveryWire.Fields(typeof(HealthTransition),"After:Ashfall.Modules.HealthStates.HealthSnapshot\nAuthorityEpoch:System.Int64\nBeforeRevision:System.Int64\nKind:System.String\nSequence:System.Int64\nSessionId:System.String");
        }
        private static string Key(HealthSnapshot s){return s.Id.Length+":"+s.Id+":"+s.Revision;}
        private static byte[] Image(HealthSnapshot s)
        {
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m,new UTF8Encoding(false,true)))
            {CheckpointWire.Snapshot(w,s);w.Flush();return m.ToArray();}
        }
        private static bool Same(byte[] a,byte[] b)
        {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        private static void Snapshot(HealthSnapshot s)
        {
            Need(s!=null&&s.Team>=-1&&s.Team<8);
            // Reuse actual owner numeric/life/generation readers, including strict bools.
            byte[] b=Image(s);using(var m=new MemoryStream(b,false))using(var r=new StrictReader(m))
            {Need(CheckpointWire.Snapshot(r)!=null);RecoveryWire.End(r);}
            Need(s.LifeGeneration<=s.Revision&&s.ConnectionGeneration<=s.Revision);
            Need(s.Deaths<=s.Revision);
            Need(s.Life==LifeState.Downed?s.DownedUntil>0:s.DownedUntil==0);
            Need(s.Life==LifeState.Dead||s.RespawnAt==0);
            Need(s.Life==LifeState.Alive||(s.Charge==0&&s.ReadyUntil==0));
        }
        private static void Transition(HealthTransition t,string session,long epoch,long ceiling)
        {
            Need(t!=null&&t.After!=null&&t.SessionId==session&&t.AuthorityEpoch==epoch&&t.Sequence>0&&t.Sequence<=ceiling);
            Need(t.BeforeRevision>=0&&t.BeforeRevision<Int64.MaxValue&&t.After.Revision==t.BeforeRevision+1);
            Need(!String.IsNullOrEmpty(t.Kind)&&t.Kind.Length<=512);
            Need(t.Kind=="registered"||t.Kind=="damage"||t.Kind=="spawn"||t.Kind=="team"||t.Kind=="connection"||t.Kind=="revive"||t.Kind=="ready-expired"||t.Kind=="bleedout"||t.Kind=="gain");
            Need((t.Kind=="registered")== (t.BeforeRevision==0));
            new UTF8Encoding(false,true).GetBytes(t.Kind);Snapshot(t.After);
        }
        private static int Add(HealthSnapshot s,List<HealthSnapshot> table,Dictionary<string,int> keys)
        {
            Snapshot(s);string key=Key(s);int index;
            if(keys.TryGetValue(key,out index)){Need(Same(Image(s),Image(table[index])));return index;}
            Need(table.Count<MaximumRecords);index=table.Count;keys.Add(key,index);table.Add(s);return index;
        }
        private static void History(List<HealthSnapshot> table)
        {
            var sorted=new List<HealthSnapshot>(table);
            sorted.Sort(delegate(HealthSnapshot a,HealthSnapshot b){int c=StringComparer.Ordinal.Compare(a.Id,b.Id);return c!=0?c:a.Revision.CompareTo(b.Revision);});
            for(int i=1;i<sorted.Count;i++)
            {
                var a=sorted[i-1];var b=sorted[i];if(a.Id!=b.Id)continue;
                long gap=b.Revision-a.Revision;
                Need(gap>0&&b.LifeGeneration>=a.LifeGeneration&&b.ConnectionGeneration>=a.ConnectionGeneration&&b.Deaths>=a.Deaths);
                Need(b.LifeGeneration-a.LifeGeneration<=gap&&b.ConnectionGeneration-a.ConnectionGeneration<=gap&&b.Deaths-a.Deaths<=gap);
            }
        }
        public static byte[] Capture(CheckpointBinding binding,HealthSnapshot[] historicalSnapshots,HealthTransition[] retainedTransitions)
        {
            Fields();Need(binding!=null&&historicalSnapshots!=null&&retainedTransitions!=null);
            Need(historicalSnapshots.Length<=MaximumRecords&&retainedTransitions.Length<=MaximumRecords);
            var table=new List<HealthSnapshot>();var keys=new Dictionary<string,int>(StringComparer.Ordinal);
            var refs=new int[historicalSnapshots.Length];
            for(int i=0;i<refs.Length;i++)refs[i]=Add(historicalSnapshots[i],table,keys);
            var sequences=new HashSet<long>();
            foreach(var t in retainedTransitions){Transition(t,binding.Session,binding.Epoch,binding.Sequence);Need(sequences.Add(t.Sequence));Add(t.After,table,keys);}
            History(table);
            var ordered=new List<HealthTransition>(retainedTransitions);
            ordered.Sort(delegate(HealthTransition a,HealthTransition b){return a.Sequence.CompareTo(b.Sequence);});
            var revisions=new Dictionary<string,long>(StringComparer.Ordinal);
            foreach(var t in ordered){long prior;Need(!revisions.TryGetValue(t.After.Id,out prior)||t.After.Revision>prior);revisions[t.After.Id]=t.After.Revision;}
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m,new UTF8Encoding(false,true)))
            {
                w.Write(0x48415243);w.Write(1);binding.Write(w);w.Write(table.Count);
                foreach(var s in table)CheckpointWire.Snapshot(w,s);
                w.Write(refs.Length);foreach(int index in refs)w.Write(index);
                w.Write(retainedTransitions.Length);foreach(var t in retainedTransitions)CheckpointWire.Transition(w,t);
                w.Flush();Need(m.Length<=MaximumBytes);return m.ToArray();
            }
        }
        public static void Restore(byte[] bytes,CheckpointBinding binding,out HealthSnapshot[] historicalSnapshots,out HealthTransition[] retainedTransitions)
        {
            historicalSnapshots=null;retainedTransitions=null;Fields();
            Need(binding!=null&&bytes!=null&&bytes.Length<=MaximumBytes);
            HealthSnapshot[] snapshots;HealthTransition[] transitions;
            using(var m=new MemoryStream(bytes,false))using(var r=new StrictReader(m))
            {
                Need(r.ReadInt32()==0x48415243&&r.ReadInt32()==1);binding.Check(r);
                int n=CheckpointWire.Count(r,MaximumRecords);var table=new HealthSnapshot[n];var used=new bool[n];
                var keys=new Dictionary<string,int>(StringComparer.Ordinal);
                for(int i=0;i<n;i++){var s=CheckpointWire.Snapshot(r);Snapshot(s);Need(!keys.ContainsKey(Key(s)));keys.Add(Key(s),i);table[i]=s;}
                n=CheckpointWire.Count(r,MaximumRecords);snapshots=new HealthSnapshot[n];
                for(int i=0;i<n;i++){int index=r.ReadInt32();Need(index>=0&&index<table.Length);snapshots[i]=table[index];used[index]=true;}
                n=CheckpointWire.Count(r,MaximumRecords);transitions=new HealthTransition[n];var sequences=new HashSet<long>();
                for(int i=0;i<n;i++)
                {
                    var t=CheckpointWire.Transition(r);Transition(t,binding.Session,binding.Epoch,binding.Sequence);Need(sequences.Add(t.Sequence));int index;
                    Need(keys.TryGetValue(Key(t.After),out index));Need(Same(Image(t.After),Image(table[index])));used[index]=true;
                    transitions[i]=new HealthTransition(t.SessionId,t.AuthorityEpoch,t.Sequence,t.Kind,t.BeforeRevision,table[index]);
                }
                foreach(bool u in used)Need(u);RecoveryWire.End(r);
            }
            // Also rejects noncanonical length prefixes; no unsupported encoding alias is accepted.
            Need(Same(bytes,Capture(binding,snapshots,transitions)));
            historicalSnapshots=snapshots;retainedTransitions=transitions;
        }
        // Deliberate owner port for the unchanged CombatPublication transition/1 packet.
        // Only the authenticated bound producer may supply this historical packet.
        public static HealthTransition RestorePublication(byte[] bytes,string session,long epoch,long sequence)
        {
            Fields();Need(bytes!=null&&bytes.Length>4&&bytes.Length<=16384&&sequence>0&&epoch>=0&&!String.IsNullOrEmpty(session));
            // Legacy packet omits Snapshot's presence byte. Insert that byte, then use owner readers.
            using(var input=new MemoryStream(bytes,false))using(var r=new StrictReader(input))
            {
                Need(r.ReadInt32()==1);CheckpointWire.Text(r);r.ReadInt64();r.ReadInt64();CheckpointWire.Text(r);r.ReadInt64();
                int split=(int)input.Position;
                var owner=new byte[bytes.Length-3];Array.Copy(bytes,4,owner,0,split-4);owner[split-4]=1;
                Array.Copy(bytes,split,owner,split-3,bytes.Length-split);
                using(var m=new MemoryStream(owner,false))using(var reader=new StrictReader(m))
                {
                    var t=CheckpointWire.Transition(reader);RecoveryWire.End(reader);Transition(t,session,epoch,sequence);Need(t.Sequence==sequence);
                    using(var canonical=new MemoryStream())using(var w=new BinaryWriter(canonical,new UTF8Encoding(false,true)))
                    {CheckpointWire.Transition(w,t);w.Flush();Need(Same(owner,canonical.ToArray()));}
                    return t;
                }
            }
        }
        // Embedded composed-checkpoint/3 records; these APIs convey history, never authority.
        public static void WriteDamageRecord(BinaryWriter writer,DamageReceipt value)
        {DamageFields();if(value!=null&&value.Snapshot!=null)Snapshot(value.Snapshot);CheckpointWire.Damage(writer,value);}
        public static DamageReceipt ReadDamageRecord(BinaryReader reader)
        {DamageFields();var value=CheckpointWire.Damage(new StrictReaderView(reader));if(value!=null&&value.Snapshot!=null)Snapshot(value.Snapshot);return value;}
        public static void WriteTransitionRecord(BinaryWriter writer,HealthTransition value)
        {Fields();Transition(value,value.SessionId,value.AuthorityEpoch,value.Sequence);CheckpointWire.Transition(writer,value);}
        public static HealthTransition ReadTransitionRecord(BinaryReader reader)
        {Fields();var value=CheckpointWire.Transition(new StrictReaderView(reader));Transition(value,value.SessionId,value.AuthorityEpoch,value.Sequence);return value;}
        private static void DamageFields()
        {Fields();RecoveryWire.Fields(typeof(DamageReceipt),"LogicalId:System.String\nResult:Ashfall.Core.RuleResult\nRetryable:System.Boolean\nSnapshot:Ashfall.Modules.HealthStates.HealthSnapshot\nTargetId:System.String");}
        // This view shares the stream position and deliberately does not own/dispose it.
        private sealed class StrictReaderView : BinaryReader
        {
            internal StrictReaderView(BinaryReader source):base(source.BaseStream,new UTF8Encoding(false,true),true){}
            public override bool ReadBoolean(){return RecoveryWire.Bool(this);}
        }
    }
}

