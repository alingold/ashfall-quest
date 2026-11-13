from pathlib import Path
import json,hashlib,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];SRC=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');manifest=SRC.parent/'SOURCE-MANIFEST.json';assert hashlib.sha256(manifest.read_bytes()).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';data=json.loads(manifest.read_text());rows={r['path']:r for r in data['files']};pins={}
for name in ['scripts/player.gd','scripts/vertical_combat_initialization.gd']:
 sha=hashlib.sha256((SRC/name).read_bytes()).hexdigest();assert sha==rows[name]['sha256'];pins[name]=sha
(HERE/'manual-source-pins.json').write_text(json.dumps({'contentId':data['contentId'],'files':pins},indent=2)+'\n')
state=ROOT/'unity/Assets/Ashfall/Integration/Playable/Calibration/R23CalibrationState.cs';out=HERE/'ManualHeightChecks.exe'
r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(out),str(state),str(HERE/'ManualHeightChecks.cs')],capture_output=True,text=True);(HERE/'manual-compile.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
r=subprocess.run([str(out)],capture_output=True,text=True);(HERE/'manual-results.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);raise SystemExit(r.returncode)
