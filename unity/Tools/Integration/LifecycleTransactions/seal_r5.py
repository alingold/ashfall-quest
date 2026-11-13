import pathlib,json,hashlib,re
R=pathlib.Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/LifecycleBindingsR5';D=R/'docs/agent-runs/20261104-three-chats/unity/LifecycleBindingsR5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
initial=json.loads((D/'inputs.json').read_text());owned=['unity/Assets/Ashfall/Modules/HealthStates/PreparedLifecycle.cs','unity/Assets/Ashfall/Modules/Sessions/PreparedLifeProjection.cs']
owned += [p.relative_to(R).as_posix() for p in (R/'unity/Assets/Ashfall/Integration/LifecycleTransactions').rglob('*.cs')]
owned += [p.relative_to(R).as_posix() for p in (R/'unity/Tools/Integration/LifecycleTransactions').rglob('*') if p.is_file()]
rows=[]
for rel in owned:
 p=R/rel;b=next((x['sha256'] for x in initial if x['path']==rel),None);rows.append(dict(path=rel,beforeSha256=b,sha256=sha(p),bytes=p.stat().st_size))
(D/'changed-sources.json').write_text(json.dumps(rows,indent=2))
symbols=[]
for rel in owned:
 if not rel.endswith('.cs'):continue
 for n,line in enumerate((R/rel).read_text().splitlines(),1):
  if re.search(r'\b(public|internal)\b',line):symbols.append(dict(path=rel,line=n,declaration=line.strip(),sha256=sha(R/rel)))
(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
drift=[]
for row in initial:
 p=R/row['path'];h=sha(p) if p.exists() else None
 if h!=row['sha256']:drift.append(dict(path=row['path'],pinned=row['sha256'],current=h,owned=row['path'] in owned))
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
assert all(sha(S/'inputs'/r['path'])==r['sha256'] for r in initial)
accepted=[]
for p in (S/'context/docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'):
 obj=json.loads(p.read_text(encoding='utf-8-sig'));accepted.append(dict(name=p.name,sha256=sha(p),status=obj.get('status'),keys=list(obj)))
(D/'accepted-read.json').write_text(json.dumps(accepted,indent=2))
files=[]
for root in [S,D]:
 for p in root.rglob('*'):
  if p.is_file() and p.name not in ['SHA256-manifest.json','SHA256-manifest.json.sha256']:files.append(dict(path=p.relative_to(R).as_posix(),sha256=sha(p),bytes=p.stat().st_size))
manifest=D/'SHA256-manifest.json';manifest.write_text(json.dumps(files,indent=2));(D/'SHA256-manifest.json.sha256').write_text(sha(manifest)+'\n')
print('Sealed',len(files),'artifacts;',len(drift),'changed pinned sources, of which',sum(not x['owned'] for x in drift),'external; accepted manifests',len(accepted))
