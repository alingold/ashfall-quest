from pathlib import Path
import subprocess,json,struct
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];BASE=ROOT/'unity/Assets/Ashfall'
sources=[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]
sources=sorted(set(sources+list((BASE/'Integration/Playable/HandsWeapons').glob('*.cs'))+list((BASE/'Integration/Playable/Gallery').glob('*.cs'))))
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'cases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p) for p in sources]+[str(HERE/'Cases.cs')]
rsp=HERE/'cases.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'cases.exe')],capture_output=True,text=True);(HERE/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
data=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';audio=json.loads((data/'audio.json').read_text())['cues']
for id in ['1911','tec9','rfb','fal','s85']:
    fit=json.loads((data/(id+'-fit.json')).read_text());manifest=json.loads((data/(id+'.json')).read_text());raw=(data/(id+'.meshbin')).read_bytes()
    magic,count=struct.unpack_from('<ii',raw);assert magic==0x5232334d and count==len(manifest['parts']);offset=8;triangles=0
    for i in range(count):
        nv,ni,mi=struct.unpack_from('<iii',raw,offset);offset+=12;assert nv>=3 and ni>0 and ni%3==0 and 0<=mi<len(manifest['materials']);offset+=nv*32
        indices=struct.unpack_from('<'+'i'*ni,raw,offset);offset+=ni*4;assert min(indices)>=0 and max(indices)<nv;triangles+=ni//3
    assert offset==len(raw) and triangles==fit['sourceVisibleTriangles']
    for mat in manifest['materials']:
        for key in ['baseColor','normal','metallicMap','occlusion']:
            if mat.get(key):assert(data/mat[key]).is_file()
    assert fit['weaponGripOrigin']==[0,0,0] and fit['supportPoint']==[0,-.025,.36]
    assert any(c['weapon']==id and c['action']=='shot' and all((data/f).is_file() for f in c['files']) for c in audio),id+' actual shot cue not merged'
    print('PASS actual '+id+' payload/fit/textures/audio '+str(triangles)+' triangles')
model=(BASE/'Integration/Playable/Gallery/R23GalleryModel.cs').read_text()
assert 'AddComponent<SourceCatalog5Optics>()' in model and 'new SourceCatalog5Feedback(definition.Id)' in model and '!fit.hideSeatedMagazine' in model and 'fit.heldMagazineGunBasis' in model
print('PASS genuine five-profile art/fit/optics/recoil/audio binding; no native load/render claim')
