"""Read/freeze policy and dispatch inputs; verify historical indexed snapshots."""
import hashlib
import json
import pathlib
import shutil

ROOT = pathlib.Path(r'C:\Users\aling\Documents\ashfall')
RUN = ROOT / 'docs/agent-runs/20261104-three-chats/unity'
OUT = ROOT / 'campaign-staging/20261104-three-chats/unity/DesktopArenaR1/policy-inputs01'
OUT.mkdir(parents=True, exist_ok=False)
paths = [ROOT / ('docs/' + name) for name in ['TWO-CHAT-20-AGENT-OVERRIDE.md', 'HILL-CLIMB-ORCHESTRATION.md',
         'OVERNIGHT-AGENT-PLAN-2026-11-03.md', 'AGENT-DISPATCH-BRIEFS-2026-11-03.md', 'agent-runs/20261104-0020/preflight.json']]
paths += [ROOT / ('unity/Documentation/' + name + '.md') for name in ['CONTRACTS-v3', 'COLLISION-LAYERS-v1',
          'NETWORK-LIFECYCLE-v4', 'DAMAGE-POLICY-v4', 'HEALTH-TRANSACTIONS-v4', 'MATCH-TRANSACTIONS-v4',
          'AUDIO-DEPLOYMENT-v2', 'POSE-LIFECYCLE-v1', 'READONLY-RESERVE-v1', 'HOST-EPOCH-GATE-v1',
          'RECOVERY-ASSEMBLY-BOUNDARY-v1', 'DESKTOP-PLAYTEST-4PM-v1']]
paths += [RUN / 'GAMEPLAY-CHECKPOINT-v1.md']
paths += [RUN / ('accepted-neutral-v%d.json' % i) for i in range(1, 7)]
paths += [RUN / part for part in ['phase16/source-index.json', 'phase16/dispatch-plan.json', 'phase18/ROOT-REPORT.md',
          'phase18/source-index.json', 'phase18/root-manifest.json', 'phase18/dispatch-plan.json', 'phase18/receipt-DesktopArenaR1.json']]
paths += [ROOT / 'unity/Tools/Integration/AvatarPose/compile_native.py', pathlib.Path(__file__).resolve()]
receipt = []
for i, path in enumerate(paths):
    data = path.read_bytes()
    text = data.decode('utf-8-sig')
    snapshot = OUT / ('%02d-' % i + path.name)
    snapshot.write_bytes(data)
    entry = {'path': str(path), 'snapshot': str(snapshot), 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data)}
    if path.suffix == '.json':
        parsed = json.loads(text)
        entry['parsedType'] = type(parsed).__name__
        entry['entries'] = len(parsed)
        if path.name.startswith('accepted-neutral'):
            entry['acceptance'] = parsed
            print(path.name + ': ' + ', '.join(str(x.get('name', '?')) for x in parsed.get('accepted', [])))
        if path.name == 'source-index.json':
            checks = []
            for row in parsed:
                source = ROOT / row['snapshot']
                actual = hashlib.sha256(source.read_bytes()).hexdigest()
                checks.append({'snapshot': row['snapshot'], 'matches': actual.lower() == row['sha256'].lower()})
            entry['snapshotChecks'] = checks
            print(str(path.parent.name) + ': %d indexed snapshots, %d hashes match' % (len(checks), sum(c['matches'] for c in checks)))
    receipt.append(entry)
(OUT / 'reads.json').write_text(json.dumps(receipt, indent=2), encoding='utf-8')
shutil.copyfile(OUT / 'reads.json', RUN / 'DesktopArenaR1/input-reads.json')
