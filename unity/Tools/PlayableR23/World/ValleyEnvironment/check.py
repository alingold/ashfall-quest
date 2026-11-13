from pathlib import Path
import hashlib,json,math,subprocess
import numpy as np
from prepare import ROOT,EXPORT,OUT,rgb9995,ambient_samples
TOOLS=Path(__file__).resolve().parent
component=ROOT/'unity/Assets/Ashfall/Integration/Playable/World/R23ValleyEnvironment.cs'
manifest=json.loads((OUT/'environment.json').read_text());world=json.loads((EXPORT/'world.json').read_text());report=[]
raw=(OUT/'sky.rgb9e5').read_bytes();source=EXPORT/manifest['sourceTexture']['image']['rawFile']
assert raw==source.read_bytes() and len(raw)==8388608
assert hashlib.sha256(raw).hexdigest()==manifest['skySha256']==world['resources'][3]['image']['sha256']
assert manifest['sourceEncoding']=='RGBE9995' and manifest['sourceFormat']==16
rgb=rgb9995(raw);rgba=np.c_[rgb,np.ones(len(rgb))].astype('<f2');assert np.array_equal(rgba[:,:3].astype(np.float64),rgb)
expanded_hash=hashlib.sha256(rgba.tobytes()).hexdigest()
report.append('PASS exact original 8,388,608 HDR bytes; source RGBE9995 retains RGB maxima6.6875/5.875/4.9375, implicit alpha1')
assert manifest['sourceWorldSha256']==hashlib.sha256((EXPORT/'world.json').read_bytes()).hexdigest()
env=world['resources'][0]['properties'];assert manifest['sourceEnvironment']==env
for key,source_key in [('ambientColor','ambient_light_color'),('fogColor','fog_light_color')]:assert manifest[key]==env[source_key]['colorUnconverted']
for key,source_key in [('ambientEnergy','ambient_light_energy'),('ambientSource','ambient_light_source'),('fogDensity','fog_density'),('exposure','tonemap_exposure'),('toneMode','tonemap_mode')]:assert manifest[key]==env[source_key]
assert manifest['ambientSource']==3 and manifest['toneMode']==2 and manifest['exposure']==1 and abs(manifest['skyEnergy']-.4)<1e-7
for original,converted in zip(world['lights'],manifest['lights']):
    t=np.array(original['globalTransform']);p=original['properties'];assert converted['sourceTransform']==t.tolist() and converted['sourceProperties']==p
    reflection=np.diag([1,1,-1]);basis=t[:9].reshape(3,3).T;unity=reflection@basis@reflection
    assert np.array_equal(converted['position'],reflection@t[9:])
    assert np.array_equal(converted['forward'],unity[:,2]) and np.array_equal(converted['up'],unity[:,1])
    assert np.allclose(np.cross(unity[:,1],unity[:,2]),unity[:,0],atol=2e-7)
assert len(manifest['lights'])==14 and sum(l['sourceClass']=='DirectionalLight3D' for l in manifest['lights'])==1
report.append('PASS one directional and13 local lights: exact source properties, energy/color, reflected world positions and forward/up bases')
samples=manifest['ambientSamples'];assert len(samples)==2048 and abs(sum(s['weight'] for s in samples)-4)<1e-12
assert np.allclose(np.linalg.norm([s['direction'] for s in samples],axis=1),1)
assert samples==ambient_samples(rgb,2048,1024)
# Independent full-image spherical integration proves that quadrature does not lose
# source energy, and constant-radiance Lambert illumination remains that radiance.
weights=np.cos(np.arange(1024)*math.pi/1024)-np.cos((np.arange(1024)+1)*math.pi/1024)
mean=(rgb.reshape(1024,2048,3)*weights[:,None,None]).sum(axis=(0,1))/(weights.sum()*2048)
sample_mean=sum(np.array(s['radiance'])*s['weight'] for s in samples)/4
assert np.allclose(mean,sample_mean,rtol=1e-11)
for normal in np.eye(3).tolist()+(-np.eye(3)).tolist():
    lambert=sum(max(0,np.dot(normal,s['direction']))*s['weight'] for s in samples)
    assert abs(lambert-1)<.0013
def uv(unity):
    g=np.array(unity)*[1,1,-1];return ((math.atan2(g[0],-g[2])/(2*math.pi))%1,math.acos(g[1])/math.pi)
for direction,expected in [([0,0,1],(0,.5)),([1,0,0],(.25,.5)),([0,0,-1],(.5,.5)),([-1,0,0],(.75,.5)),([0,1,0],(0,0)),([0,-1,0],(0,1))]:assert np.allclose(uv(direction),expected)
report.append('PASS reflected Godot cardinal panorama directions, top-row V convention;2048 source-energy-preserving ambient samples, constant-radiance Lambert error<.0013')
CSC=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe');EDITOR=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
def compile(name,files,extra):
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(TOOLS/name)]+extra+[str(p) for p in files];rsp=TOOLS/(name+'.rsp');rsp.write_text('\n'.join('"'+p+'"' for p in args));r=subprocess.run([str(CSC),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(TOOLS/(name+'.log')).write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
compile('HdrCases.exe',[component,TOOLS/'Cases.cs'],['/target:exe'])
result=subprocess.run([str(TOOLS/'HdrCases.exe'),str(OUT/'sky.rgb9e5')],capture_output=True,text=True);assert result.returncode==0,result.stdout+result.stderr
assert 'EXPANDED_SHA256='+expanded_hash in result.stdout;report.extend(result.stdout.strip().splitlines()[:-1]);report.append('PASS all2,097,152 panorama pixels losslessly expand to half; native C# fallback bytes match independent NumPy hash')
refs=list((EDITOR/'Managed/UnityEngine').glob('*.dll'))+list((EDITOR/'NetStandard/ref/2.1.0').glob('*.dll'))
compile('ValleyEnvironment.Native.dll',[component],['/target:library','/nostdlib+','/define:UNITY_6000_0_OR_NEWER']+['/reference:'+str(p) for p in refs])
report.append('PASS installed Unity6000.6.8f1 API compile with warnings as errors; no engine/shader compiler/GPU/device run')
(TOOLS/'results.txt').write_text('\n'.join(report)+'\n');print('\n'.join(report))
