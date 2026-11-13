"""Read policy/index inputs and seal this leaf's artifacts. No engine or shared writes."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
STAGE = ROOT / 'campaign-staging/20261104-three-chats/unity/DesktopHudR1'
DOCS = ROOT / 'docs/agent-runs/20261104-three-chats/unity/DesktopHudR1'

def record(p):
    data = p.read_bytes()
    return dict(path=str(p.relative_to(ROOT)), bytes=len(data), sha256=hashlib.sha256(data).hexdigest())

def run():
    base = ROOT / 'docs/agent-runs/20261104-three-chats/unity'
    paths = [ROOT / ('docs/' + n) for n in ['TWO-CHAT-20-AGENT-OVERRIDE.md', 'HILL-CLIMB-ORCHESTRATION.md',
             'OVERNIGHT-AGENT-PLAN-2026-11-03.md', 'AGENT-DISPATCH-BRIEFS-2026-11-03.md', 'agent-runs/20261104-0020/preflight.json']]
    paths += list((ROOT / 'unity/Documentation').glob('*.md'))
    paths += [base / ('accepted-neutral-v%d.json' % i) for i in range(1,7)]
    paths += [base / n for n in ['GAMEPLAY-CHECKPOINT-v1.md', 'phase16/source-index.json',
              'phase16/dispatch-plan.json', 'phase18/source-index.json', 'phase18/root-manifest.json',
              'phase18/ROOT-REPORT.md', 'phase18/dispatch-plan.json', 'phase18/receipt-DesktopHudR1.json']]
    freeze = STAGE / 'policy-inputs'
    freeze.mkdir(exist_ok=True)
    rows, summaries = [], []
    for i,p in enumerate(paths):
        data = p.read_bytes()
        text = data.decode('utf-8-sig')
        row = record(p)
        dst = freeze / ('%02d-' % i + p.name)
        if dst.exists() and dst.read_bytes() != data:
            raise RuntimeError('Frozen input changed: ' + str(p))
        dst.write_bytes(data)
        row['snapshot'] = str(dst.relative_to(ROOT))
        rows.append(row)
        if p.suffix == '.json':
            obj = json.loads(text)
            summaries.append(dict(path=row['path'], kind=type(obj).__name__, entries=len(obj),
                keys=list(obj) if isinstance(obj,dict) else [],
                scope=[x.get('scope',x.get('name','')) for x in obj.get('accepted',[])] if isinstance(obj,dict) else []))
    (DOCS / 'input-manifest.json').write_text(json.dumps(rows,indent=2))
    (DOCS / 'read-summary.json').write_text(json.dumps(summaries,indent=2))
    # Frozen phase indices are historical authority, current context is a separate new dependency.
    for version in ['phase16','phase18']:
        index = json.loads((base / version / 'source-index.json').read_text(encoding='utf-8-sig'))
        print(version, 'sources', len(index))
    print(json.dumps(summaries,indent=2))
    latest = sorted(STAGE.glob('compile[0-9][0-9]'))[-1]
    compiled = json.loads((latest / 'inputs.json').read_text())
    comparison = [dict(path=row['path'], compiledSha256=row['sha256'],
                       currentSha256=hashlib.sha256(Path(row['path']).read_bytes()).hexdigest(),
                       match=hashlib.sha256(Path(row['path']).read_bytes()).hexdigest() == row['sha256'])
                  for row in compiled]
    (DOCS / 'compatibility.json').write_text(json.dumps(dict(compiled=str(latest),
         historicalNeutralIndex='phase18:302 sources; desktop candidate is additional', current=comparison),indent=2))
    owned = [ROOT / 'unity/Assets/Ashfall/Integration/DesktopPlaytest/Hud',
             ROOT / 'unity/Tools/Integration/DesktopPlaytest/DesktopHudR1', STAGE, DOCS]
    outputs = [record(p) for folder in owned for p in sorted(folder.rglob('*'))
               if p.is_file() and p.name != 'output-manifest.json']
    (DOCS / 'output-manifest.json').write_text(json.dumps(outputs,indent=2))

if __name__ == '__main__':
    run()
