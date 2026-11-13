"""Root-run guarded intake. Default verifies only; --apply atomically replaces each listed file, manifest last."""
from pathlib import Path
import json,hashlib,sys,os
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];plan=json.loads((HERE/'publication-plan.json').read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(Path(plan['sourcePayload']))==plan['sourcePayloadSha256']
for key in ['geometry','tower','architecture','sourceArt']:assert sha(ROOT/plan[key])==plan[key+'Sha256'],key+' source binding changed'
for row in plan['files']:
 target=(ROOT/row['target']).resolve();assert target.is_relative_to((ROOT/'unity/Assets').resolve());assert sha(ROOT/row['stage'])==row['after'],row['stage']+' staged content changed'
 if row['before'] is None:assert not target.exists(),'New target already exists: '+str(target)
 else:assert target.is_file() and sha(target)==row['before'],'Refuse stale source overwrite: '+str(target)
print('PASS guarded source/data bindings and all',len(plan['files']),'publication before/after hashes')
if '--apply' in sys.argv:
 for row in plan['files']:
  target=ROOT/row['target'];target.parent.mkdir(parents=True,exist_ok=True);temporary=target.with_name(target.name+'.source-cuts-publishing');assert not temporary.exists();temporary.write_bytes((ROOT/row['stage']).read_bytes());os.replace(temporary,target)
 for row in plan['files']:assert sha(ROOT/row['target'])==row['after']
 assert sha(ROOT/plan['geometry'])==plan['geometrySha256'];assert sha(ROOT/plan['tower'])==plan['towerSha256']
 (HERE/'publication.json').write_text(json.dumps({'published':True,'files':len(plan['files']),'geometryUnchanged':True,'existing337Unchanged':True},indent=2)+'\n');print('PUBLISHED source cuts; native cooking/render/aperture qualification still pending')
