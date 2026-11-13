from pathlib import Path
import subprocess,json,shutil,hashlib,sys
R=Path(r'C:\Users\aling\Documents\ashfall')
O=R/'campaign-staging/20261104-three-chats/unity/HostMotionArbitrationR1/run01'
D=R/'docs/agent-runs/20261104-three-chats/unity/HostMotionArbitrationR1/run01'
attempt=sys.argv[1];assert attempt.isalnum()
out=O/attempt;out.mkdir()
receipt=json.loads((D/((sys.argv[2] if len(sys.argv)>2 else 'attempt04')+'.json')).read_text())
sources=[Path(x['snapshot']) for x in receipt['sources'] if not x['original'].endswith('Acceptance.cs')]
for p in [O/'consumed/unity/Assets/Ashfall/Modules/Player/Engine/UnityExclusivePlayerMotor.cs',O/'NativeFixture.cs']:
    q=out/p.name;shutil.copyfile(p,q);sources.append(q)
editor=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
refs=[editor/'NetStandard/ref/2.1.0/netstandard.dll',editor/'Managed/UnityEngine/UnityEngine.CoreModule.dll',editor/'Managed/UnityEngine/UnityEngine.PhysicsModule.dll']
args=['/nologo','/langversion:5','/warnaserror+','/nostdlib+','/define:UNITY_6000_0_OR_NEWER','/target:library','/out:"'+str(out/'MotionNativeFixture.dll')+'"']+['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in sources]
rsp=out/'compile.rsp';rsp.write_text('\n'.join(args))
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)]
p=subprocess.run(cmd,capture_output=True,text=True)
(D/(attempt+'.json')).write_text(json.dumps(dict(command=cmd,compileExit=p.returncode,output=p.stdout+p.stderr,references=[dict(path=str(x),sha256=hashlib.sha256(x.read_bytes()).hexdigest().upper()) for x in refs],sources=[dict(path=str(x),sha256=hashlib.sha256(x.read_bytes()).hexdigest().upper()) for x in sources]),indent=2))
print(p.stdout+p.stderr);print('native API compile exit',p.returncode);raise SystemExit(p.returncode)
