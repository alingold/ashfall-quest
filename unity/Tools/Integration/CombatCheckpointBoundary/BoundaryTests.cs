using System;
using System.IO;
using System.Text;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
class BoundaryTests : IHealthWorld,IAuthorityGate,IInteractionQuery
{
 public double Now{get{return 0;}} public bool InMatch{get{return true;}}public bool TeamChangesAllowed{get{return false;}}
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float e){return true;}
 public bool TryGetPose(string a,out RescuePose p){p=new RescuePose();return false;}
 static int n;static void Check(bool b){if(!b)throw new Exception("boundary assertion "+n);n++;}
 static byte[] Encode(Action<BinaryWriter> write){using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Encoding.UTF8)){write(w);w.Flush();return m.ToArray();}}
 static bool Same(byte[] a,byte[] b){return Convert.ToBase64String(a)==Convert.ToBase64String(b);}
 static void Main(){var f=new BoundaryTests();var h=new HealthService("s",7,0,false,f,f,f);h.Register(new CommandContext("s","host","r",1,7,true),"v",-1,false);var d=h.ApplyDamage(new CommandContext("s","host","d",2,7,true),"hit","v",1,25,0,false);Check(d.Result.Accepted);var ts=h.DrainTransitions();
  var bytes=Encode(w=>MatchCheckpointRecords.WriteDamageRecord(w,d));DamageReceipt restored;
  using(var r=new BinaryReader(new MemoryStream(bytes))){restored=MatchCheckpointRecords.ReadDamageRecord(r);Check(r.BaseStream.Position==bytes.Length);}
  Check(!Object.ReferenceEquals(restored,d)&&!Object.ReferenceEquals(restored.Snapshot,d.Snapshot));Check(restored.Snapshot.Health==75&&restored.LogicalId=="hit"&&restored.TargetId=="v");Check(Same(bytes,Encode(w=>MatchCheckpointRecords.WriteDamageRecord(w,restored))));
  // Every truncated prefix must throw without yielding any record.
  for(int i=0;i<bytes.Length;i++){var cut=new byte[i];Array.Copy(bytes,cut,i);bool rejected=false;try{using(var r=new BinaryReader(new MemoryStream(cut)))MatchCheckpointRecords.ReadDamageRecord(r);}catch(InvalidDataException){rejected=true;}catch(IOException){rejected=true;}Check(rejected);}
  foreach(int offset in new[]{0,bytes.Length-1}){var bad=(byte[])bytes.Clone();bad[offset]=255;bool rejected=false;try{using(var r=new BinaryReader(new MemoryStream(bad)))MatchCheckpointRecords.ReadDamageRecord(r);}catch(InvalidDataException){rejected=true;}catch(IOException){rejected=true;}Check(rejected);}
  foreach(var t in ts){var wire=Encode(w=>MatchCheckpointRecords.WriteTransitionRecord(w,t));HealthTransition cold;using(var r=new BinaryReader(new MemoryStream(wire)))cold=MatchCheckpointRecords.ReadTransitionRecord(r);Check(!Object.ReferenceEquals(cold,t));Check(Same(wire,Encode(w=>MatchCheckpointRecords.WriteTransitionRecord(w,cold))));Check(cold.After.Revision==t.After.Revision&&cold.Sequence==t.Sequence);}
  Check(MatchCheckpointRecords.Sequence(h)==2&&h.Read("v").Health==75&&h.PendingTransitionCount==0);Console.WriteLine("BOUNDARY PASS "+n);
 }
}

