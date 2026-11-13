import pathlib,json,hashlib,subprocess,shutil
root=pathlib.Path(__file__).resolve().parents[4]
stage=root/'campaign-staging/20261104-three-chats/unity/InventoryTransfers/phase03'
out=stage/'current-api-probe-full';(out/'inputs').mkdir(parents=True,exist_ok=False)
inputs=[dict(source=str(root/'unity/Assets/Ashfall/Core/Contracts.cs'),snapshot='Core-Contracts.cs')]
for module in ['InventoryItems','Pickups','WeaponHandling']:
 for p in sorted((root/'unity/Assets/Ashfall/Modules'/module).glob('*.cs')):inputs.append(dict(source=str(p),snapshot=module+'-'+p.name))
files=[];records=[]
for e in inputs:
 p=pathlib.Path(e['source']);dest=out/'inputs'/e['snapshot'];shutil.copy2(p,dest);files.append(dest);records.append(dict(source=str(p),snapshot=str(dest),sha256=hashlib.sha256(dest.read_bytes()).hexdigest().upper()))
(out/'inputs.json').write_text(json.dumps(records,indent=2))
files+=sorted((root/'unity/Assets/Ashfall/Integration/InventoryTransfers').glob('*.cs'))+[root/'unity/Tools/Integration/InventoryTransfers/Tests.cs']
args=['/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(out/'CurrentTests.exe')]+[str(p) for p in files]
rsp=out/'compile.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args))
cmd=['C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe','@'+str(rsp)]
p=subprocess.run(cmd,text=True,capture_output=True)
(out/'commands-results.json').write_text(json.dumps(dict(command=cmd,exit=p.returncode,stdout=p.stdout,stderr=p.stderr,execution='NOT RUN; compatibility probe only'),indent=2))
print(p.stdout,p.stderr,'compile exit',p.returncode)
if p.returncode==0:
 cmd=[str(out/'CurrentTests.exe')];r=subprocess.run(cmd,text=True,capture_output=True);(out/'execution.json').write_text(json.dumps(dict(command=cmd,exit=r.returncode,stdout=r.stdout,stderr=r.stderr),indent=2));print(r.stdout,r.stderr,'run exit',r.returncode)

