import pathlib,json,hashlib
R=pathlib.Path.cwd();D=R/'docs/agent-runs/20261104-three-chats/unity/ProjectileColdOwnerR5';S=R/'campaign-staging/20261104-three-chats/unity/ProjectileColdOwnerR5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
rows=json.loads((D/'before.json').read_text());drift=[];changed=[]
for row in rows:
 p=R/row['path'];now=sha(p)
 if now!=row['sha256']:
  entry={'path':row['path'],'before':row['sha256'],'now':now}
  (changed if '/Projectiles/' in row['path'] or '/ProjectileWeapons/' in row['path'] and '/Assets/' in row['path'] else drift).append(entry)
(D/'dependency-drift.json').write_text(json.dumps(drift,indent=2));(D/'changed-existing.json').write_text(json.dumps(changed,indent=2))
# Explicit symbol index over final owned source; no external gameplay sources changed.
symbols=[]
for folder in ['unity/Assets/Ashfall/Modules/Projectiles','unity/Assets/Ashfall/Integration/ProjectileWeapons']:
 for p in (R/folder).glob('*.cs'):
  symbols.append({'path':str(p.relative_to(R)),'sha256':sha(p),'symbols':[{'line':i,'source':line.strip()} for i,line in enumerate(p.read_text().splitlines(),1) if any(x in line for x in ['CaptureCold','RestoreCold','ValidateCold','ColdQuarantined','coldQuarantine','b.Targets.Clone','pending.Add','pending.Remove'])]})
(D/'symbols.json').write_text(json.dumps(symbols,indent=2));print('external drift',len(drift),'changed existing',len(changed))
