import pathlib,json,hashlib,shutil
root=pathlib.Path(__file__).resolve().parents[4]
stage=root/'campaign-staging/20261104-three-chats/unity/InventoryTransfers/phase03'
docs=root/'docs/agent-runs/20261104-three-chats/unity/InventoryTransfers/phase03'
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
inputs=json.loads((stage/'inputs.json').read_text(encoding='utf-8-sig'))
drift=[dict(source=e['source'],snapshot=e['snapshot'],expected=e['sha256'],actual=h(pathlib.Path(e['source'])),matches=h(pathlib.Path(e['source']))==e['sha256']) for e in inputs]
(docs/'final-input-drift.json').write_text(json.dumps(drift,indent=2))
source=[]
for tree in ['quest-r22/source','game']:
 for name in ['weapon_inventory.gd','weapon_rack.gd','field_consumables.gd','weapon.gd','precision_rifle.gd']:
  p=root/tree/'scripts'/name
  if p.exists():source.append(dict(path=str(p),sha256=h(p),revision='bdba6e30012e1733efd5021fd8ded593f82f26e6' if tree.startswith('quest') else 'differing skin runtime; read-only'))
(docs/'source-inputs.json').write_text(json.dumps(source,indent=2))
frozen=stage/'candidate-source';frozen.mkdir(exist_ok=False)
for folder in ['unity/Assets/Ashfall/Integration/InventoryTransfers','unity/Tools/Integration/InventoryTransfers']:
 for p in (root/folder).iterdir():
  if p.is_file():shutil.copy2(p,frozen/p.name)
entries=[]
for folder in ['unity/Assets/Ashfall/Integration/InventoryTransfers','unity/Tools/Integration/InventoryTransfers','campaign-staging/20261104-three-chats/unity/InventoryTransfers','docs/agent-runs/20261104-three-chats/unity/InventoryTransfers']:
 for p in sorted((root/folder).rglob('*')):
  if p.is_file() and p.name!='candidate-manifest.json':entries.append(dict(path=str(p.relative_to(root)),sha256=h(p),bytes=p.stat().st_size))
manifest=docs/'candidate-manifest.json';manifest.write_text(json.dumps(dict(candidate='InventoryTransfers-phase03/1',status='partial; atomic weapon transfer blocked; independent review required',files=entries),indent=2))
print(json.dumps(dict(manifest=str(manifest),sha256=h(manifest),files=len(entries),consumed=len(inputs),drift=sum(not d['matches'] for d in drift)),indent=2))
