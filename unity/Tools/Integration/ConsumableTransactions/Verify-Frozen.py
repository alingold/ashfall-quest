from pathlib import Path
import hashlib,json
R=Path(r'C:\Users\aling\Documents\ashfall');E=R/'docs/agent-runs/20261104-three-chats/unity/ConsumableTransactionsR1';O=R/'campaign-staging/20261104-three-chats/unity/ConsumableTransactionsR1'
h=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
read=[]
for rel in ['docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json','docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v2.json','docs/agent-runs/20261104-three-chats/unity/phase05-ownership.json','docs/agent-runs/20261104-three-chats/world/accepted-handoff-v2.json']:
 p=R/rel; obj=json.loads(p.read_text(encoding='utf-8-sig'));read.append({'path':rel,'sha256':h(p),'keys':list(obj)})
verification=[]
for name in ['CombatHealth-v3','Player-r4']:
 p=R/('docs/agent-runs/20261104-three-chats/unity/phase05/candidates/'+name+'/candidate-manifest.json');obj=json.loads(p.read_text(encoding='utf-8-sig'))
 for f in obj['files']:
  src=R/f['path'];verification.append({'candidate':name,'path':f['path'],'expected':f['sha256'],'actual':h(src),'match':h(src).lower()==f['sha256'].lower()})
(E/'frozen-root-verification.json').write_text(json.dumps(verification,indent=2));(E/'read-ledgers.json').write_text(json.dumps(read,indent=2))
print('frozen root entries',len(verification),'mismatches',sum(not x['match'] for x in verification))
print([x for x in verification if not x["match"]])
