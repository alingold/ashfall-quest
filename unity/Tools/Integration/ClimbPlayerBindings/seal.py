from pathlib import Path
import hashlib,json,re
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/ClimbRuntimeBindingsR8';S=R/'campaign-staging/20261104-three-chats/unity/ClimbRuntimeBindingsR8'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
symbols=[]
for tree in ['game','quest-r22/source']:
 p=R/tree/'scripts/player.gd';text=p.read_text(encoding='utf-8-sig');functions={}
 for name in ['measured_eye_height','get_head_top_height','initialize_vertical_combat_system','_apply_vertical_combat_ceiling','_sync_body_to_head','_sync_head_shape']:
  m=re.search(r'^func '+name+r'\(.*?(?=^func |\Z)',text,re.M|re.S)
  functions[name]=dict(line=text[:m.start()].count('\n')+1,text=m.group()) if m else None
 symbols.append(dict(path=str(p),sha256=sha(p),functions=functions))
p=D/'source-height-symbols.json';assert not p.exists();p.write_text(json.dumps(symbols,indent=2))
rows=[]
for root in [R/'unity/Assets/Ashfall/Integration/ClimbPlayerBindings',R/'unity/Tools/Integration/ClimbPlayerBindings',D,S]:
 for p in root.rglob('*'):
  if p.is_file():rows.append(dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size))
p=D/'output-manifest.json';assert not p.exists();p.write_text(json.dumps(rows,indent=2))
print('sealed',len(rows),'files; manifest SHA256',sha(p))
print('report SHA256',sha(D/'REPORT.md'))
