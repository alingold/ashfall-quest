from pathlib import Path
import subprocess,json,hashlib
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5';E=R/'docs/agent-runs/20261104-three-chats/unity/MotionRuntimeLoopCompositionR5';O=S/'gate-mutant';L=E/'gate-mutant';O.mkdir();L.mkdir()
base=S/'regression02';p=base/'candidate/unity/Assets/Ashfall/Integration/RuntimeLoop/HostLoop.cs';s=p.read_text();old='if(f.MotionMode!=RuntimeMotionMode.PlayerOnly)';assert old in s
mut=O/'HostLoop.cs';mut.write_text(s.replace(old,'if((int)f.MotionMode<0)'));rsptext=(E/'regression02/R5.rsp').read_text().replace(str(p),str(mut)).replace(str(base/'R5.exe'),str(O/'Mutant.exe'))
rsp=L/'Mutant.rsp';rsp.write_text(rsptext);cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True);(L/'compile.log').write_bytes(p.stdout+p.stderr);assert p.returncode==0
p=subprocess.run([str(O/'Mutant.exe')],cwd=base,capture_output=True);(L/'run.log').write_bytes(p.stdout+p.stderr);print(p.stdout.decode()[-600:]);assert p.returncode!=0
(L/'result.json').write_text(json.dumps(dict(command=cmd,exit=p.returncode,expected='killed by uncertified full composition rejection'),indent=2))
