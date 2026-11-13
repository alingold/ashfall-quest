"""Future WORLD CPU phase: copy exactly the5586 pinned files to fresh private reference."""
from pathlib import Path
import argparse,datetime,hashlib,json,shutil,time
from request import HERE,sha,inside
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--deadline');args=p.parse_args();plan=json.loads(args.plan.read_text());reference=Path(plan['reference']).resolve();source=Path(plan['original']).resolve();baseline=Path(plan['baseline'])
    remaining=(datetime.datetime.fromisoformat(args.deadline)-datetime.datetime.now(datetime.timezone.utc)).total_seconds() if args.deadline else plan['maxCopySeconds']
    copy_limit=min(plan['maxCopySeconds'],remaining)
    if copy_limit<=0:raise TimeoutError('Released slot has ended')
    if not inside(reference,HERE/'runs') or reference==source or reference.exists():raise ValueError('Reference must be new inside this helper owned runs directory')
    if sha(baseline)!=plan['baselineSha256']:raise ValueError('Baseline changed')
    rows=json.loads(baseline.read_text())['actualFiles'];total=sum(row['bytes'] for row in rows)
    if len(rows)!=plan['sourceFiles'] or total!=plan['sourceBytes'] or total>4*1024**3:raise ValueError('Pinned copy bound changed')
    digest=hashlib.sha256('\n'.join(x['path']+'\0'+x['sha256'] for x in rows).encode()).hexdigest()
    if digest!=plan['sourceTreeDigest']:raise ValueError('Pinned file list changed')
    reference.mkdir(parents=True);started=time.monotonic()
    for row in rows:
        original=(source/row['path']).resolve();target=(reference/row['path']).resolve()
        if not inside(original,source) or not inside(target,reference):raise ValueError('Pinned path escaped source/reference')
        if time.monotonic()-started>copy_limit:raise TimeoutError('Bounded reference copy exceeded time; incomplete folder retained')
        if original.stat().st_size!=row['bytes'] or sha(original)!=row['sha256']:raise ValueError('Original source differs from pinned actual R23 '+row['path'])
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(original,target)
        if sha(target)!=row['sha256']:raise IOError('Reference copy mismatch '+row['path'])
    (reference.parent/'reference-complete.json').write_text(json.dumps({'complete':True,'files':len(rows),'bytes':total,'sourceTreeDigest':digest,'elapsedSeconds':time.monotonic()-started},indent=2));print('Copied exact original pinned source; no engine launched:',reference)
if __name__=='__main__':main()
