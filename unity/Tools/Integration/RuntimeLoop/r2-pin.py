from pathlib import Path
import json,hashlib,subprocess
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR2/final04';E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR2/final04'
S.mkdir(parents=True,exist_ok=False);E.mkdir(parents=True,exist_ok=False)
old=json.loads((R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5/sealed-sources.json').read_text())
files=set()
for row in old:
 p=R/row['path']
 if '/Assets/' in p.as_posix():
  if p.parent.name=='Engine':continue
  files.update(p.parent.glob('*.cs'))
for folder in ['Integration/MatchRounds','Integration/LifecycleTransactions','Modules/Combat','Modules/InventoryItems','Modules/Audio','Modules/Presentation']:
 files.update((R/'unity/Assets/Ashfall'/folder).glob('*.cs'))
files.add(R/'unity/Assets/Ashfall/Integration/RuntimeLoop/Engine/UnityHostLoopAdapter.cs')
rows=[]
for p in sorted(files):
 b=p.read_bytes();d=S/'pin'/p.relative_to(R);d.parent.mkdir(parents=True,exist_ok=True);d.write_bytes(b);rows.append(dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(b).hexdigest(),bytes=len(b)))
(E/'pin.json').write_text(json.dumps(rows,indent=2))
cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/reference:System.Runtime.Serialization.dll','/reference:System.Web.Extensions.dll','/out:'+str(S/'Candidate.dll')]+[str(S/'pin'/x['path']) for x in rows]
(E/'compile-command.txt').write_text(subprocess.list2cmdline(cmd));p=subprocess.run(cmd,capture_output=True);(E/'compile.log').write_bytes(p.stdout+p.stderr);print(p.returncode,p.stdout.decode())
