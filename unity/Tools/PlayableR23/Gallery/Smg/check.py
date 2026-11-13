from pathlib import Path
import subprocess,json,struct,hashlib
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];BASE=ROOT/'unity/Assets/Ashfall';CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]
sources+=list((BASE/'Integration/Playable/HandsWeapons').glob('*.cs'))+list((BASE/'Integration/Playable/Gallery').glob('*.cs'));sources=sorted(set(sources))
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p) for p in sources]+[str(HERE/'Cases.cs')]
rsp=HERE/'cases.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args));r=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True);print(r.stdout+r.stderr);assert r.returncode==0
r=subprocess.run([str(HERE/'cases.exe')],capture_output=True,text=True);print(r.stdout+r.stderr);(HERE/'results.txt').write_text(r.stdout+r.stderr);assert r.returncode==0
data=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';rows=[]
for id in ['ump','p90','mp5','mp9','p90-insert-arrow','p90-charge-arrow']:
    manifest=json.loads((data/(id+'.json')).read_text());raw=(data/(id+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x5232334d and count==len(manifest['parts']);offset=8;triangles=0
    for i in range(count):
        nv,ni,mi=struct.unpack_from('<iii',raw,offset);offset+=12;assert nv>=3 and ni>0 and ni%3==0 and 0<=mi<len(manifest['materials']);offset+=nv*32
        indices=struct.unpack_from('<'+'i'*ni,raw,offset);offset+=ni*4;assert min(indices)>=0 and max(indices)<nv;triangles+=ni//3
    assert offset==len(raw)
    for material in manifest['materials']:
        for key in ['baseColor','normal','metallicMap','occlusion']:
            if material.get(key):assert(data/material[key]).is_file()
    rows.append(dict(id=id,parts=manifest['parts'],triangles=triangles,sha256=hashlib.sha256(raw).hexdigest()))
audio=json.loads((data/'audio.json').read_text())['cues']
for row in rows[:4]:
    id=row['id'];fit=json.loads((data/(id+'-fit.json')).read_text());assert row['triangles']==fit['visibleTriangles']
    assert fit['weaponGripOrigin']==[0,0,0] and fit['ammoFamily']=='smg' and 'Magazine' in row['parts'] and 'Bolt' in row['parts']
    assert fit['reloadScale']==(1 if id in ['ump','mp9'] else fit['modelScale'])
    assert any(c['weapon']==id and c['action']=='shot' for c in audio)
    for cue in audio:
        if cue['weapon']==id:
            for name in cue['files']:assert (data/name).is_file()
    if id=='p90':assert fit['heldMagazineGunBasis'] and fit['chargingMirrorX'] and fit['chargingGrip'][0]==-.05 and fit['insertionAxis']=='Unity +Z'
    if id in ['mp5','mp9']:assert fit['opticScale']==.75
    if id=='mp9':assert fit['laserRange']==180 and not fit['worldCrosshair']
base=BASE/'Integration/Playable/Gallery';model=(base/'R23GalleryModel.cs').read_text();session=(base/'R23GallerySession.cs').read_text();gallery=(base/'R23LobbyWeaponGallery.cs').read_text();fixture=(base/'R23ExpandedGalleryFixture.cs').read_text()
assert 'R23GalleryFit.Load(Definition.Id,Definition.ModelScale' in model and 'Initialize(Definition.ModelScale,Muzzle,fit.opticMountHeight,fit.opticScale)' in model
assert 'held.rotation=Root.rotation' in model and '/reloadScale' in model and 'new R23Mp9Laser(Root)' in model and 'clear(viewer.transform.position,start)' in model and 'raycast(start,Muzzle.forward,180,out hit)' in model
assert 'magazineControls.Project' in session and 'live.StepSource' in session and 'session.StepSource' in gallery and 'RunSmg' in fixture and 'RunAll' in fixture
(HERE/'payload-check.json').write_text(json.dumps(dict(payloads=rows,profiles=4,sourceInfinitePreviewReserve=True,engineRenderDevice='UNRUN'),indent=2))
print('PASS4actual SMG payload/fit/audio bindings,2source P90 arrows, actual shared projector/owner/optic/laser, no native render claim')
print('Actual private native mechanical paths; engine/render/controller/listening unrun.')
