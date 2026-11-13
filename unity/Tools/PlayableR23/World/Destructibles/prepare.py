"""Split exact original ALFA panels out of the existing native-map payload, CPU only."""
from pathlib import Path
import collections,hashlib,json,mmap,shutil,struct,time
import numpy as np
from source_panels import extract,bounds,normal,center,SOURCE,HASH
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];LIVE=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/alfa-aglae-souza';OUT=HERE/'StagedPayload';BASE=HERE/'OriginalPayload';R23=Path(r'C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def unity(rows):
    r=rows.copy();r[:,:,2]*=-1;r[:,:,5]*=-1;r[:,:,7]=1-r[:,:,7];return r
def keys(rows):
    p=np.ascontiguousarray(rows[:,:,:3]);v=p.view('V12').reshape(-1,3);return np.sort(v,axis=1).copy().view('V36').reshape(-1)
def split(poly,n,d):
    inside=[];outside=[]
    for i,a in enumerate(poly):
        b=poly[(i+1)%len(poly)];da=float(np.dot(a[:3],n))-d;db=float(np.dot(b[:3],n))-d
        (inside if da<=.000001 else outside).append(a)
        if(da<-.000001 and db>.000001)or(da>.000001 and db<-.000001):
            row=(a+(b-a)*(da/(da-db))).astype(np.float32);row[3:6]/=max(np.linalg.norm(row[3:6]),1e-20);inside.append(row);outside.append(row)
    return inside,outside
def carve(poly,planes):
    current=poly;outside=[]
    for n,d in planes:
        if len(current)<3:return outside,[]
        current,other=split(current,n,d)
        if len(other)>=3:outside.append(other)
    return outside,current
def triangles(poly):
    result=[]
    for i in range(1,len(poly)-1):
        tri=np.array([poly[0],poly[i],poly[i+1]],np.float32)
        if np.sum(np.cross(tri[1,:3]-tri[0,:3],tri[2,:3]-tri[0,:3])**2)>=1e-12:result.append(tri)
    return result
