from pathlib import Path
import hashlib,json,struct,subprocess
import numpy as np
ROOT=Path(__file__).resolve().parents[4];TOOLS=Path(__file__).resolve().parent;BASE=ROOT/'unity/Assets/Ashfall';OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Consumables'
p=json.loads((OUT/'provenance.json').read_text())
for name,digest in json.loads((TOOLS/'soda-shaker-preserved.json').read_text()).items():assert hashlib.sha256((OUT/name).read_bytes()).hexdigest()==digest,name+' changed'
print('PASS existing soda/shaker mesh and material payloads byte-identical')
SOURCE=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
for source,digest in p['inputs'].items():assert hashlib.sha256((SOURCE/source).read_bytes()).hexdigest()==digest
for model in p['models']:
    kind=model['kind'];manifest=json.loads((OUT/(kind+'.json')).read_text());data=(OUT/(kind+'.meshbin')).read_bytes();offset=8;magic,count=struct.unpack_from('<ii',data);assert magic==0x52323343 and count==len(model['surfaces']);caps=[];allpoints=[];triangles=0
    for _ in range(count):
        length,=struct.unpack_from('<i',data,offset);offset+=4;name=data[offset:offset+length].decode();offset+=length;nv,nt,mi,cap=struct.unpack_from('<iiii',data,offset);offset+=16
        vertices=np.frombuffer(data,'<f4',nv*6,offset).reshape(nv,6);offset+=nv*24;indices=np.frombuffer(data,'<i4',nt,offset);offset+=nt*4
        assert np.isfinite(vertices).all() and np.allclose(np.linalg.norm(vertices[:,3:],axis=1),1,atol=.0001) and indices.min()>=0 and indices.max()<nv
        allpoints.extend(vertices[:,:3]);triangles+=nt//3
        if cap:caps.append(name)
    assert offset==len(data) and triangles==model['triangles'] and caps
    bounds=np.ptp(np.array(allpoints),axis=0);assert bounds[1]>.12 and bounds[1]<.25 and bounds[0]<.11
    print('PASS actual source recipe',kind,count,'parts',triangles,'triangles','removable parts',len(caps))
sources=[BASE/'Core/Contracts.cs']+list((BASE/'RecoveryContracts').glob('*.cs'))+list((BASE/'Modules/Pickups').glob('*.cs'))+[BASE/'Integration/Playable/Consumables/R23ConsumableState.cs',BASE/'Integration/Playable/Consumables/LocalPickupRelocation.cs',BASE/'Integration/Playable/Consumables/R23BananaPeelGeometry.cs']
csc=Path(r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
def compile(name,files,extra):
    args=['/nologo','/langversion:5','/warnaserror+','/warn:4','/out:'+str(TOOLS/name)]+extra+[str(x) for x in files];rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+x+'"' for x in args));r=subprocess.run([str(csc),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(TOOLS/(name+'.log')).write_text(r.stdout+r.stderr);print(name,r.returncode,r.stdout,r.stderr);assert r.returncode==0
compile('mechanics.exe',sources+[TOOLS/'Mechanics.cs'],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(TOOLS/'mechanics.exe')],capture_output=True,text=True);(TOOLS/'mechanics-results.txt').write_text(r.stdout+r.stderr);print(r.stdout,r.stderr);assert r.returncode==0
compile('slot-replacement.exe',sources+[TOOLS/'SlotReplacementCases.cs'],['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll'])
r=subprocess.run([str(TOOLS/'slot-replacement.exe')],capture_output=True,text=True);(TOOLS/'slot-replacement-results.txt').write_text(r.stdout+r.stderr);print(r.stdout,r.stderr);assert r.returncode==0
editor=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');refs=list((editor/'Managed/UnityEngine').glob('*.dll'))+list((editor/'NetStandard/ref/2.1.0').glob('*.dll'))
compile('Consumables.Native.dll',sources+[BASE/'Integration/Playable/Consumables/R23VRConsumables.cs',BASE/'Integration/Playable/HandsWeapons/R23TrackedGripPose.cs',BASE/'Integration/Playable/PickupHints/R23PickupHintGeometry.cs',BASE/'Integration/Playable/Consumables/R23ConsumableAudio.cs',BASE/'Integration/Playable/Consumables/R23ConsumableAudioPolicy.cs'],['/target:library','/nostdlib+','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(x) for x in refs])
