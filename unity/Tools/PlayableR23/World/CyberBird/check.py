"""Finite source/geometry/material/support checks. No engine or device calls."""
import hashlib,importlib.util,json,math,mmap,struct,sys
from pathlib import Path
import numpy as np
import prepare as p
from source_geometry import Basis,Vec,Geometry,gd_div,gd_mod

def shader_cases():
    def cpu(v,base,rough,floor):
        x,y,z=v;grit=math.sin(x*3+z*.7)*math.sin(z*4+y*.2)*.5+.5
        seams=.1 if (y*.25)%1>=.975 else 0;streak=math.sin(x*19+z*17)*math.sin(y*.2)*.028
        c=np.array(base)*(0.90+grit*.16-seams+streak)
        if floor:
            tile=np.abs(np.mod(np.array([x,z])*.4,1)-.5);t=np.clip((max(tile)-.47)/.02,0,1);c*=1-(t*t*(3-2*t))*.3
        return c,np.clip(rough+grit*.09,.12,1)
    # Independent vector-form direct transcription of the source fragment.
    positions=np.array([[0,0,2400],[2.5,0,2402.5],[-31,3.9,2700],[.3,3.999,2765],[19,4.2,2301],[-397,24.1,2001]],float)
    grit=np.sin(positions[:,0]*3+positions[:,2]*.7)*np.sin(positions[:,2]*4+positions[:,1]*.2)*.5+.5
    for floor in (0,1):
        for rough in (.24,.53,.77):
            base=np.array([.1,.2,.3]);c=base*(.90+grit[:,None]*.16-(np.mod(positions[:,1]*.25,1)>=.975)[:,None]*.10+(np.sin(positions[:,0]*19+positions[:,2]*17)*np.sin(positions[:,1]*.2)*.028)[:,None])
            if floor:
                tile=abs(np.mod(positions[:,[0,2]]*.4,1)-.5);t=np.clip((tile.max(1)-.47)/.02,0,1);c*=1-(t*t*(3-2*t))[:,None]*.3
            for i,v in enumerate(positions):
                actual,r=cpu(v,base,rough,floor);assert np.allclose(actual,c[i],atol=1e-13)and abs(r-np.clip(rough+grit[i]*.09,.12,1))<1e-13
    shader=(p.HERE/'R23CyberWeathered.shader').read_text(encoding='utf-8')
    for term in ['p.x*3.0+p.z*0.7','p.z*4.0+p.y*0.2','step(0.975,frac(p.y*0.25))','p.x*19.0+p.z*17.0','p.xz*0.4','smoothstep(0.47,0.49','_Roughness+grit*0.09,0.12,1.0','SRGBToLinear(_Tint.rgb)','LinearToSRGB(sourceAlbedo)']:
        assert term in shader,term
    assert gd_div(5,2)==2 and gd_div(5.,2)==2.5 and gd_mod('%02d / %02d',[3,4])=='03 / 04'
    basis=Basis.looking_at(Vec(0,0,-1),Vec.UP);assert np.allclose(basis.a,np.eye(3))
    assert np.allclose((Basis(Vec.UP,math.pi/2)*Vec(0,0,1)).a,Vec(1,0,0).a,atol=1e-6)
    print('36 source weathered-math cases + integer division/format/basis seam checks PASS',flush=True)

