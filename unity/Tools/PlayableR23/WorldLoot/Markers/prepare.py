"""Stage only source-bound red cache marker cuts; never write live Assets/source."""
from pathlib import Path
import argparse,collections,hashlib,json,mmap,re,struct,time
import numpy as np

HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
SOURCE=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
LIBRARY=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps')
RED='p2a61c272'
MAPS={'battlefield-paintball-gods-pitdevil':('World','23182cc9dac699b90a994cf7f2b1595a921102b8d19c1c9e067f5fe64efe9ea1'),
      'alfa-aglae-souza':('Maps/alfa-aglae-souza','b5163f787b8414e8ddca4b7c48c3a3bc435ddb8941a58d7c2f292502b5c37e89')}
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()
def rounded(v):
    p=np.asarray(v)*1000
    return np.where(p>=0,np.floor(p+.5),np.ceil(p-.5)).astype(np.int64)
def components(points,indices):
    # archive_semantics_r8._components: weld1mm, union triangle corners.
    bins=rounded(points);_,first,inverse=np.unique(bins,axis=0,return_index=True,return_inverse=True)
    parent=np.arange(len(first),dtype=np.int32)
    def root(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=int(parent[i])
        return i
    for tri in inverse[indices]:
        a=root(int(tri[0]))
        for k in (1,2):parent[root(int(tri[k]))]=a
    roots=np.array([root(i) for i in range(len(first))],np.int32)
    triangles=roots[inverse[indices[:,0]]];out=[]
    for group in np.unique(triangles):
        ids=np.flatnonzero(triangles==group);used=points[first[roots==group]]
        low=used.min(0);high=used.max(0)
        out.append(dict(ids=ids,low=low,high=high,center=(low.astype(float)+high)*.5,triangles=len(ids)))
    return out
def old_symbol(part):
    size=part['high']-part['low'];n=part['triangles'];thin=min(size[0],size[2]);wide=max(size[0],size[2])
    if 150<=n<=192 and size[1]<.08 and thin>2.8 and wide<3.6:return 'r8-ring'
    if (n in (107,112) and abs(size[1]-.39)<.015 and thin<.07 and wide<.30) or(n==28 and abs(size[1]-.1)<.015 and thin<.07 and wide<.12):return 'r8-question'
    return None
def keys(triangles):
    points=np.ascontiguousarray(triangles,dtype='<f4');points[:,:,2]*=-1
    packed=points.view('V12').reshape(-1,3)
    return np.sort(packed,axis=1).copy().view('V36').ravel()
def source_markers(slug):
    sem=json.loads((SOURCE/'assets/data/map_semantics_r14.json').read_text())['maps'][slug]
    cache=json.loads((SOURCE/'assets/data/map_caches_r17.json').read_text())['maps'][slug]
    meta=json.loads((LIBRARY/slug/'converted/manifest.json').read_text())
    src=LIBRARY/slug/'source/map.glb'
    assert sha(src)==sem['source_sha256'] and cache['cache_fingerprint']==sem['cache_fingerprint']==meta['cache_fingerprint']
    assert not any(row['key']==RED for row in sem['marker_parts'])
    selectors=[r for r in cache['marker_parts'] if r['key']==RED];matched=[[] for _ in selectors]
    removed=set();rows=[];batches=collections.OrderedDict()
    with src.open('rb') as f,mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ) as raw:
        n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);start=28+n
        def acc(i):
            a=doc['accessors'][i];v=doc['bufferViews'][a['bufferView']];assert 'sparse' not in a
            dt=np.dtype({5121:'u1',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]);w={'SCALAR':1,'VEC3':3}[a['type']]
            return np.ndarray((a['count'],w),dt,buffer=raw,offset=start+v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',w*dt.itemsize),dt.itemsize)).copy()
        def walk(i):
            node=doc['nodes'][i];match=re.fullmatch(r'(world|roof)_(-?\d+)_(-?\d+)_'+RED,node.get('name',''))
            if match:
                assert not any(k in node for k in ['matrix','translation','scale','rotation']),node['name']
                kind,x,z=match.groups();batch=(kind,int(x)//4,int(z)//4)
                for pi,p in enumerate(doc['meshes'][node['mesh']]['primitives']):
                    positions=acc(p['attributes']['POSITION']).astype(np.float32)
                    indices=acc(p['indices']).ravel().astype(np.int32).reshape(-1,3) if 'indices' in p else np.arange(len(positions),dtype=np.int32).reshape(-1,3)
                    tri=positions[indices];batches.setdefault(batch,[]).append(tri)
                    for part in components(positions,indices):
                        for si,s in enumerate(selectors):
                            # The immutable R18 supplement records two ALFA rows
                            # that became joined104/88-triangle prepared components.
                            # Match their explicit original members, not a wider box.
                            members=s.get('source_members_r18',[s])
                            if not any(part['triangles']==member['triangles'] and np.sum((part['center']-member['center'])**2)<.000016 for member in members):continue
                            assert (part['low']>=np.array(s['min'])-.0045).all() and (part['high']<=np.array(s['max'])+.0045).all()
                            selected=keys(tri[part['ids']]);removed.update(bytes(k) for k in selected)
                            proof=dict(kind='r17-selector',selectorIndex=si,node=node['name'],primitive=pi,triangles=part['triangles'],center=part['center'].tolist(),min=part['low'].tolist(),max=part['high'].tolist())
                            matched[si].append(proof);rows.append(proof)
            for child in node.get('children',[]):walk(child)
        for i in doc['scenes'][doc.get('scene',0)]['nodes']:walk(i)
    for si,s in enumerate(selectors):
        matches=matched[si];assert len(matches)==len(s.get('source_members_r18',[s])),(si,len(matches))
        assert sum(row['triangles']for row in matches)==s['triangles']
        assert np.max(np.abs(np.min([row['min']for row in matches],axis=0)-s['min']))<.0045 and np.max(np.abs(np.max([row['max']for row in matches],axis=0)-s['max']))<.0045
    # Actual prepared converter groups4x4 cells before _components runs. Recover
    # complete original r8 rings/questions there, not by broad live color/bounds.
    for batch,parts in batches.items():
        tri=np.concatenate(parts);points=tri.reshape(-1,3);indices=np.arange(len(points),dtype=np.int32).reshape(-1,3)
        for part in components(points,indices):
            kind=old_symbol(part)
            if not kind:continue
            removed.update(bytes(k) for k in keys(tri[part['ids']]))
            rows.append(dict(kind=kind,batch=list(batch),triangles=part['triangles'],center=part['center'].tolist(),min=part['low'].tolist(),max=part['high'].tolist()))
    r8=[row for row in rows if row['kind'].startswith('r8-')]
    summary={kind:dict(components=sum(row['kind']==kind for row in r8),triangles=sum(row['triangles']for row in r8 if row['kind']==kind))for kind in ['r8-ring','r8-question']}
    return removed,dict(sourceSha256=sem['source_sha256'],cacheFingerprint=cache['cache_fingerprint'],selectors=len(selectors),selectorTriangles=sum(r['triangles']for r in selectors),selectorMatches=[row for row in rows if row['kind']=='r17-selector'],r8=summary,r8ComponentsSha256=hashlib.sha256(json.dumps(r8,sort_keys=True).encode()).hexdigest(),uniqueMarkerTriangles=len(removed),markerTriangleKeysSha256=hashlib.sha256(b''.join(sorted(removed))).hexdigest())
def stage(slug,target,inspect=False):
    folder,expected=MAPS[slug];live=ROOT/'unity/Assets/StreamingAssets/PlayableR23'/folder
    assert sha(live/'geometry.bytes')==expected,'Live map changed: inspect and explicitly rebind, never process an unknown payload'
    source_keys,proof=source_markers(slug);manifest_raw=(live/'map.json').read_bytes();manifest=json.loads(manifest_raw)
    assert manifest['sourceSha256']==proof['sourceSha256'] and manifest['version']==1
    census=dict(renderBefore=0,collisionBefore=0,renderRemoved=0,collisionRemoved=0,encodedMarkerOccurrences=0,unchangedChunks=0,changedChunks=0,disabledChunks=0)
    changes=[];chunks=[];found=set()
    with (live/'geometry.bytes').open('rb') as f,mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ) as raw:
        magic,count=struct.unpack_from('<ii',raw);assert magic==0x52323357 and count==manifest['chunks'];cursor=8
        for ci in range(count):
            begin=cursor;mid,coll,visible,nv,ni=struct.unpack_from('<iiiii',raw,cursor);cursor+=20;vertex_at=cursor;cursor+=nv*48;index_at=cursor;cursor+=ni*4
            assert 0<=mid<len(manifest['materials']) and ni>=3 and ni%3==0 and nv>0
            census['renderBefore']+=ni//3*bool(visible);census['collisionBefore']+=ni//3*bool(coll)
            if manifest['materials'][mid]['name']!=RED:
                chunks.append(bytes(raw[begin:cursor]));census['unchangedChunks']+=1;continue
            vertices=np.frombuffer(raw,dtype='<f4',count=nv*12,offset=vertex_at).reshape(nv,12)
            indices=np.frombuffer(raw,dtype='<i4',count=ni,offset=index_at).reshape(-1,3)
            xyz=np.ascontiguousarray(vertices[indices,:3]);packed=xyz.view('V12').reshape(-1,3);tri_keys=np.sort(packed,axis=1).copy().view('V36').ravel()
            cut=np.array([bytes(k) in source_keys for k in tri_keys]);found.update(bytes(k) for k in tri_keys[cut]);removed=int(cut.sum())
            if not removed:chunks.append(bytes(raw[begin:cursor]));census['unchangedChunks']+=1;continue
            # Keep original vertex/attribute bytes and chunk numbers, so every
            # existing destructible membership retains its exact index contract.
            keep=indices[~cut].astype('<i4');disabled=len(keep)==0
            if disabled:
                # Loader requires ni>=3. Preserve the inert original encoded mesh
                # with both roles disabled; do not invent a degenerate placeholder.
                chunk=struct.pack('<iiiii',mid,0,0,nv,ni)+bytes(raw[vertex_at:cursor]);census['disabledChunks']+=1
            else:chunk=struct.pack('<iiiii',mid,coll,visible,nv,keep.size)+bytes(raw[vertex_at:index_at])+keep.tobytes()
            chunks.append(chunk);census['changedChunks']+=1;census['renderRemoved']+=removed*bool(visible);census['collisionRemoved']+=removed*bool(coll);census['encodedMarkerOccurrences']+=removed
            changes.append(dict(chunk=ci,material=mid,trianglesBefore=ni//3,removed=removed,disabled=disabled,vertexBytesSha256=hashlib.sha256(raw[vertex_at:index_at]).hexdigest(),beforeSha256=hashlib.sha256(raw[begin:cursor]).hexdigest(),afterSha256=hashlib.sha256(chunk).hexdigest()))
        assert cursor==len(raw)
        del vertices,indices
    assert found==source_keys,dict(source=len(source_keys),found=len(found),missing=len(source_keys-found))
    census.update(renderAfter=census['renderBefore']-census['renderRemoved'],collisionAfter=census['collisionBefore']-census['collisionRemoved'])
    assert sha(live/'geometry.bytes')==expected and (live/'map.json').read_bytes()==manifest_raw
    proof.update(mapId=manifest['mapId'],liveFolder=str(live),originalGeometrySha256=expected,originalMapSha256=hashlib.sha256(manifest_raw).hexdigest(),census=census,changes=changes,engineRun=False,sourceModified=False,liveModified=False,chunkCountUnchanged=count)
    if not inspect:
        target.mkdir(parents=True,exist_ok=False)
        with (target/'geometry.bytes').open('xb')as out:
            out.write(struct.pack('<ii',magic,count))
            for chunk in chunks:out.write(chunk)
        (target/'map.json').write_bytes(manifest_raw);proof['geometrySha256']=sha(target/'geometry.bytes')
        proof['sourceInputs']={str(SOURCE/p):sha(SOURCE/p)for p in ['scripts/archive_semantics_r8.gd','scripts/map_semantics_r14.gd','scripts/archive_gameplay_r8.gd','tools/recover_caches_r17.cjs','tools/r14/semantic_scan.cjs','assets/data/map_semantics_r14.json','assets/data/map_caches_r17.json']}
        (target/'marker-removal.json').write_text(json.dumps(proof,indent=2)+'\n')
    print(json.dumps(dict(map=slug,sourceMarkers=proof['uniqueMarkerTriangles'],selectors=proof['selectors'],census=census,output=None if inspect else str(target)),indent=2),flush=True)
    return proof
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--inspect',action='store_true');parser.add_argument('--output',default='StagedPayload');args=parser.parse_args()
    output=(HERE/args.output).resolve();assert output.is_relative_to(HERE.resolve()) and output!=HERE.resolve()
    for slug in MAPS:stage(slug,output/slug,args.inspect)
