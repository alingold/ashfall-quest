"""CurrentOwnerAssembly/9. Compiler-only, sealed enumeration; never applies Assets.

freeze emits externally retainable capture/plan digests. validate requires both.
Lexical BuildGraph suggestions are retained separately from explicit compiler edges.
No project/package reference is supplied unless named in plan.json.
"""
import argparse
import copy
import hashlib
import importlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(r'C:\Users\aling\Documents\ashfall')
BASE = ROOT/'campaign-staging/20261104-three-chats/unity/CurrentOwnerAssemblyR9'
DOC = ROOT/'docs/agent-runs/20261104-three-chats/unity/CurrentOwnerAssemblyR9'
CSC = Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
CACHE = ROOT/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
PACKAGES = {'UnityEngine.InputSystem':'Unity.InputSystem', 'UnityEngine.XR.OpenXR':'Unity.XR.OpenXR',
            'UnityEngine.XR.Management':'Unity.XR.Management', 'Unity.Collections':'Unity.Collections',
            'Unity.Networking.Transport':'Unity.Networking.Transport', 'Unity.Mathematics':'Unity.Mathematics',
            'Unity.Burst':'Unity.Burst'}

def sha(data): return hashlib.sha256(data).hexdigest().upper()
def dump(p, obj):
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(obj, indent=2, sort_keys=True)+'\n', encoding='utf-8')
def load(p): return json.loads(p.read_bytes())
def safe(p):
    for x in (p, *p.parents):
        if x.is_symlink() or x.is_junction(): raise ValueError('link refused: '+str(x))
    return Path('\\\\?\\'+str(p)) if len(str(p))>240 and not str(p).startswith('\\\\?\\') else p
def fresh(name):
    if not re.fullmatch('[A-Za-z0-9_-]+',name): raise ValueError('direct child required')
    p=safe(BASE/name);p.mkdir(parents=True,exist_ok=False);return p
def walk(p):
    safe(p)
    if not safe(p).exists(): return
    for entry in sorted(Path('\\\\?\\'+str(p)).iterdir()):
        x=p/entry.name
        safe(x)
        if entry.is_dir(): yield from walk(x)
        else: yield x
def tools(run):
    sys.path.insert(0,str(run/'snapshot/unity/Tools/BuildGraph'))
    return importlib.import_module('build_graph')

