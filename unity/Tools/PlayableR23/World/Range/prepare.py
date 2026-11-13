"""Direct CPU translation of preserved Opus R23 sandbox_map_091 + rounded_box.

No scene export, engine process, terrain substitution or edits to the source tree.
"""
from pathlib import Path
import hashlib,json,re,struct,uuid
import numpy as np

ROOT=Path(__file__).resolve().parents[5]
HERE=Path(__file__).resolve().parent
SOURCE=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
OUTPUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/range'
MAP_SCRIPT=SOURCE/'scripts/sandbox_map_091.gd'
ART_SCRIPT=SOURCE/'scripts/field_item_models.gd'
MAGIC=0x52323357

def vector(value):
    parts=value.split(',')
    assert len(parts)==3
    return [float(item.strip()) for item in parts]

def source_recipe():
    script=MAP_SCRIPT.read_text(encoding='utf-8')
    body=re.search(r'for spec in (\[\[.*?\]\]):',script).group(1)
    vectors=[vector(row) for row in re.findall(r'Vector3\(([^)]+)\)',body)]
    assert len(vectors)==8,'Expected the four actual source box recipes'
    specs=[dict(center=vectors[i],size=vectors[i+1]) for i in range(0,8,2)]
    origin=vector(re.search(r';position=Vector3\(([^)]+)\)',script).group(1))
    spawn_local=vector(re.search(r'spawn=global_position\+Vector3\(([^)]+)\)',script).group(1))
    bevel=float(re.search(r'art\._box\(self,"RangeSurface",.*?,([.\d]+)\)',script).group(1))
    finishes=[]
    for name in ('floor','wall'):
        match=re.search(name+r'_mat:Material=art.material\(Color\("([a-f0-9]+)"\),([.\d]+),([.\d]+)\)',script)
        tint,metallic,roughness=match.groups()
        finishes.append(dict(name='Range'+name.title()+'_'+tint,color='',normal='',alpha=False,
                             metallic=float(metallic),roughness=float(roughness),
                             sourceTintHex=tint,sourceTintRgba=[int(tint[i:i+2],16)/255 for i in (0,2,4)]+[1]))
    return specs,origin,spawn_local,bevel,finishes

def reflected(value):
    return [value[0],value[1],-value[2]]

def rounded_box(size,bevel):
    """field_item_models.gd:244-263, including float32 Vector3 operations.

    The source emits triangles with explicit smooth normals, then SurfaceTool.index().
    Its axes are RIGHT/UP/BACK (+X/+Y/+Z), not Godot FORWARD (-Z).
    """
    half=np.asarray(size,dtype=np.float32)*np.float32(.5)
    radius=min(max(bevel,.00001),float(half.min())*.8)
    inner=half-np.ones(3,dtype=np.float32)*np.float32(radius)
    axes=np.eye(3,dtype=np.float32);vertices=[];normals=[]
    for axis in range(3):
        u,v,normal=axes[(axis+1)%3],axes[(axis+2)%3],axes[axis]
        us,vs=float(half[(axis+1)%3]),float(half[(axis+2)%3])
        ug=(-us,-us+radius,us-radius,us);vg=(-vs,-vs+radius,vs-radius,vs)
        for sign in (-1,1):
            for j in range(3):
                for k in range(3):
                    base=normal*half[axis]*np.float32(sign)
                    points=[base+u*np.float32(ug[a])+v*np.float32(vg[b])
                            for a,b in ((j,k),(j+1,k),(j+1,k+1),(j,k+1))]
                    for q in ((0,2,1,0,3,2) if sign>0 else (0,1,2,0,2,3)):
                        point=points[q];core=np.clip(point,-inner,inner)
                        delta=point-core;outward=delta/np.sqrt(np.dot(delta,delta))
                        vertices.append(core+outward*np.float32(radius));normals.append(outward)
    return np.asarray(vertices,dtype=np.float32),np.asarray(normals,dtype=np.float32)

