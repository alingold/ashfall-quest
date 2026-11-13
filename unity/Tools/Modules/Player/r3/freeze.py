from pathlib import Path
import json,hashlib,shutil
r=Path('C:/Users/aling/Documents/ashfall');d=r/'docs/agent-runs/20261104-three-chats/unity/Player/r3';o=r/'campaign-staging/20261104-three-chats/unity/Player/r3';p=r/'unity/Assets/Ashfall/Modules/Player'
def sha(b):return hashlib.sha256(b).hexdigest().upper()
for f in p.rglob('*.cs'):
 dest=o/'candidate-source'/f.relative_to(p);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(f.read_bytes())
rows=[]
for row in json.loads((d/'consumed-inputs.json').read_text()):
 f=Path(row['source']);actual=sha(f.read_bytes());rows.append({'source':str(f),'expected':row['sha256'],'actual':actual,'matches':actual==row['sha256']})
(d/'input-drift.json').write_text(json.dumps(rows,indent=2))
roots=[p,r/'unity/Tools/Modules/Player/r3',o,d]
files=sorted(set(f for root in roots for f in root.rglob('*') if f.is_file() and f.name not in ['changed-file-manifest.json','manifest-receipt.json']))
manifest={'candidate':'PlayerR3','status':'implementation-candidate-needs-independent-review','questRevision':'bdba6e30012e1733efd5021fd8ded593f82f26e6','excludes':['changed-file-manifest.json (self)','manifest-receipt.json (pins manifest)'],'files':[{'path':str(f.relative_to(r)).replace('\\','/'),'bytes':f.stat().st_size,'sha256':sha(f.read_bytes())} for f in files]}
f=d/'changed-file-manifest.json';f.write_text(json.dumps(manifest,indent=2));receipt={'manifest':str(f),'sha256':sha(f.read_bytes()),'files':len(files),'allConsumedInputsMatch':all(x['matches'] for x in rows)};(d/'manifest-receipt.json').write_text(json.dumps(receipt,indent=2))
print(json.dumps(receipt,indent=2))
print('stability',all(sha((r/row['path']).read_bytes())==row['sha256'] for row in manifest['files']))
