"""Bounded GLB pilot extraction. Reads originals; never starts an engine or copies packs."""
import hashlib, json, struct
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'campaign-staging/20261104-three-chats/unity/World'
EVIDENCE = ROOT / 'docs/agent-runs/20261104-three-chats/unity/World'
SOURCE = Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps')
sha = lambda b: hashlib.sha256(b).hexdigest()

def extract(path):
    raw = path.read_bytes()
    magic, version, length = struct.unpack_from('<III', raw)
    assert magic == 0x46546C67 and version == 2 and length == len(raw)
    n, kind = struct.unpack_from('<II', raw, 12)
    assert kind == 0x4e4f534a
    doc = json.loads(raw[20:20+n]); size, kind = struct.unpack_from('<II', raw, 20+n)
    assert kind == 0x004e4942
    binary = raw[28+n:28+n+size]
    def accessor(i):
        a=doc['accessors'][i]; v=doc['bufferViews'][a['bufferView']]
        assert 'sparse' not in a
        dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1',5120:'i1',5122:'<i2'}[a['componentType']])
        count={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        result=np.ndarray((a['count'],count),dt,buffer=binary,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',dt.itemsize*count),dt.itemsize)).copy()
        if a.get('normalized',False):result=np.maximum(-1,result.astype(float)/np.iinfo(dt).max)
        return result
    def local(n):
        if 'matrix' in n:return np.array(n['matrix']).reshape(4,4,order='F')
        x,y,z,w=n.get('rotation',[0,0,0,1]); m=np.eye(4)
        m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]) @ np.diag(n.get('scale',[1,1,1]))
        m[:3,3]=n.get('translation',[0,0,0]); return m
    surfaces=[]
    def walk(i,parent):
        n=doc['nodes'][i]; world=parent@local(n)
        if 'mesh' in n and len(surfaces)<2:
            for pi,p in enumerate(doc['meshes'][n['mesh']]['primitives']):
                if len(surfaces)>=2:break
                if p.get('mode',4)!=4:continue
                v=accessor(p['attributes']['POSITION']); indices=accessor(p['indices']).reshape(-1) if 'indices' in p else np.arange(len(v))
                # Exactly the first 32 source triangles, retaining vertex/normal/UV correspondence.
                indices=indices[:96]; used,remapped=np.unique(indices,return_inverse=True); v=v[used]
                if 'NORMAL' not in p['attributes']:continue
                normals=accessor(p['attributes']['NORMAL'])[used]
                if np.any(np.linalg.norm(normals,axis=1)<1e-8):continue
                uv=accessor(p['attributes']['TEXCOORD_0'])[used].reshape(-1).tolist() if 'TEXCOORD_0' in p['attributes'] else []
                mi=p.get('material',-1); mat=doc.get('materials',[])[mi] if mi>=0 else {}; pbr=mat.get('pbrMetallicRoughness',{})
                texture=''
                if 'baseColorTexture' in pbr:
                    ti=pbr['baseColorTexture']['index']; image=doc['images'][doc['textures'][ti]['source']]
                    texture=str(path)+'#image/'+str(doc['textures'][ti]['source'])
                sid='node/'+str(i)+'/primitive/'+str(pi)
                surfaces.append(dict(id=sid,semanticOwner=sid,colliderRole='structure',matrix=world.reshape(-1,order='F').tolist(),vertices=v.reshape(-1).tolist(),normals=normals.reshape(-1).tolist(),uv=uv,indices=remapped.tolist(),materialPath=str(path)+'#material/'+str(mi),texturePath=texture,color=pbr.get('baseColorFactor',[1,1,1,1]),roughness=pbr.get('roughnessFactor',1),metallic=pbr.get('metallicFactor',1),doubleSided=mat.get('doubleSided',False)))
        for child in n.get('children',[]):walk(child,world)
    for i in doc['scenes'][doc.get('scene',0)]['nodes']:walk(i,np.eye(4))
    assert len(surfaces)==2
    return raw,surfaces

def main():
    OUT.mkdir(parents=True,exist_ok=True); EVIDENCE.mkdir(parents=True,exist_ok=True)
    policies=json.loads((ROOT/'quest-r22/source/assets/data/map_semantics_r14.json').read_text())['maps']
    old=json.loads((ROOT/'campaign-staging/20261104/unity-import/inventory/pilot-neutral.json').read_text())
    receipts=[]
    for slug,indoor in [('alfa-aglae-souza',False),('battlefield-paintball-gods-pitdevil',True)]:
        path=SOURCE/slug/'source/map.glb'; raw,surfaces=extract(path)
        baseline=next(x for x in old['maps'] if x['slug']==slug); policy=policies[slug]
        # Reconcile immutable original with mounted baseline fingerprint rather than assuming the old export is accepted.
        assert sha(raw)==baseline['manifest']['source_sha256']==policy['source_sha256']
        assert baseline['source_fingerprint']==policy['cache_fingerprint']
        spawns=[]
        for mode,key in [('deathmatch','deathmatch'),('warmup','warmup')]:
            for i,s in enumerate(policy.get(key,[])[:4]):
                spawns.append(dict(id=key+'/'+str(i),mode=mode,team='shared',nativeMarker=True,position=s['center'],forward=s.get('forward',[0,0,-1])))
        if not spawns:
            for i,pos in enumerate(baseline['manifest']['recorded_spawns'][:4]):spawns.append(dict(id='recorded/'+str(i),mode='deathmatch',team='shared',position=pos,forward=[0,0,-1]))
        pads=[dict(id='jump/'+str(i),semanticOwner='jump/'+str(i),position=p['center'],normal=p['normal'],radius=p['radius'],speed=p['speed']) for i,p in enumerate(policy.get('jump',[])) if p.get('tilted')][:2]
        if not pads:pads=[dict(id='jump/'+str(i),semanticOwner='jump/'+str(i),position=p['center'],normal=p['normal'],radius=p['radius'],speed=p['speed']) for i,p in enumerate(policy.get('jump',[])[:2])]
        d=dict(schema=1,id=baseline['id'],version='world-pilot-1',sourcePath=str(path),sourceSha256=sha(raw),sourceRevision='quest bdba6e30012e1733efd5021fd8ded593f82f26e6; original GLB reconciled by source hash',approval='unreviewed-pilot',indoor=indoor,capsules=False,surfaces=surfaces,spawns=spawns,pads=pads)
        dest=OUT/(slug+'.json'); dest.write_text(json.dumps(d,separators=(',',':')),encoding='utf-8')
        receipts.append(dict(path=str(dest),sha256=sha(dest.read_bytes()),sourcePath=str(path),sourceSha256=sha(raw),fingerprint=policy['cache_fingerprint'],triangles=sum(len(s['indices'])//3 for s in surfaces),spawns=len(spawns),pads=len(pads),uvVertices=sum(len(s['uv'])//2 for s in surfaces),scope='first two eligible GLB primitives, first 32 triangles each; no texture bytes; no full-map collision or runtime acceptance'))
    (EVIDENCE/'conversion.json').write_text(json.dumps(receipts,indent=2))
    print(json.dumps(receipts,indent=2))
if __name__=='__main__':main()
