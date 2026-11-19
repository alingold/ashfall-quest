"""Independently validate native chunks, every staged spawn, and killzone separation."""
from collections import Counter
from pathlib import Path
import hashlib
import json
import mmap
import struct
import sys

import R26ArchiveCandidate as c
import numpy as np
from PIL import Image
import prepare as r6

def native_body(folder, manifest):
    file=folder/'geometry.bytes'
    collision=[]
    world_parts=[]
    stats=Counter()
    with file.open('rb') as f:
        raw=mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ)
        signature,chunks=struct.unpack_from('<2i',raw)
        assert signature==0x52323357 and 1<=chunks==manifest['chunks']<=4096
        offset=8
        for _ in range(chunks):
            m,col,vis,nv,ni=struct.unpack_from('<5i',raw,offset);offset+=20
            assert 0<=m<len(manifest['materials']) and col in (0,1) and vis in (0,1)
            assert 0<nv<=60000 and 0<ni<=3000000 and ni%3==0
            vertices=np.frombuffer(raw,'<f4',nv*12,offset).reshape(nv,12);offset+=nv*48
            indices=np.frombuffer(raw,'<i4',ni,offset).reshape(-1,3);offset+=ni*4
            assert indices.min()>=0 and indices.max()<nv and np.isfinite(vertices).all()
            assert np.allclose(np.linalg.norm(vertices[:,3:6],axis=1),1,atol=.0001)
            if col:
                collision.append(vertices[indices,:3])
                stats['colliderTriangles']+=len(indices)
            if not vis:
                continue
            stats['visibleTriangles']+=len(indices)
            if manifest['materials'][m]['name']=='sg':
                continue
            # Canonicalize vertex order and count exact triangles to 1 cm.
            # This intentionally ignores terrain tiles and normal/winding variations.
            tri=np.rint(vertices[indices,:3]*100).astype('<i4')
            order=np.lexsort((tri[:,:,2],tri[:,:,1],tri[:,:,0]),axis=1)
            world_parts.append(np.take_along_axis(tri,order[:,:,None],axis=1).reshape(-1,9))
        assert offset==len(raw)
    if world_parts:
        world=np.concatenate(world_parts)
        digest_rows=world.view(np.dtype('V36')).ravel()
        duplicates=len(world)-len(np.unique(digest_rows))
        stats.update(offTerrainTriangles=len(world),coincidentTriangles1cm=duplicates)
    return r6.spawn_math.TriangleScene(collision),dict(stats)

def source_killzones(folder, glb):
    doc=c.load_glb(glb)
    groups=[]
    with glb.open('rb') as stream:
        raw=mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ)
        base=struct.unpack_from('<I',raw,12)[0]+28
        def accessor(idx, width):
            item=doc['accessors'][idx]
            view=doc['bufferViews'][item['bufferView']]
            dtype=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[item['componentType']])
            return np.ndarray((item['count'],width),dtype,buffer=raw,
                offset=base+view.get('byteOffset',0)+item.get('byteOffset',0),
                strides=(view.get('byteStride',width*dtype.itemsize),dtype.itemsize))
        for node in doc['nodes']:
            if node['name']!='killzone':
                continue
            for primitive in doc['meshes'][node['mesh']]['primitives']:
                positions=accessor(primitive['attributes']['POSITION'],3)
                index=accessor(primitive['indices'],1).reshape(-1,3)
                triangle=positions[index]
                for group in r6.components(triangle):
                    part=triangle[group['ids']].astype(float)
                    p=part.reshape(-1,3)
                    lo,hi=group['low'],group['high']
                    if np.any(hi-lo<.01):
                        continue
                    center=(lo+hi)/2
                    normal=np.cross(part[:,1]-part[:,0],part[:,2]-part[:,0])
                    length=np.linalg.norm(normal,axis=1)
                    valid=length>1e-8
                    normal=normal[valid]/length[valid,None]
                    anchor=part[valid,0]
                    if not len(normal):
                        continue
                    normal[np.sum((center-anchor)*normal,axis=1)>0]*=-1
                    distance=np.einsum('ij,ij->i',normal,anchor)
                    planes=np.unique(np.round(np.column_stack((normal,distance)),7),axis=0)
                    # If the merged mesh group is nonconvex, a conservative AABB
                    # serves only as an offline spawn veto.
                    convex=len(planes)>=4 and np.max(p@planes[:,:3].T-planes[:,3])<.015
                    groups.append((lo,hi,planes if convex else None))
    return groups

