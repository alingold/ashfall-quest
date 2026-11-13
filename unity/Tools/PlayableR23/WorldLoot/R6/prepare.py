"""Read the actual prepared R6 gameplay metadata; no engine or mesh export."""
from pathlib import Path
import hashlib,json,math,sys
import numpy as np
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');SRC=FROZEN/'source'
sha=lambda data:hashlib.sha256(data).hexdigest()
manifest=(FROZEN/'SOURCE-MANIFEST.json').read_bytes()
assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={r['path']:r['sha256'] for r in json.loads(manifest)['files']};inputs={}
def source(name):
    raw=(SRC/name).read_bytes();assert sha(raw)==pins[name],name;inputs[name]=pins[name];return raw
adapters=json.loads(source('assets/data/archive_adapters_r10.json'))['files']
semantics=json.loads(source('assets/data/map_semantics_r14.json'))['maps']
recovered=json.loads(source('assets/data/map_caches_r17.json'))['maps']
for name in ['scripts/archive_gameplay_r8.gd','scripts/map_semantics_r14.gd','scripts/archive_map1003.gd','scripts/sparse_loot_r18.gd','scripts/weapon_wall_rack_r18.gd']:
    source(name)
sys.path.insert(0,str(ROOT/'unity/Tools/PlayableR23/World/ArchitectureR6'));from godot_resource import Resource
sys.path.insert(0,str(ROOT/'campaign-staging/20261104-three-chats/world/r12-fixture-shard-1'));from pack_reader import Pack
payload=ROOT/'unity/Assets/StreamingAssets/PlayableR23'
catalog=json.loads((payload/'TeamLaunch/launch-maps.json').read_text())
assert catalog['contentId']=='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520'
pack=Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck');rows=[];evidence=[]
def vector(v):
    assert len(v)==3 and all(math.isfinite(x) for x in v);return np.asarray(v,dtype=np.float32)
def reflected(v):
    p=vector(v);return [float(p[0]),float(p[1]),float(-p[2])]
try:
    for entry in catalog['rows']:
        if not entry['id'].startswith('archive:'):continue
        slug=entry['id'][8:];raw=pack.read('local_archive_r10/'+slug+'.res')
        assert sha(raw)==adapters[slug]==entry['adapterSha256'],slug
        resource=Resource(raw);details=[r['props']['details'] for r in resource.resources if r['type']=='Resource' and 'details' in r['props']]
        assert len(details)==1,slug;details=details[0]
        semantic=semantics[slug];extra=recovered[slug]
        assert semantic['source_sha256']==entry['sourceSha256']
        assert semantic['cache_fingerprint']==extra['cache_fingerprint']==entry['cacheFingerprint']
        actual=json.loads((payload/entry['payloadFolder']/'map.json').read_text())
        assert actual['mapId']==entry['id'] and actual['sourceSha256']==entry['sourceSha256']
        # Original prepared resource order is the RNG-site identity. Only reject
        # recovered R17 candidates under the source's strict float32 distance<1.
        sites=[vector(p) for p in details['weapon_sites']];base_count=len(sites);duplicates=0
        for candidate in extra['sites']:
            p=vector(candidate)
            if any(float(np.sum((p-old)**2,dtype=np.float32))<1 for old in sites):duplicates+=1;continue
            sites.append(p)
        # These are source-bound converted metadata bounds, never render extents.
        lib=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps')
        meta_path=lib/slug/'converted/manifest.json';meta_raw=meta_path.read_bytes();meta=json.loads(meta_raw)
        assert sha(meta_raw)==entry['manifestSha256'] and meta['source_sha256']==entry['sourceSha256'] and meta['cache_fingerprint']==entry['cacheFingerprint']
        def bounds(prefix):
            lo,hi=meta[prefix+'_min'],meta[prefix+'_max'];assert len(lo)==len(hi)==3 and all(math.isfinite(v) for v in lo+hi)
            return [lo[0],lo[1],-hi[2]],[hi[0],hi[1],-lo[2]]
        low,high=bounds('bounds');solid_low,solid_high=bounds('solid')
        row=dict(mapId=entry['id'],sourceSha256=entry['sourceSha256'],cacheFingerprint=entry['cacheFingerprint'],preparedSha256=adapters[slug],
                 sites=[dict(sourceIndex=i,position=reflected(p)) for i,p in enumerate(sites)],boundsMin=low,boundsMax=high,solidMin=solid_low,solidMax=solid_high)
        rows.append(row)
        # Preserve source R9 board bindings for the subsequent physical-rack port.
        evidence.append(dict(mapId=entry['id'],preparedSha256=adapters[slug],sourceR8=base_count,recoveredR17=len(extra['sites']),duplicates=duplicates,merged=len(sites),sourceBoards=details.get('weapon_boards_r9',[])))
        print(slug,base_count,len(sites)-base_count,'source caches',flush=True)
finally:pack.f.close()
assert len(rows)==105
out=HERE/'Payload';out.mkdir(parents=True,exist_ok=True)
(out/'caches.json').write_text(json.dumps(dict(version=1,contentId=catalog['contentId'],sourceManifestSha256=sha(manifest),maps=rows),separators=(',',':'))+'\n')
(HERE/'source-bindings.json').write_text(json.dumps(dict(sourceInputs=inputs,maps=evidence,scope='Actual prepared R8 plus ordered R17 candidates. Native floor/hazard filtering, empty-site grid and R18 sparse supplements remain runtime work.'),separators=(',',':'))+'\n')
print('Prepared exact105 R6 map rows:',sum(len(r['sites']) for r in rows),'source site candidates;',sum(len(r['sourceBoards']) for r in evidence),'source R9 boards; native unrun')
