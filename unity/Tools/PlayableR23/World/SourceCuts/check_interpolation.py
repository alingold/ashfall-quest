from pathlib import Path
import json,struct,collections,numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];BASE=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/classic-bigbox-vr';cuts=json.loads(Path(r'D:/ashfall-r6-staging/world/classic-source-cuts-v003/SOURCE-CUT-PARTITIONS.json').read_text());wanted={x['chunk']for x in cuts['chunkGuards']};chunks={}
with (BASE/'geometry.bytes').open('rb')as f:
 _,count=struct.unpack('<ii',f.read(8))
 for c in range(count):
  m,flags,vis,nv,ni=struct.unpack('<5i',f.read(20));v=f.read(nv*48);ix=f.read(ni*4)
  if c in wanted:chunks[c]=(np.frombuffer(v,dtype='<f4').reshape(-1,12).astype('float64'),np.frombuffer(ix,dtype='<i4').reshape(-1,3))
areas=collections.defaultdict(float);counts=collections.Counter();maxp=maxn=maxother=0;max_fragment_relative=0;max_fragment_absolute=0
with (HERE/'assembled-test.bytes').open('rb')as f:
 def array(dtype,count):return np.frombuffer(f.read(np.dtype(dtype).itemsize*count),dtype=dtype)
 while True:
  header=f.read(9)
  if not header:break
  chunk,owner,precise=struct.unpack('<ii?',header);origin=array('<f4',3).astype('float64');basis=array('<f4',9).astype('float64').reshape(3,3);q=array('<f4',4);x,y,z,w=q;basis=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]],dtype='float32').astype('float64');n=struct.unpack('<i',f.read(4))[0];faces=array('<i4',n);bary=array('<f8',n*9).reshape(n,3,3);kinds=array('u1',n);vertices=array('<f4',n*36).astype('float64').reshape(n,3,12);src,ix=chunks[chunk];raw=src[ix[faces]];expected=np.einsum('nij,njk->nik',bary,raw);world=vertices[:,:,:3]@basis.T+origin;normals=vertices[:,:,3:6]@basis.T;expectedn=expected[:,:,3:6];expectedn/=np.linalg.norm(expectedn,axis=2,keepdims=True)
  maxp=max(maxp,float(np.abs(world-expected[:,:,:3]).max()));maxn=max(maxn,float(np.abs(normals-expectedn).max()));maxother=max(maxother,float(np.abs(vertices[:,:,6:]-expected[:,:,6:]).max()))
  cross=np.cross(vertices[:,1,:3]-vertices[:,0,:3],vertices[:,2,:3]-vertices[:,0,:3])@basis.T;rawcross=np.cross(raw[:,1,:3]-raw[:,0,:3],raw[:,2,:3]-raw[:,0,:3]);dot=(cross*rawcross).sum(1);
  if not np.all(dot>0):
   j=int(np.flatnonzero(dot<=0)[0]);print('BAD',chunk,owner,precise,'face',faces[j],'dot',dot[j],'basis',basis,'verts',vertices[j,:,:3],'rawnormal',rawcross[j]);raise AssertionError('nonpositive native winding')
  basearea=np.linalg.norm(rawcross,axis=1);nativearea=dot/basearea;d=(bary[:,1,1]-bary[:,0,1])*(bary[:,2,2]-bary[:,0,2])-(bary[:,1,2]-bary[:,0,2])*(bary[:,2,1]-bary[:,0,1]);expectedarea=d*basearea;max_fragment_absolute=max(max_fragment_absolute,float(np.max(np.abs(nativearea-expectedarea)))*.5);assert np.all(expectedarea>0);max_fragment_relative=max(max_fragment_relative,float(np.max(np.abs(nativearea/expectedarea-1))))
  for i,face in enumerate(faces):areas[chunk,int(face)]+=nativearea[i]/basearea[i]
  counts['triangles']+=n;counts['meshes']+=1;counts['precision_batches']+=precise;counts['dynamic_triangles'if owner>=0 else'static_triangles']+=n
conservation=max(abs(x-1)for x in areas.values());print(dict(counts));print('Max source-world position error',maxp,'normalerror',maxn,'uv/colorerror',maxother,'raw area relativeerror',conservation,'max fragment area relativeerror',max_fragment_relative)
assert len(areas)==10907 and maxp<.00015 and maxn<2e-6 and maxother<.0001 and conservation<.0001
out=dict(counts,maxWorldPositionError=maxp,maxNormalError=maxn,maxUvColorError=maxother,maxRawAreaRelativeError=conservation,maxFragmentAreaRelativeError=max_fragment_relative,maxFragmentAreaAbsoluteErrorSquareMetres=max_fragment_absolute,rawFaces=len(areas),engineExecuted=False);(HERE/'interpolation-checks.json').write_text(json.dumps(out,indent=2)+'\n')
