"""Installed Unity API compilation only. Does not start Editor, player or device."""
from pathlib import Path
import subprocess,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
result=subprocess.run([sys.executable,str(ROOT/'unity/Tools/PlayableR23/check_integration.py')],capture_output=True,text=True);(HERE/'integration-api.log').write_text(result.stdout+result.stderr);print(result.stdout,result.stderr);assert result.returncode==0
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');compiled=ROOT/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler/R23Playable.EditorRuntime.dll';refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'))+[compiled]
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/nostdlib+','/target:library','/define:UNITY_EDITOR,UNITY_6000_0_OR_NEWER','/out:'+str(HERE/'MotionFix.NativeFixture.dll')]+['/reference:'+str(p)for p in refs]+[str(HERE/'Staged/R23MotionFixSmoke.cs')]
rsp=HERE/'MotionFix.NativeFixture.rsp';rsp.write_text('\n'.join('"'+x+'"'for x in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'native-fixture-api.log').write_text(r.stdout+r.stderr);print('Staged native fixture API compile:',r.returncode,r.stdout,r.stderr);assert r.returncode==0
