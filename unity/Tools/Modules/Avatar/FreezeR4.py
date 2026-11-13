from pathlib import Path
import hashlib,json,shutil
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/AvatarR4';E=R/'docs/agent-runs/20261104-three-chats/unity/AvatarR4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
m=R/'docs/agent-runs/20261104-three-chats/unity/Avatar/phase03/candidate-manifest.json';old=R/'campaign-staging/20261104-three-chats/unity/Avatar/r3'
assert sha(m)=='0083C3814D3B41CD4CB00EC5C235123FC7B8DF6FE65A0BA5E62E01FE31BF7BD4'
rows=[]
for record in json.loads(m.read_text())['files']:
 p=R/record['path'];sealed=old/'source'/record['path'] if record['path'].startswith('unity/') else p
 rows.append(dict(record,currentSha256=sha(p),currentMatches=sha(p)==record['sha256'],sealedPath=str(sealed),sealedMatches=sealed.exists() and sha(sealed)==record['sha256']))
(E/'r3-sealed-and-current-identities.json').write_text(json.dumps(rows,indent=2));assert all(x['sealedMatches'] for x in rows)
print('r3 sealed identities',len(rows),'all match; current intentionally changed',sum(not x['currentMatches'] for x in rows))
# Baseline includes root fix; validate baseline root bytes against central smallfix snapshots.
small=R/'campaign-staging/20261104-three-chats/unity/phase05/small-fixes/Avatar/validation/inputs/unity/Assets/Ashfall/Modules/Avatar'
rootfix=[]
for name in ['VerifiedSkin.cs','Engine/VerifiedSkinFactory.cs']:
 b=S/'baseline/Assets'/name;p=small/name;rootfix.append(dict(path=name,baseline=sha(b),rootSmallfix=sha(p),matches=sha(b)==sha(p)))
(E/'root-baseline-validation.json').write_text(json.dumps(rootfix,indent=2));assert all(x['matches'] for x in rootfix)
# Freeze new exact implementation and test sources. Existing dirs never overwritten.
frozen=S/'frozen'
if frozen.exists():raise SystemExit('Already frozen; allocate a new packet')
for rel in ['unity/Assets/Ashfall/Modules/Avatar','unity/Tools/Modules/Avatar']:
 shutil.copytree(R/rel,frozen/rel)
# All consumed Core/source hashes before/after, not whole-tree equivalence.
records=json.loads((E/'consumed-before.json').read_text());after=[dict(x,currentSha256=sha(Path(x['path'])),matches=sha(Path(x['path']))==x['sha256']) for x in records]
(E/'dependency-drift.json').write_text(json.dumps(after,indent=2));print('consumed drift',sum(not x['matches'] for x in after))
