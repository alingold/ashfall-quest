import hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/ConstructionPublicationR3/baseline'
OUT.mkdir(exist_ok=False)
def sha(b):return hashlib.sha256(b).hexdigest().upper()
index=json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/phase12/source-index.json').read_text(encoding='utf-8-sig'))
rows=[]
for r in index:
    b=(ROOT/r['snapshot']).read_bytes()
    assert sha(b)==r['sha256'].upper(),r
    p=OUT/r['original'];p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
    rows.append(dict(r,currentSha256=sha((ROOT/r['original']).read_bytes()),consumed=str(p.relative_to(ROOT))))
receipts=[]
for version in range(1,7):
    p=ROOT/f'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v{version}.json'
    b=p.read_bytes();data=json.loads(b.decode('utf-8-sig'))
    def walk(x):
        if isinstance(x,dict):
            if 'path' in x and 'expected' in x:
                f=ROOT/x['path']
                if f.is_file():receipts.append(dict(path=x['path'],expected=x['expected'],actual=sha(f.read_bytes())))
            for v in x.values():walk(v)
        elif isinstance(x,list):
            for v in x:walk(v)
    walk(data)
    (OUT/p.name).write_bytes(b)
# Baseline exact owned bytes, including partial/user changes.
for folder in ['unity/Assets/Ashfall/Integration/ConstructionRuntime','unity/Tools/Integration/ConstructionRuntime']:
    for p in (ROOT/folder).rglob('*'):
        if p.is_file():
            q=OUT/'owned'/p.relative_to(ROOT);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes())
(OUT/'index.json').write_text(json.dumps(rows,indent=2))
(OUT/'accepted-checks.json').write_text(json.dumps(receipts,indent=2))
print('frozen',len(rows),'sources; current drift',sum(r['sha256']!=r['currentSha256'] for r in rows))
print('accepted recursive receipts',len(receipts),'mismatches',sum(r['expected'].upper()!=r['actual'] for r in receipts))
