"""Adapt the checked R6 converter to the public archive GLB/map.json pair.

The original converter and its independent checker are read only. Frozen
Godot-side semantics are deliberately not attached to a different capture.
"""
from collections import Counter
from pathlib import Path
import hashlib
import json
import os
import shutil
import struct
import subprocess
import sys

os.environ['OPENBLAS_NUM_THREADS'] = '1'
os.environ['OMP_NUM_THREADS'] = '1'
import numpy as np
from PIL import Image

TMP = Path(__file__).resolve().parent
REPO = TMP.parents[2] / 'unity'
R6 = REPO / 'Tools/PlayableR23/World/Catalog'
WINDOWS = Path('/media/al/Windows/Users/aling/Documents/ashfall')
TEXTURES = WINDOWS / 'authoring/textures-v1'
ARCHIVE = 'https://maps.deliciouswines.org/'
STAGE = TMP / 'stage'
INDEX = json.loads((TMP/'m-index.json').read_text())
TEX = json.loads((TMP/'tex-tex.json').read_text())
NOTICE = 'Community map from the public Population: ONE archive. The creator and other rights holders retain their rights. The ASHFALL MIT license and CC0 1.0 dedication do not cover this map.'
sys.dont_write_bytecode = True
sys.path.insert(0, str(R6))

def read(p):
    return json.loads(Path(p).read_text(encoding='utf-8-sig'))

def sha(p):
    with Path(p).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()

def write(p, value):
    Path(p).write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False)+'\n')

def get_texture_table():
    replacements = read(TEXTURES/'manifest.json')
    revision = {m['slot_id']:m for m in read(TEXTURES/'v3/manifest.json')['slots'] if m.get('install')}
    table = {}
    for row in read(TEXTURES/'inventory.json'):
        source = row['sha256']
        if source in revision:
            entry = revision[source]
            path, digest = TEXTURES/'v3'/entry['output'], entry['new_sha256']
        else:
            entry = replacements[source]
            path, digest = TEXTURES/entry['replacement_path'], entry['replacement_sha256']
        for name in row['library_names']:
            assert name not in table or table[name]==(path,digest), name
            table[name] = path,digest
    alias_file=TMP/'R26TextureAliases.json'
    if alias_file.exists():
        for name,row in read(alias_file).items():
            table.setdefault(name,table[row['equivalentName']])
    return table

def input_files(slug):
    metadata = TMP / (slug+'-map.json')
    if slug=='banana-land-gods-pitdevil' and not metadata.exists():
        metadata = TMP/'maps-banana-land-gods-pitdevil-map.json'
    return metadata, TMP / (slug+'-map.glb')

def load_glb(glb):
    with glb.open('rb') as f:
        magic, version, length, size, kind = struct.unpack('<5I',f.read(20))
        assert (magic,version,kind)==(0x46546c67,2,0x4e4f534a) and length==glb.stat().st_size
        doc=json.loads(f.read(size))
        binary_size, binary_kind=struct.unpack('<2I',f.read(8))
        assert binary_kind==0x004e4942 and binary_size+size+28==length
        assert set(doc.get('extensionsRequired',())) <= {'KHR_mesh_quantization'}
        assert len(doc['buffers'])==1 and 'uri' not in doc['buffers'][0]
    return doc

def center_fallback(r6, scene, meta):
    """Find a standable floor near the archived arena center, not the roof."""
    x,y,z=r6.reflected(meta['center'])
    checked=[]
    for radius in (0,4,8,16,24,36,48,72):
        for angle in (range(1) if radius==0 else range(12)):
            dx=radius*np.cos(angle*np.pi/6)
            dz=radius*np.sin(angle*np.pi/6)
            for delta in (4,12,20,-4,32,-12,-20,-32,-48,60):
                hit=scene.floor(x+dx,z+dz,y+delta,8)
                if hit is None or r6.spawn_math.room(scene,hit[0]) is None:
                    continue
                offset=abs(hit[0][1]-y)
                checked.append((float(offset)+radius*.18,hit))
        if len(checked)>=8:
            break
    if not checked:
        return None
    checked.sort(key=lambda pair:pair[0])
    p,n,w=checked[0][1]
    return dict(spawn=(p+[0,.12,0]).tolist(),floor=p.tolist(),normal=n.tolist(),
                witness=w,source='collision floor nearest public arena center',
                proof=r6.spawn_math.room(scene,p))

