from pathlib import Path
import importlib.util,sys,json,copy,struct,hashlib,shutil
spec=importlib.util.spec_from_file_location('checks',Path(__file__).with_name('r4_checks.py'));c=importlib.util.module_from_spec(spec);spec.loader.exec_module(c)
original=c.S;c.S=original/'extra-final';c.S.mkdir(exist_ok=False);c.O=c.O/'extra-final';c.O.mkdir(exist_ok=False)
base=json.loads((c.H/'fixture-5121-5120.mapref.json').read_text());raw=(c.H/'fixture-5121-5120.glb').read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);blob=raw[28+n:];paths=[]
for name,edit in [('fractional-count',lambda d:d['accessors'][0].update(count=3.0)),('string-count',lambda d:d['accessors'][0].update(count='3')),('bool-buffer',lambda d:d['bufferViews'][0].update(buffer=False)),('float-mesh',lambda d:d['nodes'][1].update(mesh=0.0)),('component-overflow',lambda d:d['accessors'][0].update(componentType=4294972422)),('numeric-normalized',lambda d:d['accessors'][1].update(normalized=1))]:
 d=copy.deepcopy(doc);edit(d);js=json.dumps(d,separators=(',',':')).encode();js+=b' '*((-len(js))%4);g=struct.pack('<IIIII',0x46546c67,2,28+len(js)+len(blob),len(js),0x4e4f534a)+js+struct.pack('<II',len(blob),0x004e4942)+blob;p=c.S/(name+'.glb');p.write_bytes(g)
 m=copy.deepcopy(base);m['source'].update(path=str(p),length=len(g),sha256=hashlib.sha256(g).hexdigest(),jsonSha256=hashlib.sha256(js).hexdigest(),binOffset=28+len(js),binLength=len(blob));m['references']={k:d.get(k,[]) for k in m['references']}
 for j,a in enumerate(d['accessors']):
  v=d['bufferViews'][a['bufferView']];m['accessorRanges'][j].update(offset=28+len(js)+v.get('byteOffset',0)+a.get('byteOffset',0),viewOffset=28+len(js)+v.get('byteOffset',0))
 p=c.S/(name+'.mapref.json');p.write_text(json.dumps(m));paths.append(p)
world=json.loads((original/'world.json').read_text());world['approval']='accepted';p=c.S/'accepted-test-only.json';p.write_text(json.dumps(world))
prod=list((original/'final2/candidate').glob('*.cs'))+list((original/'dependencies').rglob('*.cs'))
tool=c.T/'R4BoundaryHoldouts.cs';shutil.copyfile(tool,c.S/tool.name)
exe=c.compile('BoundaryHoldouts',prod+[c.S/tool.name]);c.run([exe,original/'adapter.mapref.json',p,*paths])
