using System;
using System.IO;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.Combat;
static class ArchiveTests
{
 sealed class Ports:IAuthorityGate,IInteractionQuery,ICombatEvidence
 {
  public RuleResult Validate(CommandContext c){return c.SessionId=="s"&&c.AuthorityEpoch==3&&c.ActorId=="host"&&c.IsAuthority?RuleResult.Accept():RuleResult.Reject("auth");}
  public bool HasLineOfSight(string a,string b,Float3 p,Float3 q,float range){return true;}
  public bool TryResolve(CommandContext c,string id,out HitEvidence e){e=null;return false;}
 }
 static Ports ports=new Ports();static int n;
 static CommandContext C(){return new CommandContext("s","host","launch",1,3,true);}
 static void Need(bool b,string message){if(!b)throw new Exception(message);n++;}
 static void Reject(Action action,string message){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}catch(IOException){rejected=true;}catch(ArgumentException){rejected=true;}catch(InvalidOperationException){rejected=true;}Need(rejected,message);}
 static CombatRules Rules(int capacity=64){return new CombatRules("s",3,256,capacity,ports,ports,ports);}
 static byte[] Capture(CombatRules r){return r.CapturePendingProjectileArchive("build","catalog",7,new HostHit[0]);}
 static CombatRules.PendingProjectileArchive Restore(byte[] b,string digest=null,string build="build",string catalog="catalog",long generation=7,string session="s",long epoch=3){return CombatRules.RestorePendingProjectileArchive(b,digest??RecoveryWire.Hash(b),build,catalog,generation,session,epoch,256,64,ports,ports,ports,"host");}
 static LaunchedProjectileCapability Prepare(CombatRules r,WeaponLease w,string id="launch",string command="shot"){
  LaunchedProjectileCapability p;Need(CombatRules.PrepareLaunchedProjectile(C(),w,id,command,9,11,13,100,2,DamageKind.Area,new SourceRadialProvenance("source","catalog",new string('a',64)),out p).Accepted,"prepare actual central reservation");return p;
 }
 static void Main(string[] args){
  if(args[0]=="write"){
   var r=Rules();var w=r.Equip(C(),"w","owner","frag",1,1);var p=Prepare(r,w);
   Reject(()=>Capture(r),"prepared token blocks capture");Need(p.Validate(C()).Accepted,"validate actual prepared");Reject(()=>Capture(r),"validated prepared still blocks capture");
   p.CommitValidated();r.RetireWeapon(C(),w,1);byte[] b=Capture(r);File.WriteAllBytes(args[1],b);
   var restored=Restore(b);var x=restored.Launches[0];Need(x.LaunchId==p.LaunchId&&x.InstanceId==p.InstanceId&&x.OwnerId==p.OwnerId&&x.SourceCommand==p.SourceCommand&&x.EquipGeneration==1&&x.LifeGeneration==9&&x.ConnectionGeneration==11&&x.FrameSequence==13&&x.ExpiresTick==100&&x.MaximumPhysicalSubhits==2&&x.Committed,"exact retired pending launch identity");
   LaunchedProjectileCapability duplicate;Need(!CombatRules.PrepareLaunchedProjectile(C(),w,"other","shot",9,11,13,100,1,DamageKind.Area,new SourceRadialProvenance("source","catalog",new string('a',64)),out duplicate).Accepted,"retired cannot create new launch");
   Reject(()=>{using(var m=new MemoryStream())using(var writer=new BinaryWriter(m))r.CaptureArchive(writer,new HostHit[0]);},"legacy pending capture still refuses");
   Need(!x.Validate(C()).Accepted,"quarantine no validate grant");Reject(()=>x.PublishImpact(C(),0,new Float3(),10,5,SourceRadialFalloff.Constant),"quarantine no physical publication");x.CommitValidated();x.Abort();Need(RecoveryWire.Hash(restored.Recapture())==RecoveryWire.Hash(b),"quarantine operations preserve exact archive");
   p.PublishImpact(C(),0,new Float3(),10,5,SourceRadialFalloff.Constant);Reject(()=>Capture(r),"realized policy cannot silently disappear");
   r=Rules(3);w=r.Equip(C(),"pressure","owner","frag",1,1);p=Prepare(r,w);Need(p.Validate(C()).Accepted,"pressure validate");p.CommitValidated();Need(Capture(r).Length>0,"full slot capacity captured");Need(!CombatRules.PrepareLaunchedProjectile(C(),w,"extra","extra",1,1,1,100,1,DamageKind.Bullet,null,out duplicate).Accepted,"capacity not reset by capture");
   r=Rules();w=r.Equip(C(),"abort","owner","frag",1,1);p=Prepare(r,w);p.Abort();Need(Restore(Capture(r)).Launches.Length==0,"abort never restored as permission");
  }else{
   byte[] b=File.ReadAllBytes(args[1]);var archive=Restore(b);Need(archive.Launches.Length==1,"fresh process reconstructed one owner launch");Need(RecoveryWire.Hash(archive.Recapture())==RecoveryWire.Hash(b),"cold exact owner recapture");
   Reject(()=>Restore(b,new string('0',64)),"wrong independent digest");Reject(()=>Restore(b,null,"wrong"),"wrong build");Reject(()=>Restore(b,null,"build","wrong"),"wrong catalog");Reject(()=>Restore(b,null,"build","catalog",8),"wrong generation");Reject(()=>Restore(b,null,"build","catalog",7,"other"),"mixed session");Reject(()=>Restore(b,null,"build","catalog",7,"s",4),"mixed epoch");
   for(int i=0;i<b.Length;i++){byte[] cut=new byte[i];Array.Copy(b,cut,i);Reject(()=>Restore(cut),"truncated "+i);}
   var future=(byte[])b.Clone();future[4]=2;Reject(()=>Restore(future),"unknown format");var trailing=new byte[b.Length+1];Array.Copy(b,trailing,b.Length);Reject(()=>Restore(trailing),"trailing data");
   var alternate=new byte[b.Length+1];Array.Copy(b,0,alternate,0,8);alternate[8]=(byte)(b[8]|128);alternate[9]=0;Array.Copy(b,9,alternate,10,b.Length-9);Reject(()=>Restore(alternate),"overlong noncanonical string prefix");
   int record=-1;byte[] signature=System.Text.Encoding.UTF8.GetBytes("launch");
   for(int i=0;i<b.Length-signature.Length;i++){bool same=b[i]==6;for(int j=0;j<signature.Length&&same;j++)same=b[i+1+j]==signature[j];if(same)record=i;}
   Need(record>0,"actual capability record located");
   using(var reader=new BinaryReader(new MemoryStream(b,false))){reader.BaseStream.Position=record;reader.ReadString();reader.ReadString();int life=(int)reader.BaseStream.Position;reader.ReadInt64();int connection=(int)reader.BaseStream.Position;reader.ReadInt64();int frame=(int)reader.BaseStream.Position;reader.ReadInt64();int observed=(int)reader.BaseStream.Position;reader.ReadInt64();int boolean=(int)reader.BaseStream.Position;reader.ReadBoolean();reader.ReadString();reader.ReadString();int digest=(int)reader.BaseStream.Position+1;reader.ReadString();int count=(int)reader.BaseStream.Position;reader.ReadInt32();int slots=(int)reader.BaseStream.Position;
    foreach(int position in new[]{life,connection,frame}){var bad=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(-1L),0,bad,position,8);Reject(()=>Restore(bad),"negative capability generation");}
    var futureObserved=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(2L),0,futureObserved,observed,8);Reject(()=>Restore(futureObserved),"future validation observation");
    var badBool=(byte[])b.Clone();badBool[boolean]=2;Reject(()=>Restore(badBool),"noncanonical provenance bool");
    var upper=(byte[])b.Clone();upper[digest]=(byte)'A';Reject(()=>Restore(upper),"noncanonical content digest case");
    var shortCount=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(1),0,shortCount,count,4);Reject(()=>Restore(shortCount),"invalid reserved capacity");
    var duplicate=(byte[])b.Clone();Array.Copy(b,slots,duplicate,slots+8,8);Reject(()=>Restore(duplicate),"duplicate reserved slot alias");
    var foreign=(byte[])b.Clone();Array.Copy(BitConverter.GetBytes(1000L),0,foreign,slots,8);Reject(()=>Restore(foreign),"foreign missing event capability");
    var mixed=(byte[])b.Clone();mixed[record+1]=(byte)'x';Reject(()=>Restore(mixed),"capability marker mixed identity");
   }
   var caps=archive.Launches;caps[0]=null;Need(archive.Launches[0]!=null,"defensive launch array");Need(!archive.Launches[0].Validate(C()).Accepted,"cold quarantine validation");Reject(()=>archive.Launches[0].PublishImpact(C(),0,new Float3(),10,5,SourceRadialFalloff.Constant),"cold cannot use capability");
  }
  Console.WriteLine("PASS ArchiveTests "+args[0]+" observations="+n);
 }
}
