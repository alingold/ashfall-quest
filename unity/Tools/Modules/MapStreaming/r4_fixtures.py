from pathlib import Path
import copy,json,struct,hashlib
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/MapStreamingR4/phase05';H=R/'campaign-staging/20261104-three-chats/unity/MapStreaming/phase03'
base=json.loads((H/'fixture-5121-5120.mapref.json').read_text());rows=[]
def manifest(name,m=None,raw=None,reject=True,normal=None):
 p=S/(name+'.mapref.json');p.write_bytes(raw if raw is not None else json.dumps(m,separators=(',',':')).encode());rows.append(dict(path=str(p),reject=reject,normal=normal))
for value in [1.4,'1',True,1.0,None,2,9223372036854775808]:
 m=copy.deepcopy(base);m['schema']=value;manifest('schema-'+str(len(rows)),m)
for token in ['1e0','1.00000000000000001','9223372036854775808']:
 manifest('token-'+str(len(rows)),raw=json.dumps(base,separators=(',',':')).replace('"schema":1','"schema":'+token).encode())
manifest('duplicate',raw=json.dumps(base,separators=(',',':')).replace('"schema":1','"schema":99,"schema":1').encode())
m=copy.deepcopy(base);m['mapId']='bad\ufffd';manifest('utf8',raw=json.dumps(m,ensure_ascii=False).encode().replace(b'\xef\xbf\xbd',b'\xff'))
m=copy.deepcopy(base);m['unknown']=0;manifest('unknown',m)
for field,value in [('count',3.0),('count',True),('offset','904'),('normalized',0)]:
 m=copy.deepcopy(base);m['accessorRanges'][0][field]=value;manifest('range-'+str(len(rows)),m)
# Small local synthetic GLBs only; originals and multi-GB sources are never copied.
raw=(H/'fixture-5121-5120.glb').read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);binary=raw[28+n:]
for i,(scale,normal,expected) in enumerate([([1e200,1e-160,1],[0,127,0],[0,1,0]),([1e-200,1e200,1],[127,0,0],[1,0,0]),([-1e200,1e-160,1],[127,0,0],[-1,0,0]),([1e200,1e200,1e200],[0,0,127],[0,0,-1]),([1e-200,1e-200,1e-200],[0,127,0],[0,1,0]),([1e200,1e-160,1],[0,0,0],[0,0,0])]):
 d=copy.deepcopy(doc);d['nodes']=[dict(mesh=0,scale=scale)];blob=bytearray(binary);v=d['bufferViews'][1];blob[v['byteOffset']:v['byteOffset']+9]=bytes(normal*3)
 js=json.dumps(d,separators=(',',':')).encode();js+=b' '*((-len(js))%4);g=struct.pack('<IIIII',0x46546c67,2,28+len(js)+len(blob),len(js),0x4e4f534a)+js+struct.pack('<II',len(blob),0x004e4942)+blob;p=S/('transform-'+str(i)+'.glb');p.write_bytes(g)
 m=copy.deepcopy(base);m['source'].update(path=str(p),sha256=hashlib.sha256(g).hexdigest(),length=len(g),jsonSha256=hashlib.sha256(js).hexdigest(),binOffset=28+len(js),binLength=len(blob));m['references']={k:d.get(k,[]) for k in m['references']};m['instances'][0].update(node=0,id='node/0/mesh/0/primitive/0',matrix=[scale[0],0,0,0,0,scale[1],0,0,0,0,scale[2],0,0,0,0,1])
 for j,a in enumerate(d['accessors']):
  v=d['bufferViews'][a['bufferView']];m['accessorRanges'][j].update(offset=28+len(js)+v.get('byteOffset',0)+a.get('byteOffset',0),viewOffset=28+len(js)+v.get('byteOffset',0))
 manifest('transform-'+str(i),m,reject=False,normal=expected)
(S/'holdouts.json').write_text(json.dumps(rows));(S/'adapter.mapref.json').write_text(json.dumps(base))
world=dict(schema=1,id=base['mapId'],version='r4-test-only',sourcePath=base['source']['path'],sourceSha256=base['source']['sha256'],sourceRevision=base['source']['revision'],approval='pilot-quarantine',indoor=True,capsules=False,spawns=[],pads=[],surfaces=[dict(id='synthetic-only',semanticOwner='synthetic',colliderRole='structure',materialPath='test-only',texturePath='',matrix=[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1],vertices=[0,0,0,1,0,0,0,0,1],normals=[0,1,0]*3,indices=[0,1,2],uv=[],color=[1,1,1,1],roughness=1,metallic=0,doubleSided=False)])
(S/'world.json').write_text(json.dumps(world));world['sourceSha256']='0'*64;(S/'world-wrong.json').write_text(json.dumps(world))