def prepare_meta(slug, raw, doc, r6):
    scene = doc['scenes'][doc.get('scene',0)]['nodes']
    if len(scene)!=len(doc['nodes']) or any(set(n)-{'name','mesh'} for n in doc['nodes']):
        raise ValueError('Source bounds audit requires flat, identity-transform archive meshes')
    boxes = {}
    world_triangles = 0
    parts = Counter()
    mats = set()
    for node in doc['nodes']:
        kind, _, tail = node['name'].partition('_')
        if kind in ('world','roof'):
            mats.add(tail.split('_',2)[2])
        for primitive in doc['meshes'][node['mesh']]['primitives']:
            a = doc['accessors'][primitive['attributes']['POSITION']]
            bounds = np.asarray([a['min'],a['max']],dtype=float)
            assert np.isfinite(bounds).all() and (bounds[0]<=bounds[1]).all()
            boxes.setdefault(kind,[]).append(bounds)
            count = doc['accessors'][primitive['indices']]['count'] if 'indices' in primitive else a['count']
            assert count%3==0
            parts[kind]+=count//3
            if kind == 'world':
                world_triangles+=count//3
    assert world_triangles == raw['layers']['world_triangles'], (world_triangles,raw['layers'])
    solid = np.array(boxes['world'] + boxes.get('roof',[]))
    solid_min, solid_max = solid[:,0].min(axis=0), solid[:,1].max(axis=0)
    if boxes.get('boundary'):
        cage = np.array(boxes['boundary'])
        low,high=cage[:,0].min(axis=0),cage[:,1].max(axis=0)
    else:
        # Playfield volumes can be small template markers, even when the
        # author built a much larger map outside them. Use the archive viewer's
        # measured arena extent when the source emits no boundary mesh.
        focus=np.asarray(raw['center'],dtype=float)
        half=float(raw['extent'])*.5
        low=np.maximum(solid_min,focus-[half,1e6,half])
        high=np.minimum(solid_max,focus+[half,1e6,half])
    low[1], high[1] = solid_min[1], solid_max[1]
    assert np.isfinite([low,high]).all() and (high-low>1).all()
    candidates = raw['spawns']
    assert all(len(p)==3 and np.isfinite(p).all() for p in candidates)
    meta = dict(map_id='archive:'+slug, title=raw['name'], creator=raw['creator'],
                template=raw['template'], center=raw['center'], recorded_spawns=candidates,
                bounds_min=low.tolist(), bounds_max=high.tolist(), solid_min=solid_min.tolist(),
                solid_max=solid_max.tolist(), solid_triangles=parts['world']+parts['roof'],
                source=ARCHIVE+'maps/'+slug+'/', license=NOTICE)
    mat={}
    for key in sorted(mats):
        if key in raw['materials']:
            mat[key]=dict(raw['materials'][key])
        elif key=='vc':
            mat[key]={}
        else:
            assert key.startswith('t') and key[1:].isdigit(), key
            t=TEX[int(key[1:])]
            assert t['index']==int(key[1:])
            mat[key] = dict(name=t['name'], metal='metal' in t['name'].lower())
            if t.get('c'):
                mat[key]['c']=f'tex/{key[1:]}_c.jpg'
            if t.get('n'):
                mat[key]['n']=f'tex/{key[1:]}_n.jpg'
    return meta,mat,parts

def texture_sources(materials):
    table = get_texture_table()
    selected={}
    for material,row in materials.items():
        for channel in ('c','n'):
            if channel not in row:
                continue
            uri=row[channel]
            assert uri.startswith('tex/')
            if uri[4:] not in table:
                raise ValueError('No replacement texture for '+uri)
            source,digest=table[uri[4:]]
            assert sha(source)==digest,source
            selected[uri]=source
    return selected

