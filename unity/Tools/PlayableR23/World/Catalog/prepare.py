"""Bounded, engine-free R6 archive conversion. Original/source and existing maps are read-only.

Each child process decodes ONE GLB, validates real support, and publishes a NEW map.
No fallback plane is ever created. Existing public maps cannot be overwritten.
"""
from pathlib import Path
import argparse, collections, hashlib, importlib.util, json, mmap, os, re, shutil, struct, subprocess, sys, time, uuid
# Keep this CPU worker bounded while the root may own a capture slot.
os.environ['OPENBLAS_NUM_THREADS']='1';os.environ['OMP_NUM_THREADS']='1'
import numpy as np

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[4]
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
LIB=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive')
STAGE=Path('D:/ashfall-map-catalog-staging-r6')
LIVE=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'
EXCLUDED={'alfa-aglae-souza','classic-bigbox-vr','battlefield-paintball-gods-pitdevil','range','valley'}
MANIFEST_SHA='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
CONTENT_ID='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520'
SOURCE_PATHS=['assets/data/architecture_r24.json','assets/data/map_semantics_r14.json','assets/data/archive_semantics_r9.json','assets/data/archive_adapters_r10.json','assets/data/map_caches_r17.json','scripts/archive_catalog1003.gd','scripts/map_catalog1000.gd','scripts/archive_map1003.gd','scripts/community_map1002.gd','scripts/archive_semantics_r8.gd','scripts/archive_semantics_r9.gd','scripts/map_semantics_r14.gd','scripts/foliage_r23.gd','tools/convert_archive1003.gd']
spec=importlib.util.spec_from_file_location('catalog_source_spawn',HERE.parent/'Classic/spawn.py')
spawn_math=importlib.util.module_from_spec(spec);spec.loader.exec_module(spawn_math)

def sha(p):
    h=hashlib.sha256()
    with Path(p).open('rb')as f:
        for b in iter(lambda:f.read(1048576),b''):h.update(b)
    return h.hexdigest()
