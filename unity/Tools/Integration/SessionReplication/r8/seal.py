import pathlib,json,hashlib,re,difflib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/SessionStateReplicationR8';D=R/'docs/agent-runs/20261104-three-chats/unity/SessionStateReplicationR8';U=R/'docs/agent-runs/20261104-three-chats/unity'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert not (D/'output-manifest.json').exists()
baseline=json.loads((D/'inputs.json').read_text());audit=[]
for p in baseline:
 q=R/p['snapshot'];assert sha(q)==p['sha256'];live=R/p['path'];audit.append(dict(path=p['path'],frozen=p['sha256'],current=sha(live),changed=sha(live)!=p['sha256']))
(D/'input-audit.json').write_text(json.dumps(audit,indent=2))
index=json.loads((U/'phase13/source-index.json').read_text(encoding='utf-8-sig'));phase=[]
for x in index:
 p=R/x['snapshot'];assert sha(p).lower()==x['sha256'].lower();live=R/x['original'];phase.append(dict(path=x['original'],snapshot=x['snapshot'],sha256=x['sha256'],current=sha(live),changed=sha(live).lower()!=x['sha256'].lower()))
(D/'phase13-audit.json').write_text(json.dumps(phase,indent=2))
accepted=[]
def walk(x):
 if isinstance(x,list):
  for v in x:walk(v)
 elif isinstance(x,dict):
  if 'path' in x and 'expected' in x:
   p=pathlib.Path(x['path']);p=p if p.is_absolute() else R/p
   accepted.append(dict(path=str(p),expected=x['expected'],actual=sha(p),match=sha(p).lower()==x['expected'].lower()))
  for v in x.values():walk(v)
for p in U.glob('accepted-neutral-v*.json'):walk(json.loads(p.read_text(encoding='utf-8-sig')))
(D/'accepted-audit.json').write_text(json.dumps(accepted,indent=2))
assert all(x['match'] for x in accepted)
refs=[]
for rel in ['quest-r22/source/scripts/net_session1000.gd','game/scripts/net_session1000.gd','docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR2/native-api07/command.json','docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR2/REPORT.md']:
 p=R/rel;q=S/'reference'/rel;q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());refs.append(dict(path=rel,snapshot=str(q.relative_to(R)),sha256=sha(q)))
(D/'additional-inputs.json').write_text(json.dumps(refs,indent=2))
paths=list((R/'unity/Assets/Ashfall/Integration/SessionReplication').glob('*.cs'))+[R/'unity/Assets/Ashfall/Integration/Sessions/SessionNetworkCoordinator.cs']
changed=[];symbols=[];diff=[]
for p in paths:
 before=S/'before'/p.relative_to(R)
 if before.exists() and sha(before)==sha(p):continue
 q=S/'candidate'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes());changed.append(dict(path=str(p.relative_to(R)),snapshot=str(q.relative_to(R)),sha256=sha(p),before=sha(before) if before.exists() else None))
 text=p.read_text();symbols += [dict(path=str(p.relative_to(R)),line=i,symbol=line.strip()) for i,line in enumerate(text.splitlines(),1) if re.search(r'\b(public|internal|private)\b.*\(',line)]
 diff.extend(difflib.unified_diff(before.read_text().splitlines(True) if before.exists() else [],text.splitlines(True),fromfile='a/'+str(p.relative_to(R)),tofile='b/'+str(p.relative_to(R))))
(D/'candidate.patch').write_text(''.join(diff));(D/'changed-files.json').write_text(json.dumps(changed,indent=2));(D/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
# Verify the final successful suite used these exact candidate and pinned dependencies.
latest=sorted(S.glob('run[0-9][0-9]'))[-1];runpins=json.loads((D/latest.name/'inputs.json').read_text())
for x in runpins:assert sha(R/x['snapshot'])==x['sha256']
for x in changed:
 match=[y for y in runpins if y['path']==x['path']];assert match and match[0]['sha256']==x['sha256']
assert json.loads((D/latest.name/'result.json').read_text())['passExpected']
(D/'SUMMARY.json').write_text(json.dumps(dict(status='UNREVIEWED candidate',run=latest.name,changedFiles=len(changed),phase13Snapshots=len(phase),phase13CurrentDifferences=sum(x['changed'] for x in phase),acceptedExpectations=len(accepted),acceptedMatches=sum(x['match'] for x in accepted),M1='OPEN',engineExecution=False),indent=2))
outputs=[]
for root in [S,D,R/'unity/Tools/Integration/SessionReplication/r8']:
 for p in sorted(root.rglob('*')):
  if p.is_file():outputs.append(dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size))
(D/'output-manifest.json').write_text(json.dumps(outputs,indent=2));(D/'output-manifest.sha256').write_text(sha(D/'output-manifest.json')+'\n')
print((D/'SUMMARY.json').read_text())
