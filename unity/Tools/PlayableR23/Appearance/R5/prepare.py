"""Bounded, manifest-verified R5 outfit conversion. No engine/Blender/source writes."""
from pathlib import Path
import argparse,ast,base64,hashlib,io,json,re,struct,uuid
import numpy as np
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];SRC=ROOT/'quest-r24/source';OUT=HERE/'StagedPayload'
PREIMAGE=Path('D:/ashfall-r6-staging/world/outfits-v001/r5-preimages')
OUT.mkdir(parents=True,exist_ok=True)
CONTENT=ROOT/'campaign-staging/20261104-three-chats/world/morning-r5/r5/delivery/SOURCE-CONTENT.json'
assert hashlib.sha256(CONTENT.read_bytes()).hexdigest()=='d4fb1053acfbaa7f3eb285879472bfdd136e09bf2f7e1f2dc6478f23d8f12bc7'
content=json.loads(CONTENT.read_text());assert content['content_id']=='da1db74984ea1b82a8b60b7cb8baae9d31dc5483f387fdcf105f3dd0a308895c'
known={row['path']:row['sha256']for row in content['files']};records={};locations={}
def resolve(relative):
    assert relative in known,'Not an R5 manifest input: '+relative
    for root in [SRC,PREIMAGE]:
        path=root/relative
        if path.is_file()and hashlib.sha256(path.read_bytes()).hexdigest()==known[relative]:return path
    raise ValueError('Exact R5 input missing: '+relative)
def record(path):
    relative=next(path.relative_to(root).as_posix()for root in [SRC,PREIMAGE]if path.is_relative_to(root));digest=hashlib.sha256(path.read_bytes()).hexdigest();assert known.get(relative)==digest,'Not verified R5 input: '+relative;records[relative]=digest;locations[relative]=str(path)
def read(relative):
    path=resolve(relative);record(path);return path.read_bytes()
registry=read('scripts/gemini_player_models_099.gd').decode('utf8');drafts=json.loads(read('assets/data/draft_styles_r24.json'))
keys=json.loads(re.search(r'const STYLE_KEYS := (\[.*?\])',registry).group(1));labels=json.loads(re.search(r'const STYLES := (\[.*?\])',registry).group(1));extra=json.loads(re.search(r'const EXTRA_KITS := (\{.*\})',registry).group(1));assert len(keys)==len(labels)==85
# Reuse the existing decoder/wrist recipe without executing its original-R23
# top-level batch or allowing its old unverified input reader.
old=HERE.parent/'prepare.py';tree=ast.parse(old.read_text());functions=[n for n in tree.body if isinstance(n,ast.FunctionDef)and n.name in ['document','wrist_offset']]
exec(compile(ast.Module(body=functions,type_ignores=[]),str(old),'exec'))
created=set();tex_rows={};BUDGET_BYTES=2*1024*1024*1024
payload_bytes=sum(p.stat().st_size for p in OUT.iterdir()if p.is_file()and not p.name.endswith('.meta'))
def output(name,raw):
    global payload_bytes
    path=OUT/name
    if path.exists():assert path.read_bytes()==raw,'Refuse changing existing asset '+name
    else:
        assert payload_bytes+len(raw)<=BUDGET_BYTES,'Bounded staged payload exceeds2GiB; choose fewer actual styles'
        path.write_bytes(raw);created.add(name);payload_bytes+=len(raw)
    meta=Path(str(path)+'.meta')
    if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
    return name
def texture(raw,kind,metallic=1,roughness=1):
    image=Image.open(io.BytesIO(raw)).convert('RGBA');before=image.size
    if max(image.size)>1024:image.thumbnail((1024,1024),Image.Resampling.LANCZOS)
    if kind=='metallic':
        _,g,b,_=image.split();zero=Image.new('L',image.size,0)
        # Installed URP17.6 SampleMetallicSpecGloss ignores _Metallic when the
        # map keyword is on. Bake actual glTF factors into these linear channels.
        if metallic!=1:b=b.point(lambda x:round(x*metallic))
        alpha=g.point(lambda x:255-x)if roughness==1 else g.point(lambda x:round(255-x*roughness))
        image=Image.merge('RGBA',(b,zero,zero,alpha))
    elif kind=='occlusion':
        r,_,_,_=image.split();zero=Image.new('L',image.size,0);image=Image.merge('RGB',(zero,r,zero))
    encoded=io.BytesIO();image.save(encoded,format='PNG');data=encoded.getvalue();digest=hashlib.sha256(data).hexdigest();name='r5-'+kind+'-'+digest[:24]+'.png'
    output(name,data);tex_rows[name]=dict(sourceDimensions=before,dimensions=image.size,sha256=digest);return name
def material(data,image):
    p=data.get('pbrMetallicRoughness',{});row=dict(color=p.get('baseColorFactor',[1,1,1,1]),metallic=p.get('metallicFactor',1),roughness=p.get('roughnessFactor',1),doubleSided=data.get('doubleSided',False),baseColor='',normal='',metallicMap='',occlusion='',emission='',emissionColor=data.get('emissiveFactor',[0,0,0]),normalScale=data.get('normalTexture',{}).get('scale',1),occlusionStrength=data.get('occlusionTexture',{}).get('strength',1))
    for field,slot,kind in [('baseColor',p.get('baseColorTexture'),'albedo'),('normal',data.get('normalTexture'),'normal'),('emission',data.get('emissiveTexture'),'emission'),('metallicMap',p.get('metallicRoughnessTexture'),'metallic'),('occlusion',data.get('occlusionTexture'),'occlusion')]:
        if slot:row[field]=texture(image(slot['index']),kind,row['metallic'],row['roughness'])
    extensions=data.get('extensions',{});energy=extensions.get('KHR_materials_emissive_strength',{}).get('emissiveStrength',1);specular=extensions.get('KHR_materials_specular',{}).get('specularFactor',1)
    if energy!=1:row['emissionEnergy']=energy
    if specular!=1:row['specularFactor']=specular
    return row
