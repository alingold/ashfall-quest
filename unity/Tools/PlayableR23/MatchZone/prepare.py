from source import *
import shutil,wave
OUT=ROOT/'unity/Assets/StreamingAssets/PlayableR23/MatchZone';OUT.mkdir(parents=True,exist_ok=True)
paths=['scripts/square_zone_r3.gd','scripts/zone_surface_r6.gd','scripts/zone_outline_r3.gd','scripts/zone_danger_r5.gd','scripts/net_session1000.gd','scripts/net_rules1000.gd','scripts/map_catalog1000.gd','assets/cyber_bird/zone_r6/surface.gdshader','assets/cyber_bird/zone_r5/danger.gdshader','assets/cyber_bird/zone_r5/danger_zone_loop.wav','assets/cyber_bird/zone_r5/LICENSE.txt','assets/cyber_bird/zone_r5/AUDIO-PROVENANCE.json']
for p in paths:source(p)
for relative,name in [('assets/cyber_bird/zone_r5/danger_zone_loop.wav','danger_zone_loop.wav'),('assets/cyber_bird/zone_r5/LICENSE.txt','LICENSE.txt'),('assets/cyber_bird/zone_r5/AUDIO-PROVENANCE.json','AUDIO-PROVENANCE.json'),('assets/cyber_bird/zone_r6/surface.gdshader','source-surface.gdshader'),('assets/cyber_bird/zone_r5/danger.gdshader','source-danger.gdshader')]:shutil.copyfile(source(relative),OUT/name)
with wave.open(str(OUT/'danger_zone_loop.wav'),'rb')as wav:audio=dict(channels=wav.getnchannels(),rate=wav.getframerate(),frames=wav.getnframes(),bits=wav.getsampwidth()*8,pcmSha256=hashlib.sha256(wav.readframes(wav.getnframes())).hexdigest())
report=dict(sourceManifestSha256=sha(FROZEN/'SOURCE-MANIFEST.json'),inputs=USED,audio=audio,coordinates='State is Godot map-local X/Z; Unity map-root inverse followed by Z reflection exactly once. Original Godot catalog origin affects crosshatch phase, not local square containment.',rules=dict(ratio=.65,minSize=12,initialMinimumBase=52,edgeClearance=1,damageCadence=.1,directHealth=True,ignoreGenericDamageMultiplier=True,currentSurfaceHalfWidth=.60,nextSurfaceTotalWidth=.3048),limits=['Source RNG uniform samples are host-supplied; Godot PCG bit-sequence is not recreated.','Graphics overlay draws actual visible MeshFilter meshes; non-mesh particle/skinned renderers are not collected. Map/construction physics and materials remain caller-owned.','Unity shader import/rendering, HDR/tonemapping, spatial-audio rolloff/panning and Quest behavior remain native-unverified.'])
(OUT/'source.json').write_text(json.dumps(report,indent=2)+'\n')
for folder in [ROOT/'unity/Assets/Ashfall/Integration/Playable/MatchZone',OUT]:
 for p in [folder]+sorted(folder.rglob('*')):
  if p.suffix=='.meta':continue
  relative=p.relative_to(ROOT).as_posix();meta=p.with_name(p.name+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('ashfall-r6-matchzone/'+relative).encode()).hexdigest()+'\n'+('folderAsset: yes\n'if p.is_dir()else''))
print('Source zone payload',sum(p.stat().st_size for p in OUT.iterdir()if p.is_file()),'bytes;',audio)
