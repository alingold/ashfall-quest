from pathlib import Path
import json,hashlib,subprocess,sys
R=Path.cwd(); B=R/'campaign-staging/20261104-three-chats/unity/MotionReceiptAccountingR5'/sys.argv[1];B.mkdir(exist_ok=False)
D=R/'docs/agent-runs/20261104-three-chats/unity/MotionReceiptAccountingR5'; records=[];results=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def pin(p):
 q=B/'f'/('%04d'%len(records)+'-'+p.name);q.parent.mkdir(exist_ok=True);q.write_bytes(p.read_bytes());records.append(dict(path=str(p),snapshot=str(q),sha256=sha(q)));return q
parts={}
for n,rel in [('Core','Core'),('Player','Modules/Player'),('Flight','Modules/Flight'),('Climbing','Modules/Climbing'),('FlightMotor','Integration/FlightMotor'),('ClimbingMotor','Integration/ClimbingMotor'),('MotionAuthority','Integration/MotionAuthority')]:
 parts[n]=[pin(p) for p in sorted((R/'unity/Assets/Ashfall'/rel).glob('*.cs'))]
owner=pin(R/'unity/Assets/Ashfall/Modules/Player/Engine/NativeRootPublication.cs');parts['Player'].append(owner)
test=pin(R/'unity/Tools/Integration/MotionReceiptAccounting/AccountingTests.cs');retained=pin(R/'unity/Tools/Integration/MotionAuthority/r4/PublicationTests.cs');admit=pin(R/'unity/Tools/Integration/NativeRootOutcome/AdmissionTests.cs')
for p in (R/'unity/Documentation').glob('*.md'):pin(p)
for p in (R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'):json.loads(p.read_text(encoding='utf-8-sig'));pin(p)
for rel in ['docs/HILL-CLIMB-ORCHESTRATION.md','docs/OVERNIGHT-AGENT-PLAN-2026-11-03.md','docs/AGENT-DISPATCH-BRIEFS-2026-11-03.md','docs/TWO-CHAT-20-AGENT-OVERRIDE.md','docs/agent-runs/20261104-0020/preflight.json','docs/agent-runs/20261104-three-chats/unity/phase21/source-index.json','docs/agent-runs/20261104-three-chats/unity/phase27/ROOT-REPORT.md','docs/agent-runs/20261104-three-chats/unity/GAMEPLAY-CHECKPOINT-v1.md']:pin(R/rel)
pin(Path(__file__).resolve())
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');pin(C)
E=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data');nr=[pin(E/'NetStandard/ref/2.1.0/netstandard.dll')]+[pin(E/('Managed/UnityEngine/UnityEngine.'+n+'Module.dll')) for n in ['Core','Physics','Mathematics']]
(D/(sys.argv[1]+'-inputs.json')).write_text(json.dumps(records,indent=2))
def invoke(args,name):
 p=subprocess.run(list(map(str,args)),capture_output=True,text=True);(B/(name+'.log')).write_text(p.stdout+p.stderr);results.append(dict(name=name,argv=list(map(str,args)),exit=p.returncode,output=p.stdout+p.stderr));(D/(sys.argv[1]+'-results.json')).write_text(json.dumps(results,indent=2));print(name,p.returncode,(p.stdout+p.stderr)[-600:]);return p.returncode
def compile(n,src,refs=(),native=False,exe=False):
 o=B/(n+('.exe' if exe else '.dll'));rsp=B/(n+'.rsp');flags=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(o)+'"']
 if native:flags+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
 rsp.write_text('\n'.join(flags+['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in src]));assert invoke([C,'/noconfig','@'+str(rsp)],n+'-compile')==0;return o
try:
 dll={};order=list(parts)
 for n in order:
  dll[n]=compile(n,parts[n],list(dll.values()))
 for n,t in [('Accounting',test),('Retained63',retained),('Admission136',admit)]:
  x=compile(n,[t],list(dll.values()),exe=True);assert invoke([x],n)==0
 helper=next(p for p in parts['FlightMotor'] if p.name.endswith('-NativeReceiptAccounting.cs'));original=helper.read_text()
 for name,a,b in [('no-feedback','if(!folded)throw new InvalidOperationException("receipt feedback missing");',''),('false-complete','step.CommitValidated();committed=true;','committed=true;'),('double-fold','if(committed||folded)','if(committed)')]:
  m=B/('Mutant-'+name+'.cs');assert a in original;m.write_text(original.replace(a,b));mDll=compile('Mutant-'+name,[p for p in parts['FlightMotor'] if p!=helper]+[m],[dll[k] for k in ['Core','Player','Flight']]);x=compile('Consumer-'+name,[test],[dll[k] for k in ['Core','Player','Flight','Climbing']]+[mDll,dll['ClimbingMotor'],dll['MotionAuthority']],exe=True);assert invoke([x],'Mutant-'+name)!=0
 nd={}
 for n in order:nd[n]=compile('Native'+n,parts[n],nr+list(nd.values()),native=True)
finally:
 (D/(sys.argv[1]+'-drift.json')).write_text(json.dumps([x for x in records if sha(Path(x['path']))!=x['sha256']],indent=2))
 (D/(sys.argv[1]+'-manifest.json')).write_text(json.dumps([dict(path=str(p),sha256=sha(p)) for p in B.rglob('*') if p.is_file()],indent=2))
