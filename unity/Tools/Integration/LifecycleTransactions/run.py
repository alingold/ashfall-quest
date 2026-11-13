import pathlib, hashlib, json, subprocess
ROOT=pathlib.Path(__file__).resolve().parents[4]
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/LifecycleTransactionsR1'
STAGE=ROOT/'campaign-staging/20261104-three-chats/unity/LifecycleTransactionsR1'
OWN=['unity/Assets/Ashfall/Modules/HealthStates/PreparedLifecycle.cs','unity/Assets/Ashfall/Modules/Sessions/PreparedLifeProjection.cs','unity/Assets/Ashfall/Integration/LifecycleTransactions/Coordinator.cs','unity/Assets/Ashfall/Integration/LifecycleTransactions/MatchDirectorProjection.cs','unity/Tools/Integration/LifecycleTransactions/Scenario.cs','unity/Tools/Integration/LifecycleTransactions/EditorFixture.cs']
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
initial=json.loads((DOC/'initial.json').read_text(encoding='utf-8-sig'))
run=1
while (STAGE/('run-%02d'%run)).exists(): run+=1
out=STAGE/('run-%02d'%run);out.mkdir()
results=[]
for mode in ['initial','current']:
    folder=out/mode;folder.mkdir();sources=[];manifest=[]
    consumer=STAGE/'consumer-input/MatchDirectorPorts.cs'
    assert sha(consumer).upper()=='D5B8293786FC46ECF66B53565A8372C4C77457AF00A333C45300C351B85E9B65'
    dest=folder/'MatchDirectorPorts.cs';dest.write_bytes(consumer.read_bytes());sources.append(dest);manifest.append(dict(path='unity/Assets/Ashfall/Integration/MatchDirector/Ports.cs',sha256=sha(dest)))
    for row in initial:
        rel=row['path'];p=STAGE/'initial'/rel if mode=='initial' else ROOT/rel
        if mode=='initial': assert sha(p).upper()==row['sha256']
        dest=folder/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(p.read_bytes());sources.append(dest);manifest.append(dict(path=rel,sha256=sha(dest)))
    for rel in OWN:
        p=ROOT/rel;dest=folder/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(p.read_bytes());sources.append(dest);manifest.append(dict(path=rel,sha256=sha(dest)))
    (folder/'sources.json').write_text(json.dumps(manifest,indent=2))
    exe=folder/'Scenario.exe';rsp=folder/'compile.rsp'
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['"'+str(p)+'"' for p in sources]
    rsp.write_text('\n'.join(args))
    cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)]
    c=subprocess.run(cmd,capture_output=True,text=True);(folder/'compile.log').write_text(c.stdout+c.stderr);results.append(dict(mode=mode,command=cmd,exit=c.returncode))
    if c.returncode==0:
        for name in ['scenario','repeat']:
            r=subprocess.run([str(exe)],capture_output=True,text=True);(folder/(name+'.log')).write_text(r.stdout+r.stderr);results.append(dict(mode=mode,command=[str(exe)],exit=r.returncode,output=r.stdout+r.stderr))
    (out/'commands-results.json').write_text(json.dumps(results,indent=2))
drift=[dict(path=r['path'],initial=r['sha256'],current=sha(ROOT/r['path'])) for r in initial if sha(ROOT/r['path']).upper()!=r['sha256']]
(out/'dependency-drift.json').write_text(json.dumps(drift,indent=2))
print(json.dumps(results,indent=2));print('Evidence '+str(out))
raise SystemExit(0 if all(r['exit']==0 for r in results) and len(results)==6 else 1)