def freeze(name):
    run=fresh(name);rows=[]
    paths=[]
    for folder in ('unity/Assets','unity/Tools'):
        paths += [p for p in walk(ROOT/folder) if p.suffix in ('.cs','.py','.json','.asmdef','.asmref','.meta','.md')]
    paths += list((ROOT/'unity/Documentation').glob('*.md'))
    u=ROOT/'docs/agent-runs/20261104-three-chats/unity'
    paths += [ROOT/'docs'/n for n in ('TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md','agent-runs/20261104-0020/preflight.json')]
    paths += list(u.glob('accepted-neutral-v*.json'))+[u/'GAMEPLAY-CHECKPOINT-v1.md',u/'phase21/source-index.json',u/'phase21/ROOT-REPORT.md',u/'phase25/ROOT-REPORT.md',u/'BuildGraphR8/REPORT.md',u/'reviews/BuildGraphReviewR8/REPORT.md']
    paths += list(u.glob('*/REPORT.md'))
    paths += list((CACHE/'Packages').glob('*.json'))
    # Metadata and installed package binaries are pinned, never copied wholesale.
    package_files=[p for p in walk(CACHE/'Library/PackageCache') if p.name=='package.json' or p.suffix in ('.asmdef','.asmref') or p.name.endswith('.asmdef.meta')]
    paths += package_files
    for p in sorted(set(paths)):
        data=safe(p).read_bytes()
        # Read all policy/report inputs and parse JSON rather than merely statting.
        if p.suffix=='.json': json.loads(data)
        rel=p.relative_to(ROOT).as_posix();q=run/'snapshot'/rel
        safe(q.parent).mkdir(parents=True,exist_ok=True);safe(q).write_bytes(data)
        rows.append({'path':rel,'sha256':sha(data),'bytes':len(data)})
    refs=[CSC]
    refs += list((EDITOR/'UnityReferenceAssemblies/unity-4.8-api').glob('*.dll'))
    refs += list((EDITOR/'UnityReferenceAssemblies/unity-4.8-api/Facades').glob('*.dll'))
    refs += list((EDITOR/'Managed/UnityEngine').glob('UnityEngine*.dll'))
    refs += list((EDITOR/'Managed/UnityEngine').glob('UnityEditor.*.dll'))
    refs += [EDITOR/'NetStandard/compat/2.1.0/shims/netfx/netstandard.dll']
    refs += list((CACHE/'Library/ScriptAssemblies').glob('Unity*.dll'))
    refs += [CSC.parent/(n+'.dll') for n in ('System','System.Core','System.Xml','System.Xml.Linq','System.Runtime.Serialization')]
    identities=[{'path':str(safe(p)),'sha256':sha(p.read_bytes())} for p in sorted(set(refs)) if p.exists()]
    dump(run/'capture.json',{'files':rows,'readonlyReferences':identities})
    b=tools(run);g,guids=b.generate(run/'snapshot')
    dump(run/'lexical-graph.json',b.asdict(g));dump(run/'lexical-findings.json',b.validate(g,guids))
    nodes={a.name:a for a in g.assemblies};units={}
    for name,a in nodes.items():
        # Explicit enumeration of conservative public signature closure. These
        # are persisted edges, never implicit compiler allreferences.
        refs=sorted(set(a.references+b.dependency_closure(nodes,name))-{name})
        packages=set()
        for s in a.sources:
            text=b.code((run/'snapshot'/s).read_text(encoding='utf-8-sig'))
            packages.update(v for k,v in PACKAGES.items() if re.search(r'\b'+re.escape(k)+r'\b',text))
        units[name]={'sources':sorted(a.sources),'projectReferences':refs,'packageReferences':sorted(packages),
                     'kind':a.kind,'directory':a.directory,'existingAsmdef':a.existing_path,
                     'edgeBasis':'lexical candidate plus explicitly enumerated conservative public-signature closure; compiler authoritative',
                     'extraPublicEdges':sorted(set(refs)-set(a.references))}
    inventory=sorted(r['path'] for r in rows if r['path'].startswith('unity/Assets/') and r['path'].endswith('.cs'))
    plan={'schema':'CurrentOwnerAssembly/9','units':units,'sourceEnumeration':inventory,
          'fixtures':[
              {'name':'Boundary','sources':['unity/Tools/Integration/CombatCheckpointBoundary/BoundaryTests.cs'],'dependencies':['Ashfall.Core','Ashfall.Modules.HealthStates','Ashfall.RecoveryContracts'],'execute':True},
              {'name':'Health','sources':['unity/Tools/Modules/HealthStates/HealthTests.cs'],'dependencies':['Ashfall.Core','Ashfall.Modules.HealthStates','Ashfall.RecoveryContracts'],'execute':True},
              {'name':'NativeBoundary','sources':['unity/Tools/Integration/CombatCheckpointBoundary/NativeFixture.cs'],'dependencies':['Ashfall.Core','Ashfall.Modules.HealthStates','Ashfall.RecoveryContracts'],'execute':False}]}
    dump(run/'plan.json',plan)
    # Reversible root-only payloads: no GUID allocation or live metadata edits.
    ops=[]
    for name,a in units.items():
        target=a['existingAsmdef'] or a['directory']+'/'+name+'.asmdef'
        payload={'name':name,'references':a['projectReferences']+a['packageReferences'],
                 'noEngineReferences':a['kind']=='neutral','autoReferenced':True,
                 'includePlatforms':['Editor'] if a['kind']=='editor' else []}
        q=run/'proposal'/target;dump(q,payload)
        old=run/'snapshot'/target;meta=run/'snapshot'/(target+'.meta')
        ops.append({'target':target,'payload':str(q.relative_to(run)),'payloadSha256':sha(q.read_bytes()),
                    'beforeSha256':sha(old.read_bytes()) if old.exists() else None,
                    'metaSha256':sha(meta.read_bytes()) if meta.exists() else None,
                    'header':'asmdef JSON; no C# owner/header changes',
                    'preconditions':['capture+plan externally pinned','all source and parent metadata reconciled','acyclic graph and API gates pass','root allocates missing meta/GUID; independent review'],
                    'rollback':{'kind':'restore-before-if-payload-hash' if old.exists() else 'remove-if-payload-hash','before':str(old.relative_to(run)) if old.exists() else None}})
    dump(run/'proposal.json',{'applyImplemented':False,'operations':ops,'missingSourceMeta':[s for s in inventory if not (run/'snapshot'/(s+'.meta')).exists()]})
    seal={'capture':sha((run/'capture.json').read_bytes()),'plan':sha((run/'plan.json').read_bytes()),'proposal':sha((run/'proposal.json').read_bytes())}
    dump(run/'seal.json',seal);print(json.dumps(seal));return run

