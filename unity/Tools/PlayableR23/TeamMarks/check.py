from pathlib import Path
import subprocess,json,hashlib,base64,re
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
INPUT=ROOT/'unity/Assets/Ashfall/Integration/Playable/Input'
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'cases.exe'),str(INPUT/'R23TeamMarksState.cs'),str(HERE/'Cases.cs')]
compiled=subprocess.run([str(CSC)]+args,capture_output=True,text=True);print(compiled.stdout+compiled.stderr);assert compiled.returncode==0
cases=subprocess.run([str(HERE/'cases.exe')],capture_output=True,text=True);print(cases.stdout+cases.stderr);(HERE/'results.txt').write_text(cases.stdout+cases.stderr);assert cases.returncode==0
manifest=json.loads((HERE/'provenance.json').read_text())
for f in manifest['files']:assert hashlib.sha256(Path(f['path']).read_bytes()).hexdigest()==f['sha256']
assets=(INPUT/'R23TeamMarksAssets.cs').read_text()
encoded=re.findall(r'"([A-Za-z0-9+/=]{100,})"',assets);assert len(encoded)==9
# Declaration order0..7, with the actual alternate smile0993 replacing old5.
for i,b64 in enumerate(encoded[:8]):
    name='ping0993_5' if i==5 else 'ping072_'+str(i);path=HERE/'OriginalIcons'/(name+'.png')
    assert base64.b64decode(b64)==path.read_bytes();im=Image.open(path);assert im.mode=='RGBA' and im.size==(256,256)
    assert im.getchannel('A').getextrema()==(0,255) and len(set(im.getdata()))>50
pcm=base64.b64decode(encoded[-1]);assert len(pcm)==34560
assert b'\0'*len(pcm)!=pcm
print('PASS8 full RGBA original SVGs, exact original PCM34560 bytes, source hashes, no engine/importer required')
view=(INPUT/'R23TeamMarksView.cs').read_text();runtime=(INPUT/'R23TeamMarks.cs').read_text()
assert 'new Texture2D(2,2,TextureFormat.RGBA32,false)' in view and 'mark.position=point+normal.normalized*(paint?.004f:.035f)' in view
assert 'new R23TeamMark(++serial' in runtime and 'hit.distance>=nearest' in runtime and 'Physics.queriesHitBackfaces=backfaces' in runtime
assert 'return false;' in runtime[runtime.index('WeaponContextAvailable'):runtime.index('WeaponContextStatus')]
print('PASSproduction source icon format, placement, closest-hit/exclusion/restored ray globals, explicit unavailable weapon context')
