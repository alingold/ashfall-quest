from source import *
import wave,re
A=ROOT/'unity/Assets/Ashfall/Integration/Playable/MatchZone';P=ROOT/'unity/Assets/StreamingAssets/PlayableR23/MatchZone';j=json.loads((P/'source.json').read_text());checks=0
def need(v,why):
 global checks
 assert v,why;checks+=1
for relative,digest in j['inputs'].items():need(sha(source(relative))==digest,relative)
for original,copy in [('assets/cyber_bird/zone_r5/danger_zone_loop.wav','danger_zone_loop.wav'),('assets/cyber_bird/zone_r5/LICENSE.txt','LICENSE.txt'),('assets/cyber_bird/zone_r5/AUDIO-PROVENANCE.json','AUDIO-PROVENANCE.json'),('assets/cyber_bird/zone_r6/surface.gdshader','source-surface.gdshader'),('assets/cyber_bird/zone_r5/danger.gdshader','source-danger.gdshader')]:need(sha(source(original))==sha(P/copy),'exact original payload '+copy)
with wave.open(str(P/'danger_zone_loop.wav'),'rb')as w:need(w.getnchannels()==1 and w.getframerate()==24000 and w.getnframes()==192000 and w.getsampwidth()==2 and hashlib.sha256(w.readframes(w.getnframes())).hexdigest()==j['audio']['pcmSha256'],'exact8s mono24000Hz PCM16 source loop')
state=(A/'R24MatchZoneState.cs').read_text();view=(A/'R24MatchZone.cs').read_text();surface=(A/'Resources/R24ZoneSurface.shader').read_text();danger=(A/'Resources/R24ZoneDanger.shader').read_text();gs=source('scripts/square_zone_r3.gd').read_text();net=source('scripts/net_session1000.gd').read_text()
need('const RATIO:=.65' in gs and 'const MIN_SIZE:=12.0' in gs and 'Ratio=.65,MinimumSize=12' in state,'source ratio/minimum bound')
need('for i in 32:' in gs and 'for(int i=0;i<32;i++)' in state,'bounded source progression')
need('hurt(id,float(rules.zone_damage)*dt,0,false,true,-1,"zone")' in net and 'BypassShield' in state and 'BypassGenericDamageMultiplier' in state,'actual source direct Health callback contract')
need('if _last_tick<.10:return' in net and 'accumulator<.10' in state,'source accumulated host cadence')
need('red>.60&&white>.1524' in surface and 'world+=UnityObjectToWorldNormal(a.normal)*.002' in surface,'source actual surface widths/two millimetre depth offset')
need('float3(1,.012,.055)' in surface and 'float3(.92,.98,1)' in surface and '3.8:3.0' in surface,'source albedo+emission red/white HDR intensities')
need('axis=v.world.x+v.world.z' in danger and 'v.world.y)*.055-_SourceTime*.035' in danger and 'v.world.y)*.055+_SourceTime*.035' in danger and '.11+hatch*.16' in danger and '1.55+hatch*.5' in danger,'exact source crosshatch/time/opacity/albedo+emission recipe')
need('ZWrite Off' in danger and 'Cull Off' in danger and 'ZTest LEqual' in danger and '#pragma multi_compile_fog' not in danger,'source transparent unshaded depth-tested fog-disabled danger pass')
need('Matrix4x4.Scale(new Vector3(1,1,-1))*map.worldToLocalMatrix' in view and 'v.world=mul(_WorldToGodotMap,world).xyz+_GodotOrigin' in danger,'once reflected map-local input and genuine Godot world hatch origin')
need('Graphics.DrawMesh(mesh,filter.transform.localToWorldMatrix' in view and 'MeshRenderer Renderer' in view and 'renderer.enabled' in view and 'renderer.gameObject.activeInHierarchy' in view,'actual active source surface geometry, not sampled/fake terrain')
need('collider' not in view.lower() and 'sharedMaterial=' not in view and 'actor.position=' not in view and 'map.position=' not in view,'no collision/original-material/tracked-root mutations')
need('Mathf.Lerp(-9,-38,Mathf.Clamp01(EdgeDistance/90))' in view and 'EdgeDistance<90?-15:-11' in view and 's.dopplerLevel=0' in view and 's.maxDistance=100' in view,'source spatial nearest-edge volume/distance/outside audio policy')
need('Destroy(edgeAudio.gameObject)' in view and 'Destroy(outsideAudio.gameObject)' in view and 'Destroy(loop)' in view and 'ClearRound' in view and 'SetMap' in view,'bounded owned-resource map/reset/teardown lifecycle')
need(sum(p.stat().st_size for p in P.iterdir()if p.is_file())<500000,'bounded source payload')
for p in A.rglob('*.meta'):need(bool(re.search(r'^guid: [0-9a-f]{32}\s*$',p.read_text(),re.M)),'valid Unity metadata '+str(p.name))
result=dict(checks=checks,sourceManifestSha256=sha(FROZEN/'SOURCE-MANIFEST.json'),runtimeHashes={p.relative_to(ROOT).as_posix():sha(p)for p in sorted(A.rglob('*'))if p.is_file()},payloadHashes={p.name:sha(p)for p in sorted(P.iterdir())if p.is_file()},nativeRun=False)
(HERE/'source-checks.json').write_text(json.dumps(result,indent=2)+'\n');print(checks,'frozen-source/PCM/renderrecipe/coordinate/resource-lifecycle checks PASS')
