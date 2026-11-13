using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
class InstanceCopiesCases:IInteractionQuery {
 int checks;
 public bool HasLineOfSight(string actor,string target,Float3 origin,Float3 point,float range){return true;}
 void Check(bool condition,string label){if(!condition)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 HandlingInput Input(bool trigger=false,bool tracked=true){return new HandlingInput{Enabled=true,PrimaryTracked=tracked,SupportTracked=true,Desktop=true,Trigger=trigger};}
 void Neutral(R23RoundState round){round.Step(.25f,Input(),false,false);round.Step(.25f,Input(),false,false);}
 void Run(){
 var round=new R23RoundState(new[]{"target"},this,true);round.EnsureMagnumState("r23-test/magnum");
 string[] profiles={"mk18","akm","spr","magnum","cx4","px4","m1014","ump","p90","mp5","mp9","1911","tec9","rfb","fal","s85"};
 foreach(string profile in profiles){
  string first=null,second=null;Check(round.CreateWeaponInstance(profile,1,out first)&&round.CreateWeaponInstance(profile,5,out second),profile+" real two copies allocated");
  R23WeaponInstanceInfo a=null,b=null;Check(round.TryReadWeaponInstance(first,out a)&&round.TryReadWeaponInstance(second,out b)&&a.ProfileId==profile&&b.ProfileId==profile&&a.InstanceId!=b.InstanceId&&a.ActionId!=b.ActionId&&b.Stars==5,profile+" stable distinct identities and exact rarity");
  Check(round.SelectWeapon(first),profile+" select first instance");Neutral(round);int original=round.ActiveLoaded;Check(round.Step(.25f,Input(true),false,false),profile+" accepted real native first shot");Check(round.ActiveLoaded==original-1,profile+" first magazine debited once");
  Check(round.SelectWeapon(second),profile+" select second instance");Neutral(round);Check(round.ActiveLoaded==b.Capacity&&round.ReadWeapon(first).Shots>=1,profile+" independent full second magazine and first shot history");
  int reserve=round.ActiveReserve;round.SuspendActiveWeapon();round.SelectWeapon(first);Neutral(round);Check(round.ActiveLoaded==original-1&&round.ActiveReserve==reserve,profile+" selection and suspension never refill");
  long shots=round.ActiveSnapshot.Shots;round.Step(.25f,Input(true),false,true);Check(round.ActiveSnapshot.Shots==shots,profile+" paused fire refused");round.Step(.25f,Input(true,false),false,false);Check(round.ActiveSnapshot.Shots==shots,profile+" tracking lost fire refused");
  round.SelectWeapon(first);Neutral(round);var prior=round.ReadWeapon(second);if(profile!="s85"){round.Step(0,Input(),true,false);var opened=round.ReadWeapon(first);round.SelectWeapon(second);Neutral(round);round.SelectWeapon(first);Neutral(round);Check(round.ActiveSnapshot.Phase==opened.Phase&&round.ReadWeapon(second).Loaded==prior.Loaded&&round.ReadWeapon(second).Shots==prior.Shots,profile+" open reload stage belongs only to exact copy");}
  round.SelectWeapon(second);Check(round.RetireWeaponInstance(first)&&!round.TryReadWeaponInstance(first,out a)&&!round.SelectWeapon(first),profile+" retired copy unavailable without touching second");Check(!round.RegisterWeaponInstance(first,profile,1),profile+" retired stable action cannot be reminted in same round");
 }
 string px,mag;round.CreateWeaponInstance("px4",1,out px);round.CreateWeaponInstance("magnum",3,out mag);round.SelectWeapon(px);Neutral(round);int pistol=round.ActiveReserve;round.Step(0,Input(),true,false);Check(round.ActiveReserve==pistol+16&&round.ReadReserve(mag)==pistol+16,"independent PX4 magazine refund reaches actual shared Magnum pistol reserve");
 round.Step(0,Input(),true,false);Check(round.ReadReserve(mag)==pistol,"PX4 actual insertion draws same finite pistol pool");
 string cx;round.CreateWeaponInstance("cx4",5,out cx);round.SelectWeapon(cx);Neutral(round);Check(round.ActiveCapacity==45&&round.ActiveLoaded==45,"five star CX4 native owner capacity45");round.Step(0,Input(),true,false);int before=round.ActiveReserve;round.Step(0,Input(),true,false);Check(round.ReadWeapon(cx).Seated==45&&round.ActiveReserve==before-45,"five star full reload uses native45 round transfer");
 string spr;round.CreateWeaponInstance("spr",5,out spr);round.SelectWeapon(spr);Neutral(round);Check(round.Step(.25f,Input(true),false,false)&&round.Hit("target",new Float3(),new Float3(0,0,1),true,"spr")&&Math.Abs(round.LastTargetDamage("target")-154)<.001f,"five star SPR exact154 head damage through real HealthService");
 round.ResetTarget("target");round.SelectWeapon(mag);Neutral(round);Check(round.Step(.25f,Input(true),false,false)&&round.Hit("target",new Float3(),new Float3(0,0,1),false,"magnum")&&Math.Abs(round.LastTargetDamage("target")-71)<.001f,"three star Magnum exact71 body damage through real owner hit");
 var pose=new R23M1014Pose{Forward=new Float3(0,0,1),Right=new Float3(1,0,0),Up=new Float3(0,1,0)};var one=R23M1014Volley.DirectionsFor(pose,1);var five=R23M1014Volley.DirectionsFor(pose,.6f);Check(five.Length==8&&Math.Abs(Math.Atan2(five[1].X,five[1].Z))<Math.Abs(Math.Atan2(one[1].X,one[1].Z)),"five star M1014 true narrowed eight pellet directions");
 string bad;Check(!round.CreateWeaponInstance("cx4",0,out bad)&&!round.CreateWeaponInstance("cx4",6,out bad)&&!round.CreateWeaponInstance("unknown",5,out bad),"invalid rarity or unported profile explicitly refused");
 Check(!round.RegisterWeaponInstance(cx,"cx4",1)&&!round.RegisterWeaponInstance("duplicate-native","cx4",1,round.WeaponInstanceId(cx)),"duplicate action and duplicate native identity cannot mint ammunition");
 round.Reset();Check(!round.TryReadWeaponInstance(cx,out varDiscard)&&round.ReadWeapon("cx4").Loaded==25,"restart clears copies and recreates source default kit mechanics only");
 string bolt;round.CreateWeaponInstance("s85",5,out bolt);round.SelectWeapon(bolt);Neutral(round);Check(round.ActiveCapacity==5&&round.Step(.25f,Input(true),false,false)&&round.ActiveSnapshot.Phase==ReloadPhase.NeedsRack&&round.ActiveSnapshot.Seated==4,"five star S85 preserves source five-round owner with four behind bolt stage");
 string rfb;round.CreateWeaponInstance("rfb",1,out rfb);round.SelectWeapon(rfb);Neutral(round);round.ResetTarget("target");Check(round.Step(.25f,Input(true),false,false),"RFB accepted shot before same-frame eject");round.EjectActiveMagazineAfterShot(Input(true),false);Check(round.ActiveSnapshot.Phase==ReloadPhase.NeedsMagazine&&round.Hit("target",new Float3(),new Float3(0,0,1),false,"rfb")&&round.LastTargetDamage("target")==19,"RFB post-fire click retains real pending Health hit and ejects remainder");
 var state1=new R23WeaponFeedbackState("cx4");var state2=new R23WeaponFeedbackState("cx4");Check(state1.AcceptedShot(1,false)&&state2.AcceptedShot(1,false)&&state1.AcceptedShot(2,false)&&state2.AcceptedShots==1,"same-profile visual shot histories can be independently retained");
 round.Retire();Check(!round.CreateWeaponInstance("cx4",1,out bad),"retired round cannot create a fresh weapon lifetime");
 Console.WriteLine(checks+" real instance/rarity owner cases passed");
 }
 R23WeaponInstanceInfo varDiscard;
 static void Main(){new InstanceCopiesCases().Run();}
}


