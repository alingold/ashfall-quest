import pathlib,subprocess,json,hashlib
ROOT=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/AvatarPoseIntegrationR1'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/AvatarPoseIntegrationR1'
n=1
while (DOC/('native%02d'%n)).exists():n+=1
DOC=DOC/('native%02d'%n);DOC.mkdir()
OUT=OUT/('native%02d'%n);OUT.mkdir()
editor=pathlib.Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
project=ROOT/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
sources=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs']
for module in ['Avatar','Player','Sessions','InventoryItems','WeaponHandling','HealthStates']:
    folder=ROOT/'unity/Assets/Ashfall/Modules'/module
    sources+=list(folder.rglob('*.cs') if module in ['Avatar','Player'] else folder.glob('*.cs'))
sources+=list((ROOT/'unity/Assets/Ashfall/Integration/AvatarPose').rglob('*.cs'))
sources+=[ROOT/'unity/Tools/Integration/AvatarPose/NativeMutationFixture.cs']
references=list((editor/'Managed/UnityEngine').glob('*.dll'))
references+=[editor/'NetStandard/ref/2.1.0/netstandard.dll']
references+=list((project/'Library/ScriptAssemblies').glob('Unity.InputSystem.dll'))
references+=list((project/'Library/ScriptAssemblies').glob('Unity.XR.OpenXR.dll'))
args=['/nologo','/nostdlib+','/langversion:5','/warnaserror+','/target:library','/define:UNITY_6000_0_OR_NEWER','/out:'+str(OUT/'native-api.dll')]+['/reference:'+str(p) for p in references]+[str(p) for p in sources]
rsp=OUT/'native-api.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args))
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)]
r=subprocess.run(cmd,capture_output=True,text=True)
(DOC/'native-api.log').write_text(' '.join('"'+a+'"' for a in cmd)+'\n'+r.stdout+r.stderr)
(DOC/'native-api-inputs.json').write_text(json.dumps([{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources+references if p.exists()],indent=2))
print(r.returncode);print(r.stdout[-5000:])
