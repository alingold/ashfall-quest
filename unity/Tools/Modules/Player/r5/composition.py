from pathlib import Path
import subprocess,json,shutil
r=Path(r'C:\Users\aling\Documents\ashfall');o=r/'campaign-staging/20261104-three-chats/unity/PlayerIntegrationR5/r5';d=r/'docs/agent-runs/20261104-three-chats/unity/PlayerIntegrationR5/r5'
sources=list((r/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'))+[o/'before/unity/Assets/Ashfall/Core/Contracts.cs']
sources+=list((o/'before/unity/Assets/Ashfall/Modules/Flight').glob('*.cs'))+list((o/'before/unity/Assets/Ashfall/Modules/Climbing').glob('*.cs'))+[r/'unity/Tools/Modules/Player/r5/SiblingCompositionTests.cs']
exe=o/'Composition.exe';rsp=o/'Composition.rsp';rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources]));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);(o/'Composition-compile.log').write_text(c.stdout+c.stderr);print(c.stdout)
result=dict(command=cmd,compile=c.returncode)
if c.returncode==0:
 t=subprocess.run([str(exe)],capture_output=True,text=True);(o/'Composition-tests.log').write_text(t.stdout+t.stderr);print(t.stdout+t.stderr);result.update(test=t.returncode,output=t.stdout+t.stderr)
(d/'composition.json').write_text(json.dumps(result,indent=2));raise SystemExit(c.returncode or result.get('test',1))
