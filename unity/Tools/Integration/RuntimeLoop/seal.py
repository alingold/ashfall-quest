from pathlib import Path
import hashlib,json
root=Path(r'C:\Users\aling\Documents\ashfall');base=root/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5'
assert not (base/'sealed-sources.json').exists(),'Already sealed; new revision required'
records=[]
for r in json.loads((base/'dependencies.json').read_text()):records.append(dict(r,snapshot='inputs/'+r['path']))
for folder in ['unity/Assets/Ashfall/Integration/RuntimeLoop','unity/Tools/Integration/RuntimeLoop']:
 for p in sorted((root/folder).rglob('*.cs')):
  rel=p.relative_to(root);d=p.read_bytes();dest=base/'candidate'/rel;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(d);records.append({'path':rel.as_posix(),'snapshot':dest.relative_to(base).as_posix(),'sha256':hashlib.sha256(d).hexdigest(),'bytes':len(d)})
(base/'sealed-sources.json').write_text(json.dumps(records,indent=2))
(base/'pilot-manifest.json').write_text(json.dumps({'path':'campaign-staging/20261104-three-chats/unity/World/r3/inputs/battlefield-paintball-gods-pitdevil.json','sha256':hashlib.sha256((base/'reference-map.json').read_bytes()).hexdigest()},indent=2))
for script in ['freeze.py','references.py']:
 p=root/'unity/Tools/Integration/RuntimeLoop'/script;s=p.read_text();needle="records=[]";s=s.replace(needle,"raise SystemExit('Development capture disabled: candidate sealed; new revision required')\n"+needle,1);p.write_text(s)
print('Sealed',len(records),'sources')
