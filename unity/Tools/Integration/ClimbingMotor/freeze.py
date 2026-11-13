from pathlib import Path
import hashlib,json,shutil,re
R=Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/ClimbingBackendR5'
D=R/'docs/agent-runs/20261104-three-chats/unity/ClimbingBackendR5'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
files=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
for part in ['Modules/Climbing','Modules/Player','Modules/Flight','Integration/FlightMotor']:
 files+=list((R/'unity/Assets/Ashfall'/part).rglob('*.cs'))
files+=list((R/'unity/Tools/Modules/Climbing').rglob('*.cs'))
files += [R/'quest-r22/source/scripts/player.gd',R/'game/scripts/player.gd']
rows=[]
for p in files:
 rel=p.relative_to(R);q=S/'before'/rel;q.parent.mkdir(parents=True,exist_ok=True)
 if q.exists(): raise RuntimeError('Snapshot exists: '+str(q))
 shutil.copyfile(p,q);rows.append(dict(path=str(rel),sha256=sha(p),snapshot=str(q.relative_to(R))))
(D/'before.json').write_text(json.dumps(rows,indent=2))
for n in ['accepted-neutral-v1.json','accepted-neutral-v2.json','accepted-neutral-v3.json']:
 p=R/'docs/agent-runs/20261104-three-chats/unity'/n
 data=json.loads(p.read_text(encoding='utf-8-sig'))
 print(n,'read',p.stat().st_size,'bytes',sha(p),'top-level',list(data))
funcs=['_make_goggle_head_shape','_sync_head_shape','_resolve_support','_step_mantle']
excerpts={}
for tree in ['quest-r22/source','game']:
 text=(R/tree/'scripts/player.gd').read_text(encoding='utf-8-sig')
 excerpts[tree]={}
 for name in funcs:
  m=re.search(r'^func '+name+r'\(.*?(?=^func |\Z)',text,re.M|re.S)
  excerpts[tree][name]=m.group() if m else None
(D/'source-excerpts.json').write_text(json.dumps(excerpts,indent=2))
print('Snapshots',len(rows),'function equality',{f:excerpts['game'][f]==excerpts['quest-r22/source'][f] for f in funcs})
