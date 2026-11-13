import hashlib,json,subprocess,sys,shutil
from pathlib import Path
R=Path.cwd(); T=R/'unity/Tools/Integration/DesktopPlaytest/DesktopConstructionR1'
B=R/'campaign-staging/20261104-three-chats/unity/DesktopConstructionR1'; O=B/sys.argv[1];O.mkdir(parents=True,exist_ok=False)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
paths=[R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'unity/Assets/Ashfall/Modules/InventoryItems/BuildChargeLedger.cs']
for folder in ['Modules/Construction','Modules/ConstructionCandidate','Integration/ConstructionGeometry','Integration/DesktopPlaytest/Construction']:
    paths+=list((R/'unity/Assets/Ashfall'/folder).glob('*.cs'))
paths+=[R/'unity/Assets/Ashfall/Integration/DesktopPlaytest/DesktopPlaytestContext.cs',R/'unity/Assets/Ashfall/Integration/DesktopPlaytest/Arena/DesktopArena.cs']+list(T.glob('*'))
records=[];sources=[]
for p in paths:
    s=O/'sources'/p.relative_to(R);s.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,s)
    records.append(dict(path=str(p.relative_to(R)),snapshot=str(s.relative_to(R)),sha256=sha(s)))
    if s.suffix=='.cs':sources.append(s)
(O/'inputs.json').write_text(json.dumps(records,indent=2))
csc=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');results=[]
def compile(name,files,native=False):
    out=O/(name+('.dll' if native else '.exe'));rsp=O/(name+'.rsp')
    args=['/nologo','/langversion:5','/warnaserror+','/target:'+('library' if native else 'exe'),'/out:"'+str(out)+'"']
    if native:
        args+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
        editor=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
        refs=[editor/'NetStandard/ref/2.1.0/netstandard.dll']
        refs+=list((editor/'Managed/UnityEngine').glob('UnityEngine*.dll'))
        refs+=[R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/ScriptAssemblies/Unity.InputSystem.dll']
        args+=['/r:"'+str(p)+'"' for p in refs]
        (O/'native-reference-sha256.json').write_text(json.dumps([dict(path=str(p),sha256=sha(p)) for p in refs],indent=2))
    else:
        args+=['/r:"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll"','/r:"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll"']
    args+=['"'+str(p)+'"' for p in files];rsp.write_text('\n'.join(args))
    cmd=[str(csc),'/noconfig','@'+str(rsp)];run=subprocess.run(cmd,capture_output=True,text=True)
    (O/(name+'-compile.log')).write_text(run.stdout+run.stderr);print(name,run.returncode,run.stdout,run.stderr)
    entry=dict(name=name,command=cmd,exit=run.returncode,sha256=sha(out) if out.exists() else None);results.append(entry)
    if not native and run.returncode==0:
        run=subprocess.run([str(out)],capture_output=True,text=True);(O/(name+'-test.log')).write_text(run.stdout+run.stderr)
        entry['testExit']=run.returncode;print(run.stdout,run.stderr)
compile('ApiTests',[p for p in sources if p.name not in ['DesktopConstruction.cs','NativeFixture.cs','DesktopPlaytestContext.cs','DesktopArena.cs']])
compile('NativeApi',[p for p in sources if p.name!='ApiTests.cs'],True)
(O/'results.json').write_text(json.dumps(results,indent=2))
(O/'current-comparison.json').write_text(json.dumps([dict(path=r['path'],frozenSha256=r['sha256'],currentSha256=sha(R/r['path']),same=sha(R/r['path'])==r['sha256']) for r in records],indent=2))
assert all(r['exit']==0 and r.get('testExit',0)==0 for r in results)
