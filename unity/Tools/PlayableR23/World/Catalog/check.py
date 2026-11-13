"""Read emitted native payloads independently; CPU only, one map/process."""
import argparse,hashlib,json,mmap,struct,subprocess,sys
from pathlib import Path
import numpy as np
import prepare as p

def focused():
    # Two adjacent triangles welded at source 1mm precision form one component.
    f=np.array([[[0,0,0],[1,0,0],[1,0,1]],[[0,0,0],[1,0,1],[0,0,1]]],np.float32)
    assert len(p.components(f))==1
    e,c,n=p.marker_masks(f,'test',[dict(triangles=2,center=[.5,0,.5],visual=True)])
    assert e.all()and not c.any()
    e,c,n=p.marker_masks(f,'test',[dict(triangles=2,center=[.5,0,.5],visual=False)])
    assert not e.any()and c.all()
    e,c,n=p.marker_masks(f,'test',[dict(triangles=3,center=[.5,0,.5],visual=True),dict(triangles=2,center=[.51,0,.5],visual=True)])
    assert not e.any()and not c.any(),'No AABB/nearby collateral removal'
    # Native Unity clockwise floor -> actual outward up, full capsule and footprint.
    floor=np.array([[[-5,0,-5],[5,0,-5],[5,0,5]],[[-5,0,-5],[5,0,5],[-5,0,5]]],np.float32)
    scene=p.spawn_math.TriangleScene([floor]);hit=scene.floor(0,0,3,5)
    assert hit is not None and hit[1][1]>.99 and p.spawn_math.room(scene,hit[0])
    roof=floor.copy();roof[:,:,1]=1.5
    assert p.spawn_math.room(p.spawn_math.TriangleScene([floor,roof]),np.zeros(3))is None
    assert scene.floor(6,0,3,5)is None
    assert p.leaf_keys({'a':dict(alpha=True,c='tex/s/demo-gamemap-batched/s7_c.png'),'b':dict(alpha=True,fx=True,c='leaves.png'),'c':dict(c='leaves.png')})=={'a'}
    m=p.local(dict(translation=[2,3,4],scale=[2,1,-1]));assert np.linalg.det(m[:3,:3])<0 and np.allclose(m[:3,3],[2,3,4])
    print('Focused source weld/marker/collision-only/leaf/real-support/headroom/refusal/transform cases PASS',flush=True)

def check(slug,staged=False):
    dest=(p.STAGE if staged else p.LIVE)/slug;m=p.load(dest/'map.json');r=p.load(dest/'provenance.json');proof=p.load(dest/'spawn-proof.json')
    assert slug not in p.EXCLUDED and m['mapId']==r['mapId']=='archive:'+slug
    assert r['sourceContentId']==p.CONTENT_ID and p.sha(dest/'geometry.bytes')==r['geometrySha256']
    stream=(dest/'geometry.bytes').open('rb');raw=mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ)
    magic,count=struct.unpack_from('<ii',raw);assert magic==0x52323357 and count==m['chunks']<=4096
    cursor=8;render=collision=0;parts=[];render_attributes=hashlib.sha256();collision_attributes=hashlib.sha256()
    groups=p.load(dest/'source-chunks.json');assert len(groups)==count
    for ci in range(count):
        mid,coll,vis,nv,ni=struct.unpack_from('<iiiii',raw,cursor);cursor+=20
        assert 0<=mid<len(m['materials'])and coll in (0,1)and vis in(0,1)and 0<nv<=60000 and 3<=ni<=3000000 and ni%3==0
        v=np.frombuffer(raw,dtype='<f4',count=nv*12,offset=cursor).reshape(nv,12);cursor+=nv*48
        idx=np.frombuffer(raw,dtype='<i4',count=ni,offset=cursor);cursor+=ni*4
        assert idx.min()>=0 and idx.max()<nv and np.isfinite(v).all()
        assert np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=1e-4)
        role,key,*_=groups[ci]['sourceBatch']
        assert groups[ci]['selectedTriangles']==ni//3 and groups[ci]['collision']==bool(coll)and groups[ci]['render']==bool(vis)
        assert role not in('killzone','trigger','playfield')
        if role=='false':assert not coll and vis
        if role=='boundary':assert coll and not vis
        if key in r['leafCollisionExcluded']:assert not coll
        if coll:collision+=ni//3;parts.append(v[idx,:3].reshape(-1,3,3).copy());collision_attributes.update(v[idx].tobytes())
        if vis:render+=ni//3;render_attributes.update(v[idx].tobytes())
    assert cursor==len(raw) and render==r['renderTriangles']and collision==r['collisionTriangles']
    if 'sourceRetainedRenderAttributesSha256'in r:
        assert render_attributes.hexdigest()==r['sourceRetainedRenderAttributesSha256']
        assert collision_attributes.hexdigest()==r['sourceRetainedCollisionAttributesSha256']
    roles=r['sourceRoles'];visual_removed=sum(v for k,v in r['sourceMarkerTrianglesRemoved'].items()if not k.endswith('CollisionOnly'))
    assert render==roles.get('world',0)+roles.get('roof',0)+roles.get('false',0)-visual_removed
    assert r['sourceSolidTriangles']==roles.get('world',0)+roles.get('roof',0)
    scene=p.spawn_math.TriangleScene(parts)
    for field in ('game','warmup'):
        position=np.array(proof[field]['spawn']);floor=position-[0,.12,0]
        hit=scene.floor(position[0],position[2],floor[1]+.6,1.2)
        assert hit is not None and abs(hit[0][1]-floor[1])<.001 and p.spawn_math.room(scene,floor),field+' actual support/headroom'
        assert np.allclose(position,m['gameSpawn'if field=='game'else'warmupSpawn'])
    for name,row in r['textureInputs'].items():assert p.sha(dest/name)==row['sha256']==p.sha(row['source'])
    preview=r['preview']
    if preview['file']:assert p.sha(dest/preview['file'])==preview['sha256']==p.sha(preview['source'])
    assert proof['spawnFloorsAdded']is False
    print(slug+': native binary/attributes/roles/markers/textures/genuine-preview/spawn support PASS ('+str(render)+' render, '+str(collision)+' collision triangles)',flush=True)

def main():
    a=argparse.ArgumentParser();a.add_argument('--slug');a.add_argument('--staged',action='store_true');a.add_argument('--coverage-only',action='store_true');args=a.parse_args()
    if args.slug:check(args.slug,args.staged);return
    p.bindings();focused();delta=p.load(p.HERE/'catalog-delta.json')
    if args.coverage_only:
        registry=set(p.load(p.FROZEN/'assets/data/archive_semantics_r9.json')['maps'])
        library={r['id']for r in p.load(p.LIB/'catalog.json')['maps']}
        expected=registry-{'archive:'+s for s in p.EXCLUDED}
        assert registry==library and len(registry)==105 and len(expected)==102
        assert {r['id']for r in delta['maps']}==expected and not delta['refused']
        assert all((p.LIVE/r['slug']/'map.json').is_file()for r in delta['maps'])
        print('Frozen105 archive registry = original library; exact102 additional payload coverage PASS. Existing3 archives excluded/preserved; procedural cyber_bird separate.')
        return
    for row in delta['maps']:subprocess.run([sys.executable,str(Path(__file__).resolve()),'--slug',row['slug']],check=True)
    print(str(len(delta['maps']))+' published maps PASS; '+str(len(delta['refused']))+' precise refusals retained.',flush=True)
if __name__=='__main__':main()
