from pathlib import Path
import base64,hashlib,io,json,re,shutil,struct
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[4]
SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Appearance'
OUT.mkdir(parents=True,exist_ok=True)
records={}
def record(p):
    records[str(p.relative_to(SRC))]=hashlib.sha256(p.read_bytes()).hexdigest()
def document(p):
    record(p);raw=p.read_bytes()
    if p.suffix=='.glb':
        n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);blob=raw[28+n:]
    else:g=json.loads(raw);blob=None
    buffers=[]
    for b in g['buffers']:
        uri=b.get('uri')
        if uri is None:buffers.append(blob)
        elif uri.startswith('data:'):buffers.append(base64.b64decode(uri.split(',')[1]))
        else:record(p.parent/uri);buffers.append((p.parent/uri).read_bytes())
    def accessor(i):
        a=g['accessors'][i];v=g['bufferViews'][a['bufferView']]
        dtype=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']])
        components={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
        return np.ndarray((a['count'],components),dtype=dtype,buffer=buffers[v['buffer']],offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dtype.itemsize*components),dtype.itemsize)).copy()
    def image(i):
        im=g['images'][g['textures'][i]['source']]
        if 'bufferView' in im:
            v=g['bufferViews'][im['bufferView']];return buffers[v['buffer']][v.get('byteOffset',0):v.get('byteOffset',0)+v['byteLength']]
        uri=im['uri']
        if uri.startswith('data:'):return base64.b64decode(uri.split(',')[1])
        record(p.parent/uri);return (p.parent/uri).read_bytes()
    return g,accessor,image

def material(name,data,image):
    p=data.get('pbrMetallicRoughness',{})
    output={'color':p.get('baseColorFactor',[1,1,1,1]),'metallic':p.get('metallicFactor',1),'roughness':p.get('roughnessFactor',1),'doubleSided':data.get('doubleSided',False),'baseColor':'','normal':'','metallicMap':'','occlusion':'','emission':'','emissionColor':data.get('emissiveFactor',[0,0,0]),'normalScale':data.get('normalTexture',{}).get('scale',1),'occlusionStrength':data.get('occlusionTexture',{}).get('strength',1)}
    for key,source in [('baseColor',p.get('baseColorTexture')),('normal',data.get('normalTexture')),('emission',data.get('emissiveTexture'))]:
        if source:
            fn=name+'-'+key+'.png';(OUT/fn).write_bytes(image(source['index']));output[key]=fn
    if p.get('metallicRoughnessTexture'):
        texture=Image.open(io.BytesIO(image(p['metallicRoughnessTexture']['index']))).convert('RGB');red,green,blue=texture.split();zero=Image.new('L',texture.size,0)
        # glTF B metallic / G roughness -> URP R metallic / A smoothness.
        fn=name+'-metallic.png';Image.merge('RGBA',(blue,zero,zero,green.point(lambda x:255-x))).save(OUT/fn);output['metallicMap']=fn
    if data.get('occlusionTexture'):
        texture=Image.open(io.BytesIO(image(data['occlusionTexture']['index']))).convert('RGB');red=texture.split()[0];zero=Image.new('L',texture.size,0)
        fn=name+'-occlusion.png';Image.merge('RGB',(zero,red,zero)).save(OUT/fn);output['occlusion']=fn
    return output

def wrist_offset(side):
    p=SRC/f'assets/hands/xrtools086/Hand_Glove_low_{side}.gltf';g,_,_=document(p)
    parents={c:i for i,n in enumerate(g['nodes']) for c in n.get('children',[])}
    def world(i):
        n=g['nodes'][i]
        if 'matrix' in n:m=np.array(n['matrix']).reshape(4,4).T
        else:
            x,y,z,w=n.get('rotation',[0,0,0,1]);m=np.eye(4);m[:3,:3]=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],[2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])@np.diag(n.get('scale',[1,1,1]));m[:3,3]=n.get('translation',[0,0,0])
        return world(parents[i])@m if i in parents else m
    a=next(i for i,n in enumerate(g['nodes']) if n.get('name')=='Wrist_'+side)
    b=next(i for i,n in enumerate(g['nodes']) if n.get('name')=='Middle_Proximal_'+side)
    angle=-np.pi/2 if side=='L' else np.pi/2;c,s=np.cos(angle),np.sin(angle);rotation=np.array([[c,-s,0],[s,c,0],[0,0,1]])
    point=rotation@(world(a)[:3,3]-(world(a)[:3,3]+world(b)[:3,3])*.5)+[0,.032,0];point[2]*=-1
    return point.tolist()

