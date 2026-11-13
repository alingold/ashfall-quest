from pathlib import Path
import json,hashlib,subprocess,sys
R=Path.cwd(); B=R/'campaign-staging/20261104-three-chats/unity/MotionReceiptAccountingR5'/sys.argv[1];B.mkdir(exist_ok=False)
D=R/'docs/agent-runs/20261104-three-chats/unity/MotionReceiptAccountingR5'; records=[];results=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def pin(p):
 q=B/'f'/('%04d'%len(records)+'-'+p.name);q.parent.mkdir(exist_ok=True);q.write_bytes(p.read_bytes());records.append(dict(path=str(p),snapshot=str(q),sha256=sha(q)));return q
parts={}
records=json.loads((D/'run01-inputs.json').read_text())
def frozen(rel):return Path(next(x['snapshot'] for x in records if x['path']==str(R/rel)))
for n,rel in [('Core','Core'),('Player','Modules/Player'),('Flight','Modules/Flight'),('Climbing','Modules/Climbing'),('FlightMotor','Integration/FlightMotor'),('ClimbingMotor','Integration/ClimbingMotor'),('MotionAuthority','Integration/MotionAuthority')]:
 parts[n]=[Path(x['snapshot']) for x in records if Path(x['path']).parent==R/'unity/Assets/Ashfall'/rel and x['path'].endswith('.cs')]
parts['FlightMotor']=[p for p in parts['FlightMotor'] if not p.name.endswith('-ReservedCapsuleRoute.cs')]
owner=frozen('unity/Assets/Ashfall/Modules/Player/Engine/NativeRootPublication.cs');parts['Player'].append(owner)
# Explicit owned candidate overlays; all external dependencies stay pinned to run01.
for name in ['MotionPublicationPreparation.cs','NativeMotionReceiptAssociation.cs']:
 src=R/'unity/Assets/Ashfall/Integration/MotionAuthority'/name
 q=pin(src);parts['MotionAuthority']=[q if p.name.endswith('-'+name) else p for p in parts['MotionAuthority']]
test=pin(R/'unity/Tools/Integration/MotionReceiptAccounting/AccountingTests.cs');retained=frozen('unity/Tools/Integration/MotionAuthority/r4/PublicationTests.cs');admit=frozen('unity/Tools/Integration/NativeRootOutcome/AdmissionTests.cs')
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
E=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
nr=[frozen(str(E/'NetStandard/ref/2.1.0/netstandard.dll'))]+[frozen(str(E/('Managed/UnityEngine/UnityEngine.'+n+'Module.dll'))) for n in ['Core','Physics','Mathematics']]
def invoke(args,name):
 p=subprocess.run(list(map(str,args)),capture_output=True,text=True);(B/(name+'.log')).write_text(p.stdout+p.stderr);results.append(dict(name=name,argv=list(map(str,args)),exit=p.returncode,output=p.stdout+p.stderr));(D/(sys.argv[1]+'-results.json')).write_text(json.dumps(results,indent=2));print(name,p.returncode,(p.stdout+p.stderr)[-600:]);return p.returncode
def compile(n,src,refs=(),native=False,exe=False):
 o=B/(n+('.exe' if exe else '.dll'));rsp=B/(n+'.rsp');flags=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(o)+'"']
 if native:flags+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
 if not native:refs=list(refs)+[C.parent/'System.dll',C.parent/'System.Core.dll']
 rsp.write_text('\n'.join(flags+['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in src]));assert invoke([C,'/noconfig','@'+str(rsp)],n+'-compile')==0;return o
try:
 dll={};order=list(parts)
 for n in order:
  dll[n]=compile(n,parts[n],list(dll.values()))
 for n,t in [('Accounting',test),('Retained63',retained),('Admission136',admit)]:
  x=compile(n,[t],list(dll.values()),exe=True);assert invoke([x],n)==0
 helper=next(p for p in parts['FlightMotor'] if p.name.endswith('-NativeReceiptAccounting.cs'));original=helper.read_text()
 for name,a,b in [('no-feedback','if(!folded)throw new InvalidOperationException("receipt feedback missing");',''),('false-complete','step.CommitValidated();committed=true;','committed=true;'),('repeat-commit','if(committed)return;','')]:
  m=B/('Mutant-'+name+'.cs');assert a in original;m.write_text(original.replace(a,b));mDll=compile('Mutant-'+name,[p for p in parts['FlightMotor'] if p!=helper]+[m],[dll[k] for k in ['Core','Player','Flight']]);x=compile('Consumer-'+name,[test],[dll[k] for k in ['Core','Player','Flight','Climbing']]+[mDll,dll['ClimbingMotor'],dll['MotionAuthority']],exe=True);assert invoke([x],'Mutant-'+name)!=0
 nd={}
 for n in order:nd[n]=compile('Native'+n,parts[n],nr+list(nd.values()),native=True)
 fixture=pin(R/'unity/Tools/Integration/MotionReceiptAccounting/NativeAccountingHolds.cs')
 compile('NativeAccountingHolds',[test,fixture],nr+list(nd.values()),native=True)
finally:
 (D/(sys.argv[1]+'-drift.json')).write_text(json.dumps([x for x in records if sha(Path(x['path']))!=x['sha256']],indent=2))
 (D/(sys.argv[1]+'-manifest.json')).write_text(json.dumps([dict(path=str(p),sha256=sha(p)) for p in B.rglob('*') if p.is_file()],indent=2))

