from pathlib import Path
import json, hashlib, struct, base64, subprocess, shutil
ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/Avatar/r3'
EV=ROOT/'docs/agent-runs/20261104-three-chats/unity/Avatar/phase03'
if (EV/'candidate-manifest.json').exists(): raise SystemExit('Frozen r3; allocate a new revision')
OUT.mkdir(parents=True,exist_ok=True); EV.mkdir(parents=True,exist_ok=True)
records=[]
def sha(b):return hashlib.sha256(b).hexdigest().upper()
def glb(d,b):
    d=json.loads(json.dumps(d)); d['buffers'][0].pop('uri',None)
    j=json.dumps(d,separators=(',',':')).encode();j+=b' '*((-len(j))%4);b+=b'\0'*((-len(b))%4)
    return struct.pack('<III',0x46546c67,2,28+len(j)+len(b))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(b),0x004e4942)+b
for side in 'LR':
    p=ROOT/f'game/assets/hands/xrtools086/Hand_Glove_low_{side}.gltf';raw=p.read_bytes();d=json.loads(raw)
    snap=OUT/'inputs'/p.name;snap.parent.mkdir(exist_ok=True);snap.write_bytes(raw)
    b=base64.b64decode(d['buffers'][0]['uri'].split(',')[1]);node=next(n['name'] for n in d['nodes'] if 'mesh'in n)
    (OUT/f'{side}.gltf').write_bytes(raw);(OUT/f'{side}.glb').write_bytes(glb(d,b))
    def access(i):
        a=d['accessors'][i];v=d['bufferViews'][a['bufferView']];width={'VEC3':3,'VEC4':4,'VEC2':2,'MAT4':16,'SCALAR':1}[a['type']];fmt={5126:'f',5123:'H',5121:'B',5125:'I'}[a['componentType']];size=struct.calcsize(fmt);start=v.get('byteOffset',0)+a.get('byteOffset',0);stride=v.get('byteStride',size*width)
        return [struct.unpack_from('<'+fmt*width,b,start+k*stride) for k in range(a['count'])]
    prim=d['meshes'][0]['primitives'][0];at=prim['attributes'];pos=access(at['POSITION']);ix=access(prim['indices']);js=access(at['JOINTS_0']);ws=access(at['WEIGHTS_0'])
    # Independent scalar observations for decoder and SRS comparison.
    summary=[node,len(pos),len(ix),len(d['skins'][0]['joints']),len(d['nodes']),pos[0][0],pos[0][1],-pos[0][2],ix[0][0],ix[2][0],ix[1][0]]
    (OUT/f'{side}.expected').write_text('\n'.join(map(str,summary)))
    records.append(dict(path=str(p),sha256=sha(raw),bytes=len(raw),node=node,vertices=len(pos),indices=len(ix),joints=len(d['skins'][0]['joints']),nodes=len(d['nodes']),multiInfluenced=sum(sum(w>0 for w in row)>1 for row in ws),sourceTree='game',revision='differing runtime; hash pinned; UNREVIEWED_SOURCE_REFERENCE'))
    # Real-file negative variants retain all difficult geometry.
    for key in ['cycle','joint','weight','range','uri','duplicate','sparse','missing','stride','normalized','scheme','encoded','root','zero','nan']:
        x=json.loads(raw)
        if key=='cycle':x['nodes'][0]['children']=[0]
        if key=='joint':x['skins'][0]['joints'][0]=999
        if key=='weight':x['accessors'][at['WEIGHTS_0']]['count']=1
        if key=='range':x['accessors'][0]['byteOffset']=2**31-1
        if key=='uri':x['buffers'][0]['uri']='../bad.bin'
        if key=='duplicate':x['skins'][0]['joints'][1]=x['skins'][0]['joints'][0]
        if key=='sparse':x['accessors'][0]['sparse']={}
        if key=='missing':del x['meshes'][0]['primitives'][0]['attributes']['POSITION']
        if key=='stride':x['bufferViews'][x['accessors'][0]['bufferView']]['byteStride']=1
        if key=='normalized':x['accessors'][at['JOINTS_0']]['normalized']=True
        if key=='scheme':x['buffers'][0]['uri']='https://bad.invalid/rig.bin'
        if key=='encoded':x['buffers'][0]['uri']='%2e%2e/rig.bin'
        if key=='root':x['skins'][0]['skeleton']=0
        if key in ['zero','nan']:
            bb=bytearray(b);aa=x['accessors'][at['WEIGHTS_0']];vv=x['bufferViews'][aa['bufferView']];off=vv.get('byteOffset',0)+aa.get('byteOffset',0);struct.pack_into('<4f',bb,off,*([0]*4 if key=='zero' else [float('nan'),0,0,0]));x['buffers'][0]['uri']='data:application/octet-stream;base64,'+base64.b64encode(bb).decode()
        (OUT/f'{side}-{key}.gltf').write_text(json.dumps(x))
