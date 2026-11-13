from pathlib import Path
import hashlib,io,json,re,struct,subprocess
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[4]
TOOLS=Path(__file__).resolve().parent
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Appearance'
SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
report=[]
provenance=json.loads((OUT/'provenance.json').read_text(encoding='utf-8'))
for relative,digest in provenance['inputs'].items():
    assert hashlib.sha256((SRC/relative).read_bytes()).hexdigest()==digest,relative
report.append('PASS preserved original R23 source input hashes match')
for name,digest in json.loads((TOOLS/'baseline-three-hashes.json').read_text()).items():
    assert hashlib.sha256((OUT/name).read_bytes()).hexdigest()==digest,name
report.append('PASS original three mesh, texture and manifest outputs are byte-identical')
source_script=(SRC/'scripts/gemini_player_models_099.gd').read_text(encoding='utf-8')
labels=json.loads(re.search(r'const STYLES := (\[.*?\])',source_script).group(1))
keys=json.loads(re.search(r'const STYLE_KEYS := (\[.*?\])',source_script).group(1))
digests=set()
for row in provenance['styles']:
    key=row['id'];manifest=json.loads((OUT/(key+'.json')).read_text(encoding='utf-8'))
    assert manifest['id']==key and manifest['sourceIndex']==row['sourceIndex']
    assert keys[row['sourceIndex']]==key and manifest['label']==labels[row['sourceIndex']]
    raw=(SRC/row['sourceModel']).read_bytes();size=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+size]);blob=raw[28+size:]
    def acc(i):
        a=g['accessors'][i];v=g['bufferViews'][a['bufferView']]
        dtype=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);c={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        return np.ndarray((a['count'],c),dtype=dtype,buffer=blob,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dtype.itemsize*c),dtype.itemsize)).copy()
    mesh=(OUT/(key+'.meshbin')).read_bytes();digests.add(hashlib.sha256(mesh).hexdigest());magic,count=struct.unpack_from('<ii',mesh);assert magic==0x52323341 and 1<=count<=128
    offset=8;parts=[]
    for _ in range(count):
        length,=struct.unpack_from('<i',mesh,offset);offset+=4;name=mesh[offset:offset+length].decode();offset+=length
        nv,nt,mi=struct.unpack_from('<iii',mesh,offset);offset+=12
        vertices=np.frombuffer(mesh,dtype='<f4',count=nv*8,offset=offset).reshape(nv,8);offset+=nv*32
        indices=np.frombuffer(mesh,dtype='<i4',count=nt,offset=offset).reshape(-1,3);offset+=nt*4
        assert np.isfinite(vertices).all() and indices.min()>=0 and indices.max()<nv
        assert np.max(abs(np.linalg.norm(vertices[:,3:6],axis=1)-1))<.0001
        assert nv<=100000 and nt<=300000 and 0<=mi<manifest['gloveMaterialIndex']
        parts.append((name,vertices,indices,mi))
    names={p[0] for p in parts};required={'Jacket','ChestPlate','Belt','LUpperArm','RUpperArm','LForearm','RForearm','LThigh','RThigh','LShin','RShin','LBoot','RBoot','AvatarHead'}
    assert offset==len(mesh) and not any('Hand' in label for label in names)
    assert names==required-({'ChestPlate'} if key in ['astra','sol','argon'] else set())
    cursor=0
    for node in g['nodes']:
        if 'mesh' not in node:continue
        assert not any(k in node for k in ['matrix','translation','rotation','scale','skin'])
        for prim in g['meshes'][node['mesh']]['primitives']:
            attributes=prim['attributes'];name,vertices,indices,mi=parts[cursor];cursor+=1
            assert name==node['name']
            p=acc(attributes['POSITION']);p[:,2]*=-1;n=acc(attributes['NORMAL']);n[:,2]*=-1;uv=acc(attributes['TEXCOORD_0']);uv[:,1]=1-uv[:,1]
            assert np.array_equal(vertices,np.c_[p,n,uv])
            assert np.array_equal(indices,acc(prim['indices']).reshape(-1,3)[:,[0,2,1]])
            assert mi==prim.get('material',0)
    assert cursor==len(parts)
    def original_image(index):
        im=g['images'][g['textures'][index]['source']]
        if 'bufferView' in im:
            view=g['bufferViews'][im['bufferView']];start=view.get('byteOffset',0);return blob[start:start+view['byteLength']]
        return (SRC/row['sourceModel']).parent.joinpath(im['uri']).read_bytes()
    for expected,actual in zip(g['materials'],manifest['materials']):
        pbr=expected.get('pbrMetallicRoughness',{})
        assert expected.get('alphaMode','OPAQUE')=='OPAQUE'
        assert actual['color']==pbr.get('baseColorFactor',[1,1,1,1])
        assert actual['metallic']==pbr.get('metallicFactor',1) and actual['roughness']==pbr.get('roughnessFactor',1)
        assert actual['emissionColor']==expected.get('emissiveFactor',[0,0,0])
        for target,definition in [('baseColor',pbr.get('baseColorTexture')),('normal',expected.get('normalTexture')),('emission',expected.get('emissiveTexture'))]:
            if definition:assert (OUT/actual[target]).read_bytes()==original_image(definition['index'])
        if pbr.get('metallicRoughnessTexture'):
            orm=np.asarray(Image.open(io.BytesIO(original_image(pbr['metallicRoughnessTexture']['index']))).convert('RGB'))
            repacked=np.asarray(Image.open(OUT/actual['metallicMap']))
            assert np.array_equal(repacked[:,:,0],orm[:,:,2]) and np.array_equal(repacked[:,:,3],255-orm[:,:,1])
        if expected.get('occlusionTexture'):
            orm=np.asarray(Image.open(io.BytesIO(original_image(expected['occlusionTexture']['index']))).convert('RGB'))
            assert np.array_equal(np.asarray(Image.open(OUT/actual['occlusion']))[:,:,1],orm[:,:,0])
    tex=SRC/'assets/models/gemini_players099/textures'
    if key=='claude':tex=SRC/'assets/models/witch_doctors_r8'
    if key=='obsidian':tex=SRC/'assets/models/obsidian'
    glove=manifest['materials'][manifest['gloveMaterialIndex']];glove_key=row['gloveSourceKey']
    for target,source in [('baseColor','albedo'),('normal','normal')]:
        assert (OUT/glove[target]).read_bytes()==(tex/f'glove_{glove_key}_{source}.png').read_bytes()
    orm=np.asarray(Image.open(tex/f'glove_{glove_key}_orm.png').convert('RGB'))
    metallic=np.asarray(Image.open(OUT/glove['metallicMap']));occlusion=np.asarray(Image.open(OUT/glove['occlusion']))
    assert np.array_equal(metallic[:,:,0],orm[:,:,2]) and np.array_equal(metallic[:,:,3],255-orm[:,:,1]) and np.array_equal(occlusion[:,:,1],orm[:,:,0])
    for material in manifest['materials']:
        for channel in ['baseColor','normal','metallicMap','occlusion','emission']:
            if material[channel]:
                image=Image.open(OUT/material[channel]);image.verify()
    assert manifest['eyeToShoulder']==(.18 if key=='obsidian' else .29) and manifest['bodyRear']==(.06 if key=='obsidian' else .12)
    assert len(manifest['leftWrist'])==3 and len(manifest['rightWrist'])==3
    report.append(f'PASS {key}: {len(names)} exact source parts / {count} material surfaces, {row["vertices"]} vertices / {row["triangles"]} triangles, real PBR+glove maps, finite normals and reflected topology')
runtime=(ROOT/'unity/Assets/Ashfall/Integration/Playable/Appearance/R23PlayerAppearance.cs').read_text(encoding='utf-8')
assert re.findall(r'new R23AppearanceStyle\("(.*?)"',runtime)==keys[:8]
assert len(digests)==8
report.append('PASS eight distinct source mesh payloads; intentional missing chest plates and multiple materials retained, no duplicate hands')
editor=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'))
csc=Path(r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER','/out:'+str(TOOLS/'Appearance.Native.dll')]+['/reference:'+str(p) for p in refs]+[str(p) for p in (ROOT/'unity/Assets/Ashfall/Integration/Playable/Appearance').glob('*.cs')]
rsp=TOOLS/'native.rsp';rsp.write_text('\n'.join('"'+p+'"' for p in args))
r=subprocess.run([str(csc),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(TOOLS/'native.log').write_text(r.stdout+r.stderr);print(r.stdout,r.stderr);assert r.returncode==0
report.append('PASS installed Unity 6000.6.8f1 API compilation, warnings as errors')
report.append('No Unity engine, GPU, build, renderer or headset observation performed.')
(TOOLS/'check-results.txt').write_text('\n'.join(report)+'\n',encoding='utf-8')
print('\n'.join(report))
