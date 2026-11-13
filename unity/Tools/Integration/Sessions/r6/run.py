from pathlib import Path
import subprocess,json,hashlib,shutil
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/SessionAdmissionR6';D=R/'docs/agent-runs/20261104-three-chats/unity/SessionAdmissionR6'
i=1
while (S/('attempt-%02d'%i)).exists():i+=1
O=S/('attempt-%02d'%i);O.mkdir();L=D/O.name;L.mkdir();inputs=[];commands=[];results={}
def copy(p,group):
 q=O/group/p.name;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);inputs.append(dict(path=str(p),snapshot=str(q),sha256=hashlib.sha256(q.read_bytes()).hexdigest()));return q
def run(name,args):
 args=list(map(str,args));cmd=' '.join('/out:"'+a[5:]+'"' if a.startswith('/out:') else '"'+a+'"' if len(a)>2 and a[1]==':' else subprocess.list2cmdline([a]) for a in args)
 r=subprocess.run(cmd,capture_output=True,text=True);(L/(name+'.txt')).write_text(r.stdout+r.stderr);commands.append(dict(name=name,command=cmd,argv=args,exit=r.returncode));print(name,r.returncode,(r.stdout+r.stderr).strip());return r.returncode
def build(name,src,test,args=[]):
 exe=O/(name+'.exe');c=run(name+'-compile',[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+src+[test]);results[name]=c if c else run(name,[exe]+args)
paths=[R/'unity/Assets/Ashfall/Core/Contracts.cs',R/'unity/Assets/Ashfall/Modules/Sessions/Session.cs',R/'unity/Assets/Ashfall/Modules/Sessions/Ports.cs',*R.glob('unity/Assets/Ashfall/Modules/Networking/*.cs'),*R.glob('unity/Assets/Ashfall/Integration/Sessions/*.cs')]
src=[copy(p,p.parent.parent.name+'-'+p.parent.name) for p in paths]
before=[copy(S/'before'/p.relative_to(R),'before-'+p.parent.parent.name+'-'+p.parent.name) if (S/'before'/p.relative_to(R)).exists() else copy(p,'before-'+p.parent.parent.name+'-'+p.parent.name) for p in paths]
build('Before-UnchangedR3',before,copy(R/'unity/Tools/Integration/Sessions/R3Tests.cs','before-test'))
for name,t,args in [('UnchangedR3','unity/Tools/Integration/Sessions/R3Tests.cs',[]),('Original','unity/Tools/Modules/Sessions/SessionTests.cs',[]),('R2','unity/Tools/Modules/Sessions/R2Tests.cs',[]),('UnchangedR5','unity/Tools/Integration/Sessions/r5/Tests.cs',[]),('Capacity130Fresh','unity/Tools/Integration/Sessions/r5/Tests.cs',['capacity']),('R6','unity/Tools/Integration/Sessions/r6/Tests.cs',[])]:
 if (R/t).exists():build(name,src,copy(R/t,'test-'+name),args)
for name,t in [('NetworkingR2','NetworkingR2Tests.cs'),('NetworkingR3','NetworkingR3Tests.cs'),('NetworkingAdversarial','AdversarialR2.cs'),('OldPeerPolicy','r5/PeerTests.cs')]:
 build(name,src,copy(R/'unity/Tools/Modules/Networking'/t,'test-'+name))
# Define-OFF includes the real guarded engine source; no Unity references or stubs.
engine=[copy(p,'guarded-engine') for p in R.glob('unity/Assets/Ashfall/Modules/Networking/Engine/*.cs')]
results['DefineOFF']=run('DefineOFF',[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:library','/out:'+str(O/'DefineOFF.dll')]+src+engine)
(L/'inputs.json').write_text(json.dumps(inputs,indent=2));(L/'commands.json').write_text(json.dumps(commands,indent=2));(L/'results.json').write_text(json.dumps(results,indent=2));(L/'dependency-drift.json').write_text(json.dumps([dict(x,current=hashlib.sha256(Path(x['path']).read_bytes()).hexdigest(),matches=hashlib.sha256(Path(x['path']).read_bytes()).hexdigest()==x['sha256']) for x in inputs],indent=2))
(D/'latest.json').write_text(json.dumps(dict(staging=str(O),evidence=str(L),results=results),indent=2))

