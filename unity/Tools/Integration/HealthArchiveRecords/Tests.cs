using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.HealthStates;
sealed class Tests : IHealthWorld,IAuthorityGate,IInteractionQuery
{
 public double Now {get{return 0;}} public bool InMatch {get{return true;}} public bool TeamChangesAllowed {get{return false;}}
 public bool TryGetPose(string id,out RescuePose p){p=new RescuePose();return false;}
 public RuleResult Validate(CommandContext c){return RuleResult.Accept();}
 public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float e){return true;}
 static int count; static void Check(bool b,string s){if(!b)throw new Exception(s);count++;}
 static CheckpointBinding B(){return new CheckpointBinding("build","catalog","s","host",7,1,1,100);}
 static void Reject(byte[] b){HealthSnapshot[] s=null;HealthTransition[] t=null;bool fail=false;try{MatchCheckpointRecords.Restore(b,B(),out s,out t);}catch(InvalidDataException){fail=true;}catch(IOException){fail=true;}catch(ArgumentException){fail=true;}Check(fail&&s==null&&t==null,"reject atomically");}
 static byte[] Packet(HealthTransition t,Dictionary<string,int> at)
 {
  var s=t.After;using(var m=new MemoryStream())using(var w=new BinaryWriter(m,Encoding.UTF8)){
   w.Write(1);w.Write(t.SessionId);at["epoch"]=(int)m.Position;w.Write(t.AuthorityEpoch);at["seq"]=(int)m.Position;w.Write(t.Sequence);w.Write(t.Kind);at["before"]=(int)m.Position;w.Write(t.BeforeRevision);
   at["id"]=(int)m.Position;w.Write(s.Id);at["team"]=(int)m.Position;w.Write(s.Team);at["deaths"]=(int)m.Position;w.Write(s.Deaths);at["revision"]=(int)m.Position;w.Write(s.Revision);at["life"]=(int)m.Position;w.Write((int)s.Life);at["hp"]=(int)m.Position;w.Write(s.Health);at["shield"]=(int)m.Position;w.Write(s.Shield);at["time"]=(int)m.Position;w.Write(s.DownedUntil);w.Write(s.RespawnAt);w.Write(s.ProtectionUntil);w.Write(s.ReadyUntil);at["bool"]=(int)m.Position;w.Write(s.Connected);at["charge"]=(int)m.Position;w.Write(s.Charge);at["lg"]=(int)m.Position;w.Write(s.LifeGeneration);at["cg"]=(int)m.Position;w.Write(s.ConnectionGeneration);w.Flush();return m.ToArray();}
 }
 static void BadPacket(byte[] b,long seq){bool fail=false;try{MatchCheckpointRecords.RestorePublication(b,"s",7,seq);}catch(InvalidDataException){fail=true;}catch(IOException){fail=true;}catch(ArgumentException){fail=true;}Check(fail,"bad publication");}
 static void Main(string[] args)
 {
  if(args.Length==2&&args[0]=="cold"){
   HealthSnapshot[] s;HealthTransition[] t;MatchCheckpointRecords.Restore(File.ReadAllBytes(args[1]),B(),out s,out t);
   Check(s[0].Health==100&&s[2].Health==75,"cold historical state");Check(Object.ReferenceEquals(s[0],s[1])&&Object.ReferenceEquals(s[0],t[0].After)&&Object.ReferenceEquals(s[2],t[1].After),"cold sharing");Console.WriteLine("COLD PASS "+count);return;
  }
  if(args.Length==1&&args[0]=="guard") {bool refused=false;try{MatchCheckpointRecords.Capture(B(),new HealthSnapshot[0],new HealthTransition[0]);}catch(InvalidDataException){refused=true;}Check(refused,"field drift must refuse");Console.WriteLine("GUARD PASS");return;}
  var f=new Tests();var h=new HealthService("s",7,2,false,f,f,f);Check(h.Register(new CommandContext("s","host","register",1,7,true),"a",0,false).Accepted,"actual register");var original=h.Read("a");
  Check(h.ApplyLoss(new CommandContext("s","host","damage",2,7,true),"a",original.Revision,25,0,true).Accepted,"actual damage");var newer=h.Read("a");var events=h.DrainTransitions();
  byte[] bytes=MatchCheckpointRecords.Capture(B(),new[]{original,original,newer},events);HealthSnapshot[] snapshots;HealthTransition[] transitions;
  MatchCheckpointRecords.Restore(bytes,B(),out snapshots,out transitions);Check(!Object.ReferenceEquals(original,snapshots[0]),"fresh immutable records");Check(Object.ReferenceEquals(snapshots[0],snapshots[1])&&Object.ReferenceEquals(snapshots[0],transitions[0].After),"validated sharing");Check(snapshots[0].Health==100&&h.Read("a").Health==75,"historical not live substitute");
  for(int i=0;i<bytes.Length;i++){var truncated=new byte[i];Array.Copy(bytes,truncated,i);Reject(truncated);}var trailing=new byte[bytes.Length+1];Array.Copy(bytes,trailing,bytes.Length);Reject(trailing);
  var wrong=(byte[])bytes.Clone();wrong[4]=2;Reject(wrong);wrong=(byte[])bytes.Clone();wrong[12]^=1;Reject(wrong);
  int tableOffset;
  using(var m=new MemoryStream(bytes,false))using(var r=new BinaryReader(m)) {r.ReadInt32();r.ReadInt32();for(int i=0;i<4;i++){int len=r.ReadInt32();r.ReadBytes(len);}for(int i=0;i<4;i++)r.ReadInt64();tableOffset=(int)m.Position;}
  foreach(int invalid in new[]{-1,8193,Int32.MaxValue}) {wrong=(byte[])bytes.Clone();Array.Copy(BitConverter.GetBytes(invalid),0,wrong,tableOffset,4);Reject(wrong);}
  wrong=(byte[])bytes.Clone();wrong[tableOffset+4]=2;Reject(wrong);
  bool bound=false;HealthSnapshot[] bs=null;HealthTransition[] bt=null;
  try {MatchCheckpointRecords.Restore(bytes,new CheckpointBinding("other","catalog","s","host",7,1,1,100),out bs,out bt);}catch(InvalidDataException){bound=true;}
  Check(bound&&bs==null&&bt==null,"wrong binding atomically rejected");
  var at=new Dictionary<string,int>();byte[] packet=Packet(events[0],at);var restored=MatchCheckpointRecords.RestorePublication(packet,"s",7,events[0].Sequence);Check(restored.After.Health==100&&restored.After.Id=="a","legacy journal exact historical packet");
  var edits=new Dictionary<string,byte[]>{{"epoch",BitConverter.GetBytes(8L)},{"seq",BitConverter.GetBytes(0L)},{"before",BitConverter.GetBytes(Int64.MaxValue)},{"team",BitConverter.GetBytes(8)},{"deaths",BitConverter.GetBytes(-1)},{"revision",BitConverter.GetBytes(0L)},{"life",BitConverter.GetBytes(4)},{"hp",BitConverter.GetBytes(Single.NaN)},{"shield",BitConverter.GetBytes(101f)},{"time",BitConverter.GetBytes(Double.PositiveInfinity)},{"bool",new byte[]{2}},{"charge",BitConverter.GetBytes(-1f)},{"lg",BitConverter.GetBytes(0L)},{"cg",BitConverter.GetBytes(Int64.MaxValue)}};
  foreach(var edit in edits){var b=(byte[])packet.Clone();Array.Copy(edit.Value,0,b,at[edit.Key],edit.Value.Length);BadPacket(b,events[0].Sequence);}
  wrong=(byte[])packet.Clone();wrong[at["id"]+1]=255;BadPacket(wrong,events[0].Sequence);
  bool duplicate=false;try{MatchCheckpointRecords.Capture(B(),new HealthSnapshot[0],new[]{events[0],events[0]});}catch(InvalidDataException){duplicate=true;}Check(duplicate,"duplicate transition identity");
  // Same (id,revision), contradictory payload from an independently parsed historical packet.
  wrong=(byte[])packet.Clone();Array.Copy(BitConverter.GetBytes(99f),0,wrong,at["hp"],4);var conflict=MatchCheckpointRecords.RestorePublication(wrong,"s",7,events[0].Sequence);
  bool collided=false;try{MatchCheckpointRecords.Capture(B(),new[]{original,conflict.After},new HealthTransition[0]);}catch(InvalidDataException){collided=true;}Check(collided,"conflicting table identity");
  var path=Path.GetFullPath("archive.bin");File.WriteAllBytes(path,bytes);var proc=Process.Start(new ProcessStartInfo(typeof(Tests).Assembly.Location,"cold \""+path+"\""){UseShellExecute=false});proc.WaitForExit();Check(proc.ExitCode==0,"fresh subprocess restore");
  Check(h.Read("a").Health==75&&h.PendingTransitionCount==0,"no live mutation");Check(h.SetConnected(new CommandContext("s","host","disconnect",3,7,true),"a",h.Read("a").Revision,false).Accepted,"disconnect");
  Check(h.SetConnected(new CommandContext("s","host","reconnect",4,7,true),"a",h.Read("a").Revision,true).Accepted,"reconnect");
  Check(h.ApplyLoss(new CommandContext("s","host","lethal",5,7,true),"a",h.Read("a").Revision,75,100,true).Accepted,"lethal");
  var dead=h.Read("a");var more=h.DrainTransitions();var family=MatchCheckpointRecords.Capture(B(),new[]{dead,original,newer},more);
  MatchCheckpointRecords.Restore(family,B(),out snapshots,out transitions);
  Check(snapshots[0].Life==LifeState.Downed&&snapshots[0].Deaths==1&&snapshots[0].ConnectionGeneration==3&&snapshots[0].DownedUntil==60,"downed and reconnect exact historical generations");
  Console.WriteLine("PASS "+count+" checks");
 }
}
