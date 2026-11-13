from pathlib import Path
import json,hashlib,re
R=Path.cwd();E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableCustodyRecoveryR5';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableCustodyRecoveryR5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def save(name,data):(E/name).write_text(json.dumps(data,indent=2))
before=json.loads((E/'before.json').read_text());final=json.loads((E/'final08/inputs.json').read_text())
owned=['Modules/Pickups/','Integration/ConsumableTransactions/','Integration/InventoryTransfers/TransferBridge.cs']
def own(p):return any(x in Path(p).as_posix() for x in owned)
candidate=[dict(path=r['path'],snapshot=r['snapshot'],sha256=r['sha256']) for r in final if r['path'].endswith('.cs') and own(r['path'])]
old={r['path']:r for r in before}
changed=[r for r in candidate if r['path'] not in old or r['sha256']!=old[r['path']]['sha256']]
save('candidate-source.json',changed)
drift=[]
for row in final:
 p=R/row['path']
 if p.exists() and sha(p)!=row['sha256']:drift.append(dict(path=row['path'],frozen=row['sha256'],current=sha(p),owned=own(row['path'])))
save('current-comparison.json',dict(note='One captured comparison only, no current behavior requalification',differences=drift))
used=set()
for rsp in (O/'final08').glob('*.rsp'):
 for line in rsp.read_text().splitlines():
  value=line.strip('"')
  if value.endswith('.cs') and '\\src\\' in value:used.add(value)
save('compiled-source-current-comparison.json',[dict(snapshot=p,path=next((row['path'] for row in final if str(R/row['snapshot'])==p),None),sha256=sha(Path(p)),currentMatches=next((sha(R/row['path'])==sha(Path(p)) for row in final if str(R/row['snapshot'])==p),None)) for p in sorted(used)])
save('toolchain.json',[dict(path=str(p),sha256=sha(p)) for p in [Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'),Path(r'C:\Users\aling\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe')]])
indexes=[]
for phase in ['phase16','phase18']:
 p=E.parent/phase/'source-index.json';rows=json.loads(p.read_text());fail=[]
 for row in rows:
  q=R/row['snapshot']
  if not q.exists() or sha(q)!=row['sha256'].upper():fail.append(row)
 indexes.append(dict(phase=phase,indexSha256=sha(p),count=len(rows),failures=fail))
save('root-index-verification.json',indexes)
accepted=[]
for p in E.parent.glob('accepted-neutral-v*.json'):
 data=json.loads(p.read_bytes());accepted.append(dict(path=str(p.relative_to(R)),sha256=sha(p),status=data.get('status'),scope=data.get('scope'),accepted=[dict(name=x.get('name'),scope=x.get('scope')) for x in data.get('accepted',[])],modules=data.get('modules'),policy='Exact historical snapshots immutable; no extension/native/M1 acceptance inferred'))
save('accepted-read.json',accepted)
symbols=[]
for row in candidate:
 p=R/row['snapshot']
 symbols += [dict(path=row['path'],line=n,symbol=line.strip()) for n,line in enumerate(p.read_text().splitlines(),1) if re.search(r'\b(public|internal)\b.*\(',line)]
save('source-symbols.json',symbols)
tool=O/'sealed-tools';tool.mkdir(exist_ok=True)
for p in (R/'unity/Tools/Integration/ConsumableTransactions').glob('*R5*'):
 if p.is_file():(tool/p.name).write_bytes(p.read_bytes())
verification=[]
for name in ['before.json','context.json','final08/inputs.json']:
 rows=json.loads((E/name).read_text());bad=[r for r in rows if sha(R/r['snapshot'])!=r['sha256']];verification.append(dict(manifest=name,count=len(rows),failures=bad))
save('frozen-verification.json',verification)
manifest=[]
for root in [E,O]:
 for p in sorted(root.rglob('*')):
  if p.is_file() and p.name not in ['SHA256-manifest.json','SHA256-manifest.sha256']:
   manifest.append(dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=sha(p)))
save('SHA256-manifest.json',manifest)
(E/'SHA256-manifest.sha256').write_text('\n'.join(r['sha256']+'  '+r['path'] for r in manifest)+'\n')
print(json.dumps(dict(changed=changed,rootIndexes=indexes,currentDifferences=drift,frozen=verification,artifacts=len(manifest)),indent=2))
