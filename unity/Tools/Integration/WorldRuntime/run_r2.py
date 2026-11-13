"""Fresh immutable R2 CPU run. No engines, stubs, installs or native execution."""
import hashlib,json,pathlib,shutil,subprocess,sys
ROOT=pathlib.Path(__file__).resolve().parents[4]
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/WorldRuntimeR2'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/WorldRuntimeR2'
RUN_NAME=sys.argv[1] if len(sys.argv)>1 else 'run01'
assert RUN_NAME in ('run01','run02','run03','run04','run05')
DOC=DOC/RUN_NAME;DOC.mkdir(exist_ok=False)
CSC=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def save(name,d):(DOC/name).write_text(json.dumps(d,indent=2)+'\n')
assert not (OUT/RUN_NAME).exists(),'Preserve previous outputs; choose a fresh run directory'
RUN=OUT/RUN_NAME;RUN.mkdir()
commands=[];results=[];deps=[];sources=[]
for p in sorted((OUT/'inputs').rglob('*.cs')):
    rel=p.relative_to(OUT/'inputs')
    live=ROOT/('unity/Assets/Ashfall/Core/Contracts.cs' if rel.name=='Contracts.cs' else 'unity/Assets/Ashfall/Modules/'+('' if rel.parts[0]=='MapStreaming' else 'World/')+str(rel))
    target=RUN/'dependencies'/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(live,target);deps.append(target)
    sources.append(dict(path=str(live),snapshot=str(target),sha256=sha(target),historical=sha(p),drift=sha(p)!=sha(target)))
for folder in ['unity/Assets/Ashfall/Integration/WorldRuntime','unity/Tools/Integration/WorldRuntime']:
    for p in sorted((ROOT/folder).rglob('*.cs')):
        target=RUN/'snapshot'/p.relative_to(ROOT);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,target)
        sources.append(dict(path=str(p),snapshot=str(target),sha256=sha(target)))
prod=sorted((RUN/'snapshot/unity/Assets').rglob('*.cs'))
tests=RUN/'snapshot/unity/Tools/Integration/WorldRuntime'
review=ROOT/'campaign-staging/20261104-three-chats/unity/reviews/WorldRuntime-r1'
for name in ['Adversarial.cs','Sibling.cs']:
    shutil.copyfile(review/name,RUN/name)
combat=[]
for name in ['BulletGuard.cs','CombatRules.cs','Ports.cs','SourceRadial.cs','SwordContacts.cs']:
    p=ROOT/'unity/Assets/Ashfall/Modules/Combat'/name
    target=RUN/'combat'/p.name;target.parent.mkdir(exist_ok=True);shutil.copyfile(p,target);combat.append(target)
    sources.append(dict(path=str(p),snapshot=str(target),sha256=sha(target)))
save('source-inputs.json',sources)
def run(label,args):
    command=' '.join('"'+str(a)+'"' for a in args);commands.append(command)
    p=subprocess.run(command,capture_output=True,text=True)
    (DOC/(label+'.log')).write_text(p.stdout+p.stderr)
    results.append(dict(step=label,exit=p.returncode,output=p.stdout+p.stderr))
    save('results.json',results);(DOC/'commands.txt').write_text('\n'.join(commands)+'\n')
    assert p.returncode==0,label
def suite(name,extra):
    exe=RUN/(name+'.exe')
    run(name+'-compile',[CSC,'/nologo','/langversion:5','/warnaserror+','/r:System.Runtime.Serialization.dll','/out:'+str(exe)]+deps+prod+list(tests.glob('Native*.cs'))+[tests/'WorldRuntimeFixture.cs']+extra)
    run(name,[exe]);return exe
exe=suite('retained28',[tests/'RuntimeBindingTests.cs'])
suite('adversarial22',[RUN/'Adversarial.cs'])
suite('combat4',combat+[RUN/'Sibling.cs'])
audit=RUN/'GuardOffAudit.cs'
audit.write_text('using System;using System.Reflection;class GuardOffAudit{static void Main(string[] a){var s=Assembly.LoadFrom(a[0]);foreach(var r in s.GetReferencedAssemblies()){if(r.Name.StartsWith("Unity"))throw new Exception(r.FullName);}foreach(var t in s.GetTypes()){if(t.FullName=="Ashfall.Integration.WorldRuntime.NativeWorld")throw new Exception("Guard leaked native type");}Console.WriteLine("PASS actual guard-OFF assembly: no Unity references or NativeWorld type");}}')
auditexe=RUN/'GuardOffAudit.exe'
run('guard-audit-compile',[CSC,'/nologo','/langversion:5','/warnaserror+','/out:'+str(auditexe),audit])
run('guard-audit',[auditexe,exe])
save('dependency-after.json',[dict(path=r['path'],captured=r['sha256'],current=sha(pathlib.Path(r['path'])),drift=r['sha256']!=sha(pathlib.Path(r['path']))) for r in sources])
accepted=[]
for v in range(1,6):
    p=ROOT/f'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v{v}.json';d=json.loads(p.read_text(encoding='utf-8-sig'))
    accepted.append(dict(path=str(p),sha256=sha(p),status=d.get('status'),accepted=[{k:a[k] for k in ('name','scope') if k in a} for a in d.get('accepted',[])]))
save('acceptance-read.json',accepted)
print('\n'.join(r['step']+': '+str(r['exit']) for r in results))
