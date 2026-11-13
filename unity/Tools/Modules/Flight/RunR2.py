"""Neutral checks only. Emits exclusively beneath Flight r2; never invokes r1 wrappers."""
from pathlib import Path
import subprocess,json,sys
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/Flight/r2'
LOG=ROOT/'docs/agent-runs/20261104-three-chats/unity/Flight/evidence/r2'
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
before='--before' in sys.argv
label='before' if before else 'after'
source=ROOT/'campaign-staging/20261104-three-chats/unity/Flight/revisions/r1-before-r2' if before else ROOT
prod=source/'unity/Assets/Ashfall/Modules/Flight'
core=ROOT/'unity/Assets/Ashfall/Core/Contracts.cs'
sources=[core]+[prod/x for x in ['FlightPorts.cs','FlightMachine.cs','Capsules.cs']]
dest=OUT/label;dest.mkdir(parents=True,exist_ok=True);LOG.mkdir(parents=True,exist_ok=True)
review=ROOT/'campaign-staging/20261104-three-chats/unity/reviews/Flight-r1/Adversarial.cs'
results=[]
def run(name,args):
    result=subprocess.run([str(a) for a in args],capture_output=True,text=True)
    (LOG/(label+'-'+name+'.txt')).write_text(subprocess.list2cmdline([str(a) for a in args])+'\nEXIT '+str(result.returncode)+'\n'+result.stdout+result.stderr,encoding='utf-8')
    results.append({'name':name,'exit':result.returncode})
    print(label,name,result.returncode,result.stdout[-100:].strip())
    return result.returncode
for name,test in [('builder',ROOT/'unity/Tools/Modules/Flight/FlightTests.cs'),('reviewer',review),('regression',ROOT/'unity/Tools/Modules/Flight/R2Tests.cs')]:
    exe=dest/(name+'.exe')
    code=run(name+'-compile',[CSC,'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+sources+[test])
    if code==0:run(name+'-tests',[exe])
run('library-compile',[CSC,'/nologo','/langversion:5','/warnaserror+','/target:library','/out:'+str(dest/'Flight.dll')]+sources+[prod/'Engine/FlightRootAdapter.cs'])
(LOG/(label+'-results.json')).write_text(json.dumps(results,indent=2)+'\n')
sys.exit(1 if any(r['exit'] for r in results) else 0)
