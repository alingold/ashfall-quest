from pathlib import Path
import json,hashlib,subprocess,struct,re,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];A=ROOT/'unity/Assets/Ashfall';STAGE=HERE/'Staged'
if '--live'in sys.argv:STAGE=A/'Integration/Playable'
FROZEN=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');SRC=FROZEN/'source'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(FROZEN/'SOURCE-MANIFEST.json')=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111'
pins={r['path']:r['sha256']for r in json.loads((FROZEN/'SOURCE-MANIFEST.json').read_text())['files']}
used={}
def source(p):
 assert sha(SRC/p)==pins[p],p;used[p]=pins[p];return(SRC/p).read_text(encoding='utf-8')
bridge=source('scripts/net_bridge1000.gd');ghost=source('scripts/ghost_appearance_r13.gd');shader=source('assets/ghost_r13.gdshader');player=source('scripts/player.gd');dummy=source('scripts/practice_dummy.gd')
assert 'actor.collision_layer=0;actor.collision_mask=_local_mask_r13&~4' in bridge
assert 'p.life in ["alive","downed","eliminated"]'in bridge
assert 'actor.reset_motion();actor.require_neutral_input()'in bridge
assert 'collision_layer = 2'in player or 'collision_layer=2'in player
assert re.search(r'collision_layer\s*=\s*4',dummy)
for term in ['depth_draw_never','depth_test_disabled','cull_back','fog_disabled','0.2126, 0.7152, 0.0722','0.42']:
 assert term in shader,term
runtime=(STAGE/'Match/Resources/R24SourceGhost.shader').read_text()
for term in ['ZWrite Off ZTest Always Cull Back','float3(.2126,.7152,.0722)','_GhostOpacity','UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX','SRGBToLinear','LinearToSRGB']:
 assert term in runtime,term
assert '_ghost_material(source)'in ghost and 'record.shadow'in ghost
rig=(STAGE/'R23VRRig.Ghost.cs').read_text();head=(STAGE/'Climbing/R23HeadCollision.cs').read_text()
assert 'Body.enabled=false'not in rig and 'transform.position='not in rig and 'IgnoreLayerCollision'not in rig
assert 'Body.excludeLayers=~0' in rig and 'Body.includeLayers=0' in rig and 'Body.gameObject.layer=2' in rig
assert 'UseSourceHeadHull=true'in rig and 'HeadCollision.Ready'in rig and 'Climbing.ResetMotion()'in rig and 'DisarmMotion()'in rig
assert 'sourceGhostActorFilter==null||!sourceGhostActorFilter(c)'in head
assert 'target.Owns(candidate)'in(STAGE/'HandsWeapons/R23HandsWeapons.Ghost.cs').read_text()
assert 'constructionFrame.Paused=true'in(STAGE/'R23VRRig.cs').read_text()
# Verify actual selected-body binary art remains original, with no COLOR stream:
# the existing reader's stride is position3/normal3/UV2, indexed per surface.
appearance=A/'Integration/Playable/Appearance/R23PlayerAppearance.cs'
assert 'mesh.colors='not in appearance.read_text()
assert 'mesh.colors='not in(A/'Integration/Playable/HandsWeapons/R23Visuals.cs').read_text()
payload=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Appearance'
models=[payload/'camo.meshbin',payload/'auditor.meshbin'];surfaces=vertices=triangles=0
for p in models:
 with p.open('rb')as f:
  def n():return struct.unpack('<i',f.read(4))[0]
  assert n()==0x52323341,p;count=n()
  for i in range(count):
   length=n();label=f.read(length);nv,ni,mat=n(),n(),n();assert nv>=3 and ni%3==0 and mat>=0
   f.seek(nv*32,1);indices=struct.unpack('<'+str(ni)+'i',f.read(ni*4));assert min(indices)>=0 and max(indices)<nv
   surfaces+=1;vertices+=nv;triangles+=ni//3
  assert f.read()==b'',p
assert models,'actual body binary assets absent'
files=sorted(set([ROOT/x for x in(ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()if x]+list((A/'Integration/Playable/HandsWeapons').glob('*.cs'))+[STAGE/'Match/R24GhostLifePolicy.cs',A/'Integration/Playable/Appearance/R23AppearanceColorDomain.cs',HERE/'GhostCases.cs']))
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(HERE/'GhostCases.exe'),'/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']+[str(p)for p in files]
rsp=HERE/'ghost.rsp';rsp.write_text('\n'.join('"'+x+'"'for x in args))
p=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'compile.log').write_text(p.stdout+p.stderr);print(p.stdout+p.stderr);assert p.returncode==0
p=subprocess.run([str(HERE/'GhostCases.exe')],capture_output=True,text=True);(HERE/'cases.log').write_text(p.stdout+p.stderr);print(p.stdout+p.stderr);assert p.returncode==0
result=dict(sourceFiles=used,appearanceModels=len(models),surfaces=surfaces,vertices=vertices,triangles=triangles,geometryMutated=False,unityNativeRun=False)
(HERE/'source-check.json').write_text(json.dumps(result,indent=2)+'\n')
print('Pinned source and actual selected-body binaries:',len(models),'models,',surfaces,'surfaces,',triangles,'triangles. No geometry/material source writes.')
