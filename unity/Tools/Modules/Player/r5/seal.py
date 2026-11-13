from pathlib import Path
import hashlib,json
r=Path(r'C:\Users\aling\Documents\ashfall');o=r/'campaign-staging/20261104-three-chats/unity/PlayerIntegrationR5/r5';d=r/'docs/agent-runs/20261104-three-chats/unity/PlayerIntegrationR5/r5'
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
review=r/'campaign-staging/20261104-three-chats/unity/reviews/Player-r4/inputs'
rows=[]
for p in review.rglob('*.cs'):
 q=o/'before'/p.relative_to(review);rows.append(dict(path=str(p.relative_to(review)),expected=h(p),preserved=h(q),match=h(p)==h(q)))
(d/'preserved-r4-source-verification.json').write_text(json.dumps(rows,indent=2));assert all(x['match'] for x in rows)
print('preserved sources',len(rows),'all byte-identical to reviewed input')
paths=list((r/'unity/Assets/Ashfall/Modules/Player').rglob('*.cs'))+list((r/'unity/Tools/Modules/Player/r5').rglob('*'))+list(o.rglob('*'))+list(d.rglob('*'))
paths=sorted(set(p for p in paths if p.is_file() and p.name not in ['candidate-manifest.json','manifest-receipt.json']))
m=d/'candidate-manifest.json';m.write_text(json.dumps(dict(scope='PlayerIntegrationR5 unreviewed candidate',files=[dict(path=str(p.relative_to(r)),sha256=h(p),bytes=p.stat().st_size) for p in paths]),indent=2))
receipt=dict(manifestSha256=h(m),reportSha256=h(d/'REPORT.md'),files=len(paths),status='unreviewed; engine/real-convex/backend/runtime gates open')
(d/'manifest-receipt.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt))
