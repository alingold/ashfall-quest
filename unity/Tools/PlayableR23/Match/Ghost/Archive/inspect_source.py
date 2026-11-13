from pathlib import Path
import sys,hashlib,json
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5]
sys.path.insert(0,str(ROOT/'unity/Tools/PlayableR23/World/ArchitectureR6'))
from godot_resource import Resource
sys.path.insert(0,str(ROOT/'campaign-staging/20261104-three-chats/world/r12-fixture-shard-1'))
from pack_reader import Pack
S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source');adapters=json.loads((S/'assets/data/archive_adapters_r10.json').read_text())['files']
p=Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck')
for slug in sys.argv[1:]or['magnum-chaosvr']:
 raw=p.read('local_archive_r10/'+slug+'.res');assert hashlib.sha256(raw).hexdigest()==adapters[slug]
 resource=Resource(raw)
 for r in resource.resources:
  if 'details'in r['props']:
   details=r['props']['details'];print(slug,r['type'],list(r['props']),list(details))
   for k in details:
    if k in ['panels','spawn_floors_r9','hazards','jump_pads']:print(k,len(details[k]),str(details[k][:1])[:220])
 nodes=resource.nodes();print('areas',[(n['name'],n['props'])for n in nodes if 'kill'in n['name'].lower() or 'area'in n['name'].lower()][:4])
 areas={n['index']for n in nodes if n['props'].get('collision_layer')==32}
 for n in nodes:
  if n['parent']not in areas or 'shape'not in n['props']:continue
  shape=resource.resources[n['props']['shape'][2]];print('areaShape',n['name'],n['props'].get('transform'),shape['type'],[(k,len(v)if hasattr(v,'__len__')else v)for k,v in shape['props'].items()])
 print('root',resource.resources[-1]['type'],list(resource.resources[-1]['props']))
p.f.close()
