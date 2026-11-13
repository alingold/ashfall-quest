from pathlib import Path
import json,hashlib
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkingPeerR4/r5';S=R/'campaign-staging/20261104-three-chats/unity/NetworkingPeerR4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def rec(p):return dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size)
# Source-level drift classifications; final compiled snapshot remains fixed.
comparison=[]
for d in sorted((S/'r5').glob('attempt-*')):
 for p in (d/'inputs').rglob('*.cs'):
  original=R/p.relative_to(d/'inputs')
  comparison.append(dict(attempt=d.name,path=str(original),snapshotSHA256=sha(p),currentSHA256=sha(original),match=sha(p)==sha(original)))
(D/'dependency-comparison.json').write_text(json.dumps(comparison,indent=2))
world=R/'campaign-staging/20261104-three-chats/world/world-accepted-v2';w=json.loads((world/'handoff.json').read_text())
worldChecks=[]
for item in w['approvedChanges']:
 p=world/item['patch']; target=R/item['target']
 worldChecks.append(dict(patch=rec(p),expectedPatch=item['patchSha256'],patchMatch=sha(p).lower()==item['patchSha256'].lower(),target=rec(target),expectedTarget=item['afterSha256'],targetMatch=sha(target).lower()==item['afterSha256'].lower(),status=item['status']))
(D/'world-patch-verification.json').write_text(json.dumps(worldChecks,indent=2))
production=R/'unity/Assets/Ashfall/Modules/Networking';before=S/'before/production';latest=S/'r5/attempt-04/production'
changes=[]
for p in production.rglob('*.cs'):
 old=before/p.relative_to(production);compiled=latest/p.relative_to(production)
 changes.append(dict(current=rec(p),beforeSHA256=sha(old) if old.exists() else None,compiledSHA256=sha(compiled),compiledMatch=sha(p)==sha(compiled)))
(D/'source-symbols-and-hashes.json').write_text(json.dumps(changes,indent=2))
# Freeze all new and preserved evidence; manifest/receipt deliberately excluded from self-reference.
roots=[production,R/'unity/Tools/Modules/Networking',S,R/'docs/agent-runs/20261104-three-chats/unity/NetworkingPeerR4']
files=[]
for root in roots:
 for p in sorted(root.rglob('*')):
  if p.is_file() and p.name not in ['candidate-manifest.json','manifest.sha256.txt'] and '__pycache__' not in p.parts:files.append(rec(p))
manifest=D/'candidate-manifest.json';manifest.write_text(json.dumps(dict(status='UNREVIEWED_CANDIDATE',candidate='NetworkingPeerR4/r5',finalCompiledAttempt='attempt-04',knownExpectedFailure='Unchanged current NetworkingTests implicit lifetime capacity assertion; preserved and explicitly budgeted separately',files=files),indent=2))
(D/'manifest.sha256.txt').write_text(sha(manifest)+'  candidate-manifest.json\n')
print('Manifest',sha(manifest),'files',len(files));print('Production compiled matches',all(x['compiledMatch'] for x in changes));print('World patch matches',all(x['patchMatch'] for x in worldChecks),'target matches',all(x['targetMatch'] for x in worldChecks));print('Latest external drift',sum(not x['match'] for x in comparison if x['attempt']=='attempt-04'))
