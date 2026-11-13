"""Root-owned guarded publication. Default is read-only; --publish commits it."""
from source import *
import argparse,os
C=HERE/'Candidate';M=json.loads((C/'CANDIDATE.json').read_text());parser=argparse.ArgumentParser();parser.add_argument('--publish',action='store_true');args=parser.parse_args();assets=(ROOT/'unity/Assets').resolve()
for relative,digest in M['sourceInputs'].items():assert sha(source(relative))==digest,relative
for row in M['files']:
 p=C/row['path'];target=(ROOT/row['path']).resolve();assert target.is_relative_to(assets) and p.is_file() and sha(p)==row['sha256'],'Candidate/source scope/hash changed: '+row['path']
for relative,digest in M['baselines'].items():assert sha(ROOT/relative)==digest,'Live changed; rebase before publish: '+relative
for row in M['files']:
 target=ROOT/row['path'];assert row['path'] in M['baselines'] or not target.exists(),'New filename already exists: '+row['path']
print('All source, candidate and10live-baseline guards PASS;',len(M['files']),'files;',sum(x['bytes']for x in M['files']),'bytes')
if not args.publish:print('Read-only check. Root may run with --publish while engine lanes are idle.')
else:
 backup=HERE/'BeforePublish';backup.mkdir(exist_ok=True)
 for relative in M['baselines']:
  p=backup/relative;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes((ROOT/relative).read_bytes())
 for row in M['files']:
  target=ROOT/row['path'];target.parent.mkdir(parents=True,exist_ok=True);temporary=target.with_name(target.name+'.dual-publish-tmp');assert not temporary.exists(),'Unexpected previous writer temp: '+str(temporary);temporary.write_bytes((C/row['path']).read_bytes());os.replace(temporary,target)
 assert all(sha(ROOT/x['path'])==x['sha256']for x in M['files'])
 (HERE/'published.json').write_text(json.dumps(dict(files=M['files'],profiles=26,engineRun=False),indent=2)+'\n');print('Published coherent26-profile source dual candidate; native/Quest untested.')
