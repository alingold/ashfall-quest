from pathlib import Path
import json,re,hashlib,subprocess
ROOT=Path(__file__).resolve().parents[4]; HERE=Path(__file__).resolve().parent
SRC=Path(r'D:/ashfall-r6-staging/frozen-source-r6-v001/source')
pins=json.loads((HERE/'source-pins.json').read_text());mf=SRC.parent/'SOURCE-MANIFEST.json'
assert hashlib.sha256(mf.read_bytes()).hexdigest()==pins['manifestSha256']
for p,sha in pins['files'].items():assert hashlib.sha256((SRC/p).read_bytes()).hexdigest()==sha
text=(SRC/'scripts/net_rules1000.gd').read_text()
def read(name):return json.loads(re.sub(r'(?<=\[)\.(\d)',r'0.\1',re.search(r'const '+name+r'\s*:?=\s*(\{[^\n]+\})',text).group(1)))
defaults=read('DEFAULTS');limits=read('LIMITS')
def prop(k):return 'MapId' if k=='map' else ''.join(x.title() for x in k.split('_'))
def lit(v):return str(v).lower() if isinstance(v,bool) else repr(v)+'f' if isinstance(v,float) else json.dumps(v)
cases=[]
for k,v in defaults.items():cases.append('Check(defaults.'+prop(k)+'=='+lit(v)+',"source default '+k+'");')
for k,b in limits.items():
 cases.append('Check(Math.Abs(Number(defaults.With("'+k+'",-1e9).ReadSourceValue("'+k+'"))-'+str(b[0])+')<1e-5,"lower clamp '+k+'");')
 cases.append('Check(Math.Abs(Number(defaults.With("'+k+'",1e9).ReadSourceValue("'+k+'"))-'+str(b[1])+')<1e-5,"upper clamp '+k+'");')
 for bad in ['float.NaN','double.PositiveInfinity','"12"','true']:
  cases.append('Check(Equals(defaults.With("'+k+'",'+bad+').ReadSourceValue("'+k+'"),defaults.ReadSourceValue("'+k+'")),"malformed '+k+' '+bad.replace('"','')+'");')
for k,v in defaults.items():
 if isinstance(v,bool):cases.append('Check(Equals(defaults.With("'+k+'",1).ReadSourceValue("'+k+'"),defaults.ReadSourceValue("'+k+'")),"strict bool '+k+'");')
