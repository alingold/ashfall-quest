"""Read back both real map payloads and validate actual collision-supported entry sites."""
from pathlib import Path
import hashlib,json,struct
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[4]
TOOLS=Path(__file__).resolve().parent
BASE=ROOT/'unity/Assets/StreamingAssets/PlayableR23'
reports=[]
for folder,expected in [('World',452983),('Maps/alfa-aglae-souza',1081459)]:
    path=BASE/folder;m=json.loads((path/'map.json').read_text());render=collision=vertices=0;triangles=[]
    with (path/'geometry.bytes').open('rb') as f:
        magic,count=struct.unpack('<ii',f.read(8));assert magic==0x52323357 and count==m['chunks']
        for _ in range(count):
            mid,solid,visible,nv,ni=struct.unpack('<iiiii',f.read(20));assert 0<=mid<len(m['materials']) and ni%3==0 and nv<=60000
            v=np.frombuffer(f.read(nv*48),dtype='<f4').reshape(nv,12);indices=np.frombuffer(f.read(ni*4),dtype='<i4')
            assert np.isfinite(v).all() and indices.min()>=0 and indices.max()<nv
            assert np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=1e-4),(folder,_,m['materials'][mid]['name'],float(np.linalg.norm(v[:,3:6],axis=1).min()))
            if folder!='World' and m['materials'][mid]['name']=='s4':assert solid==0,'source leaf collision must be excluded'
            vertices+=nv;render+=ni//3*visible;collision+=ni//3*solid
            if solid:triangles.append(v[indices,:3].reshape(-1,3,3).astype('float64'))
        assert f.read()==b''
    assert render==expected
    textures=set()
    for material in m['materials']:
        for channel in ['color','normal']:
            if material[channel]:
                textures.add(material[channel])
                with Image.open(path/material[channel]) as image:image.verify()
    alltri=np.concatenate(triangles)
    sites={}
    for field in ['spawn','warmupSpawn','gameSpawn','recordedSpawn']:
        if field not in m:continue
        site=np.array(m[field]);floor_values=[]
        for x,z in [(0,0),(.3,0),(-.3,0),(0,.3),(0,-.3)]:
            p=site+[x,0,z];a,b,c=alltri[:,0],alltri[:,1],alltri[:,2];v0=b[:,[0,2]]-a[:,[0,2]];v1=c[:,[0,2]]-a[:,[0,2]];q=p[[0,2]]-a[:,[0,2]]
            den=v0[:,0]*v1[:,1]-v1[:,0]*v0[:,1];valid=abs(den)>1e-9;safe=np.where(valid,den,1)
            u=(q[:,0]*v1[:,1]-v1[:,0]*q[:,1])/safe;v=(v0[:,0]*q[:,1]-q[:,0]*v0[:,1])/safe;h=a[:,1]+u*(b[:,1]-a[:,1])+v*(c[:,1]-a[:,1])
            valid&=(u>=-1e-6)&(v>=-1e-6)&(u+v<=1.000001)&(h<=p[1]+.6)&(h>=p[1]-1)
            assert valid.any(),(folder,field,p)
            floor_values.append(float(h[valid].max()))
        assert max(floor_values)-min(floor_values)<.3
        center=site+[0,.975,0];half=np.array([.25,.975,.25]);t=alltri-center;t=t[(t.min(axis=1)<=half).all(axis=1)&(t.max(axis=1)>=-half).all(axis=1)]
        edges=[t[:,1]-t[:,0],t[:,2]-t[:,1],t[:,0]-t[:,2]];axes=[np.broadcast_to(a,(len(t),3)) for a in np.eye(3)]+[np.cross(edges[0],edges[1])]+[np.cross(edge,a) for edge in edges for a in np.eye(3)]
        overlap=np.ones(len(t),bool)
        for axis in axes:
            proj=(t*axis[:,None,:]).sum(axis=2);radius=(abs(axis)*half).sum(axis=1);overlap&=(proj.min(axis=1)<=radius+1e-7)&(proj.max(axis=1)>=-radius-1e-7)
        assert not overlap.any(),(folder,field,'standing volume intersects')
        sites[field]={'position':site.tolist(),'floor':floor_values,'standingVolumeIntersections':0}
    reports.append({'map':m['mapId'],'chunks':count,'vertices':vertices,'renderTriangles':render,'collisionTrianglesBeforeBackfaces':collision,'textures':len(textures),'entrySites':sites,'completePayload':True,'finiteUnitNormals':True})
preserved=json.loads((TOOLS/'paintball-preserved.json').read_text())
for name,digest in preserved.items():assert hashlib.sha256((BASE/'World'/name).read_bytes()).hexdigest()==digest,name+' changed'
alpha=json.loads((ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/World/alfa-aglae-souza-data-check.json').read_text())
for name,source in alpha['textureInputs'].items():
    assert hashlib.sha256(Path(source['source']).read_bytes()).hexdigest()==source['sha256']
    assert hashlib.sha256((BASE/'Maps/alfa-aglae-souza'/name).read_bytes()).hexdigest()==source['sha256']
result={'maps':reports,'paintballFilesByteIdentical':len(preserved),'alphaOriginalOverrideTextureFilesByteIdentical':len(alpha['textureInputs']),'scope':'CPU payload, source-file identity, floor and standing-volume geometry checks only. No engine/renderer/physics cook/device.'}
(ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/World/map-selection-payload-check.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
