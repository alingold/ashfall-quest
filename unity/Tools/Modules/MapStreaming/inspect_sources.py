import json,struct,hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[4]
for r in json.loads((root/'docs/agent-runs/20261104-three-chats/unity/World/conversion.json').read_text(encoding='utf-8-sig')):
 p=Path(r['sourcePath'])
 with p.open('rb') as f:
  h=f.read(20); n=struct.unpack_from('<I',h,12)[0]; d=json.loads(f.read(n))
 print(p, p.stat().st_size, {k:len(d.get(k,[])) for k in ['nodes','meshes','accessors','bufferViews','materials','images']})
 print('modes',sorted({q.get('mode',4) for m in d['meshes'] for q in m['primitives']}),'sparse',sum('sparse'in a for a in d['accessors']),'extensions',d.get('extensionsRequired'),'attrs',sorted({k for m in d['meshes'] for q in m['primitives'] for k in q['attributes']}))
 print('roots',d['scenes'], 'buffers',d['buffers'])
