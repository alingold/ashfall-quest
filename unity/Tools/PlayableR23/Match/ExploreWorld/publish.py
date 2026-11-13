from pathlib import Path
import hashlib,json,shutil
HERE=Path(__file__).resolve().parent;STAGE=HERE/'Staged';plan=json.loads((HERE/'candidate.json').read_text());LIVE=Path(plan['root']);sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
for row in plan['files']:
 p=LIVE/row['path'];assert sha(STAGE/row['path'])==row['stagedSha256'],row['path'];assert sha(p)==row['baselineSha256']if row['baselineSha256']else not p.exists(),('changed baseline',row['path'])
changed=[]
for row in plan['files']:
 src=STAGE/row['path'];dst=LIVE/row['path'];dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst);changed.append(row['path']);meta=dst.with_name(dst.name+'.meta')
 if not meta.exists():shutil.copyfile(src.with_name(src.name+'.meta'),meta);changed.append(row['path']+'.meta')
(HERE/'published.json').write_text(json.dumps(dict(changedFiles=changed,sourceFiles=plan['files'],nativeExecuted=False),indent=2)+'\n');print('Published shared archive world planner APIs:',len(changed),'changed/new paths. No RootMain/WorldLoot/Consumables/Map/metadata edits.')