# Analytic two-bone triangle, translation bind and mesh node offset. Expected bent vertex is hand calculated.
b=bytearray();views=[];acs=[]
def add(values,typ,ct,fmt):
    while len(b)%4:b.append(0)
    start=len(b);flat=[x for row in values for x in row];b.extend(struct.pack('<'+fmt*len(flat),*flat));views.append(dict(buffer=0,byteOffset=start,byteLength=len(b)-start));acs.append(dict(bufferView=len(views)-1,componentType=ct,count=len(values),type=typ));return len(acs)-1
p=add([(1,0,2),(2,0,2),(1,1,2)],'VEC3',5126,'f');n=add([(0,0,1)]*3,'VEC3',5126,'f');u=add([(0,0),(1,0),(0,1)],'VEC2',5126,'f');j=add([(0,1,0,0)]*3,'VEC4',5121,'B');w=add([(0.25,0.75,0,0)]*3,'VEC4',5126,'f');ix=add([(0,),(1,),(2,)],'SCALAR',5123,'H')
# inverse joint worlds times mesh world: joint0 identity, joint1 translate(+1,0,0); mesh translate(0,0,2)
bm=add([(1,0,0,0,0,1,0,0,0,0,1,0,0,0,2,1),(1,0,0,0,0,1,0,0,0,0,1,0,-1,0,2,1)],'MAT4',5126,'f')
d=dict(asset={'version':'2.0'},buffers=[dict(byteLength=len(b),uri='data:application/octet-stream;base64,'+base64.b64encode(b).decode())],bufferViews=views,accessors=acs,nodes=[dict(name='root',children=[1,2]),dict(name='joint',translation=[1,0,0]),dict(name='mesh',mesh=0,skin=0,translation=[0,0,2])],skins=[dict(joints=[0,1],inverseBindMatrices=bm,skeleton=0)],meshes=[dict(primitives=[dict(attributes=dict(POSITION=p,NORMAL=n,TEXCOORD_0=u,JOINTS_0=j,WEIGHTS_0=w),indices=ix)])])
(OUT/'analytic.gltf').write_text(json.dumps(d));(OUT/'analytic.glb').write_bytes(glb(d,bytes(b)))
# Normalized unsigned-byte weights exercise actual glTF integer influence decoding.
x=json.loads(json.dumps(d));bb=bytearray(b);aa=x['accessors'][w];vv=x['bufferViews'][aa['bufferView']];off=vv.get('byteOffset',0);bb[off:off+12]=bytes([64,191,0,0]*3);aa['componentType']=5121;aa['normalized']=True;x['buffers'][0]['uri']='data:application/octet-stream;base64,'+base64.b64encode(bb).decode();(OUT/'normalized.gltf').write_text(json.dumps(x))
# External dependency closure variants.
x=json.loads(json.dumps(d));x['buffers'][0]['uri']='rig.bin';(OUT/'external.gltf').write_text(json.dumps(x));(OUT/'rig.bin').write_bytes(b)
def wire_string(s):
    b=s.encode();n=len(b);prefix=bytearray()
    while n>=128:prefix.append((n&127)|128);n>>=7
    prefix.append(n);return bytes(prefix)+b
(OUT/'external.closure').write_text(sha(wire_string(sha((OUT/'external.gltf').read_bytes()))+wire_string('rig.bin')+wire_string(sha(bytes(b)))))
(EV/'source-inputs.json').write_text(json.dumps(records,indent=2))
compiler=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs']+sorted((ROOT/'unity/Assets/Ashfall/Modules/Avatar').rglob('*.cs'))
for test in ['AvatarTests','AvatarR2Tests','AvatarR3Tests']:
    exe=OUT/(test+'.exe');args=[str(compiler),'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+list(map(str,sources))+[str(Path(__file__).parent/(test+'.cs'))]
    log='COMMAND '+' '.join('"'+a+'"' for a in args)+'\n';r=subprocess.run(args,capture_output=True,text=True);log+=r.stdout+r.stderr+'\nCOMPILE EXIT '+str(r.returncode)+'\n'
    if r.returncode:(EV/(test+'.txt')).write_text(log);raise SystemExit(log)
    r=subprocess.run([str(exe)],cwd=OUT,capture_output=True,text=True);log+='COMMAND "'+str(exe)+'"\n'+r.stdout+r.stderr+'\nRUN EXIT '+str(r.returncode)+'\n';(EV/(test+'.txt')).write_text(log);print(r.stdout,r.stderr)
    if r.returncode:raise SystemExit(r.returncode)



