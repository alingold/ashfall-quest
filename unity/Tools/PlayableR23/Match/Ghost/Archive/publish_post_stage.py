from pathlib import Path
import hashlib,json
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5];sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
old=json.loads((HERE/'Payload/maps.json').read_text());new=json.loads((HERE/'Payload/maps-post-stage.json').read_text());proof=json.loads((HERE/'post-stage-checks.json').read_text());pub=json.loads((HERE/'published.json').read_text())
assert proof['appendedPanels']==162 and proof['exactR21Attachments']==90 and proof['additionalWarmupHints']==19
assert sha(HERE/'Payload/maps-post-stage.json')==proof['payloadSha256']
added=0
for a,b in zip(old['rows'],new['rows']):
 assert a['id']==b['id'];count=len(a['panels']);assert b['panels'][:count]==a['panels'];assert b['warmup'][:len(a['warmup'])]==a['warmup']
 for key in a:
  if key not in ['panels','warmup']:assert a[key]==b[key],(a['id'],key)
 delta=len(b['panels'])-count;assert delta in [0,18];added+=delta
assert added==162
for p,h in proof['sourceInputs'].items():assert new['sourceInputs'][p]==h
livePayload=ROOT/'unity/Assets/StreamingAssets/PlayableR23/TeamLaunch/ArchiveRecovery/maps.json';liveReader=ROOT/'unity/Assets/Ashfall/Integration/Playable/Match/R24ArchiveRecoveryCatalog.cs';stageReader=HERE/'Staged/Match/R24ArchiveRecoveryCatalog.cs'
assert sha(livePayload)==pub['payloadSha256'],'Archive payload changed concurrently'
entry=next(r for r in pub['sourceFiles']if r['path']=='Match/R24ArchiveRecoveryCatalog.cs');assert sha(liveReader)==entry['stagedSha256'],'Archive reader changed concurrently'
text=liveReader.read_text();assert text.count(pub['payloadSha256'])==1;text=text.replace(pub['payloadSha256'],proof['payloadSha256']);stageReader.write_text(text)
before=dict(payload=sha(livePayload),reader=sha(liveReader))
livePayload.write_bytes((HERE/'Payload/maps-post-stage.json').read_bytes());liveReader.write_text(text)
(HERE/'post-stage-publication.json').write_text(json.dumps(dict(before=before,after=dict(payload=sha(livePayload),reader=sha(liveReader)),changedFiles=[str(livePayload.relative_to(ROOT)),str(liveReader.relative_to(ROOT))],appendedPanels=162,attachments=90,warmupHints=19,nativeExecuted=False),indent=2)+'\n')
print('Published guarded exact post-R18/R21 archive headroom +R18 warmup completion; only metadata and reader hash changed.')
