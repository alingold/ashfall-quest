from pathlib import Path
import subprocess,re,json
ROOT=Path(__file__).resolve().parents[4];TOOLS=Path(__file__).resolve().parent
PROJECT=ROOT/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
EDITOR=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
profile=next((PROJECT/'Library/PackageCache').glob('com.unity.xr.openxr@*/Runtime/Features/Interactions/OculusTouchControllerProfile.cs'))
source=profile.read_text();assert 'alias = "aimPose", usage = "Pointer"' in source and 'public PoseControl pointer' in source
assert re.search(r'name = "pointer".*?type = ActionType.Pose.*?interactionPath = aim',source,re.S)
assert re.search(r'name = "menu".*?interactionPath = menu,.*?UserPaths.leftHand',source,re.S)
assert '"Primary", "menuButton", "systemButton"' in source
print('PASS installed OpenXR Oculus profile binds pointer/aimPose separately from grip and left menu to its own action')
def compile(name,files,extra):
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(TOOLS/name)]+extra+[str(p) for p in files]
 rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args));result=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(TOOLS/(name+'.log')).write_text(result.stdout+result.stderr);assert result.returncode==0,result.stdout+result.stderr;print('PASS',name,'compile')
compile('pose-cases.exe',[TOOLS/'staged/R23AimPoseRules.cs',TOOLS/'PoseCases.cs'],['/target:exe'])
result=subprocess.run([str(TOOLS/'pose-cases.exe')],capture_output=True,text=True);(TOOLS/'pose-results.txt').write_text(result.stdout+result.stderr);print(result.stdout);assert result.returncode==0,result.stderr
refs=list((EDITOR/'Managed/UnityEngine').glob('*.dll'))+list((EDITOR/'NetStandard/ref/2.1.0').glob('*.dll'))+[PROJECT/'Library/ScriptAssemblies/Unity.InputSystem.dll']
published=ROOT/'unity/Assets/Ashfall/Integration/Playable/Input'
for staged in (TOOLS/'staged').glob('*.cs'):
 if (published/staged.name).exists():assert staged.read_bytes()==(published/staged.name).read_bytes(),'Published source differs: '+staged.name
files=list(published.glob('*.cs')) if published.exists() else list((TOOLS/'staged').glob('*.cs'))
compile('R23ControllerAimInput.dll',files,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
print('No engine/device/input read executed. Published/staged source parity verified.' if published.exists() else 'No engine/device/input read executed. Sources remain staged outside Assets.')
