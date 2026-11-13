using System;
using System.IO;
using System.Security.Cryptography;
using Ashfall.Core;
using Ashfall.Modules.Projectiles;
static class ColdTests
{
 static int n;
 static void Need(bool b,string label){if(!b)throw new Exception(label);n++;}
 static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
 static ProjectileSimulation Restore(byte[] b,Fixture f,string digest=null,string build="build",long generation=7){return ProjectileSimulation.RestoreCold(b,digest??Hash(b),build,"catalog",generation,"s",3,"host",f.W.MapRevision,f.W,f.W,f.A);}
 static void Reject(byte[] b,Fixture f){bool reject=false;try{Restore(b,f);}catch(InvalidDataException){reject=true;}catch(EndOfStreamException){reject=true;}catch(ArgumentException){reject=true;}Need(reject,"malformed image rejected");}
 sealed class Poison : IProjectileDamage
 {
  public int Calls;public bool Frozen=true;
  public RuleResult Register(CommandContext c,string id,long serial,long expires,string instance,string owner,string kind,long generation){return RuleResult.Accept();}
  public RuleResult Radial(CommandContext c,string id,int sub,Float3 p,float r,float power,bool falloff,Target[] targets,RadialPolicy policy){Calls++;Frozen &=targets.Length==1&&targets[0]!=null&&targets[0].Id=="b";targets[0]=null;return RuleResult.Reject("pressure");}
 }
 static void Skip(BinaryReader r){int count=r.ReadInt32();r.ReadBytes(count);}
 static void BadInt(byte[] b,int offset,int value,Fixture f){byte[] changed=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(value),0,changed,offset,4);Reject(changed,f);}
 static void Main(string[] args)
 {
  string path=Path.Combine(args[0],"cold.bin");var f=new Fixture(true);
  if(args[1]=="write"){
   f.W.Contact=true;f.W.Targets=new[]{f.T("b"),f.T("c")};Need(f.Launch("smaw").Accepted,"launch");for(int i=0;i<4092;i++)Need(f.H.Advance(f.C("pressure/"+i)).Accepted,"pressure");f.Step();
   Need(f.H.Read("b").Shield==40&&f.H.Read("c").Shield==100&&f.S.PendingDeliveries("shot")==1,"actual partial per-target delivery");
   byte[] image=f.S.CaptureCold(f.C(),"build","catalog",7);File.WriteAllBytes(path,image);File.WriteAllText(path+".sha",Hash(image));
   File.WriteAllBytes(Path.Combine(args[0],"adapter-evidence.bin"),f.A.CaptureColdEvidence(f.C()));
   var cold=Restore(image,f);Need(cold.ColdQuarantined&&cold.PendingDeliveries("shot")==1,"fresh quarantine pending");Need(Hash(cold.CaptureCold(f.C(),"build","catalog",7))==Hash(image),"all fields exact byte roundtrip");
   f.W.Targets=new[]{f.T("a")};f.Steps(300);Need(f.H.Read("b").Shield==40&&f.H.Read("c").Shield==40&&f.H.Read("a").Shield==100,"original pending batch drains once without requery target");
   var poison=new Poison();f=new Fixture();f.W.Contact=true;f.W.Targets=new[]{f.T("b")};f.S=new ProjectileSimulation("s",3,"host",f.W.MapRevision,f.W,f.W,poison,RadialPolicy.StructuralLineOfSightStaged);Need(f.S.Launch(f.C(),"x",1,"w","a",1,"smaw",1,new Float3(),new Float3()).Accepted,"poison launch");f.Step();f.Step();Need(poison.Calls==2&&poison.Frozen,"untrusted port array cannot corrupt retained evidence");
   Console.WriteLine("WRITE "+n+" PASS");return;
  }
  byte[] b=File.ReadAllBytes(path);var s=Restore(b,f,File.ReadAllText(path+".sha"));Need(s.LogicalTick==1&&s.PendingDeliveries("shot")==1&&s.ColdQuarantined,"cold subprocess original pending state");
  long rev=f.H.Read("b").Revision;f.W.QueryFail=true;
  Need(!s.Advance(new CommandContext("s","host","resume",2,3,true)).Accepted&&s.LogicalTick==1,"quarantine refuses advance");
  Need(!s.Launch(new CommandContext("s","host","regrant",1,3,true),"extra",9,"w","a",1,"frag",1,new Float3(),new Float3()).Accepted,"quarantine refuses new launch");
  Need(f.H.Read("b").Revision==rev&&s.PendingDeliveries("shot")==1,"no contacts or Health mutations");
  Need(f.A.ValidateColdContinuation(f.C(),Hash(b)).Reason=="combat-launch-capability-restoration-unavailable","digest is not receipt");
  f.Tick=1;Need(Hash(s.CaptureCold(f.C(),"build","catalog",7))==Hash(b),"cold complete reexport");
  for(int cut=0;cut<b.Length;cut++){var t=new byte[cut];Array.Copy(b,t,cut);Reject(t,f);}
  // Malformed finite/clock/count fields with recomputed digests reach decoding.
  using(var reader=new BinaryReader(new MemoryStream(b))){
   reader.ReadInt32();reader.ReadInt32();Skip(reader);Skip(reader);reader.ReadInt64();Skip(reader);reader.ReadInt64();Skip(reader);Skip(reader);
   int cap=(int)reader.BaseStream.Position;BadInt(b,cap,Int32.MaxValue,f);reader.ReadInt32();reader.ReadInt32();int clock=(int)reader.BaseStream.Position;var negative=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(-1L),0,negative,clock,8);Reject(negative,f);reader.ReadInt64();Skip(reader);int countOffset=(int)reader.BaseStream.Position;reader.ReadInt32();int shotOffset=(int)reader.BaseStream.Position;
   byte[] duplicate=new byte[b.Length+b.Length-shotOffset];Array.Copy(b,duplicate,b.Length);Array.Copy(b,shotOffset,duplicate,b.Length,b.Length-shotOffset);Array.Copy(BitConverter.GetBytes(2),0,duplicate,countOffset,4);Reject(duplicate,f);
   Skip(reader);Skip(reader);Skip(reader);Skip(reader);Skip(reader);reader.ReadInt64();reader.ReadInt64();reader.ReadInt64();reader.ReadInt32();int position=(int)reader.BaseStream.Position;
   var invalidBool=(byte[])b.Clone();invalidBool[position+44]=2;Reject(invalidBool,f);
   BadInt(b,position,0x7fc00000,f);BadInt(b,position,0x7f800000,f);
  }
  var trailing=new byte[b.Length+1];Array.Copy(b,trailing,b.Length);Reject(trailing,f);
  var future=(byte[])b.Clone();future[4]=2;Reject(future,f);
  var utf8=(byte[])b.Clone();utf8[12]=255;Reject(utf8,f);
  Reject(new byte[4*1024*1024+1],f);
  var corrupt=(byte[])b.Clone();corrupt[0]^=1;Reject(corrupt,f);
  bool denied=false;try{Restore(b,f,new string('0',64));}catch(InvalidDataException){denied=true;}Need(denied,"wrong trusted digest");
  denied=false;try{Restore(b,f,null,"foreign");}catch(InvalidDataException){denied=true;}Need(denied,"wrong build");
  denied=false;try{Restore(b,f,null,"build",8);}catch(InvalidDataException){denied=true;}Need(denied,"stale generation");
  Need(Hash(b)==File.ReadAllText(path+".sha"),"input preserved");Console.WriteLine("READ "+n+" PASS");
 }
}
