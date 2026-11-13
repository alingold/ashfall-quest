"""Read back the published range payload and compare it to the actual source recipe."""
import hashlib,json,math,struct
import numpy as np
from prepare import OUTPUT,HERE,MAP_SCRIPT,ART_SCRIPT,MAGIC,source_recipe

def main():
    manifest=json.loads((OUTPUT/'map.json').read_text(encoding='utf-8'))
    specs,origin,spawn_local,bevel,finishes=source_recipe()
    assert manifest['version']==1 and manifest['mapId']=='range' and manifest['title']=='SANDBOX RANGE'
    assert origin==[0,0,600] and spawn_local==[0,.2,8] and bevel==.01
    assert len(specs)==len(manifest['boxes'])==manifest['chunks']==4
    assert manifest['spawn']==[0,.2,-608]
    assert np.allclose(manifest['boundsMin'],[-45.15,-.4,-650],atol=1e-7,rtol=0)
    assert np.allclose(manifest['boundsMax'],[45.15,16,-550],atol=1e-7,rtol=0)
    for source,entry in zip((MAP_SCRIPT,ART_SCRIPT),manifest['source']['scripts']):
        assert entry['sha256']==hashlib.sha256(source.read_bytes()).hexdigest()
    assert len(manifest['materials'])==2 and manifest['materials']==finishes
    assert [(m['sourceTintHex'],m['metallic'],m['roughness']) for m in finishes]==[('48545b',.35,.65),('26343a',.4,.62)]
    count_vertices=count_triangles=0;all_vertices=[];floor_triangles=None;max_normal_error=0;worst_alignment=-math.inf
    with (OUTPUT/'geometry.bytes').open('rb') as inp:
        magic,count=struct.unpack('<ii',inp.read(8));assert magic==MAGIC and count==4
        for chunk in range(count):
            mid,collision,visible,nv,ni=struct.unpack('<iiiii',inp.read(20))
            assert mid==(0 if chunk==0 else 1) and collision==0 and visible==1
            assert 0<nv<=324 and ni==324 and ni%3==0
            vertices=np.frombuffer(inp.read(nv*48),dtype='<f4').reshape(nv,12).astype(float)
            indices=np.frombuffer(inp.read(ni*4),dtype='<i4').reshape(-1,3)
            assert np.isfinite(vertices).all() and indices.min()>=0 and indices.max()<nv
            assert np.all(vertices[:,6:8]==0),'No UVs are authored by source rounded_box'
            assert np.allclose(vertices[:,8:12],finishes[mid]['sourceTintRgba'],atol=3e-8,rtol=0),'Source raw RGB tint must not be linearized or painted'
            normals=vertices[:,3:6];error=np.abs(np.linalg.norm(normals,axis=1)-1)
            assert error.max()<2e-6 and (np.linalg.norm(normals,axis=1)>.999).all()
            max_normal_error=max(max_normal_error,float(error.max()))
            triangles=vertices[indices,:3];face=np.cross(triangles[:,1]-triangles[:,0],triangles[:,2]-triangles[:,0])
            area=np.linalg.norm(face,axis=1);assert (area>1e-9).all(),'Degenerate geometry'
            smooth=normals[indices].sum(axis=1);smooth/=np.linalg.norm(smooth,axis=1)[:,None]
            agreement=np.sum(face/area[:,None]*smooth,axis=1)
            assert (agreement<-.5).all(),'Expected Unity-clockwise winding against outward source normals after a single reflection/swap'
            worst_alignment=max(worst_alignment,float(agreement.max()))
            center=np.asarray(manifest['boxes'][chunk]['center']);size=np.asarray(manifest['boxes'][chunk]['size'])
            expected_center=np.asarray(origin)+specs[chunk]['center'];expected_center[2]*=-1
            assert np.array_equal(center,expected_center) and np.array_equal(size,specs[chunk]['size'])
            assert np.allclose(vertices[:,:3].min(axis=0),center-size/2,atol=5e-5,rtol=0)
            assert np.allclose(vertices[:,:3].max(axis=0),center+size/2,atol=5e-5,rtol=0)
            if chunk==0:floor_triangles=triangles
            all_vertices.append(vertices[:,:3]);count_vertices+=nv;count_triangles+=ni//3
        assert inp.read()==b'','Complete v1 payload consumed'
    all_vertices=np.concatenate(all_vertices)
    assert np.allclose(all_vertices.min(axis=0),manifest['boundsMin'],atol=5e-5,rtol=0)
    assert np.allclose(all_vertices.max(axis=0),manifest['boundsMax'],atol=5e-5,rtol=0)
    # Native descriptor support, a 0.5 m footprint, and exact rendered floor under spawn.
    spawn=np.array(manifest['spawn']);support=[]
    for dx,dz in ((0,0),(.25,0),(-.25,0),(0,.25),(0,-.25)):
        point=spawn+[dx,0,dz];top=[]
        for box in manifest['boxes']:
            center=np.array(box['center']);half=np.array(box['size'])/2
            if abs(point[0]-center[0])<=half[0] and abs(point[2]-center[2])<=half[2] and center[1]+half[1]<point[1]:top.append(center[1]+half[1])
        assert top and max(top)==0;support.append(max(top))
        for box in manifest['boxes'][1:]:
            lo=np.asarray(box['center'])-np.asarray(box['size'])/2;hi=np.asarray(box['center'])+np.asarray(box['size'])/2
            assert not (np.minimum(point+[.25,1.95,.25],hi)>np.maximum(point+[-.25,0,-.25],lo)).all(),'Standing volume intersects source walls'
    # Independently locate the central flat top triangles containing spawn X/Z.
    hits=[]
    for tri in floor_triangles:
        if not np.allclose(tri[:,1],0,atol=1e-6,rtol=0):continue
        a,b,c=tri[:,[0,2]];p=spawn[[0,2]]
        def cross(u,v):return float(u[0]*v[1]-u[1]*v[0])
        signs=[cross(b-a,p-a),cross(c-b,p-b),cross(a-c,p-c)]
        if min(signs)>=-1e-6 or max(signs)<=1e-6:hits.append(0)
    assert hits,'Source rendered floor absent at spawn'
    report=dict(mapId='range',chunks=count,vertices=count_vertices,renderTriangles=count_triangles,
                collisionMeshTriangles=0,nativeBoxDescriptors=4,sourceBoxesMatch=True,sourceRoundedBevel=.01,
                spawn=manifest['spawn'],floorSupportHeights=support,renderedFloorAtSpawn=True,standingWallsClear=True,
                boundsMin=manifest['boundsMin'],boundsMax=manifest['boundsMax'],unitNormalMaxError=max_normal_error,
                worstClockwiseAlignment=worst_alignment,uvAuthored=False,rawSourceTintRetained=True,
                sourceInputs=manifest['source']['scripts'],geometryBytes=(OUTPUT/'geometry.bytes').stat().st_size,
                geometrySha256=hashlib.sha256((OUTPUT/'geometry.bytes').read_bytes()).hexdigest(),
                limits='CPU recipe/payload validation only. No Godot/Unity export, engine, render or device execution. Valley environment is inherited in source and is not ported here.')
    (HERE/'checks.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
