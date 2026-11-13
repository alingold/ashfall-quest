"""Compiler/CPU only. Never starts Unity. Each invocation uses a fresh run directory."""
from pathlib import Path
import hashlib,json,shutil,subprocess,struct,zlib
R=Path(r'C:\Users\aling\Documents\ashfall')
T=R/'unity/Tools/Integration/AvatarNativeAcceptance'
S=R/'campaign-staging/20261104-three-chats/unity/AvatarNativeAcceptanceR7'
D=R/'docs/agent-runs/20261104-three-chats/unity/AvatarNativeAcceptanceR7'
CSC=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(p,data):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(data,indent=2))
if not (S/'pinned.json').exists():
    paths=[R/'unity/Assets/Ashfall/Core/Contracts.cs']
    for area in ['RecoveryContracts']:
        paths+=sorted((R/'unity/Assets/Ashfall'/area).rglob('*.cs'))
    for module in ['Avatar','Player','Sessions','HealthStates','InventoryItems','WeaponHandling']:
        paths+=sorted((R/'unity/Assets/Ashfall/Modules'/module).rglob('*.cs'))
    paths+=sorted((R/'unity/Assets/Ashfall/Integration/AvatarPose').rglob('*.cs'))
    paths += [R/'unity/Tools/Modules/Avatar/AvatarR4Fixtures.cs',R/'unity/Tools/Integration/AvatarPose/LifecycleR2Tests.cs']
    context=list((R/'unity/Documentation').glob('*.md'))
    context += [R/'docs'/n for n in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md']]
    base=R/'docs/agent-runs/20261104-three-chats/unity'
    context += [base/('accepted-neutral-v%d.json'%i) for i in range(1,7)]
    context += [base/'phase21/ROOT-REPORT.md',base/'phase21/source-index.json',base/'GAMEPLAY-CHECKPOINT-v1.md',R/'docs/agent-runs/20261104-0020/preflight.json']
    fixture=R/'campaign-staging/20261104-three-chats/unity/AvatarR4'
    paths += [fixture/n for n in ['L.gltf','R.gltf','L.expected','R.expected']]
    paths += sorted(fixture.glob('*Grip*.res*'))
    rows=[]
    for p in paths+context+[CSC]:
        relative=p.relative_to(R) if p.is_relative_to(R) else Path('compiler')/p.name
        q=S/'frozen'/relative;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
        rows.append(dict(path=str(p),frozen=str(q),sha256=sha(q),bytes=q.stat().st_size))
    # Pin installed API binaries, including actual package assemblies, without import/build.
    editor=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
    project=R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
    refs=[p for p in sorted((editor/'Managed/UnityEngine').glob('*.dll'))+sorted((editor/'Managed').glob('UnityEditor*.dll')) if p.name!='UnityEditor.dll']
    refs += [editor/'NetStandard/ref/2.1.0/netstandard.dll',project/'Library/ScriptAssemblies/Unity.InputSystem.dll',project/'Library/ScriptAssemblies/Unity.XR.OpenXR.dll']
    for p in refs:
        q=S/'refs'/p.name;q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
        rows.append(dict(path=str(p),frozen=str(q),sha256=sha(q),bytes=q.stat().st_size))
    dump(S/'pinned.json',rows);dump(D/'before.json',rows)
rows=json.loads((S/('pinned-v3.json' if (S/'pinned-v3.json').exists() else ('pinned-v2.json' if (S/'pinned-v2.json').exists() else 'pinned.json'))).read_text())
if not (S/'bcl-pinned.json').exists():
    extra=[]
    for name in ['System.dll','System.Core.dll','System.Xml.dll','mscorlib.dll']:
        p=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319')/name
        q=S/'bcl'/name;q.parent.mkdir(exist_ok=True);shutil.copyfile(p,q)
        extra.append(dict(path=str(p),frozen=str(q),sha256=sha(q),bytes=q.stat().st_size))
    dump(S/'bcl-pinned.json',extra)
rows+=json.loads((S/'bcl-pinned.json').read_text())
assert all(sha(Path(x['frozen']))==x['sha256'] for x in rows),'immutable input drift'
i=1
while (S/('run%02d'%i)).exists():i+=1
O=S/('run%02d'%i);O.mkdir();E=D/O.name;E.mkdir(parents=True)
owned=[]
for p in sorted(T.glob('*')):
    if p.is_file():q=O/p.name;shutil.copyfile(p,q);owned.append(dict(path=str(p),frozen=str(q),sha256=sha(q)))
dump(E/'tool-inputs.json',owned)
F=S/'frozen';A=F/'unity/Assets/Ashfall'
work=O/'fixtures';work.mkdir()
for p in (F/'campaign-staging/20261104-three-chats/unity/AvatarR4').glob('*'):shutil.copyfile(p,work/p.name)
def png(pixel):
    def chunk(t,b):return struct.pack('>I',len(b))+t+b+struct.pack('>I',zlib.crc32(t+b)&0xffffffff)
    return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',2,2,8,6,0,0,0))+chunk(b'IDAT',zlib.compress((b'\0'+bytes(pixel)*2)*2))+chunk(b'IEND',b'')
for n,pixel in [('albedo',(150,150,150,255)),('orm',(255,128,0,255)),('normal',(128,128,255,255))]:
    (work/('glove_camo_'+n+'.png')).write_bytes(png(pixel))
dump(E/'fixture-inputs.json',[dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size) for p in sorted(work.glob('*'))])
results=[]
def compile(name,sources,refs,native=False,exe=False):
    out=O/(name+('.exe' if exe else '.dll'))
    args=['/nologo','/langversion:5','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:'+str(out)]
    if native:args+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER;UNITY_EDITOR']
    args+=['/reference:'+str(p) for p in refs]+[str(p) for p in sources]
    rsp=O/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"' for a in args))
    cmd=[str(CSC),'/noconfig','@'+str(rsp)]
    r=subprocess.run(cmd,capture_output=True,text=True);(E/(name+'-compile.log')).write_text(r.stdout+r.stderr)
    results.append(dict(name=name,command=cmd,compile=r.returncode,responseSha256=sha(rsp)))
    if r.returncode:raise RuntimeError(name+'\n'+r.stdout+r.stderr)
    return out
