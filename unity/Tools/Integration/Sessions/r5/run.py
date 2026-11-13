from pathlib import Path
import subprocess,json,hashlib,shutil
R=Path(__file__).resolve().parents[5]; S=R/'campaign-staging/20261104-three-chats/unity/SessionsR4/r5';D=R/'docs/agent-runs/20261104-three-chats/unity/SessionsR4/r5'
num=1
while (S/('attempt-%02d'%num)).exists():num+=1
O=S/('attempt-%02d'%num);O.mkdir();L=D/O.name;L.mkdir();records=[];inputs=[]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def copy(p,group):
 q=O/group/p.name;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q);inputs.append(dict(path=str(p),snapshot=str(q),sha256=sha(q)));return q
def run(name,args):
 args=[str(x) for x in args]
 encoded=' '.join(('/out:"'+x[5:]+'"') if x.startswith('/out:') else ('"'+x+'"') if len(x)>2 and x[1]==':' else subprocess.list2cmdline([x]) for x in args)
 r=subprocess.run(encoded,capture_output=True,text=True);(L/(name+'.txt')).write_text(r.stdout+r.stderr);records.append(dict(name=name,argv=args,command=encoded,exit=r.returncode));(L/'commands.json').write_text(json.dumps(records,indent=2));print(name,r.returncode,(r.stdout+r.stderr).strip());return r.returncode
def build(name,src,test):
 exe=O/(name+'.exe');code=run(name+'-compile',[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+src+[test]);return code if code else run(name,[exe]+(['capacity'] if name=='CapacityHardGate' else []))
core=[copy(R/'unity/Assets/Ashfall/Core/Contracts.cs','Core')]
sessions=[copy(p,'Sessions') for p in sorted((R/'unity/Assets/Ashfall/Modules/Sessions').glob('*.cs'))]
bridge=[copy(p,'Bridge') for p in sorted((R/'unity/Assets/Ashfall/Integration/Sessions').glob('*.cs'))]
net=[copy(p,'Networking') for p in sorted((R/'unity/Assets/Ashfall/Modules/Networking').glob('*.cs'))]
test=copy(Path(__file__).with_name('Tests.cs'),'Tests')
results={}
for name,t,src in [('Original',R/'unity/Tools/Modules/Sessions/SessionTests.cs',core+sessions),('R2',R/'unity/Tools/Modules/Sessions/R2Tests.cs',core+sessions),('R3Current',R/'unity/Tools/Integration/Sessions/R3Tests.cs',core+sessions+bridge+net),('Candidate',test,core+sessions+bridge+net),('CapacityHardGate',test,core+sessions+bridge+net)]:
 results[name]=build(name,src,copy(t,'test-'+name))
adapted=O/'R3Monotonic.cs'
text=(R/'unity/Tools/Integration/Sessions/R3Tests.cs').read_text().replace('static long tick,life;','static long tick,life,canonicalSequence;').replace('long n;serial.TryGetValue(actor,out n);serial[actor]=++n;long t=++tick;','long n=++canonicalSequence;long t=++tick;')
adapted.write_text(text)
results['R3Monotonic']=build('R3Monotonic',core+sessions+bridge+net,adapted)
# Frozen r3 must reproduce against its own sealed expected source closure.
old=R/'campaign-staging/20261104-three-chats/unity/Sessions/r3'
frozen=[copy(p,'frozen-'+p.parent.name) for folder in ['inputs/Core','inputs/Networking','attempt-02/Sessions','attempt-02/Integration'] for p in sorted((old/folder).glob('*.cs'))]
results['FrozenR3']=build('FrozenR3',frozen,copy(R/'unity/Tools/Integration/Sessions/R3Tests.cs','frozen-test'))
(L/'inputs.json').write_text(json.dumps(inputs,indent=2));(L/'results.json').write_text(json.dumps(results,indent=2))
(L/'dependency-drift.json').write_text(json.dumps([dict(x,current_sha256=sha(Path(x['path'])),matches=sha(Path(x['path']))==x['sha256']) for x in inputs],indent=2))
print('RESULTS',results)
