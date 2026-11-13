import hashlib,json,pathlib,subprocess,sys
ROOT=pathlib.Path(r'C:\Users\aling\Documents\ashfall')
STAGE=ROOT/r'campaign-staging\20261104-three-chats\unity\SessionReplication\r4'
DOC=ROOT/r'docs\agent-runs\20261104-three-chats\unity\SessionReplication\r4'
CSC=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def save(p,obj):p.write_text(json.dumps(obj,indent=2)+'\n',encoding='utf-8')
def snapshot(src,dst):
 dst.parent.mkdir(parents=True,exist_ok=True);b=src.read_bytes();dst.write_bytes(b)
 assert src.read_bytes()==b and sha(dst)==sha(src),'snapshot race'
 return {'path':str(src),'snapshot':str(dst),'sha256':sha(dst)}
# Every invocation has new evidence, never regenerates prior runs.
i=1
while (STAGE/('attempt-%02d'%i)).exists():i+=1
OUT=STAGE/('attempt-%02d'%i);OUT.mkdir();LOG=DOC/OUT.name;LOG.mkdir()
commands=[];pins=[]
def copyset(files,prefix):
 result=[]
 for src in files:
  dst=OUT/prefix/src.name;pins.append(snapshot(src,dst));result.append(dst)
 return result
def suite(name,files):
 exe=OUT/(name+'.exe');args=[str(CSC),'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+[str(p) for p in files]
 command='"%s" /nologo /langversion:5 /warnaserror+ /target:exe /out:"%s" %s'%(CSC,exe,' '.join('"%s"'%p for p in files))
 run=subprocess.run(command,capture_output=True,text=True);(LOG/(name+'-compile.txt')).write_text(run.stdout+run.stderr)
 commands.append({'argv':args,'command':command,'exit':run.returncode})
 if run.returncode: return False
 run=subprocess.run('"%s"'%exe,capture_output=True,text=True,timeout=100)
 (LOG/(name+'-run.txt')).write_text(run.stdout+run.stderr);commands.append({'argv':[str(exe)],'command':'"%s"'%exe,'exit':run.returncode,'binary_sha256':sha(exe)})
 print(name,run.returncode,run.stdout.strip());return run.returncode==0
checks=[]
for module in ['Networking','Sessions']:
 manifest=ROOT/('docs/agent-runs/20261104-three-chats/unity/'+module+'/r3/candidate-manifest.json');m=json.loads(manifest.read_text(encoding='utf-8-sig'))
 rows=m.get('files',m.get('artifacts',[]));bad=[]
 for row in rows:
  p=ROOT/row['path']
  if sha(p)!=row['sha256'].upper():bad.append(row['path'])
 checks.append({'module':module,'manifest_sha256':sha(manifest),'rows':len(rows),'mismatches':bad});assert not bad
save(LOG/'frozen-verification-before.json',checks)
netbase=ROOT/r'campaign-staging\20261104-three-chats\unity\Networking\r3\candidate'
net=copyset([netbase/'Contracts.cs']+sorted((netbase/'production').glob('*.cs')),'frozen-networking')
ntest=copyset([netbase/'tools'/'NetworkingR3Tests.cs'],'frozen-networking-test')
ssbase=ROOT/r'campaign-staging\20261104-three-chats\unity\Sessions\r3'
ss=copyset(sorted((ssbase/'attempt-02'/'Sessions').glob('*.cs')),'frozen-sessions')
si=copyset(sorted((ssbase/'attempt-02'/'Integration').glob('*.cs')),'frozen-sessions-integration')
sn=copyset(sorted((ssbase/'inputs'/'Networking').glob('*.cs'))+[ssbase/'inputs'/'Core'/'Contracts.cs'],'frozen-sessions-networking')
st=copyset([ssbase/'attempt-02'/'test-R3'/'R3Tests.cs'],'frozen-sessions-test')
results=[suite('FrozenNetworkingR3',net+ntest),suite('FrozenSessionsR3',sn+ss+si+st)]
live=copyset([ROOT/r'unity\Assets\Ashfall\Core\Contracts.cs'],'current-core')
for folder in [r'Modules\Networking',r'Modules\Sessions',r'Integration\Sessions']:
 live+=copyset(sorted((ROOT/r'unity\Assets\Ashfall'/folder).glob('*.cs')),folder.replace('\\','-'))
owned=copyset(sorted((ROOT/r'unity\Assets\Ashfall\Integration\SessionReplication').glob('*.cs')),'candidate')
test=copyset([ROOT/r'unity\Tools\Integration\SessionReplication\r4\Tests.cs'],'candidate-test')
save(LOG/'inputs-before.json',pins)
results.append(suite('SessionReplication',live+owned+test))
post=[dict(p,current_sha256=sha(pathlib.Path(p['path'])),snapshot_sha256=sha(pathlib.Path(p['snapshot']))) for p in pins]
save(LOG/'inputs-after.json',post);save(LOG/'commands.json',commands)
save(LOG/'results.json',{'suites':results,'input_drift':[p for p in post if p['sha256']!=p['current_sha256'] or p['sha256']!=p['snapshot_sha256']]})
assert all(results),'suite failure'
assert all(p['sha256']==p['current_sha256']==p['snapshot_sha256'] for p in post),'input drift'
