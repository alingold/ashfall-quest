import pathlib, subprocess, json, hashlib, shutil
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/AvatarLifecycleBindingR2';D=R/'docs/agent-runs/20261104-three-chats/unity/AvatarLifecycleBindingR2'
# All external inputs are pinned once. Owned new sources refreshed per explicit compiler iteration.
paths=[R/x['path'] for x in json.loads((D/'before.json').read_text()) if x['path'].endswith('.cs') and not x['path'].startswith('unity\\Tools\\Integration')]
paths+=list((R/'unity/Assets/Ashfall/Integration/AvatarPose').rglob('*.cs'))
paths+=list((R/'unity/Tools/Integration/AvatarPose').glob('*R2Tests.cs'))+[R/'unity/Tools/Integration/AvatarPose/PoseIntegrationTests.cs']+[R/'unity/Tools/Modules/Avatar/AvatarLifecycleR8NativeTests.cs']
paths=list(dict.fromkeys(p for p in paths if p.exists()))
sources=[]
for p in paths:
 owned='Integration/AvatarPose/' in p.as_posix() or p.name in ['AvatarView.cs','AvatarLifecycleR8NativeTests.cs']
 q=S/'candidate'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True)
 if owned or not q.exists():shutil.copyfile(p if owned else S/'before'/p.relative_to(R),q)
 sources.append(q)
editor=pathlib.Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data');project=R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+[editor/'NetStandard/ref/2.1.0/netstandard.dll']+[project/'Library/ScriptAssemblies/Unity.InputSystem.dll',project/'Library/ScriptAssemblies/Unity.XR.OpenXR.dll']
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
results=[]
for name in ['neutral','regression29','native-api']:
 native=name=='native-api'
 out=S/(name+('.dll' if native else '.exe'))
 selected=sources
 args=['/nologo','/langversion:5','/warnaserror+','/target:'+('library' if native else 'exe'),'/out:'+str(out)]
 if native:args+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs]
 else:args+=['/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Xml.dll','/main:'+('PoseIntegrationTests' if name=='regression29' else 'LifecycleR2Tests'),'/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.dll','/reference:C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\System.Core.dll']
 args+=[str(p) for p in selected];rsp=S/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[csc,'/noconfig','@'+str(rsp)]
 r=subprocess.run(cmd,capture_output=True,text=True);(D/(name+'.log')).write_text(r.stdout+r.stderr);record={'name':name,'command':cmd,'compile':r.returncode};print(name,r.returncode,r.stdout[-5000:])
 if not native and r.returncode==0:
  fixture=R/'campaign-staging/20261104-three-chats/unity/AvatarR4';r=subprocess.run([str(out)],cwd=fixture,capture_output=True,text=True);record['run']=r.returncode;(D/(name+'-run.log')).write_text(r.stdout+r.stderr);print(r.stdout,r.stderr)
 results.append(record)
(D/'results.json').write_text(json.dumps(results,indent=2))
(D/'compiler-inputs.json').write_text(json.dumps([{'path':str(p.relative_to(R)) if p.is_relative_to(R) else str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sources+refs],indent=2))
