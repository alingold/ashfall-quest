import hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
out=ROOT/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeR2/FrozenAccepted';out.mkdir(exist_ok=False)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
accepted=json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v3.json').read_text(encoding='utf-8-sig'))
checks=[]
def walk(x):
    if isinstance(x,dict):
        if 'path' in x and 'expected' in x:
            p=ROOT/x['path'];checks.append({'path':str(p),'expected':x['expected'],'actual':sha(p),'match':sha(p).lower()==x['expected'].lower()})
        for v in x.values():walk(v)
    elif isinstance(x,list):
        for v in x:walk(v)
walk(accepted)
(out/'accepted-v3-checks.json').write_text(json.dumps(checks,indent=2))
receipts=json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/phase05/small-fix-results.json').read_text())
results=[]
for r in receipts:
    if r['group']!='Construction':continue
    name=r['label'];exe=out/(name+'.exe');sources=[Path(p) for p in r['compiler'] if p.endswith('.cs')]
    # These sealed expected sources, rather than current working dependencies, reproduce acceptance.
    rsp=out/(name+'.rsp');rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in sources))
    command=['C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe','@'+str(rsp)]
    c=subprocess.run(command,capture_output=True,text=True);(out/(name+'-compile.log')).write_text(c.stdout+c.stderr)
    result={'label':name,'command':command,'compileExit':c.returncode,'inputs':[{'path':str(p),'sha256':sha(p)} for p in sources]}
    if c.returncode==0:
        t=subprocess.run([str(exe)],capture_output=True,text=True);(out/(name+'-tests.log')).write_text(t.stdout+t.stderr);result.update(testExit=t.returncode,output=t.stdout,exeSha256=sha(exe));print(name,t.stdout.strip().splitlines()[-1])
    else:print(c.stdout+c.stderr)
    results.append(result)
(out/'results.json').write_text(json.dumps(results,indent=2))
print('accepted manifest checks',len(checks),'mismatch',sum(not c['match'] for c in checks))
raise SystemExit(1 if any(r['compileExit'] or r.get('testExit',1) for r in results) or any(not c['match'] for c in checks) else 0)