def validate(slug):
    folder=c.STAGE/slug
    mp=folder/'map.json'
    rp=folder.parent/(slug+'-recovery.json')
    lp=folder.parent/(slug+'-launch.json')
    p=c.read(folder/'provenance.json')
    m=c.read(mp)
    assert m['mapId']=='archive:'+slug and m['sourceSha256']==p['sourceGlbSha256'] and m['version']==1
    assert m['boundsMax'][0]>m['boundsMin'][0] and m['boundsMax'][2]>m['boundsMin'][2]
    assert c.sha(folder/'geometry.bytes')==p['geometrySha256']
    digest=p['sourceGlbSha256']
    _,glb=c.input_files(slug)
    assert c.sha(glb)==digest
    for mat in m['materials']:
        for name in (mat['color'],mat['normal']):
            if not name:
                continue
            assert Path(name).name==name and c.sha(folder/name)==p['textureInputs'][name]['sha256']
            with Image.open(folder/name) as image:
                image.load()
                assert image.width and image.height
    scene,stats=native_body(folder,m)
    assert stats['visibleTriangles']==p['renderTriangles']
    assert stats['colliderTriangles']==p['collisionTriangles']
    recover=c.read(rp)
    launch=c.read(lp)
    original_game=m['gameSpawn']
    original_warm=m['warmupSpawn']
    zones=source_killzones(folder,glb)
    def outside_hazards(source):
        point=np.array([source[a] for a in 'xyz'])
        for y in (.1,.9,1.7):
            test=point+[0,y,0]
            for lo,hi,planes in zones:
                if np.all(test>=lo-.3) and np.all(test<=hi+.3):
                    if planes is None or np.all(planes[:,:3]@test-planes[:,3]<=.3):
                        return False
        return True
    def safe(points):
        filtered=[]
        for point in points:
            x,y,z=(point[a] for a in 'xyz')
            hit=scene.floor(x,-z,y+.6,1.2)
            assert hit is not None and abs(hit[0][1]+.12-y)<.001 and r6.spawn_math.room(scene,hit[0]), point
            if outside_hazards(point):
                filtered.append(point)
        return filtered
    recovery_fields=('recorded','deathmatch','warmup')
    for field in recovery_fields:
        recover[field]=safe(recover[field])
        assert len(recover[field])>=2, ('Insufficient hazard-clear sites',field,len(recover[field]))
    def spawn(point):
        return [point['x'],point['y'],-point['z']]
    m['spawn']=m['gameSpawn']=spawn(recover['deathmatch'][0])
    m['warmupSpawn']=spawn(recover['warmup'][0])
    def as_point(p):
        return dict(zip('xyz',map(float,p)))
    def as_plane(p):
        return dict(zip('xyzd',map(float,p)))
    recover['hazards']=[dict(minimum=as_point(lo),maximum=as_point(hi),
            planes=[as_plane(v) for v in planes])
            for lo,hi,planes in zones if planes is not None]
    if [original_game,original_warm]!=[m['gameSpawn'],m['warmupSpawn']]:
        c.write(mp,m)
    recover['manifestSha256']=launch['manifestSha256']=c.sha(mp)
    c.write(rp,recover)
    c.write(lp,launch)
    proof_path=folder/'spawn-proof.json'
    proof=c.read(proof_path)
    proof['finalRuntimeValidation']=dict(gameSpawn=m['gameSpawn'],warmupSpawn=m['warmupSpawn'],
        matchSites=[spawn(p) for p in recover['deathmatch']],
        warmupSites=[spawn(p) for p in recover['warmup']],
        sourceKillzoneGroups=len(zones),convexRuntimeKillzones=len(recover['hazards']))
    c.write(proof_path,proof)
    p['finalManifestSha256']=recover['manifestSha256']
    c.write(folder/'provenance.json',p)
    receipt=dict(mapId=m['mapId'],sourceSha256=digest,manifestSha256=c.sha(mp),
        chunks=m['chunks'],matchSites=len(recover['deathmatch']),warmupSites=len(recover['warmup']),
        triangleCounts=stats,killzoneGroups=len(zones),runtimeKillzoneGroups=len(recover['hazards']))
    c.write(folder.parent/(slug+'-audit.json'),receipt)
    return receipt

if __name__=='__main__':
    for slug in sys.argv[1:]:
        print(json.dumps(validate(slug)),flush=True)