def execute(name,out):
    r=subprocess.run([str(out)],cwd=work,capture_output=True,text=True);(E/(name+'-run.log')).write_text(r.stdout+r.stderr)
    results.append(dict(name=name,command=[str(out)],cwd=str(work),run=r.returncode));return r.returncode
groups=[('Core',[A/'Core/Contracts.cs']),('Recovery',sorted((A/'RecoveryContracts').rglob('*.cs')))]
groups += [(m,sorted((A/'Modules'/m).rglob('*.cs'))) for m in ['HealthStates','Sessions','Avatar','Player','InventoryItems','WeaponHandling']]
groups += [('AvatarPose',sorted((A/'Integration/AvatarPose').rglob('*.cs')))]
try:
    bcl=[S/'bcl'/n for n in ['System.dll','System.Core.dll','System.Xml.dll']]
    cpu=[]
    for name,src in groups:cpu.append(compile('cpu-'+name,src,bcl+cpu))
    fixture=O/'PublicFixtures.cs'
    test=compile('cpu-acceptance',[O/'AcceptanceCpu.cs',fixture],bcl+cpu,exe=True)
    assert execute('cpu-acceptance',test)==0
    regression=compile('cpu-retained-r2',[F/'unity/Tools/Integration/AvatarPose/LifecycleR2Tests.cs',fixture],bcl+cpu,exe=True)
    assert execute('cpu-retained-r2',regression)==0
    # Each mutant changes production owner behavior; tests remain byte-identical.
    original=A/'Integration/AvatarPose/PoseFrame.cs'
    mutants=[('connection','Object.ReferenceEquals(connection,session.GetConnectionIdentity(identity.Actor))','true'),('ownership','Object.ReferenceEquals(token,owner)','true'),('legacy','if(connection!=null&&','if(connection==null)return true;\n            if(connection!=null&&'),('revoke','if(Owns(token)){owner=null;','{owner=null;')]
    for name,old,new in mutants:
        text=original.read_text();assert text.count(old)==1
        path=O/('mutant-'+name+'.cs');path.write_text(text.replace(old,new))
        src=[path if p==original else p for p in groups[-1][1]]
        dll=compile('mutant-'+name,src,bcl+cpu[:-1])
        # Assembly references retain the same dependency names; put mutant at the
        # exact baseline assembly identity by compiling to a private directory.
        mutant_dir=O/('case-'+name);mutant_dir.mkdir()
        for p in cpu:shutil.copyfile(p,mutant_dir/p.name)
        rsp=O/('mutant-'+name+'.rsp');content=rsp.read_text().replace('/out:'+str(dll),'/out:'+str(mutant_dir/'cpu-AvatarPose.dll'))
        rsp2=mutant_dir/'compile.rsp';rsp2.write_text(content)
        r=subprocess.run([str(CSC),'/noconfig','@'+str(rsp2)],capture_output=True,text=True)
        (E/('mutant-'+name+'-identity.log')).write_text(r.stdout+r.stderr);results.append(dict(name='mutant-'+name+'-identity',command=[str(CSC),'/noconfig','@'+str(rsp2)],compile=r.returncode,responseSha256=sha(rsp2)));assert r.returncode==0
        shutil.copyfile(test,mutant_dir/test.name)
        assert execute('mutant-'+name,mutant_dir/test.name)!=0,'surviving mutant '+name
    native=[];refs=[p for p in sorted((S/'refs').glob('*.dll')) if p.name!='UnityEditor.dll']
    for name,src in groups:native.append(compile('native-'+name,src,refs+native,True))
    compile('native-entrypoints',[O/'NativeAcceptance.cs',O/'AcceptanceCpu.cs',fixture],refs+native,True)
finally:
    dump(E/'results.json',results)
    dump(E/'dependency-drift.json',[dict(x,current=sha(Path(x['path'])),match=sha(Path(x['path']))==x['sha256']) for x in rows])
    dump(E/'outputs.json',[dict(path=str(p),sha256=sha(p),bytes=p.stat().st_size) for p in sorted(O.rglob('*')) if p.is_file()])
print('PASS compiler/CPU qualification; native entrypoints STAGED UNRUN: '+str(E))






