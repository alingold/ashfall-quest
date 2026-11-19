using System;
using System.Collections.Generic;
using System.IO;
using Ashfall.Core;
using Ashfall.Integration.Playable.Match;
using Ashfall.Integration.Playable.Networking;
using Ashfall.Modules.HealthStates;
using Ashfall.Modules.Sessions;

namespace Ashfall.Integration.Playable.Networking {
 public sealed partial class R24LanLobby {
  // Fixture hook: drive the room build-damage choke point with raw damage (what melee, blasts and mounted guns pass).
  internal void FixtureDamagePanel(string id,float raw){DamageSharedPanel(id,raw);}
 }
}

partial class LobbyCases
{
 static float Total(R24LanHealthState h){return h.Health+h.Shield;}
 // ---- 1. rule math -----------------------------------------------------------------------
 static void R19RulesMath()
 {
  foreach(float m in new[]{0f,1f,3f}){
   var r=R24PlayableMatchRules.ForPreset("team_respawn","range").With("headshots",m);
   Need(Math.Abs(r.ScaleHeadshotDamage(40,true)-40*m)<.0001f,"head damage x"+m);
   Need(r.ScaleHeadshotDamage(40,false)==40,"body damage never scaled by HEADSHOTS at x"+m);
  }
  foreach(float m in new[]{0f,1f,2.5f,3f}){
   var r=R24PlayableMatchRules.ForPreset("team_respawn","range").With("build_damage",m);
   Need(Math.Abs(r.ScaleBuildDamage(40)-40*m)<.0001f,"build damage x"+m);
  }
  var big=R24PlayableMatchRules.ForPreset("team_respawn","range").With("headshots",9f).With("build_damage",9f);
  Need(big.Headshots==3f&&big.BuildDamage==3f,"clamped to the 300% limit");
  // Team Revive has no clock; TDM and FFA keep theirs; the stored cap survives a mode switch.
  var revive=R24PlayableMatchRules.ForPreset("team_revive","range");
  Need(revive.TimeLimit==0&&!revive.HasTimeCap&&revive.Pretty("time_limit")=="NO CAP","Team Revive: no time cap");
  Need((int)revive.ReadSourceValue("time_limit")==600,"stored time cap kept for syncing");
  var tdm=revive.With("mode","respawn");Need(tdm.TimeLimit==600&&tdm.HasTimeCap,"switching to Team Deathmatch restores the 10 minute cap");
  Need(R24PlayableMatchRules.ForPreset("free_for_all","range").TimeLimit==600,"FFA keeps its time cap");
  Need(tdm.With("time_limit",120).TimeLimit==120,"TDM cap editable");
  Need(revive.With("time_limit",120).TimeLimit==0,"editing the stored cap in Team Revive still leaves no clock");
  Need(R24PlayableMatchRules.ForPreset("team_revive","archive:5-levels-zyro").TimeLimit==0,"five-level Team Revive has no clock either");
  Need(R24PlayableMatchRules.ForPreset("team_respawn","archive:5-levels-zyro").With("score_limit",25).Rounds==5,"five-level round policy keeps 5 rounds");
  // round-trip through the room payload codec keeps both tunables
  var tuned=tdm.With("headshots",3f).With("build_damage",0f);
  using(var s=new MemoryStream()){using(var w=new BinaryWriter(s))R24LanRoundCodec.Write(w,tuned);
   using(var rd=new BinaryReader(new MemoryStream(s.ToArray()))){R24PlayableMatchRules back=R24LanRoundCodec.Read(rd,"range");Need(back.Headshots==3f&&back.BuildDamage==0f&&back.TimeLimit==600,"payload carries headshots, build damage, time cap");}}
 }
 // ---- 2. room headshot multiplier through a real shot ---------------------------------------
 static float R19HeadLoss(float headshots,bool head)
 {
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
   var rules=R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("starting_shield",0).With("damage",1f).With("headshots",headshots);string hash=new string('b',64);
   StartRound(pair,new HazardWorld(),rules,hash);string guest=pair.Client.OwnActor;for(int i=0;i<4;i++)KeepRaw(pair,hash,0,8);
   float before=Total(Health(pair.Host,guest));var muzzle=new Float3(7.6f,1.6f,0);
   Need(pair.Host.SubmitShot("cx4",0,muzzle,new[]{Dir(muzzle,head?new Float3(0,1.7f,0):new Float3(0,1.25f,0))}),"host volley");
   for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);
   return before-Total(Health(pair.Host,guest));
  }
 }
 static void R19Headshots()
 {
  float body=R19HeadLoss(1f,false),h1=R19HeadLoss(1f,true);
  Need(body>0&&h1>body,"native head damage exceeds body damage at 100% (body="+body+" head="+h1+")");
  float h0=R19HeadLoss(0f,true),h3=R19HeadLoss(3f,true);
  Need(h0==0,"HEADSHOTS 0%: head hit deals no damage, got "+h0);
  Need(Math.Abs(h3-3*h1)<.01f,"HEADSHOTS 300%: head damage is three times 100% ("+h3+" vs "+h1+")");
  Need(Math.Abs(R19HeadLoss(0f,false)-body)<.001f&&Math.Abs(R19HeadLoss(3f,false)-body)<.001f,"body damage unchanged at 0% and 300%");
  Console.WriteLine("PASS room headshots body="+body+" head100="+h1+" head0="+h0+" head300="+h3);
 }
 // ---- 3. room build damage per source ---------------------------------------------------------
 static float R19PanelLoss(float build,Func<R24LanLobby,PlacedPanelRef,Action> source)
 {
  using(var pair=new Pair()){
   var world=new BuildWorld();for(int i=0;i<5;i++)pair.Tick();pair.Host.BindConstructionWorld(world);pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float range){return range;});string hash=new string('b',64);
   StartRound(pair,new HazardWorld(),R24PlayableMatchRules.ForPreset("free_for_all","range").With("zone_enabled",false).With("protection",0f).With("build_damage",build),hash);
   Action raw=delegate{pair.Host.PublishRawTracking(Body(hash,Rev(pair.Host),8,0,0,7,rx:1.5f,ry:1.2f,rz:1.5f));pair.Client.PublishRawTracking(Body(hash,Rev(pair.Client),0,0,0));pair.Tick();};for(int i=0;i<4;i++)raw();
   Need(pair.Host.RequestBuild(1),"place panel: "+pair.Host.LastBuildRejection);for(int i=0;i<4;i++)raw();var first=pair.Host.SharedPanels[0];float health=first.Health;
   var act=source(pair.Host,new PlacedPanelRef{Id=first.Id,X=first.Shape.Center.X,Z=first.Shape.Center.Z});act();for(int i=0;i<8;i++)raw();
   return health-pair.Host.SharedPanels[0].Health;
  }
 }
 sealed class PlacedPanelRef{internal string Id;internal float X,Z;}
 static void R19BuildDamage()
 {
  Func<R24LanLobby,PlacedPanelRef,Action> bullet=delegate(R24LanLobby l,PlacedPanelRef p){return delegate{Need(l.SubmitShot("cx4",0,new Float3(p.X,1.5f,p.Z),new[]{new Float3(0,-1,0)}),"firearm ray at panel");};};
  Func<R24LanLobby,PlacedPanelRef,Action> raw100=delegate(R24LanLobby l,PlacedPanelRef p){return delegate{l.FixtureDamagePanel(p.Id,20);};};
  float b1=R19PanelLoss(1f,bullet),b0=R19PanelLoss(0f,bullet),b25=R19PanelLoss(2.5f,bullet);
  Need(b1>0&&b0==0&&Math.Abs(b25-2.5f*b1)<.05f,"bullet: build damage 0% / 100% / 250% -> "+b0+" / "+b1+" / "+b25);
  float s1=R19PanelLoss(1f,raw100),s0=R19PanelLoss(0f,raw100),s25=R19PanelLoss(2.5f,raw100);
  Need(Math.Abs(s1-20)<.01f&&s0==0&&Math.Abs(s25-50)<.01f,"raw source (melee/blast/mounted choke point): "+s0+" / "+s1+" / "+s25);
  Console.WriteLine("PASS room build damage bullet "+b0+"/"+b1+"/"+b25+" choke point "+s0+"/"+s1+"/"+s25);
 }
 static void R19ScanSources()
 {
  // Every room source feeds raw damage into DamageSharedPanel; the rule is applied exactly once there.
  string root=Environment.GetEnvironmentVariable("ASHFALL_UNITY_ROOT")??"/home/al/ashfall/unity";string net=root+"/Assets/Ashfall/Integration/Playable/Networking/";
  int appliers=0;foreach(string file in Directory.GetFiles(net,"*.cs")){string text=File.ReadAllText(file);int at=0;while((at=text.IndexOf("ScaleBuildDamage",at,StringComparison.Ordinal))>=0){appliers++;at++;}
   if(!file.EndsWith("R24LanLobby.Construction.cs",StringComparison.Ordinal))Need(text.IndexOf("GameplayRules.BuildDamage",StringComparison.Ordinal)<0&&text.IndexOf("roundRules.BuildDamage",StringComparison.Ordinal)<0&&text.IndexOf("ScaleBuildDamage",StringComparison.Ordinal)<0,"room source applies BUILD DAMAGE itself: "+file);}
  Need(appliers==1,"exactly one room applier of BUILD DAMAGE, found "+appliers);
  Console.WriteLine("PASS room sources pass raw damage; one applier");
 }
 // ---- 4. Team Revive never ends on the clock ----------------------------------------------------
 static void R19ReviveNoClock(bool revive)
 {
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();pair.Host.BindCombatWorld(delegate(Float3 o,Float3 d,float r){return r;});
   var rules=R24PlayableMatchRules.ForPreset(revive?"team_revive":"team_respawn","range").With("zone_enabled",false).With("rounds",1).With("time_limit",600).With("protection",0f);string hash=new string('b',64);
   StartRound(pair,new HazardWorld(),rules,hash);string guest=pair.Client.OwnActor;
   Need(pair.Host.SelectedMap.Rules.TimeLimit==(revive?0:600)&&pair.Client.SelectedMap.Rules.TimeLimit==(revive?0:600),"host and guest read the effective time cap");
   double start=pair.Host.SelectedMap.StartsAt;int ticks=0;
   while(pair.Now<start+660&&!pair.Host.SelectedMap.Results&&pair.Host.MapStage==SessionStage.Match){KeepRaw(pair,hash,0,8);ticks++;}
   if(revive){
    Need(pair.Host.SelectedMap.Active&&!pair.Host.SelectedMap.Results&&pair.Host.MapStage==SessionStage.Match&&pair.Host.SelectedMap.Round==1,"Team Revive still running 660 s in (past the old 10 minute cap)");
    Need(!pair.Client.SelectedMap.Results&&pair.Client.RoundElapsed>=600,"guest clock keeps counting up, no cap");
    var h=Health(pair.Host,guest);Need(pair.Host.ApplyHostHealthLoss(guest,h.Revision,h.Health,h.Shield,true).Accepted,"knock down the other team's only player");
    for(int i=0;i<6;i++)KeepRaw(pair,hash,0,8);
    Need(pair.Host.SelectedMap.Results||pair.Host.MapStage!=SessionStage.Match,"match ends once one team remains");
    Console.WriteLine("PASS Team Revive ran "+(pair.Now-start).ToString("F0")+" s (>600) and ended only on elimination");
   }else{
    Need(pair.Host.SelectedMap.Results||pair.Host.MapStage!=SessionStage.Match,"Team Deathmatch still ends on the clock");
    Console.WriteLine("PASS Team Deathmatch ended on the 600 s clock");
   }
  }
 }
 // ---- 5. host changes both settings in warm-up; the guest sees them ---------------------------
 static void R19WarmupSync()
 {
  using(var pair=new Pair()){
   for(int i=0;i<5;i++)pair.Tick();
   string hash=new string('b',64);Need(pair.Host.BeginMapSelection("range",hash,R24PlayableMatchRules.ForPreset("team_revive","range"))&&pair.Host.ReportMapLoaded(hash),"warm-up world");for(int i=0;i<4;i++)pair.Tick();Need(pair.Client.ReportMapLoaded(hash),"guest in warm-up");for(int i=0;i<6;i++)pair.Tick();
   {var w=R24PlayableMatchRules.ForPreset("team_revive","range").With("headshots",2f).With("build_damage",1.5f);Need(pair.Host.CanEditWarmupRules&&pair.Host.HostSetRoomTeamCount(w),"host edits the warm-up world rules");for(int i=0;i<6;i++)pair.Tick();
    var gw=pair.Client.SelectedMap.SourceRules;Need(pair.Client.SelectedMap.Rules==null&&pair.Client.SelectedMap.Active&&gw.Headshots==2f&&gw.BuildDamage==1.5f,"guest sees HEADSHOTS 200% / BUILD DAMAGE 150% in the warm-up world");}
   Need(pair.Host.BeginRoomLobby(),"room lobby");for(int i=0;i<6;i++)pair.Tick();Need(pair.Host.SelectedMap!=null&&pair.Host.SelectedMap.Home,"lobby home");
   var edit=R24PlayableMatchRules.ForPreset("team_revive","range").With("headshots",3f).With("build_damage",0f);
   Need(pair.Host.CanEditRoomTeams&&pair.Host.HostSetRoomTeamCount(edit),"host edits warm-up rules");for(int i=0;i<6;i++)pair.Tick();
   var g=pair.Client.SelectedMap.SourceRules;Need(g!=null&&g.Headshots==3f&&g.BuildDamage==0f,"guest reads HEADSHOTS 300% and BUILD DAMAGE 0% in warm-up");
   var edit2=edit.With("headshots",.5f).With("build_damage",2.5f);Need(pair.Host.HostSetRoomTeamCount(edit2),"host edits again");for(int i=0;i<6;i++)pair.Tick();
   g=pair.Client.SelectedMap.SourceRules;Need(g.Headshots==.5f&&g.BuildDamage==2.5f,"guest follows the second edit");
   Console.WriteLine("PASS warm-up sync host->guest");
  }
 }
 public static void R19Main(){R19RulesMath();Console.WriteLine("PASS rules math");R19Headshots();R19BuildDamage();R19ReviveNoClock(true);R19ReviveNoClock(false);R19WarmupSync();R19ScanSources();Console.WriteLine("PASS RULES19 ("+checks+" checks)");}
}
