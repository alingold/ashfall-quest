using System;
using System.IO;
using Ashfall.Core;
using Ashfall.Modules.Modes;
using Ashfall.RecoveryContracts;
namespace Ashfall.Integration.MatchRounds
{
    internal sealed partial class CommandLedger
    {
        internal void Write(BinaryWriter w){w.Write(seen.Count);foreach(string key in seen)RecoveryWire.Text(w,key);w.Write(ticks.Count);foreach(var pair in ticks){RecoveryWire.Text(w,pair.Key);w.Write(pair.Value);}}
        internal void Read(BinaryReader r){int n=RecoveryWire.Count(r,capacity);for(int i=0;i<n;i++)RecoveryWire.Require(seen.Add(RecoveryWire.Text(r)));n=RecoveryWire.Count(r,capacity);for(int i=0;i<n;i++){string key=RecoveryWire.Text(r);long tick=r.ReadInt64();RecoveryWire.Require(key.Length>0&&tick>=0);ticks.Add(key,tick);}}
    }
    public sealed partial class RoundController
    {
        private static void CheckFields(){RecoveryWire.Fields(typeof(RoundController),"<EndReason>k__BackingField:System.String\n<Epoch>k__BackingField:System.Int64\n<RoundIndex>k__BackingField:System.Int32\n<Stage>k__BackingField:Ashfall.Modules.Modes.RoundStage\ncheckpointReservations:System.Collections.Generic.HashSet`1[System.Object]\nclockTickFloor:System.Int64\ndeaths:System.Collections.Generic.Dictionary`2[System.String,System.Int64]\ndirector:System.String\ngate:Ashfall.Core.IAuthorityGate\njournalCapacity:System.Int32\nlastTime:System.Double\nledger:Ashfall.Integration.MatchRounds.CommandLedger\nlifecycle:Ashfall.Integration.MatchRounds.CommandLedger\nmutation:System.Int64\npersonal:System.Collections.Generic.Dictionary`2[System.String,System.Int32]\nroster:Ashfall.Modules.Modes.IModeRoster\nrules:Ashfall.Modules.Modes.ModeRules\nsession:System.String\nstarted:System.Double\ntotals:System.Collections.Generic.Dictionary`2[System.String,System.Int32]");RecoveryWire.Fields(typeof(CommandLedger),"authority:Ashfall.Core.IAuthorityGate\ncapacity:System.Int32\nepoch:System.Int64\nseen:System.Collections.Generic.HashSet`1[System.String]\nsession:System.String\nticks:System.Collections.Generic.Dictionary`2[System.String,System.Int64]");}
        public const string CheckpointCodec="MatchRoundsCold/1";
        // Rules are supplied from the pinned catalog and compared exactly, never clamped on read.
        private static void RuleImage(BinaryWriter w,ModeRules x){RecoveryWire.Text(w,x.Mode);w.Write(x.Teams);w.Write(x.ScoreLimit);w.Write(x.TimeLimit);w.Write(x.Rounds);w.Write(x.Catalog);w.Write(x.InfiniteAmmo);w.Write(x.InfiniteBuilds);w.Write(x.ZoneEnabled);w.Write(x.Damage);}
        private static void CheckRules(BinaryReader r,ModeRules x){RecoveryWire.Require(RecoveryWire.Text(r)==x.Mode&&r.ReadInt32()==x.Teams&&r.ReadInt32()==x.ScoreLimit&&r.ReadInt32()==x.TimeLimit&&r.ReadInt32()==x.Rounds&&RecoveryWire.Bool(r)==x.Catalog&&RecoveryWire.Bool(r)==x.InfiniteAmmo&&RecoveryWire.Bool(r)==x.InfiniteBuilds&&RecoveryWire.Bool(r)==x.ZoneEnabled&&RecoveryWire.Float(r)==x.Damage);}
        private static double TimeValue(BinaryReader r){double n=r.ReadDouble();RecoveryWire.Require(!Double.IsNaN(n)&&!Double.IsInfinity(n)&&n>=0);return n;}
        public byte[] CaptureCheckpoint(CheckpointBinding binding)
        {
            CheckFields();RecoveryWire.Require(binding!=null&&binding.Session==session&&binding.Epoch==Epoch&&checkpointReservations.Count==0);
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
                RecoveryWire.Text(w,CheckpointCodec);binding.Write(w);RuleImage(w,rules);RecoveryWire.Text(w,director);w.Write(journalCapacity);w.Write(mutation);w.Write(started);w.Write(lastTime);w.Write(clockTickFloor);w.Write((int)Stage);w.Write(RoundIndex);RecoveryWire.Text(w,EndReason);
                w.Write(totals.Count);foreach(var p in totals){RecoveryWire.Text(w,p.Key);w.Write(p.Value);}w.Write(personal.Count);foreach(var p in personal){RecoveryWire.Text(w,p.Key);w.Write(p.Value);}w.Write(deaths.Count);foreach(var p in deaths){RecoveryWire.Text(w,p.Key);w.Write(p.Value);}ledger.Write(w);lifecycle.Write(w);w.Flush();byte[] b=m.ToArray();RecoveryWire.Require(b.Length<=RecoveryWire.Limit);return b;
            }
        }
        public static RoundController RestoreCheckpoint(byte[] bytes,CheckpointBinding binding,ModeRules rules,IModeRoster roster,string director,IAuthorityGate gate,int capacity)
        {
            CheckFields();using(var r=RecoveryWire.Reader(bytes)){
                RecoveryWire.Require(RecoveryWire.Text(r)==CheckpointCodec);binding.Check(r);CheckRules(r,rules);RecoveryWire.Require(RecoveryWire.Text(r)==director&&r.ReadInt32()==capacity);
                var x=new RoundController(rules,roster,director,binding.Session,binding.Epoch,gate,capacity);x.mutation=r.ReadInt64();RecoveryWire.Require(x.mutation>=0);x.started=TimeValue(r);x.lastTime=TimeValue(r);x.clockTickFloor=r.ReadInt64();RecoveryWire.Require(x.clockTickFloor>=-1&&x.started<=x.lastTime);x.Stage=(RoundStage)RecoveryWire.Count(r,3);x.RoundIndex=RecoveryWire.Count(r,rules.Rounds);x.EndReason=RecoveryWire.Text(r);RecoveryWire.Require((x.Stage==RoundStage.Loading||x.RoundIndex>=1)&&(x.Stage!=RoundStage.Complete||x.RoundIndex==rules.Rounds));
                int n=RecoveryWire.Count(r,65536);for(int i=0;i<n;i++)x.totals.Add(RecoveryWire.Text(r),RecoveryWire.Count(r,Int32.MaxValue));n=RecoveryWire.Count(r,65536);for(int i=0;i<n;i++)x.personal.Add(RecoveryWire.Text(r),RecoveryWire.Count(r,Int32.MaxValue));n=RecoveryWire.Count(r,65536);for(int i=0;i<n;i++){string key=RecoveryWire.Text(r);long life=r.ReadInt64();RecoveryWire.Require(life>=0);x.deaths.Add(key,life);}x.ledger.Read(r);x.lifecycle.Read(r);RecoveryWire.End(r);return x;
            }
        }
    }
}
