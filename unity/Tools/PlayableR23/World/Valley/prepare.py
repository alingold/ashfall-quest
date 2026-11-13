"""Actual exported Valley -> v2 payload. Full publication rejects lost MultiMesh poses.
No engine/source mutation/no procedural terrain reconstruction. --diagnostic-direct
stages a noncatalog partial under this tool only, never StreamingAssets.
"""
from pathlib import Path
import argparse,collections,hashlib,itertools,json,math,shutil,struct
import numpy as np
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
DEFAULT=ROOT/'unity/Tools/PlayableR23/SourceWorldExport/runs/prepared-r23-valley-range/valley'
DEST=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/valley'
REFLECT=np.diag([1.,1.,-1.])
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def unpack(value,default=None):
    if value is None:return default
    if isinstance(value,dict):
        for key in ['vector3','vector2','colorUnconverted','colorLinearUnconverted','transform3D']:
            if key in value:return value[key]
    return value
def transform(values):
    a=np.asarray(values,float);assert a.shape==(12,) and np.isfinite(a).all();return a[:9].reshape(3,3,order='F'),a[9:]
def quaternion(matrix):
    m=np.asarray(matrix,float);trace=np.trace(m)
    if trace>0:
        s=math.sqrt(trace+1)*2;q=[(m[2,1]-m[1,2])/s,(m[0,2]-m[2,0])/s,(m[1,0]-m[0,1])/s,.25*s]
    else:
        i=int(np.argmax(np.diag(m)));j=(i+1)%3;k=(i+2)%3;s=math.sqrt(max(0,1+m[i,i]-m[j,j]-m[k,k]))*2;q=[0.,0.,0.,0.];q[i]=.25*s;q[j]=(m[i,j]+m[j,i])/s;q[k]=(m[i,k]+m[k,i])/s;q[3]=(m[k,j]-m[j,k])/s
    q=np.asarray(q);return (q/np.linalg.norm(q)).tolist()
def hull_indices(points):
    """Enumerate supporting planes of <=128 actual convex points; no hull inflation."""
    points=np.asarray(points,float);planes={};tolerance=max(1.,np.ptp(points,axis=0).max())*2e-6
    for a,b,c in itertools.combinations(range(len(points)),3):
        normal=np.cross(points[b]-points[a],points[c]-points[a]);length=np.linalg.norm(normal)
        if length<tolerance*tolerance:continue
        normal/=length;distance=float(normal@points[a]);signed=points@normal-distance
        if signed.max()<=tolerance:pass
        elif signed.min()>=-tolerance:normal=-normal;distance=-distance
        else:continue
        on=tuple(np.where(np.abs(points@normal-distance)<=tolerance)[0]);planes.setdefault(on,normal)
    triangles=[]
    for members,normal in planes.items():
        if len(members)<3:continue
        ids=list(members);center=points[ids].mean(0);u=points[ids[0]]-center;u/=np.linalg.norm(u);v=np.cross(normal,u)
        xy=np.column_stack(((points[ids]-center)@u,(points[ids]-center)@v));order=sorted(range(len(ids)),key=lambda i:(xy[i,0],xy[i,1]))
        def cross(a,b,c):
            ab=xy[b]-xy[a];ac=xy[c]-xy[a];return ab[0]*ac[1]-ab[1]*ac[0]
        chain=[]
        for i in order:
            while len(chain)>=2 and cross(chain[-2],chain[-1],i)<=tolerance*tolerance:chain.pop()
            chain.append(i)
        lower=len(chain)
        for i in order[-2::-1]:
            while len(chain)>lower and cross(chain[-2],chain[-1],i)<=tolerance*tolerance:chain.pop()
            chain.append(i)
        polygon=[ids[i] for i in chain[:-1]]
        for i in range(1,len(polygon)-1):triangles.append([polygon[0],polygon[i],polygon[i+1]])
    result=np.asarray(triangles,np.int32);assert len(result)>=4
    # Every cloud point lies on/inside every actual hull face.
    for tri in result:
        n=np.cross(points[tri[1]]-points[tri[0]],points[tri[2]]-points[tri[0]]);assert ((points-points[tri[0]])@n).max()<=tolerance*np.linalg.norm(n)*2
    return result.ravel()
