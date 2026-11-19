using System;
using Ashfall.Core;
using Ashfall.Integration.DesktopPlaytest.Combat;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Modules.WeaponHandling;
static class SprMechanics
{
 sealed class Sight:IInteractionQuery { public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return true;} }
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 static LocalRifleSandbox Gun(){return new LocalRifleSandbox(new string[0],new Sight(),SourceRifleDefinition.Spr,new R23RifleReserve(),"test/spr");}
 static HandlingInput Hand(bool grip,Float3 point){return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Grip=grip,SupportLocal=point,ForegripDistance=.5f};}
 public static void Run(){
  var p=SourceRifleDefinition.Spr;var gun=Gun();Check(p.Capacity==4&&p.SemiAutomatic&&p.Interval==.32f&&p.AmmoFamily=="sniper"&&p.BodyDamage==55&&p.HeadDamage==92,"SPR current first-rarity catalog overrides stale setup: four rounds, semi320ms, sniper pool,55/92damage");
  Check(gun.Step(0,true,false,false)&&gun.Snapshot.Loaded==3&&gun.Snapshot.Chambered&&gun.Snapshot.Phase==ReloadPhase.Ready,"SPR shot remains chambered ready, not converted to bolt-action mechanism");
  for(int i=0;i<10;i++)gun.Step(.1f,true,false,false);
  Check(gun.Snapshot.Shots==1&&gun.Snapshot.Loaded==3&&gun.Reserve==2400,"held SPR trigger never repeats or performs shadow ammo debit");
  gun.Step(0,false,false,false);Check(gun.Step(.01f,true,false,false)&&gun.Snapshot.Loaded==2,"released SPR trigger permits next accepted shot");
  var cadence=Gun();cadence.Step(0,true,false,false);cadence.Step(.01f,false,false,false);bool early=cadence.Step(.25f,true,false,false);
  Check(!early&&cadence.Step(.06f,true,false,false)&&cadence.Snapshot.Shots==2,"SPR native cadence rejects early click and accepts320ms boundary");
  var latch=new R23SemiTriggerLatch();latch.Shot();latch.Sample(new R23HandInput(true,false,0,.3f).TriggerReleased);
  Check(!latch.Allows(true),"SPR analog semi latch does not rearm at intermediate .3 trigger travel");
  latch.Sample(new R23HandInput(true,false,0,.19f).TriggerReleased);Check(latch.Allows(true),"SPR analog semi latch rearms only below source .2 threshold");
  latch.Shot();latch.Sample(new R23HandInput(true,false,0,0,false,true).TriggerReleased);Check(!latch.Allows(true),"pressed trigger button prevents false analog rearm");
  Check(Math.Abs((p.Offer.Y+.278f)*p.ModelScale-.127f)<.00001f,"SPR source magazine offer is raised exactly five physical inches");
  var physical=Gun();physical.Step(0,true,false,false);physical.Step(0,false,true,false);physical.Step(.01f,Hand(false,p.Offer),false,false);physical.Step(.01f,Hand(true,p.Offer),false,false);
  Check(physical.Snapshot.Phase==ReloadPhase.HeldMagazine&&physical.Reserve==2403,"SPR tactical eject refunds three live rounds; raised offer grabs without debit");
  physical.Step(.01f,Hand(true,p.Insert),false,false);Check(physical.Snapshot.Phase==ReloadPhase.HeldMagazine,"SPR source raised offer still requires actual60mm upward insertion travel");
  physical.Step(.01f,Hand(true,new Float3(p.Insert.X,p.Offer.Y+.061f,p.Insert.Z)),false,false);
  Check(physical.Snapshot.Phase==ReloadPhase.NeedsRack&&physical.Snapshot.Seated==4&&physical.Reserve==2399,"SPR source upward insertion seats exact four rounds and debits sniper reserve once");
  physical.Step(.01f,Hand(false,p.Charging),false,false);physical.Step(.01f,Hand(true,p.Charging),false,false);physical.Step(.01f,Hand(true,new Float3(p.Charging.X,p.Charging.Y,p.Charging.Z-.08f)),false,false);
  Check(physical.Snapshot.Chambered&&physical.Snapshot.Loaded==4&&physical.Reserve==2399,"SPR exact source rear T-handle AABB grip and80mm rearward pull chamber existing rounds");
  var round=new R23RoundState(new[]{"target/0"},new Sight(),true);round.SelectWeapon("spr");string instance=round.WeaponInstanceId("spr");round.Step(0,false,false,false);
  Check(round.Step(.16f,true,false,false)&&round.Hit("target/0",new Float3(),new Float3(0,0,5))&&round.ReadTarget("target/0").Shield==45,"SPR fresh source body hit applies55 real native shield damage");
  round.Step(.16f,false,false,false);Check(round.Step(.16f,true,false,false)&&round.Hit("target/0",new Float3(),new Float3(0,0,5),true,"spr")&&round.ReadTarget("target/0").Shield==0&&round.ReadTarget("target/0").Health==53,"SPR actual head hit applies92 across remaining shield into health");
  round.Step(0,false,true,false);round.Step(0,false,true,false);int pool=round.ReadReserve("spr");
  Check(pool==2398&&round.ReadReserve("mk18")==2400&&round.ReadReserve("akm")==2400,"SPR sniper reload pool is independent of shared MK18/AKM rifle ammunition");
  round.SelectWeapon("mk18");round.SelectWeapon("spr");
  Check(round.WeaponInstanceId("spr")==instance&&round.ReadWeapon("spr").Seated==4&&round.ReadWeapon("spr").Phase==ReloadPhase.NeedsRack&&round.ReadReserve("spr")==pool,"SPR equip/custody suspension preserves exact instance, seated magazine and separate reserve");
  float width=R23SprScopeMath.Width*1.32f,height=R23SprScopeMath.Height*1.32f;
  var corners=new[]{new Float3(-width/2,-height/2,.2f),new Float3(width/2,-height/2,.2f),new Float3(-width/2,height/2,.2f),new Float3(width/2,height/2,.2f)};
  float extent=R23SprScopeMath.HalfVerticalTangent(corners);
  Check(Math.Abs(extent-.03575f)<.00001f&&R23SprScopeMath.PixelWidth==520&&R23SprScopeMath.PixelHeight==307,"SPR optical camera extent matches physical source window and exact3x angular magnification");
  for(int i=0;i<corners.Length;i++)corners[i]=new Float3(corners[i].X+.03f,corners[i].Y,corners[i].Z);float left=R23SprScopeMath.HalfVerticalTangent(corners);for(int i=0;i<corners.Length;i++)corners[i]=new Float3(corners[i].X-.06f,corners[i].Y,corners[i].Z);float right=R23SprScopeMath.HalfVerticalTangent(corners);
  Check(left>extent&&Math.Abs(left-right)<.00001f,"per-eye off-axis optical extents cover actual eye-dependent aperture corners symmetrically");
  Check(Math.Abs(R23SprScopeMath.SampleAngle(.04f,.02f,.01f)-.01666667f)<.00001f&&R23SprScopeMath.ReticleDepth==1,"SPR angular scene reprojection uses3x and fixed one-metre virtual reticle plane");
  Check(R23SprScopeMath.Alignment(.05f,.085f,.96f,true)&&R23SprScopeMath.Alignment(.60f,0,1,true)&&!R23SprScopeMath.Alignment(.049f,0,1,true)&&!R23SprScopeMath.Alignment(.601f,0,1,true)&&!R23SprScopeMath.Alignment(.2f,.086f,1,true)&&!R23SprScopeMath.Alignment(.2f,0,.959f,true)&&!R23SprScopeMath.Alignment(.2f,0,1,false),"SPR scope respects source eye relief, alignment, gaze and map-occlusion gates");
 }
}
