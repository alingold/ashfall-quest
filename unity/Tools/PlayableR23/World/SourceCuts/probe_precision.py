from pathlib import Path
import json,struct,numpy as np,collections
ROOT=Path(r'C:/Users/aling/Documents/ashfall');data=json.loads(Path(r'D:/ashfall-r6-staging/world/classic-source-cuts-v003/SOURCE-CUT-PARTITIONS.json').read_text());wanted={x['chunk'] for x in data['chunkGuards']};chunks={};owners={x['ownerIndex']:np.array(x['center']) for x in data['appendDefinitions']}
with (ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/classic-bigbox-vr/geometry.bytes').open('rb') as f:
 _,count=struct.unpack('<ii',f.read(8))
 for chunk in range(count):
  mat,col,vis,nv,ni=struct.unpack('<5i',f.read(20));buf=f.read(nv*48);ix=f.read(ni*4)
  if chunk in wanted:chunks[chunk]=(np.frombuffer(buf,dtype='<f4').reshape(-1,12),np.frombuffer(ix,dtype='<i4').reshape(-1,3))
counts=collections.Counter();minlens={}
for face in data['trianglePartitions']:
 verts,idx=chunks[face['chunk']];base=verts[idx[face['triangle']],:3].astype('float64');norm=np.cross(base[1]-base[0],base[2]-base[0]);center=(verts[:,:3].min(0).astype('float64')+verts[:,:3].max(0))/2
 groups=[(-1,face['staticBarycentricTriangles'])]+[(g['ownerIndex'],g['triangles'])for g in face['ownedBarycentricTriangles']]
 for owner,tris in groups:
  for tri in tris:
   p=np.asarray(tri)@base;counts['triangles']+=1
   for mode,anchor in [('world',np.zeros(3)),('owner_or_chunk',owners.get(owner,center)),('rawface',base[0]),('fragment',p.mean(0))]:
    q=(p-anchor).astype('float32').astype('float64');dot=np.dot(np.cross(q[1]-q[0],q[2]-q[0]),norm)
    if dot<=0:counts[mode+('_static' if owner==-1 else '_owner')]+=1
print(dict(counts))
