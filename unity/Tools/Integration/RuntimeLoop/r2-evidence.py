from pathlib import Path
import json,hashlib
R=Path.cwd();E=R/'docs/agent-runs/20261104-three-chats/unity/RuntimeLoopR2';S=R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR2';F=E/'final04'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
rows=json.loads((F/'pin.json').read_text());drift=[]
for row in rows:
 p=R/row['path'];h=sha(p) if p.exists() else None
 if h!=row['sha256']:drift.append(dict(row,currentSha256=h))
(F/'end-drift.json').write_text(json.dumps(drift,indent=2))
comparison=[]
for row in json.loads((R/'campaign-staging/20261104-three-chats/unity/RuntimeLoopR1/r5/dependencies.json').read_text()):
 p=R/row['path'];comparison.append(dict(path=row['path'],historicalSha256=row['sha256'],currentSha256=sha(p),changed=sha(p)!=row['sha256']))
(E/'final04/historical-dependency-comparison.json').write_text(json.dumps(comparison,indent=2))
references=[]
for row in json.loads((R/'docs/agent-runs/20261104-three-chats/unity/reviews/RuntimeLoop-r1/source-reference-checks.json').read_text()):
 p=R/row['path'];text=p.read_text(encoding='utf-8-sig').splitlines();symbols=[]
 for v in row['symbols']:symbols.append(dict(symbol=v['symbol'],lines='\n'.join(text[v['startLine']-1:v['endLine']])))
 references.append(dict(path=row['path'],sha256=sha(p),priorSha256=row['actual'],matchesPrior=sha(p)==row['actual'],symbols=symbols))
(E/'final04/source-reference-checks.json').write_text(json.dumps(references,indent=2))
# Copies only small actually consumed producer/fixture source bytes, no refreshed dependency claims.
for p in (R/'unity/Tools/Integration/RuntimeLoop').glob('*'):
 if p.is_file():d=S/'final04'/'tools'/p.name;d.parent.mkdir(exist_ok=True);d.write_bytes(p.read_bytes())
result=dict(finalSources=len(rows),sourceBytes=sum(x['bytes'] for x in rows),endDrift=len(drift),historicalDependencyChanges=sum(x['changed'] for x in comparison),repeatByteEqual=(F/'scenario.log').read_bytes()==(F/'repeat.log').read_bytes(),historicalRepeatByteEqual=(E/'historical/frozen137-scenario.log').read_bytes()==(E/'historical/frozen137-repeat.log').read_bytes(),scenario140=0,repeat140=0,strict=2,gates64=0,eightUnknownGroups=True,freshIndependentReview='REQUIRED; not delegated by leaf')
(E/'final04/results.json').write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2));print('dependency directories:',sorted(set(str(Path(x['path']).parent) for x in rows)))
