"""Focused geometry/source checks; does not start Unity, a renderer or a device."""
from pathlib import Path
import hashlib,json,struct,wave
import numpy as np

ROOT=Path(__file__).resolve().parents[4]
SRC=ROOT/'quest-r24/source'
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
provenance=json.loads((OUT/'provenance.json').read_text())
for entry in provenance['inputs']:
    assert hashlib.sha256((SRC/entry['source']).read_bytes()).hexdigest()==entry['sha256'],entry['source']
for entry in provenance['geometry']:
    raw=(OUT/(entry['name']+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x5232334d
    manifest=json.loads((OUT/(entry['name']+'.json')).read_text());materials=manifest['materials'];assert len(manifest['parts'])==count
    if entry['name'] in ['mk18','akm','spr']:assert 'Magazine' in manifest['parts']
    if entry['name'] in ['akm','spr']:assert 'Bolt' in manifest['parts']
    if entry['name']=='spr-scope':assert 'RearOcularGlass' in manifest['parts'] and 'ReticleViewPlane' not in manifest['parts'] and any(m.get('alphaMode')=='BLEND' for m in materials)
    for material in materials:
        for key in ['baseColor','normal','metallicMap','occlusion']:
            if material.get(key):assert (OUT/material[key]).is_file(),material[key]
    offset=8;vertices=triangles=0;positions=[]
    for _ in range(count):
        nv,nt,mi=struct.unpack_from('<iii',raw,offset);offset+=12
        assert nv>0 and nt>0 and nt%3==0 and 0<=mi<len(materials)
        v=np.frombuffer(raw,'<f4',nv*8,offset).reshape(-1,8);offset+=nv*32
        indices=np.frombuffer(raw,'<i4',nt,offset);offset+=nt*4
        assert np.isfinite(v).all() and indices.min()>=0 and indices.max()<nv
        assert np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=.001)
        vertices+=nv;triangles+=nt//3;positions.append(v[:,:3])
    assert offset==len(raw) and vertices==entry['vertices'] and triangles==entry['triangles']
    bounds=np.concatenate(positions);size=bounds.max(axis=0)-bounds.min(axis=0)
    if entry['name']=='mk18':assert .97<size[2]*.92<.99
    if entry['name']=='akm':assert 1.05<size[2]*.945<1.07
    if entry['name']=='spr':assert 1.19<size[2]*1.32<1.20
    print(f"PASS {entry['name']}: {triangles} source triangles, valid materials/indices/normals, raw bounds {size.round(4).tolist()}")
print('PASS payload matches quest-r24 source hashes; source-fit lengths MK18 .978m / AKM 1.060m / SPR 1.194m')
audio=json.loads((OUT/'audio.json').read_text())
routes=json.loads((SRC/'assets/audio/reference0992/manifest.json').read_text())
audio_count=0
for cue in audio['cues']:
    assert cue['files'],cue
    expected=routes['weapons'].get(cue['weapon'],{}).get(cue['action'],[]) if cue['weapon'] else routes['generic'].get(cue['action'],[])
    if not expected:expected=[f"res://assets/audio/sfx/{cue['action']}_{i:02d}.wav" for i in range(2)]
    assert len(expected)==len(cue['files'])
    for file,source in zip(cue['files'],expected):
        assert (OUT/file).read_bytes()==(SRC/source.removeprefix('res://')).read_bytes(),file
        with wave.open(str(OUT/file),'rb') as wav:
            assert wav.getnchannels() in (1,2) and wav.getsampwidth()==2 and wav.getnframes()>0
            assert 8000<=wav.getframerate()<=96000 and wav.getnframes()/wav.getframerate()<10
        audio_count+=1
assert any(c['weapon']=='mk18' and c['action']=='shot' for c in audio['cues'])
assert any(c['weapon']=='akm' and c['action']=='shot' for c in audio['cues'])
assert any(c['weapon']=='spr' and c['action']=='shot' for c in audio['cues'])
print(f'PASS {audio_count} exact source WAV cues: valid PCM, source hashes recorded, MK18/AKM/SPR routing present')
