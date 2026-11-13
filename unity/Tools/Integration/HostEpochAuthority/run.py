from pathlib import Path
import json,hashlib,subprocess,sys,shutil
r=Path.cwd();d=r/'docs/agent-runs/20261104-three-chats/unity/HostEpochAuthorityR1';s=r/'campaign-staging/20261104-three-chats/unity/HostEpochAuthorityR1';out=s/sys.argv[1];out.mkdir()
inputs=json.loads((d/'inputs.json').read_text());src=[Path(x['snapshot']) for x in inputs]
assert all(hashlib.sha256(Path(x['snapshot']).read_bytes()).hexdigest()==x['sha256'] for x in inputs)
for p in (r/'unity/Assets/Ashfall/Integration/HostEpochAuthority').glob('*.cs'):
 q=out/p.name;shutil.copyfile(p,q);src.append(q)
csc=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe';results=[]
def run(cmd):
 p=subprocess.run([str(x) for x in cmd],capture_output=True,text=True);results.append(dict(command=[str(x) for x in cmd],exit=p.returncode,stdout=p.stdout,stderr=p.stderr));(d/(sys.argv[1]+'-commands.json')).write_text(json.dumps(results,indent=2));print(p.stdout,p.stderr);p.check_returncode()
def compile(name,sources,args):
 rsp=out/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+']+args+['"'+str(p)+'"' for p in sources]));run([csc,'@'+str(rsp)])
compile('Actual',src,['/target:library','/out:"'+str(out/'Actual.dll')+'"'])
q=out/'Tests.cs';shutil.copyfile(r/'unity/Tools/Integration/HostEpochAuthority/Tests.cs',q)
compile('Tests',[q],['/target:exe','/r:"'+str(out/'Actual.dll')+'"','/out:"'+str(out/'Tests.exe')+'"'])
run([out/'Tests.exe',out])
drift=[dict(source=x['source'],expected=x['sha256'],actual=hashlib.sha256(Path(x['source']).read_bytes()).hexdigest()) for x in inputs if hashlib.sha256(Path(x['source']).read_bytes()).hexdigest()!=x['sha256']]
(d/(sys.argv[1]+'-drift.json')).write_text(json.dumps(drift,indent=2));print('External drift',len(drift))
