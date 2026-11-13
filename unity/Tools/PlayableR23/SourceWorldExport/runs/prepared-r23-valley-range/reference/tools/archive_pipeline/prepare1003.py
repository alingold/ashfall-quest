from pathlib import Path
import json,struct,sys,hashlib,os,shutil
R=Path(__file__).resolve().parents[3];A=R/'candidate/local_archive'
def prepare(slug):
 D=A/'maps'/slug/'source';out=D/'geometry.glb';receipt=D/'geometry.json';src=D/'map.glb';digest=hashlib.sha256(src.read_bytes()).hexdigest()
 if receipt.exists() and out.exists():
  old=json.loads(receipt.read_text())
  if old['source']==digest and out.stat().st_size==old['bytes'] and hashlib.sha256(out.read_bytes()).hexdigest()==old['sha256']:return old
 if shutil.disk_usage(A).free<50*1024**3+src.stat().st_size*4:raise RuntimeError('disk reserve')
 blob=src.read_bytes();n,kind=struct.unpack_from('<II',blob,12);j=json.loads(blob[20:20+n]);binary_start=20+n;binary_len,binary_type=struct.unpack_from('<II',blob,binary_start);assert binary_type==0x004e4942
 if set(j.get('extensionsRequired',[]))-{'KHR_mesh_quantization'}:raise ValueError('unsupported required GLB extensions '+str(j['extensionsRequired']))
 binary=bytearray(blob[binary_start+8:binary_start+8+binary_len]);attrs={i for mesh in j['meshes'] for prim in mesh['primitives'] for key,i in prim['attributes'].items() if not key.startswith('JOINTS')}
 converted=0
 for index in sorted(attrs):
  accessor=j['accessors'][index];ct=accessor['componentType']
  if ct==5126:continue
  assert 'sparse' not in accessor and accessor['type'] in ['SCALAR','VEC2','VEC3','VEC4']
  fmt,denom,signed={5120:('b',127,True),5121:('B',255,False),5122:('h',32767,True),5123:('H',65535,False),5125:('I',4294967295,False)}[ct];components={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[accessor['type']];packed=struct.Struct('<'+fmt*components)
  view=j['bufferViews'][accessor['bufferView']];assert view.get('buffer',0)==0
  start=view.get('byteOffset',0)+accessor.get('byteOffset',0);stride=view.get('byteStride',packed.size);values=[]
  for i in range(accessor['count']):
   row=packed.unpack_from(binary,start+i*stride)
   values.extend((max(-1.0,v/denom) if signed else v/denom) if accessor.get('normalized') else float(v) for v in row)
  binary.extend(b'\x00'*((-len(binary))%4));offset=len(binary);data=struct.pack('<'+'f'*len(values),*values);binary.extend(data)
  accessor.update(bufferView=len(j['bufferViews']),componentType=5126,byteOffset=0);accessor.pop('normalized',None);j['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':len(data),'target':34962});converted+=1
 j['buffers']=[{'byteLength':len(binary)}]
 for key in ['extensionsRequired','extensionsUsed']:
  if key in j:j[key]=[v for v in j[key] if v!='KHR_mesh_quantization']
 for key in ['images','textures','materials','samplers']:j.pop(key,None)
 for mesh in j['meshes']:
  for prim in mesh['primitives']:prim.pop('material',None)
 header=json.dumps(j,separators=(',',':')).encode();header+=b' '*((-len(header))%4);binary.extend(b'\x00'*((-len(binary))%4));tail=struct.pack('<II',len(binary),0x004e4942)+binary
 result=struct.pack('<III',0x46546c67,2,20+len(header)+len(tail))+struct.pack('<II',len(header),0x4e4f534a)+header+tail;out.write_bytes(result)
 row={'source':digest,'sha256':hashlib.sha256(result).hexdigest(),'bytes':len(result),'expandedAccessors':converted};receipt.write_text(json.dumps(row,indent=2)+'\n');return row
if __name__=='__main__':print(json.dumps(prepare(sys.argv[1])))
