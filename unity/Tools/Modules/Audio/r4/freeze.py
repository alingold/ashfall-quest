import pathlib,hashlib,json,shutil
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/Audio/r4';E=R/'docs/agent-runs/20261104-three-chats/unity/Audio/r4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
assert not (E/'candidate-manifest.json').exists()
owned=['unity/Assets/Ashfall/Modules/Audio','unity/Assets/Ashfall/Integration/AudioSettings','unity/Tools/Modules/Audio','unity/Tools/Integration/AudioSettings']
files=[]
for rel in owned:
 for p in (R/rel).rglob('*'):
  if p.is_file():
   q=S/'candidate'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);files.append(dict(path=str(p),sha256=sha(p),snapshot=str(q)))
before=json.loads((E/'before.json').read_text());old={x['path']:x['sha256'] for x in before};assert all(sha(pathlib.Path(x['snapshot']))==x['sha256'] for x in before)
changed=[x for x in files if old.get(x['path'])!=x['sha256']]
deps=json.loads((E/'inputs.json').read_text());assert all(sha(pathlib.Path(x['path']))==x['sha256']==sha(pathlib.Path(x['snapshot'])) for x in deps)
historical=[]
for rel in ['docs/agent-runs/20261104-three-chats/unity/Audio/r3/candidate-manifest.json','docs/agent-runs/20261104-three-chats/unity/reviews/Audio-r3/REPORT.md','campaign-staging/20261104-three-chats/unity/Audio/r3/source-audio-inventory.json','docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json']:
 p=R/rel;historical.append(dict(path=str(p),sha256=sha(p)))
evidence=[]
for base in [S,E]:
 for p in sorted(base.rglob('*')):
  if p.is_file():evidence.append(dict(path=str(p),sha256=sha(p)))
manifest=dict(status='candidate-awaiting-fresh-independent-review',api='ASHFALL-AUDIO-POLICY/4; additive publication receipt and retry API',quest_revision='bdba6e30012e1733efd5021fd8ded593f82f26e6',changed_files=changed,production_and_tools=files,consumed_dependencies=deps,historical_readonly=historical,files=evidence,remaining_gates=['fresh independent review','Unity API/import/curve/filter/runtime','missing source climb WAVs','listening/flight/device/network/performance'])
p=E/'candidate-manifest.json';p.write_text(json.dumps(manifest,indent=2));print('MANIFEST',str(p),sha(p));print('changed',len(changed),'frozen files',len(evidence),'production/tools',len(files));print('\n'.join(x['path']+' '+x['sha256'] for x in changed))
(E/'candidate-manifest.sha256.txt').write_text(sha(p)+'  '+str(p)+'\n')
