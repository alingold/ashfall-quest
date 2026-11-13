from pathlib import Path
import subprocess, json, hashlib, shutil, sys
root=Path(__file__).resolve().parents[4]
stage=root/'campaign-staging/20261104-three-chats/unity/WeaponHandling/r3'
label=sys.argv[1] if len(sys.argv)>1 else 'build'
out=stage/label
out.mkdir(exist_ok=False)
report=root/'docs/agent-runs/20261104-three-chats/unity/WeaponHandling/r3'/label
report.mkdir(exist_ok=True)
prod=root/'unity/Assets/Ashfall/Modules/WeaponHandling'
tool=Path(__file__).parent
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
logs=[]
def run(args,expected=0):
 p=subprocess.run([str(x) for x in args],capture_output=True,text=True)
 logs.append(dict(args=[str(x) for x in args],command=subprocess.list2cmdline([str(x) for x in args]),exitCode=p.returncode,stdout=p.stdout,stderr=p.stderr,expected=expected))
 (report/'commands-and-results.json').write_text(json.dumps(logs,indent=2))
 if p.returncode!=expected: raise RuntimeError(p.stdout+p.stderr)
 return p.stdout
def compile(name,src,reference=None,library=False):
 target=out/(name+('.dll' if library else '.exe'))
 run([csc,'/nologo','/langversion:5','/warnaserror+','/target:'+('library' if library else 'exe'),'/out:'+str(target)]+(['/reference:'+str(reference)] if reference else [])+list(src))
 return target
core=stage/'inputs/Core-Contracts.cs'
review=root/'campaign-staging/20261104-three-chats/unity/reviews/WeaponHandling-r2'
for name in ['SourceClockAcceptance.cs','Independent.cs']:
 shutil.copy2(review/name,stage/'inputs'/name)
before=compile('Before',[core]+sorted((stage/'before/production').rglob('*.cs')),library=True)
probe=compile('BeforeClock',[stage/'inputs/SourceClockAcceptance.cs'],before)
print(run([probe],1).strip())
current=compile('Production',[core]+sorted(prod.rglob('*.cs')),library=True)
for name in ['BehaviorTests','ReviewerRegressions','R2Tests','R3Tests','SourceClockAcceptance']:
 src=[tool/(name+'.cs')] if name!='SourceClockAcceptance' else [stage/'inputs/SourceClockAcceptance.cs']
 if name in ['BehaviorTests','ReviewerRegressions','R2Tests']:src.append(tool/'TestSpawn.cs')
 exe=compile(name,src,current)
 result=run([exe]);(report/(name+'.txt')).write_text(result);print(name+': '+result.splitlines()[-1])
# Preserve the independent harness. Only its explicitly labeled old-defect diagnostic
# is updated in an owned derivative to assert the corrected source clock.
ind=(stage/'inputs/Independent.cs').read_text()
ind=ind.replace('Near(lost.W.Snapshot.CooldownSeconds,.115f)','Near(lost.W.Snapshot.CooldownSeconds,0f)').replace('reproduce tracking-loss frozen timer','tracking-loss timer now recovers').replace('SOURCE DIFFERENCE: tracking-loss 250ms sample leaves cooldown=','CORRECTED SOURCE CLOCK: tracking-loss 250ms sample leaves cooldown=')
(stage/'IndependentR3.cs').write_text(ind)
exe=compile('IndependentR3',[stage/'IndependentR3.cs'],current)
result=run([exe]);(report/'IndependentR3.txt').write_text(result);print(result.splitlines()[-1])
