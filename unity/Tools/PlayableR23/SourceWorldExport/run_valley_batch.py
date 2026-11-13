"""One coordinated private copy/import/Valley export, bounded by the released UTC deadline."""
from pathlib import Path
import argparse,datetime,json,subprocess,sys
here=Path(__file__).resolve().parent
p=argparse.ArgumentParser();p.add_argument('--plan',type=Path,required=True);p.add_argument('--godot',type=Path,required=True);p.add_argument('--deadline',required=True);args=p.parse_args()
cutoff=datetime.datetime.fromisoformat(args.deadline)
plan=json.loads(args.plan.read_text());run=Path(plan['run']);phases=[]
commands=[]
if plan.get('reuseImportedFrom'):
    from request import sha,inside
    reference=Path(plan['reference']).resolve()
    baseline=json.loads(Path(plan['baseline']).read_text())
    if not inside(reference,here/'runs') or sha(plan['baseline'])!=plan['baselineSha256']:raise ValueError('Reused reference baseline identity')
    for row in baseline['actualFiles']:
        if (cutoff-datetime.datetime.now(datetime.timezone.utc)).total_seconds()<=2:raise SystemExit('Deadline reached verifying reused reference')
        target=(reference/row['path']).resolve()
        if not inside(target,reference) or target.stat().st_size!=row['bytes'] or sha(target)!=row['sha256']:raise ValueError('Reused reference changed: '+row['path'])
    phases.append({'phase':'verify-reused-imported-reference','files':len(baseline['actualFiles']),'engineRun':False})
else:
    commands.append(('copy',[sys.executable,str(here/'stage_reference.py'),'--plan',str(args.plan),'--deadline',args.deadline]))
    commands.append(('import',[sys.executable,str(here/'run_phase.py'),'--plan',str(args.plan),'--godot',str(args.godot),'--phase','import','--deadline',args.deadline]))
commands.append(('valley',[sys.executable,str(here/'run_phase.py'),'--plan',str(args.plan),'--godot',str(args.godot),'--phase','valley','--deadline',args.deadline]))
for phase,command in commands:
    remaining=(cutoff-datetime.datetime.now(datetime.timezone.utc)).total_seconds()
    if remaining<=2:raise SystemExit('Released deadline reached before '+phase)
    print('VALLEY_PHASE '+phase+' remainingSeconds='+str(round(remaining)),flush=True)
    result=subprocess.run(command)
    phases.append({'phase':phase,'exit':result.returncode,'finishedUTC':datetime.datetime.now(datetime.timezone.utc).isoformat()})
    (run/'batch-result.json').write_text(json.dumps({'complete':False,'phases':phases,'deadline':args.deadline},indent=2))
    if result.returncode:raise SystemExit(result.returncode)
from validate_export import validate
evidence=validate(run/'valley')
(run/'batch-result.json').write_text(json.dumps({'complete':True,'phases':phases,'deadline':args.deadline,'validated':evidence},indent=2))
print('VALLEY_BATCH_COMPLETE '+str(run/'valley'),flush=True)
