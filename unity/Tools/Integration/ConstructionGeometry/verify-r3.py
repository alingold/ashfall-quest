"""Preserve the independent r2 reproducer and exercise old/new real sources."""
from pathlib import Path
import hashlib,json,subprocess
root=Path(__file__).resolve().parents[4]
stage=root/'campaign-staging/20261104-three-chats/unity/Construction/r3'
out=stage/'review-regressions';out.mkdir(exist_ok=False)
review=root/'campaign-staging/20261104-three-chats/unity/reviews/Construction-r2'
core=stage/'inputs/source/Contracts.cs'
records=[]
for name,module,test in [('BoundaryBefore',stage/'inputs/Construction-r2',review/'DirectionBoundary.cs'),('BoundaryAfter',root/'unity/Assets/Ashfall/Modules/Construction',review/'DirectionBoundary.cs'),('IndependentAfter',root/'unity/Assets/Ashfall/Modules/Construction',review/'Independent.cs')]:
    # Keep immutable copies of the read-only reviewer input rather than edit it.
    copy=out/(name+'-input.cs');copy.write_bytes(test.read_bytes())
    sources=[core]+sorted(module.glob('*.cs'))+[copy]
    exe=out/(name+'.exe');rsp=out/(name+'.rsp')
    rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in sources))
    command=['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','@'+str(rsp)]
    c=subprocess.run(command,capture_output=True,text=True);(out/(name+'-compile.txt')).write_text(c.stdout+c.stderr)
    r={'name':name,'command':command,'compileExit':c.returncode,'inputs':[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources]}
    if c.returncode==0:
        t=subprocess.run([str(exe)],capture_output=True,text=True);(out/(name+'-results.txt')).write_text(t.stdout+t.stderr)
        r.update(testExit=t.returncode,exeSha256=hashlib.sha256(exe.read_bytes()).hexdigest(),summary=t.stdout.splitlines()[-1])
    records.append(r);print(name,r)
(out/'receipts.json').write_text(json.dumps(records,indent=2))
if any(r['compileExit'] or r.get('testExit',1)!=(1 if r['name']=='BoundaryBefore' else 0) for r in records):raise SystemExit(1)
