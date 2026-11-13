from pathlib import Path
import subprocess,re,json,struct,hashlib
ROOT=Path(__file__).resolve().parents[4];TOOLS=Path(__file__).resolve().parent;BASE=ROOT/'unity/Assets/Ashfall'
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe');EDITOR=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
sources=[ROOT/p for p in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]
sources+=[BASE/'Integration/Playable/Gallery/R23GallerySession.cs',BASE/'Integration/Playable/Gallery/R23MagnumGallerySession.cs']
sources+=[BASE/'Integration/Playable/HandsWeapons'/name for name in ['R23MagnumRuntime.cs','R23MagnumOwner.cs']]
sources+=list((BASE/'Integration/Playable/HandsWeapons').glob('*.cs'))+list((BASE/'Integration/Playable/Gallery').glob('*.cs'))
sources=sorted(set(sources))
def run(name,files,extra):
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(TOOLS/name)]+extra+[str(p) for p in files]
 rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args));result=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(TOOLS/(name+'.log')).write_text(result.stdout+result.stderr);print(name,result.returncode,result.stdout+result.stderr);assert result.returncode==0
run('gallery-cases.exe',sources+[TOOLS/'Cases.cs'],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
result=subprocess.run([str(TOOLS/'gallery-cases.exe')],capture_output=True,text=True);(TOOLS/'results.txt').write_text(result.stdout+result.stderr);print(result.stdout+result.stderr);assert result.returncode==0
run('magnum-gallery-cases.exe',sources+[TOOLS/'MagnumCases.cs'],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
magnum=subprocess.run([str(TOOLS/'magnum-gallery-cases.exe')],capture_output=True,text=True);(TOOLS/'magnum-results.txt').write_text(magnum.stdout+magnum.stderr);print(magnum.stdout+magnum.stderr);assert magnum.returncode==0
source=(ROOT/'quest-r24/source/scripts/lobby_gallery_091.gd').read_text();assert 'preview_shots097+=1' in source and 'distance>2.0' in source and 'distance>.35' in source and 'live097=original.get_script().new()' in source
print('PASS actual source gallery dock/reach/fresh live097 references inspected')
data=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons';payload=[]
for name in ('magnum','magnum-cylinder','magnum-offer'):
 manifest=json.loads((data/(name+'.json')).read_text());raw=(data/(name+'.meshbin')).read_bytes();magic,count=struct.unpack_from('<ii',raw);assert magic==0x5232334d and count==len(manifest['parts']);offset=8;triangles=0
 for p in range(count):
  nv,nt,mi=struct.unpack_from('<iii',raw,offset);offset+=12;assert nv>=3 and nt>=3 and nt%3==0 and 0<=mi<len(manifest['materials']);offset+=nv*32
  indices=struct.unpack_from('<'+'i'*nt,raw,offset);offset+=nt*4;assert min(indices)>=0 and max(indices)<nv;triangles+=nt//3
 assert offset==len(raw)
 for material in manifest['materials']:
  for key in ('baseColor','normal','metallicMap','occlusion'):
   if material.get(key):assert (data/material[key]).is_file()
 payload.append(dict(name=name,parts=count,triangles=triangles,sha256=hashlib.sha256(raw).hexdigest()))
for i in range(7):
 assert 'LiveRound'+str(i) in json.loads((data/'magnum-cylinder.json').read_text())['parts']
 assert len([p for p in json.loads((data/'magnum-offer.json').read_text())['parts'] if p.startswith('OfferRound'+str(i)+'.')])>=4
model=(BASE/'Integration/Playable/Gallery/R23MagnumGalleryModel.cs').read_text();gallery=(BASE/'Integration/Playable/Gallery/R23LobbyWeaponGallery.cs').read_text()
assert 'view=owner.AddComponent<R23MagnumView>()' in model and 'R23MagnumRuntime.ModelScale' in model and 'new R23Audio(Root,Muzzle)' in model
assert 'TryApplyHit' not in gallery and 'R23RoundState' not in gallery and 'SourceRifleDefinition.ForId("magnum")' not in gallery
(TOOLS/'magnum-payload-check.json').write_text(json.dumps(dict(payloads=payload,privateReserve=1200,capacity=7,noCarriedOwnerReference=True,engineRenderDevice='UNRUN'),indent=2))
print('PASS actual Magnum body/seven-chamber/seven-round-offer payloads, exact source view/audio adapter and no carried-owner/damage binding')
native=sorted(set(sources+list((BASE/'Integration/Playable/HandsWeapons').glob('*.cs'))+list((BASE/'Integration/Playable/Gallery').glob('*.cs'))))
refs=list((EDITOR/'Managed/UnityEngine').glob('*.dll'))+list((EDITOR/'NetStandard/ref/2.1.0').glob('*.dll'))
run('gallery-native.dll',native,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
print('CPU/native API checks only; no engine, rendering or input device run.')
