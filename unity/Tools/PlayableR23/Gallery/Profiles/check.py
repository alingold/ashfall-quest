from pathlib import Path
import subprocess,json,hashlib,struct,re
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];BASE=ROOT/'unity/Assets/Ashfall'
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]
sources+=list((BASE/'Integration/Playable/HandsWeapons').glob('*.cs'))+list((BASE/'Integration/Playable/Gallery').glob('*.cs'));sources=sorted(set(sources))
for case in ['Cases','M1014Cases']:
    exe=HERE/(case+'.exe');rsp=HERE/(case+'.rsp')
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(exe),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p) for p in sources]+[str(HERE/(case+'.cs'))]
    rsp.write_text('\n'.join('"'+a+'"' for a in args));r=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True);print(r.stdout+r.stderr);assert r.returncode==0
    r=subprocess.run([str(exe)],capture_output=True,text=True);print(r.stdout+r.stderr);(HERE/(case+'-results.txt')).write_text(r.stdout+r.stderr);assert r.returncode==0
data=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';payload=[]
for name in ['cx4','px4','m1014','m1014-bolt','m1014-pack','m1014-arrow']:
    manifest=json.loads((data/(name+'.json')).read_text());raw=(data/(name+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x5232334d and count==len(manifest['parts']);offset=8;triangles=0
    for i in range(count):
        nv,ni,mi=struct.unpack_from('<iii',raw,offset);offset+=12;assert nv>=3 and ni>=3 and ni%3==0 and 0<=mi<len(manifest['materials']);offset+=nv*32
        indices=struct.unpack_from('<'+'i'*ni,raw,offset);offset+=ni*4;assert min(indices)>=0 and max(indices)<nv;triangles+=ni//3
    assert offset==len(raw)
    for mat in manifest['materials']:
        for key in ['baseColor','normal','metallicMap','occlusion']:
            if mat.get(key):assert(data/mat[key]).is_file()
    payload.append(dict(id=name,parts=count,triangles=triangles,sha256=hashlib.sha256(raw).hexdigest()))
assert [p['triangles'] for p in payload]==[6188,5400,76594,2548,1584,36]
for id,scale,muzzle in [('cx4',.855,[0,.045,.62]),('px4',.7,[0,.045,.29]),('m1014',1,[0,.045,.77])]:
    fit=json.loads((data/(id+'-fit.json')).read_text());assert fit['modelScale']==scale and fit['weaponGripOrigin']==[0,0,0] and fit['muzzle']==muzzle
audio=json.loads((data/'audio.json').read_text())['cues'];fragment=json.loads((data/'m1014-audio-cues.json').read_text())['cues']
for cue in fragment:
    assert next(c['files'] for c in audio if c['weapon']==cue['weapon'] and c['action']==cue['action'])==cue['files']
    for name,source in zip(cue['files'],cue['sourceFiles']):assert(data/name).read_bytes()==(Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')/source.removeprefix('res://')).read_bytes()
for id in ['cx4','px4']:assert any(c['weapon']==id and c['action']=='shot' for c in audio)
model=(BASE/'Integration/Playable/Gallery/R23GalleryModel.cs').read_text();gallery=(BASE/'Integration/Playable/Gallery/R23LobbyWeaponGallery.cs').read_text()
assert 'else if(Definition.Id=="spr")' in model and 'InitializeCx4Rings(Muzzle)' in model and 'V(Definition.SupportPoint)*reloadScale' in model
assert 'TryApplyHit' not in gallery and 'R23RoundState' not in gallery and 'PreviewVolley(volley)' in gallery and 'IsWeaponReady(ViewedWeaponId)' in gallery
(HERE/'payload-check.json').write_text(json.dumps(dict(payloads=payload,actualFitProfiles=3,exactM1014CueBindings=len(fragment),engineRenderDevice='UNRUN'),indent=2))
print('PASS6 genuine art payloads, source fit profiles, merged exact M1014 audio, profile-specific scope/sight/support and no carried/damage binding')
print('Actual private native owners only; no engine, art import/render, controller or listening run.')
