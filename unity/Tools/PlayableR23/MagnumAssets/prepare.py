"""Original R23 Magnum art only. Unique payload names; no runtime/general assets edits."""
from pathlib import Path
import collections,hashlib,io,json,math,struct,uuid
import numpy as np
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
SRC=Path(r'C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';FOLDER=SRC/'assets/models/download_magnum'
S=np.diag([1.,1.,-1.]);TAU=math.tau
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def unit(v):
    a=np.asarray(v,float);return a/max(float(np.linalg.norm(a)),1e-20)
def rx(a):
    c,s=math.cos(a),math.sin(a);return np.array([[1,0,0],[0,c,-s],[0,s,c]])
def rz(a):
    c,s=math.cos(a),math.sin(a);return np.array([[c,-s,0],[s,c,0],[0,0,1]])
def material(hexcolor,metal,rough,texture='',double=False):
    color=[int(hexcolor[i:i+2],16)/255 for i in [0,2,4]]+[1]
    return dict(color=color,metallic=metal,roughness=rough,doubleSided=double,alphaMode='OPAQUE',baseColor=texture,normal='',metallicMap='',occlusion='')
def cylinder(radius,height,sides,rings=1):
    v=[];n=[];uv=[];indices=[]
    for j in range(rings+2):
        fraction=j/(rings+1);y=height*.5-height*fraction
        for i in range(sides+1):
            u=i/sides;x,z=(0.,1.) if i==sides else (math.sin(u*TAU),math.cos(u*TAU));v.append([x*radius,y,z*radius]);n.append([x,0,z]);uv.append([u,fraction*.5])
            if i>0 and j>0:
                prev=(j-1)*(sides+1);row=j*(sides+1);indices.extend([prev+i-1,prev+i,row+i-1,prev+i,row+i,row+i-1])
    for sign in [1,-1]:
        row=len(v);v.append([0,sign*height*.5,0]);n.append([0,sign,0]);uv.append([.25 if sign==1 else .75,.75])
        for i in range(sides+1):
            a=i/sides*TAU;x,z=(0.,1.) if i==sides else (math.sin(a),math.cos(a));v.append([x*radius,sign*height*.5,z*radius]);n.append([0,sign,0]);uv.append([(x+1)*.25+(0 if sign==1 else .5),.5+(z+1)*.25 if sign==1 else 1-(z+1)*.25])
            if i>0:indices.extend([row,len(v)-1,len(v)-2] if sign==1 else [row,len(v)-2,len(v)-1])
    return np.asarray(v),np.asarray(n),np.asarray(uv),np.asarray(indices,np.int32)
def torus(inner,outer,rings=24,segments=8):
    v=[];n=[];uv=[];indices=[];radius=(outer-inner)*.5
    for i in range(rings+1):
        a=i/rings*TAU;ni=np.array([0.,-1.]) if i==rings else np.array([-math.sin(a),-math.cos(a)])
        for j in range(segments+1):
            b=j/segments*TAU;nj=np.array([-1.,0.]) if j==segments else np.array([-math.cos(b),math.sin(b)]);k=nj*radius+[inner+radius,0]
            v.append([ni[0]*k[0],k[1],ni[1]*k[0]]);n.append([ni[0]*nj[0],nj[1],ni[1]*nj[0]]);uv.append([i/rings,j/segments])
            if i>0 and j>0:
                row=i*(segments+1);prev=(i-1)*(segments+1);indices.extend([row+j-1,prev+j,prev+j-1,row+j-1,row+j,prev+j])
    return np.asarray(v),np.asarray(n),np.asarray(uv),np.asarray(indices,np.int32)
def beveled(size,bevel):
    x,y,z=np.asarray(size)*.5;b=min(bevel,min(x,y)*.6);ring=[[-x+b,-y],[x-b,-y],[x,-y+b],[x,y-b],[x-b,y],[-x+b,y],[-x,y-b],[-x,-y+b]];v=[];n=[]
    for i in range(8):
        aa=ring[i];cc=ring[(i+1)%8];a=np.array([*aa,-z]);c=np.array([*cc,-z]);d=np.array([*cc,z]);e=np.array([*aa,z]);normal=unit([c[1]-a[1],a[0]-c[0],0])
        for p in [a,e,d,a,d,c]:v.append(p);n.append(normal)
        for p in [[0,0,-z],a,c]:v.append(p);n.append([0,0,-1])
        for p in [[0,0,z],d,e]:v.append(p);n.append([0,0,1])
    return np.asarray(v),np.asarray(n),np.zeros((len(v),2)),np.arange(len(v),dtype=np.int32)
def glb():
    raw=(FOLDER/'model.glb').read_bytes();length=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+length]);binary=raw[28+length:]
    def acc(index):
        a=g['accessors'][index];view=g['bufferViews'][a['bufferView']];dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);components={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        result=np.ndarray((a['count'],components),dtype=dt,buffer=binary,offset=view.get('byteOffset',0)+a.get('byteOffset',0),strides=(view.get('byteStride',dt.itemsize*components),dt.itemsize)).copy();return result/np.iinfo(dt).max if a.get('normalized') else result
    assert len(g['nodes'])==1 and g['nodes'][0]['name']=='Body';p=g['meshes'][0]['primitives'][0];a=p['attributes'];pos=acc(a['POSITION']);normal=acc(a['NORMAL']);uv=acc(a['TEXCOORD_0']);indices=acc(p['indices']).ravel() if 'indices'in p else np.arange(len(pos));image=g['images'][0];view=g['bufferViews'][image['bufferView']];png=binary[view.get('byteOffset',0):view.get('byteOffset',0)+view['byteLength']]
    return (pos,normal,uv,indices.astype(np.int32)),g['materials'][0],png
class Asset:
    def __init__(self,name,materials):self.name=name;self.materials=materials;self.parts=[];self.recipes=[]
    def add(self,name,mesh,mi,rotation=None,at=(0,0,0),fit=None,gltf=False,recipe=None):
        v,n,uv,indices=mesh;rotation=np.eye(3) if rotation is None else rotation;v=v@rotation.T+at;n=n@rotation.T
        if fit is not None:
            scale,offset=fit;v=v*scale+offset;n=n/scale
        n=n/np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-20);v=v@S;n=n@S;uv=uv.copy();uv[:,1]=1-uv[:,1]
        # Original glTF is CCW; authored Godot primitives are CW. Conversion
        # reflects Z once and compensates the original convention exactly once.
        indices=indices.reshape(-1,3)[:,[0,2,1]].ravel() if gltf else indices.ravel()
        vertices=np.column_stack((v,n,uv)).astype('<f4');assert np.isfinite(vertices).all();self.parts.append((name,vertices,indices.astype('<i4'),mi));self.recipes.append(dict(part=name,recipe=recipe,triangles=len(indices)//3,sourcePosition=list(at),sourceRotation=rotation.tolist(),sourceFit=None if fit is None else [np.asarray(x).tolist()for x in fit]))
    def write(self):
        with (OUT/(self.name+'.meshbin')).open('wb') as f:
            f.write(struct.pack('<ii',0x5232334d,len(self.parts)))
            for name,vertices,indices,mi in self.parts:f.write(struct.pack('<iii',len(vertices),len(indices),mi));f.write(vertices.tobytes());f.write(indices.tobytes())
        (OUT/(self.name+'.json')).write_text(json.dumps(dict(materials=self.materials,parts=[p[0]for p in self.parts]),indent=2))
        return dict(name=self.name,vertices=sum(len(v)for name,v,i,mi in self.parts),triangles=sum(len(i)//3 for name,v,i,mi in self.parts),surfaces=len(self.parts),parts=self.recipes)
def main():
    before={p.name:sha(p) for p in OUT.iterdir() if p.is_file() and not p.name.startswith('magnum')};body_mesh,body_source,png=glb();source_pbr=body_source['pbrMetallicRoughness'];body_mat=material('ffffff',source_pbr['metallicFactor'],source_pbr['roughnessFactor'],'magnum-body-baseColor.png',body_source.get('doubleSided',False));(OUT/'magnum-body-baseColor.png').write_bytes(png)
    image=Image.open(io.BytesIO(png));crop=image.crop((5,8,271,248));crop.save(OUT/'magnum-drum-finish.png')
    body=Asset('magnum',[body_mat]);body.add('Body',body_mesh,0,gltf=True,recipe='Exact entire fitted original R23 Body GLB primitive;5382triangles')
    finish=dict(body_mat,baseColor='magnum-drum-finish.png');cm=[finish,material('161d20',.25,.7),material('8c9698',.86,.29),material('20272b',.36,.57),material('b79548',.8,.35),material('ad8545',.78,.38)];drum=Asset('magnum-cylinder',cm)
    om=[material('b79548',.8,.35),material('59646b',.85,.3),material('ad8545',.78,.38),material('20272b',.36,.57)];offer=Asset('magnum-offer',om)
    provenance=json.loads((FOLDER/'asset-provenance.json').read_text());center=np.array(provenance['cylinder_center']);scale=np.array([.66,.66,.60]);offset=center-np.array([0,.035,-.11])*scale;fit=(scale,offset);offer_fit=(np.array([.66,.66,1.]),np.zeros(3))
    drum.add('DrumBody',cylinder(.043,.090,21),0,rx(math.pi/2),(0,.035,-.11),fit,recipe='revolver._rod:21radial/rings1; fitted receiver crop finish')
    live_homes=[];offer_homes=[]
    for i in range(7):
        angle=TAU*i/7;p=np.array([math.cos(angle)*.027,.035+math.sin(angle)*.027,-.062]);live_homes.append(((p+[0,0,.0035])*scale+offset)@S)
        drum.add('DrumRecess'+str(i),cylinder(.011,.002,12),1,rx(math.pi/2),p+[0,0,.002],fit,recipe='revolver chamber recess')
        drum.add('LiveRound'+str(i),cylinder(.008,.007,12),4,rx(math.pi/2),p+[0,0,.0035],fit,recipe='revolver.cartridges live round visibility owner')
        drum.add('LiveRound'+str(i)+'.Primer',cylinder(.003,.0005,8),5,rx(math.pi/2),p+[0,0,.00725],fit,recipe='handgun_finish_063 nested primer, cartridge parent quaternion composed')
        q=np.array([math.cos(angle)*.027,math.sin(angle)*.027,0]);offer_homes.append(q*np.array([.66,.66,1.])@S)
        offer.add('OfferRound'+str(i)+'.Case',cylinder(.009,.045,12),0,rx(-math.pi/2),q+[0,0,-.0225],offer_fit,recipe='revolver offered case')
        offer.add('OfferRound'+str(i)+'.Tip',cylinder(.007,.015,12),1,rx(-math.pi/2),q+[0,0,-.0525],offer_fit,recipe='revolver offered tip; original59646b steel, not drum texture')
        offer.add('OfferRound'+str(i)+'.Rim',torus(.008,.010),2,rx(math.pi/2),q+[0,0,-.002],offer_fit,recipe='handgun_finish_063 offered case rim24x8')
        offer.add('OfferRound'+str(i)+'.TailRim',torus(.008,.0093),3,rx(math.pi/2),q+[0,0,-.037],offer_fit,recipe='handgun_finish_063 offered rear rim24x8')
        drum.add('DrumFlute'+str(i),beveled((.010,.0015,.057),.0004),3,rz(angle-math.pi*.5),(math.cos(angle)*.043,.035+math.sin(angle)*.043,-.110),fit,recipe='handgun_finish_063 original eight-sided beveled extrusion')
    for z in [-.153,-.066]:drum.add('DrumRim'+str(z),torus(.040,.0445),2,rx(math.pi/2),(0,.035,z),fit,recipe='handgun_finish_063 cylinder edge ring24x8')
    # Preserve functional round prefixes; combine only immobile surfaces sharing
    # both role and material (the same source SurfaceTool batching behavior).
    groups=collections.OrderedDict()
    for name,v,indices,mi in drum.parts:
        role='DrumRecesses' if name.startswith('DrumRecess') else 'DrumRims' if name.startswith('DrumRim') else 'DrumFlutes' if name.startswith('DrumFlute') else name
        groups.setdefault((role,mi),[]).append((v,indices))
    combined=[]
    for (name,mi),members in groups.items():
        vertices=[];indices=[];count=0
        for v,i in members:vertices.append(v);indices.append(i+count);count+=len(v)
        combined.append((name,np.concatenate(vertices),np.concatenate(indices),mi))
    drum.parts=combined;records=[asset.write()for asset in [body,drum,offer]]
    front_height=.104+(.065-.104)*(-.365+.025)/(-45.392+.025)
    metadata=dict(version=1,id='magnum',loadNames=['magnum','magnum-cylinder','magnum-offer'],coordinates='Unity local Y-up/+Zforward; source Z reflected once. All homes before overall modelScale; no .84 baked into vertices.',modelScale=.84,weaponGripOrigin=[0,0,0],trackedGripPitchUnityDegrees=90,renderedHandPhysicalOffset=[0,0,-.05715],renderedHandOffsetInsideScaledModel=[0,0,-.05715/.84],muzzle=[0,.065,.392],rearSight=[0,.104,.025],frontSight=[0,front_height,.365],zeroDistance=45,cylinderClosedRoot=[0,0,0],cylinderOpenRoot=[-.10,0,0],cylinderCenter=(center@S).tolist(),cylinderFitScale=scale.tolist(),cylinderFitSourceOffset=offset.tolist(),liveRoundHomes=[p.tolist()for p in live_homes],offerRestRoot=[-.18,.095,-.13],offerRoundHomes=[p.tolist()for p in offer_homes],offerFitScale=[.66,.66,1],offerHeldWorldScale=1,offerRestWorldScale=.84,mirrorReloadXForLeftDominance=True,loadTravel=.06,closeTravel=.05,reloadRadius=.18,capacity=7,bodyPart='Body',cylinderStaticPrefixes=['DrumBody','DrumRecesses','DrumRims','DrumFlutes'],livePrefixes=['LiveRound'+str(i)for i in range(7)],offerPrefixes=['OfferRound'+str(i)for i in range(7)],ejector=None,ejectorSource='revolver.eject_cases is intentionally pass; no separate moving ejector exists',notes=['Hide every mesh whose name begins LiveRoundN (includes .Primer).','Hide every OfferRoundN prefix by actual deficit/reserve.','Apply cylinderRoot translation to entire fitted cylinder subtree only.','Rest offer inherits model .84; held offer source global basis is unit scale.','Source hides authored extra sight/frame detail meshes; downloaded cosmetic sights remain in Body.'])
    (OUT/'magnum-fit.json').write_text(json.dumps(metadata,indent=2));(OUT/'magnum-ATTRIBUTION.txt').write_bytes((FOLDER/'ATTRIBUTION.txt').read_bytes())
    inputs=['assets/models/download_magnum/model.glb','assets/models/download_magnum/asset-provenance.json','assets/models/download_magnum/ATTRIBUTION.txt']+['scripts/'+s+'.gd'for s in ['downloaded_magnum_model','magnum_finish_094','revolver','handgun_finish_063','weapon','weapon_finish','model_scale_084','playtest_fit_088']]
    outputs={p.name:sha(p)for p in OUT.iterdir()if p.is_file()and p.name.startswith('magnum')and p.suffix!='.meta' and p.name!='magnum-provenance.json'}
    report=dict(baseline='Actual ORIGINAL Opus R23, not mutable R24',inputs={p:sha(SRC/p)for p in inputs},assets=records,bodyTextureEmbeddedSha256=hashlib.sha256(png).hexdigest(),drumCrop=dict(source='embedded Body baseColor',rect=[5,8,266,240],method='Lossless exact source pixel crop; no resampling'),sourceBodyBoundsMin=provenance['bounds_min'],sourceBodyBoundsMax=provenance['bounds_max'],outputs=outputs,officialPrimitiveReference=dict(url='https://github.com/godotengine/godot/blob/master/scene/resources/3d/primitive_meshes.cpp',sha256=sha(HERE/'GodotPrimitives.cpp')),limits='CPU recipe conversion/geometry validation only. No engine, GPU, Blender, device or runtime integration. Original downloaded six-shot drum/loader remain source-omitted; functional seven-chamber geometry retained. No separate source ejector. Overall .84 model scale left for owner.')
    (OUT/'magnum-provenance.json').write_text(json.dumps(report,indent=2))
    for p in OUT.iterdir():
        if p.name.startswith('magnum')and p.suffix!='.meta':
            meta=p.with_name(p.name+'.meta')
            if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
    assert all(sha(OUT/name)==digest for name,digest in before.items()),'Existing hand/gun/general payload changed'
    (HERE/'preserved-existing.json').write_text(json.dumps(before,indent=2));print(json.dumps([{k:r[k]for k in ['name','vertices','triangles','surfaces']}for r in records],indent=2));print('Unique magnum payloads only; existing files byte-identical:',len(before))
if __name__=='__main__':main()
