from pathlib import Path
import hashlib,json
ROOT=Path(__file__).resolve().parents[4];SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/JumpPads';OUT.mkdir(parents=True,exist_ok=True)
sem=json.loads((SRC/'assets/data/map_semantics_r14.json').read_text())['maps'];archive=json.loads((SRC/'assets/data/archive_semantics_r9.json').read_text())['maps'];maps=[]
for slug in ['battlefield-paintball-gods-pitdevil','alfa-aglae-souza','classic-bigbox-vr']:
    row=sem[slug];meta=archive['archive:'+slug];assert row['source_sha256']==meta['source_sha256']
    pads=[]
    for i,pad in enumerate(row.get('jump',[])):
        center=list(pad['center']);normal=list(pad['normal']);center[2]*=-1;normal[2]*=-1
        pads.append({'sourceIndex':i,'center':center,'normal':normal,'radius':pad['radius'],'speed':pad.get('speed',45),'tilted':pad.get('tilted',False)})
    if slug.startswith('battlefield'):assert not pads and not any('jumppad' in str(material).lower() for material in meta['materials'].values())
    maps.append({'mapId':'archive:'+slug,'sourceSha256':row['source_sha256'],'pads':pads})
inputs={}
for map_id,relative in [('range','scripts/sandbox_map_091.gd'),('valley','scripts/world.gd')]:
    digest=hashlib.sha256((SRC/relative).read_bytes()).hexdigest();maps.append({'mapId':map_id,'sourceSha256':digest,'pads':[]});inputs[relative]=digest
for relative in ['assets/data/map_semantics_r14.json','assets/data/archive_semantics_r9.json','scripts/map_semantics_r14.gd','scripts/archive_gameplay_r8.gd','scripts/archive_semantics_r8.gd','scripts/archive_semantics_r9.gd']:
    inputs[relative]=hashlib.sha256((SRC/relative).read_bytes()).hexdigest()
(OUT/'pads.json').write_text(json.dumps({'version':1,'maps':maps},indent=2)+'\n')
(OUT/'provenance.json').write_text(json.dumps({'baseline':'original preserved Opus R23 2026-11-03 ashfall-r23/source','inputs':inputs,'maps':[{'mapId':m['mapId'],'sourceSha256':m['sourceSha256'],'pads':len(m['pads']),'tilted':sum(p['tilted'] for p in m['pads'])} for m in maps],'conversion':'Actual source rows only. Reflect local center/normal Z once, no campaign4800 offset. Preserve source order, radius and speed. Paintball has no native rows or legacy jump-pad material; no replacement pads invented.'},indent=2))
print([(m['mapId'],len(m['pads']),sum(p['tilted'] for p in m['pads'])) for m in maps])
