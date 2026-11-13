from pathlib import Path
import hashlib,json,subprocess,struct
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];BASE=ROOT/'unity/Assets/Ashfall';OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/HealthStations'
record=json.loads((OUT/'provenance.json').read_text());SOURCE=Path(record['sourceRoot']);source=json.loads((SOURCE/'assets/data/map_semantics_r14.json').read_text())['maps'];data=json.loads((OUT/'stations.json').read_text());checks=0
for p,digest in record['inputs'].items():assert hashlib.sha256((SOURCE/p).read_bytes()).hexdigest()==digest;checks+=1
assert len(data['maps'])==len(source)==105
lines=['using Ashfall.Core;using Ashfall.Integration.Playable.JumpPads;static class SourceRows{public static readonly R23SourceHealthStation[] All=new R23SourceHealthStation[]{']
def vector(v):return 'new Float3('+','.join(repr(float(x))+'f'for x in v)+')'
for row in data['maps']:
    original=source[row['mapId'].replace('archive:','')];assert row['sourceSha256']==original['source_sha256']and row['cacheFingerprint']==original['cache_fingerprint']
    assert len(row['stations'])==len(original['health']);checks+=2
    for i,(s,h)in enumerate(zip(row['stations'],original['health'])):
        center=list(h['center']);normal=list(h['normal']);center[2]*=-1;normal[2]*=-1
        assert s==dict(sourceIndex=i,center=center,normal=normal,radius=h['radius'],healthPerSecond=h['health_per_second']);checks+=1
        lines.append('new R23SourceHealthStation('+json.dumps(row['mapId']+'/health/'+str(i))+','+vector(s['center'])+','+vector(s['normal'])+','+repr(s['radius'])+','+str(s['healthPerSecond'])+'f),')
lines+=['};}'];(HERE/'SourceRows.cs').write_text('\n'.join(lines))
assert sum(len(m['stations'])for m in data['maps'])==430
print('PASS',checks,'exact original source payload/hash checks')
sources=[ROOT/line for line in (ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]
sources+=[BASE/'Integration/Playable/JumpPads/R23HealthStations.cs',HERE/'Mechanics.cs',HERE/'SourceRows.cs']
sources+=[BASE/'Integration/Playable/HandsWeapons/R23MagnumRuntime.cs',BASE/'Integration/Playable/HandsWeapons/R23MagnumOwner.cs']
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe');exe=HERE/'mechanics.exe'
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(exe),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p)for p in sources]
rsp=HERE/'mechanics.rsp';rsp.write_text('\n'.join('"'+a+'"'for a in args));r=subprocess.run([str(csc),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe)],capture_output=True,text=True);(HERE/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout,r.stderr);assert r.returncode==0
