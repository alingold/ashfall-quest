from pathlib import Path
import hashlib,json,re,sys
R=Path(r'C:\Users\aling\Documents\ashfall');D=R/'docs/agent-runs/20261104-three-chats/unity/ClimbRuntimeBindingsR8';S=R/'campaign-staging/20261104-three-chats/unity/ClimbRuntimeBindingsR8'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def save(n,x):
 p=D/n
 if p.exists():raise RuntimeError('already sealed '+str(p))
 p.write_text(json.dumps(x,indent=2))
inputs=json.loads((D/'inputs.json').read_text());D=D/sys.argv[1];D.mkdir();drift=[]
for row in inputs:
 for key in ['original','snapshot']:
  p=R/row[key]
  if sha(p)!=row['sha256'].upper():drift.append(dict(kind=key,path=str(p),expected=row['sha256'],actual=sha(p)))
save('dependency-drift.json',drift)
accepted=[]
def walk(x,ledger):
 if isinstance(x,list):
  for y in x:walk(y,ledger)
 elif isinstance(x,dict):
  if 'path' in x and ('expected' in x or 'sha256' in x):
   p=Path(x['path']);p=p if p.is_absolute() else R/p;expected=x.get('expected',x.get('sha256'))
   if isinstance(expected,str) and len(expected)==64:accepted.append(dict(ledger=ledger,path=str(p),expected=expected,actual=sha(p) if p.exists() else None,match=p.exists() and sha(p)==expected.upper()))
  for y in x.values():walk(y,ledger)
for p in (S/'inputs/docs/agent-runs/20261104-three-chats/unity').glob('accepted-neutral-v*.json'):walk(json.loads(p.read_text(encoding='utf-8-sig')),p.name)
save('accepted-verification.json',accepted)
phase=json.loads((S/'inputs/docs/agent-runs/20261104-three-chats/unity/phase15/source-index.json').read_text(encoding='utf-8-sig'));pins={x['original'].replace('\\','/'):x for x in inputs};comparison=[]
for row in phase:
 key=row['original'].replace('\\','/')
 if key in pins:comparison.append(dict(path=key,phase15=row['sha256'],consumed=pins[key]['sha256'],match=row['sha256'].upper()==pins[key]['sha256'].upper()))
save('phase15-comparison.json',comparison)
symbols=[]
for name in ['game','quest-r22/source']:
 p=R/name/'scripts/player.gd';txt=p.read_text(encoding='utf-8-sig');functions={}
 for symbol in ['_make_goggle_head_shape','_sync_head_shape','_resolve_support','_step_mantle','_recenter','_calibrate_height']:
  m=re.search(r'^func '+symbol+r'\(.*?(?=^func |\Z)',txt,re.M|re.S)
  functions[symbol]=dict(line=txt[:m.start()].count('\n')+1,text=m.group()) if m else None
 symbols.append(dict(path=str(p),sha256=sha(p),functions=functions))
save('source-symbols.json',symbols)
refs=[]
for arg in (S/sys.argv[2]/'NativeAPI.rsp').read_text().splitlines():
 if arg.startswith('/reference:'):
  p=Path(arg.split(':',1)[1].strip('"'));refs.append(dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size))
save('reference-inventory.json',refs)
files=[]
for folder in [R/'unity/Assets/Ashfall/Integration/ClimbPlayerBindings',R/'unity/Tools/Integration/ClimbPlayerBindings']:
 for p in folder.rglob('*'):
  if p.is_file():files.append(dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size))
save('changed-files-sha256.json',files)
print('dependency drift',len(drift),'accepted',len(accepted),'mismatches',sum(not x['match'] for x in accepted),'phase15',len(comparison),'mismatches',sum(not x['match'] for x in comparison),'references',len(refs))
