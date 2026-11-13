from pathlib import Path
import hashlib,json
ROOT=Path(__file__).resolve().parents[5]
D=ROOT/'docs/agent-runs/20261104-three-chats/unity/Sessions/r3'
S=ROOT/'campaign-staging/20261104-three-chats/unity/Sessions/r3'
M=D/'candidate-manifest.json'
if M.exists():raise RuntimeError('Candidate already frozen')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
historical=ROOT/'docs/agent-runs/20261104-three-chats/unity/Sessions/evidence/r2/candidate-manifest-final.json'
assert sha(historical)=='0555FB5814D109C6B75EBB1DB624BC982F4624166175F9E50B940B78BF2B5D97'
assert sha(S/'before/Session.cs')=='F241888E224CB1E764A78D3C823C86BD2C988DF7CB0A271C953DB3BECFD35EC2'
assert sha(S/'before/Ports.cs')=='EDEF027BA26F7819914F749AC5D3CAF22B786C825623D0D6A7988F1FC7A76C81'
production=list((ROOT/'unity/Assets/Ashfall/Modules/Sessions').glob('*.cs'))+list((ROOT/'unity/Assets/Ashfall/Integration/Sessions').glob('*.cs'))
for p in production:
 snapshot=S/'attempt-02'/('Integration' if 'Integration' in p.parts else 'Sessions')/p.name
 assert sha(p)==sha(snapshot),str(p)
roots=[D,S,ROOT/'unity/Tools/Modules/Sessions/r3',ROOT/'unity/Tools/Integration/Sessions']
paths=set(production)
for directory in roots:paths.update(p for p in directory.rglob('*') if p.is_file())
entries=[dict(path=p.relative_to(ROOT).as_posix(),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(paths)]
M.write_text(json.dumps(dict(candidate='Sessions/3-phase03',status='implementation-candidate-awaiting-independent-review',historical_manifest_sha256=sha(historical),files=entries),indent=2))
(D/'manifest.sha256.txt').write_text(sha(M)+'  candidate-manifest.json\n')
print('FROZEN',len(entries),sha(M))
