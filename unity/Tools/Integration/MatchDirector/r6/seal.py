import pathlib,json,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/MatchTransactionsR6';D=R/'docs/agent-runs/20261104-three-chats/unity/MatchTransactionsR6'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
owned=list((R/'unity/Assets/Ashfall/Integration/MatchDirector').glob('*.cs'))+list((R/'unity/Assets/Ashfall/Integration/MatchRounds').glob('*.cs'))+[R/'unity/Assets/Ashfall/Modules/Sessions/PreparedMatchLifecycle.cs']
for p in owned:
 q=S/'candidate'/(p.parent.name+'-'+p.name);q.parent.mkdir(exist_ok=True)
 if q.exists():assert q.read_bytes()==p.read_bytes()
 else:q.write_bytes(p.read_bytes())
records=[]
for rel in ['quest-r22/source/scripts/net_session1000.gd','game/scripts/net_session1000.gd']:
 p=R/rel;records.append(dict(path=rel,sha256=sha(p),bytes=p.stat().st_size))
(D/'source-reference-hashes.json').write_text(json.dumps(records,indent=2))
base=R/'docs/agent-runs/20261104-three-chats/unity/MatchDirectorR4/r5/manifest.sha256.json'
verify=[]
for item in json.loads(base.read_text(encoding='utf-8-sig')):
 p=R/item['path'];verify.append(dict(path=item['path'],expected=item['sha256'],actual=sha(p),match=sha(p)==item['sha256'].upper(),ownedProduction=p in owned))
(D/'prior-artifact-verification.json').write_text(json.dumps(verify,indent=2))
assert all(x['match'] or x['ownedProduction'] for x in verify),'unowned old artifact changed'
print('old manifest entries',len(verify),'unchanged',sum(x['match'] for x in verify),'owned changes',sum(not x['match'] and x['ownedProduction'] for x in verify))
entries=[]
paths=owned+list((R/'unity/Tools/Integration/MatchDirector/r6').glob('*'))+list(S.rglob('*'))+list(D.rglob('*'))
manifest=D/'manifest.sha256.json'
for p in sorted(set(paths)):
 if p.is_file() and p!=manifest:entries.append(dict(path=str(p.relative_to(R)),sha256=sha(p),bytes=p.stat().st_size))
manifest.write_text(json.dumps(entries,indent=2))
print('candidate seal',sha(manifest),'entries',len(entries))
