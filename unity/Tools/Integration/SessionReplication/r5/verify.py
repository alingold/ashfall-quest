import pathlib,json,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/SessionReplicationR5';S=R/'campaign-staging/20261104-three-chats/unity/SessionReplicationR5'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
checks=[]
def walk(o):
 if isinstance(o,dict):
  if 'path' in o and ('expected' in o or 'sha256' in o):
   p=pathlib.Path(o['path']);p=p if p.is_absolute() else R/p;expected=o.get('expected',o.get('sha256'))
   if isinstance(expected,str) and len(expected)==64:checks.append(dict(path=str(p),expected=expected,actual=sha(p) if p.is_file() else None))
  for v in o.values():walk(v)
 elif isinstance(o,list):
  for v in o:walk(v)
for p in sorted((R/'docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json')):walk(json.loads(p.read_text(encoding='utf-8-sig')))
(D/'accepted-verification.json').write_text(json.dumps(dict(checks=checks,mismatches=[x for x in checks if x['actual']!=x['expected'].upper()]),indent=2))
p=R/'docs/agent-runs/20261104-three-chats/unity/SessionReplication/r4/candidate-manifest.json';o=json.loads(p.read_text(encoding='utf-8-sig'));rows=[]
for row in o['files']:
 q=R/row['path'];actual=sha(q);rows.append(dict(row,actual=actual,match=actual==row['sha256'].upper()))
(D/'r4-immutable-verification.json').write_text(json.dumps(dict(manifest_sha256=sha(p),entries=len(rows),rows=rows),indent=2))
# Latest observation compared to executed dependencies, never substitute bytes.
pins=json.loads((D/'attempt-02/inputs.json').read_text());drift=[]
for row in pins:
 p=pathlib.Path(row['path'])
 if '\\dependencies\\' in row['snapshot']:drift.append(dict(row,current_sha256=sha(p),match=sha(p)==row['sha256']))
(D/'final-dependency-drift.json').write_text(json.dumps(drift,indent=2))
print('Accepted checks',len(checks),'mismatches',sum(x['actual']!=x['expected'].upper() for x in checks));print('R4',len(rows),'matching',sum(x['match'] for x in rows));print('Final executed dependencies',len(drift),'current drift',sum(not x['match'] for x in drift))
