using System;
using Ashfall.Core;
using Ashfall.Modules.HealthStates;
using Ashfall.Integration.Playable.HandsWeapons;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.Appearance;
class GhostCases
{
 sealed class Query:IInteractionQuery{public bool HasLineOfSight(string a,string b,Float3 x,Float3 y,float radius){return true;}}
 static int checks;
 static void N(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
 static bool Near(float a,float b){return Math.Abs(a-b)<.00001f;}
 static void Policy(R23RoundState round,bool ghost,bool motion,bool items,string label)
 {
  var life=round.PlayerHealthState.Life;
  N(R24GhostLifePolicy.Ghost(round.LocalMatchHealthConfigured,life)==ghost,label+" appearance");
  N(R24GhostLifePolicy.Motion(round.LocalMatchHealthConfigured,true,life)==motion,label+" normal movement");
  N(R24GhostLifePolicy.Items(round.LocalMatchHealthConfigured,true,life)==items,label+" items");
 }
 static void Main()
 {
  foreach(LifeState life in Enum.GetValues(typeof(LifeState)))
  {
   N(R24GhostLifePolicy.Motion(false,false,life)&&R24GhostLifePolicy.Items(false,false,life),"Explore unchanged "+life);
   N(!R24GhostLifePolicy.Ghost(false,life),"Explore no ghost "+life);
   N(!R24GhostLifePolicy.Motion(true,false,life)&&!R24GhostLifePolicy.Items(true,false,life),"results/session blocks normal input "+life);
  }
  N(!R24GhostLifePolicy.Motion(true,true,LifeState.Spectator)&&!R24GhostLifePolicy.Items(true,true,LifeState.Spectator),"no invented spectator traversal");
  var round=new R23RoundState(new[]{"actual-target"},new Query(),true);
  round.ConfigureLocalMatchHealth("revive",2,0,100,73,2,10,1,0,1);
  Policy(round,false,true,true,"canonical alive");
  round.Damage(1000);Policy(round,false,true,true,"protected hit rejected");
  round.AdvanceLocalMatchHealth(2);round.Damage(1000);
  N(round.PlayerHealthState.Life==LifeState.Downed&&round.Health==0,"real canonical knockdown, not fake counter");
  Policy(round,true,true,false,"actual downed");
  var dead=round.PlayerHealthState;
  round.Damage(1000);N(round.PlayerHealthState.Revision==dead.Revision,"ghost damage cannot create another death");
  round.AdvanceLocalMatchHealth(12);N(round.PlayerHealthState.Life==LifeState.Dead,"real bleedout eliminated");
  Policy(round,true,true,false,"actual eliminated");
  N(R24GhostLifePolicy.Ghost(true,round.PlayerHealthState.Life),"ghost appearance retained while results overlay blocks motion");
  N(!round.TryRespawnLocalMatch(),"no fabricated allied revival or respawn in revive mode");
  round.EndLocalMatchHealth();Policy(round,false,true,true,"actual end restores Explore");
  round.ConfigureLocalMatchHealth("respawn",0,-1,100,73,0,10,1,0,1);
  round.ApplyDirectPlayerDamage(100);Policy(round,true,true,false,"actual respawn waiting");
  round.AdvanceLocalMatchHealth(.99);N(!round.TryRespawnLocalMatch(),"ghost does not bypass real respawn deadline");
  round.AdvanceLocalMatchHealth(1);N(round.TryRespawnLocalMatch(),"real same Health owner respawns");Policy(round,false,true,true,"actual respawn restoration");
  N(Near(R24GhostLifePolicy.Gray(0,0,0,1),.17f),"source black frontal gray");
  N(Near(R24GhostLifePolicy.Gray(1,1,1,1),.86f),"source white frontal gray");
  N(Near(R24GhostLifePolicy.Gray(1,1,1,0),1),"source white rim saturates");
  N(Near(R24GhostLifePolicy.Gray(1,0,0,1),.17f+.2126f*.69f),"source red luminance");
  N(Near(R24GhostLifePolicy.Gray(0,1,0,-1),.17f+.7152f*.69f),"source abs facing and green weight");
  N(Near(R24GhostLifePolicy.Gray(0,0,1,0),.17f+.0722f*.69f+.14f),"source blue rim weight");
  for(int i=0;i<=100;i++){float linear=i/100f;N(Near(R23AppearanceColorDomain.LinearRgb(R23AppearanceColorDomain.ShaderRgb(linear,true)),linear),"actual selected skin tint gamma roundtrip "+i);}
  N(R24GhostLifePolicy.Opacity==.42f,"source alpha opacity");
  Console.WriteLine(checks+" source ghost policy/real Health/downed/bleedout/respawn/end/linear tone cases PASS");
 }
}
