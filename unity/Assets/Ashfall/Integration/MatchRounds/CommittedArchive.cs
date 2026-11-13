using System;
using System.IO;
using Ashfall.Modules.Modes;
using Ashfall.Modules.HealthStates;
using Ashfall.RecoveryContracts;
namespace Ashfall.Integration.MatchRounds
{
    public sealed partial class RoundController
    {
        private static void Actor(BinaryWriter w,ModeActor a)
        {RecoveryWire.Text(w,a.Id);RecoveryWire.Text(w,a.Name);RecoveryWire.Text(w,a.Life);w.Write(a.Team);w.Write(a.Loaded);w.Write(a.Spectator);}
        private static ModeActor Actor(BinaryReader r)
        {string id=RecoveryWire.Text(r),name=RecoveryWire.Text(r),life=RecoveryWire.Text(r);int team=r.ReadInt32();RecoveryWire.Require(id.Length>0&&team>=-1&&team<8&&(life=="alive"||life=="dead"||life=="downed"||life=="spectator"));return new ModeActor(id,name,team,life,RecoveryWire.Bool(r),RecoveryWire.Bool(r));}
        // Historical eligibility, never recaptured from the present roster.
        public byte[] CaptureCommittedArchive(CommittedEvent e)
        {
            RecoveryWire.Fields(typeof(CommittedEvent),"Connection:System.Int64\nEpoch:System.Int64\nGeneration:System.Int64\nHasKiller:System.Boolean\nKiller:Ashfall.Modules.Modes.ModeActor\nLife:System.Int64\nOwner:Ashfall.Integration.MatchRounds.RoundController\nRevision:System.Int64\nRound:System.Int32\nSequence:System.Int64\nVictim:Ashfall.Modules.Modes.ModeActor");
            RecoveryWire.Require(e!=null&&e.Owner==this&&e.Epoch==Epoch&&e.Round==RoundIndex);
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m))
            {w.Write(1);w.Write(e.Epoch);w.Write(e.Round);w.Write(e.Life);w.Write(e.Sequence);w.Write(e.Revision);w.Write(e.Generation);w.Write(e.Connection);Actor(w,e.Victim);w.Write(e.HasKiller);if(e.HasKiller)Actor(w,e.Killer);w.Flush();return m.ToArray();}
        }
        public static RoundController RestoreCheckpointWithCommittedArchive(byte[] rounds,CheckpointBinding binding,ModeRules rules,IModeRoster roster,string director,Ashfall.Core.IAuthorityGate gate,int capacity,byte[] committed,HealthTransition original,out CommittedEvent receipt)
        {
            receipt=null;var round=RestoreCheckpoint(rounds,binding,rules,roster,director,gate,capacity);
            var restored=round.RestoreCommittedArchive(committed,original);receipt=restored;return round;
        }
        private CommittedEvent RestoreCommittedArchive(byte[] bytes,HealthTransition original)
        {
            RecoveryWire.Fields(typeof(CommittedEvent),"Connection:System.Int64\nEpoch:System.Int64\nGeneration:System.Int64\nHasKiller:System.Boolean\nKiller:Ashfall.Modules.Modes.ModeActor\nLife:System.Int64\nOwner:Ashfall.Integration.MatchRounds.RoundController\nRevision:System.Int64\nRound:System.Int32\nSequence:System.Int64\nVictim:Ashfall.Modules.Modes.ModeActor");
            using(var r=RecoveryWire.Reader(bytes))
            {
                RecoveryWire.Require(r.ReadInt32()==1&&r.ReadInt64()==Epoch&&r.ReadInt32()==RoundIndex);
                long life=r.ReadInt64(),sequence=r.ReadInt64(),revision=r.ReadInt64(),generation=r.ReadInt64(),connection=r.ReadInt64();
                ModeActor v=Actor(r);bool has=RecoveryWire.Bool(r);ModeActor k=has?Actor(r):default(ModeActor);RecoveryWire.End(r);
                RecoveryWire.Require(original!=null&&original.SessionId==session&&original.AuthorityEpoch==Epoch&&original.Sequence==sequence&&original.After.Id==v.Id&&original.After.Revision==revision&&original.After.LifeGeneration==generation&&original.After.ConnectionGeneration==connection&&life>0&&!v.Spectator&&(!has||!k.Spectator)&&(original.After.Life==LifeState.Dead||original.After.Life==LifeState.Downed));
                return new CommittedEvent(this,original,life,v,k,has);
            }
        }
    }
}
