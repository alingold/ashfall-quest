import hashlib,json,subprocess,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[4]
BASE=ROOT/'campaign-staging/20261104-three-chats/unity/ConstructionPublicationR3'
out=BASE/sys.argv[1];out.mkdir(exist_ok=False)
index=json.loads((BASE/'baseline/index.json').read_text())
sources=[ROOT/r['consumed'] for r in index if not '/Integration/ConstructionRuntime/' in r['original'].replace('\\','/')]
sources+=list((ROOT/'unity/Assets/Ashfall/Integration/ConstructionRuntime').rglob('*.cs'))
sources+=list((ROOT/'unity/Assets/Ashfall/Integration/ConstructionTransactions').rglob('*.cs'))
sources+=list((ROOT/'unity/Tools/Integration/ConstructionRuntime').glob('*.cs'))
sources+=list((ROOT/'unity/Tools/Integration/ConstructionTransactions').glob('*.cs'))
inputs=[]
for i,p in enumerate(sources):
    q=out/'sources'/f'{i}-{p.name}';q.parent.mkdir(exist_ok=True);q.write_bytes(p.read_bytes());inputs.append(q)
results=[]
for main in ['PublicationTests','CanonicalTests','RuntimeFixtures','R2Holdouts']:
    exe=out/(main+'.exe');rsp=out/(main+'.rsp')
    rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/r:System.Runtime.Serialization.dll\n/main:'+main+'\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in inputs))
    cmd=['C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe','@'+str(rsp)]
    c=subprocess.run(cmd,capture_output=True,text=True);(out/(main+'-compile.log')).write_text(c.stdout+c.stderr)
    r=dict(main=main,command=cmd,compileExit=c.returncode)
    if c.returncode==0:
        t=subprocess.run([str(exe)],capture_output=True,text=True);(out/(main+'-tests.log')).write_text(t.stdout+t.stderr);r.update(testExit=t.returncode,exeSha256=hashlib.sha256(exe.read_bytes()).hexdigest());print(main,t.stdout[-350:],t.stderr[-350:])
    else:print(c.stdout+c.stderr)
    results.append(r)
(out/'results.json').write_text(json.dumps(results,indent=2))
(out/'sources.json').write_text(json.dumps([dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()) for p,q in zip(sources,inputs)],indent=2))
sys.exit(int(any(r['compileExit'] or r.get('testExit',1) for r in results)))
