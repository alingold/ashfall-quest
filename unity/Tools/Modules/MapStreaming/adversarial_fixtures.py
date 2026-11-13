"""Create only tiny malformed fixture sources; never modify actual GLBs."""
import copy,json,struct,hashlib
import inventory as inv
base=inv.OUT
original=json.loads((base/'fixture-5121-5120.mapref.json').read_text());source=Path=None
raw=(base/'fixture-5121-5120.glb').read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);binary=raw[28+n:]
rows=[]
def emit(name,change_doc=None,change_bin=None):
 d=copy.deepcopy(doc);b=bytearray(binary)
 if change_doc:change_doc(d)
 if change_bin:change_bin(b)
 js=json.dumps(d,separators=(',',':')).encode();js+=b' '*((-len(js))%4);glb=struct.pack('<III',0x46546c67,2,28+len(js)+len(b))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(b),0x004e4942)+b
 path=base/('bad-'+name+'.glb');path.write_bytes(glb);m=copy.deepcopy(original);m['source'].update(path=str(path.resolve()),sha256=inv.digest(path),length=len(glb),jsonSha256=hashlib.sha256(js).hexdigest(),binOffset=28+len(js),binLength=len(b))
 for k in m['references']:m['references'][k]=d.get(k,[])
 for i,a in enumerate(d['accessors']):
  v=d['bufferViews'][a['bufferView']];m['accessorRanges'][i].update(offset=28+len(js)+v.get('byteOffset',0)+a.get('byteOffset',0),viewOffset=28+len(js)+v.get('byteOffset',0),viewLength=v['byteLength'],count=a['count'])
 dest=base/('bad-'+name+'.mapref.json');inv.write(dest,m);rows.append(str(dest))
emit('out-of-range-index',change_bin=lambda b:b.__setitem__(doc['bufferViews'][3]['byteOffset'],7))
emit('nonfinite-position',change_bin=lambda b:b.__setitem__(slice(0,4),struct.pack('<f',float('nan'))))
emit('view-overrun',lambda d:d['bufferViews'][0].update(byteLength=999999))
emit('sparse',lambda d:d['accessors'][0].update(sparse={}))
emit('cycle',lambda d:d['nodes'][1].update(children=[0]))
emit('singular',lambda d:d['nodes'][0].update(scale=[0,1,1]))
emit('unsupported-mode',lambda d:d['meshes'][0]['primitives'][0].update(mode=5))
emit('required-extension',lambda d:d.update(extensionsRequired=['KHR_draco_mesh_compression']))
inv.write(inv.DOC/'malformed-paths.json',rows)
print('Generated',len(rows),'tiny negative cases')
