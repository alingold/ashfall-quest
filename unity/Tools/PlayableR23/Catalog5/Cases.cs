using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.DesktopPlaytest.Combat;
using Ashfall.Integration.Playable.HandsWeapons;
static class CatalogCases
{
 static int count;static void Check(bool v,string why){count++;if(!v)throw new Exception(why);}
 static bool Near(float a,float b){return Math.Abs(a-b)<.00002f;}
 sealed class Query:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 c,Float3 d,float r){return true;}}
 static HandlingInput Input(bool trigger=false,bool grip=false,Float3 point=new Float3()){return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,Trigger=trigger,Grip=grip,SupportLocal=point,ForegripDistance=1};}
 static LocalRifleSandbox Gun(SourceRifleDefinition d,R23RifleReserve pool){return new LocalRifleSandbox(new[]{"target"},new Query(),d,pool,"catalog/"+d.Id);}
 static void Rack(LocalRifleSandbox g,SourceRifleDefinition p){g.StepSource(0,Input(false,false,p.Charging),HandlingAction.Sample,false);g.StepSource(0,Input(false,true,p.Charging),HandlingAction.Sample,false);g.StepSource(.12f,Input(false,true,new Float3(p.Charging.X,p.Charging.Y,p.Charging.Z-p.HandlePull-.002f)),HandlingAction.Sample,false);}
 static int Main(){try{
  string[] ids={"1911","tec9","rfb","fal","s85"},families={"pistol","pistol","rifle","rifle","sniper"};int[] caps={8,12,25,15,5};float[] rates={5,14.4f,9.5f,5,1.4f},body={44,25,19,33,65},head={61,30,23,44,105};
  for(int i=0;i<ids.Length;i++)
  {
   var p=SourceRifleDefinition.ForId(ids[i]);Check(p!=null&&p.Capacity==caps[i]&&Near(p.Interval,1/rates[i])&&p.BodyDamage==body[i]&&p.HeadDamage==head[i]&&p.AmmoFamily==families[i],ids[i]+" exact source profile");
   Check(!p.Pistol&&p.SupportPoint.Y==-.025f&&p.SupportPoint.Z==.36f&&p.VrOpeningOnly,ids[i]+" actual catalog base support and physical opening");
   var pool=new R23RifleReserve(1200);var gun=Gun(p,pool);gun.SuspendControls();Check(!gun.StepSource(.1f,Input(true),HandlingAction.Sample,false),ids[i]+" neutral rearm");gun.StepSource(0,Input(),HandlingAction.Sample,false);
   Check(gun.StepSource(0,Input(true),HandlingAction.Sample,false)&&gun.LoadedRounds==p.Capacity-1,ids[i]+" actual owner discharge");
   var round=new R23RoundState(new[]{"target"},new Query(),true);Check(round.SelectWeapon(p.Id),ids[i]+" actual production definition registration");round.Step(0,Input(),false,false);Check(round.Step(0,Input(true),false,false)&&round.Hit("target",new Float3(),new Float3(0,0,3),false,p.Id)&&round.LastTargetDamage("target")==p.BodyDamage,ids[i]+" actual production HealthService body damage");
   var headRound=new R23RoundState(new[]{"target"},new Query(),true);headRound.SelectWeapon(p.Id);headRound.Step(0,Input(),false,false);Check(headRound.Step(0,Input(true),false,false)&&headRound.Hit("target",new Float3(),new Float3(0,0,3),true,p.Id)&&headRound.LastTargetDamage("target")==p.HeadDamage,ids[i]+" actual production listed head damage");
   Check(!gun.StepSource(p.Interval*.1f,Input(true),HandlingAction.Sample,false),ids[i]+" refuses early shot");
   if(p.BoltAction)
   {
    Check(gun.Snapshot.Phase==ReloadPhase.NeedsRack&&gun.Snapshot.Loaded==0&&gun.Snapshot.Seated==4&&!gun.Snapshot.Chambered&&pool.Available==1200,"S85 actual nonfinal shot seats remainder without reserve debit");
    for(int j=0;j<4;j++)Check(!gun.StepSource(.2f,Input(true),HandlingAction.Sample,false),"S85 held trigger cannot bypass bolt");
    Rack(gun,p);Check(gun.Snapshot.Phase==ReloadPhase.Ready&&gun.LoadedRounds==4&&pool.Available==1200,"S85 physical release/regrip/BACK cycle rechambers same rounds");
    gun.StepSource(.12f,Input(),HandlingAction.Sample,false);Check(gun.StepSource(0,Input(true),HandlingAction.Sample,false)&&gun.Snapshot.Seated==3&&gun.LoadedRounds==3,"S85 next shot returns remaining3 to bolt stage");
   }
   else if(p.SemiAutomatic)
   {gun.StepSource(.25f,Input(true),HandlingAction.Sample,false);Check(gun.LoadedRounds==p.Capacity-1,ids[i]+" held trigger stays semi");gun.StepSource(0,Input(),HandlingAction.Sample,false);Check(gun.StepSource(0,Input(true),HandlingAction.Sample,false),ids[i]+" neutral then fresh semi shot");}
   else
   {Check(gun.StepSource(p.Interval,Input(true),HandlingAction.Sample,false)&&gun.LoadedRounds==p.Capacity-2,ids[i]+" real automatic held trigger");}
   int remaining=gun.LoadedRounds;gun.StepSource(0,Input(),HandlingAction.Eject,false);Check(gun.Snapshot.Phase==ReloadPhase.NeedsMagazine&&pool.Available==1200+remaining,ids[i]+" physical eject refunds actual remainder");
   gun.StepSource(0,Input(false,false,p.Offer),HandlingAction.Sample,false);gun.StepSource(0,Input(false,true,p.Offer),HandlingAction.Sample,false);Check(gun.Snapshot.Phase==ReloadPhase.HeldMagazine,ids[i]+" actual offer acquisition");
   if(p.S85Insertion)
   {
    gun.StepSource(0,Input(false,true,new Float3(p.Offer.X,p.Offer.Y+.05f,p.Offer.Z)),HandlingAction.Sample,false);Check(gun.Snapshot.Phase==ReloadPhase.HeldMagazine,"S85 .05local lift does not bypass source .06stroke");
    gun.StepSource(0,Input(false,true,new Float3(p.Offer.X,p.Offer.Y+.061f,p.Offer.Z)),HandlingAction.Sample,false);
   }
   else gun.StepSource(0,Input(false,true,p.Insert),HandlingAction.Sample,false);
   Check(gun.Snapshot.Phase==ReloadPhase.NeedsRack&&gun.Snapshot.Seated==p.Capacity,ids[i]+" actual source seating");
   if(p.Id=="rfb")
   {gun.StepSource(0,Input(false,false,p.Charging),HandlingAction.Sample,false);gun.StepSource(0,Input(false,true,p.Charging),HandlingAction.Sample,false);gun.StepSource(.12f,Input(false,true,new Float3(p.Charging.X,p.Charging.Y,p.Charging.Z-.076f)),HandlingAction.Sample,false);Check(gun.Snapshot.Phase==ReloadPhase.NeedsRack,"RFB .076 cannot replace actual .08handlepull");}
   Rack(gun,p);Check(gun.Snapshot.Phase==ReloadPhase.Ready&&gun.LoadedRounds==p.Capacity&&pool.Available==1200+remaining-p.Capacity,ids[i]+" physical charging keeps finite ammo ledger");
   var rarity=p.WithRarity(p.Capacity+2,p.BodyDamage+1,p.HeadDamage+1);Check(rarity.BoltAction==p.BoltAction&&rarity.S85Insertion==p.S85Insertion&&rarity.HandlePull==p.HandlePull&&rarity.VrOpeningOnly==p.VrOpeningOnly&&rarity.SourceManualControls==p.SourceManualControls,ids[i]+" immutable rarity copy retains source mechanics");
   var f=new SourceCatalog5Feedback(p.Id);Check(f.AcceptedShot(1,false)&&!f.AcceptedShot(1,true)&&f.AcceptedShots==1&&f.Flash,ids[i]+" feedback observes accepted native serial once");f.Advance(.05f);Check(f.Distance>0&&f.PitchRadians>0,ids[i]+" actual source recoil presentation");f.ClearPresentation();Check(f.Distance==0&&f.PitchRadians==0&&f.AcceptedShots==1,ids[i]+" suspend clears presentation without resetting identity");
  }
  Check(SourceRifleDefinition.All.Length==14,"additive catalog14 owner definitions");
  Check(!SourceRifleDefinition.Rfb.SourceManualControls&&SourceRifleDefinition.Tec9.SourceManualControls&&!SourceRifleDefinition.Tec9.SemiAutomatic,"source RFB click-only custompoll and TEC9 automatic");
  foreach(string id in new[]{"1911","fal"})
  {float peak=id=="1911"?.097f:.125f,zero=id=="1911"?.205f:.270f,under=id=="1911"?.264f:.333f,end=id=="1911"?.43f:.51f;Check(Near(SourceCatalog5Feedback.SignedPulse(id,peak),1)&&Near(SourceCatalog5Feedback.SignedPulse(id,zero),0)&&Near(SourceCatalog5Feedback.SignedPulse(id,under),id=="1911"?-.28f:-.16f)&&SourceCatalog5Feedback.SignedPulse(id,end)==0,id+" exact signed pulse extrema");}
  var rfb=new SourceCatalog5Feedback("rfb");rfb.AcceptedShot(1,false);float one=rfb.PitchRadians,dist=rfb.Distance;rfb.SetSupported(true);Check(Near(rfb.PitchRadians,one*.4f)&&Near(rfb.Distance,dist*.4f),"RFB actual supported presentation .4 of same unsupported endpoint");
  Console.WriteLine("PASS "+count+" real Catalog5 owner/health/ammo/reload/source-feedback cases");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
