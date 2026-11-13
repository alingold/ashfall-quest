from pathlib import Path
import json,hashlib,re,shutil
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopBindingsR4';E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopBindingsR4'
def h(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
rows=json.loads((E/'pin.json').read_text());consumed={str(Path(x['path'])).lower() for x in json.loads((E/'final06/sources.json').read_text())};drift=[];verification=[]
for x in rows:
 snap=S/'pinned'/x['original'];assert h(snap)==x['sha256'].upper();original=R/x['original'];actual=h(original) if original.exists() else None
 if actual!=x['sha256'].upper():drift.append({'original':x['original'],'pinned':x['sha256'],'current':actual,'consumed':str(snap).lower() in consumed})
 verification.append({'path':str(snap),'sha256':h(snap)})
(E/'dependency-drift.json').write_text(json.dumps({'scope':'Single final hash comparison; no moving-source adoption','drift':drift},indent=2))
for p in (S/'before').rglob('*'):
 if p.is_file():verification.append({'before':str(p),'sha256':h(p)})
refs=[]
for name in ['UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll']:
 p=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Managed/UnityEngine')/name;refs.append({'path':str(p),'sha256':h(p)})
p=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll');refs.append({'path':str(p),'sha256':h(p)})
(E/'reference-identities.json').write_text(json.dumps(refs,indent=2));(E/'snapshot-verification.json').write_text(json.dumps(verification,indent=2))
symbols=[]
for p in list((R/'unity/Assets/Ashfall/Integration/RuntimeLoop').rglob('*.cs'))+[S/'pinned/unity/Assets/Ashfall/Modules/Player/PlayerSettingsPublication.cs',S/'pinned/unity/Assets/Ashfall/Integration/PersonalSettings/TypedPlayerSettingsPort.cs']:
 symbols.append({'path':str(p),'sha256':h(p),'symbols':[{'line':i+1,'text':line.strip()} for i,line in enumerate(p.read_text().splitlines()) if re.search(r'\b(public|internal)\b.*\(',line)]})
(E/'source-symbols.json').write_text(json.dumps(symbols,indent=2))
manifest=[]
for base in [R/'unity/Assets/Ashfall/Integration/RuntimeLoop',R/'unity/Tools/Integration/RuntimeLoop',S,E]:
 for p in base.rglob('*'):
  if p.is_file() and p.name not in ['sha256-manifest.json','seal.json']:
   manifest.append({'path':str(p.relative_to(R)),'sha256':h(p),'bytes':p.stat().st_size})
(E/'sha256-manifest.json').write_text(json.dumps(manifest,indent=2));(E/'seal.json').write_text(json.dumps({'manifestSha256':h(E/'sha256-manifest.json'),'fileCount':len(manifest),'status':'partial unpromoted candidate','M1':'OPEN','dependencyDriftCount':len(drift),'consumedDriftCount':sum(x['consumed'] for x in drift)},indent=2))
print((E/'seal.json').read_text())
