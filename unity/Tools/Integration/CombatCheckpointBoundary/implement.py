from pathlib import Path
R=Path(r'C:\Users\aling\Documents\ashfall');A=R/'unity/Assets/Ashfall'
p=A/'Modules/Combat/CheckpointState.cs';s=p.read_text();s=s.replace('using Ashfall.Modules.HealthStates;','using Ashfall.Core;\nusing Ashfall.RecoveryContracts;')
s=s.replace('internal Dictionary<HostHit,int> WriteCheckpoint','private Dictionary<HostHit,int> WriteCheckpoint').replace('internal HostHit[] ReadCheckpoint','private HostHit[] ReadCheckpoint')
start=(A/'Modules/HealthStates/CheckpointState.cs').read_text();primitive=start[start.index('        internal static int Count'):start.index('        internal static void Result')]
s=s.replace('        internal static int Index', '''        // Archive indices are immutable; callers cannot alter the owner's mapping.
        public sealed class ArchiveBindings
        {
            readonly Dictionary<HostHit,int> indices;
            internal ArchiveBindings(Dictionary<HostHit,int> value){indices=value;}
            public int this[HostHit hit] {get{return indices[hit];}}
        }
        public ArchiveBindings CaptureArchive(BinaryWriter writer,IEnumerable<HostHit> retained)
        {return new ArchiveBindings(WriteCheckpoint(writer,retained));}
        // Always reconstruct a NEW unpublished owner. Never import into a live rules object.
        public static CombatRules RestoreArchive(BinaryReader reader,string session,long epoch,long maxAge,int capacity,
            IAuthorityGate authority,ICombatEvidence facts,IInteractionQuery query,string host,out HostHit[] bindings)
        {
            bindings=null;
            var value=new CombatRules(session,epoch,maxAge,capacity,authority,facts,query,host);
            var decoded=value.ReadCheckpoint(reader);bindings=decoded;return value;
        }
        private static class CheckpointWire
        {
'''+primitive+'''        }
        public static int Index''')
p.write_text(s)
# Owner codecs retain the exact legacy embedded record layout. Constructors remain internal.
p=A/'Modules/HealthStates/MatchCheckpointRecords.cs';s=p.read_text();pos=s.rfind('    }')
s=s[:pos]+'''        // Embedded composed-checkpoint/3 records; these APIs convey history, never authority.
        public static void WriteDamageRecord(BinaryWriter writer,DamageReceipt value)
        {DamageFields();if(value!=null&&value.Snapshot!=null)Snapshot(value.Snapshot);CheckpointWire.Damage(writer,value);}
        public static DamageReceipt ReadDamageRecord(BinaryReader reader)
        {DamageFields();var value=CheckpointWire.Damage(new StrictReaderView(reader));if(value!=null&&value.Snapshot!=null)Snapshot(value.Snapshot);return value;}
        public static void WriteTransitionRecord(BinaryWriter writer,HealthTransition value)
        {Fields();Transition(value,value.SessionId,value.AuthorityEpoch,value.Sequence);CheckpointWire.Transition(writer,value);}
        public static HealthTransition ReadTransitionRecord(BinaryReader reader)
        {Fields();var value=CheckpointWire.Transition(new StrictReaderView(reader));Transition(value,value.SessionId,value.AuthorityEpoch,value.Sequence);return value;}
        private static void DamageFields()
        {Fields();RecoveryWire.Fields(typeof(DamageReceipt),"LogicalId:System.String\\nResult:Ashfall.Core.RuleResult\\nRetryable:System.Boolean\\nSnapshot:Ashfall.Modules.HealthStates.HealthSnapshot\\nTargetId:System.String");}
        // This view shares the stream position and deliberately does not own/dispose it.
        private sealed class StrictReaderView : BinaryReader
        {
            internal StrictReaderView(BinaryReader source):base(source.BaseStream,new UTF8Encoding(false,true),true){}
            public override bool ReadBoolean(){return RecoveryWire.Bool(this);}
        }
'''+s[pos:];p.write_text(s)
# Primitive wire utility is local to the composition. Health records always go through owner API.
health=(A/'Modules/HealthStates/CheckpointState.cs').read_text();util=health[health.index('    internal static class CheckpointWire'):health.index('        internal static void Snapshot')]
util+='''        internal static void Damage(BinaryWriter w,DamageReceipt d){MatchCheckpointRecords.WriteDamageRecord(w,d);}
        internal static DamageReceipt Damage(BinaryReader r){return MatchCheckpointRecords.ReadDamageRecord(r);}
        internal static void Transition(BinaryWriter w,HealthTransition t){MatchCheckpointRecords.WriteTransitionRecord(w,t);}
        internal static HealthTransition Transition(BinaryReader r){return MatchCheckpointRecords.ReadTransitionRecord(r);}
    }
'''
(A/'Integration/CombatPublication/ArchiveWire.cs').write_text('using System;\nusing System.IO;\nusing System.Text;\nusing System.Security.Cryptography;\nusing Ashfall.Core;\nusing Ashfall.Modules.HealthStates;\nnamespace Ashfall.Integration {\n'+util+'}\n')
# Both partial namespaces need the same local primitive helper.
for p in (A/'Integration/CombatPublication').glob('*.cs'):
 if 'namespace Ashfall.Integration.CombatPublication' in p.read_text():
  s=p.read_text();s='using CheckpointWire = Ashfall.Integration.CheckpointWire;\n'+s;p.write_text(s)
p=A/'Integration/CombatPublication/ColdCheckpoint.cs';s=p.read_text().replace('rules.WriteCheckpoint(w,collisionBindings.Keys)','rules.CaptureArchive(w,collisionBindings.Keys)')
s=s.replace('var b=new CombatHealthBridge(s,world,facts,authority,query,c.SessionId,c.AuthorityEpoch,c.ActorId,cap);\n                    var hs=b.rules.ReadCheckpoint(r);','HostHit[] hs;\n                    var b=new CombatHealthBridge(s,world,facts,authority,query,c.SessionId,c.AuthorityEpoch,c.ActorId,cap,r,out hs);')
marker='        internal byte[] CreateColdCheckpoint'
s=s.replace(marker,'''        private CombatHealthBridge(HealthService service,IHealthWorld world,ICombatEvidence facts,
            IAuthorityGate authority,IInteractionQuery query,string session,long epoch,string host,int cap,
            BinaryReader reader,out HostHit[] bindings):this(service,world,facts,authority,query,session,epoch,host,cap)
        {rules=CombatRules.RestoreArchive(reader,session,epoch,256,cap,authority,this,query,host,out bindings);}
'''+marker);p.write_text(s)
