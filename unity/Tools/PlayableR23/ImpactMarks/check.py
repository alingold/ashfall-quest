from pathlib import Path
import subprocess,hashlib,json
ROOT=Path(__file__).resolve().parents[4];T=Path(__file__).resolve().parent
P=ROOT/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons';SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
files=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs',P/'R23ImpactMarksKernel.cs',T/'Cases.cs'];csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(T/'ImpactCases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p) for p in files]
rsp=T/'cases.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args));r=subprocess.run([str(csc),'/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(T/'ImpactCases.exe')],capture_output=True,text=True);(T/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
shader=(P/'Resources/R23ImpactMarks.shader').read_text();assert 'ZWrite Off' in shader and 'Cull Off' in shader and 'Blend SrcAlpha OneMinusSrcAlpha' in shader and 'ZTest LEqual' in shader and 'UNITY_VERTEX_OUTPUT_STEREO' in shader
records={name:hashlib.sha256((SRC/name).read_bytes()).hexdigest() for name in ['scripts/impact_marks.gd','scripts/main.gd','scripts/world.gd']};(T/'source-inputs.json').write_text(json.dumps({'baseline':str(SRC),'inputs':records},indent=2));print('PASS source depth-tested transparent unlit/stereo shader contract (shader GPU compilation unclaimed)')
