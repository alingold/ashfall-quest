from pathlib import Path
import hashlib,json,subprocess,sys
ROOT=Path(__file__).resolve().parents[5]
STAGE=ROOT/'campaign-staging/20261104-three-chats/unity/Flight/r3'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/Flight/phase03'
number=1
while (STAGE/('run%02d'%number)).exists(): number+=1
out=STAGE/('run%02d'%number);out.mkdir()
logs=DOC/('run%02d'%number);logs.mkdir(parents=True)
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
prod=ROOT/'unity/Assets/Ashfall/Modules/Flight'
inputs=STAGE/'inputs'
core=inputs/'Contracts.cs'
flight=sorted(prod.glob('*.cs'))
player=sorted((inputs/'Player-phase03').glob('*.cs'))
bridge=sorted((ROOT/'unity/Assets/Ashfall/Integration/FlightMotor').glob('*.cs'))
records=[]
for p in sorted(inputs.rglob('*')):
 if p.is_file(): records.append({'path':str(p.relative_to(ROOT)),'sha256':sha(p)})
# Pin every external public dependency actually consumed, compile only owned snapshots.
drift=[]
for p in player+[inputs/'Player-phase03/Engine/UnityPlayerDriver.cs',core]:
 live=ROOT/'unity/Assets/Ashfall/Modules/Player'/p.relative_to(inputs/'Player-phase03') if p!=core else ROOT/'unity/Assets/Ashfall/Core/Contracts.cs'
 drift.append({'snapshot':str(p.relative_to(ROOT)),'live':str(live.relative_to(ROOT)),'snapshotSha256':sha(p),'liveSha256':sha(live),'matches':sha(p)==sha(live)})
old=json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/Flight/evidence/r2/consumed-source-hashes.json').read_text(encoding='utf-8-sig'))
source=[]
for entry in old:
 p=Path(entry['path']);p=p if p.is_absolute() else ROOT/p
 source.append({'path':str(p),'expected':entry['sha256'],'actual':sha(p),'matches':sha(p)==entry['sha256']})
(logs/'inputs.json').write_text(json.dumps({'snapshots':records,'dependencyDrift':drift,'r2SourceComparison':source},indent=2))
results=[]
def run(name,args):
 args=list(map(str,args));r=subprocess.run(args,capture_output=True,text=True)
 (logs/(name+'.txt')).write_text(subprocess.list2cmdline(args)+'\nEXIT '+str(r.returncode)+'\n'+r.stdout+r.stderr)
 results.append({'name':name,'argv':args,'exit':r.returncode});print(name,r.returncode,r.stdout[-200:])
 return r.returncode
for name,test in [('original',ROOT/'unity/Tools/Modules/Flight/FlightTests.cs'),('r1-review',ROOT/'campaign-staging/20261104-three-chats/unity/reviews/Flight-r1/Adversarial.cs'),('r2-regression',ROOT/'unity/Tools/Modules/Flight/R2Tests.cs'),('r3-composition',ROOT/'unity/Tools/Integration/FlightMotor/R3Tests.cs')]:
 exe=out/(name+'.exe');deps=[core]+flight+(player+bridge if name=='r3-composition' else [])
 if run(name+'-compile',[CSC,'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+deps+[test])==0:run(name+'-tests',[exe])
run('neutral-library',[CSC,'/nologo','/langversion:5','/warnaserror+','/target:library','/out:'+str(out/'FlightMotor.dll')]+[core]+flight+player+bridge+list((prod/'Engine').glob('*.cs')))
(logs/'results.json').write_text(json.dumps(results,indent=2))
sys.exit(1 if any(r['exit'] for r in results) else 0)

