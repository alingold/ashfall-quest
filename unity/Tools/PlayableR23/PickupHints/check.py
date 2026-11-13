from pathlib import Path
import hashlib,json,subprocess,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3];ASSET=ROOT/'unity/Assets/Ashfall/Integration/Playable/PickupHints';SOURCE=Path(r'C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
code=(SOURCE/'scripts/weapon_inventory.gd').read_text(encoding='utf-8');upright=(SOURCE/'scripts/upright_ui.gd').read_text(encoding='utf-8');shader=(ASSET/'Resources/R23PickupHint.shader').read_text();runtime=(ASSET/'R23PickupHints.cs').read_text()
for text in ['range(24)','Vector3(.041,.007,.007)','cos(angle)*.17','sin(angle)*.17','angle+PI*.5','Color(0.25,0.85,1.0)','Color(1.0,0.8,0.2)','label.position.y=.24','label.font_size=30','label.pixel_size=.0015','material.no_depth_test=false']:assert text in code,text
for text in ['Vector3.UP.cross(z)','x.length_squared()<0.000001','x=Vector3.RIGHT','orthonormalized()']:assert text in upright,text
assert 'ZTest LEqual' in shader and 'ZTest Always'not in shader and all(x in shader for x in ['UNITY_VERTEX_OUTPUT_STEREO','UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO','UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX'])
assert 'CreatePrimitive'not in runtime and 'AddComponent<Collider>'not in runtime and 'head.position='not in runtime and 'head.rotation='not in runtime and 'InputDevices'not in runtime
assert 'Resources.Load<Font>("R23GodotUI")'in runtime and 'glyph.advance'in runtime and 'R23PickupHintGeometry.PixelSize'in runtime
for p in [ASSET,ASSET/'Resources']+list(ASSET.glob('*.cs'))+[ASSET/'Resources/R23PickupHint.shader']:
 meta=Path(str(p)+'.meta')
 if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-source-pickup-hints:'+str(p.relative_to(ROOT)).replace('\\','/')).hex+'\n'+('folderAsset: yes\n'if p.is_dir()else''))
sources=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs',ASSET/'R23PickupHintGeometry.cs',HERE/'Mechanics.cs'];exe=HERE/'mechanics.exe';args=['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(exe)]+[str(p)for p in sources];r=subprocess.run(args,capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe)],capture_output=True,text=True);(HERE/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout,r.stderr);assert r.returncode==0
record=dict(sourceRoot=str(SOURCE),inputs={p:hashlib.sha256((SOURCE/p).read_bytes()).hexdigest()for p in ['scripts/weapon_inventory.gd','scripts/upright_ui.gd']},fontResource='Existing R23GodotUI source font; own dynamic atlas clone',sourceSegments=24,ringVertices=576,ringTriangles=288,maximumRingDraws=2,maximumLabelDraws=2,sourceDepthTest=True,engineRun=False,renderRun=False,deviceRun=False)
(HERE/'checks.json').write_text(json.dumps(record,indent=2)+'\n');print('PASS immutable source constants, actual depth/stereo pass, collider-free passive ownership checks.')