source=SRC/'scripts/gemini_player_models_099.gd';text=source.read_text(encoding='utf-8');labels=json.loads(re.search(r'const STYLES := (\[.*?\])',text).group(1))
styles=[]
for index,key in enumerate(['camo','witch_doctor','eclipse_ronin','astra','sol','argon','claude','obsidian']):
    relative=f'assets/models/gemini_players099/player_kit_{key}_pcvr.glb'
    if index>=3: relative=f'assets/models/witch_doctors_r8/witch_doctor_{key}.glb'
    if key=='claude': relative='assets/models/witch_doctors_r8/witch_doctor_claude_pcvr.glb'
    if key=='obsidian': relative='assets/models/obsidian/player_kit_obsidian_pcvr.glb'
    path=SRC/relative;g,accessor,image=document(path)
    mats=[material(key+'-body-'+str(i),m,image) for i,m in enumerate(g['materials'])]
    # Source style_gloves uses the same existing XRTools glove mesh with these authored maps.
    tex=SRC/'assets/models/gemini_players099/textures';glove_key=key if index<3 else 'witch_doctor'
    if key=='claude':tex=SRC/'assets/models/witch_doctors_r8';glove_key='claude'
    if key=='obsidian':tex=SRC/'assets/models/obsidian';glove_key='obsidian'
    def glove_image(i):
        p=tex/f'glove_{glove_key}_{["albedo","normal","orm"][i]}.png';record(p);return p.read_bytes()
    glove={'pbrMetallicRoughness':{'baseColorTexture':{'index':0},'metallicRoughnessTexture':{'index':2}},'normalTexture':{'index':1},'occlusionTexture':{'index':2}}
    glove_index=len(mats);mats.append(material(key+'-glove',glove,glove_image))
    parts=[]
    for node in g['nodes']:
        if 'mesh' not in node:continue
        # Source apply_style takes mesh-local geometry; its analytic render poses replace authored nodes.
        assert not any(k in node for k in ['matrix','translation','rotation','scale','skin'])
        for prim in g['meshes'][node['mesh']]['primitives']:
            assert prim.get('mode',4)==4
            a=prim['attributes'];v=accessor(a['POSITION']);n=accessor(a['NORMAL']);uv=accessor(a['TEXCOORD_0'])
            v[:,2]*=-1;n[:,2]*=-1;uv[:,1]=1-uv[:,1]
            indices=accessor(prim['indices']).astype('<i4').reshape(-1,3)[:,[0,2,1]]
            assert np.isfinite(v).all() and np.isfinite(n).all() and indices.min()>=0 and indices.max()<len(v)
            parts.append((node['name'],np.c_[v,n,uv].astype('<f4'),indices.astype('<i4'),prim.get('material',0)))
    with (OUT/(key+'.meshbin')).open('wb') as f:
        f.write(struct.pack('<ii',0x52323341,len(parts)))
        for name,vertices,indices,mi in parts:
            encoded=name.encode();f.write(struct.pack('<i',len(encoded)));f.write(encoded);f.write(struct.pack('<iii',len(vertices),indices.size,mi));f.write(vertices.tobytes());f.write(indices.tobytes())
    manifest={'version':1,'id':key,'label':labels[index],'sourceIndex':index,'eyeToShoulder':.18 if key=='obsidian' else .29,'bodyRear':.06 if key=='obsidian' else .12,'gloveMaterialIndex':glove_index,'leftWrist':wrist_offset('L'),'rightWrist':wrist_offset('R'),'materials':mats}
    (OUT/(key+'.json')).write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    styles.append({'id':key,'label':labels[index],'sourceIndex':index,'sourceModel':relative,'gloveSourceKey':glove_key,'vertices':sum(len(v) for _,v,_,_ in parts),'triangles':sum(i.size//3 for _,_,i,_ in parts),'parts':[p[0] for p in parts]})
for path in [source,SRC/'scripts/player_body.gd',SRC/'scripts/grip_hand_086.gd',SRC/'assets/models/gemini_players099/ATTRIBUTION.txt',SRC/'assets/models/gemini_players099/PROVENANCE.json',SRC/'assets/models/witch_doctors_r8/PROVENANCE.json',SRC/'assets/models/witch_doctors_r8/CLAUDE-KIT-README.md',SRC/'assets/models/obsidian/PROVENANCE.json']:
    record(path)
shutil.copyfile(SRC/'assets/models/gemini_players099/ATTRIBUTION.txt',OUT/'ATTRIBUTION.txt')
(OUT/'provenance.json').write_text(json.dumps({'baseline':'original preserved Opus R23 2026-11-03 ashfall-r23/source','styles':styles,'inputs':records,'originalSkinProvenance':{folder:json.loads((SRC/('assets/models/'+folder+'/PROVENANCE.json')).read_text(encoding='utf-8')) for folder in ['witch_doctors_r8','obsidian']},'conversion':'Complete source mesh-local geometry, no decimation. Multiple primitives retain source material slots on the same analytic part; absent ChestPlate remains empty as source apply_style requires. Reflect Z and triangle winding once; flip glTF UV V once. Extract original albedo/normal/emission PNG and repack ORM into URP metallic/smoothness and occlusion channels. Analytic body pose follows actual source player_body.gd profiles; no tracked root/pose writes.'},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(styles,ensure_ascii=False,indent=2))
