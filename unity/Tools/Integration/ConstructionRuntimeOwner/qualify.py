import hashlib,json,subprocess
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/ConstructionRuntimeOwnerR5';F=B/'qualified-tools';F.mkdir(exist_ok=False)
for p in (R/'unity/Tools/Integration/ConstructionRuntimeOwner').glob('*'):
 if p.is_file():(F/p.name).write_bytes(p.read_bytes())
paths=list(F.glob('*'))+list(Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319').glob('*.dll'))+[Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')]
paths+=[Path(x['path']) for x in json.loads((B/'run02/native02/results.json').read_text())['inputs'] if 'Program Files' in x['path']]
rows=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in paths];(F/'tools-before.json').write_text(json.dumps(rows,indent=2))
commands=[]
for name in ['run.py','mutations.py','native_compile.py']:
 cmd=[r'C:\Users\aling\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe',str(F/name)];t=subprocess.run(cmd,capture_output=True,text=True);(F/(name+'.log')).write_text(t.stdout+t.stderr);commands.append(dict(command=cmd,exit=t.returncode));print(name,t.returncode,t.stdout[-200:]);assert t.returncode==0
(F/'commands.json').write_text(json.dumps(commands,indent=2));(F/'tools-after.json').write_text(json.dumps([dict(r,match=hashlib.sha256(Path(r['path']).read_bytes()).hexdigest()==r['sha256']) for r in rows],indent=2))