def make_chunk(spec,origin,bevel,finish):
    positions,normals=rounded_box(spec['size'],bevel)
    # Godot local mesh -> part center -> map origin, then reflect Z exactly once.
    positions=(positions+np.asarray(spec['center'],dtype=np.float32))+np.asarray(origin,dtype=np.float32)
    positions[:,2]*=-1;normals[:,2]*=-1
    expanded=np.c_[positions,normals,np.zeros((len(positions),2),dtype=np.float32),
                   np.tile(np.asarray(finish['sourceTintRgba'],dtype=np.float32),(len(positions),1))].astype('<f4')
    # Faithful SurfaceTool.index equivalence: only vertices with identical attributes weld.
    unique=[];indices=[];seen={}
    for row in expanded:
        key=row.tobytes()
        if key not in seen:seen[key]=len(unique);unique.append(row)
        indices.append(seen[key])
    # Reflection reverses orientation; swap each triangle once for Unity clockwise fronts.
    indices=np.asarray(indices,dtype='<i4').reshape(-1,3)[:,[0,2,1]].ravel()
    return np.asarray(unique,dtype='<f4'),indices

def ensure_meta(path,folder=False):
    target=path.with_name(path.name+'.meta')
    if not target.exists():
        target.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\n' if folder else '')+
                          'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')

def main():
    specs,origin,spawn_local,bevel,finishes=source_recipe()
    OUTPUT.mkdir(parents=True,exist_ok=True)
    chunks=[];boxes=[]
    for spec in specs:
        mid=0 if spec['size'][1]<1 else 1
        vertices,indices=make_chunk(spec,origin,bevel,finishes[mid]);chunks.append((mid,vertices,indices))
        center=[a+b for a,b in zip(spec['center'],origin)]
        boxes.append(dict(center=reflected(center),size=spec['size']))
    source_min=[min(origin[a]+spec['center'][a]-spec['size'][a]/2 for spec in specs) for a in range(3)]
    source_max=[max(origin[a]+spec['center'][a]+spec['size'][a]/2 for spec in specs) for a in range(3)]
    spawn=[a+b for a,b in zip(origin,spawn_local)]
    inputs=[dict(path=path.as_posix(),sha256=hashlib.sha256(path.read_bytes()).hexdigest()) for path in (MAP_SCRIPT,ART_SCRIPT)]
    manifest=dict(version=1,mapId='range',title='SANDBOX RANGE',sourceSha256=inputs[0]['sha256'],
                  spawn=reflected(spawn),boundsMin=[source_min[0],source_min[1],-source_max[2]],
                  boundsMax=[source_max[0],source_max[1],-source_min[2]],materials=finishes,chunks=len(chunks),boxes=boxes,
                  source=dict(scripts=inputs,translation='Direct CPU recipe port; not an engine export',
                              originGodot=origin,spawnLocalGodot=spawn_local,spawnWorldGodot=spawn,
                              boundsMinGodot=source_min,boundsMaxGodot=source_max,boxSpecsLocalGodot=specs,
                              sourceRoundedBox=dict(script='scripts/field_item_models.gd',line=244,bevel=bevel,uv='not authored; zero-filled payload slots'),
                              bodyGroups=[['climbable','construction_terrain'] if spec['size'][1]<1 else ['climbable'] for spec in specs],
                              environment='Source range inherits the existing Valley environment, which is not ported by this asset task.',
                              scope='Only the actual four render boxes and four BoxShape3D descriptors. Inventory shifts, training/dynamic systems and Valley environment remain runtime-owned.'))
    (OUTPUT/'map.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    with (OUTPUT/'geometry.bytes').open('wb') as out:
        out.write(struct.pack('<ii',MAGIC,len(chunks)))
        for mid,vertices,indices in chunks:
            out.write(struct.pack('<iiiii',mid,0,1,len(vertices),len(indices)))
            out.write(vertices.tobytes());out.write(indices.tobytes())
    for path in (OUTPUT/'map.json',OUTPUT/'geometry.bytes'):ensure_meta(path)
    print('Prepared actual source SANDBOX RANGE:',len(chunks),'render-only chunks;',len(boxes),'BoxShape descriptors;',sum(len(i)//3 for _,_,i in chunks),'triangles')

if __name__=='__main__':main()
