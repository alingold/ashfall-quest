from pathlib import Path
import json,hashlib,shutil
root=Path(__file__).resolve().parents[4]
stage=root/'campaign-staging/20261104-three-chats/unity/WeaponHandling/r3'
report=root/'docs/agent-runs/20261104-three-chats/unity/WeaponHandling/r3'
prod=root/'unity/Assets/Ashfall/Modules/WeaponHandling'
tools=Path(__file__).parent
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def record(p):return dict(path=str(p.relative_to(root)).replace('\\','/'),sha256=sha(p))
inputs=[]
for folder,tree in [('quest-r22-source','quest-r22/source'),('game','game')]:
 for copy in sorted((stage/'inputs'/folder).iterdir()):
  original=root/tree/('assets/data' if copy.suffix=='.json' else 'scripts')/copy.name
  row=record(original);row.update(snapshot=str(copy.relative_to(root)),snapshotSha256=sha(copy),matches=sha(original)==sha(copy));inputs.append(row)
original=root/'unity/Assets/Ashfall/Core/Contracts.cs'
inputs.append(dict(record(original),snapshot=str((stage/'inputs/Core-Contracts.cs').relative_to(root)),matches=sha(original)==sha(stage/'inputs/Core-Contracts.cs')))
(report/'consumed-inputs.json').write_text(json.dumps(inputs,indent=2))
assert all(x['matches'] for x in inputs),'Consumed source drift: do not claim frozen integration'
old=json.loads((root/'docs/agent-runs/20261104-three-chats/unity/WeaponHandling/evidence/r2/candidate-manifest.json').read_text())
(report/'r2-manifest-preserved.json').write_text(json.dumps(dict(path='docs/agent-runs/20261104-three-chats/unity/WeaponHandling/evidence/r2/candidate-manifest.json',sha256=sha(root/'docs/agent-runs/20261104-three-chats/unity/WeaponHandling/evidence/r2/candidate-manifest.json')),indent=2))
frozen=stage/'source'
frozen.mkdir(exist_ok=False)
shutil.copytree(prod,frozen/'production');shutil.copytree(tools,frozen/'tools')
changes=[]
for directory,previous in [(prod,stage/'before/production'),(tools,stage/'before/tools')]:
 for p in sorted(directory.rglob('*')):
  if not p.is_file():continue
  oldpath=previous/p.relative_to(directory)
  if not oldpath.exists() or sha(p)!=sha(oldpath):changes.append(dict(record(p),previousSha256=sha(oldpath) if oldpath.exists() else None))
(report/'changed-files.json').write_text(json.dumps(changes,indent=2))
profiles=[]
for folder in ['quest-r22-source','game']:
 data=json.loads((stage/'inputs'/folder/'one_star_050.json').read_text())
 profiles.append(dict(tree=folder,profiles=[p for p in data['profiles'] if p['index'] in [1,4,7,11,12]]))
(report/'source-profile-trace.json').write_text(json.dumps(profiles,indent=2))
paths=[p for directory in [prod,tools,stage,report] for p in directory.rglob('*') if p.is_file() and p.name!='candidate-manifest.json']
manifest=dict(candidate='WeaponHandling-r3/1',questRevision='bdba6e30012e1733efd5021fd8ded593f82f26e6',status='implementation candidate; independent review required',files=[record(p) for p in sorted(set(paths))])
target=report/'candidate-manifest.json';target.write_text(json.dumps(manifest,indent=2))
print('Frozen '+str(len(manifest['files']))+' files; changed production/tools='+str(len(changes))+'; no consumed input drift')
print('Manifest SHA256 '+sha(target))