def setup_r6(meta, mats, paths, sha256, root):
    """Bind the historical converter to a fresh, hash-pinned source receipt."""
    import prepare as r6
    r6.HERE=root
    r6.STAGE=STAGE
    r6.LIB=root/'archive'
    r6.FROZEN=root/'frozen'
    slug=meta['map_id'].split(':',1)[1]
    cache=hashlib.sha256(('ashfall live archive GLB '+sha256).encode()).hexdigest()
    meta.update(source_sha256=sha256,cache_fingerprint=cache)
    source=r6.LIB/'maps'/slug
    for folder in (source/'source',source/'converted',r6.FROZEN/'assets/data',r6.LIB/'shared'):
        folder.mkdir(parents=True,exist_ok=True)
    for src,dest in zip(paths, (source/'source/map.json',source/'source/map.glb')):
        if not dest.exists():
            os.link(src,dest)
    for uri,path in texture_sources(mats).items():
        dest=r6.LIB/'shared'/uri
        dest.parent.mkdir(parents=True,exist_ok=True)
        if not dest.exists():
            dest.symlink_to(path)
    write(source/'converted/manifest.json',meta)
    write(source/'source/provenance.json',dict(source=meta['source'], license=NOTICE,
            glb=dict(sha256=sha256), materials=mats))
    write(r6.LIB/'catalog.json', {'maps':[dict(slug=slug,cache_fingerprint=cache)]})
    write(r6.FROZEN/'assets/data/map_semantics_r14.json', {'maps':{slug:dict(source_sha256=sha256,cache_fingerprint=cache)}})
    write(r6.FROZEN/'assets/data/archive_semantics_r9.json', {'maps':{meta['map_id']:dict(source_sha256=sha256,materials=mats)}})
    write(r6.FROZEN/'assets/data/map_caches_r17.json', {'maps':{}})
    write(r6.FROZEN/'assets/data/archive_adapters_r10.json', {'files':{}})
    bindings={'cached live map.json':sha(paths[0]),'cached live map.glb':sha256,'archived R6 converter':sha(R6/'prepare.py')}
    r6.bindings=lambda: bindings
    return r6

def prepare_runtime_rows(out, meta):
    p=read(out/'provenance.json')
    m=read(out/'map.json')
    proof=read(out/'spawn-proof.json')
    as_point=lambda xyz: dict(zip('xyz', map(float,xyz)))
    to_source=lambda xyz: [xyz[0],xyz[1],-xyz[2]]
    candidates = proof['verifiedMatchSites']
    warmups = proof['verifiedWarmupSites']
    low,high=meta['bounds_min'],meta['bounds_max']
    solid_min,solid_max=meta['solid_min'],meta['solid_max']
    manifest=sha(out/'map.json')
    digest=p['sourceGlbSha256']
    binding=dict(id=m['mapId'],sourceSha256=digest,manifestSha256=manifest,
                 cacheFingerprint=p['cacheFingerprint'],adapterSha256='')
    launch=dict(binding,payloadFolder='Maps/'+out.name,sourceOrigin=[0,0,0],removedOrigin=[0,0,0],
                catalogMinX=low[0],catalogMinZ=low[2],catalogMaxX=high[0],catalogMaxZ=high[2],
                solidMinY=solid_min[1],solidMaxY=solid_max[1],hasArchiveMetadata=True,
                hasDeploymentTop=True,deploymentTop=solid_max[1])
    recovery=dict(binding,boundsMin=as_point(low),boundsMax=as_point(high),
                  solidMin=as_point(solid_min),solidMax=as_point(solid_max),
                  center=as_point(meta['center']),
                  recorded=[as_point(to_source(v)) for v in candidates],
                  deathmatch=[as_point(to_source(v)) for v in candidates],
                  warmup=[as_point(to_source(v)) for v in warmups],
                  spawnFloors=[],panels=[],hazards=[])
    for role in ('launch','recovery'):
        write(out.parent/(out.name+'-'+role+'.json'),locals()[role])