def load(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(p,value):
    p=Path(p);temporary=p.with_name(p.name+'.tmp-'+str(os.getpid()))
    temporary.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');os.replace(temporary,p)
def bindings():
    manifest=FROZEN.parent/'SOURCE-MANIFEST.json';assert sha(manifest)==MANIFEST_SHA
    m=load(manifest);assert m['contentId']==CONTENT_ID
    rows={r['path']:r for r in m['files']};result={}
    for rel in SOURCE_PATHS:
        p=FROZEN/rel;actual=sha(p);assert actual==rows[rel]['sha256'],rel
        result[rel]=actual
    assert result['assets/data/architecture_r24.json']=='1cb3f22b3267a0d825eadbd3cc2b132f17740fdd0061a74ed5ff44e6e133644d'
    return result
def local(node):
    if 'matrix'in node:return np.array(node['matrix']).reshape(4,4,order='F')
    x,y,z,w=node.get('rotation',[0,0,0,1]);m=np.eye(4)
    m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(node.get('scale',[1,1,1]))
    m[:3,3]=node.get('translation',[0,0,0]);return m
def components(faces):
    """Source r8 1mm Godot-round weld, original first-point bounds and triangle count."""
    flat=faces.reshape(-1,3);scaled=flat*np.float32(1000)
    keys=np.where(scaled>=0,np.floor(scaled+.5),np.ceil(scaled-.5)).astype(np.int64)
    _,first,inverse=np.unique(keys,axis=0,return_index=True,return_inverse=True)
    parents=np.arange(len(first),dtype=np.int32)
    def root(i):
        while parents[i]!=i:parents[i]=parents[parents[i]];i=int(parents[i])
        return i
    for tri in inverse.reshape(-1,3):
        for k in (1,2):parents[root(int(tri[k]))]=root(int(tri[0]))
    roots=np.array([root(i)for i in range(len(first))],np.int32)
    tri_roots=roots[inverse.reshape(-1,3)[:,0]];groups=collections.OrderedDict();points={}
    for i in np.argsort(first):points.setdefault(int(roots[i]),[]).append(int(first[i]))
    for i,g in enumerate(tri_roots):groups.setdefault(int(g),[]).append(i)
    return [dict(ids=np.array(ids,np.int32),low=flat[points[g]].min(axis=0),high=flat[points[g]].max(axis=0))for g,ids in groups.items()]
def marker_masks(faces,key,selectors):
    erase=np.zeros(len(faces),bool);collision_only=np.zeros(len(faces),bool);counts=collections.Counter()
    if key!='p2a61c272'and not selectors:return erase,collision_only,counts
    # Same bounded four-metre / 27-neighbor selector lookup as the source.
    # Large archives contain thousands of authoring parts; avoid a quadratic scan.
    buckets=collections.defaultdict(list)
    for row in selectors:buckets[tuple(np.floor(np.array(row['center'])/4).astype(int))].append(row)
    for group in components(faces):
        ids=group['ids'];lo=group['low'];hi=group['high'];size=hi-lo;center=(hi+lo)*np.float32(.5);count=len(ids)
        ring=150<=count<=192 and size[1]<.08 and min(size[0],size[2])>2.8 and max(size[0],size[2])<3.6
        question=(count in (107,112)and abs(size[1]-.39)<.015 and min(size[0],size[2])<.07 and max(size[0],size[2])<.30)or(count==28 and abs(size[1]-.1)<.015 and min(size[0],size[2])<.07 and max(size[0],size[2])<.12)
        if key=='p2a61c272'and(ring or question):erase[ids]=True;counts['r8RedRing'if ring else'r8RedQuestion']+=count;continue
        cell=np.floor(center/4).astype(int)
        nearby=[row for x in (-1,0,1)for y in (-1,0,1)for z in (-1,0,1)for row in buckets.get((cell[0]+x,cell[1]+y,cell[2]+z),())]
        for row in nearby:
            matched=row['triangles']==count or(not row['visual']and row['triangles']==12 and count==24)
            if matched and np.sum((center-np.array(row['center']))**2)<.000016:
                # R14 rehomes these visuals under OriginalSpawnMarkers, visible in
                # local play. R17 cache symbols are retired by the real caches.
                local_marker=row.get('category')=='r14'and row['visual']
                (erase if row['visual']and not local_marker else collision_only)[ids]=True
                counts['r14LocalMarkerCollisionOnly'if local_marker else'r14r17Visual'if row['visual']else'decorativeCollisionOnly']+=count;break
    return erase,collision_only,counts
def leaf_keys(materials):
    known={'tex/s/demo-gamemap-2-batched/s4_c.png','tex/s/demo-gamemap-batched/s7_c.png','tex/s/demo-gamemap-batched/s46_c.png'}
    return {k for k,r in materials.items()if r.get('alpha')and not r.get('fx')and(r.get('c','').lower()in known or any(w in Path(r.get('c','')).name.lower()for w in ['leaf','leaves','foliage']))}
def reflected(v):return [float(v[0]),float(v[1]),float(-v[2])]
def choose_spawns(scene,meta,sem):
    proof=dict(recipe='source native marker validation, recorded support, then 19x19 floor-grid fallback',spawnFloorsAdded=False,standingClearance=1.9288,capsuleRadius=.3)
    low=np.array(meta['bounds_min']);high=np.array(meta['bounds_max'])
    def validate(v,top,depth,source):
        p=np.array(reflected(v));hit=scene.floor(p[0],p[2],p[1]+top,depth)
        if hit is None:return None
        floor,n,w=hit;room=spawn_math.room(scene,floor)
        if room is None:return None
        return dict(spawn=(floor+[0,.12,0]).tolist(),floor=floor.tolist(),normal=n.tolist(),witness=w,source=source,proof=room)
    native={}
    for kind in ('deathmatch','warmup'):
        native[kind]=[]
        for i,r in enumerate(sem.get(kind,[])):
            row=validate(r['center'],1.5,8,'native '+kind+' '+str(i))
            if row is None:continue
            y=row['floor'][1];hint=r['center']
            if y<hint[1]-6 or y>hint[1]+1:continue
            if kind=='deathmatch'and not(low[0]<=hint[0]<=high[0]and low[2]<=hint[2]<=high[2]):continue
            native[kind].append(row)
    recorded=[]
    for i,r in enumerate(meta.get('recorded_spawns',[])):
        if len(r)!=3 or not np.isfinite(r).all():continue
        if not(low[0]<=r[0]<=high[0]and low[2]<=r[2]<=high[2]):continue
        row=validate(r,2,5,'recorded '+str(i))
        if row is not None and not any(np.linalg.norm(np.array(row['spawn'])-v['spawn'])<1 for v in recorded):recorded.append(row)
    if recorded:
        ordered=sorted(recorded,key=lambda r:r['spawn'][1]);median=ordered[len(ordered)//2]['spawn'][1]
        lobby=[r for r in ordered if r['spawn'][1]>median+20];match=[r for r in ordered if r['spawn'][1]<=median+20]
        game=match[0]if match else recorded[0]
        focus=np.array(reflected(meta['center']));warm=lobby[0]if lobby else min(match or recorded,key=lambda r:np.sum((np.array(r['spawn'])-focus)**2))
        proof['recordedValid']=len(recorded)
    else:
        # Reuse previously checked source 19x19 floor-grid/capsule implementation.
        g,w,grid=spawn_math.fallback(scene,meta);game=grid['game'];warm=grid['warmup'];proof['fallback']=grid
    if native['deathmatch']:game=native['deathmatch'][0]
    if native['warmup']:warm=native['warmup'][0]
    proof.update(game=game,warmup=warm,nativeValid={k:len(v)for k,v in native.items()},recordedInput=len(meta.get('recorded_spawns',[])))
    return game['spawn'],warm['spawn'],proof
def meta(p):
    target=Path(str(p)+'.meta')
    if target.exists():return
    guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6-catalog/'+Path(p).relative_to(LIVE).as_posix()).hex
    target.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+('folderAsset: yes\nDefaultImporter:\n'if Path(p).is_dir()else'DefaultImporter:\n')+'  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
def publish(out,slug):
    assert slug not in EXCLUDED and re.fullmatch('[a-z0-9-]+',slug)
    dest=LIVE/slug
    if dest.exists():
        # Resume only an interrupted NEW publication recorded by our own ledger.
        # Every existing payload byte must still equal its isolated stage.
        receipt=HERE/'results'/f'{slug}.json';assert receipt.is_file(),'Unowned existing map '+slug
        row=load(receipt);assert not row.get('published')and not row.get('refused'),'Refusing to overwrite published map '+slug
        assert row['sourceSha256']==load(out/'map.json')['sourceSha256']
        for existing in dest.iterdir():
            if existing.name.endswith('.meta'):continue
            assert existing.is_file()and(out/existing.name).is_file()and sha(existing)==sha(out/existing.name),'Unowned partial publication change: '+str(existing)
    assert shutil.disk_usage('C:/').free>8*1024**3+sum(p.stat().st_size for p in out.iterdir()if p.is_file()),'C: headroom would drop below 8GiB'
    # Link duplicate NEW output textures, never link mutable output to original library.
    table=load(HERE/'texture-links.json')if(HERE/'texture-links.json').exists()else{}
    dest.mkdir(exist_ok=True);meta(dest)
    for p in out.iterdir():
        if not p.is_file():continue
        d=dest/p.name
        if d.exists():assert sha(d)==sha(p);meta(d);continue
        if p.suffix.lower()in('.png','.jpg','.jpeg')and p.name!='preview.png':
            h=sha(p);old=Path(table[h])if h in table else None
            if old is not None and old.is_file()and sha(old)==h:
                try:os.link(old,d)
                except OSError:
                    # NTFS caps a file at 1024 links; start a new exact-content owner.
                    shutil.copyfile(p,d);table[h]=str(d)
            else:shutil.copyfile(p,d);table[h]=str(d)
        else:shutil.copyfile(p,d)
        assert sha(d)==sha(p);meta(d)
    write(HERE/'texture-links.json',table)
    return dest
def convert(slug,do_publish=False):
    started=time.monotonic();assert slug not in EXCLUDED
    bound=bindings();cat={r['slug']:r for r in load(LIB/'catalog.json')['maps']};catalog=cat[slug];source=LIB/'maps'/slug
    sem=load(FROZEN/'assets/data/map_semantics_r14.json')['maps'][slug]
    archive=load(FROZEN/'assets/data/archive_semantics_r9.json')['maps']['archive:'+slug]
    caches=load(FROZEN/'assets/data/map_caches_r17.json')['maps'].get(slug,{})
    metadata=load(source/'converted/manifest.json');prov=load(source/'source/provenance.json');src=source/'source/map.glb';digest=sha(src)
    assert digest==sem['source_sha256']==archive['source_sha256']==metadata['source_sha256']==prov['glb']['sha256']
    assert sem['cache_fingerprint']==metadata['cache_fingerprint']==catalog['cache_fingerprint']
    if caches:assert caches['cache_fingerprint']==metadata['cache_fingerprint']
    assert archive['materials']==prov['materials'],'source material registry mismatch'
    out=STAGE/slug;out.mkdir(parents=True,exist_ok=True);materials=[];material_ids={};textures={}
    for key,row in prov['materials'].items():
        result=dict(name=key,color='',normal='',metallic=.35 if row.get('metal')else 0,roughness=.72,alpha=bool(row.get('alpha')or'glass'in row.get('name','').lower()),vertexColor=True,normalScale=.6,cull=2,transparency=1 if row.get('alpha')else 0)
        for field,name in [('c','color'),('n','normal')]:
            if field not in row:continue
            p=LIB/'shared'/row[field];assert p.is_file(),p;h=sha(p);dest=key+'_'+field+p.suffix
            if not(out/dest).is_file()or sha(out/dest)!=h:shutil.copyfile(p,out/dest)
            result[name]=dest;textures[dest]=dict(source=str(p),sha256=h,bytes=p.stat().st_size)
        material_ids[key]=len(materials);materials.append(result)
    neutral=len(materials);materials.append(dict(name='source-walkthrough',color='',normal='',metallic=0,roughness=1,alpha=False,vertexColor=True,cull=2))
    selectors=collections.defaultdict(list)
    for field,visual in [('marker_parts',True),('decorative_collision_parts',False)]:
        for row in sem.get(field,[]):selectors[row['key']].append(dict(row,visual=visual,category='r14'))
    for row in caches.get('marker_parts',[]):selectors[row['key']].append(dict(row,visual=True,category='r17'))
    leaves=leaf_keys(archive['materials']);batches=collections.OrderedDict();roles=collections.Counter();zero_fixed=0;negative=aligned=source_solid=0;nodes=0;unported_roles=[]
    stream=src.open('rb');raw=mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ);n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);offset=28+n
    def accessor(i):
        a=doc['accessors'][i];v=doc['bufferViews'][a['bufferView']];assert'sparse'not in a
        dt=np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]);width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        x=np.ndarray((a['count'],width),dt,buffer=raw,offset=offset+v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',width*dt.itemsize),dt.itemsize))
        if a.get('normalized'):return np.maximum(-1,x.astype(np.float32)/np.iinfo(dt).max)
        return x
    def walk(i,parent):
        nonlocal nodes,zero_fixed,negative,aligned,source_solid
        node=doc['nodes'][i];m=parent@local(node)
        if'mesh'in node:
            name=node.get('name','');match=re.fullmatch(r'(world|roof)_(-?\d+)_(-?\d+)_(.+)',name)
            if match:kind,cx,cz,key=match.groups();cell=(int(cx)//4,int(cz)//4);mid=material_ids[key]
            else:kind=name.split('_primitive')[0];cell=(i,0);key='source-walkthrough';mid=neutral;assert kind in('boundary','false','playfield','killzone','trigger'),'Unknown source role: '+name
            nodes+=1
            for pi,p in enumerate(doc['meshes'][node['mesh']]['primitives']):
                assert p.get('mode',4)==4;a=p['attributes'];pos=accessor(a['POSITION']);idx=accessor(p['indices']).astype(np.int32).ravel().copy()if'indices'in p else np.arange(len(pos),dtype=np.int32)
                assert len(idx)%3==0 and len(idx)>0 and idx.min()>=0 and idx.max()<len(pos)
                roles[kind]+=len(idx)//3
                if kind in('playfield','killzone','trigger'):
                    q=pos@m[:3,:3].T+m[:3,3];unported_roles.append(dict(role=kind,sourceNode=name,boundsMin=q.min(axis=0).tolist(),boundsMax=q.max(axis=0).tolist(),triangles=len(idx)//3));continue
                xyz=(pos@m[:3,:3].T+m[:3,3]).astype(np.float32);xyz[:,2]*=-1
                norm=(accessor(a['NORMAL'])@np.linalg.inv(m[:3,:3])).astype(np.float32)if'NORMAL'in a else np.zeros_like(xyz);norm[:,2]*=-1;lengths=np.linalg.norm(norm,axis=1);norm/=np.maximum(lengths[:,None],1e-20)
                if np.linalg.det(m[:3,:3])<0:idx=idx.reshape(-1,3)[:,[0,2,1]].ravel()
                tri=xyz[idx].reshape(-1,3,3);cross=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);dots=np.sum(cross*norm[idx].reshape(-1,3,3).mean(axis=1),axis=1);valid=np.abs(dots)>1e-8;negative+=int((dots[valid]<0).sum());aligned+=int(valid.sum())
                bad=lengths<1e-10
                if bad.any():
                    derived=np.zeros_like(norm)
                    for k in range(3):np.add.at(derived,idx.reshape(-1,3)[:,k],-cross)
                    size=np.linalg.norm(derived,axis=1);derived/=np.maximum(size[:,None],1e-20);derived[size<1e-10]=[0,1,0];norm[bad]=derived[bad];zero_fixed+=int(bad.sum())
                uv=accessor(a['TEXCOORD_0']).astype(np.float32).copy()if'TEXCOORD_0'in a else np.zeros((len(pos),2),np.float32);uv[:,1]=1-uv[:,1]
                col=accessor(a['COLOR_0']).astype(np.float32)if'COLOR_0'in a else np.ones((len(pos),4),np.float32)
                if col.shape[1]==3:col=np.column_stack((col,np.ones(len(pos),np.float32)))
                vertex=np.column_stack((xyz,norm,uv,col)).astype('<f4');assert np.isfinite(vertex).all()
                batches.setdefault((kind,key,*cell),[]).append((vertex,idx,dict(node=i,primitive=pi,name=name)))
                if match:source_solid+=len(idx)//3
        for child in node.get('children',[]):walk(child,m)
    for i in doc['scenes'][doc.get('scene',0)]['nodes']:walk(i,np.eye(4))
    assert source_solid==metadata['solid_triangles'],(source_solid,metadata['solid_triangles'])
    chunks=[];collision_parts=[];marker_counts=collections.Counter();render_count=collision_count=0;source_groups=[]
    render_attributes=hashlib.sha256();collision_attributes=hashlib.sha256()
    for batch,parts in batches.items():
        kind,key,cx,cz=batch;mid=material_ids.get(key,neutral);v=np.concatenate([p[0]for p in parts]);off=0;indices=[]
        for vertex,idx,_ in parts:indices.append(idx+off);off+=len(vertex)
        ii=np.concatenate(indices).reshape(-1,3);faces=v[ii,:3].copy();faces[:,:,2]*=-1
        erase,collision_only,counts=marker_masks(faces,key,selectors[key]);marker_counts.update(counts)
        collision=kind!='false'and key not in leaves;visible=kind!='boundary'
        # Cut only source-connected semantic components, never a marker AABB.
        masks=[(~erase&~collision_only,collision,visible)]
        if collision_only.any()and visible:masks.append((~erase&collision_only,False,True))
        for mask,coll,vis in masks:
            selected=ii[mask]
            if not len(selected):continue
            if vis:render_attributes.update(v[selected].tobytes())
            if coll:collision_attributes.update(v[selected].tobytes())
            if coll:collision_parts.append(v[selected,:3].copy());collision_count+=len(selected)
            if vis:render_count+=len(selected)
            # Compact each <=60k-vertex triangle segment; attributes are copied exactly.
            for start in range(0,len(selected),20000):
                section=selected[start:start+20000];used,inverse=np.unique(section,return_inverse=True);vv=v[used];jj=inverse.ravel().astype('<i4')
                chunks.append((mid,int(coll),int(vis),vv,jj));source_groups.append(dict(sourceBatch=list(batch),sourceParts=[p[2]for p in parts],selectedTriangles=len(section),render=bool(vis),collision=bool(coll)))
    assert collision_parts and 0<len(chunks)<=4096,(len(chunks),'source geometry exceeds native chunk limit')
    print(slug+': decoded '+str(source_solid)+' solid triangles, qualifying actual floor...',flush=True)
    scene=spawn_math.TriangleScene(collision_parts);game,warmup,spawn_proof=choose_spawns(scene,metadata,sem)
    geometry=out/'geometry.bytes'
    with geometry.open('wb')as f:
        f.write(struct.pack('<ii',0x52323357,len(chunks)))
        for mid,coll,vis,v,idx in chunks:f.write(struct.pack('<iiiii',mid,coll,vis,len(v),len(idx)));f.write(v.tobytes());f.write(idx.tobytes())
    low=metadata['bounds_min'];high=metadata['bounds_max']
    manifest=dict(version=1,mapId=metadata['map_id'],title=metadata['title'],sourceSha256=digest,spawn=game,gameSpawn=game,warmupSpawn=warmup,boundsMin=[low[0],low[1],-high[2]],boundsMax=[high[0],high[1],-low[2]],materials=materials,chunks=len(chunks),cameraFarDistance=max(2000,float(np.linalg.norm(np.array(metadata['solid_max'])-metadata['solid_min']))*1.5+200))
    write(out/'map.json',manifest);write(out/'spawn-proof.json',spawn_proof);write(out/'source-chunks.json',source_groups)
    preview=source/'converted/preview.png'
    if not preview.is_file():preview=source/'source/thumb.jpg'
    preview_name=''
    if preview.is_file():preview_name='preview'+preview.suffix;shutil.copyfile(preview,out/preview_name)
    original_metadata={k:metadata.get(k)for k in ['map_id','title','creator','template','center','recorded_spawns','bounds_min','bounds_max','solid_min','solid_max','source','license']}
    source_environment=dict(background='101a2b'if'Moon'in metadata.get('template','')else'9db6c6',ambientColor='c5d5e2',ambientEnergy=.6 if'Moon'in metadata.get('template','')else .8,sunEulerGodot=[-53,-28,0],sunEnergy=1,shadows=False,toneMapper='Filmic',source='scripts/archive_map1003.gd:70-77')
    write(out/'source-semantics.json',dict(mapId=metadata['map_id'],coordinates='original unreflected Godot local; runtime must reflect Z once',metadata=original_metadata,sourceEnvironment=source_environment,semantics=sem,caches=caches,metadataVolumes=unported_roles))
    (out/'SOURCE-LICENSE.txt').write_text(prov['license']+'\nSource: '+prov['source']+'\nCreator: '+metadata['creator']+'\n',encoding='utf-8')
    report=dict(mapId=metadata['map_id'],title=metadata['title'],creator=metadata['creator'],sourceContentId=CONTENT_ID,sourceInputs=bound,archiveInputs={str(p):sha(p)for p in [src,source/'source/provenance.json',source/'source/map.json',source/'converted/manifest.json',LIB/'catalog.json']},sourceGlbSha256=digest,cacheFingerprint=metadata['cache_fingerprint'],preparedAdapterSha256=load(FROZEN/'assets/data/archive_adapters_r10.json')['files'].get(slug),sourceSolidTriangles=source_solid,sourceRoles=dict(roles),renderTriangles=render_count,collisionTriangles=collision_count,sourceMarkerTrianglesRemoved=dict(marker_counts),leafCollisionExcluded=sorted(leaves),sourceZeroNormalsRepaired=zero_fixed,clockwiseNormalAgreement=negative/max(1,aligned),chunks=len(chunks),materials=len(materials),textureInputs=textures,spawn=game,warmupSpawn=warmup,preview=dict(file=preview_name,source=str(preview),sha256=sha(preview)if preview_name else None),geometrySha256=sha(geometry),engineRun=False,renderRun=False,deviceRun=False,limits=['Native mesh cooking/render/entry have not run.','Raw archive geometry and source StandardMaterial overrides are ported; prepared adapter dynamic panels/aperture repair, spawn-floor reconstruction, fixtures, hazards and art repairs remain unported for this map. Static source panels are retained, not deleted.','Source foliage collision exclusion is applied; distant foliage LOD is not ported.','Source local support and capsule queries run against decoded original geometry, not Godot physics. No support geometry is invented.','Current v1 source shader retains its existing alpha-cutout lighting approximation; R6 architecture shader flags are a separate root-owned integration.'],elapsedSeconds=round(time.monotonic()-started,3))
    report['converterVersion']=2
    report['sourceRetainedRenderAttributesSha256']=render_attributes.hexdigest()
    report['sourceRetainedCollisionAttributesSha256']=collision_attributes.hexdigest()
    report['payloadBytes']=sum(p.stat().st_size for p in out.iterdir()if p.is_file());write(out/'provenance.json',report)
    # Parse the actual emitted bytes independently before any asset publication.
    subprocess.run([sys.executable,str(HERE/'check.py'),'--slug',slug,'--staged'],check=True)
    destination=publish(out,slug)if do_publish else out
    result=dict(id=metadata['map_id'],slug=slug,title=metadata['title'],creator=metadata['creator'],relativeFolder='Maps/'+slug,preview=preview_name,sourcePreview=str(preview)if preview_name else None,sourceSha256=digest,cacheFingerprint=metadata['cache_fingerprint'],spawn=game,warmupSpawn=warmup,chunks=len(chunks),renderTriangles=render_count,collisionTriangles=collision_count,payloadBytes=report['payloadBytes'],published=do_publish,destination=str(destination),nativeValidated=False,limits=report['limits'])
    (HERE/'results').mkdir(exist_ok=True);write(HERE/'results'/f'{slug}.json',result);print(json.dumps(result,ensure_ascii=False),flush=True)
    return result
def main():
    p=argparse.ArgumentParser();p.add_argument('--slug');p.add_argument('--central',action='store_true');p.add_argument('--all',action='store_true');p.add_argument('--publish',action='store_true');p.add_argument('--limit',type=int);a=p.parse_args()
    if a.slug:convert(a.slug,a.publish);return
    bindings();keys=[k.removeprefix('archive:')for k in load(FROZEN/'assets/data/architecture_r24.json')['maps']]
    if a.all:keys+= [r['slug']for r in load(LIB/'catalog.json')['maps']if r['slug']not in keys]
    assert a.central or a.all,'Choose --slug/--central/--all';keys=[s for s in keys if s not in EXCLUDED]
    if a.limit:keys=keys[:a.limit]
    failures=[]
    for slug in keys:
        if a.publish and(HERE/'STOP').exists():print('Stopped between maps by owned STOP flag.',flush=True);break
        if(LIVE/slug).exists():continue
        command=[sys.executable,str(Path(__file__).resolve()),'--slug',slug]+(['--publish']if a.publish else[])
        # No parallel GLB decode: one process owns one map and releases memory on exit.
        result=subprocess.run(command,capture_output=True,text=True,encoding='utf-8',errors='replace')
        print(result.stdout,flush=True)
        if result.returncode:
            reason=result.stderr[-5000:];failures.append(dict(slug=slug,reason=reason));print('REFUSED '+slug+': '+reason,flush=True)
            (HERE/'results').mkdir(exist_ok=True);write(HERE/'results'/f'{slug}.json',dict(id='archive:'+slug,slug=slug,published=False,refused=True,reason=reason))
        rows=[load(f)for f in sorted((HERE/'results').glob('*.json'))];write(HERE/'catalog-delta.json',dict(sourceContentId=CONTENT_ID,maps=[r for r in rows if r.get('published')],refused=[r for r in rows if r.get('refused')]))
    if failures:print('Refused '+str(len(failures))+' maps; exact reasons retained, remaining maps continue.',flush=True)
if __name__=='__main__':main()
