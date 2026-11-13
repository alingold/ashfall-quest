"""Actual original R23 r8 red-ring extraction and r17 merge. CPU only, read-only source."""
from pathlib import Path
import argparse,collections,hashlib,json,math,mmap,re,struct,uuid
import numpy as np
ROOT=Path(__file__).resolve().parents[4]
SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Loot'
DATA=SRC/'assets/data'
LIBRARY=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps')
RED='p2a61c272'

def sha(path):
    h=hashlib.sha256()
    with path.open('rb')as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def red_batches(slug):
    """Read-only marker API: ordered converter batch -> Godot faces[triangle,corner,xyz]."""
    folder=LIBRARY/slug;source=folder/'source/map.glb';geometry=folder/'source/geometry.glb';receipt=json.loads((folder/'source/geometry.json').read_text());meta=json.loads((folder/'converted/manifest.json').read_text())
    sem=json.loads((DATA/'map_semantics_r14.json').read_text())['maps'][slug];cache=json.loads((DATA/'map_caches_r17.json').read_text())['maps'][slug]
    assert sha(source)==receipt['source']==sem['source_sha256']==meta['source_sha256']
    assert cache['cache_fingerprint']==sem['cache_fingerprint']==meta['cache_fingerprint']
    assert geometry.stat().st_size==receipt['bytes']and sha(geometry)==receipt['sha256']
    batches=collections.OrderedDict();red_nodes=0
    with geometry.open('rb')as file,mmap.mmap(file.fileno(),0,access=mmap.ACCESS_READ)as raw:
        length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);base=28+length
        assert g['scenes'][g.get('scene',0)]['nodes']==list(range(len(g['nodes'])))and all('children'not in n for n in g['nodes']),slug+' nonflat scene'
        def acc(index):
            a=g['accessors'][index];b=g['bufferViews'][a['bufferView']];assert a['componentType']==5126 and 'sparse'not in a
            width={'VEC3':3,'SCALAR':1}[a['type']]
            return np.ndarray((a['count'],width),'<f4',raw,offset=base+b.get('byteOffset',0)+a.get('byteOffset',0),strides=(b.get('byteStride',width*4),4)).copy()
        for node in g['nodes']:
            match=re.fullmatch(r'(world|roof)_(-?\d+)_(-?\d+)_'+RED,node.get('name',''))
            if not match:continue
            assert not any(k in node for k in ['matrix','translation','rotation','scale']),node['name']
            red_nodes+=1;kind,cx,cz=match.groups();batch=f'{kind}_{RED}_{int(cx)//4}_{int(cz)//4}'
            for primitive in g['meshes'][node['mesh']]['primitives']:
                assert primitive.get('mode',4)==4;v=acc(primitive['attributes']['POSITION'])
                if 'indices'in primitive:
                    a=g['accessors'][primitive['indices']];b=g['bufferViews'][a['bufferView']];dt=np.dtype({5121:'u1',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]);ix=np.ndarray((a['count'],),dt,raw,offset=base+b.get('byteOffset',0)+a.get('byteOffset',0),strides=(b.get('byteStride',dt.itemsize),)).astype(np.int64)
                else:ix=np.arange(len(v))
                assert len(ix)%3==0 and np.isfinite(v).all()
                # Godot reverses imported glTF corner winding; source triangle order is retained.
                batches.setdefault(batch,[]).append(v[ix.reshape(-1,3)[:,[0,2,1]]])
    batches=collections.OrderedDict((name,np.concatenate(rows))for name,rows in batches.items())
    return batches,dict(sourceSha256=sem['source_sha256'],geometrySha256=receipt['sha256'],cacheFingerprint=cache['cache_fingerprint'],redNodes=red_nodes,batchOrder=list(batches),sceneNodeOrder='flat glTF scene array order',sourceGeometryReceipt=receipt)

