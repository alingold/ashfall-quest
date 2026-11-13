import json,hashlib,subprocess
from pathlib import Path
R=Path.cwd();B=R/'campaign-staging/20261104-three-chats/unity/PickupSupplyPreparationR1';T=R/'unity/Tools/Integration/PickupSupplyPreparation';Q=B/'qualified-tools';Q.mkdir()
paths=list(T.glob('*'))+list(Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319').glob('*.dll'))+[Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'),Path(r'C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')]+list((B/'sources').rglob('*.cs'))
rows=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in paths];(Q/'before.json').write_text(json.dumps(rows,indent=2))
for p in T.glob('*'):
 if p.is_file():(Q/p.name).write_bytes(p.read_bytes())
results=[]
for name in ['run.py','mutations.py','native_compile.py']:
 cmd=[r'C:/Users/aling/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',str(Q/name)];t=subprocess.run(cmd,capture_output=True,text=True);(Q/(name+'.log')).write_text(t.stdout+t.stderr);results.append(dict(command=cmd,exit=t.returncode));print(name,t.returncode,t.stdout[-500:]);assert t.returncode==0
(Q/'results.json').write_text(json.dumps(results,indent=2));(Q/'after.json').write_text(json.dumps([dict(r,match=hashlib.sha256(Path(r['path']).read_bytes()).hexdigest()==r['sha256']) for r in rows],indent=2))
