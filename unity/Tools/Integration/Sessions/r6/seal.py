from pathlib import Path
import json,hashlib,shutil
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/SessionAdmissionR6';S=R/'campaign-staging/20261104-three-chats/unity/SessionAdmissionR6'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
before=json.loads((D/'before.json').read_text());changes=[]
for x in before:
 p=Path(x['path']);assert sha(Path(x['snapshot']))==x['sha256'].upper()
 if sha(p)!=x['sha256'].upper():changes.append(dict(path=str(p),before_sha256=x['sha256'].upper(),sha256=sha(p)))
added=[*R.glob('unity/Tools/Integration/Sessions/r6/*.*'),*R.glob('unity/Tools/Modules/Networking/r6/*.*')]
for p in added:changes.append(dict(path=str(p),before_sha256=None,sha256=sha(p)))
for x in changes:
 p=Path(x['path']);q=S/'final'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);x['snapshot']=str(q)
(D/'changed-files-sha256.json').write_text(json.dumps(changes,indent=2))
# Pin candidate source symbols alongside exact file hashes.
symbols={}
for x in changes:
 p=Path(x['path'])
 if p.suffix=='.cs':symbols[str(p)]=dict(sha256=sha(p),symbols=[dict(line=i,text=l.strip()) for i,l in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1) if any(k in l for k in ['PrepareJoinTrusted','ValidateJoin(','CommitJoinValidated','AbortJoin(','network-generation/','IngressSequence','hostThread','IdentityHistoryCount'])])
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
entries=[]
for base in [S,D]:
 for p in sorted(base.rglob('*')):
  if p.is_file() and p.name not in ['candidate-manifest.json','manifest.sha256.txt']:
   entries.append(dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size))
manifest=D/'candidate-manifest.json';manifest.write_text(json.dumps(dict(status='UNREVIEWED candidate; fresh independent review required',files=entries),indent=2));(D/'manifest.sha256.txt').write_text(sha(manifest)+'  '+str(manifest)+'\n')
assert all(sha(Path(x['path']))==x['sha256'] for x in entries)
print('changed production',len([x for x in changes if '\\Assets\\' in x['path']]),'changed/added files',len(changes),'sealed entries',len(entries),'manifest SHA256',sha(manifest))
print('production hashes',json.dumps([x for x in changes if '\\Assets\\' in x['path']],indent=2))
