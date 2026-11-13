from pathlib import Path
import hashlib,json
r=Path.cwd(); roots=['unity/Assets/Ashfall/Modules/SaveMigration','unity/Tools/Modules/SaveMigration','campaign-staging/20261104-three-chats/unity/SaveMigration','docs/agent-runs/20261104-three-chats/unity/SaveMigration']
e=r/roots[-1]/'phase03'; manifest=e/'candidate-manifest.json'
assert not manifest.exists(),'Frozen manifest exists; create a new candidate version'
files=[]
for folder in roots:
 for p in sorted((r/folder).rglob('*')):
  if p.is_file() and p!=manifest:
   b=p.read_bytes();files.append(dict(path=p.relative_to(r).as_posix(),bytes=len(b),sha256=hashlib.sha256(b).hexdigest().upper()))
manifest.write_text(json.dumps(dict(status='implementation-candidate-needs-independent-review',questHead='bdba6e30012e1733efd5021fd8ded593f82f26e6',files=files),indent=2))
print('Files:',len(files),'Bytes:',sum(x['bytes'] for x in files),'Manifest SHA256:',hashlib.sha256(manifest.read_bytes()).hexdigest().upper())
