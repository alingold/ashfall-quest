from pathlib import Path
import hashlib,json,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];B=ROOT/'unity/Assets/Ashfall'
prepared=json.loads((HERE/'prepared.json').read_text());snapshot=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
for path,sha in prepared['sourceInputs'].items():assert hashlib.sha256((snapshot/path).read_bytes()).hexdigest()==sha,path
payload=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Loot/weapon-boards-r18.json';assert hashlib.sha256(payload.read_bytes()).hexdigest()==prepared['payloadSha256']
files=[B/'Core/Contracts.cs',B/'Integration/Playable/World/Loot/R24WeaponRackPlan.cs',HERE/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(HERE/'Cases.exe')]+[str(p) for p in files]
(HERE/'cases.rsp').write_text('\n'.join('"'+a+'"' for a in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(HERE/'cases.rsp')],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'Cases.exe')],capture_output=True,text=True);(HERE/'cases.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
print('Frozen source/payload hash bindings PASS; actual native boards remain unrun')
