using System;
using Ashfall.Core;
using Ashfall.Modules.WeaponHandling;
using Ashfall.Integration.Playable.HandsWeapons;
class PracticeBindingCases {
 sealed class Query:IInteractionQuery {public bool HasLineOfSight(string a,string b,Float3 p,Float3 q,float range){return true;}}
 static int cases;static void N(bool condition,string label){if(!condition)throw new Exception(label);cases++;}
 static HandlingInput Input(bool trigger=false,bool grip=false,Float3 point=new Float3()){return new HandlingInput{Enabled=true,PrimaryTracked=true,SupportTracked=true,ForegripDistance=1,Trigger=trigger,Grip=grip,SupportLocal=point};}
 static R23M1014Pose Pose(){return new R23M1014Pose{Origin=new Float3(0,1,0),Forward=new Float3(0,0,1),Right=new Float3(1,0,0),Up=new Float3(0,1,0)};}
 static bool Step(R23RoundState r,HandlingInput input,HandlingAction action=HandlingAction.Sample){return r.StepM1014(.02f,input,action,Pose(),false,false);}
 static void Main(){
  var r=new R23RoundState(new[]{"target"},new Query(),true);r.EnsureMagnumState("r23-world/magnum");
  r.ConfigureLocalMatchHealth("respawn",0,-1,100,100,3,10,1,0,0);r.SetLocalMatchInfiniteAmmo(true);
  N(r.SelectWeapon("m1014"),"actual source action resolves");r.SuspendActiveWeapon();
  N(r.ActiveActionId=="m1014"&&r.ActiveWeaponId=="m1014"&&r.ActiveLoaded==6&&r.ActiveSnapshot.Phase==ReloadPhase.Ready,"actual native initial entitlement is ready6 under Practice rules");
  N(r.ActiveReserve==-1&&r.ReadStoredAmmoReserveFamily("shells")==480,"infinite sentinel preserves real stored480");
  N(!Step(r,Input())&&r.ActiveLoaded==6,"neutral cannot consume/eject initial six");
  N(Step(r,Input(true))&&r.ActiveLoaded==5&&r.LastM1014Volley.Directions.Length==8,"first real owner shot debits one shell and emits8pellets");
  N(r.ReadStoredAmmoReserveFamily("shells")==480,"actual shot does not debit reserve");
  Step(r,Input());Step(r,Input(),HandlingAction.Eject);N(r.ActiveSnapshot.Phase==ReloadPhase.NeedsMagazine&&r.ActiveLoaded==0,"source opening ejects real magazine");
  N(!r.M1014.ReloadGripInProgress&&R23ReloadGripRules.InZone("m1014",r.ActiveSnapshot,R23M1014Definition.Offer),"source offer proximity claims before actual contact");
  Step(r,Input());Step(r,Input(false,true,R23M1014Definition.Offer));Step(r,Input(false,true,R23M1014Definition.Socket));
  N(r.ActiveSnapshot.Phase==ReloadPhase.NeedsRack&&r.ActiveSnapshot.Seated==6,"actual support UP seats6 through infinite real-family wrapper");
  N(!r.M1014.ReloadGripInProgress&&R23ReloadGripRules.InZone("m1014",r.ActiveSnapshot,R23M1014Definition.Handle),"fresh native M1014 handle claims before world pickup, with no prior contact");
  N(!R23ReloadGripRules.InZone("m1014",r.ActiveSnapshot,new Float3(1,0,0)),"waiting M1014 reload leaves distant support hand free for ordinary pickup");
  Step(r,Input());Step(r,Input(false,true,R23M1014Definition.Handle));var handle=R23M1014Definition.Handle;var pull=new Float3(handle.X,handle.Y,handle.Z-.09f);Step(r,Input(false,true,pull));
  N(r.ActiveLoaded==6&&r.ActiveSnapshot.Phase==ReloadPhase.Ready&&r.ActiveReserve==-1&&r.ReadStoredAmmoReserveFamily("shells")==480,"actual BACK chambers6 without finite reserve mutation");
  string copy;N(r.CreateWeaponInstance("m1014",1,out copy),"independent cache instance created");N(r.ReadWeapon(copy).Loaded==6,"cache copy has its own native entitlement");
  r.SelectWeapon("knife");N(r.ActiveLoaded==0&&r.ActiveActionId=="knife","a legacy melee pickup has zero loaded: diagnostics must record action identity instead of inferring an empty M1014");
  r.SelectWeapon("magnum");r.StepMagnum(.02f,new R23MagnumInput(Input()),false);N(r.StepMagnum(.02f,new R23MagnumInput(Input(true)),false),"actual finite existing Magnum can discharge under infinite policy");r.StepMagnum(.02f,new R23MagnumInput(Input()),false);r.StepMagnum(.02f,new R23MagnumInput(Input(),primaryButton:true),false);
  N(r.Magnum.Snapshot.Phase==ReloadPhase.CylinderOpen&&r.Magnum.OfferedRoundCount==1,"infinite real-family sentinel offers exactly one missing visible Magnum round");r.SetLocalMatchInfiniteAmmo(false);N(r.Magnum.OfferedRoundCount==1,"same finite positive reserve retains offered round count");
  r.SelectWeapon("m1014");N(r.ActiveLoaded==6,"selection preserves native M1014 after actual physical reload");N(r.ActiveReserve==480,"finite backing retained on policy exit");
  r.SetLocalMatchInfiniteAmmo(true);Step(r,Input(),HandlingAction.Eject);Step(r,Input());Step(r,Input(false,true,R23M1014Definition.Offer));Step(r,Input(false,true,R23M1014Definition.Socket));N(r.ActiveSnapshot.Phase==ReloadPhase.NeedsRack&&r.ActiveSnapshot.Seated==6,"same source UP insertion reaches actual seated6 before boundary check");
  Step(r,Input());N(!r.M1014.ReloadGripInProgress,"physical neutral releases actual reload contact");Step(r,Input(false,true,handle));N(r.M1014.ReloadGripInProgress,"fresh physical handle grip acquires actual charge contact");
  Step(r,Input(false,true,new Float3(handle.X,handle.Y,handle.Z-.084f)));N(r.ActiveSnapshot.Phase==ReloadPhase.NeedsRack&&r.M1014.ReloadGripInProgress&&Math.Abs(r.ActiveSnapshot.BoltTravel-.084f)<.00001f,"actual .084 stroke preserves partial bolt/contact below source .085 threshold");
  Step(r,Input(false,true,new Float3(handle.X,handle.Y,handle.Z-.10f)));N(r.ActiveSnapshot.Phase==ReloadPhase.Ready&&r.ActiveLoaded==6&&!r.M1014.ReloadGripInProgress,"same held grip authored .10 full stroke cycles real seated6 without threshold relaxation");N(r.ReadStoredAmmoReserveFamily("shells")==480,"full physical source stroke under infinite policy leaves finite reserve unchanged");
  var rack=new HandlingSnapshot{Phase=ReloadPhase.NeedsRack};N(R23ReloadGripRules.InZone("m1014",rack,new Float3(-.06f,.045f,.10f),true),"source handle mirrors for left primary/right support");
  var handleAt=R23M1014Definition.Handle;N(R23ReloadGripRules.InZone("m1014",rack,new Float3(handleAt.X,handleAt.Y+.1699f,handleAt.Z))&&!R23ReloadGripRules.InZone("m1014",rack,new Float3(handleAt.X,handleAt.Y+.1701f,handleAt.Z)),"source .115 radius plus .055 reach strict boundary remains exact");
  foreach(var d in SourceRifleDefinition.All){if(d.Id=="awp"||d.Id=="m60")continue;N(!R23ReloadGripRules.InZone(d.Id,new HandlingSnapshot{Phase=ReloadPhase.Ready},d.Offer),d.Id+" loaded gun has no offhand reload proximity claim");N(R23ReloadGripRules.InZone(d.Id,new HandlingSnapshot{Phase=ReloadPhase.NeedsMagazine},d.Offer),d.Id+" actual source offer claims before contact");N(R23ReloadGripRules.InZone(d.Id,rack,d.ChargingPoint(true),true),d.Id+" exact physical handle claims with source left-primary mirroring");}
  N(!R23ReloadGripRules.InZone("uzis",rack,new Float3())&&!R23ReloadGripRules.InZone("matadors",rack,new Float3())&&!R23ReloadGripRules.InZone("knife",rack,new Float3()),"paired occupied-hand flick/melee do not invent offhand reload zones");
  N(R23ReloadGripRules.InZone("magnum",new HandlingSnapshot{Phase=ReloadPhase.CylinderOpen},R23MagnumRuntime.Offer(false)),"source Magnum open-cylinder offer reservation");
  N(R23ReloadGripRules.InZone("dt11",new HandlingSnapshot{Phase=ReloadPhase.NeedsMagazine,BoltTravel=.72f},R24Dt11Definition.Offer(.72f)),"source DT11 moving shell-pair offer reservation");
  N(R23ReloadGripRules.InZone("awp",new HandlingSnapshot{Phase=ReloadPhase.NeedsBolt},R24AwpDefinition.Bolt),"source precision elliptical bolt reservation");
  N(R23ReloadGripRules.InZone("m60",rack,R24M60Definition.Grab(R24M60Stage.NeedsCharge,true),true,R24M60Stage.NeedsCharge),"source M60 active exact charging handle reservation");
  Console.WriteLine(cases+" real Round/M1014 Practice binding, native physical reload and source pre-pickup reload-zone cases PASS");
 }
}
