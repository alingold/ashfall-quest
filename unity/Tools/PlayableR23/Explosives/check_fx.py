from pathlib import Path
import importlib.util,hashlib,json,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
spec=importlib.util.spec_from_file_location('frozen_items',ROOT/'unity/Tools/PlayableR23/Consumables/R5/source.py');source=importlib.util.module_from_spec(spec);spec.loader.exec_module(source)
inputs=[source.verify(p)for p in ['scripts/explosive_effect.gd','scripts/explosive_projectile.gd']]
port=json.loads((ROOT/'unity/Assets/StreamingAssets/PlayableR23/Explosives/provenance.json').read_text())
for row in port['geometry']:
 relative=row['source'].replace('\\','/');p=source.read(relative);assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'];inputs.append(source.verify(relative))
for row in port['audio']:
 if row.get('sourceRecipe'):continue
 relative=row['source'].removeprefix('res://').replace('\\','/');p=source.read(relative);assert hashlib.sha256(p.read_bytes()).hexdigest()==row['sha256'];inputs.append(source.verify(relative))
files=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs',ROOT/'unity/Assets/Ashfall/Integration/Playable/Explosives/R24ExplosionMotion.cs',HERE/'ExplosionMotionCases.cs']
args=['/nologo','/langversion:5','/warnaserror+','/warn:4','/target:exe','/out:'+str(HERE/'ExplosionMotionCases.exe')]+[str(p)for p in files];rsp=HERE/'fx.rsp';rsp.write_text('\n'.join('"'+p+'"'for p in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'fx-compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'ExplosionMotionCases.exe')],capture_output=True,text=True);(HERE/'fx-results.txt').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
(HERE/'fx-source.json').write_text(json.dumps(dict(contentId=source.CONTENT_ID,inputs=inputs,cpu=r.stdout.strip(),engineRun=False,shaderRenderRun=False),indent=2)+'\n')
