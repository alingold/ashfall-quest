using System;
using Ashfall.Core;
using Ashfall.RecoveryContracts;
using Ashfall.Modules.WeaponHandling;
class Tests
{
 static int checks,commands;static long tick,sequence;
 static void A(bool b,string s){checks++;if(!b)throw new Exception(s);}
 sealed class Host:IAuthorityGate,ISourceWeaponLifetimeAuthority,ISourceLaunchBinding
 {
  public bool Allow=true;public long Life=1,Connection=1,Equip=1; public int Calls;
  public RuleResult Validate(CommandContext c){Calls++;return Allow?RuleResult.Accept():RuleResult.Reject("denied");}
  public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Validate(c);}
  public RuleResult AuthorizeSourceNew(CommandContext c,string id,string profile,int n,int r){return Validate(c);}
  public RuleResult AuthorizeRestore(CommandContext c,string id,string actor,long epoch){return Validate(c);}
  public RuleResult Validate(CommandContext c,string id,long equip,long life,long connection,long frame){Calls++;return Allow&&equip==Equip&&life==Life&&connection==Connection?RuleResult.Accept():RuleResult.Reject("binding");}
  public RuleResult ValidateSource(CommandContext c,string id,HandlingInput input,double seconds,SourceLaunchGeometry g){return Validate(c);}
 }
 static CommandContext C(string actor="a",long epoch=1){return new CommandContext("s",actor,"c"+(++commands),++tick,epoch,true);}
 static HandlingInput I(bool trigger=false,bool grip=false,Float3 point=new Float3()) {return new HandlingInput{Sequence=++sequence,Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=trigger,Grip=grip,SupportLocal=point};}
 static SourceLaunchGeometry G(){return new SourceLaunchGeometry("pinned-source",new Float3(1,2,3),new Float3(0,0,1),new Float3(1,0,0),2,9.8f,20,1);}
 static SourceExplosiveSnapshot S(WeaponHandling w){SourceExplosiveSnapshot s;A(w.TryReadSourceExplosive(out s),"source read");return s;}
 static WeaponHandling New(WeaponLifetimes l,Host h,string id,string profile){WeaponHandling w;A(l.CreateSourceExplosive(C(),id,profile,h,out w).Accepted,"create source "+profile);return w;}
 static bool Prepare(WeaponLifetimes l,Host h,string id,bool rocket,double seconds,out IPreparedSourceLaunch p,HandlingInput input)
 {var c=C();return rocket?l.PrepareSourceRocketFire(c,id,new SourceRocketInput{Frame=input,SimulationSeconds=seconds,Geometry=G()},h.Equip,h.Life,h.Connection,c.Tick+10,h,out p).Accepted:l.PrepareSourceThrow(c,id,new SourceThrowInput{Frame=input,SimulationSeconds=seconds,Geometry=G()},h.Equip,h.Life,h.Connection,c.Tick+10,h,out p).Accepted;}
 static void Rearm(WeaponHandling w,double seconds,string actor="a",long epoch=1){A(w.SampleSourceExplosive(C(actor,epoch),I(),seconds,G()).Accepted,"neutral rearm");}
 static void Fire(WeaponLifetimes l,Host h,WeaponHandling w,string id,bool rocket,double seconds)
 {
  IPreparedSourceLaunch p;A(Prepare(l,h,id,rocket,seconds,out p,I(true)),"prepare launch");int n=S(w).Loaded;
  A(p.Descriptor!=null&&p.Descriptor.After.Loaded==n-1&&S(w).Loaded==n,"descriptor before publication");
  A(p.Validate(tick).Accepted,"validate");int calls=h.Calls;p.CommitValidated();p.CommitValidated();p.Abort();
  A(p.Committed&&S(w).Loaded==n-1&&h.Calls==calls,"commit once no callbacks");
  if(!rocket)A(p.Descriptor.Velocity.Y>0&&p.Descriptor.Velocity.Z>0,"aimed upward throw");
 }
 static void Main()
 {
  foreach(string profile in new[]{"frag","zone"})
  {
   var h=new Host();var l=new WeaponLifetimes("s",h);var w=New(l,h,"x",profile);IPreparedSourceLaunch p;
   A(!Prepare(l,h,"x",false,0,out p,I(true)),"starts requiring release");Rearm(w,0);
   A(!Prepare(l,h,"x",true,0,out p,I(true)),"wrong family");
   A(Prepare(l,h,"x",false,0,out p,I(true)),"abort prepare");
   IPreparedWeaponTransfer transfer;IPreparedWeaponSuspension suspension;IPreparedWeaponFire generic;
   A(!l.PrepareTransfer(C(),"x","b",2,out transfer).Accepted,"transfer excluded");
   A(!w.PrepareSuspension(out suspension).Accepted,"suspension excluded");
   A(!l.PrepareFire(C(),"x",I(true),1,1,1,tick+2,h,out generic).Accepted,"generic fire rejects");
   A(!w.SampleSourceExplosive(C(),I(),0,G()).Accepted,"sample excluded");
   p.Abort();p.Abort();A(S(w).Loaded==2,"abort no debit");Fire(l,h,w,"x",false,0);
   A(!Prepare(l,h,"x",false,1,out p,I(true)),"held no repeat");Rearm(w,.1);
   A(!Prepare(l,h,"x",false,.349,out p,I(true)),"cooldown");Fire(l,h,w,"x",false,.35);
   Rearm(w,1);A(!Prepare(l,h,"x",false,1,out p,I(true)),"two only");
   A(!l.CreateSourceExplosive(C(),"x",profile,h,out w).Accepted,"no repeat grant");
  }
  {
   var h=new Host();var l=new WeaponLifetimes("s",h);var w=New(l,h,"rocket","smaw");
   Rearm(w,0);Fire(l,h,w,"rocket",true,0);
   for(int n=0;n<2;n++)
   {
    double time=n+1;Rearm(w,time);
    A(w.SampleSourceExplosive(C(),I(false,true,new Float3(0,.045f,.8452f)),time,G()).Accepted,"offer grab");
    A(S(w).HeldRocket&&S(w).Reloads==2-n,"grab no debit");
    A(w.SampleSourceExplosive(C(),I(false,true,new Float3(.3f,.045f,.76f)),time,G()).Accepted,"bad radial sample");
    A(S(w).Loaded==0,"bad insertion no debit");
    A(w.SampleSourceExplosive(C(),I(false,true,new Float3(0,.045f,.76f)),time,G()).Accepted,"bore push");
    A(S(w).Loaded==1&&S(w).Reloads==1-n,"insertion debits once");
    IPreparedSourceLaunch early;Rearm(w,time);A(!Prepare(l,h,"rocket",true,time,out early,I(true)),"insertion stage cooldown");Rearm(w,time+.121);Fire(l,h,w,"rocket",true,time+.121);
   }
   Rearm(w,4);w.SampleSourceExplosive(C(),I(false,true,new Float3(0,.045f,.8452f)),4,G());A(!S(w).HeldRocket&&S(w).Reloads==0,"finite exhausted");
  }
  {
   var h=new Host();var l=new WeaponLifetimes("s",h);var w=New(l,h,"x","frag");Rearm(w,0);IPreparedSourceLaunch p;
   A(Prepare(l,h,"x",false,0,out p,I(true)),"identity token");h.Connection=2;A(!p.Validate(tick).Accepted,"reconnect stale");p.CommitValidated();A(S(w).Loaded==2,"stale no debit");p.Abort();h.Connection=1;
   A(Prepare(l,h,"x",false,0,out p,I(true)),"life prepare");h.Life=2;A(!p.Validate(tick).Accepted,"life stale");p.Abort();h.Life=1;
   A(Prepare(l,h,"x",false,0,out p,I(true)),"equip prepare");h.Equip=2;A(!p.Validate(tick).Accepted,"equip stale");p.Abort();h.Equip=1;
   A(Prepare(l,h,"x",false,0,out p,I(true)),"expiry prepare");A(!p.Validate(tick+11).Accepted,"expiry rejects");p.CommitValidated();A(S(w).Loaded==2,"expiry no debit");
   A(Prepare(l,h,"x",false,0,out p,I(true)),"retry");p.Abort();
   IPreparedWeaponSuspension suspend;A(w.PrepareSuspension(out suspend).Accepted,"suspend reserve");A(!Prepare(l,h,"x",false,0,out p,I(true)),"suspend excludes launch");A(suspend.Validate().Accepted,"suspend validate");suspend.CommitValidated();A(!Prepare(l,h,"x",false,0,out p,I(true)),"suspend needs release");Rearm(w,0);Fire(l,h,w,"x",false,0);
   IPreparedWeaponTransfer t;A(l.PrepareTransfer(C(),"x","b",2,out t).Accepted,"transfer prepare");A(!Prepare(l,h,"x",false,1,out p,I(true)),"transfer excludes launch");A(t.Validate().Accepted,"transfer validate");t.CommitValidated();var next=t.Result.Handler;A(S(next).Loaded==1&&S(next).Shots==1,"transfer conserved");
   A(!w.SampleSourceExplosive(C(),I(),1,G()).Accepted,"old instance retired");Rearm(next,1,"b",2);
   bool refused=false;try{WeaponCheckpointCodec.Capture(next,new CheckpointBinding("build","catalog","s","b",2,1,1,1),"p");}catch(NotSupportedException){refused=true;}A(refused,"schema1 explicitly refuses source");
   A(!next.SampleSourceExplosive(C("b",2),I(),Double.NaN,G()).Accepted,"nan clock");
   var malformed=new SourceLaunchGeometry("id",new Float3(),new Float3(float.NaN,0,1),new Float3(1,0,0),2,9.8f,20,1);
   A(!next.SampleSourceExplosive(C("b",2),I(),1,malformed).Accepted,"nonfinite geometry");
  }
  {
   var h=new Host();var l=new WeaponLifetimes("s",h);WeaponHandling w;
   A(l.CreateNew(C(),"legacy",WeaponMechanism.ServiceRifle,4,new FiniteAmmoReserve(8,8),h,out w).Accepted,"legacy grant");
   var b=new CheckpointBinding("build","catalog","s","a",1,1,1,1);var bytes=WeaponCheckpointCodec.Capture(w,b,"reserve");FiniteAmmoReserve reserve;
   var restored=WeaponCheckpointCodec.Restore(bytes,b,"reserve",h,out reserve);
   A(restored.Snapshot.Loaded==4&&reserve.Available==8,"schema1 firearm roundtrip");
   byte[] future=(byte[])bytes.Clone();future[4]=2;bool refused=false;try{WeaponCheckpointCodec.Restore(future,b,"reserve",h,out reserve);}catch(System.IO.InvalidDataException){refused=true;}A(refused,"unknown schema refuses");
   IPreparedWeaponSuspension suspension;A(w.PrepareSuspension(out suspension).Accepted,"legacy suspend prepare");A(suspension.Validate().Accepted,"legacy validated");
   refused=false;try{WeaponCheckpointCodec.Capture(w,b,"reserve");}catch(System.IO.InvalidDataException){refused=true;}A(refused,"validated pending capture refuses");suspension.Abort();
   A(WeaponCheckpointCodec.Capture(w,b,"reserve").Length==bytes.Length,"abort releases checkpoint exclusion");
  }
  Console.WriteLine("PASS "+checks+" public observations");
 }
}
