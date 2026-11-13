from pathlib import Path
import hashlib,json,re
R=Path(r'C:\Users\aling\Documents\ashfall')
S=R/'campaign-staging/20261104-three-chats/unity/AvatarNativeAcceptanceR7'
D=R/'docs/agent-runs/20261104-three-chats/unity/AvatarNativeAcceptanceR7'
T=R/'unity/Tools/Integration/AvatarNativeAcceptance'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(name,value):(D/name).write_text(json.dumps(value,indent=2))
inputs=json.loads((S/'pinned-v2.json').read_text())+json.loads((S/'bcl-pinned.json').read_text())
dump('final-dependency-drift.json',[dict(x,current=sha(Path(x['path'])),match=sha(Path(x['path']))==x['sha256']) for x in inputs])
symbols=[]
for p in [Path(x['frozen']) for x in inputs if x['path'].endswith('.cs')]+list(T.glob('*.cs')):
    for i,line in enumerate(p.read_text().splitlines(),1):
        if re.search(r'public .*\(|public .*event ',line):symbols.append(dict(path=str(p),sha256=sha(p),line=i,symbol=line.strip()))
dump('source-symbols.json',symbols)
results=json.loads((D/'run08/results.json').read_text())
assert all(x.get('compile',0)==0 for x in results)
assert all(x['run']!=0 if x['name'].startswith('mutant-') else x['run']==0 for x in results if 'run' in x)
tools=json.loads((D/'run08/tool-inputs.json').read_text())
assert all(sha(Path(x['path']))==x['sha256'] for x in tools),'tool changed since final qualification'
dump('summary.json',dict(status='UNREVIEWED',qualification='run08',newCpuObservations=197,retainedLifecycleAssertions=24,compiledKilledMutants=4,native='COMPILED SEPARATE DLLS; UNRUN',historical616and676='preserved; not rerun',sourceDrift=[x['path'] for x in inputs if sha(Path(x['path']))!=x['sha256']],commands=results))
paths=[p for folder in [S,D,T] for p in folder.rglob('*') if p.is_file() and p.name not in ['artifact-sha256.json','artifact-sha256.json.sha256']]
dump('artifact-sha256.json',[dict(path=str(p.relative_to(R)),bytes=p.stat().st_size,sha256=sha(p)) for p in sorted(paths)])
(D/'artifact-sha256.json.sha256').write_text(sha(D/'artifact-sha256.json')+'\n')
print('Sealed '+str(len(paths))+' files; manifest '+sha(D/'artifact-sha256.json'))
