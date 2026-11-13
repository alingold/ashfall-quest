from pathlib import Path
import hashlib,json,wave
R=Path(__file__).resolve().parents[4];U=R/'unity';O=Path('D:/ashfall-r6-staging/impact-audio-next');O.mkdir(parents=True,exist_ok=True)
B=Path('D:/ashfall-r6-staging/hand-articulation-next/freeze-convergence');before=json.loads((B/'inputs.json').read_text());sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def frozen_text(relative):
 base=B
 while True:
  p=base/'source'/relative
  if p.is_file():return p.read_text(encoding='utf-8-sig')
  receipt=json.loads((base/'inputs.json').read_text());base=Path(receipt['sourceBase'])
def same_lines(a,b):return a.splitlines()==b.splitlines()
prefix='Assets/Ashfall/Integration/Playable/HandsWeapons/'
normal='if(impactMarks!=null)impactMarks.AddHit(hit.point,hit.normal,hit.collider);'
pellet='if(impactMarks!=null)impactMarks.AddHit(hit.point,hit.normal,hit.collider,true);'
blocked='if(impactMarks!=null)impactMarks.AddHit(blocked.point,blocked.normal,blocked.collider,true);'
preserved=[]
for name in ['R23HandsWeapons.cs','R23HandsWeapons.Awp.cs','R23HandsWeapons.M60.cs','R23HandsWeapons.Dt11.cs','R23HandsWeapons.Dual.cs']:
 rel=prefix+name;actual=(U/rel).read_text(encoding='utf-8-sig');restored=actual
 if name=='R23HandsWeapons.cs':
  added='        public event Action<Vector3,Collider> SurfaceImpact;\n        void PresentSurfaceImpact(RaycastHit hit,bool pellet=false,bool muzzleBlocked=false){if(impactMarks!=null)impactMarks.AddHit(hit.point,hit.normal,hit.collider,pellet);if(!muzzleBlocked&&SurfaceImpact!=null)SurfaceImpact(hit.point,hit.collider);}\n'
  assert restored.count(added)==1;restored=restored.replace(added,'')
 restored=restored.replace('PresentSurfaceImpact(hit,false,blocked);',normal).replace('PresentSurfaceImpact(blocked,true,true);',blocked).replace('PresentSurfaceImpact(hit,true);',pellet)
 restored=restored.replace('host.PresentSurfaceImpact(hit);','if(host.impactMarks!=null)host.impactMarks.AddHit(hit.point,hit.normal,hit.collider);')
 assert same_lines(restored,frozen_text(rel)),name+' changed ballistic/damage/decal/weapon code beyond the intended sound hook'
 preserved.append(name)
root='Assets/Ashfall/Integration/Playable/R23PlayableLoop.cs';text=(U/root).read_text();restored=text.replace('                Gameplay.SurfaceImpact+=TraversalAudio.PlayImpact;\n','').replace('if(TraversalAudio!=null)Gameplay.SurfaceImpact-=TraversalAudio.PlayImpact;','');assert same_lines(restored,frozen_text(root))
wav_prefix='Assets/StreamingAssets/PlayableR23/TraversalAudio/'
original_wavs=[row for row in before['content'] if row['path'].startswith(wav_prefix) and row['path'].endswith('.wav')]
assert len(original_wavs)==20
for row in original_wavs:assert sha(U/row['path'])==row['sha256']
asset=U/wav_prefix;provenance=json.loads((asset/'provenance.json').read_text());source=Path(provenance['sourceRoot']);pins={row['path']:row['sha256'] for row in json.loads((source.parent/'SOURCE-MANIFEST.json').read_text())['files']};routing=json.loads((source/'assets/audio/reference0992/manifest.json').read_text())['generic']
new=[]
for kind in ['impact_metal','impact_concrete']:
 assert not routing.get(kind), 'Update source route rather than silently use fallback'
 for i in range(3):
  file=kind+'_%02d.wav'%i;rel='assets/audio/sfx/'+file;p=asset/file;assert sha(p)==pins[rel]==sha(source/rel)
  with wave.open(str(p)) as w,wave.open(str(source/rel)) as original:
   assert w.getparams()==original.getparams() and w.getnchannels()==1 and w.getsampwidth()==2
   new.append(dict(file=file,sha256=sha(p),frames=w.getnframes(),rate=w.getframerate(),pcmSha256=hashlib.sha256(w.readframes(w.getnframes())).hexdigest()))
record=dict(ballisticPathsPreserved=preserved,rootOnlySubscriptionChanged=True,original20WavsUnchanged=True,sourceImpactWavs=new,nativeExecuted=False,headsetAudibilityVerified=False)
(O/'CPU-RESULT.json').write_text(json.dumps(record,indent=2)+'\n');print('PASS five actual ballistic paths retain original damage/recoil/decals; Root subscription only; original20 WAVs unchanged; six impact file/PCM/source pins PASS.')
