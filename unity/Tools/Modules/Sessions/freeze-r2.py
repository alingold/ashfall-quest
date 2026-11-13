"""Freeze local r2 evidence and precise read-only dependency identities after tests."""
import hashlib
import json
from pathlib import Path
import shutil

root = Path(__file__).resolve().parents[4]
ev = root / 'docs/agent-runs/20261104-three-chats/unity/Sessions/evidence/r2'
stage = root / 'campaign-staging/20261104-three-chats/unity/Sessions'
manifest_path = ev / 'candidate-manifest.json'
if manifest_path.exists():
    raise RuntimeError('Evidence already frozen; do not overwrite')

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def record(path):
    return dict(path=str(path), bytes=path.stat().st_size, sha256=digest(path))

def write(name, value):
    (ev / name).write_text(json.dumps(value, indent=2), encoding='utf-8')

old_inputs = json.loads((ev.parents[1] / 'source-inputs.json').read_text(encoding='utf-8-sig'))
inputs = []
for entry in old_inputs['inputs']:
    path = Path(entry['path'])
    item = record(path)
    item['r1_sha256'] = entry['sha256']
    item['matches_r1'] = item['sha256'] == entry['sha256']
    inputs.append(item)
for relative in ['unity/Documentation/CONTRACTS-v1.md', 'docs/agent-runs/20261104-three-chats/unity/ownership.json', 'docs/agent-runs/20261104-three-chats/unity/resume-01/reconciliation.json', 'docs/agent-runs/20261104-three-chats/unity/resume-01/source-checkpoint.json', 'docs/agent-runs/20261104-three-chats/unity/reviews/Sessions-r1/REPORT.md', 'campaign-staging/20261104-three-chats/unity/reviews/Sessions-r1/AdversarialTests.cs']:
    inputs.append(record(root / relative))
write('source-inputs.json', dict(quest_baseline=old_inputs['questBaseline'], inputs=inputs))

checkpoint = json.loads((root / 'docs/agent-runs/20261104-three-chats/unity/resume-01/source-checkpoint.json').read_text(encoding='utf-8-sig'))
expected = {str(Path(e['Path'])): e['Hash'] for e in checkpoint}
dependencies = []
for module, names in [('Networking', ['Authority.cs','Ports.cs','Protocol.cs']), ('HealthStates',['HealthService.cs','Ports.cs']), ('Modes',['RoundController.cs','Ports.cs'])]:
    for name in names:
        path = root / 'unity/Assets/Ashfall/Modules' / module / name
        saved = ev / 'dependencies' / module / name
        saved.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, saved)
        item = record(saved)
        item.update(source_path=str(path), checkpoint_sha256=expected.get(str(path)), source_matches_snapshot=digest(path)==digest(saved))
        item['matches_checkpoint'] = item['sha256'] == item['checkpoint_sha256']
        dependencies.append(item)
write('dependency-inputs.json', dependencies)

old = json.loads((ev.parents[1] / 'candidate-manifest.json').read_text(encoding='utf-8-sig'))
history = []
for item in old['files']:
    path = Path(item['path'])
    current = record(path)
    current.update(r1_sha256=item['sha256'], unchanged=digest(path)==item['sha256'])
    history.append(current)
changed_history = [r for r in history if not r['unchanged']]
assert len(changed_history)==1 and Path(changed_history[0]['path']).name=='Session.cs', changed_history
write('historical-preservation.json', history)

snap = stage / 'revisions/r1-before-r2'
before_session = digest(snap / 'production/Session.cs')
assert before_session == expected[str(root / 'unity/Assets/Ashfall/Modules/Sessions/Session.cs')]
assert digest(snap / 'production/Ports.cs') == digest(root / 'unity/Assets/Ashfall/Modules/Sessions/Ports.cs')
write('changed-files.json', [record(root / x) for x in ['unity/Assets/Ashfall/Modules/Sessions/Session.cs','unity/Tools/Modules/Sessions/R2Tests.cs','unity/Tools/Modules/Sessions/run-r2.py','unity/Tools/Modules/Sessions/freeze-r2.py']])

files = list((root / 'unity/Assets/Ashfall/Modules/Sessions').glob('*.cs'))
files += list((root / 'unity/Tools/Modules/Sessions').glob('*'))
files += list((stage / 'r2').glob('*'))
files += [p for p in snap.rglob('*') if p.is_file()]
files += [p for p in ev.rglob('*') if p.is_file()]
manifest = dict(status='candidate; fresh independent review required; unpromoted', api_version='Sessions/2', validation=dict(original_checks=112,reviewer_checks=5,new_r2_checks=321, csharp='5',warnaserror=True), files=[record(p) for p in sorted(files) if p.is_file()])
manifest_path.write_text(json.dumps(manifest, indent=2), encoding='utf-8')
shutil.copyfile(manifest_path, stage / 'r2/candidate-manifest.json')
# Verify once without executing or modifying any frozen outputs.
assert all(digest(Path(e['path'])) == e['sha256'] for e in manifest['files'])
print('Frozen manifest SHA256:',digest(manifest_path))
print('Production Session.cs SHA256:',digest(root / 'unity/Assets/Ashfall/Modules/Sessions/Session.cs'))
print('Manifest entries:',len(manifest['files']))
print('Dependency checkpoint drift:',[str(Path(x['source_path']).relative_to(root)) for x in dependencies if not x['matches_checkpoint']])
