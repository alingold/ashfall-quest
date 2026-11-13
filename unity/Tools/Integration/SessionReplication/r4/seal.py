import hashlib,json,pathlib
ROOT=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
D=ROOT/r'docs\agent-runs\20261104-three-chats\unity\SessionReplication\r4'
S=ROOT/r'campaign-staging\20261104-three-chats\unity\SessionReplication\r4'
P=ROOT/r'unity\Assets\Ashfall\Integration\SessionReplication'
T=ROOT/r'unity\Tools\Integration\SessionReplication'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def save(p,data):
 assert not p.exists(),str(p)+' already sealed'
 p.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
verification={'frozen':[],'owned_mismatch':[],'external_dependency_drift':[]}
for module in ['Networking','Sessions']:
 m=ROOT/('docs/agent-runs/20261104-three-chats/unity/'+module+'/r3/candidate-manifest.json');data=read(m)
 rows=data.get('files',data.get('artifacts',[]));bad=[r for r in rows if sha(ROOT/r['path'])!=r['sha256'].upper()]
 verification['frozen'].append({'module':module,'manifest_sha256':sha(m),'records':len(rows),'mismatches':bad})
pins=read(D/'attempt-07'/'inputs-before.json')
for p in pins:
 current=sha(pathlib.Path(p['path']));snap=sha(pathlib.Path(p['snapshot']))
 if current!=p['sha256'] or snap!=p['sha256']:
  key='owned_mismatch' if '\\SessionReplication\\' in p['path'] else 'external_dependency_drift'
  verification[key].append(dict(p,current=current,snapshot_sha256=snap))
verification['final_consumed_inputs']=pins
accepted=read(ROOT/r'docs\agent-runs\20261104-three-chats\unity\accepted-neutral-v1.json')
verification['accepted_neutral']=[{'path':r['path'],'expected':r['expected'],'actual':sha(pathlib.Path(r['path']))} for r in accepted['sourceChecks']]
world=ROOT/r'campaign-staging\20261104-three-chats\world\world-accepted-v2\handoff.json'
world_review=read(ROOT/r'docs\agent-runs\20261104-three-chats\world\r6-handoff-review\review.json')
verification['world_handoff']={'actual':sha(world),'expected':world_review['handoffSha256'].upper(),'revision':world_review['candidateRevision']}
assert world_review['candidateRevision']=='ce3b3b0771d779d93625bbe501c35c8953ab617924b4ae57b41cc1b299b5c0a3'
netinputs=read(ROOT/r'docs\agent-runs\20261104-three-chats\unity\Networking\r3\inputs.json')
verification['source_reconciliation']=[dict(r,actual=sha(ROOT/r['path'])) for r in netinputs if r['path'].startswith(('quest-r22','game'))]
assert not verification['owned_mismatch'] and not verification['external_dependency_drift']
assert all(not r['mismatches'] for r in verification['frozen'])
assert all(r['expected']==r['actual'] for r in verification['accepted_neutral'])
assert verification['world_handoff']['expected']==verification['world_handoff']['actual']
assert all(r['sha256']==r['actual'] for r in verification['source_reconciliation'])
# Freeze current reviewable owned sources, keeping every historical attempt intact.
for root,label in [(P,'production'),(T,'tools')]:
 for src in root.rglob('*'):
  if src.is_file():
   dst=S/'sealed'/label/src.relative_to(root);dst.parent.mkdir(parents=True,exist_ok=True)
   assert not dst.exists();dst.write_bytes(src.read_bytes())
save(D/'final-verification.json',verification)
paths=[]
for root in [P,T,S,D]:paths.extend(p for p in root.rglob('*') if p.is_file())
manifest={'status':'UNREVIEWED_COORDINATOR_CANDIDATE','final_attempt':'attempt-07','files':[{'path':str(p.relative_to(ROOT)),'sha256':sha(p),'bytes':p.stat().st_size} for p in sorted(paths)],'remaining_gates':['fresh independent review','trusted authentication and generation registry','saturated Session departure lifecycle cleanup','full-domain replication','Unity/UTP native runtime','packaged desktop/XR/Quest/WAN/security']}
save(D/'candidate-manifest.json',manifest)
(D/'manifest.sha256.txt').write_text(sha(D/'candidate-manifest.json')+'  candidate-manifest.json\n')
print('SEALED',sha(D/'candidate-manifest.json'),'files',len(paths),'frozen',[(r['module'],r['records']) for r in verification['frozen']],'input pins',len(pins),'drift0; accepted-neutral14 match')
