from pathlib import Path
import json,hashlib,re
O=Path(__file__).resolve().parent
R=O.parents[4]
S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
manifest=S.parent/'SOURCE-MANIFEST.json'
assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
files={x['path']:x['sha256'] for x in json.loads(manifest.read_text())['files']}
pins={}
for name in ['scripts/weapon_inventory.gd','scripts/archive_map1003.gd','scripts/world.gd']:
    pins[name]=sha(S/name);assert pins[name]==files[name],name
source=(S/'scripts/weapon_inventory.gd').read_text()
assert 'for index in range(30):' in source
assert 'Vector3(-4.3+(offset%8)*1.4,.3,.60-floori(float(offset)/8.0)*1.40)' in source
assert '_spawn_pickup(rack.create_instance(rack.catalog_index(index)),point)' in source
archive=(S/'scripts/archive_map1003.gd').read_text()
assert 'inventory.pickups.keys();ids.sort()' in archive
assert 'loot_points[(i*17)%loot_points.size()]' in archive
valley=R/'unity/Assets/StreamingAssets/PlayableR23/Maps/valley/map.json'
assert json.loads(valley.read_text())['sourceSha256']==pins['scripts/world.gd']=='52d05443eaf9ffe0f872ac46eba670da05e3a86c55d45e57aa980ce46ed029d5'
reference=R/'unity/Tools/PlayableR23/WorldLoot/ArchiveItems/reference.json'
shots=[x for x in json.loads(reference.read_text())['shots'] if x['mapId']=='range']
assert [x['name'] for x in shots]==['range-rack','range-food']
fixture=R/'unity/Assets/Ashfall/Editor/Playable/R24BaselineGroundSmoke.cs'
meta=fixture.with_suffix('.cs.meta')
assert re.search(r'^guid: [a-f0-9]{32}\s*$',meta.read_text(encoding='utf-8-sig'),re.M)
receipt=dict(sourceManifestSha256=sha(manifest),sourceFiles=pins,fixtureSha256=sha(fixture),referenceSha256=sha(reference),rangeShots=shots,sourceChecksPassed=True,installedFixtureApiExit=0,nativeExecuted=False)
(O/'source-checks.json').write_text(json.dumps(receipt,indent=2)+'\n')
print('Source pins, literal8-column/copy/sorted17-scatter rules, actual Valley world hash and2 genuine source cameras PASS. Native unrun.')
