import hashlib,json,shutil
from pathlib import Path
R=Path.cwd(); B=R/'campaign-staging/20261104-three-chats/unity/DesktopConstructionR1';D=R/'docs/agent-runs/20261104-three-chats/unity/DesktopConstructionR1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
U=R/'docs/agent-runs/20261104-three-chats/unity'
paths=[R/'docs'/n for n in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md']]
paths+=[R/'docs/agent-runs/20261104-0020/preflight.json']+list((R/'unity/Documentation').glob('*.md'))
paths+=[U/'GAMEPLAY-CHECKPOINT-v1.md']+[U/('accepted-neutral-v%d.json'%n) for n in range(1,7)]
paths+=[U/'phase16/source-index.json',U/'phase16/dispatch-plan.json',U/'phase18/ROOT-REPORT.md',U/'phase18/source-index.json',U/'phase18/root-manifest.json',U/'phase18/dispatch-plan.json']
for folder in ['Construction','ConstructionPreparedR4','ConstructionRuntimeR2','ConstructionPublicationR3','reviews/ConstructionPreparedReviewR4']:
    paths+=list((U/folder).glob('*.json'))+list((U/folder).glob('*.md'))
paths+=list((R/'unity/Assets/Ashfall/Integration/DesktopPlaytest').rglob('*.cs'))
inventory=[]
for p in paths:
    data=p.read_text(encoding='utf-8-sig'); parsed=json.loads(data) if p.suffix=='.json' else None
    dest=B/'context-inputs-final'/p.relative_to(R);dest.parent.mkdir(parents=True,exist_ok=True)
    if dest.exists():assert dest.read_bytes()==p.read_bytes()
    else:shutil.copy2(p,dest)
    inventory.append(dict(path=str(p.relative_to(R)),sha256=sha(dest),bytes=p.stat().st_size,jsonEntries=len(parsed) if parsed is not None else None))
(D/'read-inventory-final.json').write_text(json.dumps(inventory,indent=2))
checks=[]
for phase in ['phase16','phase18']:
    index=json.loads((U/phase/'source-index.json').read_text())
    rows=[]
    for record in index:
        frozen=R/record['snapshot'];expected=record['sha256'].upper()
        assert sha(frozen)==expected
        rows.append(dict(path=record.get('path',record.get('original')),snapshot=record['snapshot'],sha256=expected))
    checks.append(dict(phase=phase,count=len(rows),allSnapshotsMatch=True))
(D/'pin-verification-final.json').write_text(json.dumps(checks,indent=2))
inputs=json.loads((B/'run06/inputs.json').read_text());index={r['path'].replace('\\','/'):r for r in json.loads((U/'phase18/source-index.json').read_text())}
comparison=[]
for r in inputs:
    k=r['path'].replace('\\','/'); pin=index.get(k)
    comparison.append(dict(path=k,tested=r['sha256'],current=sha(R/r['path']),currentMatches=sha(R/r['path'])==r['sha256'],phase18=pin['sha256'] if pin else None,phase18Matches=r['sha256']==pin['sha256'].upper() if pin else None))
(D/'root-current-comparison-final.json').write_text(json.dumps(comparison,indent=2))
owned=list((R/'unity/Assets/Ashfall/Integration/DesktopPlaytest/Construction').glob('*'))+list((R/'unity/Tools/Integration/DesktopPlaytest/DesktopConstructionR1').glob('*'))
(D/'changed-files-sha256-final.json').write_text(json.dumps([dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size) for p in owned if p.is_file()],indent=2))
(D/'output-sha256-final.json').write_text(json.dumps([dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size) for p in B.rglob('*') if p.is_file()],indent=2))
print(json.dumps(dict(readInputs=len(inventory),pins=checks,outputs=sum(1 for p in B.rglob('*') if p.is_file())),indent=2))
