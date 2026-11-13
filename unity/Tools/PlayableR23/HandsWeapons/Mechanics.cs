using System;
using Ashfall.Core;
using Ashfall.Integration.Playable.HandsWeapons;
class Mechanics : IInteractionQuery
{
 public bool AllowSight=true;
 public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float range) { return AllowSight; }
 static void Check(bool ok,string label) { if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label); }
 static void Main() {
  var input=new R23HandsInputFrame{Grip=.55f,Trigger=.65f};
  Check(input.GripHeld&&input.TriggerHeld,"analog-only controllers activate grip and trigger thresholds");
  input.Grip=.54f;input.Trigger=.64f;Check(!input.GripHeld&&!input.TriggerHeld,"below-threshold analog input remains released");
  input.GripPressed=input.TriggerPressed=true;input.Grip=input.Trigger=float.NaN;Check(input.GripHeld&&input.TriggerHeld,"valid button path survives absent analog readings");
  input.GripPressed=input.TriggerPressed=false;Check(!input.GripHeld&&!input.TriggerHeld,"invalid analog readings cannot create a press");
  var mapState=new R23RoundState(new string[0],new Mechanics());Check(mapState.Health==100,"normal map works without practice targets");
  var query=new Mechanics();var state=new R23RoundState(new[]{"target/0"},query);
  Check(state.Health==100&&state.Rifle.Snapshot.Loaded==24&&state.Rifle.Reserve==96,"initial real health and finite ammunition");
  Check(!state.Step(.12f,true,false,true)&&state.Rifle.Snapshot.Loaded==24,"paused/unequipped path does not fire");
  state.Step(.12f,false,false,false);
  int shots=0;
  for(int i=0;i<120;i++)if(state.Step(.12f,true,false,false))shots++;
  Check(shots==24&&state.Rifle.Snapshot.Loaded==0&&state.Rifle.Reserve==96,"finite magazine dry fire, no shadow debit");
  state.Step(.12f,false,true,false);for(int i=0;i<80;i++)state.Step(.12f,false,false,false);
  Check(state.Rifle.Snapshot.Loaded==24&&state.Rifle.Reserve==72&&!state.Rifle.Reloading,"actual staged reload debits reserve");
  state.Rifle.SuspendControls();Check(!state.Step(.12f,true,false,false)&&state.Rifle.Snapshot.Loaded==24&&state.Rifle.Reserve==72,"selection/focus suspension retains ammo and blocks held trigger");
  state.Step(.12f,false,false,false);
  Check(state.Step(.12f,true,false,false),"production fire owner accepts loaded shot");
  Check(state.Hit("target/0",new Float3(0,0,0),new Float3(0,0,5))&&state.Hits==1,"actual combat and health hit");
  Check(!state.Hit("target/0",new Float3(0,0,0),new Float3(0,0,5))&&state.Hits==1,"shot cannot hit twice");
  Check(state.ReadTarget("target/0").Shield==78&&state.ReadTarget("target/0").Health==100&&state.LastTargetDamage("target/0")==22,"source MK18 body damage is real shield-first health state");
  for(int i=0;i<20;i++)state.Step(.12f,false,false,true);
  Check(state.ReadTarget("target/0").Shield==78,"paused target reset timer is suspended");
  for(int i=0;i<13;i++)state.Step(.12f,false,false,false);
  Check(state.ReadTarget("target/0").Shield==100&&state.Rifle.Snapshot.Loaded==23&&state.TotalTargetDamage("target/0")==0,"source 1.5s target reset restores only target, never ammunition");
  Check(state.Step(.12f,true,false,false)&&state.Hit("target/0",new Float3(0,0,0),new Float3(0,0,5),true,"mk18")&&state.ReadTarget("target/0").Shield==67,"source MK18 head damage 33");
  Check(state.Step(.12f,true,false,false)&&state.Hit("target/0",new Float3(0,0,0),new Float3(0,0,5),false,"akm")&&state.ReadTarget("target/0").Shield==47,"source AKM body damage 20");
  Check(state.Step(.12f,true,false,false)&&state.Hit("target/0",new Float3(0,0,0),new Float3(0,0,5),true,"akm")&&state.ReadTarget("target/0").Shield==17,"source AKM head damage 30");
  query.AllowSight=false;Check(state.Step(.12f,true,false,false)&&!state.Hit("target/0",new Float3(0,0,0),new Float3(0,0,5))&&state.ReadTarget("target/0").Shield==17,"map obstruction blocks target mutation");query.AllowSight=true;
  for(int i=0;i<6;i++){state.Step(.12f,true,false,false);state.Hit("target/0",new Float3(0,0,0),new Float3(0,0,5));}
  Check(state.ReadTarget("target/0").Health==0,"source dummy receives lethal real health damage");
  int beforeResetAmmo=state.Rifle.Snapshot.Loaded;for(int i=0;i<13;i++)state.Step(.12f,false,false,false);
  Check(state.ReadTarget("target/0").Id=="target/0"&&state.ReadTarget("target/0").Health==100&&state.ReadTarget("target/0").Shield==100&&state.Rifle.Snapshot.Loaded==beforeResetAmmo,"dead dummy resets stable target identity without player/ammo reset");
  state.Damage(25);Check(state.Health==75,"actual player health damage");state.Damage(float.NaN);state.Damage(-1);Check(state.Health==75,"invalid damage rejected");
  state.Damage(100);Check(state.Health==0&&!state.Step(.12f,true,false,false),"dead player cannot fire");
  state.Reset();Check(state.Health==100&&state.Hits==0&&state.Rifle.Snapshot.Loaded==24&&state.Rifle.Reserve==96&&state.ReadTarget("target/0").Health==100,"round reset restores real owners and target state");
  SourceMechanics.Run();
  BothHandsAndGains.Run();
  SprMechanics.Run();
 }
}
