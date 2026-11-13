from pathlib import Path
import hashlib,json,subprocess,uuid
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];BASE=ROOT/'unity/Assets/Ashfall';SOURCE=Path(r'C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source');ASSETS=BASE/'Integration/Playable/Activities'
inputs={r:hashlib.sha256((SOURCE/r).read_bytes()).hexdigest()for r in ['scripts/field_builder.gd','scripts/build_lightning_085.gd','scripts/build_grid.gd','scripts/building_input.gd']}
source=(SOURCE/'scripts/build_lightning_085.gd').read_text();assert all(s in source for s in ['STRAND_COUNT:=10','HALF_WIDTH:=.004','MAX_PERIOD_M:=.4572','Color("58ff44")','Color("050706")','get_ticks_msec())*.006','88217+strand*15737+knot*379+tick*577','88271+strand*1471'])
shader=(ASSETS/'Resources/R23BuildPreview.shader').read_text();assert all(s in shader for s in ['Blend [_SrcBlend] [_DstBlend]','ZWrite [_ZWrite]','ZTest LEqual','Cull Off','UNITY_VERTEX_OUTPUT_STEREO','UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO','UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX'])
render=(ASSETS/'R23ConstructionPreview.cs').read_text();assert 'AddComponent<Collider>'not in render and 'CreatePrimitive'not in render
activity=(ASSETS/'R23VRActivities.cs').read_text();assert 'F(Ashfall.Integration.Playable.HandsWeapons.R23TrackedGripPose.Forward(hand))'in activity and 'previewMaterial'not in activity
assert 'ghost.SetInt("_ZWrite",0)'in render and 'ghost.SetInt("_SrcBlend",5)'in render and 'ghost.SetInt("_DstBlend",10)'in render and 'electric.SetInt("_ZWrite",1)'in render
for p in [ASSETS/'R23BuildLightningRecipe.cs',ASSETS/'R23ConstructionPreview.cs',ASSETS/'Resources',ASSETS/'Resources/R23BuildPreview.shader']:
    m=Path(str(p)+'.meta')
    if not m.exists():m.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-source-build-preview:'+str(p.relative_to(ROOT)).replace('\\','/')).hex+'\n'+('folderAsset: yes\n'if p.is_dir()else''))
(HERE/'source-provenance.json').write_text(json.dumps(dict(sourceRoot=str(SOURCE),inputs=inputs,engineRun=False,deviceRun=False,shaderCompiledByEngine=False),indent=2))
print('PASS source constants, explicit depth/alpha/XR pass and collider-free preview checks')
csc='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe';exe=HERE/'mechanics.exe'
args=[csc,'/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(exe),str(BASE/'Core/Contracts.cs'),str(ASSETS/'R23BuildLightningRecipe.cs'),str(HERE/'Mechanics.cs')]
r=subprocess.run(args,capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(exe)],capture_output=True,text=True);(HERE/'results.txt').write_text(r.stdout+r.stderr);print(r.stdout,r.stderr);assert r.returncode==0
