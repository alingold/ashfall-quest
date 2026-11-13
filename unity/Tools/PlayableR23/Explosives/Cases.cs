using System;
using System.Collections.Generic;
using Ashfall.Core;
using Ashfall.Integration.Playable.Explosives;
using Ashfall.Integration.Playable.HandsWeapons;
class ExplosiveCases
{
 static int cases;static void Check(bool value,string why){cases++;if(!value)throw new Exception(why);}
 static void Near(float a,float b,string why){Check(Math.Abs(a-b)<.0002f,why);}
 sealed class Query:IInteractionQuery {public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return false;}}
 sealed class Effects:IR23ExplosiveEffects
 {
  public readonly R23RoundState Round=new R23RoundState(new[]{"a","b"},new Query(),true);
  public R23ExplosiveTarget[] Targets=new[]{new R23ExplosiveTarget("a",new Float3(5,0,0)),new R23ExplosiveTarget("b",new Float3(6,0,0))};public bool ChangeCenters;
  public R23ExplosiveTarget[] ReadDamageTargets(){return Targets;}
  public bool ApplyTargetDamage(string id,float amount,Float3 origin){bool accepted=Round.ApplyTargetDamage(id,amount);if(ChangeCenters)Targets[1]=new R23ExplosiveTarget("b",new Float3(1000,0,0));return accepted;}
  public bool ApplyPlayerDamage(float amount,Float3 origin){float before=Round.Health+Round.Shield;Round.Damage(amount);return Round.Health+Round.Shield<before;}
 }
 static void Main()
 {
  var s=new R23GrenadeState();foreach(string id in new[]{"f1","f2","f3"})Check(s.Register(id,"frag"),"register one frag payload "+id);Check(s.Register("z1","zone")&&!s.Register("f1","frag")&&!s.Register("bad","rocket"),"source ids single entitlement and genuine kinds");
  s.SetOtherSlotsUsed(2);Check(s.Pickup("f1",2,true)&&s.Pickup("f2",2,true),"two frags share remaining source slot");Check(!s.Pickup("f3",1,true)&&!s.Pickup("z1",1,true),"third frag and zone cannot overfill source3slotbudget");
  Near(s.Cooldown,.25f,"source rack draw delay");s.Advance(.25f);R23OwnedGrenade spent;Check(!s.TryThrow(true,true,true,out spent),"equipping held trigger cannot throw");s.TryThrow(false,true,true,out spent);Check(!s.TryThrow(true,true,false,out spent),"blocked head-to-origin spends no item");R23GrenadeItemState item;s.TryRead("f2",out item);Check(item.Owned&&!item.Spent,"through-wall rejected item intact");
  Check(!s.TryThrow(true,true,true,out spent),"held trigger after rejected press cannot retry automatically");s.TryThrow(false,true,true,out spent);Check(s.TryThrow(true,true,true,out spent)&&spent.Id=="f2"&&s.HeldId=="f1"&&s.HeldHand==2,"accepted press consumes selected id and selects remaining frag stack");
  Check(!s.Pickup("f2",1,true)&&!s.Register("f2","frag"),"spent id cannot collect or regrant");Check(!s.TryThrow(true,true,true,out spent),"remaining stack member requires neutral");s.TryThrow(false,true,true,out spent);s.Advance(.249f);Check(!s.TryThrow(true,true,true,out spent),"remaining physical item has its own .25 draw delay");s.TryThrow(false,true,true,out spent);s.Advance(.0011f);Check(s.TryThrow(true,true,true,out spent)&&s.OwnedItems.Length==0,"remaining frag spends exactly once");
  Check(s.Pickup("z1",1,true),"free source slot accepts real zone");s.Suspend();s.Advance(.25f);Check(!s.TryThrow(true,true,true,out spent),"focus loss/recovery held trigger stays disarmed");s.TryThrow(false,true,true,out spent);Check(s.TryThrow(true,true,true,out spent),"fresh ready zone press consumes one real payload");
  s.ResetRound();Check(s.OwnedItems.Length==0&&s.HeldId==null&&s.Pickup("f1",1,true)&&s.Pickup("f2",1,true),"explicit round reset restores registered world sources only");int slot=s.OwnedItems[0].Slot;Check(!s.DropSlot(2)&&s.OwnedItems.Length==2,"unknown drop slot no partial mutation");Check(s.DropSlot(slot)&&s.OwnedItems.Length==0&&s.HeldId==null,"frag stack atomic drop withdraws both owned ids");Check(s.Pickup("f1",2,true),"drop preserves same unspent physical item");
  foreach(float height in new[]{0f,.65f,1.4f,2.1f})
  {
   Float3 v=R23GrenadeMath.LaunchVelocity(new Float3(0,0,1),new Float3(1,0,0),height);Check(v.IsFinite&&v.Y>0&&v.Z>0,"real source elevated velocity");Near(v.Y,v.Z,"level aim exact45degree elevation");float h=Math.Max(0,height-.035f),flight=(v.Y+(float)Math.Sqrt(v.Y*v.Y+2*9.8f*h))/9.8f;Near(v.Z*flight,R23GrenadeMath.FlatRange,"source calibrated flatfoot maximum range");
  }
  Check(R23GrenadeMath.LaunchVelocity(new Float3(0,1,0),new Float3(1,0,0),1.5f).IsFinite,"vertical aim finite source right-axis fallback");Near(R23GrenadeMath.Length(R23GrenadeMath.GroundRollVelocity(new Float3(4,0,0),new Float3(0,1,0),.1f,0)),3.6f,"source ground roll4m/s2");Near(R23GrenadeMath.Length(R23GrenadeMath.GroundRollVelocity(new Float3(4,0,0),new Float3(0,1,0),.1f,3.048f)),0,"source maximum3.048mroll");
  foreach(double duration in new[]{3.5,2.7})
  {var beats=R23GrenadeMath.WarningTimes(duration);Check(beats.Length==(int)Math.Ceiling(duration*3.2)&&Math.Abs(beats[0]-.09)<.00001&&Math.Abs(beats[beats.Length-1]-(duration-.09))<.00001,"source accelerating warning endpoints/count");for(int i=1;i<beats.Length;i++)Check(beats[i]>beats[i-1]&&(i==1||beats[i]-beats[i-1]<beats[i-1]-beats[i-2]),"warning spacing accelerates monotonically");}
  var fx=new Effects{ChangeCenters=true};var frag=new R23GrenadeClock("f1","frag");int blasts=0;for(int i=0;i<174;i++)frag.Advance(.02f,true,new Float3(),delegate(R23GrenadeBlast b){blasts++;});Check(blasts==0,"frag cannot detonate early");
  frag.Advance(.02f,false,new Float3(),delegate(R23GrenadeBlast b){blasts++;});Check(blasts==0&&!frag.Ended,"pause freezes live fuse");
  var frame=frag.Advance(.02f,true,new Float3(),delegate(R23GrenadeBlast b){blasts++;Check(b.Apply(fx,new Float3(2,0,0))==3,"real health owner accepts snapshotted blast targets+self");});Check(frame.Detonated&&frag.Ended&&blasts==1,"frag source3.5sfuse single blast");frag.Advance(.25f,true,new Float3(),delegate(R23GrenadeBlast b){blasts++;});Check(blasts==1,"ended frag never redamages");
  Near(fx.Round.Shield,0,"real selfblast depleted shield first");Near(fx.Round.Health,80,"real selfblast remaining20 reaches health");Near(fx.Round.LastTargetDamage("a"),75,"source target5m linear damage75 despite no LOS");Near(fx.Round.LastTargetDamage("b"),60,"destroying/moving earlier target cannot change snapshotted later target amount");Check(fx.Round.ApplyPlayerGain(0,25)&&fx.Round.Shield==25,"real existing health gain can restore damaged shield");Check(fx.Round.ApplyPlayerGain(20,0)&&fx.Round.Health==100,"real existing health gain can restore damaged health");
  foreach(float dt in new[]{.02f,.1f,.25f})
  {
   var clock=new R23GrenadeClock("z","zone");for(int i=0;i<4;i++)clock.Advance(.25f,true,new Float3(),null);Check(clock.Contact()&&!clock.Contact(),"zone sticks once on real contact");var radii=new List<float>();bool activated=false;int guard=0;
   while(!clock.Ended&&guard++<1000){var f=clock.Advance(dt,true,new Float3(),delegate(R23GrenadeBlast b){radii.Add(b.Radius);});activated|=f.Activated;}
   Check(activated&&clock.ZoneTicks==20&&radii.Count==19,"zone source activation/lifetime20boundaries19positive-radiusdamage callbacks");for(int i=0;i<radii.Count;i++)Near(radii[i],15*(1-(i+1)/20f),"zone exact radius at each .5s boundary independent of frame partition");
  }
  var airborne=new R23GrenadeClock("z","zone");for(int i=0;i<32;i++)airborne.Advance(.25f,true,new Float3(),null);Check(airborne.Ended&&!airborne.Active,"unstuck zone expires8s without fake blast");
  var zoneFx=new Effects();zoneFx.Targets=new R23ExplosiveTarget[0];var zoneClock=new R23GrenadeClock("zone","zone");zoneClock.Contact();for(int i=0;i<52;i++)zoneClock.Advance(.25f,true,new Float3(),delegate(R23GrenadeBlast b){b.Apply(zoneFx,new Float3());});Near(zoneFx.Round.Shield,5,"real zone center damage19x5 leaves5shield");Near(zoneFx.Round.Health,100,"zone damage has no parallel fake health counter");
  Console.WriteLine("PASS "+cases+" finite grenade inventory/source ballistics/fuse/zone/warning/actual HealthService cases");
 }
}