def check(folder=p.OUT):
    p.bindings();shader_cases();folder=Path(folder);m=p.read(folder/'map.json');r=p.read(folder/'provenance.json');city=p.read(folder/'city.json');chunks=p.read(folder/'source-chunks.json')
    assert m['mapId']=='cyber_bird'and m['version']==2 and r['sourceContentId']==p.CONTENT_ID
    assert p.sha(folder/'geometry.bytes')==r['geometrySha256'] and p.sha(p.HERE/'source-boxes.npy')==r['sourceBoxReceiptsSha256']
    assert r['buildings']==144 and r['stairs']==r['storeys']==825 and r['bridges']==18 and r['boxes']==114367 and r['ramps']==1650
    assert len(city['buildings'])==144 and len(city['sourceFootRamps'])==1650 and len(city['sourceSigns'])==876
    assert {int(b['id'])for b in city['buildings']if b['id']<100}==set(range(64))
    receipts=np.load(p.HERE/'source-boxes.npy');assert receipts.shape==(114367,15)
    f=(folder/'geometry.bytes').open('rb');raw=mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ);magic,count=struct.unpack_from('<ii',raw);assert magic==0x52323357 and count==m['chunks']==len(chunks)<=4096
    cursor=8;render=collision=foot=0;world_parts=[];unit_positions={};material_counts=np.zeros(12,np.int64);canopy=False
    for ci,row in enumerate(chunks):
        mid,coll,vis,nv,ni=struct.unpack_from('<iiiii',raw,cursor);cursor+=20
        assert 0<=mid<len(m['materials'])and 0<nv<=60000 and ni>=3 and ni%3==0
        v=np.frombuffer(raw,'<f4',count=nv*18,offset=cursor).reshape(nv,18);cursor+=nv*72
        idx=np.frombuffer(raw,'<i4',count=ni,offset=cursor);cursor+=ni*4
        assert np.isfinite(v).all()and idx.min()>=0 and idx.max()<nv and np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=1e-5)
        layer=m['chunkSourceLayers'][ci];assert layer==row['sourceLayer']and m['chunkBackfaceCollision'][ci]is False
        assert m['chunkCastShadows'][ci]==row['castShadows'] and m['chunkVisibilityRanges'][ci]==row['visibilityRangeEnd']
        if layer==16:
            assert coll and not vis and not row['backface'];foot+=ni//3
        else:
            assert layer in (0,1)
            if coll:
                assert layer==1;world_parts.append(v[idx.reshape(-1,3)[:,[0,2,1]],:3].copy())
        if mid<12 and vis:
            assert nv%24==0 and ni%36==0;material_counts[mid]+=ni//36
            assert np.allclose(abs(v[:,12:15]),.5)
            t=v[idx,:3].reshape(-1,3,3);n=v[idx,3:6].reshape(-1,3,3).mean(1);dots=np.sum(np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0])*n,axis=1)
            assert (dots>0).all(),'Actual reflected Godot BoxMesh winding must face its outward native normal'
        if 'Visible_Flight_Canopy'in row['sourceChunks']:assert not row['castShadows'];canopy=True
        if vis:render+=ni//3
        if coll:collision+=ni//3
    assert cursor==len(raw)and render==r['counts']['renderTriangles']==114367*12+876*2
    assert collision==r['counts']['collisionTriangles']==r['solidBoxes']*12+1650*2 and foot==3300 and canopy
    assert np.array_equal(material_counts,np.bincount(receipts[:,0].astype(int),minlength=12))
    assert np.allclose(m['spawn'],[-9,.16,-2765])and np.allclose(city['originGodot'],[0,0,2400])
    spec=importlib.util.spec_from_file_location('cyber_floor_math',p.HERE.parent/'Classic/spawn.py');floor_math=importlib.util.module_from_spec(spec);spec.loader.exec_module(floor_math)
    scene=floor_math.TriangleScene(world_parts);hit=scene.floor(-9,-2765,2.16,5)
    assert hit is not None and abs(hit[0][1])<1e-6 and floor_math.room(scene,hit[0]),'Authored spawn actual ground and full standing capsule'
    # Independent oriented-box ray checks prove source facade omissions stay open.
    centers=receipts[:,3:6];basis=receipts[:,6:].reshape(-1,3,3);half=np.abs(basis).sum(2)*.5;solid=receipts[:,1]>0
    tested=0
    for window in city['windows'][::max(1,len(city['windows'])//32)]:
        point=np.array(window['at']);normal=np.array(window['normal']);a=point-normal*.35;b=point+normal*.35;lo=np.minimum(a,b);hi=np.maximum(a,b)
        candidates=np.flatnonzero(solid&((centers-half)<=hi+1e-5).all(1)&((centers+half)>=lo-1e-5).all(1))
        for i in candidates:
            inv=np.linalg.inv(basis[i]);aa=inv@(a-centers[i]);bb=inv@(b-centers[i]);delta=bb-aa;t0=0.;t1=1.;intersects=True
            for axis in range(3):
                if abs(delta[axis])<1e-12:
                    if abs(aa[axis])>.5+1e-6:intersects=False;break
                else:
                    enter=(-.5-aa[axis])/delta[axis];leave=(.5-aa[axis])/delta[axis];t0=max(t0,min(enter,leave));t1=min(t1,max(enter,leave))
            if intersects and t0<t1-1e-6:raise AssertionError('Source window omission closed by solid box '+str(i))
        tested+=1
    assert tested>=32
    assert p.sha(folder/'preview.png')==r['sourceInputs']['assets/maps1000/cyber_bird.png']
    assert p.sha(folder/'preview-top.png')==r['sourceInputs']['assets/maps1000/cyber_bird_top.png']
    for mat in m['materials']:
        if mat.get('color'):assert (folder/mat['color']).is_file()
    print('CyberBird actual-source census, v2 binary/attributes/roles/winding, '+str(tested)+' real open windows, authored floor/capsule, source previews PASS',flush=True)
if __name__=='__main__':check(Path(sys.argv[1])if len(sys.argv)>1 else p.OUT)
