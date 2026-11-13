import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
STAGE=ROOT/'campaign-staging/20261104-three-chats/unity/ConstructionPublicationR3'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/ConstructionPublicationR3'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
accepted=json.loads((STAGE/'baseline/accepted-checks.json').read_text())
for r in accepted:r['after']=sha(ROOT/r['path']);r['unchanged']=r['after']==r['actual'];r['matchesExpected']=r['after']==r['expected'].upper()
rows=json.loads((STAGE/'baseline/index.json').read_text())
drift=[dict(original=r['original'],pinned=r['sha256'],atStart=r['currentSha256'],now=sha(ROOT/r['original'])) for r in rows if r['sha256']!=sha(ROOT/r['original'])]
owned=[]
for folder in ['unity/Assets/Ashfall/Integration/ConstructionRuntime','unity/Assets/Ashfall/Integration/ConstructionTransactions','unity/Tools/Integration/ConstructionTransactions']:
    for p in (ROOT/folder).rglob('*'):
        if not p.is_file() or '__pycache__' in str(p):continue
        old=STAGE/'baseline/owned'/p.relative_to(ROOT)
        if old.exists() and sha(old)==sha(p):continue
        owned.append(dict(path=str(p.relative_to(ROOT)),beforeSha256=sha(old) if old.exists() else None,sha256=sha(p),bytes=p.stat().st_size))
(DOC/'changed-files-sha256.json').write_text(json.dumps(owned,indent=2))
(DOC/'accepted-after.json').write_text(json.dumps(accepted,indent=2))
(DOC/'source-drift.json').write_text(json.dumps(drift,indent=2))
result=dict(frozenSources=len(rows),acceptedComparisons=len(accepted),acceptedMismatch=sum(not r['matchesExpected'] for r in accepted),acceptedChanged=sum(not r['unchanged'] for r in accepted),changedFiles=len(owned),manifestSha256=sha(DOC/'changed-files-sha256.json'))
(DOC/'audit-summary.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
assert result['acceptedMismatch']==0 and result['acceptedChanged']==0
