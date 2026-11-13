using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
class R3Tests
{
 sealed class Gate:IAuthorityGate {public bool Allow=true; public RuleResult Validate(CommandContext c){return Allow?RuleResult.Accept():RuleResult.Reject("denied");}}
 sealed class Ammo:IAmmoReserve {public int Count=30,Takes,Refunds;public int Available{get{return Count;}} public int Take(int n){Takes++;int a=Math.Min(n,Count);Count-=a;return a;}public int Refund(int n){Refunds++;Count+=n;return n;}}
 sealed class Approval:IWeaponLifetimeAuthority {public bool Allow=true;public RuleResult AuthorizeNew(CommandContext c,string id,WeaponMechanism m,int n){return Allow?RuleResult.Accept():RuleResult.Reject("denied");}public RuleResult AuthorizeRestore(CommandContext c,string id,string a,long e){return AuthorizeNew(c,id,0,0);}}
 sealed class Rig {
  public WeaponHandling W;public Ammo A=new Ammo();public Gate G=new Gate();public Approval P=new Approval();public WeaponLifetimes L;public long N,Epoch=1;public HandlingInput I=new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,ForegripDistance=1};
  public Rig(WeaponMechanism m,int rounds){L=new WeaponLifetimes("s",P);Check(L.CreateNew(C(),"item",m,rounds,A,G,out W).Accepted,"grant");}
  public CommandContext C(){return new CommandContext("s","a","c"+N,N,Epoch,true);}
  public bool Do(HandlingAction a,float dt){N++;I.Sequence=N;I.DeltaSeconds=dt;return W.Apply(C(),a,I).Accepted;}
  public void At(float x,float y,float z,bool grip){I.SupportLocal=new Float3(x,y,z);I.Grip=grip;Do(HandlingAction.Sample,0);}
  public void Transfer(){N++;WeaponTransfer t;var old=W;Check(L.Capture(C(),"item",out t).Accepted,"capture");N++;Epoch++;Check(L.Restore(C(),"item",t,out W).Accepted,"restore");N++;I.Sequence=N;Check(!old.Apply(C(),HandlingAction.Fire,I).Accepted,"old retired");WeaponHandling ignored;Check(!L.Restore(C(),"item",t,out ignored).Accepted,"receipt consumed");}
 }
 static int count;
 static void Check(bool b,string s){if(!b)throw new Exception(s);count++;}
 static bool Near(float a,float b){return Math.Abs(a-b)<.000002f;}
 static void Main(){
  foreach(WeaponMechanism m in new[]{WeaponMechanism.ServiceRifle,WeaponMechanism.PrecisionRifle,WeaponMechanism.MagnumRevolver})
  for(int unavailable=0;unavailable<5;unavailable++){
   var r=new Rig(m,WeaponHandling.MagazineCapacity(m));r.I.Trigger=true;Check(r.Do(HandlingAction.Fire,0),"first shot");var before=r.W.Snapshot;
   if(unavailable==0)r.I.PrimaryTracked=false;if(unavailable==1)r.I.Enabled=false;if(unavailable==2)r.I.PrimaryClimbing=true;if(unavailable==3)r.I.HeadObstructed=true;if(unavailable==4)r.I.Paused=true;
   Check(!r.Do(HandlingAction.Fire,.25f),"unavailable cannot fire");var after=r.W.Snapshot;
   Check(Near(after.CooldownSeconds,unavailable==4?before.CooldownSeconds:Math.Max(0,before.CooldownSeconds-.25f)),"correct recovery clock");
   Check(unavailable==4?Near(after.RecoilDistance,before.RecoilDistance):after.RecoilDistance<before.RecoilDistance,"recoil clock");
   r.I.PrimaryTracked=r.I.Enabled=true;r.I.PrimaryClimbing=r.I.HeadObstructed=r.I.Paused=false;
   Check(!r.Do(HandlingAction.Fire,0),"held trigger remains disarmed");r.I.Trigger=false;r.Do(HandlingAction.Sample,0);
   if(m==WeaponMechanism.ServiceRifle&&unavailable!=4){r.I.Trigger=true;Check(r.Do(HandlingAction.Fire,0),"rearmed after lost tracking");Check(Near(r.W.Snapshot.CooldownSeconds,.115f),"no banked overrun");}
  }
  foreach(bool left in new[]{false,true}){
   var r=new Rig(WeaponMechanism.MagnumRevolver,7);r.I.LeftHanded=left;r.I.Desktop=false;
   r.I.Trigger=true;Check(r.Do(HandlingAction.Fire,0),"magnum shot");Check(!r.Do(HandlingAction.Fire,.25f),"semi held rejects");r.I.Trigger=false;r.Do(HandlingAction.Sample,.1f);r.I.Trigger=true;Check(r.Do(HandlingAction.Fire,0),"semi release rearms at catalog 1/3 interval");
   Check(r.Do(HandlingAction.Eject,0),"open");Check(r.W.Snapshot.Loaded==5&&r.A.Refunds==0,"opening retains live rounds");
   float sign=left?-1:1;r.At(-.18f*sign,.095f,-.13f,false);r.At(-.18f*sign,.095f,-.13f,true);
   r.At(-.10f*sign,.035f,-.071f,true);Check(r.W.Snapshot.Phase==ReloadPhase.CylinderOpen,"short stroke rejects");
   r.At(-.10f*sign,.035f,.10f,true);Check(r.W.Snapshot.Phase==ReloadPhase.CylinderLoaded&&r.W.Snapshot.Loaded==7&&r.A.Count==28,"forward stroke tops missing rounds only");
   r.At(-.10f*sign,.10f,.10f,true);Check(r.W.Snapshot.Phase==ReloadPhase.CylinderLoaded,"same grip cannot close");
   r.At(-.10f*sign,.035f,.10f,false);r.At(-.10f*sign,.035f,.10f,true);
   r.Transfer();Check(r.W.Snapshot.Phase==ReloadPhase.CylinderLoaded&&r.W.Snapshot.Loaded==7,"transfer cylinder preserves ammo");
   r.At(-.10f*sign,.10f,.10f,true);Check(!r.W.Snapshot.Chambered,"transfer cancels close contact");
   r.At(-.10f*sign,.035f,.10f,false);r.At(-.10f*sign,.035f,.10f,true);r.At(-.10f*sign,.09f,.10f,true);
   Check(r.W.Snapshot.Chambered&&r.W.Snapshot.Phase==ReloadPhase.Ready,"fresh upward close");Check(!r.Do(HandlingAction.Fire,0),"close disarms trigger");
   r.I.Trigger=false;r.Do(HandlingAction.Sample,0);r.I.Trigger=true;Check(!r.Do(HandlingAction.Fire,.119f),"close delay");Check(r.Do(HandlingAction.Fire,.001f),"close delay boundary");
   Check(r.A.Count+r.W.Snapshot.Loaded+r.W.Snapshot.Shots==37,"ammo conservation including transfer");
  }
  var empty=new Rig(WeaponMechanism.MagnumRevolver,0);empty.A.Count=0;Check(!empty.Do(HandlingAction.ReloadStep,0)&&empty.W.Snapshot.Loaded==0,"empty reserve no fabricated rounds");empty.A.Count=2;Check(empty.Do(HandlingAction.ReloadStep,0)&&empty.W.Snapshot.Loaded==2,"partial fill");Check(empty.Do(HandlingAction.ReloadStep,0),"desktop close");
  for(int j=0;j<2;j++){empty.I.Trigger=false;empty.Do(HandlingAction.Sample,.25f);empty.I.Trigger=true;Check(empty.Do(HandlingAction.Fire,.12f),"partial cylinder fires");}
  Check(empty.W.Snapshot.Phase==ReloadPhase.CylinderOpen&&empty.W.Snapshot.Loaded==0,"empty auto opens");empty.Transfer();Check(empty.W.Snapshot.Phase==ReloadPhase.CylinderOpen,"open transfer");
  var deny=new WeaponLifetimes("s",new Approval{Allow=false});WeaponHandling w;Check(!deny.CreateNew(new CommandContext("s","a","new",0,1,true),"i",WeaponMechanism.MagnumRevolver,7,new Ammo(),new Gate(),out w).Accepted,"grant denied");Check(!deny.CreateNew(new CommandContext("s","a","new",0,1,true),"i",WeaponMechanism.MagnumRevolver,8,new Ammo(),new Gate(),out w).Accepted,"capacity bounded");
  foreach(int rounds in new[]{0,1,5,7}){
   var r=new Rig(WeaponMechanism.MagnumRevolver,rounds);int total=rounds+r.A.Count;
   if(rounds>0){r.Transfer();Check(r.W.Snapshot.Loaded==rounds&&r.W.Snapshot.Chambered,"ready transfer");r.I.Trigger=false;r.Do(HandlingAction.Sample,0);Check(r.Do(HandlingAction.Eject,0),"open after transfer");}
   r.G.Allow=false;Check(!r.Do(HandlingAction.ReloadStep,.25f)&&r.A.Count==30,"gate rejects reserve mutation");r.G.Allow=true;
   r.I.SupportReserved=true;Check(!r.Do(HandlingAction.ReloadStep,0)&&r.W.Snapshot.Loaded==rounds,"reserved support cannot load");r.I.SupportReserved=false;
   r.I.Grip=false;r.Do(HandlingAction.Sample,0);Check(r.Do(HandlingAction.ReloadStep,0),"load after release");
   Check(r.W.Snapshot.Loaded+r.A.Count==total&&r.A.Refunds==0,"topup conserves original rounds");
   r.P.Allow=false;r.N++;WeaponTransfer t;Check(r.L.Capture(r.C(),"item",out t).Accepted,"capture before denied restore");r.N++;r.Epoch++;Check(!r.L.Restore(r.C(),"item",t,out w).Accepted,"denied restore preserves receipt");r.P.Allow=true;r.N++;Check(r.L.Restore(r.C(),"item",t,out r.W).Accepted,"retry restore once approved");
   Check(r.W.Snapshot.Loaded+r.A.Count==total,"denied restoration did not mint rounds");
  }
  for(int mode=0;mode<3;mode++){
   var r=new Rig(WeaponMechanism.MagnumRevolver,2);r.I.Desktop=false;r.Do(HandlingAction.Eject,0);
   r.At(-.18f,.095f,-.13f,false);r.At(-.18f,.095f,-.13f,true);r.At(-.1f,.035f,.1f,true);
   r.At(-.1f,.035f,.1f,false);r.At(-.1f,.035f,.1f,true);
   if(mode==0)r.I.SupportTracked=false;if(mode==1)r.I.SupportReserved=true;if(mode==2)r.I.PrimaryTracked=false;
   r.Do(HandlingAction.Sample,.25f);r.I.SupportTracked=r.I.PrimaryTracked=true;r.I.SupportReserved=false;
   r.At(-.1f,.10f,.1f,true);Check(!r.W.Snapshot.Chambered,"interrupted close needs fresh grip");
   r.At(-.1f,.035f,.1f,false);r.At(-.1f,.035f,.1f,true);r.At(-.1f,.09f,.1f,true);Check(r.W.Snapshot.Chambered,"fresh contact completes after interruption");
  }
  Console.WriteLine("PASS R3 checks="+count);
 }
}
