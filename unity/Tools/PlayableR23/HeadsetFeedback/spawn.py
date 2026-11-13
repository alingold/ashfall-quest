"""Settle the original white-ring Paintball lobby spawn on current collision data.
Does not regenerate geometry or touch a released build. Run --apply to update metadata.
"""
import argparse, hashlib, importlib.util, json, struct
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parents[4]
R23=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
payload=ROOT/'unity/Assets/StreamingAssets/PlayableR23/World'
spec=importlib.util.spec_from_file_location('source_spawn',ROOT/'unity/Tools/PlayableR23/World/Classic/spawn.py')
module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
    geometry=payload/'geometry.bytes';raw=geometry.read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x52323357
    parts=[];offset=8
    for _ in range(count):
        material,collision,visible,nv,ni=struct.unpack_from('<iiiii',raw,offset);offset+=20
        vertices=np.frombuffer(raw,dtype='<f4',count=nv*12,offset=offset).reshape(nv,12);offset+=nv*48
        indices=np.frombuffer(raw,dtype='<i4',count=ni,offset=offset);offset+=ni*4
        if collision:parts.append(vertices[indices,:3].reshape(-1,3,3).copy())
    assert offset==len(raw)
    scene=module.TriangleScene(parts)
    semantics=json.loads((R23/'assets/data/map_semantics_r14.json').read_text())['maps']['battlefield-paintball-gods-pitdevil']
    row=semantics['warmup'][0];point=np.array(row['center'],float);point[2]*=-1
    hit=scene.floor(point[0],point[2],point[1]+.6,1.8);assert hit is not None,'White-ring lobby spawn has no current supporting roof'
    floor,normal,witness=hit;proof=module.room(scene,floor);assert proof is not None,'Authored roof spawn lacks support/headroom'
    spawn=(floor+[0,.12,0]).tolist()
    path=payload/'map.json';metadata=json.loads(path.read_text());game=metadata.get('gameSpawn',metadata['spawn'])
    report=dict(source='original Opus R23 map_semantics_r14 warmup white annulus + direction arrow',sourceCenter=row['center'],warmupSpawn=spawn,gameSpawn=game,normal=normal.tolist(),witness=witness,proof=proof,geometrySha256=hashlib.sha256(raw).hexdigest(),nativeVerified=False)
    if args.apply:
        metadata['warmupSpawn']=spawn;metadata['gameSpawn']=game;metadata['recordedSpawn']=metadata.get('recordedSpawn',game)
        path.write_text(json.dumps(metadata,indent=2)+'\n')
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
