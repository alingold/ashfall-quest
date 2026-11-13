from pathlib import Path
import subprocess,json,hashlib,shutil
root=Path(__file__).resolve().parents[4]
out=root/'campaign-staging/20261104-three-chats/unity/ParityHarness/r2'
ev=root/'docs/agent-runs/20261104-three-chats/unity/ParityHarness/evidence/r2'
num=1
while (out/('run-%02d'%num)).exists(): num+=1
build=out/('run-%02d'%num); build.mkdir(parents=True)
evidence=ev/('run-%02d'%num); evidence.mkdir(parents=True)
sources=[root/'unity/Assets/Ashfall/Core/Contracts.cs',root/'unity/Tools/ToolchainProbe/ConversionProbe.cs']
sources+=sorted((root/'unity/Assets/Ashfall/Modules/ParityHarness').glob('*.cs'))
sources+=sorted((root/'unity/Tools/Modules/ParityHarness').glob('*.cs'))
sources+=sorted((root/'unity/Tools/Modules/ParityHarness/Integration').glob('*.cs'))
for module in ['Flight','WeaponHandling']: sources+=sorted((root/('unity/Assets/Ashfall/Modules/'+module)).glob('*.cs'))
records=[]; frozen=[]
for src in sources:
    rel=src.relative_to(root); dest=build/'inputs'/rel; dest.parent.mkdir(parents=True,exist_ok=True)
    data=src.read_bytes(); dest.write_bytes(data); dest.chmod(0o444); frozen.append(dest)
    records.append({'path':rel.as_posix(),'sha256':hashlib.sha256(data).hexdigest(),'snapshot':str(dest)})
(evidence/'consumed-sources.json').write_text(json.dumps(records,indent=2))
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
exe=build/'LinkedHarness.exe'
command=[csc,'/nologo','/target:exe','/langversion:5','/warn:4','/warnaserror+','/reference:System.Web.Extensions.dll','/main:Ashfall.Modules.ParityHarness.LinkedMain','/out:'+str(exe)]+[str(x) for x in frozen]
(evidence/'compile-command.txt').write_text(subprocess.list2cmdline(command))
p=subprocess.run(command,capture_output=True,text=True); (evidence/'compile.log').write_text(p.stdout+p.stderr)
if p.returncode: print(p.stdout+p.stderr); raise SystemExit(p.returncode)
results=[]
for label,args,expected in [('linked',[str(evidence/'linked-results.json')],0),('repeat',[str(evidence/'repeat-results.json')],0),('strict',[str(evidence/'strict-results.json'),'--strict'],2),('unbound',[str(evidence/'unbound-results.json'),'5'],0),('self-test',['--self-test'],0)]:
    target=exe
    if label in ['unbound','self-test']:
        target=build/'UnboundHarness.exe'
        if not target.exists():
            cmd=command.copy(); cmd[cmd.index('/main:Ashfall.Modules.ParityHarness.LinkedMain')]='/main:Ashfall.Modules.ParityHarness.HarnessMain'; cmd[cmd.index('/out:'+str(exe))]='/out:'+str(target)
            p=subprocess.run(cmd,capture_output=True,text=True); (evidence/'unbound-compile.log').write_text(p.stdout+p.stderr)
            (evidence/'unbound-compile-command.txt').write_text(subprocess.list2cmdline(cmd))
            if p.returncode: raise RuntimeError(p.stdout+p.stderr)
    p=subprocess.run([str(target)]+args,capture_output=True,text=True); (evidence/(label+'.log')).write_text(p.stdout+p.stderr)
    results.append({'test':label,'exit':p.returncode,'expected':expected,'command':subprocess.list2cmdline([str(target)]+args)})
    print(label,p.returncode,p.stdout[-300:],p.stderr)
def semantics(path):
    obj=json.loads(path.read_text());
    for row in obj['Results']:
        for k in ['MinTicks','MedianTicks','MaxTicks']: row.pop(k)
    return obj
same=semantics(evidence/'linked-results.json')==semantics(evidence/'repeat-results.json')
drift=[x['path'] for x in records if hashlib.sha256((root/x['path']).read_bytes()).hexdigest()!=x['sha256']]
validation={'commands':results,'repeat_semantics_equal':same,'source_drift_after_run':drift,'build':str(build),'evidence':str(evidence)}
(evidence/'validation.json').write_text(json.dumps(validation,indent=2))
print(json.dumps(validation,indent=2))
raise SystemExit(0 if same and all(x['exit']==x['expected'] for x in results) else 1)
