"""CPU validation of a future real export. Does not generate world geometry."""
from pathlib import Path
import argparse,hashlib,json,math,struct
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def require(ok,message):
    if not ok:raise ValueError(message)

def validate_multimeshes(rows):
    identity=[1,0,0,0,1,0,0,0,1,0,0,0]
    for row in rows:
        count=row['instanceCount'];instances=row['instances']
        require(len(instances)==count,'MultiMesh instance count mismatch')
        captured=row.get('transformSource')=='original _flush_batches input capture'
        buffer=row.get('sourceBuffer') or {}
        require(count==0 or captured or buffer.get('count',0)>0,
                'MultiMesh poses unavailable: empty renderer buffer without source input capture')
        require(count<2 or any(item['localTransform']!=identity for item in instances),
                'MultiMesh has only identity poses; reject incomplete architecture')
        for index,item in enumerate(instances):
            require(item['index']==index,'MultiMesh source ordering changed')
            for key in ['localTransform','globalTransform']:
                require(len(item[key])==12 and all(math.isfinite(x) for x in item[key]),'Invalid MultiMesh transform')
def validate(folder):
    folder=Path(folder).resolve();marker=json.loads((folder/'complete.json').read_text());d=json.loads((folder/'world.json').read_text());binary=folder/'payload.bin'
    require(marker.get('complete') is True and d.get('complete') is True,'No complete genuine export')
    require(sha(folder/'world.json')==marker['worldSha256'],'world.json digest mismatch')
    require(sha(binary)==d['payloadSha256']==marker['payloadSha256'],'payload digest mismatch')
    require(binary.stat().st_size==d['bytes'],'payload length mismatch');require(d['byteOrder'] in ['little','big'],'Unknown source endianness')
    endian='<' if d['byteOrder']=='little' else '>';data=binary.read_bytes();blobs=0
    def inspect(value):
        nonlocal blobs
        if isinstance(value,list):
            for child in value:inspect(child)
        elif isinstance(value,dict):
            if 'blob' in value:
                offset,length=value['offset'],value['bytes'];expected=value['count']*value['components']*value['scalarBytes']
                require(value['blob']=='payload.bin' and isinstance(offset,int) and isinstance(length,int),'Invalid span descriptor')
                require(0<=offset<=len(data) and 0<=length<=len(data)-offset and expected==length,'Packed array span outside payload or wrong stride');blobs+=1
            else:
                for child in value.values():inspect(child)
    inspect(d);require(blobs>0,'No actual packed array data')
    mesh_ids={mesh['id'] for mesh in d['meshes']};require(len(mesh_ids)==len(d['meshes']) and mesh_ids,'Missing or duplicate source meshes')
    vertices=0;triangles=0
    for mesh in d['meshes']:
        require(mesh['surfaces'],'Source mesh has no surfaces')
        for surface in mesh['surfaces']:
            arrays=surface['arrays'];require(isinstance(arrays,list) and len(arrays)>=13,'Mesh array slot layout')
            v=arrays[0];require(isinstance(v,dict) and v.get('components')==3 and v.get('count',0)>0,'Actual3D vertex arrays required')
            require(v['scalarBytes'] in [4,8],'Unknown position precision')
            values=struct.unpack_from(endian+('f' if v['scalarBytes']==4 else 'd')*(v['count']*3),data,v['offset'])
            require(all(math.isfinite(x) for x in values),'Nonfinite source vertex position');vertices+=v['count']
            indices=arrays[12];count=v['count']
            if indices is not None and indices['count']>0:
                require(indices['scalarBytes'] in [4,8] and indices['components']==1,'Index array type')
                ix=struct.unpack_from(endian+('i' if indices['scalarBytes']==4 else 'q')*indices['count'],data,indices['offset'])
                require(all(0<=x<v['count'] for x in ix),'Index outside source vertex array');count=len(ix)
            if surface['primitive']==3:require(count%3==0,'Source triangle list incomplete');triangles+=count//3
    for instance in d['meshInstances']+d['multiMeshes']:
        require(instance['mesh'] in mesh_ids,'Render instance references missing source mesh')
        require(len(instance['globalTransform'])==12,'Actual source transform required')
    validate_multimeshes(d['multiMeshes'])
    require(d['staticBodies'],'No source collision bodies')
    resources={r['id']:r for r in d['resources']}
    for body in d['staticBodies']:
        for shape in body['shapes']:
            require(shape['shape']['resourceRef'] in resources,'Collision shape resource missing')
            require(len(shape['globalTransform'])==12 and len(shape['ownerTransform'])==12,'Collision owner transform missing')
    texture_count=0
    for resource in d['resources']:
        image=resource.get('image')
        if image:
            image_file=(folder/image['rawFile']).resolve();require(image_file.is_relative_to(folder),'Texture path escapes export')
            require(image_file.stat().st_size==image['bytes'] and sha(image_file)==image['sha256'],'Exact source image digest/length mismatch');texture_count+=1
        if 'shaderFile' in resource:
            shader=(folder/resource['shaderFile']).resolve();require(shader.is_relative_to(folder) and sha(shader)==resource['shaderSha256'],'Exact source shader missing')
    if d['map']=='range':
        require(d['spawn']==[0,.2,608] or all(abs(a-b)<1e-5 for a,b in zip(d['spawn'],[0,.2,608])),'Actual Range source spawn/placement')
        require(len(d['meshInstances'])==4 and len(d['staticBodies'])==4 and len(d['multiMeshes'])==0,'Actual Range four surfaces/four collider bodies')
        expected=[(0,-.2,600),(0,8,562),(-45,4,600),(45,4,600)]
        actual=[tuple(body['shapes'][0]['globalTransform'][9:]) for body in d['staticBodies']]
        require(all(any(all(abs(a-b)<1e-4 for a,b in zip(at,e)) for at in actual) for e in expected),'Actual Range source collider centers')
    elif d['map']=='valley':
        require(d['spawn']==[0,1,8],'Actual Valley source spawn')
        require(sum(row['path'].split('/')[-1].startswith('Mountain_') for row in d['meshInstances'])==24,'Actual24 generated source massifs')
        require(sum(row['path'].split('/')[-1]=='MountainTerrainSurface' for row in d['staticBodies'])==24,'Actual24 source massif collision surfaces')
        require(any(row['class']=='NoiseTexture2D' and row.get('image') for row in d['resources']),'Actual source generated noise image absent')
        require(d['multiMeshes'],'Actual source batched architecture absent')
    else:raise ValueError('Unsupported map id')
    return {'complete':True,'map':d['map'],'meshes':len(mesh_ids),'vertices':vertices,'uniqueMeshTriangles':triangles,'meshInstances':len(d['meshInstances']),'multiMeshes':len(d['multiMeshes']),'staticBodies':len(d['staticBodies']),'exactTextureImages':texture_count,'arrayBlobs':blobs,'unsupported':len(d['unsupported'])}
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('folder',type=Path);args=p.parse_args();print(json.dumps(validate(args.folder),indent=2))
