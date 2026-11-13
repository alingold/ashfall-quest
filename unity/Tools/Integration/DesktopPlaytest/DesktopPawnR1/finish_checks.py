from pathlib import Path
import hashlib,json,subprocess
R=Path(r'C:\Users\aling\Documents\ashfall'); O=R/'campaign-staging/20261104-three-chats/unity/DesktopPawnR1/compile03'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
rows=json.loads((O/'inputs.json').read_text())
src=[R/r['snapshot'] for r in rows]
rsp=O/'neutral.rsp';exe=O/'Neutral.exe'
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/main:DesktopPawnNeutralChecks','/out:'+str(exe)]
args+=[str(p) for p in src]
rsp.write_text('\n'.join('"'+a+'"' for a in args))
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)]
x=subprocess.run(cmd,cwd=R,capture_output=True,text=True)
result=dict(command=cmd,compileExit=x.returncode,compileOutput=x.stdout+x.stderr)
if x.returncode==0:
    x=subprocess.run([str(exe)],cwd=O,capture_output=True,text=True);result.update(runCommand=[str(exe)],runExit=x.returncode,output=x.stdout+x.stderr)
(O/'neutral-results.json').write_text(json.dumps(result,indent=2));print(result)
project=R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
refs=json.loads((O/'references.json').read_text());installed=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Managed\UnityEngine')
provenance=[]
for r in refs:
    p=Path(r['path']); q=installed/p.name
    if p.name=='Unity.InputSystem.dll':q=project/'Library/ScriptAssemblies/Unity.InputSystem.dll'
    if q.exists():provenance.append(dict(reference=str(p),installed=str(q),installedSha256=sha(q),match=sha(q)==r['sha256']))
(O/'installed-reference-comparison.json').write_text(json.dumps(provenance,indent=2))
package=next((project/'Library/PackageCache').glob('com.unity.inputsystem@*'))
api=[package/'package.json']+[next(package.rglob(name)) for name in ['Keyboard.cs','Mouse.cs','KeyControl.cs','DeltaControl.cs','ButtonControl.cs']]
apirows=[]
for p in api:
    content=p.read_text(encoding='utf-8-sig');apirows.append(dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size))
(O/'input-system-source-read.json').write_text(json.dumps(apirows,indent=2))
print('Input System',json.loads((package/'package.json').read_text())['version'])