code='''using System;
using System.Collections.Generic;
using Ashfall.Integration.Playable.Match;
static class MatchChecks
{
 static int count;
 static void Check(bool condition,string message){count++;if(!condition)throw new Exception(message);}
 static double Number(object value){return Convert.ToDouble(value);}
 static void Main()
 {
  var defaults=R24PlayableMatchRules.ForPreset("team_revive");
'''+''.join('  '+x+'\n' for x in cases)+'''
  var respawn=R24PlayableMatchRules.ForPreset("team_respawn");
  Check(respawn.Mode=="respawn"&&respawn.Teams==2&&respawn.ZoneEnabled,"team respawn source preset");
  var ffa=R24PlayableMatchRules.ForPreset("free_for_all");Check(ffa.Mode=="respawn"&&ffa.Teams==0,"FFA source preset");
  var practice=R24PlayableMatchRules.ForPreset("practice","archive:alfa-aglae-souza");
  Check(practice.Mode=="respawn"&&practice.Damage==0&&practice.Catalog&&practice.InfiniteAmmo&&practice.InfiniteBuilds&&practice.ScoreLimit==0&&practice.TimeLimit==0&&!practice.ZoneEnabled,"practice eight exact source overrides");
  Check(practice.MapId=="archive:alfa-aglae-souza","preset retains selected installed map");
  Check(practice.With("zone_damage",9f).MapId==practice.MapId,"edits retain selected archive");
  Check(practice.With("zone_enabled",true).PresetId=="team_respawn","preset indicator recomputes from source predicates");
  Check(defaults.PresetId=="team_revive"&&respawn.PresetId=="team_respawn"&&ffa.PresetId=="free_for_all"&&practice.PresetId=="practice","all source preset predicates");
  var changed=defaults.With("teams",5).With("zone_wait",22.5f);
  Check(defaults.Teams==2&&defaults.ZoneWait==30&&changed.Teams==5&&changed.ZoneWait==22.5f,"snapshots immutable across edits");
  Check(defaults.With("starting_health",25.9f).StartingHealth==25,"source integer truncation");
  var modeOptions=R24PlayableMatchRules.Choices("mode");modeOptions[0]="corrupted";
  Check(R24PlayableMatchRules.Choices("mode")[0]=="revive","choices do not expose mutable source state");
  var kit=defaults.StartingCatalog;kit[0]=999;Check(defaults.StartingCatalog[0]==0,"starting kit array cannot mutate rules");
  Check(String.Join(",",defaults.StartingCatalog)=="0,2,9","standard source CX4 PX4 M1014 kit");
  Check(String.Join(",",defaults.With("loadout","pistols").StartingCatalog)=="2,7,19","pistols source kit");
  Check(String.Join(",",defaults.With("loadout","swords").StartingCatalog)=="14,15,16","swords source kit");
  Check(String.Join(",",defaults.WithMap("archive:magnum-chaosvr").StartingCatalog)=="7","Magnum map source override");
  Check(defaults.With("mode","deathmatch").Mode=="revive","invalid source mode defaults");
  Check(defaults.With("loadout","fake").Loadout=="standard","invalid source loadout defaults");
  var untrusted=R24PlayableMatchRules.FromSourceValues(new Dictionary<string,object>{{"map","archive:fake"},{"unknown",1}});
  Check(untrusted.MapId=="range","external archive input needs real catalog validator");
  Check(R24PlayableMatchRules.FromSourceValues(new Dictionary<string,object>{{"map","invented"}},id=>true).MapId=="range","source choice whitelist cannot be bypassed by archive validator");
  var validated=R24PlayableMatchRules.FromSourceValues(new Dictionary<string,object>{{"map","archive:alfa-aglae-souza"}},id=>id=="archive:alfa-aglae-souza");
  Check(validated.MapId=="archive:alfa-aglae-souza","validated real catalog archive accepted");
  Check(defaults.Pretty("zone_damage")=="5.0 HP / s"&&defaults.Pretty("zone_speed")=="2.0 m / s"&&defaults.Pretty("zone_wait")=="30 s","source zone units");
  Check(defaults.Pretty("time_limit")=="10 MIN"&&practice.Pretty("time_limit")=="UNLIMITED"&&ffa.Pretty("teams")=="FREE-FOR-ALL","source menu labels");
  Check(R24PlayableMatchRules.Editable("match","zone_damage")&&!R24PlayableMatchRules.Editable("match","zone_speed")&&R24PlayableMatchRules.Editable("lobby","zone_speed"),"source live vs next warmup eligibility");
  bool invalid=false;try{defaults.With("invented",1);}catch(ArgumentException){invalid=true;}Check(invalid,"unknown field not silently invented");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.Notice("downed",10,false)=="YOU ARE A GHOST \u00b7 WAITING FOR REVIVE","actual source downed notice");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.Notice("respawning",1.5f,false)=="YOU DIED \u00b7 RESPAWN 1.5s","actual source given-clock respawn notice");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.Notice("respawning",-3,false)=="YOU DIED \u00b7 RESPAWN 0.0s","source negative remaining clamp");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.Notice("eliminated",0,false)=="ELIMINATED \u00b7 WAITING FOR ROUND","actual source eliminated notice");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.Notice("alive",0,true)=="ROUND COMPLETE","actual source results notice");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.Normalize("Downed")=="downed"&&Ashfall.Integration.Playable.UI.R24MatchLifeText.Normalize(null)==null,"authoritative case/null contract");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.Title(null,false)=="YOU DIED","non-match legacy caption unchanged");
  foreach(string ghost in new[]{"downed","eliminated","respawning","dead"}){
   Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.IsGhost(ghost,false),"source local nonmodal ghost "+ghost);
   Check(!Ashfall.Integration.Playable.UI.R24MatchLifeText.IsGhost(ghost,true),"results excluded from ghost motion "+ghost);
   Check(!Ashfall.Integration.Playable.UI.R24MatchLifeText.UseLegacyDeathModal(true,ghost),"HP0 local ghost never forces legacy modal "+ghost);
  }
  Check(!Ashfall.Integration.Playable.UI.R24MatchLifeText.IsGhost("alive",false)&&!Ashfall.Integration.Playable.UI.R24MatchLifeText.IsGhost(null,false),"alive/Explore not ghost");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.UseLegacyDeathModal(true,null),"nonmatch HP0 retains YOU DIED modal");
  Check(!Ashfall.Integration.Playable.UI.R24MatchLifeText.UseLegacyDeathModal(false,null),"normal Explore HUD remains nonmodal");
  Check(!Ashfall.Integration.Playable.UI.R24MatchLifeText.UseLegacyDeathModal(true,"alive"),"match notice authority tolerates HUD/life publication ordering");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.PanelWidth==.34f&&Ashfall.Integration.Playable.UI.R24MatchLifeText.PanelHeight==.013f&&Ashfall.Integration.Playable.UI.R24MatchLifeText.PanelX==-.02f&&Ashfall.Integration.Playable.UI.R24MatchLifeText.PanelY==-.333f,"source compact panel dimensions/position");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.PanelUnityZ==.803f&&Ashfall.Integration.Playable.UI.R24MatchLifeText.TextUnityZ==.798f,"Godot HUD Z reflected once");
  Check(Ashfall.Integration.Playable.UI.R24MatchLifeText.TextEmHeight==.0095f&&Ashfall.Integration.Playable.UI.R24MatchLifeText.SourceFontSize==32,"source32glyph/em size");
  Console.WriteLine("PASS "+count+" source rules/preset/immutable/domain/life/ghost cases");
 }
}
'''
(HERE/'MatchChecks.cs').write_text(code)
asset=ROOT/'unity/Assets/Ashfall/Integration/Playable/Match/R24PlayableMatchRules.cs'
subprocess.run([r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(HERE/'MatchChecks.exe'),str(asset),str(ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/R24MatchLifeText.cs'),str(HERE/'MatchChecks.cs')],check=True)
r=subprocess.run([str(HERE/'MatchChecks.exe')],check=True,capture_output=True,text=True);print(r.stdout.strip())
(HERE/'checks.log').write_text(r.stdout)
