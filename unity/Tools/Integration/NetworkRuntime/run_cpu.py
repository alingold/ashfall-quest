import hashlib, json, pathlib, subprocess
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/NetworkRuntimeR1'
D=R/'docs/agent-runs/20261104-three-chats/unity/NetworkRuntimeR1'
C=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
i=1
while (S/('attempt-%02d'%i)).exists(): i+=1
O=S/('attempt-%02d'%i); O.mkdir(); L=D/O.name; L.mkdir()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
commands=[]; inputs=[]; outcomes=[]
def pin(p,where):
    q=O/where/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
    b=p.read_bytes();q.write_bytes(b)
    inputs.append(dict(path=str(p),snapshot=str(q),sha256=sha(q)))
    return q
def sources(base):
    root=base/'unity/Assets/Ashfall'
    result=[root/'Core/Contracts.cs',root/'Modules/Sessions/Session.cs',root/'Modules/Sessions/Ports.cs']
    for part in ['Modules/Networking','Integration/Sessions','Integration/SessionReplication']:
        result+=sorted((root/part).glob('*.cs'))
    return result
candidate=[pin(p,'candidate') for p in sorted((R/'unity/Assets/Ashfall/Integration/NetworkRuntime').rglob('*.cs'))]
tests=[pin(p,'candidate') for p in sorted((R/'unity/Tools/Integration/NetworkRuntime').glob('*.cs'))]
def suite(name,deps):
    exe=O/(name+'.exe')
    cmd='"%s" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%s" '%(C,exe)+' '.join('"%s"'%p for p in deps+candidate+tests)
    for phase,command in [('compile',cmd),('run','"%s"'%exe)]:
        p=subprocess.run(command,capture_output=True,text=True,timeout=60)
        (L/(name+'-'+phase+'.txt')).write_text(p.stdout+p.stderr)
        commands.append(dict(command=command,exit=p.returncode));print(name,phase,p.returncode,p.stdout+p.stderr)
        if p.returncode: outcomes.append(dict(suite=name,phase=phase,exit=p.returncode));return
    outcomes.append(dict(suite=name,exit=0))
initial=sources(S/'initial')
suite('Initial',[pin(p,'initial-run') for p in initial])
suite('Current',[pin(p,'current-run') for p in sources(R)])
(L/'inputs.json').write_text(json.dumps(inputs,indent=2))
(L/'commands.json').write_text(json.dumps(commands,indent=2))
(L/'results.json').write_text(json.dumps(outcomes,indent=2))
drift=[]
for x in inputs:
    if 'current-run' in x['snapshot']:drift.append(dict(path=x['path'],tested=x['sha256'],current=sha(pathlib.Path(x['path']))))
(L/'drift-after.json').write_text(json.dumps(drift,indent=2))
raise SystemExit(1 if any(x['exit'] for x in outcomes) else 0)
