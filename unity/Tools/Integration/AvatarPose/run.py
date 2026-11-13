import pathlib, subprocess, json, hashlib, shutil
ROOT=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/AvatarPoseIntegrationR1'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/AvatarPoseIntegrationR1'
run=1
while (DOC/('run%02d'%run)).exists():run+=1
DOC=DOC/('run%02d'%run);DOC.mkdir()
OUT=OUT/('run%02d'%run);OUT.mkdir()
CSC=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
sources=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs']
for module in ['Avatar','Player','Sessions','InventoryItems','WeaponHandling','HealthStates']:
    sources+=list((ROOT/'unity/Assets/Ashfall/Modules'/module).glob('*.cs'))
sources+=list((ROOT/'unity/Assets/Ashfall/Integration/AvatarPose').rglob('*.cs'))
sources+=[ROOT/'unity/Tools/Modules/Avatar/AvatarR4Fixtures.cs',ROOT/'unity/Tools/Integration/AvatarPose/PoseIntegrationTests.cs']
before=[];frozen=[]
for p in sources:
    dest=OUT/'frozen-before'/p.relative_to(ROOT);dest.parent.mkdir(parents=True,exist_ok=True)
    initial=ROOT/'campaign-staging/20261104-three-chats/unity/AvatarPoseIntegrationR1/inputs'/p.relative_to(ROOT)
    shutil.copyfile(initial if initial.exists() else p,dest);frozen.append(dest)
    before.append({'path':str(p.relative_to(ROOT)),'sha256':sha(p)})
(DOC/'compiler-inputs.json').write_text(json.dumps(before,indent=2))
fixture=OUT/'fixtures';fixture.mkdir(exist_ok=True)
original=ROOT/'campaign-staging/20261104-three-chats/unity/AvatarR4'
for pattern in ['L.gltf','R.gltf','L.expected','R.expected','*.res','*.res.sha']:
    for p in original.glob(pattern):shutil.copyfile(p,fixture/p.name)
(DOC/'fixture-inputs.json').write_text(json.dumps([{'path':str(p),'sha256':sha(p)} for p in fixture.iterdir()],indent=2))
results=[]
for name,inputs in [('frozen-before',frozen),('current',sources)]:
    exe=OUT/(name+'.exe');cmd=[str(CSC),'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+[str(p) for p in inputs]
    c=subprocess.run(cmd,capture_output=True,text=True);log=' '.join('"'+x+'"' for x in cmd)+'\n'+c.stdout+c.stderr
    record={'scope':name,'compile':c.returncode}
    if c.returncode==0:
        r=subprocess.run([str(exe)],cwd=fixture,capture_output=True,text=True);record['run']=r.returncode;log+='\n'+subprocess.list2cmdline([str(exe)])+'\n'+r.stdout+r.stderr
    (DOC/(name+'.log')).write_text(log);results.append(record)
(DOC/'results.json').write_text(json.dumps(results,indent=2))
(DOC/'dependency-drift.json').write_text(json.dumps([dict(x,current=sha(ROOT/x['path']),match=x['sha256']==sha(ROOT/x['path'])) for x in before],indent=2))
print(json.dumps(results))
