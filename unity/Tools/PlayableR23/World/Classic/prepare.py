"""CPU-only Classic conversion, staged outside Assets. No original/shared writes."""
from pathlib import Path
import collections,hashlib,json,mmap,re,shutil,struct,time,ctypes
import numpy as np
from spawn import TriangleScene,fallback
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];OUT=HERE/'StagedPayload';R23=Path(r'C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
SOURCE=Path(r'C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps/classic-bigbox-vr')
EXPECTED='0179873e65c372825682725737bfcd34762132cf9d759342425fff0d2bb8079d';ADAPTER='a687f6b9bb18aceadeb030f0ce91eae5630b7d52ee87217fd06048fdfeed35d1'
def sha(p):
    h=hashlib.sha256()
    with p.open('rb')as f:
        for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
    return h.hexdigest()
def main():
    started=time.monotonic();src=SOURCE/'source/map.glb';assert sha(src)==EXPECTED
    sem=json.loads((R23/'assets/data/map_semantics_r14.json').read_text(encoding='utf-8'))['maps']['classic-bigbox-vr'];archive=json.loads((R23/'assets/data/archive_semantics_r9.json').read_text(encoding='utf-8'))['maps']['archive:classic-bigbox-vr'];adapter=json.loads((R23/'assets/data/archive_adapters_r10.json').read_text(encoding='utf-8'))['files']['classic-bigbox-vr']
    meta=json.loads((SOURCE/'converted/manifest.json').read_text(encoding='utf-8'));prov=json.loads((SOURCE/'source/provenance.json').read_text(encoding='utf-8'))
    assert sem['source_sha256']==archive['source_sha256']==meta['source_sha256']==prov['glb']['sha256']==EXPECTED and adapter==ADAPTER
    assert not meta['recorded_spawns']and not sem['deathmatch']and not sem['warmup']
    OUT.mkdir(exist_ok=True);textures={};materials=[];material_ids={}
    for key,row in prov['materials'].items():
        result=dict(name=key,color='',normal='',metallic=.35 if row.get('metal')else 0,roughness=.72,alpha=bool(row.get('alpha')),vertexColor=True,normalScale=.6,cull=2,transparency=1 if row.get('alpha')else 0)
        for field,name in [('c','color'),('n','normal')]:
            if field not in row:continue
            p=SOURCE.parents[1]/'shared'/row[field];assert p.is_file(),p
            dest=key+'_'+field+p.suffix;shutil.copyfile(p,OUT/dest);digest=sha(p);assert sha(OUT/dest)==digest
            result[name]=dest;textures[dest]=dict(source=str(p),sha256=digest,bytes=p.stat().st_size)
        material_ids[key]=len(materials);materials.append(result)
    stream=src.open('rb');raw=mmap.mmap(stream.fileno(),0,access=mmap.ACCESS_READ);n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);binary_offset=28+n
    def accessor(i):
        a=doc['accessors'][i];v=doc['bufferViews'][a['bufferView']];assert 'sparse'not in a
        dt=np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]);width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        x=np.ndarray((a['count'],width),dt,buffer=raw,offset=binary_offset+v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',width*dt.itemsize),dt.itemsize))
        if a.get('normalized'):return np.maximum(-1,x.astype(np.float32)/np.iinfo(dt).max)
        return x
    def local(node):
        if 'matrix'in node:return np.array(node['matrix']).reshape(4,4,order='F')
        x,y,z,w=node.get('rotation',[0,0,0,1]);m=np.eye(4);m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(node.get('scale',[1,1,1]));m[:3,3]=node.get('translation',[0,0,0]);return m
    batches=collections.OrderedDict();collision_parts=[];nodes=0;source_triangles=0;negative=0;aligned=0;zero_fixed=0;vertices_total=0;node_bounds=[];part_sources={}
    def walk(i,parent):
        nonlocal nodes,source_triangles,negative,aligned,zero_fixed,vertices_total
        node=doc['nodes'][i];m=parent@local(node)
        if 'mesh'in node:
            match=re.fullmatch(r'(world|roof)_(-?\d+)_(-?\d+)_(.+)',node.get('name',''));assert match,'Unsupported Classic source role: '+node.get('name','')
            nodes+=1;kind,cx,cz,key=match.groups();mid=material_ids[key];cell=(int(cx)//4,int(cz)//4)
            for primitive_index,primitive in enumerate(doc['meshes'][node['mesh']]['primitives']):
                assert primitive.get('mode',4)==4;a=primitive['attributes'];pos=accessor(a['POSITION']);idx=accessor(primitive['indices']).astype(np.int32).ravel().copy()if'indices'in primitive else np.arange(len(pos),dtype=np.int32)
                assert len(idx)%3==0 and idx.min()>=0 and idx.max()<len(pos)
                xyz=(pos@m[:3,:3].T+m[:3,3]).astype(np.float32);xyz[:,2]*=-1
                norm=(accessor(a['NORMAL'])@np.linalg.inv(m[:3,:3])).astype(np.float32);norm[:,2]*=-1;lengths=np.linalg.norm(norm,axis=1);norm/=np.maximum(lengths[:,None],1e-20)
                if np.linalg.det(m[:3,:3])<0:idx=idx.reshape(-1,3)[:,[0,2,1]].ravel()
                tri=xyz[idx].reshape(-1,3,3);cross=np.cross(tri[:,1]-tri[:,0],tri[:,2]-tri[:,0]);dots=np.sum(cross*norm[idx].reshape(-1,3,3).mean(axis=1),axis=1);valid=np.abs(dots)>1e-8;negative+=int((dots[valid]<0).sum());aligned+=int(valid.sum())
                bad=lengths<1e-10
                if bad.any():
                    derived=np.zeros_like(norm)
                    for k in range(3):np.add.at(derived,idx.reshape(-1,3)[:,k],-cross)
                    size=np.linalg.norm(derived,axis=1);derived/=np.maximum(size[:,None],1e-20);derived[size<1e-10]=[0,1,0];norm[bad]=derived[bad];zero_fixed+=int(bad.sum())
                uv=accessor(a['TEXCOORD_0']).astype(np.float32).copy()if'TEXCOORD_0'in a else np.zeros((len(pos),2),np.float32);uv[:,1]=1-uv[:,1]
                col=accessor(a['COLOR_0']).astype(np.float32)if'COLOR_0'in a else np.ones((len(pos),4),np.float32)
                if col.shape[1]==3:col=np.column_stack((col,np.ones(len(pos),np.float32)))
                vertex=np.column_stack((xyz,norm,uv,col)).astype('<f4');assert np.isfinite(vertex).all()and vertex.shape[1]==12
                batches.setdefault((mid,cell),[]).append((vertex,idx));collision_parts.append(tri);source_triangles+=len(idx)//3;vertices_total+=len(vertex)
                part_sources[id(vertex)]=dict(nodeIndex=i,primitiveIndex=primitive_index,vertices=len(vertex),indices=len(idx))
                node_bounds.append(dict(node=node['name'],minimum=xyz.min(axis=0).tolist(),maximum=xyz.max(axis=0).tolist(),triangles=len(idx)//3))
        for child in node.get('children',[]):walk(child,m)
    for i in doc['scenes'][doc.get('scene',0)]['nodes']:walk(i,np.eye(4))
    assert source_triangles==meta['solid_triangles']==1319789 and nodes==1946 and negative/aligned>.99
    chunks=[]
    for(mid,cell),parts in batches.items():
        current=[];count=0
        for vertex,idx in parts:
            if count+len(vertex)>60000 and current:chunks.append((mid,current));current=[];count=0
            assert len(vertex)<=60000;current.append((vertex,idx));count+=len(vertex)
        if current:chunks.append((mid,current))
    assert len(chunks)<=2048
    (HERE/'chunk-sources.json').write_text(json.dumps([dict(material=mid,parts=[part_sources[id(v)]for v,idx in parts])for mid,parts in chunks],indent=2)+'\n')
    print('Geometry decoded:',nodes,'source nodes;',source_triangles,'triangles;',len(chunks),'chunks. Deriving original floor-grid spawn...',flush=True)
    scene=TriangleScene(collision_parts);game,warmup,spawn_proof=fallback(scene,meta)
    geometry=OUT/'geometry.bytes'
    with geometry.open('wb')as f:
        f.write(struct.pack('<ii',0x52323357,len(chunks)))
        for mid,parts in chunks:
            total=sum(len(v)for v,i in parts);ii=[];offset=0
            for vertex,idx in parts:ii.append(idx+offset);offset+=len(vertex)
            indices=np.concatenate(ii).astype('<i4');f.write(struct.pack('<iiiii',mid,1,1,total,len(indices)))
            for vertex,idx in parts:f.write(vertex.tobytes())
            f.write(indices.tobytes())
    low=meta['bounds_min'];high=meta['bounds_max'];manifest=dict(version=1,mapId=meta['map_id'],title=meta['title'],sourceSha256=EXPECTED,spawn=game,gameSpawn=game,warmupSpawn=warmup,boundsMin=[low[0],low[1],-high[2]],boundsMax=[high[0],high[1],-low[2]],materials=materials,chunks=len(chunks))
    manifest['cameraFarDistance']=max(2000,float(np.linalg.norm(np.array(meta['solid_max'])-meta['solid_min']))*1.5+200)
    (OUT/'map.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (HERE/'spawn-proof.json').write_text(json.dumps(spawn_proof,indent=2)+'\n');(HERE/'source-node-census.json').write_text(json.dumps(node_bounds,indent=2)+'\n')
    source_inputs={str(p):sha(p)for p in [src,SOURCE/'source/provenance.json',SOURCE/'source/map.json',SOURCE/'converted/manifest.json',R23/'assets/data/map_semantics_r14.json',R23/'assets/data/archive_semantics_r9.json',R23/'assets/data/archive_adapters_r10.json',R23/'scripts/archive_map1003.gd',R23/'scripts/community_map1002.gd',R23/'scripts/player.gd',R23/'tools/convert_archive1003.gd']}
    asset_bytes=sum(p.stat().st_size for p in OUT.iterdir()if p.is_file());assert asset_bytes<=300*1024*1024
    class Memory(ctypes.Structure):
        _fields_=[('cb',ctypes.c_ulong),('faults',ctypes.c_ulong)]+[(s,ctypes.c_size_t)for s in ['peakWorkingSet','workingSet','peakPagedPool','pagedPool','peakNonPagedPool','nonPagedPool','pagefile','peakPagefile']]
    memory=Memory();memory.cb=ctypes.sizeof(memory);ctypes.windll.psapi.GetProcessMemoryInfo(ctypes.c_void_p(-1),ctypes.byref(memory),memory.cb)
    report=dict(mapId=meta['map_id'],sourceGlbSha256=EXPECTED,originalPreparedAdapterSha256=ADAPTER,hashClarification='a687... is the original prepared .res adapter hash; 017987... is the raw map.glb identity verified by both original semantic datasets.',sourceInputs=source_inputs,textureInputs=textures,nodes=nodes,sourceVertices=vertices_total,renderTriangles=source_triangles,collisionTriangles=source_triangles,chunks=len(chunks),materials=len(materials),textures=len(textures),missingTextures=[],zeroSourceNormalsGeometricallyRepaired=zero_fixed,clockwiseNormalAgreement=negative/aligned,spawn=game,warmupSpawn=warmup,spawnOrigin='original floor-grid fallback; no recorded/authored spawn rows',payloadBytes=asset_bytes,geometrySha256=sha(geometry),elapsedSeconds=round(time.monotonic()-started,3),peakWorkingSetBytes=memory.peakWorkingSet,sourceCameraFarDistance=max(2000,float(np.linalg.norm(np.array(meta['solid_max'])-meta['solid_min']))*1.5+200),sourceLicense=meta['license'],engineRun=False,renderRun=False,deviceRun=False,limits=['Native mesh cooking/render/entry unverified.','v1 loader retains its existing alpha-cutout interpretation of the three source alpha-blended material roles.','Original raw geometry/static roles retained; prepared destructible scripts, fixtures, art repairs and runtime hazards are not separately ported.','CPU fallback uses original solid triangles and source capsule/footprint rules, without instantiating Godot prepared adapters.'])
    (OUT/'provenance.json').write_text(json.dumps(report,indent=2)+'\n');(OUT/'SOURCE-LICENSE.txt').write_text(meta['license']+'\nSource: '+meta['source']+'\nCreator: '+meta['creator']+'\n',encoding='utf-8')
    report['payloadBytes']=sum(p.stat().st_size for p in OUT.iterdir()if p.is_file());(OUT/'provenance.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({k:v for k,v in report.items()if k not in ['sourceInputs','textureInputs']},indent=2),flush=True)
if __name__=='__main__':main()
