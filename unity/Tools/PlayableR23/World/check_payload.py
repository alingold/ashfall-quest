"""Focused exported payload readback and image integrity check, no engine."""
import json,struct
from pathlib import Path
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[4]
p=ROOT/'unity/Assets/StreamingAssets/PlayableR23/World';m=json.loads((p/'map.json').read_text())
render=collision=vertices=0;maxVertices=0
with (p/'geometry.bytes').open('rb') as f:
 magic,n=struct.unpack('<ii',f.read(8));assert magic==0x52323357 and n==m['chunks']
 for _ in range(n):
  mid,coll,vis,nv,ni=struct.unpack('<iiiii',f.read(20));assert 0<=mid<len(m['materials']) and ni%3==0
  v=np.frombuffer(f.read(nv*48),dtype='<f4').reshape(nv,12);idx=np.frombuffer(f.read(ni*4),dtype='<i4')
  assert np.isfinite(v).all() and idx.min()>=0 and idx.max()<nv
  assert np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=1e-4)
  vertices+=nv;maxVertices=max(maxVertices,nv);render+=ni//3*vis;collision+=ni//3*coll
 assert f.read()==b''
textures=set()
for mat in m['materials']:
 for k in ['color','normal']:
  if mat[k]:
   textures.add(mat[k])
   with Image.open(p/mat[k]) as im:im.verify()
source=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source/assets/data/map_semantics_r14.json')
sem=json.loads(source.read_text())['maps']['battlefield-paintball-gods-pitdevil']
removed=sum(row['triangles'] for category in ['marker_parts','decorative_collision_parts'] for row in sem.get(category,[]))
assert removed==518 and render==452983 and collision==452999-removed and len(textures)==38
report=dict(chunks=n,vertices=vertices,maxChunkVertices=maxVertices,renderTriangles=render,collisionTrianglesBeforeBackfaces=collision,decodedTextureFiles=len(textures),finiteAttributes=True,indexRangesValid=True,unitNormals=True,completePayloadRead=True)
(ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/World/payload-check.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
