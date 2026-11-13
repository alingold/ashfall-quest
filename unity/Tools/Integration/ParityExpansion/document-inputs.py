from pathlib import Path
import hashlib,json,re
r=Path.cwd();ev=r/'docs/agent-runs/20261104-three-chats/unity/ParityExpansion/r3';stage=r/'campaign-staging/20261104-three-chats/unity/ParityExpansion/r3'
refs={'player.gd':['launch_from_pad_r10'],'precision_rifle.gd':['insert_magazine','bolt_sample','_finish_cycle'],'weapon.gd':['grab_magazine','insert_magazine','rack_bolt'],'team_launch_r12.gd':['eligible','plan']}
records=[]
for tree in ['quest-r22/source','game']:
 for file,names in refs.items():
  p=r/tree/'scripts'/file;d=p.read_bytes();lines=d.decode('utf-8-sig').splitlines();parts=[]
  for name in names:
   for i,line in enumerate(lines):
    if re.match(r'^func '+re.escape(name)+r'\(',line):
     end=next((j for j in range(i+1,len(lines)) if lines[j].startswith('func ')),len(lines));parts.append({'symbol':name,'startLine':i+1,'endLine':end,'source':'\n'.join(lines[i:end])})
  out=stage/'source-excerpts'/tree/file;out.parent.mkdir(parents=True,exist_ok=True);out.write_text('\n\n'.join(x['source'] for x in parts))
  records.append({'path':p.relative_to(r).as_posix(),'sha256':hashlib.sha256(d).hexdigest(),'revision':'bdba6e30012e1733efd5021fd8ded593f82f26e6' if tree.startswith('quest') else 'differing skin runtime read-only; hash identity only','symbols':[{k:v for k,v in x.items() if k!='source'} for x in parts]})
(ev/'source-references.json').write_text(json.dumps(records,indent=2))
accepted=json.loads((r/'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json').read_text(encoding='utf-8-sig'))
checks=[]
for x in accepted['sourceChecks']:
 if x['name']=='ParityHarness':
  p=Path(x['path']);h=hashlib.sha256(p.read_bytes()).hexdigest().upper();checks.append({'path':str(p),'expected':x['expected'],'actual':h,'match':h==x['expected']})
(ev/'accepted-preservation.json').write_text(json.dumps(checks,indent=2));assert all(x['match'] for x in checks)
print('Source reference excerpts and accepted harness hash preservation recorded')
