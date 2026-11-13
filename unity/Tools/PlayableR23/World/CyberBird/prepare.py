"""Exact frozen R6 Cyber Bird source-math -> bounded native v2 payload; no engine."""
from pathlib import Path
import argparse,collections,hashlib,importlib.util,json,math,mmap,os,shutil,struct,sys,time,uuid
os.environ['OPENBLAS_NUM_THREADS']='1';os.environ['OMP_NUM_THREADS']='1'
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from source_geometry import Geometry,serial,rgba,Basis,Vec
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
OUT=Path('D:/ashfall-cyber-bird-staging-r6')
LIVE=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/cyber_bird'
CAPTURE=ROOT/'unity/Tools/PlayableR23/SourceWorldExport/runs/prepared-r23-valley-range/valley'
MANIFEST_SHA='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
CONTENT_ID='23951bcc4c358a38a0fd1ad59fa24cef0171f5df6c6c2cc2e38dca5a095f5520'
REFLECT=np.array([1,1,-1],np.float32)
def sha(p):
    h=hashlib.sha256()
    with Path(p).open('rb')as f:
        for b in iter(lambda:f.read(1048576),b''):h.update(b)
    return h.hexdigest()
def read(p):return json.loads(Path(p).read_text(encoding='utf-8'))
def write(p,v):Path(p).write_text(json.dumps(serial(v),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def bindings():
    manifest=FROZEN.parent/'SOURCE-MANIFEST.json';assert sha(manifest)==MANIFEST_SHA
    m=read(manifest);assert m['contentId']==CONTENT_ID;table={r['path']:r for r in m['files']};out={}
    for rel in ['scripts/cyber_bird.gd','scripts/cyber_bird_registration.gd','scripts/map_catalog1000.gd','scripts/city_life_r5.gd','assets/cyber_bird/weathered.gdshader','assets/cyber_bird/CREDITS.txt','assets/maps1000/cyber_bird.png','assets/maps1000/cyber_bird_top.png']:
        out[rel]=sha(FROZEN/rel);assert out[rel]==table[rel]['sha256'],rel
    return out
def unit_box():
    """Use actual already captured Godot4.7.2 BoxMesh topology/atlas, not glTF cube assumptions."""
    d=read(CAPTURE/'world.json');complete=read(CAPTURE/'complete.json')
    assert sha(CAPTURE/'world.json')==complete['worldSha256'] and sha(CAPTURE/'payload.bin')==d['payloadSha256']
    mesh=next(m for m in d['meshes']if m['class']=='BoxMesh');a=mesh['surfaces'][0]['arrays']
    raw=(CAPTURE/'payload.bin').read_bytes()
    def acc(v,dt):return np.frombuffer(raw,dt,count=v['count']*v['components'],offset=v['offset']).reshape(v['count'],v['components']).copy()
    p=acc(a[0],'<f4');n=acc(a[1],'<f4');uv=acc(a[4],'<f4');idx=acc(a[12],'<i4').ravel()
    # Unit primitive coordinates are analytically +/- .5. The capture's size is
    # known; decoded octahedral normals are rounded back to its exact axis normals.
    p=p/np.array(mesh['aabb']['size'],np.float32)
    assert np.allclose(abs(p),.5,atol=1e-7)and p.shape==(24,3)and len(idx)==36
    p=np.sign(p)*np.float32(.5);n=np.round(n);assert np.allclose(np.linalg.norm(n,axis=1),1)
    proof=dict(worldSha256=sha(CAPTURE/'world.json'),payloadSha256=sha(CAPTURE/'payload.bin'),mesh=mesh['id'],primitive='BoxMesh size ONE; source 24 vertices/36 indices/3x2 atlas; exact axis normals restored from packed-normal quantization',sourceGodot=d['godot']if'godot'in d else d.get('godotVersion'))
    return p,n,uv,idx,proof
def material_rows():
    rows=[]
    for name,c,metal,rough in [('road','14212c',.72,.24),('concrete','344350',.10,.77),('metal','172935',.76,.30),('copper','855645',.72,.35),('floor','3d454c',.30,.53),('warmwall','675950',.15,.72)]:
        rows.append(dict(name=name,profile='cyberWeathered',tint=rgba(c),metallic=metal,roughness=rough,bounceColor=[.24,.16,.085,1]if name in('floor','warmwall')else[0,0,0,0],floorFinish=1 if name=='floor'else 0,cull=0,emissionColor=[0,0,0,1],emissionEnabled=False,color='',normal=''))
    for name,c in [('cyan','21e9ef'),('pink','fb458e'),('gold','ffd17d'),('violet','9a7bff'),('white','e6f6ff')]:
        rows.append(dict(name=name,profile='standard',tint=rgba(c),color='',normal='',metallic=0,roughness=1,cull=0,emissionColor=rgba(c),emissionEnabled=True,emissionEnergy=3.5))
    rows.append(dict(name='dark',profile='standard',tint=rgba('070f19'),color='',normal='',metallic=.5,roughness=.34,cull=0))
    return rows
def make_environment(g):
    return dict(version=1,mapId='cyber_bird',sourceContentId=CONTENT_ID,sourceEnvironment=dict(background_mode=1,background_color=dict(colorUnconverted=rgba('07101f')),ambient_light_source=3,ambient_light_color=dict(colorUnconverted=rgba('799bb9')),ambient_light_energy=.48,tonemap_mode=2,glow_enabled=True,glow_intensity=.9,glow_bloom=.14,fog_enabled=True,fog_light_color=dict(colorUnconverted=rgba('1d3043')),fog_density=.0018),directional=dict(name='City_Moon',sourceEulerDegrees=[-47,-24,0],color=rgba('9ac8ed'),energy=.7,shadows=True,shadowMaxDistance=110,shadowMode='parallel2splits'),pooledStreetLights=dict(count=4,color=rgba('ffbd79'),energy=5,range=19,angle=58,attenuation=1.3,shadows=False,sourceEulerDegrees=[-90,0,0],sites=g.env['_street_sites']),pooledInteriorLights=dict(count=6,range=12,energy=2.3,shadows=False,nearestInteriorCount=3,nearestNeonCount=3,interiorSites=g.env['_light_sites'],neonSites=g.env['_neon_sites']),source='scripts/cyber_bird.gd::_make_environment/update_lighting',limits='Exact source descriptors; root owns Unity light pooling, filmic/glow/fog adaptation. No HDR sky exists for this color-background map.')

class Writer:
    def __init__(self,folder,materials):
        self.folder=folder;self.materials=materials;self.groups=collections.defaultdict(list);self.counts=collections.Counter();self.chunkmeta=[];self.hashes=collections.defaultdict(hashlib.sha256);self.low=np.full(3,np.inf);self.high=-self.low
        self.path=folder/'geometry.bytes';self.f=self.path.open('wb');self.f.write(struct.pack('<ii',0x52323357,0))
    def add(self,key,v,idx,source=None):
        v=np.asarray(v,dtype='<f4');idx=np.asarray(idx,dtype='<i4').ravel();assert v.shape[1]==18 and len(idx)%3==0 and np.isfinite(v).all()
        parts=self.groups[key]
        if sum(len(v)for v,_,_ in parts)+len(v)>60000:self.flush(key);parts=self.groups[key]
        parts.append((v,idx,source));self.hashes[str(key)].update(v[idx].tobytes())
        if key[2]:self.low=np.minimum(self.low,v[:,:3].min(0));self.high=np.maximum(self.high,v[:,:3].max(0))
    def flush(self,key):
        parts=self.groups[key]
        if not parts:return
        mid,coll,visible,layer,backface,detail,distance,shadows=key;vertices=np.concatenate([v for v,_,_ in parts]);indices=[];offset=0
        for v,idx,_ in parts:indices.append(idx+offset);offset+=len(v)
        indices=np.concatenate(indices).astype('<i4');assert len(vertices)<=60000 and indices.min()>=0 and indices.max()<len(vertices)
        self.f.write(struct.pack('<iiiii',mid,coll,visible,len(vertices),len(indices)));self.f.write(vertices.tobytes());self.f.write(indices.tobytes())
        self.chunkmeta.append(dict(material=mid,sourceLayer=layer,backface=backface,detail=detail,visibilityRangeEnd=distance,castShadows=shadows,collision=bool(coll),render=bool(visible),vertices=len(vertices),triangles=len(indices)//3,sourceInstances=len(parts),sourceChunks=sorted(set(s for _,_,s in parts if s))))
        if visible:self.counts['renderTriangles']+=len(indices)//3
        if coll:self.counts['collisionTriangles']+=len(indices)//3
        self.groups[key]=[]
    def finish(self):
        for key in list(self.groups):self.flush(key)
        self.f.seek(4);self.f.write(struct.pack('<i',len(self.chunkmeta)));self.f.close();assert len(self.chunkmeta)<=4096

def make_vertices(p,n,uv,basis,origin):
    world=(p@basis.T+origin)*REFLECT;normal=(n@np.linalg.inv(basis))*REFLECT;length=np.linalg.norm(normal,axis=1);normal/=np.maximum(length[:,None],1e-20)
    tex=uv.copy();tex[:,1]=1-tex[:,1]
    return np.column_stack((world,normal,tex,np.ones((len(p),4),np.float32),p,n)).astype('<f4')

def signs(g,w):
    font_path=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI/Resources/R23GodotUI.ttf'
    assert sha(font_path)=='09b346e12a76ff87bf9fdfb9e7f0f4840413ad330da6d78dc71c2ce463ce1637' or sha(font_path)=='ab424b0dfe9aa67c0305a992402750431ea024254047f6e09a99c0b89955e9a1'
    font=ImageFont.truetype(str(font_path),96);cache={}
    for row in g.signs:
        text=row['text'];color=tuple(int(round(v*255))for v in rgba(dict(cyan='21e9ef',pink='fb458e',gold='ffd17d',violet='9a7bff',white='e6f6ff')[row['color']]))
        key=(text,color)
        if key not in cache:
            bounds=ImageDraw.Draw(Image.new('RGBA',(1,1))).multiline_textbbox((0,0),text,font=font,spacing=0,align='center',stroke_width=4)
            width=math.ceil(bounds[2]-bounds[0]+4);height=math.ceil(bounds[3]-bounds[1]+4)
            assert width<=2048 and height<=512
            image=Image.new('RGBA',(width,height));draw=ImageDraw.Draw(image);draw.multiline_text((2-bounds[0],2-bounds[1]),text,font=font,fill=color,align='center',spacing=0,stroke_width=4,stroke_fill=tuple(int(round(v*255))for v in rgba('07101b')))
            filename='sign-'+hashlib.sha256((text+str(color)).encode()).hexdigest()[:16]+'.png';image.save(w.folder/filename)
            mid=len(w.materials);w.materials.append(dict(name='source-sign-'+str(mid),profile='standard',tint=[1,1,1,1],color=filename,normal='',metallic=0,roughness=1,cull=2,transparency=1,alpha=True,unshaded=True,emissionEnabled=False))
            cache[key]=(mid,width,height)
        mid,width,height=cache[key];size=row['pixelSize'];ww=width*size;hh=height*size
        p=np.array([[-ww/2,hh/2,0],[ww/2,hh/2,0],[-ww/2,-hh/2,0],[ww/2,-hh/2,0]],np.float32)
        n=np.tile([0,0,1],(4,1)).astype(np.float32);uv=np.array([[0,0],[1,0],[0,1],[1,1]],np.float32)
        b=Basis(Vec.UP,row['rotation']).a;v=make_vertices(p,n,uv,b,np.array(row['position']))
        w.add((mid,0,1,0,False,False,300,False),v,np.array([0,1,2,1,3,2]),row['sourceChunk'])
    return dict(fontSha256=sha(font_path),sourceFont='Godot4.7.2 default OpenSans SemiBold; matching original glyphs, added source-independent star fallback never used here',signInstances=len(g.signs),uniqueSignImages=len(cache),rasterizer='Pillow font96, source4px outline/pixel_size/modulate; native Godot Label3D raster/line-baseline parity remains unverified')

def prepare(publish=False):
    start=time.monotonic();bound=bindings();OUT.mkdir(parents=True,exist_ok=True)
    source=(FROZEN/'scripts/cyber_bird.gd').read_text(encoding='utf-8');g=Geometry(source)
    (HERE/'executed_geometry.py.txt').write_text(g.python,encoding='utf-8')
    p,n,uv,idx,primitive=unit_box();materials=material_rows();byname={r['name']:i for i,r in enumerate(materials)};w=Writer(OUT,materials)
    solid=0;unit_hash=hashlib.sha256(p[idx].tobytes()).hexdigest();actual_box_receipts=[];deployment=0
    for box in g.boxes:
        basis=box['basis']@np.diag(box['size']);origin=g.env['global_position'].a+box['chunkPosition']+box['position']
        v=make_vertices(p,n,uv,basis,origin);coll=int(box['solid']);solid+=coll
        shadows=box['material']not in('cyan','pink','gold','violet','white')and box['chunk']!='Visible_Flight_Canopy'
        w.add((byname[box['material']],coll,1,1 if coll else 0,False,box['detail'],170 if box['detail']else 950,shadows),v,idx,box['chunk'])
        if box['chunk']not in('Finished_City_Seawall','Visible_Flight_Canopy'):deployment=max(deployment,float((p@basis.T+origin)[:,1].max()))
        actual_box_receipts.append([byname[box['material']],coll,int(box['detail']),*origin.tolist(),*basis.ravel().tolist()])
    for ramp in g.ramps:
        # Exact source six vertices, one-sided layer16 foot support. Zero source
        # normal/UV has no render meaning; supply geometric normal for valid data.
        pos=ramp['faces'];cross=-np.cross(pos[1]-pos[0],pos[2]-pos[0]);cross/=np.linalg.norm(cross)
        normal=np.tile(cross,(6,1));v=make_vertices(pos,normal,np.zeros((6,2),np.float32),np.eye(3,dtype=np.float32),np.zeros(3,np.float32))
        w.add((0,1,0,16,False,False,0,False),v,np.arange(6),ramp['chunk'])
    label_proof=signs(g,w);w.finish()
    spawn=[-9,.16,-2765]
    manifest=dict(version=2,mapId='cyber_bird',title='CYBER BIRD',sourceSha256=bound['scripts/cyber_bird.gd'],sourceContentId=CONTENT_ID,sourceOrigin=[0,0,2400],spawn=spawn,gameSpawn=spawn,warmupSpawn=spawn,boundsMin=w.low.tolist(),boundsMax=w.high.tolist(),materials=w.materials,chunks=len(w.chunkmeta),chunkSourceLayers=[r['sourceLayer']for r in w.chunkmeta],chunkBackfaceCollision=[r['backface']for r in w.chunkmeta],chunkVisibilityRanges=[r['visibilityRangeEnd']for r in w.chunkmeta],chunkCastShadows=[r['castShadows']for r in w.chunkmeta],cameraFarDistance=1000,cyberEnvironmentFile='environment.json',environmentFile='',cyberBirdMetadataFile='city.json')
    write(OUT/'map.json',manifest);write(OUT/'environment.json',make_environment(g));write(OUT/'source-chunks.json',w.chunkmeta)
    # Detailed box receipts are CPU proof, not 35MB of runtime JSON allocations.
    np.save(HERE/'source-boxes.npy',np.asarray(actual_box_receipts,np.float32))
    metadata=dict(mapId='cyber_bird',originGodot=[0,0,2400],coordinates='world-baked original Godot; reflect Z once for Unity',spawnGodot=[-9,.16,2765],deploymentTop=deployment,fallResetBelow=-12,buildings=g.env['buildings'],windows=g.env['windows'],routes=g.env['routes'],lootPoints=g.env['loot_points'],sourceSigns=g.signs,sourceChunks=g.chunks,sourceFootRamps=g.ramps,sourceStatistics=dict(g.env['statistics'],collision_triangles=solid*12,batches=len(w.chunkmeta)),scope='source build() geometry only; source enter() traffic,384liveweaponinstances,ammo/consumables and gameplay remain runtime integration')
    write(OUT/'city.json',metadata)
    for name in ['cyber_bird.png','cyber_bird_top.png']:shutil.copyfile(FROZEN/'assets/maps1000'/name,OUT/('preview-top.png'if'_top'in name else'preview.png'))
    shutil.copyfile(FROZEN/'assets/cyber_bird/CREDITS.txt',OUT/'SOURCE-CREDITS.txt')
    report=dict(sourceContentId=CONTENT_ID,sourceInputs=bound,engineRun=False,renderRun=False,sourceMath='Restricted frozen GDScript geometry subset; unsupported syntax fails, integer division/array loops/dictionary fields mapped explicitly; Vector/Basis operations float32',primitive=primitive,unitFaceHash=unit_hash,boxes=len(g.boxes),solidBoxes=solid,buildings=len(g.env['buildings']),storeys=g.env['statistics']['floors'],stairs=g.env['statistics']['stairs'],bridges=g.env['statistics']['bridges'],ramps=len(g.ramps),labels=label_proof,chunks=len(w.chunkmeta),materials=len(w.materials),counts=dict(w.counts),sourceBoxReceiptsSha256=sha(HERE/'source-boxes.npy'),geometrySha256=sha(OUT/'geometry.bytes'),elapsedSeconds=round(time.monotonic()-start,3),limits=['No native/render/headset validation.','Root must bind cyberWeathered shader, color-background environment/light pools and layer16 foot-only collision; environmentFile remains empty to avoid misusing Valley-only HDR loader.','Runtime traffic,live weapon/ammo/consumable placements,weather lighting pools and occluder nodes are preserved/described or separate features, not represented as fake scenery.','Label3D uses genuine Godot default glyphs rasterized on CPU; exact engine text baseline/raster and glow/tone response remain unverified.','Native source visibility distances are recorded; global native batching differs from each original per-building MultiMesh visibility owner.'])
    write(OUT/'provenance.json',report)
    print(json.dumps({k:report[k]for k in ['boxes','solidBoxes','buildings','storeys','stairs','bridges','ramps','labels','chunks','materials','counts','elapsedSeconds']},indent=2),flush=True)
    if publish:
        import check;check.check(OUT)
        assert not LIVE.exists(),'Do not overwrite historical/newly published map'
        assert shutil.disk_usage('C:/').free>8*1024**3+sum(p.stat().st_size for p in OUT.iterdir()if p.is_file())
        LIVE.mkdir()
        for pth in [LIVE]+list(OUT.iterdir()):
            target=pth if pth==LIVE else LIVE/pth.name
            if pth!=LIVE:shutil.copyfile(pth,target)
            guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r6-cyberbird/'+target.name).hex
            Path(str(target)+'.meta').write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+('folderAsset: yes\n'if target.is_dir()else'')+'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
        print('Published '+str(LIVE),flush=True)
    return OUT
if __name__=='__main__':
    a=argparse.ArgumentParser();a.add_argument('--publish',action='store_true');args=a.parse_args();prepare(args.publish)
