from pathlib import Path
import hashlib,json,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];SOURCE=Path(r'C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HealthStations'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def meta(path):
    m=Path(str(path)+'.meta')
    if not m.exists():m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-original-health:'+str(path.relative_to(ROOT)).replace('\\','/')).hex+'\n'+('folderAsset: yes\n' if path.is_dir() else ''))
def main():
    source=SOURCE/'assets/data/map_semantics_r14.json';sem=json.loads(source.read_text())['maps'];maps=[]
    archive=json.loads((SOURCE/'assets/data/archive_semantics_r9.json').read_text())['maps']
    for slug,row in sem.items():
        assert row['source_sha256']==archive['archive:'+slug]['source_sha256']
        stations=[]
        for i,health in enumerate(row.get('health',[])):
            center=list(health['center']);normal=list(health['normal']);center[2]*=-1;normal[2]*=-1
            stations.append(dict(sourceIndex=i,center=center,normal=normal,radius=health['radius'],healthPerSecond=health['health_per_second']))
        maps.append(dict(mapId='archive:'+slug,sourceSha256=row['source_sha256'],cacheFingerprint=row['cache_fingerprint'],stations=stations))
    OUT.mkdir(parents=True,exist_ok=True);meta(OUT)
    p=OUT/'stations.json';p.write_text(json.dumps(dict(version=1,maps=maps),indent=2)+'\n');meta(p)
    provenance=dict(sourceRoot=str(SOURCE),inputs={r:sha(SOURCE/r)for r in ['assets/data/map_semantics_r14.json','assets/data/archive_semantics_r9.json','scripts/map_semantics_r14.gd']},
        maps=len(maps),mapsWithStations=sum(bool(m['stations'])for m in maps),stations=sum(len(m['stations'])for m in maps),
        conversion='Original source rows, order, rate and radius unchanged; center/normal Z reflected once into Unity local coordinates; native map root transforms to world.',
        geometryAdded=False,engineRun=False,deviceRun=False,limits='Valley/Range have no rows in this archive semantics dataset; no synthetic stations. Physical contact must pass native geometry queries before root health owner receives gain.')
    p=OUT/'provenance.json';p.write_text(json.dumps(provenance,indent=2)+'\n');meta(p)
    meta(ROOT/'unity/Assets/Ashfall/Integration/Playable/JumpPads/R23HealthStations.cs')
    print('Original health rows:',provenance['stations'],'in',provenance['maps'],'maps;',[(m['mapId'],len(m['stations']))for m in maps if m['mapId']in ['archive:battlefield-paintball-gods-pitdevil','archive:alfa-aglae-souza']])
if __name__=='__main__':main()
