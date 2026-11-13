"""Execute the real managed Unity quaternion helpers without starting the engine."""
from pathlib import Path
import json,subprocess
root=Path(__file__).resolve().parents[4]
here=Path(__file__).resolve().parent
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
out=root/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler/grip'
out.mkdir(parents=True,exist_ok=True)
source=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source/scripts/weapon.gd').read_text(encoding='utf-8-sig')
assert 'return raw * Transform3D(Basis(Vector3.RIGHT, -PI / 2.0), Vector3.ZERO)' in source
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/nostdlib+','/target:exe','/define:UNITY_6000_0_OR_NEWER','/out:'+str(out/'GripPoseChecks.dll')]
args+=['/reference:'+str(p) for p in (editor/'NetStandard/ref/2.1.0').glob('*.dll')]
args+=['/reference:'+str(editor/'Managed/UnityEngine/UnityEngine.MathematicsModule.dll')]
args+=['/reference:'+str(editor/'Managed/UnityEngine/UnityEngine.CoreModule.dll'),str(here/'GripPoseChecks.cs'),str(root/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons/R23TrackedGripPose.cs')]
rsp=out/'checks.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args),encoding='utf-8')
run=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True)
assert run.returncode==0,run.stdout+run.stderr
(out/'GripPoseChecks.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.19'}}}),encoding='utf-8')
run=subprocess.run(['C:/Program Files/dotnet/dotnet.exe',str(out/'GripPoseChecks.dll'),str(editor/'Managed/UnityEngine')],capture_output=True,text=True)
(out/'results.txt').write_text(run.stdout+run.stderr,encoding='utf-8');print(run.stdout+run.stderr);raise SystemExit(run.returncode)
