import pathlib,json,subprocess,hashlib,sys
root=pathlib.Path(__file__).resolve().parents[4]
stage=root/'campaign-staging/20261104-three-chats/unity/InventoryTransfers/phase03'
out=stage/sys.argv[1]
out.mkdir(exist_ok=False)
sources=sorted((stage/'inputs').glob('*.cs'))+sorted((root/'unity/Assets/Ashfall/Integration/InventoryTransfers').glob('*.cs'))+[root/'unity/Tools/Integration/InventoryTransfers/Tests.cs']
args=['C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(out/'Tests.exe')]+[str(p) for p in sources]
rsp=out/'compile.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args[1:]))
commands=[]
for cmd in [[args[0],'@'+str(rsp)],[str(out/'Tests.exe')]]:
 p=subprocess.run(cmd,capture_output=True,text=True);commands.append(dict(command=cmd,exit=p.returncode,stdout=p.stdout,stderr=p.stderr));print(p.stdout,p.stderr)
 if p.returncode:break
(out/'commands-results.json').write_text(json.dumps(commands,indent=2))
sys.exit(commands[-1]['exit'])
