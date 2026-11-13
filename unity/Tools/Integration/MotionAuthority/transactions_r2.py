from pathlib import Path
import subprocess, json, hashlib, sys

R = Path(r'C:\Users\aling\Documents\ashfall')
O = R/'campaign-staging/20261104-three-chats/unity/HostMotionTransactionsR2'
D = R/'docs/agent-runs/20261104-three-chats/unity/HostMotionTransactionsR2'
run = O/sys.argv[1]
run.mkdir(exist_ok=False)
before = O/'before'
inputs = json.loads((D/'inputs.json').read_text())
files = []
manifest = []
owned = ('unity/Assets/Ashfall/Integration/FlightMotor/', 'unity/Assets/Ashfall/Integration/MotionAuthority/')
prepared = 'unity/Assets/Ashfall/Modules/Flight/PreparedMotion.cs'
for item in inputs:
    rel = item['source'].replace('\\', '/')
    if not rel.endswith('.cs') or '/Engine/' in rel:
        continue
    p = R/rel if rel.startswith(owned) or rel == prepared else R/item['snapshot']
    files.append(p)
for folder in owned:
    for p in (R/folder).glob('*.cs'):
        if p not in files:
            files.append(p)
sealed = []
for i,p in enumerate(files):
    q = run/(str(i)+'-'+p.name)
    data = p.read_bytes();q.write_bytes(data);sealed.append(q)
    manifest.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(data).hexdigest()))
results = []
compiler = r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
def execute(argv):
    p = subprocess.run(list(map(str,argv)),capture_output=True,text=True)
    results.append(dict(argv=list(map(str,argv)),exit=p.returncode,output=p.stdout+p.stderr))
    print(p.returncode,(p.stdout+p.stderr)[-1400:]);return p.returncode
def compile_test(name,test):
    q = run/test.name;q.write_bytes(test.read_bytes())
    manifest.append(dict(source=str(test),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()))
    rsp=run/(name+'.rsp');exe=run/(name+'.exe')
    refs=[Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319')/x for x in ['System.dll','System.Core.dll']]
    rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:"'+str(exe)+'"']+['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in sealed+[q]]))
    return execute([compiler,'/noconfig','@'+str(rsp)]) == 0 and execute([exe]) == 0
ok = compile_test('TransactionsR2',R/'unity/Tools/Integration/MotionAuthority/TransactionsR2Tests.cs')
for rel in ['unity/Tools/Integration/FlightMotor/r7/R7Tests.cs','unity/Tools/Integration/FlightMotor/r6/R6Tests.cs','unity/Tools/Integration/FlightMotor/r5/R5Tests.cs','unity/Tools/Integration/FlightMotor/R3Tests.cs','unity/Tools/Modules/Flight/FlightTests.cs','unity/Tools/Modules/Flight/R2Tests.cs']:
    ok = compile_test(Path(rel).stem,R/rel) and ok
(D/(sys.argv[1]+'-results.json')).write_text(json.dumps(dict(results=results,sources=manifest,passed=ok),indent=2))
sys.exit(0 if ok else 1)
