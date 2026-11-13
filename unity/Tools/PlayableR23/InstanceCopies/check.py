from pathlib import Path
import subprocess,json
T=Path(__file__).resolve().parent;R=T.parents[3];P=R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
src=sorted(set([R/p for p in (R/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+list(P.glob('*.cs'))))
CSC='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
def compile(name,files,options):
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(T/name)]+options+[str(p)for p in files];rsp=T/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"'for a in args));r=subprocess.run([CSC,'/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/(name+'.log')).write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
compile('Cases.exe',src+[T/'Cases.cs'],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(T/'Cases.exe')],capture_output=True,text=True);(T/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');refs=list((E/'Managed/UnityEngine').glob('*.dll'))+list((E/'NetStandard/ref/2.1.0').glob('*.dll'))
compile('Hands.Runtime.dll',src,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p)for p in refs]);print('PASS installed Unity API compile (all Hands runtime; no engine)')
compile('Hands.Editor.dll',src,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER;UNITY_EDITOR']+['/reference:'+str(p)for p in refs]);print('PASS installed Unity Editor API compile (no engine)')
# Retained production-owner regression suites, with output kept inside this task folder.
for label,files,defines in [
 ('Retained97',[R/'unity/Tools/PlayableR23/HandsWeapons'/n for n in ['Mechanics.cs','SourceMechanics.cs','BothHandsAndGains.cs','SprMechanics.cs']],[]),
 ('Smg71',[R/'unity/Tools/PlayableR23/Smg4/Cases.cs'],[]),
 ('Shotgun43',[R/'unity/Tools/PlayableR23/ArsenalGameplay/Cases.cs'],[]),
 ('CxPx48',[R/'unity/Tools/PlayableR23/Cx4Px4/Cases.cs'],[]),
 ('Magnum48',[R/'unity/Tools/PlayableR23/SidearmRuntime/Cases.cs'],['/define:PRODUCTION_ROUND']),
 ('Feedback19',[P/'R23WeaponFeedbackChecks.cs'],['/define:ASHFALL_FEEDBACK_CHECKS'])]:
 compile(label+'.exe',sorted(set(src+files)),['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+defines)
 r=subprocess.run([str(T/(label+'.exe'))],capture_output=True,text=True);(T/(label+'-results.txt')).write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr;print('PASS retained',label,'production cases')
# Verify the handoff's live profile array and immutable rarity values against preserved source.
import re,hashlib
SOURCE=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
rarity=json.loads((SOURCE/'assets/data/weapon_rarity_r6.json').read_text())['weapons'];code=(P/'R23WeaponInstances.cs').read_text();bindings=0
for profile in ['mk18','akm','spr','magnum','cx4','px4','m1014','ump','p90','mp5','mp9','1911','tec9','rfb','fal','s85']:
 line=next(x for x in code.splitlines()if 'case "'+profile+'"'in x)
 arrays=[[float(x.removesuffix('f'))for x in row.split(',')]for row in re.findall(r'new float\[\]\{([^}]+)\}',line)]
 for actual,key in zip(arrays,['magazine','body','head','recoil','spread']):assert len(actual)==5 and all(abs(a-float(b))<1e-7 for a,b in zip(actual,rarity[profile][key])),(profile,key);bindings+=1
hands=(P/'R23HandsWeapons.cs').read_text();ids=re.search(r'readonly string\[\] weaponIds=\{([^}]+)',hands)[1];live=re.findall(r'"([^"]+)"',ids);assert len(live)==16 and len(set(live))==16
for field,fitfield in [('sourceMuzzles','muzzle'),('magazineHomes','magazineHome')]:
 expression=re.search(r'readonly Vector3\[\] '+field+r'=\{([^}]+)',hands)[1]
 actual=re.findall(r'new Vector3\(([^)]+)\)|Vector3\.zero',expression)
 for index in range(11,16):
  fit=json.loads((R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'/(live[index]+'-fit.json')).read_text())
  values=[float(v.removesuffix('f'))for v in actual[index].split(',')];assert all(abs(a-b)<1e-7 for a,b in zip(values,fit[fitfield]));bindings+=1
manifest=json.loads((R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons/audio.json').read_text())['cues'];fragment=json.loads((R/'unity/Tools/PlayableR23/Catalog5/catalog5-audio-cues.json').read_text())['cues']
for cue in fragment:assert cue in manifest;bindings+=1
records={name:hashlib.sha256((SOURCE/name).read_bytes()).hexdigest()for name in ['scripts/weapon_inventory.gd','scripts/weapon_rack.gd','scripts/weapon_rarity_r6.gd','assets/data/weapon_rarity_r6.json','scripts/catalog_firearm.gd']}
(T/'source-bindings.json').write_text(json.dumps(dict(preservedSource=str(SOURCE),sourceSha256=records,liveProfiles=live,checks=bindings,engineRun=False,deviceRun=False),indent=2)+'\n');print('PASS',bindings,'exact source rarity / live fit / exact audio bindings')
compile('Catalog100.exe',sorted(set(src+[R/'unity/Tools/PlayableR23/Catalog5/Cases.cs'])),['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(T/'Catalog100.exe')],capture_output=True,text=True);(T/'Catalog100-results.txt').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr;print(r.stdout.strip())
