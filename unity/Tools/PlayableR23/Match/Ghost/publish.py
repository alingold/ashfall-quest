from pathlib import Path
import hashlib,json,shutil,uuid,sys
HERE=Path(__file__).resolve().parent;STAGE=HERE/'Staged'
recovery='--recovery'in sys.argv
if recovery:STAGE=HERE/'RecoveryStaged'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
plan=json.loads((HERE/('recovery-candidate.json'if recovery else'candidate.json')).read_text());LIVE=Path(plan['root'])
# Validate all source baselines before the first write. This never writes main
# Root, UI, Health, shared weapon sources or historical/native snapshots.
for row in plan['files']:
 src=STAGE/row['path'];dst=LIVE/row['path']
 assert sha(src)==row['stagedSha256'],('stage changed; rerun prepare_seams',row['path'])
 assert sha(dst)==row['baselineSha256']if row['baselineSha256']else not dst.exists(),('live baseline changed',row['path'])
changed=[]
for row in plan['files']:
 src=STAGE/row['path'];dst=LIVE/row['path'];dst.parent.mkdir(parents=True,exist_ok=True)
 shutil.copyfile(src,dst);changed.append(row['path'])
 meta=dst.with_name(dst.name+'.meta')
 if not meta.exists():shutil.copyfile(src.with_name(src.name+'.meta'),meta);changed.append(row['path']+'.meta')
 for folder in list(dst.parents):
  if folder==LIVE:break
  meta=folder.with_name(folder.name+'.meta')
  if not meta.exists():
   rel=folder.relative_to(LIVE).as_posix();guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall/R6/ghost/folder/'+rel).hex
   meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n');changed.append(rel+'.meta')
(HERE/('recovery-published.json'if recovery else'published.json')).write_text(json.dumps(dict(changedFiles=changed,sourceFiles=plan['files']),indent=2)+'\n')
print('Published source ghost candidate:',len(plan['files']),'source files;',len(changed),'total changed/new paths.')
