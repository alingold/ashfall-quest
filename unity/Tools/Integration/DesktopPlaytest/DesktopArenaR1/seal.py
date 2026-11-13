"""Seal this owner's delivery, without mutating any shared source."""
import hashlib
import json
import pathlib

ROOT = pathlib.Path(r'C:\Users\aling\Documents\ashfall')
DOC = ROOT / 'docs/agent-runs/20261104-three-chats/unity/DesktopArenaR1'
OUT = ROOT / 'campaign-staging/20261104-three-chats/unity/DesktopArenaR1'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


owned = [ROOT / 'unity/Assets/Ashfall/Integration/DesktopPlaytest/Arena',
         ROOT / 'unity/Tools/Integration/DesktopPlaytest/DesktopArenaR1', DOC, OUT]
manifest = DOC / 'output-manifest.json'
assert not manifest.exists(), 'Keep previous seals immutable; create a new named seal for a later candidate.'
files = sorted(p for folder in owned for p in folder.rglob('*') if p.is_file())
records = [{'path': str(p.relative_to(ROOT)), 'bytes': p.stat().st_size, 'sha256': sha(p)} for p in files]
compile_result = json.loads((DOC / 'compile01-results.json').read_text(encoding='utf-8'))
comparison = []
for row in compile_result['inputs']:
    if 'snapshot' in row:
        source = pathlib.Path(row['path'])
        comparison.append({'path': str(source), 'frozenSha256': row['sha256'], 'currentSha256': sha(source),
                           'match': sha(source) == row['sha256']})
result = {'status': 'candidate; API compile PASS; native execution and independent review OPEN',
          'priorState': 'Arena/ArenaR1 directories absent in initial desktop directory inventory; additive new files only',
          'files': records, 'currentComparedToCompile01': comparison,
          'rootGate': 'Ashfall.Tools.Integration.DesktopPlaytest.DesktopArenaNativeFixture.Run(System.Action<string>)',
          'nativeExecuted': False, 'M1': 'OPEN'}
manifest.write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps({'manifest': str(manifest), 'sha256': sha(manifest), 'fileCount': len(records),
                  'currentMatchesFrozen': all(row['match'] for row in comparison)}))