def convert(slug):
    meta_path,glb=input_files(slug)
    raw=read(meta_path)
    doc=load_glb(glb)
    digest=sha(glb)
    assert isinstance(raw['name'],str) and raw['creator']==INDEX[slug]['creator']
    trial=TMP/'trials'/slug
    meta,mats,roles=prepare_meta(slug,raw,doc,None)
    r6=setup_r6(meta,mats,(meta_path,glb),digest,trial)
    # Run the actual R6 checker in this process with the same scoped configuration.
    import check
    original_run = subprocess.run
    original_choose=r6.choose_spawns
    def choose(scene, mm, sem):
        gm,wm,proof=original_choose(scene,mm,sem)
        if raw.get('under_map') or proof.get('recordedValid',0)==0:
            floor=center_fallback(r6,scene,meta)
            if floor is not None:
                gm=wm=floor['spawn']
                proof['game']=proof['warmup']=floor
                proof['recipe']+='; fallback ranks floors near archive arena center'
        def support(at):
            hit=scene.floor(at[0],at[2],at[1]+.6,1.2)
            if hit is None or abs(hit[0][1]+.12-at[1])>.01 or not r6.spawn_math.room(scene,hit[0]):
                return None
            return hit[0]+[0,.12,0]
        local_bounds=np.array([r6.reflected(meta['bounds_min']),r6.reflected(meta['bounds_max'])])
        low=local_bounds.min(axis=0)+[.6,0,.6]
        high=local_bounds.max(axis=0)-[.6,0,.6]
        match=[]; warm=[]
        def add(rows,at,sep=1):
            if len(rows)>=40 or any(np.linalg.norm(at-x)<sep for x in rows):
                return
            if (at<low).any() or (at>high).any():
                return
            valid=support(at)
            if valid is not None:
                rows.append(valid)
        add(match,np.asarray(gm))
        add(warm,np.asarray(wm))
        for authored in (() if raw.get('under_map') else meta['recorded_spawns']):
            at=np.array(r6.reflected(authored))
            hit=scene.floor(at[0],at[2],at[1]+2,5)
            if hit is None:continue
            valid=hit[0]+[0,.12,0]
            if valid[1]<=gm[1]+20 and valid[1]>=gm[1]-35:
                add(match,valid)
            if np.linalg.norm(valid-np.asarray(wm))<30:
                add(warm,valid)
        # Fill available nearby slots using actual collision, at the chosen floor
        # level. Open maps without recorded points also get distinct LAN sites.
        for rows, origin in ((match,gm),(warm,wm)):
            for radius in (2,4,8,12,18,26,36,48):
                if len(rows)>=24:
                    break
                for angle in range(12):
                    a=angle*np.pi/6
                    x=origin[0]+radius*np.cos(a);z=origin[2]+radius*np.sin(a)
                    hit=scene.floor(x,z,origin[1]+2,6)
                    if hit is not None:
                        add(rows,hit[0]+[0,.12,0],1.2)
        if len(match)<2 or len(warm)<2:
            raise ValueError(f'Insufficient validated source space: {len(match)} match, {len(warm)} warm-up')
        proof.update(verifiedMatchSites=[p.tolist() for p in match],
                     verifiedWarmupSites=[p.tolist() for p in warm])
        return gm,wm,proof
    def run(args,check=False):
        assert args[-2:]==[slug,'--staged'] and str(args[1]).endswith('/check.py')
        checker.check(slug,True)
    checker=check
    subprocess.run=run
    r6.choose_spawns=choose
    try:
        result=r6.convert(slug,False)
    finally:
        subprocess.run=original_run
        r6.choose_spawns=original_choose
    out=STAGE/slug
    p=read(out/'provenance.json')
    p.update(sourceUrl=meta['source'],archivePage=ARCHIVE+'m/'+slug+'/',
             sourceFirst=INDEX[slug]['first'],sourceLast=INDEX[slug]['last'],
             archiveCaptureId=raw.get('map_id'),license=NOTICE,
             upstreamMapJsonSha256=sha(meta_path),
             texturePolicy='ASHFALL original/CC0 replacement kit, same archive texture URI')
    # Keep machine-specific absolute scratch paths out of distributed provenance.
    p['archiveInputs']={meta['source']+name:h for name,h in [('map.glb',digest),('map.json',sha(meta_path))]}
    for entry in p['textureInputs'].values():
        entry['source']=str(Path(entry['source']).resolve().relative_to(WINDOWS))
    p['preview']={'file':''}
    write(out/'provenance.json',p)
    prepare_runtime_rows(out,meta)
    write(trial/'converted-result.json',result)
    return result

if __name__=='__main__':
    try:
        convert(sys.argv[1])
    except (AssertionError,ValueError,FileNotFoundError,KeyError) as error:
        print(f'FAILED {sys.argv[1]}: {type(error).__name__}: {error}', file=sys.stderr)
        raise
