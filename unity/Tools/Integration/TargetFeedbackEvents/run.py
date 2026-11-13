from pathlib import Path
import hashlib,json,shutil,subprocess,re,sys
root=Path(r'C:/Users/aling/Documents/ashfall'); stage=root/'campaign-staging/20261104-three-chats/unity/TargetFeedbackEventsR10'; docs=root/'docs/agent-runs/20261104-three-chats/unity/TargetFeedbackEventsR10'; tools=root/'unity/Tools/Integration/TargetFeedbackEvents'
run=stage/('run'+sys.argv[1]);run.mkdir(exist_ok=False)
rows=json.loads((docs/'input-manifest.json').read_text()); mapping={x['original']:Path(x['snapshot']) for x in rows}; identities=[]
def pin(p):
 p=Path(p);key=str(p)
 if key in mapping:return mapping[key]
 b=p.read_bytes();dst=run/'pinned'/str(len(identities))/p.name;dst.parent.mkdir(parents=True,exist_ok=True);dst.write_bytes(b);identities.append(dict(original=str(p),snapshot=str(dst),sha256=hashlib.sha256(b).hexdigest()));mapping[key]=dst;return dst
# Freeze tools, candidate and runtime references before compiling anything.
csc=pin(Path(r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'))
# Compiler remains installed executable; snapshot is identity evidence, no copied runtime launch.
csc=Path(r'C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
base=root/'campaign-staging/20261104-three-chats/unity/reviews/PresentationLiveReviewR9'
for p in tools.glob('*'):pin(p)
mapping.pop(str(root/'unity/Assets/Ashfall/Integration/PersonalSettings/Engine/NativeTargetFeedbackView.cs'),None)
view=pin(root/'unity/Assets/Ashfall/Integration/PersonalSettings/Engine/NativeTargetFeedbackView.cs');pool=pin(root/'unity/Assets/Ashfall/Integration/PersonalSettings/Engine/NativeTargetFeedbackPool.cs')
def parse(name):
 lines=(mapping[str(base/name)]).read_text().splitlines(); refs=[];sources=[]
 for line in lines:
  if line.startswith(('/r:','/reference:')):refs.append(pin(line.split(':',1)[1].strip('"')))
  elif line.startswith('"'):sources.append(pin(line.strip('"')))
 return refs,sources
cpu_refs,cpu_sources=parse('R8Retained.rsp');native_refs,native_sources=parse('NativeAPIOnly.rsp')
# Verify native frozen APIs against current installed6000.6.8f1 bytes, not substitutes.
installed=Path(r'C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
api=[]
installed_files={}
for f in installed.rglob('*.dll'):installed_files.setdefault(f.name,[]).append(f)
for p in native_refs:
 hits=installed_files.get(p.name,[]);match=[x for x in hits if hashlib.sha256(x.read_bytes()).digest()==hashlib.sha256(p.read_bytes()).digest()]
 if not match:raise Exception('No installed API identity match '+str(p))
 api.append(dict(snapshot=str(p),installed=str(match[0]),sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
(docs/('installed-api-identities-'+sys.argv[1]+'.json')).write_text(json.dumps(api,indent=2))
(docs/('test-input-manifest-'+sys.argv[1]+'.json')).write_text(json.dumps(identities,indent=2))
results=[]
def compile(name,refs,sources,extra):
 rsp=run/(name+'.rsp');out=run/(name+('.exe' if '/target:exe' in extra else '.dll'))
 lines=['/nologo','/langversion:5','/warnaserror+']+extra+['/out:"'+str(out)+'"']+['/r:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in sources]
 rsp.write_text('\n'.join(lines));cmd=[str(csc),'/noconfig','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,text=True);(run/(name+'-compile.log')).write_text(p.stdout+p.stderr);results.append(dict(name=name,command=cmd,exit=p.returncode,output=p.stdout+p.stderr));(docs/('results-'+sys.argv[1]+'.json')).write_text(json.dumps(results,indent=2));return out,p.returncode
def execute(name,path):
 cmd=[str(path)];p=subprocess.run(cmd,capture_output=True,text=True);(run/(name+'-run.log')).write_text(p.stdout+p.stderr);results.append(dict(name=name,command=cmd,exit=p.returncode,output=p.stdout+p.stderr));(docs/('results-'+sys.argv[1]+'.json')).write_text(json.dumps(results,indent=2));return p.returncode
ret,rc=compile('RetainedR8',cpu_refs,cpu_sources,['/target:exe','/main:PresentationR7Tests']);assert rc==0;assert execute('RetainedR8',ret)==0
cpu,rc=compile('ActualCPU',cpu_refs,cpu_sources,['/target:library']);assert rc==0
pd,rc=compile('EventPool',cpu_refs,[pool],['/target:library']);assert rc==0
for name in ['PoolTests','BindingTests']:
 exe,rc=compile(name,cpu_refs+[cpu,pd],[pin(tools/(name+'.cs'))],['/target:exe','/main:'+name]);assert rc==0;assert execute(name,exe)==0
for name,old,new in [('Lifetime','.85f','1.15f'),('Rounding','MidpointRounding.AwayFromZero','MidpointRounding.ToEven'),('Minimum','Math.Max(1,Math.Round','Math.Max(0,Math.Round'),('Concurrent','if(!entries[i].Active)','if(i==0)'),('Fade','Remaining/.22f','Remaining/.44f')]:
 d=run/('mutant-'+name);d.mkdir();mut=d/'NativeTargetFeedbackPool.cs';text=pool.read_text();assert old in text;mut.write_text(text.replace(old,new,1));dll,rc=compile('Mutant'+name,cpu_refs,[mut],['/target:library']);assert rc==0
 exe,rc=compile('TestMutant'+name,cpu_refs+[dll],[pin(tools/'PoolTests.cs')],['/target:exe','/main:PoolTests']);assert rc==0;assert execute('TestMutant'+name,exe)!=0
# Keep native production dependency assembly separate from owned native view/pool and fixtures.
ns=[p for p in native_sources if not any(x in p.name for x in ['NativeTargetFeedbackView','PresentationNative','PresentationAdapter'])]
dep,rc=compile('NativeDependencies',native_refs,ns,['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER;UNITY_EDITOR']);assert rc==0
nv,rc=compile('NativeTargetFeedback',native_refs+[dep],[view,pool],['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER;UNITY_EDITOR']);assert rc==0
fixture,rc=compile('NativeFixtures',native_refs+[dep,nv],[pin(tools/'NativeGates.cs'),pin(tools/'RenderLifetimeFixture.cs')],['/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER;UNITY_EDITOR']);assert rc==0
(docs/'results.json').write_text(json.dumps(results,indent=2));print('Checks: '+str(len(results))+' command receipts');print('Native DLLs compiled only; never executed')

