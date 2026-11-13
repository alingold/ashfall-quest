using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
class R2Tests
{
 sealed class Gate:IAuthorityGate { public RuleResult Validate(CommandContext c){return RuleResult.Accept();} }
 sealed class Ammo:IAmmoReserve { public int Count=100,Room=1000; public int Available{get{return Count;}} public int Take(int n){int a=Math.Min(n,Count);Count-=a;return a;} public int Refund(int n){int a=Math.Min(n,Room);Count+=a;Room-=a;return a;} }
 sealed class Rig {
  public Ammo A=new Ammo();public WeaponHandling W;public WeaponLifetimes L;public TestLifetimeApproval Approval=new TestLifetimeApproval();public long N; public long Epoch=1; public string Actor="a"; public HandlingInput I=new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Desktop=true,ForegripDistance=1};
  public Rig(bool p){L=new WeaponLifetimes("s",Approval);Check(L.CreateNew(C("spawn"),"stable",p?WeaponMechanism.PrecisionRifle:WeaponMechanism.ServiceRifle,p?3:24,A,new Gate(),out W).Accepted,"authorized initial magazine");}
  public CommandContext C(string id){return new CommandContext("s",Actor,id,N,Epoch,true);}
  public RuleResult Do(HandlingAction a,float dt){N++;I.Sequence=N;I.DeltaSeconds=dt;return W.Apply(C("c"+N),a,I);}
  public void At(float x,float y,float z,bool grip){I.SupportLocal=new Float3(x,y,z);I.Grip=grip;Do(HandlingAction.Sample,0);}
  public WeaponTransfer Capture(){N++;WeaponTransfer t;Check(L.Capture(C("cap"+N),"stable",out t).Accepted,"capture stable lifetime");return t;}
  public void Restore(WeaponTransfer t){N++;Epoch++;Check(L.Restore(C("restore"+N),"stable",t,out W).Accepted,"approved epoch restore");}
 }
 static int checks;
 static void Check(bool b,string name){if(!b)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static bool Near(float a,float b){return Math.Abs(a-b)<.000002f;}
 static void Cycle(bool p,bool physical){var r=new Rig(p);r.Do(HandlingAction.ReloadStep,0);r.Do(HandlingAction.ReloadStep,0);if(physical){r.I.Desktop=false;if(p){r.At(.072f,.045f,.07f,false);r.At(.072f,.045f,.07f,true);r.At(.072f,.045f,-.05f,true);}else{r.At(-.045f,.064f,.1f,false);r.At(-.045f,.064f,.1f,true);r.At(-.045f,.064f,.02f,true);}}else r.Do(HandlingAction.ReloadStep,0);
  r.I.Trigger=false;r.Do(HandlingAction.Sample,0);r.I.Trigger=true;Check(!r.Do(HandlingAction.Fire,0).Accepted,"zero post-cycle fire rejected p="+p+" physical="+physical);Check(!r.Do(HandlingAction.Fire,.119f).Accepted,"119ms post-cycle rejected");Check(r.Do(HandlingAction.Fire,.001f).Accepted,"120ms post-cycle accepted");}
 static void Main(){
  Cycle(false,false);Cycle(false,true);Cycle(true,false);Cycle(true,true);
  foreach(float stroke in new float[]{0,.2f,1f}){var r=new Rig(true);r.I.Trigger=true;r.Do(HandlingAction.Fire,0);r.I.Trigger=false;r.Do(HandlingAction.Sample,stroke==1?.25f:stroke);if(stroke==1){r.Do(HandlingAction.Sample,.25f);r.Do(HandlingAction.Sample,.25f);r.Do(HandlingAction.Sample,.25f);}r.Do(HandlingAction.ReloadStep,0);float expected=stroke==1?.12f:Math.Max(.12f,.95f-stroke);Check(Near(r.W.Snapshot.CooldownSeconds,expected),"precision cycle preserves max existing .95 and .12 slow="+stroke);r.I.Trigger=true;Check(!r.Do(HandlingAction.Fire,0).Accepted,"precision cannot fire immediately after cycle");}
  foreach(float step in new float[]{.01f,.02f,.025f,.05f,.1f}){var r=new Rig(false);r.I.Trigger=true;r.Do(HandlingAction.Fire,0);for(int j=0;j<(int)Math.Round(1/step);j++)r.Do(HandlingAction.Fire,step);Check(r.W.Snapshot.Shots==9,"source-derived cadence at sample="+step);r.I.Sequence=++r.N;r.I.DeltaSeconds=.25f;long before=r.W.Snapshot.Shots;Check(!r.W.Apply(new CommandContext("s","a","same",r.N-1,1,true),HandlingAction.Fire,r.I).Accepted&&r.W.Snapshot.Shots==before,"same tick positive time rejected");}
  var pause=new Rig(false);pause.I.Trigger=true;pause.Do(HandlingAction.Fire,0);float cd=pause.W.Snapshot.CooldownSeconds;pause.I.Paused=true;for(int j=0;j<20;j++)pause.Do(HandlingAction.Sample,.25f);Check(Near(pause.W.Snapshot.CooldownSeconds,cd),"paused time not banked");pause.I.Paused=false;pause.I.Trigger=false;pause.Do(HandlingAction.Sample,0);pause.I.Trigger=true;Check(!pause.Do(HandlingAction.Fire,0).Accepted,"pause resume no accelerated shot");pause.Do(HandlingAction.Fire,.115f);Check(pause.W.Snapshot.Shots==2,"fresh active time fires one shot");
  // Every mechanical phase, plus pending/spent/partial-refund and hand-contact variants.
  for(int mode=0;mode<10;mode++){
   bool p=mode>=5;var r=new Rig(p);r.I.Trigger=true;r.Do(HandlingAction.Fire,0);r.I.Trigger=false;r.Do(HandlingAction.Sample,0);
   int scenario=mode%5;
   if(scenario==1){if(p){r.Do(HandlingAction.Eject,0);r.Do(HandlingAction.ReloadStep,0);}else r.Do(HandlingAction.Eject,0);}
   if(scenario==2||scenario==3){if(p){r.Do(HandlingAction.Eject,0);r.Do(HandlingAction.ReloadStep,0);}else r.Do(HandlingAction.Eject,0);r.I.Desktop=false;r.At(0,-.278f,.15f,true);if(scenario==3){r.At(0,-.18f,.15f,true);r.At(p?.072f:-.045f,p?.045f:.064f,p?.07f:.1f,false);r.At(p?.072f:-.045f,p?.045f:.064f,p?.07f:.1f,true);r.At(p?.072f:-.045f,p?.045f:.064f,p?.04f:.06f,true);}}
   if(scenario==4){r.A.Room=1;r.Do(HandlingAction.Eject,0);if(p)r.Do(HandlingAction.ReloadStep,0);}
   HandlingSnapshot b=r.W.Snapshot;int total=r.A.Count+b.Loaded+b.Seated+(int)b.Shots;var old=r.W;WeaponTransfer t=r.Capture();HandlingSnapshot immutable=t.Snapshot;r.Restore(t);var a=r.W.Snapshot;
   Check(a.Loaded==b.Loaded&&a.Seated==b.Seated&&a.SpentChamber==b.SpentChamber&&a.ReloadPending==b.ReloadPending&&a.Chambered==b.Chambered&&a.Shots==b.Shots,"transfer preserves ammo chamber pending shots mode="+mode);
   Check(a.Phase==(b.Phase==ReloadPhase.HeldMagazine?ReloadPhase.NeedsMagazine:b.Phase),"transfer phase matches source held-offer cancellation");
   Check(Near(a.CooldownSeconds,b.CooldownSeconds)&&Near(a.RecoilPitch,b.RecoilPitch)&&Near(a.RecoilYaw,b.RecoilYaw)&&a.RecoilPattern==b.RecoilPattern&&Near(a.IdleSeconds,b.IdleSeconds),"transfer preserves cooldown recoil pattern idle");
   Check(a.TriggerReleaseRequired&&!a.ClaimsSupport&&a.BoltTravel==0,"transfer disarms transient contacts");
   Check(r.A.Count+a.Loaded+a.Seated+(int)a.Shots==total,"reserve conservation through transfer");
   r.I.Trigger=true;Check(!r.Do(HandlingAction.Fire,0).Accepted,"transfer held trigger rejected");
   r.N++;r.I.Sequence=r.N;Check(!old.Apply(new CommandContext("s","a","retired",r.N,1,true),HandlingAction.Fire,r.I).Accepted,"retired handler cannot mutate");
   WeaponHandling ignored;Check(!r.L.Restore(r.C("duplicate"),"stable",t,out ignored).Accepted,"duplicate receipt rejected");Check(!r.L.CreateNew(r.C("respawn"),"stable",p?WeaponMechanism.PrecisionRifle:WeaponMechanism.ServiceRifle,p?3:24,r.A,new Gate(),out ignored).Accepted,"stable instance recreation rejected");
   Check(t.Snapshot.Loaded==immutable.Loaded&&t.Snapshot.Shots==immutable.Shots&&Near(t.Snapshot.CooldownSeconds,immutable.CooldownSeconds),"receipt immutable after restore activity");
  }
  // Continue real physical reloads across transfer, with stale hand samples denied.
  foreach(bool precision in new bool[]{false,true}){
   var r=new Rig(precision);r.Do(HandlingAction.ReloadStep,0);r.Do(HandlingAction.ReloadStep,0);
   r.I.Desktop=false;float x=precision?.072f:-.045f,y=precision?.045f:.064f,z=precision?.07f:.1f;
   r.At(x,y,z,false);r.At(x,y,z,true);r.At(x,y,z-.04f,true);
   int total=r.A.Count+r.W.Snapshot.Loaded+r.W.Snapshot.Seated;
   var t=r.Capture();r.Restore(t);r.At(x,y,z-.2f,true);
   Check(!r.W.Snapshot.Chambered,"stale held grip cannot complete transferred partial rack");
   r.At(x,y,z,false);r.At(x,y,z,true);r.At(x,y,z-(precision?.12f:.08f),true);
   Check(r.W.Snapshot.Chambered&&r.W.Snapshot.Phase==ReloadPhase.Ready,"fresh physical contact chambers after transfer");
   Check(r.A.Count+r.W.Snapshot.Loaded+r.W.Snapshot.Seated==total,"physical completion conserves transferred reserve");
   t=r.Capture();r.Restore(t);Check(r.W.Snapshot.Phase==ReloadPhase.Ready&&r.W.Snapshot.Chambered,"ready transfer preserved for both mechanisms");
  }
  var bind=new Rig(false);var token=bind.Capture();WeaponHandling w;bind.N++;bind.Epoch=2;Check(!bind.L.Restore(bind.C("wrong-instance"),"other",token,out w).Accepted,"wrong stable instance rejected");Check(!bind.L.Restore(new CommandContext("other","a","foreign",bind.N,2,true),"stable",token,out w).Accepted,"wrong session rejected");Check(!new WeaponLifetimes("s",new TestLifetimeApproval()).Restore(bind.C("foreign-ledger"),"stable",token,out w).Accepted,"foreign ledger rejected");bind.Approval.Allow=false;Check(!bind.L.Restore(bind.C("denied"),"stable",token,out w).Accepted,"unapproved transition rejected");bind.Approval.Allow=true;bind.Epoch=0;Check(!bind.L.Restore(bind.C("old-epoch"),"stable",token,out w).Accepted,"epoch rollback rejected");bind.Epoch=2;bind.Actor="new-owner";Check(bind.L.Restore(bind.C("new-owner"),"stable",token,out w).Accepted,"approved ownership transfer");Check(w.Snapshot.Loaded==24&&bind.A.Count==100,"ownership transfer no reserve mutation");
  var deny=new WeaponLifetimes("s",new TestLifetimeApproval{Allow=false});Check(!deny.CreateNew(new CommandContext("s","a","denied",0,1,true),"new",WeaponMechanism.ServiceRifle,24,new Ammo(),new Gate(),out w).Accepted,"initial grant needs separate approval");
  Console.WriteLine("PASS total="+checks);
 }
}