def verify(run,capture,plan):
    if sha((run/'capture.json').read_bytes())!=capture: raise ValueError('capture seal mismatch')
    if sha((run/'plan.json').read_bytes())!=plan: raise ValueError('plan seal mismatch')
    cap=load(run/'capture.json');p=load(run/'plan.json')
    for r in cap['files']:
        path=Path(r['path'])
        if path.is_absolute() or '..' in path.parts or '\\' in r['path'] or ':' in r['path']: raise ValueError('unsafe source')
        if sha(safe(run/'snapshot'/path).read_bytes())!=r['sha256']: raise ValueError('snapshot drift: '+r['path'])
    for r in cap['readonlyReferences']:
        if sha(safe(Path(r['path'])).read_bytes())!=r['sha256']: raise ValueError('installed dependency drift: '+r['path'])
    membership(cap,p)
    return p

def membership(cap,p):
    expected=sorted(r['path'] for r in cap['files'] if r['path'].startswith('unity/Assets/') and r['path'].endswith('.cs'))
    actual=[s for a in p['units'].values() for s in a['sources']]
    if sorted(actual)!=expected or p['sourceEnumeration']!=expected: raise ValueError('source enumeration omission/duplicate')
    known={r['path'] for r in cap['files']}
    for name,a in p['units'].items():
        if not re.fullmatch(r'[A-Za-z_][\w.]*',name): raise ValueError('invalid unit')
        if any(x not in p['units'] for x in a['projectReferences']): raise ValueError('unknown project edge')
    if {x['name'] for x in p['fixtures']}!={'Boundary','Health','NativeBoundary'}: raise ValueError('fixture omission')
    for f in p['fixtures']:
        if not f['sources'] or any(s not in known for s in f['sources']): raise ValueError('fixture source omission')
        if any(d not in p['units'] for d in f['dependencies']): raise ValueError('fixture edge')
    return p

def compile_one(out,name,sources,refs,engine=False,exe=False):
    if not engine:refs=refs+[CSC.parent/(n+'.dll') for n in ('System','System.Core','System.Xml','System.Xml.Linq','System.Runtime.Serialization')]
    refs=list(dict.fromkeys(refs))
    out.mkdir(parents=True,exist_ok=False);artifact=out/(name+('.exe' if exe else '.dll'))
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(artifact)+'"']
    if engine: args+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER;UNITY_STANDALONE_WIN']
    args+=['/reference:"'+str(p)+'"' for p in refs]+['"'+str(p)+'"' for p in sources]
    rsp=out/'compile.rsp';rsp.write_text('\n'.join(args),encoding='utf-8')
    cmd=[str(CSC),'/noconfig','@'+str(rsp)]
    z=subprocess.run(cmd,capture_output=True,timeout=90);(out/'compiler.log').write_bytes(z.stdout+z.stderr)
    dump(out/'command.json',cmd);dump(out/'references.json',[{'path':str(p),'sha256':sha(p.read_bytes())} for p in refs])
    return z.returncode,artifact

