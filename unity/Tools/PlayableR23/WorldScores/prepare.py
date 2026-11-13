"""Source-bound scoreboard faces, including prepared R9 and measured R18/R19.
CPU only; immutable source/pack/current map geometry are never modified.
"""
from pathlib import Path
import sys,json,hashlib,struct
import numpy as np
ROOT=Path(__file__).resolve().parents[4]
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');SRC=FROZEN/'source'
sys.path.insert(0,str(ROOT/'unity/Tools/PlayableR23/World/ArchitectureR6'))
from godot_resource import Resource
sys.path.insert(0,str(ROOT/'campaign-staging/20261104-three-chats/world/r12-fixture-shard-1'))
from pack_reader import Pack
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
assert sha(FROZEN/'SOURCE-MANIFEST.json')=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={r['path']:r['sha256'] for r in json.loads((FROZEN/'SOURCE-MANIFEST.json').read_text())['files']}
used={}
def source(p):
    assert sha(SRC/p)==pins[p],p
    used[p]=pins[p];return SRC/p
adapters=json.loads(source('assets/data/archive_adapters_r10.json').read_text())['files']
semantics=json.loads(source('assets/data/map_semantics_r14.json').read_text())['maps']
extra=json.loads(Path('D:/ashfall-r6-staging/world/playtest-6237-map-bindings-v001/SCOREBOARD-SOURCE-APERTURES.json').read_text())
for p,h in extra['sourcePins'].items():assert sha(source(p))==h
def arrays(mesh):
    out=[]
    for s in mesh['props']['_surfaces']:
        n=s['vertex_count'];v=np.frombuffer(s['vertex_data'],'<f4',n*3).reshape(n,3).copy()
        assert len(s['vertex_data'])>=n*12
        ix=np.frombuffer(s['index_data'],'<u2' if n<65536 else '<i4').astype(int) if s.get('index_data') else np.arange(n)
        attr=s.get('attribute_data',b'');stride=len(attr)//n if n else 0;offset=4 if s['format']&8 else 0
        uv=np.ndarray((n,2),'<f4',attr,offset=offset,strides=(stride,4)).copy() if s['format']&16 else None
        out.append((v,ix,uv))
    return out
def digest(mesh):
    faces=np.concatenate([v[ix] for v,ix,_ in arrays(mesh)])
    step=float(np.float32(.0001));snapped=(np.floor(faces.astype(float)/step+.5)*step).astype('<f4')
    return hashlib.sha256(snapped.tobytes()).hexdigest()
def reflect(v):return [float(v[0]),float(v[1]),-float(v[2])]
pack=Pack(ROOT/'builds/ASHFALL-Map-Adapters-R10.pck');maps=[];checked=[]
try:
    for slug,expected in adapters.items():
        folder='World' if slug=='battlefield-paintball-gods-pitdevil' else 'Maps/'+slug
        live=ROOT/'unity/Assets/StreamingAssets/PlayableR23'/folder/'map.json'
        if not live.exists():continue
        current=json.loads(live.read_text());raw=pack.read('local_archive_r10/'+slug+'.res');assert hashlib.sha256(raw).hexdigest()==expected,slug
        r=Resource(raw);details=next(x['props']['details'] for x in r.resources if 'details' in x['props']);faces=[]
        for mi,ref in enumerate(details.get('score_faces_r9',[])):
            assert ref[1]==2
            for si,(v,ix,uv) in enumerate(arrays(r.resources[ref[2]])):
                assert uv is not None and np.isfinite(v).all() and np.isfinite(uv).all()
                faces.append(dict(id=f'prepared-r9/{mi}/{si}',vertices=[reflect(p) for p in v],uv=uv.tolist(),indices=ix.reshape(-1,3)[:,[0,2,1]].ravel().tolist()))
        extras=[x for x in extra['faces'] if x['mapId']=='archive:'+slug]
        if extras:
            assert semantics[slug]['cache_fingerprint']==extras[0]['cacheFingerprint']
            nodes={n['name']:n for n in r.nodes()}
            for entry in extras:
                for binding in entry['sourceFrames']:
                    node=nodes[binding['path']];assert node['props'].get('transform',[1,0,0,0,1,0,0,0,1,0,0,0])==[1,0,0,0,1,0,0,0,1,0,0,0]
                    ref=node['props']['mesh'];assert ref[1]==2;assert digest(r.resources[ref[2]])==binding['sha256'],(slug,binding['path'])
                faces.append(dict(id=entry['sourceClass']+'/'+str(len(faces)),vertices=entry['unityLocalCornersTLBLBRTR'],uv=entry['uv'],indices=entry['unityIndices']))
        checked.append(dict(mapId='archive:'+slug,preparedSha256=expected,legacyR9=len(details.get('score_faces_r9',[])),measured=len(extras),faces=len(faces)))
        if not faces:continue
        assert current['sourceSha256']==semantics[slug]['source_sha256']
        for face in faces:
            face['vertices']=np.asarray(face['vertices']).ravel().tolist();face['uv']=np.asarray(face['uv']).ravel().tolist()
        maps.append(dict(mapId='archive:'+slug,sourceSha256=current['sourceSha256'],cacheFingerprint=semantics[slug]['cache_fingerprint'],preparedSha256=expected,faces=faces))
finally:pack.f.close()
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/WorldScores';OUT.mkdir(parents=True,exist_ok=True)
(OUT/'boards.json').write_text(json.dumps(dict(version=1,maps=maps),indent=2)+'\n')
(OUT/'source.json').write_text(json.dumps(dict(inputs=used,preparedChecked=checked,sourceCanvas=[1024,1280],refreshSeconds=1,sourceUvOrigin='top-left; runtime texture projection reflects V once',authority='Presentation only. Unavailable score/kill fields are dashes, never inferred from practice targets.'),indent=2)+'\n')
print(json.dumps(dict(mapsChecked=len(checked),mapsWithBoards=len(maps),faces=sum(len(m['faces']) for m in maps),legacy=sum(x['legacyR9'] for x in checked),measured=sum(x['measured'] for x in checked),perMap=[(m['mapId'],len(m['faces'])) for m in maps]),indent=2))
