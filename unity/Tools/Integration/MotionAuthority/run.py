from pathlib import Path
import subprocess,json,hashlib,shutil,sys
R=Path(r'C:\Users\aling\Documents\ashfall')
O=R/'campaign-staging/20261104-three-chats/unity/HostMotionArbitrationR1/run01'
D=R/'docs/agent-runs/20261104-three-chats/unity/HostMotionArbitrationR1/run01'
attempt=sys.argv[1];assert attempt.isalnum()
out=O/attempt;out.mkdir()
base=O/'consumed/unity/Assets/Ashfall'
if len(sys.argv)>2 and sys.argv[2]=='latest':
    base=out/'latest-consumed/unity/Assets/Ashfall'
    originals=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
    for part in ['Modules/Player','Modules/Flight','Modules/Climbing','Integration/FlightMotor','Integration/ClimbingMotor']:
        originals+=list((R/'unity/Assets/Ashfall'/part).glob('*.cs'))
    for p in originals:
        q=base/p.relative_to(R/'unity/Assets/Ashfall');q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
        assert hashlib.sha256(p.read_bytes()).digest()==hashlib.sha256(q.read_bytes()).digest()
sources=[base/'Core/Contracts.cs']
for part in ['Modules/Player','Modules/Flight','Modules/Climbing','Integration/FlightMotor','Integration/ClimbingMotor']:
    sources+=list((base/part).glob('*.cs'))
sources+=list((R/'unity/Assets/Ashfall/Integration/MotionAuthority').glob('*.cs'))+[R/'unity/Tools/Integration/MotionAuthority/Acceptance.cs']
sealed=[];manifest=[]
for i,p in enumerate(sources):
    q=out/(str(i)+'-'+p.name);shutil.copyfile(p,q);sealed.append(q)
    manifest.append(dict(original=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest().upper()))
args=['/nologo','/langversion:5','/warnaserror+','/target:exe','/main:MotionAcceptance','/out:"'+str(out/'Acceptance.exe')+'"']+['"'+str(p)+'"' for p in sealed]
rsp=out/'compile.rsp';rsp.write_text('\n'.join(args))
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)]
p=subprocess.run(cmd,capture_output=True,text=True);print(p.stdout+p.stderr)
result=dict(command=cmd,compileExit=p.returncode,compileOutput=p.stdout+p.stderr,sources=manifest)
if p.returncode==0:
    p=subprocess.run([str(out/'Acceptance.exe')],capture_output=True,text=True);result.update(testExit=p.returncode,testOutput=p.stdout+p.stderr);print(p.stdout+p.stderr)
(D/(attempt+'.json')).write_text(json.dumps(result,indent=2))
sys.exit(p.returncode)