def validate(run,capture,plan,output='validation'):
    if not re.fullmatch('[A-Za-z0-9_-]+',output):raise ValueError('direct output child')
    p=verify(run,capture,plan);out=run/output;out.mkdir(exist_ok=False)
    cap=load(run/'capture.json');available={Path(r['path']).name:Path(r['path']) for r in cap['readonlyReferences']}
    summary={}
    for profile in ('cpu','guarded'):
        results={};artifacts={};engine=profile=='guarded'
        def build(name,chain):
            if name in results:return
            if name in chain:results[name]={'cycle':chain+[name]};return
            a=p['units'][name]
            if a['kind']=='editor':results[name]={'deferred':'Editor/test platform; not player evidence'};return
            if not engine and a['kind']=='engine':results[name]={'deferred':'native API guard requires installed profile'};return
            for ref in a['projectReferences']:build(ref,chain+[name])
            if name in results:return
            bad=[r for r in a['projectReferences'] if results[r]!=0]
            if bad:results[name]={'blocked':bad};return
            packages=a['packageReferences'] if engine else []
            missing=[r for r in packages if r+'.dll' not in available]
            if missing:results[name]={'missingPackages':missing};return
            refs=[artifacts[r] for r in a['projectReferences']]+[available[r+'.dll'] for r in packages]
            if engine:
                refs += [Path(r['path']) for r in cap['readonlyReferences'] if 'UnityReferenceAssemblies' in r['path'] or 'shims' in r['path']]
                if a['kind']=='engine':refs += [Path(r['path']) for r in cap['readonlyReferences'] if '/Managed/UnityEngine/' in r['path'].replace('\\','/') and Path(r['path']).name.startswith('UnityEngine')]
            else:refs += [CSC.parent/(n+'.dll') for n in ('System','System.Core','System.Xml','System.Xml.Linq','System.Runtime.Serialization')]
            code,artifact=compile_one(out/profile/name,name,[run/'snapshot'/s for s in a['sources']],refs,engine)
            results[name]=code
            if code==0:artifacts[name]=artifact
        for n in sorted(p['units']):build(n,[])
        dump(out/profile/'results.json',results)
        fs={}
        for f in p['fixtures']:
            if f['execute']==engine:continue
            bad=[d for d in f['dependencies'] if d not in artifacts]
            if bad:fs[f['name']]={'blocked':bad};continue
            code,artifact=compile_one(out/profile/('fixture-'+f['name']),f['name'],[run/'snapshot'/s for s in f['sources']],[artifacts[d] for d in f['dependencies']]+([available['UnityEngine.CoreModule.dll'],available['netstandard.dll'],EDITOR/'UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll'] if engine else []),engine,f['execute'])
            record={'compile':code,'execution':'UNRUN native' if engine else None}
            if code==0 and f['execute']:
                # CLR resolves sibling owner DLLs from an explicit private fixture directory.
                import shutil
                for d in f['dependencies']:shutil.copyfile(artifacts[d],artifact.parent/artifacts[d].name)
                z=subprocess.run([str(artifact)],cwd=artifact.parent,capture_output=True,timeout=45)
                record.update(exit=z.returncode,stdout=z.stdout.decode(errors='replace'),stderr=z.stderr.decode(errors='replace'))
            fs[f['name']]=record
        dump(out/profile/'fixtures.json',fs)
        summary[profile]={'pass':sum(x==0 for x in results.values()),'directFailures':sum(isinstance(x,int) and x!=0 for x in results.values()),'blockedOrDeferred':sum(isinstance(x,dict) for x in results.values())}
    dump(out/'summary.json',summary);print(json.dumps(summary))

def drift(run):
    cap=load(run/'capture.json');rows=[]
    for r in cap['files']:
        p=safe(ROOT/r['path']);now=sha(p.read_bytes()) if p.exists() else None
        if now!=r['sha256']:rows.append({'path':r['path'],'captured':r['sha256'],'now':now})
    frozen={r['path'] for r in cap['files'] if r['path'].startswith('unity/Assets/')}
    added=[p.relative_to(ROOT).as_posix() for p in walk(ROOT/'unity/Assets') if p.suffix in ('.cs','.meta','.asmdef','.asmref') and p.relative_to(ROOT).as_posix() not in frozen]
    dump(run/'dependency-drift.json',{'changedOrRemoved':rows,'addedAssets':added,'policy':'one final observation; never rebase test inputs'})

def main():
    a=argparse.ArgumentParser();a.add_argument('action',choices=['freeze','validate','drift']);a.add_argument('name');a.add_argument('--capture');a.add_argument('--plan');a.add_argument('--output',default='validation');v=a.parse_args()
    if v.action=='freeze':freeze(v.name)
    else:
        if not re.fullmatch('[A-Za-z0-9_-]+',v.name):raise ValueError('direct child')
        run=safe(BASE/v.name)
        if v.action=='validate':validate(run,v.capture,v.plan,v.output)
        else:drift(run)
if __name__=='__main__':main()