def main():
    BASE.mkdir(exist_ok=True)
    if not(BASE/'geometry.bytes').exists():
        assert sha(LIVE/'geometry.bytes')=='2ad46276bcdec115904b8891859c4da058d849c1f31e01e6972fc23b779e7987','ALFA baseline changed; do not reconvert processed output'
        shutil.copyfile(LIVE/'geometry.bytes',BASE/'geometry.bytes');shutil.copyfile(LIVE/'map.json',BASE/'map.json')
    assert sha(BASE/'geometry.bytes')=='2ad46276bcdec115904b8891859c4da058d849c1f31e01e6972fc23b779e7987'
    started=time.monotonic();batches,erased,panels=extract();print('Original assemblies:',len(panels),'glass',sum(p['glass']for p in panels),'doors',sum(not p['glass']for p in panels),flush=True)
    removed={};replacements={};trim_candidates={};entries=[]
    for key,rows in batches.items():
        for raw in keys(unity(rows[erased[key]])):removed.setdefault(key[1],set()).add(bytes(raw))
    # Exact source r9 aperture clipping: vc trim inside a recognized panel belongs to it.
    for pi,panel in enumerate(panels):
        n=normal(panel['main']);right=np.cross([0,1,0],n);right/=max(np.linalg.norm(right),1e-20)
        if abs(n[1])>.1:continue
        points=panel['main'][:,:,:3].reshape(-1,3);project=points@right;lowy=points[:,1].min();highy=points[:,1].max();middle=float(np.dot(center(panel),n));planes=[(right,float(project.max())+.003),(-right,-float(project.min())+.003),(np.array([0,1,0]),float(highy)+.003),(np.array([0,-1,0]),-float(lowy)+.003),(n,middle+.32),(-n,-middle+.32)]
        entries.append((pi,planes))
        for key,rows in batches.items():
            if key[1]!='vc':continue
            lo=rows[:,:,:3].min(axis=1);hi=rows[:,:,:3].max(axis=1);candidate=np.flatnonzero((lo<=panel['high']+.3601).all(axis=1)&(hi>=panel['low']-.3601).all(axis=1)&~erased[key])
            for ti in candidate:trim_candidates.setdefault((key,int(ti)),[]).append((pi,planes))
    changed=0;fragments=0;trim_area_error=0
    for(key,ti),choices in trim_candidates.items():
        original=batches[key][ti];pieces=[list(original)];cut=False
        for pi,planes in choices:
            remaining=[]
            for piece in pieces:
                outside,inside=carve(piece,planes);inside_tri=triangles(inside)
                area=sum(np.linalg.norm(np.cross(t[1,:3]-t[0,:3],t[2,:3]-t[0,:3]))for t in inside_tri)
                if len(inside)<3 or area<.000001:remaining.append(piece);continue
                panels[pi]['parts'].append(('vc',np.array(inside_tri,np.float32)));remaining.extend(outside);cut=True;fragments+=1
            pieces=remaining
        if cut:
            rest=[t for poly in pieces for t in triangles(poly)];keybytes=bytes(keys(unity(original[None]))[0]);replacements[keybytes]=unity(np.array(rest,np.float32))if rest else np.empty((0,3,12),np.float32);changed+=1
    OUT.mkdir(exist_ok=True)
    original_manifest=json.loads((BASE/'map.json').read_text(encoding='utf-8'));manifest=dict(original_manifest);materials=manifest['materials'];bykey={r['name']:i for i,r in enumerate(materials)}
    # Preserve existing exact texture files and manifest material identities.
    for row in materials:
        for field in ['color','normal']:
            if row.get(field):shutil.copyfile(LIVE/row[field],OUT/row[field])
    raw=(BASE/'geometry.bytes').read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x52323357 and count==manifest['chunks'];cursor=8;chunks=[];original_render=original_collision=static_render=static_collision=0;render_removed=collision_removed=0
    for ci in range(count):
        mid,coll,visible,nv,ni=struct.unpack_from('<iiiii',raw,cursor);cursor+=20;v=np.frombuffer(raw,dtype='<f4',count=nv*12,offset=cursor).reshape(nv,12);cursor+=nv*48;idx=np.frombuffer(raw,dtype='<i4',count=ni,offset=cursor).reshape(-1,3);cursor+=ni*4;key=materials[mid]['name'];original_render+=len(idx)*bool(visible);original_collision+=len(idx)*bool(coll)
        if key not in ['vc','s48']:chunks.append((mid,coll,visible,v,idx.ravel()));static_render+=len(idx)*bool(visible);static_collision+=len(idx)*bool(coll);continue
        rows=v[idx];keep=[];append=[]
        for ti,k in enumerate(keys(rows)):
            k=bytes(k)
            if k in removed.get(key,set()):render_removed+=bool(visible);collision_removed+=bool(coll);continue
            if key=='vc'and k in replacements:
                append.extend(replacements[k]);continue
            keep.append(ti)
        idxkeep=idx[keep].ravel()if keep else np.empty(0,np.int32)
        if len(append):
            extra=np.array(append,np.float32).reshape(-1,12);ii=np.arange(len(extra),dtype=np.int32)+len(v);v=np.concatenate((v,extra));idxkeep=np.concatenate((idxkeep,ii))
        if len(idxkeep):chunks.append((mid,coll,visible,v,idxkeep));static_render+=len(idxkeep)//3*bool(visible);static_collision+=len(idxkeep)//3*bool(coll)
    assert cursor==len(raw)
    owners=[-1]*len(chunks);clear=[False]*len(chunks);definitions=[];panel_triangles=0
    for pi,panel in enumerate(panels):
        group=collections.OrderedDict()
        for key,rows in panel['parts']:group.setdefault(key,[]).append(unity(rows))
        for key,parts in group.items():
            rows=np.concatenate(parts);v=rows.reshape(-1,12).astype('<f4');idx=np.arange(len(v),dtype='<i4');assert len(v)<=200000
            chunks.append((bykey[key],1,1,v,idx));owners.append(pi);clear.append(panel['glass']and key=='s48');panel_triangles+=len(rows)
        lo=panel['low'];hi=panel['high'];probe=panel['main'][np.argmax(np.linalg.norm(np.cross(panel['main'][:,1,:3]-panel['main'][:,0,:3],panel['main'][:,2,:3]-panel['main'][:,0,:3]),axis=1)),:,:3].mean(axis=0);n=normal(panel['main']);c=center(panel);s=hi-lo
        definitions.append(dict(id='alfa/panel/'+str(pi),glass=bool(panel['glass']),center=[float(c[0]),float(c[1]),float(-c[2])],size=s.tolist(),probe=[float(probe[0]),float(probe[1]),float(-probe[2])],normal=[float(n[0]),float(n[1]),float(-n[2])],health=1))
    assert len(chunks)<=4096
    manifest.update(chunks=len(chunks),destructibles=definitions,chunkPanelIds=owners,chunkClearGlass=clear)
    (OUT/'map.json').write_text(json.dumps(manifest,indent=2)+'\n')
    with(OUT/'geometry.bytes').open('wb')as f:
        f.write(struct.pack('<ii',magic,len(chunks)))
        for mid,coll,visible,v,idx in chunks:f.write(struct.pack('<iiiii',mid,coll,visible,len(v),len(idx)));f.write(v.astype('<f4').tobytes());f.write(idx.astype('<i4').tobytes())
    source_inputs={str(R23/relative):sha(R23/relative)for relative in ['scripts/archive_semantics_r8.gd','scripts/archive_semantics_r9.gd','scripts/archive_panel_r8.gd','scripts/archive_passage_cut_r9.gd','scripts/archive_panel_batches_r17.gd','assets/materials/clear_glass_r10.gdshader','assets/data/archive_semantics_r9.json','assets/data/map_semantics_r14.json']};source_inputs[str(SOURCE)]=HASH
    result=dict(mapId=manifest['mapId'],originalGeometrySha256=hashlib.sha256(raw).hexdigest(),geometrySha256=sha(OUT/'geometry.bytes'),sourceInputs=source_inputs,panels=len(panels),glass=sum(p['glass']for p in panels),doors=sum(not p['glass']for p in panels),panelTriangles=panel_triangles,originalRenderTriangles=original_render,originalCollisionTriangles=original_collision,staticRenderTriangles=static_render,staticCollisionTriangles=static_collision,basePanelRenderTrianglesRemoved=render_removed,basePanelCollisionTrianglesRemoved=collision_removed,trimOriginalTrianglesCut=changed,trimInsideFragments=fragments,chunks=len(chunks),elapsedSeconds=round(time.monotonic()-started,3),engineRun=False,deviceRun=False,limits='Exact connected components and pane/frame/door grouping plus source aperture clipping; no touch/melee/network triggers or shard FX are added by this bullet damage slice. Source first accepted hit retires each assembly regardless of positive damage magnitude.')
    (OUT/'destructibles-provenance.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2),flush=True)
if __name__=='__main__':main()
