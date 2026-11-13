"""Future WORLD-only bounded engine phase. Not called during exporter preparation."""
from pathlib import Path
import argparse,datetime,json,os,subprocess,time
from request import HERE,sha,inside
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--godot',type=Path,required=True);p.add_argument('--phase',choices=['import','valley','range'],required=True);p.add_argument('--deadline');args=p.parse_args();plan=json.loads(args.plan.read_text());run=Path(plan['run']).resolve();reference=Path(plan['reference']).resolve();source=Path(plan['original']).resolve()
    origin_run=Path(plan.get('reuseImportedFrom',run)).resolve()
    if not inside(run,HERE/'runs') or not inside(origin_run,HERE/'runs') or reference!=origin_run/'reference' or reference==source or not (origin_run/'reference-complete.json').exists():raise ValueError('Complete owned private reference copy required')
    if origin_run!=run:
        old=json.loads((origin_run/'plan.json').read_text())
        if sha(origin_run/'plan.json')!=plan['reusedPlanSha256'] or old['reference']!=str(reference) or old['sourceTreeDigest']!=plan['sourceTreeDigest']:raise ValueError('Reused imported reference identity changed')
        if args.phase=='import':raise ValueError('Reexport must not reimport the preserved reference')
    if sha(plan['exporter'])!=plan['exporterSha256']:raise ValueError('Exporter changed since plan')
    if sha(plan['baseline'])!=plan['baselineSha256']:raise ValueError('Pinned baseline changed')
    for relative,digest in plan['criticalSourceHashes'].items():
        if sha(reference/relative)!=digest or sha(source/relative)!=digest:raise ValueError('Critical source/reference changed '+relative)
    command=[str(args.godot.resolve()),'--headless','--audio-driver','Dummy','--xr-mode','off','--path',str(reference),'--log-file',str(run/(args.phase+'-godot.log'))]
    if args.phase=='import':command+=['--editor','--import','--quit'];timeout=plan['importTimeoutSeconds']
    else:
        if not (origin_run/'import-result.json').exists() or json.loads((origin_run/'import-result.json').read_text())['exitCode']!=0:raise ValueError('Successful private reference import required')
        request_path=run/(args.phase+'-request.json');request=json.loads(request_path.read_text())
        if request['map']!=args.phase or Path(request['referenceRoot']).resolve()!=reference or not inside(request['output'],run):raise ValueError('Request paths changed')
        command+=['--script',plan['exporter'],'--','--request='+str(request_path)];timeout=plan['exportTimeoutSeconds']
    if args.deadline:timeout=min(timeout,(datetime.datetime.fromisoformat(args.deadline)-datetime.datetime.now(datetime.timezone.utc)).total_seconds()-2)
    if timeout<=0:raise TimeoutError('Released slot has ended')
    flags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0;started=time.monotonic();log=run/(args.phase+'-engine.log')
    with log.open('w',encoding='utf-8') as output:
        process=subprocess.Popen(command,stdout=output,stderr=subprocess.STDOUT,creationflags=flags)
        try:code=process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            if os.name=='nt':subprocess.run(['taskkill.exe','/PID',str(process.pid),'/T','/F'],capture_output=True)
            else:process.kill()
            process.wait();code=-1
    result={'phase':args.phase,'command':command,'exitCode':code,'elapsedSeconds':time.monotonic()-started,'log':str(log),'boundedSeconds':timeout}
    (run/(args.phase+'-result.json')).write_text(json.dumps(result,indent=2))
    for relative,digest in plan['criticalSourceHashes'].items():
        if sha(source/relative)!=digest:raise ValueError('Original source critical hash changed '+relative)
    if code!=0:raise SystemExit('Engine phase failed/timed out; inspect '+str(log))
    if args.phase!='import':
        from validate_export import validate
        print(validate(run/args.phase))
    else:print('Private import completed; original source not executed:',reference)
if __name__=='__main__':main()
