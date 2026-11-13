"""Direct compiler runner. No engines, wrappers, installs or policy changes."""
from pathlib import Path
import subprocess, json
ROOT=Path(__file__).resolve().parents[5]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/World/r3'
LOG=ROOT/'docs/agent-runs/20261104-three-chats/unity/World/r3'
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs']+sorted((ROOT/'unity/Assets/Ashfall/Modules/World').glob('*.cs'))
pins=[(sources[0],OUT/'inputs/Core-Contracts.cs'),(ROOT/'docs/agent-runs/20261104-three-chats/unity/reviews/World-r2/Holdouts.cs',OUT/'inputs/Holdouts.cs'),(ROOT/'docs/agent-runs/20261104-three-chats/unity/reviews/World-r1/r2/Adversarial.cs',OUT/'inputs/Adversarial.cs')]
for current,pin in pins:
    if current.read_bytes()!=pin.read_bytes():raise RuntimeError('Pinned dependency drift: '+str(current))
samples=[ROOT/'campaign-staging/20261104-three-chats/unity/World'/n for n in ['alfa-aglae-souza.json','battlefield-paintball-gods-pitdevil.json']]
suites=[('BoundaryTests',ROOT/'unity/Tools/Modules/World/r3/BoundaryTests.cs',samples),('Original22',ROOT/'unity/Tools/Modules/World/WorldTests.cs',samples),('SpawnLift5',ROOT/'unity/Tools/Modules/World/SpawnLiftRegression.cs',samples),('Holdouts35',ROOT/'docs/agent-runs/20261104-three-chats/unity/reviews/World-r2/Holdouts.cs',samples[:1])]
results=[];commands=[]
suites.append(('Reviewer24',ROOT/'docs/agent-runs/20261104-three-chats/unity/reviews/World-r1/r2/Adversarial.cs',samples))
for name,test,args in suites:
    exe=OUT/(name+'.exe')
    cmd=[str(CSC),'/nologo','/langversion:5','/warnaserror','/r:System.Runtime.Serialization.dll','/out:'+str(exe)]+list(map(str,sources+[test]))
    commands.append(subprocess.list2cmdline(cmd)); cp=subprocess.run(cmd,capture_output=True,text=True);(LOG/(name+'-compile.log')).write_text(cp.stdout+cp.stderr)
    if cp.returncode: raise RuntimeError(cp.stdout+cp.stderr)
    cmd=[str(exe)]+list(map(str,args));commands.append(subprocess.list2cmdline(cmd));cp=subprocess.run(cmd,capture_output=True,text=True);(LOG/(name+'.log')).write_text(cp.stdout+cp.stderr)
    results.append(dict(suite=name,compileExit=0,testExit=cp.returncode,output=cp.stdout.strip().splitlines()[-1:]));print(name,cp.returncode,cp.stdout.strip().splitlines()[-1:])
    if cp.returncode: raise RuntimeError(cp.stdout+cp.stderr)
(LOG/'commands.txt').write_text('\n'.join(commands)+'\n');(LOG/'test-results.json').write_text(json.dumps(results,indent=2))
