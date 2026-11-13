from pathlib import Path
import subprocess,json,sys
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[5]
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
COMP=ROOT/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'
args=[line.strip().strip('"')for line in (COMP/'R23Playable.BuildAndSmoke.rsp').read_text(encoding='utf-8-sig').splitlines()]
refs=[v for v in args if v.startswith('/reference:')]
# Existing editor fixture assembly provides the genuine capture/terminal service.
refs.append('/reference:'+str(COMP/'R23Playable.BuildAndSmoke.dll'))
real_ports='--real-ports' in sys.argv
settings=['/nologo','/langversion:latest','/warn:4','/warnaserror+','/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER;UNITY_EDITOR'+('' if real_ports else ';R6_UI_COMPILE_PORTS'),'/out:'+str(HERE/'R6UiSmoke.API.dll')]
files=[str(HERE/'R24R6UiSmoke.cs')]+([] if real_ports else [str(HERE/'CompileOnlyPorts.cs')])
rsp=HERE/'api.rsp';rsp.write_text('\n'.join('"'+v+'"'for v in settings+refs+files),encoding='utf-8')
proc=subprocess.run([str(E/'NetCoreRuntime/dotnet.exe'),str(E/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'),'/noconfig','@'+str(rsp)],capture_output=True,text=True)
print(proc.stdout+proc.stderr);assert proc.returncode==0
source=(HERE/'R24R6UiSmoke.cs').read_text(encoding='utf-8')
assert 'public IEnumerator Run()' in source
assert 'loop.UI.SetOwnedLoadout(' not in source and 'SetLocomotionAnchorProvider(' not in source
assert 'loop.Gameplay.EquipOwned(' not in source and 'Reflection' not in source
assert 'loop.WeaponWheel.Step(' in source and 'loop.Supplies.TryCollect(' in source
assert 'loop.Gameplay.MotionDamping' in source and 'loop.Gameplay.OffhandInfluence' in source
assert source.count('var '+ 'cleanup=Add(')==1
print('Staged fixture installed Unity/runtime API compile PASS. '+('Actual Root-owned Editor ports resolved; no stubs.' if real_ports else 'Two agreed Editor input ports use THROWING compile-only extensions until Root publishes instance methods.')+' Native/capture/device NOT RUN.')
