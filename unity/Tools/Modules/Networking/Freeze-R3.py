from pathlib import Path
import hashlib,json,shutil
ROOT=Path(__file__).resolve().parents[4]
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/Networking/r3'
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/Networking/r3'
PROD=ROOT/'unity/Assets/Ashfall/Modules/Networking'
TOOLS=Path(__file__).parent
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def rec(p): return dict(path=str(p.relative_to(ROOT)).replace('\\','/'),sha256=sha(p),bytes=p.stat().st_size)
def main():
 shutil.copy2(Path(__file__),OUT/'candidate/tools'/Path(__file__).name)
 old=ROOT/'campaign-staging/20261104-three-chats/unity/Networking/r2/source'
 checks=[]
 for part in ['production','tools']:
  for p in (old/part).rglob('*'):
   if p.is_file():
    before=OUT/'before'/part/p.relative_to(old/part)
    checks.append(dict(path=str(p.relative_to(old)),r2Sha256=sha(p),preservedSha256=sha(before),matches=sha(p)==sha(before)))
 assert all(x['matches'] for x in checks), 'pre-edit bytes drifted from reviewed r2'
 (DOC/'r2-baseline-verification.json').write_text(json.dumps(checks,indent=2))
 changed=[]
 for src,part in [(PROD,'production'),(TOOLS,'tools')]:
  for p in sorted(src.rglob('*')):
   if p.is_file():
    prior=OUT/'before'/part/p.relative_to(src)
    if not prior.exists() or sha(p)!=sha(prior): changed.append(dict(rec(p),beforeSha256=sha(prior) if prior.exists() else None))
 (DOC/'changed-files.json').write_text(json.dumps(changed,indent=2))
 inputs=json.loads((DOC/'inputs.json').read_text())
 assert all(sha(ROOT/x['path'])==x['sha256'] for x in inputs),'consumed input drift'
 for p in PROD.rglob('*.cs'): assert sha(p)==sha(OUT/'candidate/production'/p.relative_to(PROD))
 artifacts=[]
 for root in [PROD,TOOLS,OUT,DOC]:
  for p in sorted(root.rglob('*')):
   if p.is_file() and p.name not in ['candidate-manifest.json','manifest.sha256']:
    artifacts.append(rec(p))
 manifest=dict(status='UNREVIEWED_CANDIDATE',questRevision='bdba6e30012e1733efd5021fd8ded593f82f26e6',inputs=inputs,changedFiles=changed,artifacts=artifacts)
 target=DOC/'candidate-manifest.json'; target.write_text(json.dumps(manifest,indent=2))
 rows=[sha(target)+'  '+str(target.relative_to(ROOT)).replace('\\','/')]+[x['sha256']+'  '+x['path'] for x in artifacts]
 (DOC/'manifest.sha256').write_text('\n'.join(rows)+'\n')
 print('CANDIDATE MANIFEST SHA256 '+sha(target)); print('Artifacts',len(artifacts),'changed source/tool files',len(changed),'r2 checks',len(checks))
if __name__=='__main__': main()
