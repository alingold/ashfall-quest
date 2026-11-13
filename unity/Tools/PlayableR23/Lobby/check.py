"""Focused source-recipe/geometry checks + installed Unity API compile; no engine."""
from pathlib import Path
import hashlib,json,math,struct,subprocess
import numpy as np
ROOT=Path(__file__).resolve().parents[4];HERE=Path(__file__).resolve().parent;OUT=HERE/'StagedPayload';SRC=ROOT/'quest-r24/source'
m=json.loads((OUT/'lobby.json').read_text());p=json.loads((OUT/'provenance.json').read_text());checks=0
def check(ok,label):
    global checks
    assert ok,label
    checks+=1
for file,digest in p['inputs'].items():check(hashlib.sha256((SRC/file).read_bytes()).hexdigest()==digest,'actual source '+file)
check(len(m['towers'])==28,'28 real source tower recipes');check(m['authoredParts'].count('TowerNeonBand')==112,'four source neon bands each');check(len(m['authoredParts'])==151,'source151 authored parts')
check(m['origin']==[1000,0,-1000] and m['spawn']==[0,.3,-3.8],'actual source lobby isolation and spawn');check(m['menu']==[.1,1.3,-1.55] and m['preview']==[1.35,.31,.65],'actual menu and full-size preview anchors')
check(len(m['colliders'])==5,'deck and four invisible source boundaries');deck=m['colliders'][0];check(abs(deck['at'][1]+deck['size'][1]/2)<1e-9,'deck walk surface exactly0')
for t in m['towers']:
    check(-40<=t['x']<=40 and -55<=t['z']<=-18 and 5<=t['height']<=25 and 2<=t['width']<=5,'source tower ranges')
triangles=0
with (OUT/'lobby.meshbin').open('rb') as f:
    check(struct.unpack('<ii',f.read(8))==(0x5232334c,5),'five actual source material surfaces')
    bounds=[]
    for surface in range(5):
        mi,vc,ic=struct.unpack('<iii',f.read(12));a=np.frombuffer(f.read(vc*24),dtype='<f4').reshape(-1,6);indices=np.frombuffer(f.read(ic*4),dtype='<i4');v=a[:,:3];n=a[:,3:];triangles+=ic//3
        check(np.isfinite(a).all() and np.all(indices>=0) and np.all(indices<vc),'finite valid geometry')
        check(np.allclose(np.linalg.norm(n,axis=1),1,atol=2e-5),'unit source normals')
        faces=v[indices.reshape(-1,3)];cross=np.cross(faces[:,1]-faces[:,0],faces[:,2]-faces[:,0]);dots=np.einsum('ij,ij->i',cross,n[indices.reshape(-1,3)].mean(axis=1))
        check(np.all(dots>=-1e-6),'front winding matches normals')
        bounds.append([v.min(axis=0).tolist(),v.max(axis=0).tolist()])
    check(not f.read(),'no unparsed geometry')
check(triangles==16476==m['triangles'],'source triangle total')
check(bounds[0]==[[-11.,-.30000001192092896,-7.],[11.,0.,11.]],'actual deck22x18 positions and source Z reflection')
staged=HERE/'StagedCode';csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe');editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
for source,destination in [(staged,ROOT/'unity/Assets/Ashfall/Integration/Playable/Lobby'),(OUT,ROOT/'unity/Assets/StreamingAssets/PlayableR23/Lobby')]:
    if destination.exists():
        for file in source.rglob('*'):
            if file.is_file():check(file.read_bytes()==(destination/file.relative_to(source)).read_bytes(),'published source/payload matches checked staging')
refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'))
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/nostdlib+','/define:UNITY_6000_0_OR_NEWER','/out:'+str(HERE/'Lobby.Native.dll')]+['/reference:'+str(r) for r in refs]+[str(x) for x in staged.glob('*.cs')]
rsp=HERE/'Lobby.Native.rsp';rsp.write_text('\n'.join('"'+x+'"' for x in args));result=subprocess.run([str(csc),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'Lobby.Native.log').write_text(result.stdout+result.stderr);check(result.returncode==0,result.stdout+result.stderr)
report=f'PASS {checks} focused source, geometry and published-byte checks; 151 authored parts /28 towers /112 bands /16476 triangles /5 material surfaces /5 colliders. Installed Unity6000.6.8f1 API compile passes warnings-as-errors. No engine/shader compilation/GPU/device/build run.\n'
(HERE/'results.txt').write_text(report);print(report)