def red_components(faces):
    """Source _components:1mm half-away weld, directional union, first-point row order."""
    points=[];parents=[];ids={};tris=[]
    def root(i):
        while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
        return i
    for tri in faces:
        corners=[]
        for v in tri:
            scaled=np.asarray(v,dtype=np.float32)*np.float32(1000);s=scaled.astype(float)
            key=tuple(np.where(s>=0,np.floor(s+.5),np.ceil(s-.5)).astype(np.int64))
            if key not in ids:ids[key]=len(parents);parents.append(len(parents));points.append(v.copy())
            corners.append(ids[key])
        for k in [1,2]:parents[root(corners[k])]=root(corners[0])
        tris.append(corners[0])
    rows=collections.OrderedDict()
    for i,v in enumerate(points):
        key=root(i)
        if key not in rows:rows[key]=dict(low=v.copy(),high=v.copy(),ids=[],firstPoint=i)
        rows[key]['low']=np.minimum(rows[key]['low'],v);rows[key]['high']=np.maximum(rows[key]['high'],v)
    for ti,i in enumerate(tris):rows[root(i)]['ids'].append(ti)
    result=[]
    for key,row in rows.items():
        row['componentRoot']=key;row['size']=row['high']-row['low'];row['center']=row['low']+row['size']*np.float32(.5);row['triangles']=len(row['ids']);result.append(row)
    return result

def is_ring(row):
    s=row['size'];return 150<=row['triangles']<=192 and s[1]<.08 and min(s[0],s[2])>2.8 and max(s[0],s[2])<3.6
def is_question(row):
    s=row['size'];thin=min(s[0],s[2]);width=max(s[0],s[2]);n=row['triangles'];return (n in [107,112]and abs(s[1]-.39)<.015 and thin<.07 and width<.30)or(n==28 and abs(s[1]-.1)<.015 and thin<.07 and width<.12)
def plain(row):
    return dict(componentRoot=row['componentRoot'],triangles=row['triangles'],min=row['low'].tolist(),max=row['high'].tolist(),center=row['center'].tolist(),firstPoint=row['firstPoint'],firstTriangle=row['ids'][0])
def extract_r8(slug):
    batches,qualification=red_batches(slug);rings=[];questions=[];components=0
    for batch,faces in batches.items():
        groups=red_components(faces);components+=len(groups)
        # Winding changes may alter union root IDs; they cannot reorder disconnected rings.
        reverse=red_components(faces[:,[0,2,1]])
        assert [(r['center'].tolist(),r['triangles'])for r in groups if is_ring(r)]==[(r['center'].tolist(),r['triangles'])for r in reverse if is_ring(r)]
        for row in groups:
            if is_ring(row):rings.append(dict(batch=batch,**plain(row)))
            elif is_question(row):questions.append(dict(batch=batch,**plain(row)))
    qualification.update(components=components,rings=rings,questions=questions,ringCount=len(rings),questionCount=len(questions),redTriangles=sum(len(x)for x in batches.values()))
    return rings,qualification
def merge_sites(rings,recovered):
    sites=[dict(source='r8-ring',originalIndex=i,godotPosition=np.asarray(r['center'],np.float32))for i,r in enumerate(rings)];rejected=[]
    for i,site in enumerate(recovered):
        p=np.asarray(site,np.float32);assert p.shape==(3,)and np.isfinite(p).all()
        distances=[float(np.sum((p-old['godotPosition'])**2,dtype=np.float32))for old in sites]
        if distances and min(distances)<1:rejected.append(dict(r17Index=i,duplicateOf=distances.index(min(distances)),distanceSquared=min(distances)));continue
        sites.append(dict(source='r17-recovered',originalIndex=i,godotPosition=p))
    return sites,rejected
def focused_checks():
    assert -1//4==-1 and -4//4==-1 and -5//4==-2
    v=[];tri=[]
    for i in range(20):
        for j in range(4):
            u=math.tau*i/20;w=math.tau*j/4;v.append([(1.6+.01*math.cos(w))*math.cos(u),.01*math.sin(w),(1.6+.01*math.cos(w))*math.sin(u)])
    for i in range(20):
        for j in range(4):
            a=i*4+j;b=i*4+(j+1)%4;c=((i+1)%20)*4+j;d=((i+1)%20)*4+(j+1)%4;tri.extend([[a,b,c],[b,d,c]])
    faces=np.asarray(v,np.float32)[tri];both=np.concatenate((faces+np.array([9,2,3],np.float32),faces+np.array([-9,2,3],np.float32)));rows=red_components(both)
    assert len(rows)==2 and all(is_ring(x)for x in rows)and rows[0]['center'][0]>rows[1]['center'][0]
    sites,rejected=merge_sites([plain(rows[0])],[[9.5,2,3],[10,2,3],[20,2,3],[20.5,2,3]])
    assert len(sites)==3 and [r['r17Index']for r in rejected]==[0,3]and sites[1]['originalIndex']==1
    return dict(positiveRingTriangles=len(faces),orderedDisconnectedRings=len(rows),strictDistanceBoundary=True)

