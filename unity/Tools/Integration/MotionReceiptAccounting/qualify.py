from pathlib import Path
import json,hashlib,subprocess,shutil
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/MotionReceiptAccountingR5';B=S/'qualification01';B.mkdir(exist_ok=False);D=R/'docs/agent-runs/20261104-three-chats/unity/MotionReceiptAccountingR5';records=[];results=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def pin(p,q):
 q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());records.append(dict(path=str(p),snapshot=str(q),sha256=sha(q)));return q
# Preserve exact separately compiled run08 native closure. Never execute these DLLs.
for p in (S/'run08').glob('Native*.dll'):pin(p,B/p.name)
for p in (S/'run08').glob('*.rsp'):pin(p,B/'responses'/p.name)
for p in (S/'run01/f').glob('*Module.dll'):pin(p,B/'refs'/p.name.split('-',1)[1])
for p in (S/'run01/f').glob('*-netstandard.dll'):pin(p,B/'refs/netstandard.dll')
# Retain and run ONLY the historical managed feature closures. Never native DLLs.
old=R/'campaign-staging/20261104-three-chats/unity/reviews/NativeRootOutcomeReviewR1'
for directory,names in [('h-run04',['RetainedPreparedOwnersTests','RetainedTransactionsR2Tests','RetainedR7Tests','RetainedR6Tests','RetainedR2Tests','RetainedFlightTests','PublicConsumer']),('h-preservation02',['RootPreservation','PadPreservation','FrameRetry'])]:
 for p in (old/directory).glob('*.dll'):
  if not p.name.startswith('Native'):pin(p,B/directory/p.name)
 for name in names:
  x=pin(old/directory/(name+'.exe'),B/directory/(name+'.exe'));p=subprocess.run([str(x)],capture_output=True,text=True);(B/(name+'.log')).write_text(p.stdout+p.stderr);results.append(dict(name=name,argv=[str(x)],exit=p.returncode,expected=1 if directory=='h-preservation02' else 0,output=p.stdout+p.stderr));assert p.returncode==(1 if directory=='h-preservation02' else 0)
# External native owner fixtures are compilation-only.
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');refs=list((B/'refs').glob('*.dll'))+[B/'NativeCore.dll',B/'NativePlayer.dll']
p=pin(R/'unity/Tools/Integration/NativeRootOutcome/NativeFixtures.cs',B/'NativeOwnerFixtures.cs')
rsp=B/'NativeOwnerFixtures.rsp';rsp.write_text('\n'.join(['/nologo','/noconfig','/langversion:5','/warn:4','/warnaserror+','/nostdlib+','/define:UNITY_6000_0_OR_NEWER','/target:library','/out:"'+str(B/'NativeOwnerFixtures.dll')+'"']+['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"']))
args=[str(C),'/noconfig','@'+str(rsp)];p=subprocess.run(args,capture_output=True,text=True);(B/'native-compile.log').write_text(p.stdout+p.stderr);results.append(dict(name='NativeOwnerFixtures-compile-only',argv=args,exit=p.returncode,output=p.stdout+p.stderr));assert p.returncode==0
(D/'qualification01-inputs.json').write_text(json.dumps(records,indent=2));(D/'qualification01-results.json').write_text(json.dumps(results,indent=2));(D/'qualification01-manifest.json').write_text(json.dumps([dict(path=str(p),sha256=sha(p)) for p in B.rglob('*') if p.is_file()],indent=2));print([(x['name'],x['exit']) for x in results])
