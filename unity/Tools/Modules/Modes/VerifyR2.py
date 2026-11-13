"""Neutral Modes r2 verification. No Unity launch or policy changes; r1 binaries untouched."""
from pathlib import Path
import subprocess, json, hashlib
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/Modes/r2'
EVIDENCE=ROOT/'docs/agent-runs/20261104-three-chats/unity/Modes/evidence/r2'
SNAP=ROOT/'campaign-staging/20261104-three-chats/unity/Modes/revisions/r1-before-r2'
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
BASE=['/nologo','/langversion:5','/warn:4','/warnaserror+']
log=[]
def run(args, expected=0):
 p=subprocess.run([str(x) for x in args],capture_output=True,text=True)
 log.append({'command':[str(x) for x in args],'exit':p.returncode,'stdout':p.stdout,'stderr':p.stderr})
 if p.returncode!=expected:
  (EVIDENCE/'checks.json').write_text(json.dumps(log,indent=2));raise RuntimeError(log[-1])
 return p.stdout
def compile(folder,name,sources,library=False):
 folder.mkdir(parents=True,exist_ok=True)
 target=folder/(name+('.dll' if library else '.exe'))
 args=BASE+['/target:'+('library' if library else 'exe'),'/out:"'+str(target)+'"']
 if not library:args+=['/reference:"'+str(folder/'Ashfall.Modes.dll')+'"']
 args+=['"'+str(s)+'"' for s in sources]
 rsp=folder/(name+'.rsp');rsp.write_text('\n'.join(args))
 run([CSC,'@'+str(rsp)]);return target
OUT.mkdir(parents=True,exist_ok=True);EVIDENCE.mkdir(parents=True,exist_ok=True)
core=ROOT/'unity/Assets/Ashfall/Core/Contracts.cs'
review=ROOT/'campaign-staging/20261104-three-chats/unity/reviews/Modes-r1/Adversarial.cs'
# Reproduce the original failure with snapshotted sources, never original outputs.
before=OUT/'before'
compile(before,'Ashfall.Modes',[core]+sorted((SNAP/'unity/Assets/Ashfall/Modules/Modes').glob('*.cs')),True)
oldtests=compile(before,'BuilderTests',[SNAP/'unity/Tools/Modules/Modes/Tests.cs'])
assert '64 behavioral assertions' in run([oldtests])
adversarial=compile(before,'Adversarial',[review])
assert 'RESULT passed=21 failed=4' in run([adversarial],1)
compile(OUT,'Ashfall.Modes',[core]+sorted((ROOT/'unity/Assets/Ashfall/Modules/Modes').glob('*.cs')),True)
for name,source in [('BuilderTests',ROOT/'unity/Tools/Modules/Modes/Tests.cs'),('R2Tests',ROOT/'unity/Tools/Modules/Modes/R2Tests.cs'),('Adversarial',review)]:
 exe=compile(OUT,name,[source]);run([exe])
(EVIDENCE/'checks.json').write_text(json.dumps(log,indent=2))
(EVIDENCE/'checks.txt').write_text('\n'.join('COMMAND: '+repr(row['command'])+'\nEXIT: '+str(row['exit'])+'\n'+row['stdout']+row['stderr'] for row in log))
print('PASS: fresh before/after compiles; r1 64 builder PASS, reviewer 21 PASS / 4 FAIL; r2 all suites PASS')
