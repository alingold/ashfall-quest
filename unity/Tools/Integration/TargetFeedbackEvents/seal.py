from pathlib import Path
import hashlib,json
root=Path(r'C:/Users/aling/Documents/ashfall');docs=root/'docs/agent-runs/20261104-three-chats/unity/TargetFeedbackEventsR10';stage=root/'campaign-staging/20261104-three-chats/unity/TargetFeedbackEventsR10'
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
rows=json.loads((docs/'input-manifest.json').read_text());checks=[];drift=[]
for row in rows:
 snap=Path(row['snapshot']);assert sha(snap)==row['sha256'];p=Path(row['original']);actual=sha(p) if p.exists() else None
 checks.append(dict(snapshot=str(snap),sha256=sha(snap),matched=True))
 if actual!=row['sha256']:drift.append(dict(original=str(p),frozen=row['sha256'],current=actual,owned=p.name=='NativeTargetFeedbackView.cs'))
(docs/'frozen-verification.json').write_text(json.dumps(checks,indent=2));(docs/'moving-drift.json').write_text(json.dumps(drift,indent=2))
# Final candidate/tool bytes separate from immutable before files.
outputs=[root/'unity/Assets/Ashfall/Integration/PersonalSettings/Engine/NativeTargetFeedbackView.cs',root/'unity/Assets/Ashfall/Integration/PersonalSettings/Engine/NativeTargetFeedbackPool.cs']+list((root/'unity/Tools/Integration/TargetFeedbackEvents').glob('*'))
manifest=[]
for p in outputs:
 dst=stage/'final-source'/p.name;dst.parent.mkdir(exist_ok=True);dst.write_bytes(p.read_bytes());manifest.append(dict(path=str(p),snapshot=str(dst),sha256=sha(p)))
for p in stage.rglob('*'):
 if p.is_file():manifest.append(dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size))
(docs/'output-manifest.json').write_text(json.dumps(manifest,indent=2))
symbols={}
for p in outputs:
 if p.suffix=='.cs':symbols[str(p)]=[dict(line=i,text=s.strip()) for i,s in enumerate(p.read_text().splitlines(),1) if any(t in s for t in ['public ','void Draw','bool ResourcesLive','ResetIfNeeded','void Store'])]
(docs/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
print('Preserved',len(checks),'frozen inputs; moving drift',len(drift),'; output files',len(manifest))
