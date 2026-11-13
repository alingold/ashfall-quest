from pathlib import Path
import json,hashlib,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];WORLD=Path('unity/Assets/Ashfall/Integration/Playable/World');BASE=Path('unity/Assets/StreamingAssets/PlayableR23/Maps/classic-bigbox-vr');pins=json.loads((HERE/'baseline.json').read_text());prepared=json.loads((HERE/'prepared.json').read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
entries=[]
def add(src,target,before=None):entries.append({'stage':str(src.relative_to(ROOT)),'target':str(target),'before':before,'after':sha(src)})
new=HERE/'Runtime/R24SourceCuts.cs';meta=new.with_suffix('.cs.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
add(new,WORLD/'Destructibles/R24SourceCuts.cs');add(meta,WORLD/'Destructibles/R24SourceCuts.cs.meta')
for name in ['Resources/R24Architecture.shader','SourceArt/R24SourceArtPlan.cs','SourceArt/R24SourceArtMaterials.cs','R23PlayableMap.cs']:
 target=WORLD/name;add(HERE/'Runtime'/name,target,pins[str(target)])
blob=HERE/'Payload/classic-source-cuts-v003.bytes';meta=blob.with_suffix('.bytes.meta')
if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
add(blob,BASE/blob.name);add(meta,BASE/meta.name);add(HERE/'Payload/map.json',BASE/'map.json',prepared['baseMapSha256'])
receipt={'state':'STAGED_READY_FOR_ROOT_GUARDED_PUBLICATION_NATIVE_PENDING','sourcePayload':'D:/ashfall-r6-staging/world/classic-source-cuts-v003/SOURCE-CUT-PARTITIONS.json','sourcePayloadSha256':prepared['sourceCutsSha256'],'geometry':str(BASE/'geometry.bytes'),'geometrySha256':prepared['geometrySha256'],'tower':str(BASE/'classic-tower18-passages319.json'),'towerSha256':prepared['baseTowerSha256'],'architecture':str(BASE/'architecture.json'),'architectureSha256':prepared['architectureSha256'],'sourceArt':'unity/Assets/StreamingAssets/PlayableR23/SourceArt/classic-bigbox-vr.json','sourceArtSha256':prepared['sourceArtSha256'],'files':entries}
(HERE/'publication-plan.json').write_text(json.dumps(receipt,indent=2)+'\n');print('Publication plan ready:',len(entries),'files; original geometry/tower/material data untouched')
