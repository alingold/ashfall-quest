from health_epoch_r2 import R,S,D
import hashlib,json,shutil
tools=S/'final-tools';tools.mkdir(exist_ok=True)
for folder in ['unity/Tools/Integration/HostEpochAuthority','unity/Tools/Modules/HealthStates']:
 for p in (R/folder).iterdir():
  if p.is_file():q=tools/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
changes=[]
for x in json.loads((D/'before.json').read_text()):
 p=__import__('pathlib').Path(x['source'])
 if p.is_file():
  h=hashlib.sha256(p.read_bytes()).hexdigest()
  if h!=x['sha256']:changes.append(dict(source=str(p),before=x['sha256'],after=h,owned=any(t in str(p).replace('\\','/') for t in ['Modules/HealthStates/','Integration/HostEpochAuthority/'])))
# Current source drift is observational; it is not attribution of concurrent unowned edits.
(D/'observed-changes.json').write_text(json.dumps(changes,indent=2))
rows=[]
for p in sorted(S.rglob('*')):
 if p.is_file():rows.append(dict(path=str(p),size=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
for p in sorted(D.iterdir()):
 if p.is_file() and p.name not in ['SHA256-manifest.json','SHA256-manifest.digest.txt']:rows.append(dict(path=str(p),size=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
p=__import__('pathlib').Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
rows.append(dict(path=str(p),size=p.stat().st_size,sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
b=json.dumps(rows,indent=2).encode();(D/'SHA256-manifest.json').write_bytes(b);(D/'SHA256-manifest.digest.txt').write_text(hashlib.sha256(b).hexdigest()+'\n')
print('sealed',len(rows),'files; digest',hashlib.sha256(b).hexdigest())
