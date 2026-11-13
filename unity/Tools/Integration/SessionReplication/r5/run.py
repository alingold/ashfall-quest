import pathlib,json,hashlib,subprocess,sys
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/SessionReplicationR5';D=R/'docs/agent-runs/20261104-three-chats/unity/SessionReplicationR5';CSC=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest().upper()
i=1
while (S/('attempt-%02d'%i)).exists():i+=1
O=S/('attempt-%02d'%i);O.mkdir();L=D/O.name;L.mkdir();commands=[];pins=[]
def copy(p,q):
 b=p.read_bytes();q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b);assert p.read_bytes()==b;pins.append(dict(path=str(p),snapshot=str(q),sha256=sha(q)));return q
def suite(name,files,args=[],main=None,library=False):
 exe=O/(name+'.exe');cmd='"%s" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%s" '%(CSC,exe)+('/main:'+main+' ' if main else '')+' '.join('"%s"'%p for p in files)
 def run(c,phase):
  r=subprocess.run(c,capture_output=True,text=True,timeout=120);(L/(name+'-'+phase+'.txt')).write_text(r.stdout+r.stderr);commands.append(dict(command=c,exit=r.returncode));print(name,phase,r.returncode,(r.stdout+r.stderr)[-1500:]);return r.returncode
 if library:cmd=cmd.replace('/target:exe','/target:library')
 if run(cmd,'compile')==0:
  if library:return 0
  return run('"%s" '%exe+' '.join(args),'run')
 return -1
before=S/'before'
deps=sum([sorted((before/x).glob('*.cs')) for x in ['Core','Modules/Networking','Modules/Sessions','Integration/Sessions']],[])
if '--current' in sys.argv:
 deps=[]
 for folder in ['Core','Modules/Networking','Modules/Sessions','Integration/Sessions']:
  for p in sorted((R/'unity/Assets/Ashfall'/folder).glob('*.cs')):
   if p.name=='PreparedLifeProjection.cs':continue # unrelated concurrent lifecycle owner, not called by this closure
   deps.append(copy(p,O/'dependencies'/folder/p.name))
if '--sealed' in sys.argv:
 deps=[copy(p,O/'dependencies'/p.relative_to(S/'attempt-02/dependencies')) for p in sorted((S/'attempt-02/dependencies').rglob('*.cs'))]
owned=[copy(p,O/'candidate'/p.name) for p in sorted((R/'unity/Assets/Ashfall/Integration/SessionReplication').glob('*.cs'))]
tests=[copy(p,O/'tests'/p.name) for p in sorted((R/'unity/Tools/Integration/SessionReplication/r5').glob('*.cs'))]
codec=copy(R/'campaign-staging/20261104-three-chats/unity/phase07/codec-check/Acceptance.cs',O/'codec/Acceptance.cs')
suite('RootRepairBaseline',deps+sorted((before/'Integration/SessionReplication').glob('*.cs'))+[codec])
suite('BoundedCodec',deps+owned+[codec])
suite('R5',deps+owned+tests)
engine=copy(R/'unity/Assets/Ashfall/Modules/Networking/Engine/UnityTransportAdapter.cs',O/'guarded/UnityTransportAdapter.cs')
suite('DefineOFF',deps+owned+[engine],library=True)
suite('Sequence1RequiredGate',deps+owned+tests,['restart'])
B=R/'campaign-staging/20261104-three-chats/unity/SessionReplication/r4/attempt-07'
legacy=sum([sorted((B/x).glob('*.cs')) for x in ['current-core','Modules-Networking','Modules-Sessions','Integration-Sessions','candidate']],[])
suite('FrozenR4',legacy+[B/'candidate-test/Tests.cs'])
suite('FrozenR4Independent',legacy+[R/'campaign-staging/20261104-three-chats/unity/reviews/SessionReplication-r4/Independent.cs'])
(L/'commands.json').write_text(json.dumps(commands,indent=2));(L/'inputs.json').write_text(json.dumps(pins,indent=2))
rows=json.loads((D/'before.json').read_text());(L/'dependency-drift.json').write_text(json.dumps([dict(x,current_sha256=sha(pathlib.Path(x['path']))) for x in rows if 'Integration\\SessionReplication' not in x['path']],indent=2))
