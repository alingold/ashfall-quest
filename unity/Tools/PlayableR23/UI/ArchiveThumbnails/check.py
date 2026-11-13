from pathlib import Path
import subprocess,json,hashlib,re
from PIL import Image
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
UI=ROOT/'unity/Assets/Ashfall/Integration/Playable/UI'
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
sources=[UI/'R23UiState.cs',UI/'R23MapSelection.cs',HERE/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'cases.exe')]+[str(p) for p in sources]
run=subprocess.run([str(CSC)]+args,capture_output=True,text=True);print(run.stdout+run.stderr);assert run.returncode==0
run=subprocess.run([str(HERE/'cases.exe')],capture_output=True,text=True);print(run.stdout+run.stderr);(HERE/'results.txt').write_text(run.stdout+run.stderr);assert run.returncode==0
data=json.loads((HERE/'provenance.json').read_text())
for entry in data['entries']:
    path=ROOT/entry['asset'];source=Path(entry['source_path']);raw=path.read_bytes()
    assert raw==source.read_bytes() and hashlib.sha256(raw).hexdigest()==entry['sha256']==entry['receipt']['sha256']
    with Image.open(path) as im:assert im.size==(800,450) and im.mode=='RGB'
    meta=path.with_suffix('.jpg.meta').read_text();assert re.search(r'^guid: [a-f0-9]{32}$',meta,re.M)
    assert 'nPOTScale: 0' in meta and 'alphaIsTransparency: 0' in meta and 'textureType: 0' in meta
    for target in ['Standalone','Android']:
        section=meta.split('buildTarget: '+target)[1].split('  - serializedVersion:')[0]
        assert 'textureFormat: 4' in section and 'textureCompression: 0' in section and 'overridden: 1' in section
    assert 'Resources/'+entry['resource'] in str(path.with_suffix('')).replace('\\','/')
assert len(data['entries'])==3 and data['missing_genuine_previews']==[]
print('PASS3 original RGB JPEG bytes/receipts/resource paths and full-color native-target import metadata; engine/render unrun')