def main(check_only=False):
    checks=focused_checks()
    cache=json.loads((DATA/'map_caches_r17.json').read_text())['maps']
    sem=json.loads((DATA/'map_semantics_r14.json').read_text())['maps']
    maps=[];details={}
    for slug in ['battlefield-paintball-gods-pitdevil','alfa-aglae-souza','classic-bigbox-vr']:
        c,s=cache[slug],sem[slug]
        assert c['cache_fingerprint']==s['cache_fingerprint']
        meta=json.loads((LIBRARY/slug/'converted/manifest.json').read_text())
        rings,qualification=extract_r8(slug);combined,rejected=merge_sites(rings,c['sites'])
        sites=[dict(sourceIndex=i,position=[float(row['godotPosition'][0]),float(row['godotPosition'][1]),float(-row['godotPosition'][2])],source=row['source'],originalIndex=row['originalIndex'])for i,row in enumerate(combined)]
        qualification.pop('questions') # Keep the count; question geometry is not cache-site data.
        qualification.update(recoveredR17=len(c['sites']),rejectedR17Duplicates=rejected,combinedCandidates=len(sites),r17Added=len(sites)-len(rings),reorderedDuplicateOrdinals=sum(r['r17Index']!=r['duplicateOf']for r in rejected));details['archive:'+slug]=qualification
        lo,hi=meta['bounds_min'],meta['bounds_max'];slo,shi=meta['solid_min'],meta['solid_max']
        maps.append(dict(mapId='archive:'+slug,sourceSha256=s['source_sha256'],cacheFingerprint=c['cache_fingerprint'],sites=sites,
            boundsMin=[lo[0],lo[1],-hi[2]],boundsMax=[hi[0],hi[1],-lo[2]],solidMin=[slo[0],slo[1],-shi[2]],solidMax=[shi[0],shi[1],-slo[2]]))
        print(slug,'r8',len(rings),'r17 added',len(sites)-len(rings),'duplicates',len(rejected),'combined',len(sites),flush=True)
    inputs=['assets/data/map_caches_r17.json','assets/data/map_semantics_r14.json','scripts/archive_semantics_r8.gd','scripts/archive_semantics_r9.gd','scripts/archive_gameplay_r8.gd','scripts/archive_map1003.gd','scripts/map_semantics_r14.gd','scripts/ground_item_visibility_r21.gd','tools/convert_archive1003.gd','tools/archive_pipeline/prepare1003.py']
    digests={p:sha(SRC/p)for p in inputs};pinned={p['path']:p['sha256']for p in json.loads((ROOT/'docs/agent-runs/20261104-three-chats/world/r24-integration/R23-BASELINE.json').read_text())['actualFiles']}
    assert all(digest==pinned[path]for path,digest in digests.items()),'Original source differs from pinned R23 baseline'
    provenance=dict(baseline=str(SRC),inputs=digests,checks=checks,
        conversion='Original Godot-local float32 r8 red-ring centers in converter first-insertion batch/component order, then source r17 order; reject each r17 site only when distanceSquared<1 to any already accepted site. Reflect Z once on final output.',
        orderQualification='CPU reconstruction from hash-verified geometry.glb and original converter/component algorithms. All supported scenes flat with identity red-node transforms. No spatial/component-root sorting. Original compressed R10 prepared resource details were not independently decoded, so prepared-resource byte-order/native-filter seed equivalence is not claimed.',
        limits='Candidates require native source floor ray, bounds and hazard validation before final site index/seed assignment. R18 sparse supplements and mounted weapon boards remain outside this payload. Classic has no red/r17 sites and retains runtime floor-grid fallback. Unsupported catalog draws remain unspawned. No engine/native/device job run.',maps=details)
    if not check_only:
        OUT.mkdir(parents=True,exist_ok=True);(OUT/'caches.json').write_text(json.dumps(dict(version=1,maps=maps),indent=2)+'\n');(OUT/'provenance.json').write_text(json.dumps(provenance,indent=2)+'\n')
        for p in [OUT,OUT/'caches.json',OUT/'provenance.json']:
            meta=Path(str(p)+'.meta')
            if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n'if p.is_dir()else''))
    print('PASS ordered original r8 extraction/strict merge/source hash bindings; checkOnly='+str(check_only))
    return provenance
if __name__=='__main__':
    args=argparse.ArgumentParser();args.add_argument('--check-only',action='store_true');main(args.parse_args().check_only)
