from pathlib import Path
import json,hashlib,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];src=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');m=src.parent/'SOURCE-MANIFEST.json';assert hashlib.sha256(m.read_bytes()).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';manifest=json.loads(m.read_text());rows={x['path']:x for x in manifest['files']};pins={}
for f in ['scripts/rescue_training_dummy.gd','scripts/main.gd','scripts/revive_paddles.gd']:
 sha=hashlib.sha256((src/f).read_bytes()).hexdigest();assert rows[f]['sha256']==sha;pins[f]=sha
(HERE/'source-pins.json').write_text(json.dumps({'contentId':manifest['contentId'],'files':pins},indent=2)+'\n')
base=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
files=[base/'R23WeaponPoseMath.cs',base/'R24RevivePaddlesState.cs',base/'R24RescueTrainingDummyState.cs',base/'R24RescueTrainingDummyGeometry.cs',HERE/'Cases.cs']
# WeaponPoseMath has a profile resolver dependency; use existing complete pure owner source set.
files=list(set(files+[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+list(base.glob('*.cs'))))
rsp=HERE/'cases.rsp';rsp.write_text('\n'.join('"'+str(x)+'"'for x in ['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(HERE/'Cases.exe')]+files))
r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
r=subprocess.run([str(HERE/'Cases.exe')],capture_output=True,text=True);(HERE/'results.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);raise SystemExit(r.returncode)