def convert(key):
    index=keys.index(key);config=extra[key];directory=config['dir'].removeprefix('res://');tier='light';relative=directory+'player_kit_'+key+'_light.glb'
    if relative not in known:relative=relative.replace('_light.glb','_pcvr.glb');tier='pcvr-fallback'
    path=resolve(relative);g,accessor,image=document(path);materials=[material(m,image)for m in g['materials']];parts=[];body_materials={}
    for node in g['nodes']:
        if 'mesh'not in node:continue
        assert not any(k in node for k in ['matrix','translation','rotation','scale','skin'])
        for primitive in g['meshes'][node['mesh']]['primitives']:
            assert primitive.get('mode',4)==4;a=primitive['attributes'];v=accessor(a['POSITION']);n=accessor(a['NORMAL']);uv=accessor(a['TEXCOORD_0']);v[:,2]*=-1;n[:,2]*=-1;uv[:,1]=1-uv[:,1]
            indices=accessor(primitive['indices']).astype('<i4').reshape(-1,3)[:,[0,2,1]];assert np.isfinite(v).all()and np.isfinite(n).all()and indices.min()>=0 and indices.max()<len(v)
            mi=primitive.get('material',0);body_materials.setdefault(node['name'],mi);parts.append((node['name'],np.c_[v,n,uv].astype('<f4'),indices.astype('<i4'),mi))
    if 'glove_from_body_part'in config:glove_index=body_materials[config['glove_from_body_part']]
    else:
        names=['albedo','normal','orm']
        def glove_image(i):return read(directory+'glove_'+key+'_'+names[i]+'.png')
        glove_index=len(materials);glove=material(dict(pbrMetallicRoughness=dict(baseColorTexture=dict(index=0),metallicRoughnessTexture=dict(index=2)),normalTexture=dict(index=1),occlusionTexture=dict(index=2)),glove_image)
        if 'metallic_specular'in config.get('glove_material_parameters',{})and config['glove_material_parameters']['metallic_specular']!=.5:glove['specularFactor']=config['glove_material_parameters']['metallic_specular']*2
        materials.append(glove)
    stream=io.BytesIO();stream.write(struct.pack('<ii',0x52323341,len(parts)))
    for name,vertices,indices,mi in parts:
        encoded=name.encode();stream.write(struct.pack('<i',len(encoded)));stream.write(encoded);stream.write(struct.pack('<iii',len(vertices),indices.size,mi));stream.write(vertices.tobytes());stream.write(indices.tobytes())
    output(key+'.meshbin',stream.getvalue());manifest=dict(version=1,id=key,label=labels[index],sourceIndex=index,eyeToShoulder=config['eye_to_shoulder'],bodyRear=config['body_rear'],gloveMaterialIndex=glove_index,leftWrist=wrist_offset('L'),rightWrist=wrist_offset('R'),materials=materials)
    if config.get('chestplate_follow_jacket'):manifest['chestplateFollowsJacket']=True
    if key in ['anna_tabby','anna_rainbow','anna']:manifest['stabilizePreviewLegs']=True
    output(key+'.json',json.dumps(manifest,ensure_ascii=False,indent=2).encode('utf8'))
    row=dict(id=key,sourceIndex=index,label=labels[index],sourceModel=relative,tier=tier,triangles=sum(i.size//3 for _,_,i,_ in parts),vertices=sum(len(v)for _,v,_,_ in parts),parts=[name for name,_,_,_ in parts],gloveFromBody=config.get('glove_from_body_part'))
    print(json.dumps(row),flush=True);return row
def main(ids,receipt_name):
    before={p.name:hashlib.sha256(p.read_bytes()).hexdigest()for p in OUT.iterdir()if p.is_file()and not p.name.endswith('.meta')};styles=[convert(key)for key in ids]
    for name,digest in before.items():assert hashlib.sha256((OUT/name).read_bytes()).hexdigest()==digest
    new_bytes=sum((OUT/name).stat().st_size for name in created);assert payload_bytes<=BUDGET_BYTES
    receipt=dict(contentId=content['content_id'],sourceManifestSha256=hashlib.sha256(CONTENT.read_bytes()).hexdigest(),styles=styles,inputs=records,inputLocations=locations,textures=tex_rows,newPayloadBytes=new_bytes,cumulativePayloadBytes=payload_bytes,textureCap=1024,sharedTextures=True,existingFilesUnchanged=len(before),remainingIndices=[i for i in range(85)if i not in list(range(8))+[r['sourceIndex']for r in styles]],limits='CPU conversion only; shader/normal tangent response/import/device unrun. Actual supplied LIGHT or prescribed missing-LIGHT PCVR fallback; no geometry decimation or invented outfit. Per-instance existing current+pending kit bound2; no retained idle caches.')
    (HERE/receipt_name).write_text(json.dumps(receipt,indent=2)+'\n');print('NEW PAYLOAD BYTES',new_bytes,'cumulative',payload_bytes,'<=2GiB; preexisting files unchanged')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--ids',nargs='+',default=['polymer_gold','hattie_golden','roy_golden','anna_golden']);parser.add_argument('--all-remaining',action='store_true');parser.add_argument('--receipt',default='receipt.json');args=parser.parse_args();ids=[k for k in keys[8:]if k not in ['polymer_gold','hattie_golden','roy_golden','anna_golden']]if args.all_remaining else args.ids;main(ids,args.receipt)
