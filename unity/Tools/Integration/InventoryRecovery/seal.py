from pathlib import Path
import hashlib,json
r=Path.cwd();d=r/'docs/agent-runs/20261104-three-chats/unity/InventoryRecoveryR1';o=r/'campaign-staging/20261104-three-chats/unity/InventoryRecoveryR1';h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
inputs=json.loads((d/'final03/inputs.json').read_text());assert all(h(Path(v['snapshot']))==v['sha256'] for v in inputs)
for v in inputs:
 p=Path(v['source'])
 if 'before' not in p.parts:assert h(p)==v['sha256'],str(p)
initial=json.loads((d/'before.json').read_text());drift=[dict(path=v['path'],before=v['sha256'],current=h(r/v['path']),owned=v['path'].endswith('FiniteAmmoReserve.cs')) for v in initial if h(r/v['path'])!=v['sha256']];(d/'final-dependency-drift.json').write_text(json.dumps(drift,indent=2));print('Final external drift:',[v['path'] for v in drift if not v['owned']])
owned=[r/'unity/Assets/Ashfall/Modules/InventoryItems/CheckpointState.cs',r/'unity/Assets/Ashfall/Modules/WeaponHandling/CheckpointState.cs',r/'unity/Assets/Ashfall/Modules/WeaponHandling/FiniteAmmoReserve.cs']+list((r/'unity/Assets/Ashfall/Integration/InventoryRecovery').glob('*.cs'))
(d/'changed-files.json').write_text(json.dumps([{'path':str(p.relative_to(r)),'sha256':h(p)} for p in owned],indent=2))
files=owned+[p for base in [r/'unity/Tools/Integration/InventoryRecovery',d,o] for p in base.rglob('*') if p.is_file() and p.name not in ['SHA256-manifest.json','SHA256-manifest.digest.txt']]
records=[{'path':str(p.relative_to(r)),'bytes':p.stat().st_size,'sha256':h(p)} for p in sorted(set(files))];manifest=d/'SHA256-manifest.json';assert not manifest.exists();manifest.write_text(json.dumps(records,indent=2));(d/'SHA256-manifest.digest.txt').write_text(h(manifest)+'\n');print('Sealed files',len(records),'manifest',h(manifest));result=json.loads((d/'final03/results.json').read_text());print('Final results',[(v['exit'],v['stdout'].count('PASS ')) for v in result])