class Converter:
    def __init__(self,source,diagnostic):
        self.source=Path(source);self.data=json.loads((self.source/'world.json').read_text());self.raw=(self.source/'payload.bin').read_bytes();self.resources={r['id']:r for r in self.data['resources']};self.meshes={m['id']:m for m in self.data['meshes']};self.diagnostic=diagnostic
        marker=json.loads((self.source/'complete.json').read_text());assert marker['complete'] and sha(self.source/'world.json')==marker['worldSha256']
        assert sha(self.source/'payload.bin')==self.data['payloadSha256'];assert self.data['complete'] and self.data['map']=='valley' and self.data['byteOrder']=='little'
        self.qualification=json.loads((self.source.parent/'qualification.json').read_text());self.materials=[];self.material_ids={};self.textures={};self.groups=collections.defaultdict(list);self.boxes=[];self.shape_records=[];self.render_census=[];self.collision_census=collections.Counter();self.omitted=[];self.vertex_total=0;self.convex_serial=0
    def array(self,descriptor):
        if descriptor is None:return None
        width=int(descriptor['scalarBytes']);kind=descriptor['variantType'];dtype={29:'u1',30:'<i4',31:'<i8',32:'<f4',33:'<f8'}.get(kind,'<f'+str(width));a=np.frombuffer(self.raw,dtype=dtype,count=descriptor['count']*descriptor['components'],offset=descriptor['offset']).reshape(descriptor['count'],descriptor['components']);assert a.nbytes==descriptor['bytes'];return a.copy()
    def ref(self,value):return None if value is None else self.resources[value['resourceRef']]
    def require_batches(self):
        invalid=[]
        for mm in self.data['multiMeshes']:
            n=mm['instanceCount'];rows=mm['instances'];matrices=[np.asarray(x['localTransform']) for x in rows]
            empty=mm.get('sourceBuffer',{}).get('count',0)==0;identity=np.array([1,0,0,0,1,0,0,0,1,0,0,0])
            captured=mm.get('transformSource')=='original _flush_batches input capture'
            if len(rows)!=n or (n>0 and empty and not captured) or (n>1 and all(np.allclose(m,identity,atol=1e-7) for m in matrices)):invalid.append({'path':mm['path'],'count':n,'reason':'headless getter lost actual source transforms; empty buffer/all identity or no original-input capture'})
        self.invalid_batches=invalid
        if invalid and not self.diagnostic:raise ValueError('Refusing full Valley publication: '+str(sum(x['count'] for x in invalid))+' invalid MultiMesh poses. Corrected genuine capture required; no output written.')
    def texture(self,value):
        resource=self.ref(value)
        if resource is None:return ''
        image=resource.get('image');assert image and image.get('viewFile'),('Actual material image missing',resource['id'])
        source=self.source/image['viewFile'];assert source.suffix.lower()=='.png';name=resource['id']+'.png';self.textures[name]={'source':str(source),'sha256':sha(source),'rawImageSha256':image['sha256'],'sourceResource':resource['id']};return name
    def material(self,value):
        resource=self.ref(value);rid='source-default' if resource is None else resource['id']
        if rid in self.material_ids:return self.material_ids[rid]
        p={} if resource is None else resource.get('properties',{});profile='standard' if resource is None or resource['class']=='StandardMaterial3D' else 'alpine';assert resource is None or resource['class'] in ['StandardMaterial3D','ShaderMaterial']
        row=dict(name=rid,profile=profile,tint=unpack(p.get('albedo_color'),[1,1,1,1]),color=self.texture(p.get('albedo_texture')),normal=self.texture(p.get('normal_texture')) if p.get('normal_enabled') else '',roughnessMap=self.texture(p.get('roughness_texture')),metallicMap=self.texture(p.get('metallic_texture')),occlusionMap=self.texture(p.get('ao_texture')) if p.get('ao_enabled') else '',emissionMap=self.texture(p.get('emission_texture')) if p.get('emission_enabled') else '',metallic=p.get('metallic',0),roughness=p.get('roughness',1),normalScale=p.get('normal_scale',1) if p.get('normal_enabled') else 0,metallicChannel=p.get('metallic_texture_channel',0),roughnessChannel=p.get('roughness_texture_channel',0),occlusionChannel=p.get('ao_texture_channel',0),emissionColor=unpack(p.get('emission'),[0,0,0,1]),emissionEnergy=p.get('emission_energy_multiplier',1) if p.get('emission_enabled') else 0,emissionEnabled=p.get('emission_enabled',False),uvScale=unpack(p.get('uv1_scale'),[1,1,1]),uvOffset=unpack(p.get('uv1_offset'),[0,0,0]),uvTriplanar=p.get('uv1_triplanar',False),uvWorldTriplanar=p.get('uv1_world_triplanar',False),uvTriplanarSharpness=p.get('uv1_triplanar_sharpness',1),vertexColor=p.get('vertex_color_use_as_albedo',False),vertexColorSrgb=p.get('vertex_color_is_srgb',False),transparency=p.get('transparency',0),alpha=p.get('transparency',0)!=0,alphaCutoff=p.get('alpha_scissor_threshold',.5),cull=p.get('cull_mode',0),unshaded=p.get('shading_mode',1)==0,snowHeight=0)
        if profile=='alpine':
            shader=self.ref(p.get('shader'));assert shader and 'alpine_terrain.gdshader' in shader['source'];row.update(snowHeight=resource['uniforms']['snow_height'],roughness=1,cull=2)
        row['emissionOperator']=p.get('emission_operator',0)
        index=len(self.materials);self.material_ids[rid]=index;self.materials.append(row);return index
    def vertices(self,positions,normals,uv,colors,matrix):
        basis,origin=transform(matrix);assert abs(np.linalg.det(basis))>1e-12
        world=(positions@basis.T+origin)@REFLECT;normal=normals@np.linalg.inv(basis)@REFLECT;lengths=np.linalg.norm(normal,axis=1);normal[lengths>1e-20]/=lengths[lengths>1e-20,None]
        uv=uv.copy();uv[:,1]=1-uv[:,1]
        result=np.column_stack((world,normal,uv,colors,positions,normals)).astype('<f4');assert result.shape[1]==18 and np.isfinite(result).all();return result
    def add(self,key,vertices,indices):
        indices=np.asarray(indices,np.int32).ravel();assert len(indices)%3==0 and len(indices)>0 and indices.min()>=0 and indices.max()<len(vertices)
        # Split by actual triangle subsets, remapping existing vertices without alteration.
        for start in range(0,len(indices),60000):
            part=indices[start:start+60000];used,inverse=np.unique(part,return_inverse=True);self.groups[key].append((vertices[used],inverse.astype('<i4')))
    def render(self,node,matrix,color=None):
        mesh=self.meshes[node['mesh']];basis,_=transform(matrix)
        for si,surface in enumerate(mesh['surfaces']):
            assert surface['primitive']==3
            arrays=surface['arrays'];pos=self.array(arrays[0]);norm=self.array(arrays[1]);norm=np.zeros_like(pos) if norm is None else norm;uv=self.array(arrays[4]);uv=np.zeros((len(pos),2)) if uv is None else uv;col=self.array(arrays[3]);col=np.ones((len(pos),4)) if col is None else col
            if color is not None:col=col*np.asarray(color)
            idx=self.array(arrays[12]);idx=np.arange(len(pos),dtype=np.int32) if idx is None or idx.size==0 else idx.ravel().astype(np.int32)
            if np.linalg.det(basis)<0:idx=idx.reshape(-1,3)[:,[0,2,1]].ravel()
            material=node.get('activeSurfaceMaterials',[surface['material']]*len(mesh['surfaces']))[si];material=node.get('materialOverride') or material or surface['material'];mid=self.material(material)
            self.add((mid,0,1,0,False),self.vertices(pos,norm,uv,col,matrix),idx);self.render_census.append({'source':node['path'],'surface':si,'triangles':len(idx)//3})
    def collision_mesh(self,points,indices,matrix,layer,convex,backface,source,ballistic_flags=0):
        points=np.asarray(points,float);zeros=np.zeros_like(points);vertices=self.vertices(points,zeros,np.zeros((len(points),2)),np.ones((len(points),4)),matrix);basis,_=transform(matrix);indices=np.asarray(indices,np.int32).reshape(-1,3)
        # Convex point-cloud hull faces are newly triangulated outward; concave
        # faces retain the original Godot index order and handedness conversion.
        if (np.linalg.det(basis)>=0) if convex else (np.linalg.det(basis)<0):indices=indices[:,[0,2,1]]
        mid=self.material(None)
        # Each convex owner MUST stay separate: batching different hulls would
        # ask Unity to cook one enclosing hull and fill the source's open space.
        if convex:self.convex_serial+=1
        self.add((mid,3 if convex else 1,0,layer,backface,self.convex_serial if convex else 0,ballistic_flags),vertices,indices.ravel());self.collision_census[source]+=len(indices)
    def shapes(self):
        for body in self.data['staticBodies']:
            groups=body.get('groups',[]);ballistic_flags=(1 if 'ballistic_proxy_ignore' in groups else 0)|(2 if 'stair_support' in groups else 0)
            for index,shape in enumerate(body['shapes']):
                if shape['disabled'] or body['collisionLayer']==0:continue
                resource=self.ref(shape['shape']);p=resource['properties'];kind=resource['class'];matrix=shape['globalTransform'];basis,origin=transform(matrix);layer=body['collisionLayer'];record=dict(body=body['path'],shapeIndex=index,kind=kind,sourceLayer=layer,sourceTransform=matrix,sourceProperties=p,sourceMargin=p.get('margin',0),sourceGroups=groups,ballisticExclusionFlags=ballistic_flags);self.shape_records.append(record)
                if kind=='BoxShape3D':
                    size=np.asarray(unpack(p['size']),float);scale=np.linalg.norm(basis,axis=0);rotation=basis/scale
                    if np.allclose(rotation.T@rotation,np.eye(3),atol=1e-5):
                        if np.linalg.det(rotation)<0:rotation[:,0]*=-1
                        self.boxes.append(dict(name=body['path']+'/shape'+str(index),center=(origin@REFLECT).tolist(),size=(size*scale).tolist(),rotation=quaternion(REFLECT@rotation@REFLECT),sourceLayer=layer,**({'ballisticExclusionFlags':ballistic_flags} if ballistic_flags else {})));continue
                    points=np.asarray(list(itertools.product(*[[-v/2,v/2] for v in size])));self.collision_mesh(points,hull_indices(points),matrix,layer,True,False,'shearedBox',ballistic_flags);record['exactShearedBoxHull']=True
                elif kind=='ConcavePolygonShape3D':
                    points=self.array(resource['faces']);self.collision_mesh(points,np.arange(len(points)),matrix,layer,False,resource['backfaceCollision'],'concave',ballistic_flags)
                elif kind=='ConvexPolygonShape3D':
                    points=self.array(resource['points']);record['sourcePoints']=points.tolist();self.collision_mesh(points,hull_indices(points),matrix,layer,True,False,'convex',ballistic_flags)
                elif kind=='CylinderShape3D':
                    radius=p['radius'];height=p['height'];n=64;angles=np.arange(n)*math.tau/n;points=np.asarray([[radius*math.cos(a),y,radius*math.sin(a)] for y in [-height/2,height/2] for a in angles]);tri=[]
                    for i in range(n):
                        j=(i+1)%n;tri.extend([[i,n+j,j],[i,n+i,n+j]])
                    for i in range(1,n-1):tri.extend([[0,i,i+1],[n,n+i+1,n+i]])
                    # Orient analytical prism triangles against its known outward directions.
                    tri=np.asarray(tri);a,b,c=points[tri[:,0]],points[tri[:,1]],points[tri[:,2]];centers=(a+b+c)/3;out=centers.copy();caps=np.abs(centers[:,1])>height*.499;out[~caps,1]=0;out[caps,0]=out[caps,2]=0;bad=(np.cross(b-a,c-a)*out).sum(1)<0;tri[bad]=tri[bad][:,[0,2,1]]
                    self.collision_mesh(points,tri,matrix,layer,True,False,'cylinder64',ballistic_flags);record.update(approximation='64-sided inscribed convex prism, not capsule',radialSagitta=radius*(1-math.cos(math.pi/n)),worldSagittaBound=radius*(1-math.cos(math.pi/n))*np.linalg.svd(basis,compute_uv=False).max(),segments=n)
                else:raise ValueError('Unsupported source collision '+kind)
    def run(self):
        self.require_batches();output=HERE/'diagnostic-direct' if self.diagnostic else DEST
        # Capture all real source surface finishes now, including those whose
        # batches await valid poses. Their presence never supplies missing geometry.
        for resource in self.data['resources']:
            if resource['class'] in ['StandardMaterial3D','ShaderMaterial']:self.material({'resourceRef':resource['id']})
        for node in self.data['meshInstances']:
            if node['visible']:self.render(node,node['globalTransform'])
        if not self.diagnostic:
            for node in self.data['multiMeshes']:
                for instance in node['instances']:
                    if node['visible'] and instance['visible']:self.render(node,instance['globalTransform'],instance.get('color'))
        else:self.omitted=self.invalid_batches
        self.shapes();chunks=[]
        for key,parts in self.groups.items():
            vs=[];ii=[];count=0
            def flush():
                if vs:chunks.append((key,np.concatenate(vs),np.concatenate(ii).astype('<i4')))
            for v,i in parts:
                if count+len(v)>60000:flush();vs=[];ii=[];count=0
                vs.append(v);ii.append(i+count);count+=len(v)
            flush()
        assert len(chunks)<=2048
        output.mkdir(parents=True,exist_ok=True)
        with (output/'geometry.bytes').open('wb') as f:
            f.write(struct.pack('<ii',0x52323357,len(chunks)))
            for key,vertices,indices in chunks:
                mid,flags,visible=key[:3];f.write(struct.pack('<iiiii',mid,flags,visible,len(vertices),len(indices)));f.write(vertices.tobytes());f.write(indices.tobytes())
        for name,row in self.textures.items():shutil.copyfile(row['source'],output/name);assert sha(output/name)==row['sha256']
        visible_bounds=[(v[:,:3].min(0),v[:,:3].max(0)) for key,v,i in chunks if key[2]];bounds_min=np.min([b[0] for b in visible_bounds],axis=0).tolist();bounds_max=np.max([b[1] for b in visible_bounds],axis=0).tolist()
        def markers(value):
            if isinstance(value,dict) and 'vector3'in value:return (np.asarray(value['vector3'])@REFLECT).tolist()
            if isinstance(value,list):return [markers(v) for v in value]
            if isinstance(value,dict):return {k:markers(v) for k,v in value.items()}
            return value
        manifest=dict(version=2,mapId='diagnostic:valley-direct' if self.diagnostic else 'valley',title='RELAY VALLEY (DIRECT-ONLY DIAGNOSTIC)' if self.diagnostic else 'RELAY VALLEY',sourceSha256=self.data['sourceHashes']['scripts/world.gd'],spawn=(np.asarray(self.data['spawn'])@REFLECT).tolist(),boundsMin=bounds_min,boundsMax=bounds_max,materials=self.materials,chunks=len(chunks),chunkSourceLayers=[key[3] for key,_,_ in chunks],chunkBackfaceCollision=[key[4] for key,_,_ in chunks],chunkBallisticExclusionFlags=[key[6] if len(key)>6 else 0 for key,_,_ in chunks],boxes=self.boxes,environmentFile='environment.json',diagnosticOnly=self.diagnostic,omittedMultiMeshInstances=sum(x['count'] for x in self.omitted),sourceMarkers=self.data['authoredMarkers'],markers=markers(self.data['authoredMarkers']))
        (output/'map.json').write_text(json.dumps(manifest,indent=2))
        report=dict(completePlayableMap=not self.diagnostic,diagnosticOnly=self.diagnostic,source=str(self.source),sourceWorldSha256=sha(self.source/'world.json'),sourcePayloadSha256=sha(self.source/'payload.bin'),qualificationSha256=sha(self.source.parent/'qualification.json'),qualification=self.qualification,omittedMultiMeshes=self.omitted,renderTriangles=sum(len(i)//3 for key,v,i in chunks if key[2]),collisionTriangles=sum(len(i)//3 for key,v,i in chunks if key[1]),collisionTriangleKinds=dict(self.collision_census),boxShapes=len(self.boxes),shapeOccurrences=len(self.shape_records),chunks=len(chunks),materials=len(self.materials),textureImages=self.textures,geometrySha256=sha(output/'geometry.bytes'),sourceShapeRecords=self.shape_records,renderSurfaceCensus=self.render_census,conversion='Z reflection once; source CW index order retained except determinant-negative compensation. Normals inverse-transpose+normalize. UV V flipped once. Last6floats preserve raw original Godot local position/normal. No terrain approximation.',limits=['1124 MultiMesh poses invalid in original export; full conversion requires corrected genuine capture'] if self.diagnostic else [])
        (output/'provenance.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k in ['completePlayableMap','diagnosticOnly','renderTriangles','collisionTriangles','collisionTriangleKinds','boxShapes','shapeOccurrences','chunks','materials','limits']},indent=2));return output
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--source',type=Path,default=DEFAULT);p.add_argument('--diagnostic-direct',action='store_true');a=p.parse_args();Converter(a.source,a.diagnostic_direct).run()
if __name__=='__main__':main()
