"""Exact R9/R18 source board merge. CPU only; no scene/geometry mutation."""
from pathlib import Path
import hashlib,json,sys
import numpy as np
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];F=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');S=F/'source'
sha=lambda b:hashlib.sha256(b).hexdigest();manifest=(F/'SOURCE-MANIFEST.json').read_bytes();assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={r['path']:r['sha256'] for r in json.loads(manifest)['files']};inputs={}
def source(name):
    b=(S/name).read_bytes();assert sha(b)==pins[name],name;inputs[name]=sha(b);return b
fixtures=json.loads(source('assets/data/map_fixtures_r18.json'));adapters=json.loads(source('assets/data/archive_adapters_r10.json'))['files']
for name in ['scripts/map_weapon_boards_r18.gd','scripts/weapon_wall_rack_r18.gd','scripts/weapon_rarity_r6.gd','scripts/weapon.gd']:source(name)
old=json.loads((HERE.parent/'R6/source-bindings.json').read_text())['maps'];catalog=json.loads((ROOT/'unity/Assets/StreamingAssets/PlayableR23/TeamLaunch/launch-maps.json').read_text());look={r['id']:r for r in catalog['rows']}
sys.path.insert(0,str(ROOT/'unity/Tools/PlayableR23/World/ArchitectureR6'));from godot_resource import Resource
sys.path.insert(0,str(ROOT/'campaign-staging/20261104-three-chats/world/r12-fixture-shard-1'));from pack_reader import Pack
pack=Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck');rows=[];before=0;added=0;duplicates=0
def v(p):
    a=np.asarray(p,dtype=np.float32);assert a.shape==(3,) and np.isfinite(a).all();return a
def row(id,lo,size,normal,hint=None):
    lo,size,n=v(lo),v(size),v(normal);assert np.all(size>0) and abs(float(np.linalg.norm(n))-1)<.001 and abs(float(n[1]))<.01
    r=dict(id=id,min=lo.tolist(),size=size.tolist(),normal=n.tolist(),hasFloorHint=hint is not None,floorHint=float(hint or 0));return r
try:
    for meta in old:
        id=meta['mapId'];slug=id[8:];frame=look[id];boards=[]
        if meta['sourceBoards']:
            raw=pack.read('local_archive_r10/'+slug+'.res');assert sha(raw)==adapters[slug]==meta['preparedSha256'];res=Resource(raw)
            details=[r['props']['details'] for r in res.resources if r['type']=='Resource' and 'details' in r['props']];assert len(details)==1 and details[0]['weapon_boards_r9']==meta['sourceBoards']
            for i,b in enumerate(meta['sourceBoards']):boards.append(row('r9/'+str(i),b['box'][:3],b['box'][3:],b['normal'],b.get('floor_hint')));before+=1
        source_row=fixtures['maps'].get(slug,{})
        if source_row.get('weapon_boards_r18'):
            assert source_row['source_sha256']==frame['sourceSha256'] and source_row['cache_fingerprint']==frame['cacheFingerprint']
            for b in source_row['weapon_boards_r18']['boards']:
                assert b['word']=='WEAPONS' and len(b['letters'])==7 and b['selector']['total_letter_triangles']==688
                item=row('r18/'+b['board_id'],b['box_min'],v(b['box_max'])-v(b['box_min']),b['normal'],b.get('floor_hint'))
                center=v(item['min'])+v(item['size'])*np.float32(.5);normal=v(item['normal'])
                if any(float(np.sum((center-(v(p['min'])+v(p['size'])*np.float32(.5)))**2,dtype=np.float32))<1 and float(np.dot(v(p['normal'])/np.linalg.norm(v(p['normal'])),normal))>.95 for p in boards):duplicates+=1;continue
                boards.append(item);added+=1
        if boards:rows.append(dict(mapId=id,sourceSha256=frame['sourceSha256'],cacheFingerprint=frame['cacheFingerprint'],boards=boards))
finally:pack.f.close()
assert before==2 and added+duplicates==1344 and len(rows)==37
out=HERE/'Payload';out.mkdir(parents=True,exist_ok=True);p=out/'weapon-boards-r18.json';p.write_text(json.dumps(dict(version=1,contentId=catalog['contentId'],sourceManifestSha256=sha(manifest),maps=rows),separators=(',',':'))+'\n')
(HERE/'prepared.json').write_text(json.dumps(dict(sourceInputs=inputs,maps=len(rows),originalR9=before,r18Added=added,duplicates=duplicates,boards=sum(len(r['boards']) for r in rows),payloadSha256=sha(p.read_bytes()),payloadBytes=p.stat().st_size,engineExecuted=False),indent=2)+'\n')
print('Prepared',len(rows),'source maps /',sum(len(r['boards']) for r in rows),'boards;',before,'R9,',added,'R18 additions,',duplicates,'source merge duplicates;',p.stat().st_size,'bytes; no native claim')
