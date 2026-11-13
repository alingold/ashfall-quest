"""Installed-API compilation + actual source head/part payload checks. No engine/GPU/device."""
from pathlib import Path
import hashlib
import json
import struct
import subprocess
import numpy as np

ROOT=Path(__file__).resolve().parents[4]
OUT=ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/UI/preview-check'
OUT.mkdir(parents=True,exist_ok=True)
SRC=ROOT/'quest-r24/source'
DATA=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Appearance'
BASE=ROOT/'unity/Assets/Ashfall/Integration/Playable'
provenance=json.loads((DATA/'provenance.json').read_text(encoding='utf-8'))
for relative,digest in provenance['inputs'].items():
    assert hashlib.sha256((SRC/relative).read_bytes()).hexdigest()==digest,relative
checks=['Actual R23 source input hashes still match existing converted payload provenance']
for style in provenance['styles']:
    key=style['id'];raw=(DATA/(key+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x52323341 and count==14
    offset=8;parts={}
    for _ in range(count):
        length,=struct.unpack_from('<i',raw,offset);offset+=4;name=raw[offset:offset+length].decode();offset+=length
        nv,nt,mi=struct.unpack_from('<iii',raw,offset);offset+=12
        values=np.frombuffer(raw,dtype='<f4',offset=offset,count=nv*8).reshape(nv,8);offset+=nv*32
        indices=np.frombuffer(raw,dtype='<i4',offset=offset,count=nt);offset+=nt*4
        assert np.isfinite(values).all() and indices.min()>=0 and indices.max()<nv
        assert name not in parts;parts[name]=values
    assert offset==len(raw) and len(parts)==14 and 'AvatarHead' in parts
    head=parts['AvatarHead'][:,:3]
    source=(SRC/f'assets/models/gemini_players099/player_kit_{key}_pcvr.glb').read_bytes()
    length=struct.unpack_from('<I',source,12)[0];g=json.loads(source[20:20+length]);blob=source[28+length:]
    node=next(n for n in g['nodes'] if n.get('name')=='AvatarHead');assert not any(k in node for k in ('matrix','translation','rotation','scale'))
    primitive=g['meshes'][node['mesh']]['primitives'][0];a=g['accessors'][primitive['attributes']['POSITION']];v=g['bufferViews'][a['bufferView']]
    source_head=np.ndarray((a['count'],3),dtype='<f4',buffer=blob,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',12),4)).copy();source_head[:,2]*=-1
    assert np.array_equal(source_head,head),key+' source head changed'
    assert 1.4<float(head[:,1].min())<1.9 and 1.65<float(head[:,1].max())<2.2,key+' authored head is not standing-avatar coordinates'
    assert np.max(np.ptp(head,axis=0))>.15,key+' head is not full-size'
    checks.append(key+': 14 complete parts; AvatarHead exact source positions at standing height, no eye-space double translation')
hands=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
for key in ('left','right'):
    raw=(hands/(key+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x5232334d and count>0
    assert (hands/(key+'.json')).is_file();checks.append(key+': existing source glove mesh/manifest available for preview reuse')

editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
refs=[p for p in (editor/'Managed/UnityEngine').glob('*.dll') if not p.name.startswith('UnityEditor')]+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'))
sources=[BASE/'Appearance/R23PlayerAppearance.cs',BASE/'UI/R23AppearancePreview.cs',BASE/'HandsWeapons/R23Visuals.cs']
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER','/out:'+str(OUT/'R23AppearancePreview.dll')]+['/reference:'+str(p) for p in refs]+[str(p) for p in sources]
rsp=OUT/'compile.rsp';rsp.write_text('\n'.join('"'+arg+'"' for arg in args),encoding='utf-8')
run=subprocess.run([str(csc),'/noconfig','@'+str(rsp)],capture_output=True,text=True);print(run.stdout+run.stderr)
(OUT/'compile.log').write_text(run.stdout+run.stderr,encoding='utf-8');assert run.returncode==0
checks.append('Installed Unity6000.6.8f1 API compilation, C#5 warnings-as-errors PASS')
(OUT/'results.json').write_text(json.dumps(dict(checks=checks,native_render_async_lifecycle_headset='UNRUN: coordinator engine/device lane'),indent=2),encoding='utf-8')
print('\n'.join('PASS '+check for check in checks))
