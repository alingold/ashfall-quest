from pathlib import Path
import subprocess,json,hashlib,sys
R=Path.cwd();label=sys.argv[1] if len(sys.argv)>1 else 'final04';assert label.isalnum();S=R/'campaign-staging/20261104-three-chats/unity/WeaponWorldTransfersR2'/label;S.mkdir(exist_ok=False);D=R/'docs/agent-runs/20261104-three-chats/unity/WeaponWorldTransfersR2'
prod=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
for folder in ['Modules/InventoryItems','Modules/WeaponHandling','Modules/Pickups','Integration/InventoryTransfers','Integration/WorldWeaponTransfers']:prod+=sorted((R/'unity/Assets/Ashfall'/folder).rglob('*.cs'))
# Freeze exact small source snapshot before compiling.
for p in prod:
 q=S/'candidate'/p.relative_to(R);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(p.read_bytes())
prod=[S/'candidate'/p.relative_to(R) for p in prod]
logs=[]
def build(name,inputs,target='exe',refs=[]):
 out=S/name;out.mkdir(exist_ok=True);binary=out/('candidate.dll' if target=='library' else 'test.exe');rsp=out/'compile.rsp'
 args=['/nologo','/langversion:5','/warnaserror+','/target:'+target,'/out:'+str(binary)]+['/reference:'+str(p) for p in refs]+[str(p) for p in inputs]
 rsp.write_text('\n'.join('"'+a+'"' for a in args));cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);e={'name':name,'command':cmd,'compileExit':c.returncode,'diagnostics':c.stdout+c.stderr}
 if c.returncode==0 and target=='exe':
  import shutil
  for ref in refs:shutil.copyfile(ref,out/ref.name)
  t=subprocess.run([str(binary)],capture_output=True,text=True);e.update(runExit=t.returncode,stdout=t.stdout+t.stderr)
 logs.append(e);print(json.dumps(e));(D/(label+'-results.json')).write_text(json.dumps(logs,indent=2));return binary
test=S/'WorldTests.cs';test.write_bytes((R/'unity/Tools/Integration/WorldWeaponTransfers/WorldTests.cs').read_bytes());dll=build('public-library',prod,'library');build('world-public-api',[test],refs=[dll])
assert all(e['compileExit']==0 and e.get('runExit',0)==0 for e in logs)


