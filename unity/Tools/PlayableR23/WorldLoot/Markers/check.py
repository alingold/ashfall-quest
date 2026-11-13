"""Read staged bytes independently; no live/source mutation or engine call."""
from pathlib import Path
import collections,hashlib,importlib.util,json,mmap,struct
import numpy as np
from prepare import HERE,ROOT,SOURCE,MAPS,RED,sha

spec=importlib.util.spec_from_file_location('source_cache_export',HERE.parent/'prepare.py')
source=importlib.util.module_from_spec(spec);spec.loader.exec_module(source)
def read_chunks(path):
    raw=path if isinstance(path,(bytes,bytearray))else path.read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x52323357;cursor=8;chunks=[]
    for ci in range(count):
        begin=cursor;mid,co,vi,nv,ni=struct.unpack_from('<iiiii',raw,cursor);cursor+=20
        assert 0<=co<=1 and 0<=vi<=1 and 1<=nv<=200000 and 3<=ni<=3000000 and ni%3==0
        vertex_at=cursor;cursor+=nv*48;indices=np.frombuffer(raw,dtype='<i4',count=ni,offset=cursor);assert min(indices)>=0 and max(indices)<nv;cursor+=ni*4
        chunks.append((mid,co,vi,nv,ni,raw[begin:cursor],raw[vertex_at:cursor]))
    assert cursor==len(raw);return chunks

for slug,(folder,expected) in MAPS.items():
    live=ROOT/'unity/Assets/StreamingAssets/PlayableR23'/folder;out=HERE/'StagedPayload'/slug
    proof=json.loads((out/'marker-removal.json').read_text());assert expected==proof['originalGeometrySha256']
    live_bytes=(live/'geometry.bytes').read_bytes();live_hash=hashlib.sha256(live_bytes).hexdigest()
    assert live_hash in [expected,proof['geometrySha256']]
    if live_hash!=expected:
        # Reconstruct captured original flags in memory; no rollback write/copy.
        original=bytearray(live_bytes);cursor=8;wanted={r['chunk']for r in proof['changes']}
        for ci in range(struct.unpack_from('<i',original,4)[0]):
            mid,co,vi,nv,ni=struct.unpack_from('<iiiii',original,cursor)
            if ci in wanted:assert(co,vi)==(0,0);struct.pack_into('<ii',original,cursor+4,1,1)
            cursor+=20+nv*48+ni*4
        assert hashlib.sha256(original).hexdigest()==expected
        live_bytes=bytes(original)
    assert sha(out/'geometry.bytes')==proof['geometrySha256']
    assert (out/'map.json').read_bytes()==(live/'map.json').read_bytes()
    manifest=json.loads((out/'map.json').read_text());before=read_chunks(live_bytes);after=read_chunks(out/'geometry.bytes')
    assert len(before)==len(after)==manifest['chunks'];changed=[];r0=c0=r1=c1=0
    for ci,(old,new) in enumerate(zip(before,after)):
        mid,co,vi,nv,ni,chunk,body=old;r0+=ni//3*vi;c0+=ni//3*co
        r1+=new[4]//3*new[2];c1+=new[4]//3*new[1]
        assert new[0]==mid and new[3:5]==(nv,ni) and body==new[6],('attribute/index mutation',ci)
        if old==new:continue
        changed.append(ci)
        assert manifest['materials'][mid]['name']==RED and (co,vi)==(1,1) and new[1:3]==(0,0)
        assert chunk[:4]==new[5][:4] and chunk[12:]==new[5][12:]
        if 'chunkPanelIds'in manifest:assert manifest['chunkPanelIds'][ci]==-1 and not manifest['chunkClearGlass'][ci]
    census=proof['census'];assert [r0,c0,r1,c1]==[census[k]for k in ['renderBefore','collisionBefore','renderAfter','collisionAfter']]
    assert changed==[row['chunk']for row in proof['changes']] and len(changed)==census['disabledChunks']
    # Independent original prepared-geometry reader/ordered components, maintained
    # by the source-cache exporter. Its input is fingerprint/hash-bound geometry.glb;
    # staging recovered exact original raw-map.glb members instead.
    batches,qualification=source.red_batches(slug);selectors=json.loads((SOURCE/'assets/data/map_caches_r17.json').read_text())['maps'][slug]['marker_parts']
    counts=collections.Counter();triangles=collections.Counter();all_keys=set()
    for name,faces in batches.items():
        for part in source.red_components(faces):
            if source.is_ring(part):kind='r8-ring'
            elif source.is_question(part):kind='r8-question'
            else:
                assert any(row['key']==RED and row['triangles']==part['triangles'] and np.sum((np.array(row['center'])-part['center'])**2)<.000016 for row in selectors),(slug,name,part['center'],part['triangles'])
                kind='r17-remainder'
            counts[kind]+=1;triangles[kind]+=part['triangles']
        xyz=faces.copy();xyz[:,:,2]*=-1;packed=np.ascontiguousarray(xyz,dtype='<f4').view('V12').reshape(-1,3)
        all_keys.update(bytes(k)for k in np.sort(packed,axis=1).copy().view('V36').ravel())
    assert len(all_keys)==proof['uniqueMarkerTriangles'] and hashlib.sha256(b''.join(sorted(all_keys))).hexdigest()==proof['markerTriangleKeysSha256']
    assert sum(triangles.values())==r0-r1==c0-c1
    for kind in ['r8-ring','r8-question']:assert counts[kind]==proof['r8'][kind]['components'] and triangles[kind]==proof['r8'][kind]['triangles']
    print('PASS',slug,'source components',dict(counts),'triangles',dict(triangles),'exact render/collision cut',r0-r1,'unchanged complete chunks',len(before)-len(changed),'map/materials/vertices/normals/UV/colors/indices/destructible maps byte-identical')
    assert sha(live/'geometry.bytes')==live_hash and sha(live/'map.json')==proof['originalMapSha256']
print('PASS captured/staged render/collision marker removal; check performed no engine/device/source/live writes')
