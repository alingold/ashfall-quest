"""Original R23 full M1014 Body/Bolt plus its actual six-shell pack/arrow. CPU only."""
from pathlib import Path
import collections,hashlib,importlib.util,io,json,math,shutil,struct,uuid
import numpy as np
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];SRC=Path(r'C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');FOLDER=SRC/'assets/models/download_m1014';OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
spec=importlib.util.spec_from_file_location('source_magnum_primitives',HERE.parent/'MagnumAssets/prepare.py');primitives=importlib.util.module_from_spec(spec);spec.loader.exec_module(primitives)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def main():
    before={p.name:sha(p)for p in OUT.iterdir()if p.is_file()and not p.name.startswith('m1014')};primitives.OUT=OUT
    raw=(FOLDER/'model.glb').read_bytes();length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);blob=raw[28+length:];assert sha(FOLDER/'model.glb')==json.loads((FOLDER/'asset-provenance.json').read_text())['output_sha256']
    def acc(index):
        a=g['accessors'][index];v=g['bufferViews'][a['bufferView']];dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);w={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        x=np.ndarray((a['count'],w),dt,buffer=blob,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',w*dt.itemsize),dt.itemsize)).copy();return x/np.iinfo(dt).max if a.get('normalized')else x
    images=[]
    for i,image in enumerate(g['images']):
        v=g['bufferViews'][image['bufferView']];data=blob[v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']];p=OUT/('m1014-image-'+str(i)+'.png');p.write_bytes(data);images.append(dict(file=p.name,sha256=sha(p),sourceBufferView=image['bufferView']))
    def image(index):return images[g['textures'][index]['source']]['file']
    materials=[]
    for i,m in enumerate(g['materials']):
        p=m.get('pbrMetallicRoughness',{});row=dict(color=p.get('baseColorFactor',[1,1,1,1]),metallic=p.get('metallicFactor',1),roughness=p.get('roughnessFactor',1),doubleSided=m.get('doubleSided',False),alphaMode=m.get('alphaMode','OPAQUE'),baseColor='',normal='',metallicMap='',occlusion='')
        if p.get('baseColorTexture'):row['baseColor']=image(p['baseColorTexture']['index'])
        if m.get('normalTexture'):row['normal']=image(m['normalTexture']['index'])
        if p.get('metallicRoughnessTexture'):
            original=Image.open(OUT/image(p['metallicRoughnessTexture']['index'])).convert('RGBA');pixels=np.asarray(original);unity=np.zeros_like(pixels);unity[:,:,0]=pixels[:,:,2];unity[:,:,3]=255-pixels[:,:,1];dest=OUT/('m1014-metallic-'+str(i)+'.png');Image.fromarray(unity).save(dest);row['metallicMap']=dest.name
        if m.get('occlusionTexture'):row['occlusion']=image(m['occlusionTexture']['index'])
        materials.append(row)
    assets=[]
    for node in g['nodes']:
        assert node['name']in ['Body','Bolt']and set(node)=={'name','mesh'}
        used=list(dict.fromkeys(p.get('material',0)for p in g['meshes'][node['mesh']]['primitives']))
        asset=primitives.Asset('m1014'if node['name']=='Body'else'm1014-bolt',[materials[i]for i in used])
        for pi,p in enumerate(g['meshes'][node['mesh']]['primitives']):
            a=p['attributes'];pos=acc(a['POSITION']);n=acc(a['NORMAL']);uv=acc(a['TEXCOORD_0']);idx=acc(p['indices']).ravel()if'indices'in p else np.arange(len(pos));asset.add(node['name']+'.'+str(pi),(pos,n,uv,idx.astype(np.int32)),used.index(p.get('material',0)),gltf=True,recipe='Entire actual original fitted M1014 '+node['name']+' GLB surface; node identity, bolt home applied by runtime only')
        assets.append(asset.write())
    brass=primitives.material('b79548',.8,.35);polymer=primitives.material('2b302e',.03,.78);edge=primitives.material('94958d',.82,.26);dark=primitives.material('172025',.48,.52);pack=primitives.Asset('m1014-pack',[brass,polymer,edge,dark])
    for i in range(6):
        p=np.array([0,(i-2.5)*.035,0])
        for name,radius,height,z,sides,mat,angle in [('Core',.014,.06,-.03,10,0,-math.pi/2),('Hull',.0142,.047,-.0315,12,1,-math.pi/2),('Base',.0148,.009,-.0045,12,2,-math.pi/2),('Primer',.004,.0005,.00075,10,3,math.pi/2)]:
            pack.add('Shell'+str(i)+'.'+name,primitives.cylinder(radius,height,sides),mat,primitives.rx(angle),p+[0,0,z],recipe='Exact auto_shotgun six-pack + heavy_model_finish.m1014 manufacture')
    def batch_static(asset):
        groups=collections.OrderedDict()
        for name,v,indices,mi in asset.parts:groups.setdefault(mi,[]).append((v,indices))
        combined=[]
        for mi,parts in groups.items():
            vs=[];idx=[];offset=0
            for v,i in parts:vs.append(v);idx.append(i+offset);offset+=len(v)
            combined.append(('SourceStaticMaterial'+str(mi),np.concatenate(vs),np.concatenate(idx),mi))
        asset.parts=combined
    batch_static(pack);assets.append(pack.write())
    def box(size):
        s=np.array(size)*.5;v=[];n=[]
        for axis in range(3):
            for sign in [-1,1]:
                normal=np.eye(3)[axis]*sign;u=np.eye(3)[(axis+1)%3];w=np.eye(3)[(axis+2)%3];center=normal*s[axis];corners=[center+u*s[(axis+1)%3]*a+w*s[(axis+2)%3]*b for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]]
                order=[0,2,1,0,3,2]if sign==1 else[0,1,2,0,2,3]
                for j in order:v.append(corners[j]);n.append(normal)
        return np.array(v),np.array(n),np.zeros((36,2)),np.arange(36,dtype=np.int32)
    arrow=primitives.Asset('m1014-arrow',[dict(color=[.05,.72,.75,1],metallic=.2,roughness=.4,doubleSided=False,alphaMode='OPAQUE',baseColor='',normal='',metallicMap='',occlusion='')]);arrow.add('Stem',box([.009,.09,.006]),0,at=[0,.045,0],recipe='auto_shotgun UpThenBackArrow stem')
    for side in [-1,1]:arrow.add('ArrowArm'+str(side),box([.009,.035,.006]),0,primitives.rz(side*.65),[side*.01,.077,0],recipe='Source two rotated box arrow arms')
    batch_static(arrow);assets.append(arrow.write());assert sum(a['triangles']for a in assets[:2])==79142
    fit=dict(id='m1014',modelScale=1,loadNames=['m1014','m1014-bolt','m1014-pack','m1014-arrow'],weaponGripOrigin=[0,0,0],muzzle=[0,.045,.77],handle=[.06,.045,.10],offer=[0,-.25,.14],socket=[0,-.10,.14],foregrip=[0,-.025,.36],renderedHandPhysicalOffset=[0,.0381,-.0381],mirrorHandleXAndBoltScaleForLeft=True,handlePullDirection=[0,0,-1],handlePullThreshold=.085,loadUpThreshold=.10,shotVisualCarrierSeconds=.095,shotVisualCarrierTravel=.038,flashSeconds=.055,sourceActualBoltContact=json.loads((FOLDER/'asset-provenance.json').read_text())['actual_bolt_contact'],notes=['Body includes actual authored iron sights; no invented optic.','Do not apply source raw fit scale.698799: it is already baked in original model.glb.','Apply handle home exactly once to separate local Bolt geometry.','Shell pack always draws all6source shells when offered, including partial finite reserves.'])
    (OUT/'m1014-fit.json').write_text(json.dumps(fit,indent=2));shutil.copyfile(FOLDER/'ATTRIBUTION.txt',OUT/'m1014-ATTRIBUTION.txt')
    routing=json.loads((SRC/'assets/audio/reference0992/manifest.json').read_text());cues=[]
    for action in ['shot','mag_eject','pouch','mag_insert','bolt','dry']:
        paths=routing['weapons'].get('m1014',{}).get(action,[])or routing['generic'].get(action,[])or['res://assets/audio/sfx/'+action+'_%02d.wav'%i for i in range(2)]
        files=[]
        for i,path in enumerate(paths):
            src=SRC/path.removeprefix('res://');dest='m1014-audio-'+action+'-'+str(i)+'.wav';shutil.copyfile(src,OUT/dest);files.append(dest)
        cues.append(dict(weapon='m1014',action=action,files=files,sourceFiles=paths))
    (OUT/'m1014-audio-cues.json').write_text(json.dumps(dict(cues=cues),indent=2))
    inputs=['assets/models/download_m1014/model.glb','assets/models/download_m1014/asset-provenance.json','assets/models/download_m1014/ATTRIBUTION.txt','scripts/auto_shotgun.gd','scripts/downloaded_m1014_model.gd','scripts/heavy_model_finish.gd','scripts/weapon.gd','scripts/model_scale_084.gd','scripts/playtest_fit_088.gd','assets/data/one_star_050.json','assets/data/one_star0991.json','assets/audio/reference0992/manifest.json']
    report=dict(sourceRoot=str(SRC),inputs={p:sha(SRC/p)for p in inputs},assets=assets,embeddedImages=images,outputs={p.name:sha(p)for p in OUT.iterdir()if p.is_file()and p.name.startswith('m1014')and p.suffix!='.meta'and p.name!='m1014-provenance.json'},engineRun=False,deviceRun=False,limits='CPU exact geometry/parts conversion; source textures copied, metallicRoughness repacked into Unity R-metal/A-smoothness. No shader/GPU/render acceptance. Current source six-pack mechanics supersede stale auto_shotgun_042 tube tests.')
    (OUT/'m1014-provenance.json').write_text(json.dumps(report,indent=2))
    for p in OUT.iterdir():
        if p.name.startswith('m1014')and p.suffix!='.meta':
            meta=Path(str(p)+'.meta')
            if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-original-m1014:'+p.name).hex+'\n')
    # Shared audio.json may be updated concurrently by its assigned owner; do not write it.
    protected={k:v for k,v in before.items()if k!='audio.json'};assert all(sha(OUT/k)==v for k,v in protected.items())
    (HERE/'preserved-existing.json').write_text(json.dumps(protected,indent=2));print(json.dumps([dict(name=a['name'],triangles=a['triangles'],surfaces=a['surfaces'])for a in assets],indent=2));print('Source M1014 assets/audio fragment ready; shared audio.json untouched.')
if __name__=='__main__':main()
