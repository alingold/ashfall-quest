from pathlib import Path
import subprocess,json,shutil,hashlib
r=Path(r'C:\Users\aling\Documents\ashfall');o=r/'campaign-staging/20261104-three-chats/unity/PlayerIntegrationR5/r5';d=r/'docs/agent-runs/20261104-three-chats/unity/PlayerIntegrationR5/r5'
sources=list((r/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'))+[o/'before/unity/Assets/Ashfall/Core/Contracts.cs']
paths=list((r/'unity/Assets/Ashfall/Modules/Presentation').glob('*.cs'))+list((r/'unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'))
paths+=[o/'before/unity/Assets/Ashfall/Integration/PersonalSettings'/n for n in ['PlayerSettingsBinding.cs','PersonalSettingsAdapter.cs','RawSettingsJson.cs','Ports.cs']]
rows=[]
for p in paths:
 q=o/'settings-inputs'/(p.parent.name+'-'+p.name);shutil.copyfile(p,q) if q.parent.exists() else (q.parent.mkdir(),shutil.copyfile(p,q));sources.append(q);rows.append(dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
sources += [o/'PlayerEffectiveSettingsPort.cs',r/'unity/Tools/Modules/Player/r5/SettingsCompositionTests.cs'];exe=o/'SettingsComposition.exe';rsp=o/'SettingsComposition.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources]));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(o/'SettingsComposition-compile.log').write_text(c.stdout+c.stderr);print(c.stdout)
result=dict(command=cmd,compile=c.returncode,inputs=rows)
if c.returncode==0:
 t=subprocess.run([str(exe)],capture_output=True,text=True);(o/'SettingsComposition-tests.log').write_text(t.stdout+t.stderr);print(t.stdout+t.stderr);result.update(test=t.returncode,output=t.stdout+t.stderr)
(d/'settings-composition.json').write_text(json.dumps(result,indent=2));raise SystemExit(c.returncode or result.get('test',1))

