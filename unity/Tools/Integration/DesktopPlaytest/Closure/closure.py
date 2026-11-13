"""Bounded desktop capture/compile/replay. Never imports or starts Unity.

Source membership is explicit in a JSON plan. Unit dependencies are explicit and
separately compiled; csc is the actual symbol/dependency oracle (no stub fallback).
Capture is exclusive, replay reads frozen bytes, all failures leave receipts.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[5]
CSC = Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
PROJECT = ROOT / 'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
OWN = Path(__file__).resolve().parent
STAGE = ROOT / 'campaign-staging/20261104-three-chats/unity/DesktopBuildClosureR1'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def dump(path, data):
    path.write_text(json.dumps(data, indent=2, sort_keys=True)+'\n', encoding='utf-8')

def members(entry):
    paths = [ROOT / p for p in entry.get('files', [])]
    for directory in entry.get('directories', []):
        folder = ROOT / directory
        if not folder.is_dir():
            raise ValueError('required source directory absent: '+str(folder))
        found = sorted(folder.glob('*.cs')) # Never implicit Engine/Editor/test recursion.
        if not found:
            raise ValueError('empty required source directory: '+str(folder))
        paths.extend(found)
    if not paths:
        raise ValueError('empty source unit')
    return sorted(set(p.resolve(strict=True) for p in paths if p.name not in entry.get('exclude', {})))

def membership_snapshot(receipt):
    # Capture-time enumeration. Replay must never use a moving source directory
    # as its completeness oracle. Pin capture.json and this file externally too;
    # their internal digests provide integrity, not authentication.
    return {key:receipt.get(key) for key in ('schema','planSha256','desktop','construction',
            'roots','sources','units','fixtures','probe','requiredTypes')}

def capture(plan_path, dest, desktop, construction):
    dest.mkdir(parents=True, exist_ok=False)
    receipt = {'schema': 1, 'status': 'CAPTURING', 'sources': [], 'references': [], 'units': [],
               'desktop': desktop, 'construction': construction, 'limits': 'compile evidence only; native/build/launch OPEN'}
    dump(dest/'capture.json', receipt)
    try:
        receipt['tool'] = {'sha256':sha(Path(__file__).read_bytes())}
        (dest/'closure.py').write_bytes(Path(__file__).read_bytes())
        receipt['compiler'] = {'path':str(CSC),'sha256':sha(CSC.read_bytes())}
        plan_bytes = plan_path.read_bytes()
        (dest/'plan.json').write_bytes(plan_bytes)
        receipt['planSha256']=sha(plan_bytes)
        plan = json.loads(plan_bytes)
        units = list(plan['units'])
        if desktop:
            entry = dict(plan['desktop'])
            entry['directories'] = entry['directories'] + (entry.get('constructionDirectories', []) if construction else [])
            if construction:
                entry['dependencies'] = entry['dependencies'] + entry['constructionDependencies']
            units.append(entry)
        if construction and not desktop:
            raise ValueError('--construction requires --desktop')
        # Calculate the reachable unit closure, retaining declared topological order.
        by_name={entry['name']:entry for entry in units}
        roots=['Desktop'] if desktop else plan['roots']
        roots += [d for fixture in plan['fixtures'] for d in fixture['dependencies']]
        reachable=set()
        def visit(name, active):
            if name in active: raise ValueError('dependency cycle: '+name)
            if name in reachable: return
            if name not in by_name: raise ValueError('unknown dependency: '+name)
            for dep in by_name[name]['dependencies']: visit(dep,active|{name})
            reachable.add(name)
        for name in roots: visit(name,set())
        units=[entry for entry in units if entry['name'] in reachable]
        receipt['roots']=sorted(set(roots))
        copied = {}
        def freeze(p, category):
            if p in copied:
                return copied[p]
            data = p.read_bytes()
            if category == 'sources' and len(data)>1024*1024:
                raise ValueError('oversize C# input: '+str(p))
            relative = category+'/'+str(len(receipt[category])).zfill(3)+'-'+p.name
            target = dest/relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
            record = {'original': str(p), 'frozen': relative, 'sha256': sha(data), 'bytes': len(data)}
            receipt[category].append(record)
            copied[p] = relative
            return relative
        names = set()
        for entry in units:
            name = entry['name']
            if name in names or not set(entry['dependencies']).issubset(names):
                raise ValueError('units must be unique and topologically ordered: '+name)
            paths = members(entry)
            if len(paths)>100:
                raise ValueError('source unit exceeds bounded 100 files')
            for p in paths:
                p.relative_to(ROOT)
                if 'Engine' in p.parts or 'Editor' in p.parts:
                    raise ValueError('native/legacy driver excluded: '+str(p))
            receipt['units'].append({'name': name, 'dependencies': entry['dependencies'], 'sources': [freeze(p,'sources') for p in paths], 'excluded': entry.get('exclude', {})})
            names.add(name)
        fixtures = []
        for fixture in plan['fixtures']:
            item = dict(fixture)
            item['sources'] = [freeze(p,'sources') for p in members(fixture)]
            fixtures.append(item)
        receipt['fixtures'] = fixtures
        # Deliberate real API witness, plus root context. Neither starts components.
        receipt['probe'] = [freeze(OWN/'InstalledApiProbe.cs','sources'), freeze(ROOT/plan['context'],'sources')]
        if desktop:
            receipt['requiredTypes'] = freeze(OWN/'DesktopRequiredTypes.cs','sources')
        api = EDITOR/'UnityReferenceAssemblies/unity-4.8-api'
        refs = [api/n for n in ('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll')]
        refs += [api/'Facades/netstandard.dll']
        refs += [EDITOR/'Managed/UnityEngine'/('UnityEngine.'+n+'Module.dll') for n in ('Core','Physics','IMGUI','TextRendering','InputLegacy','UI','XR','Mathematics')]
        refs += [PROJECT/'Library/ScriptAssemblies/Unity.InputSystem.dll']
        for p in refs:
            if not p.is_file():
                raise ValueError('missing installed reference: '+str(p))
            freeze(p.resolve(), 'references')
        # Package source and resolved-lock identities accompany the actual DLL identity.
        lock = PROJECT/'Packages/packages-lock.json'
        lock_data = lock.read_bytes()
        dependencies = json.loads(lock_data)['dependencies']
        if dependencies['com.unity.inputsystem']['version']!='1.20.1':
            raise ValueError('Input package version mismatch')
        receipt['packageLock'] = {'original': str(lock), 'sha256':sha(lock_data), 'inputVersion':'1.20.1'}
        (dest/'packages-lock.json').write_bytes(lock_data)
        package = list((PROJECT/'Library/PackageCache').glob('com.unity.inputsystem*/package.json'))
        package = [p for p in package if json.loads(p.read_bytes()).get('version')=='1.20.1']
        if len(package)!=1:
            raise ValueError('expected exactly one resolved Input1.20.1 package')
        data=package[0].read_bytes();(dest/'input-package.json').write_bytes(data)
        receipt['inputPackage']={'original':str(package[0]),'sha256':sha(data)}
        receipt['compiler'] = {'path':str(CSC),'sha256':sha(CSC.read_bytes())}
        receipt['tool'] = {'sha256':sha(Path(__file__).read_bytes())}
        (dest/'closure.py').write_bytes(Path(__file__).read_bytes())
        receipt['editorVersion']='6000.6.8f1'
        dump(dest/'membership.json',membership_snapshot(receipt))
        receipt['membershipSha256']=sha((dest/'membership.json').read_bytes())
        receipt['status']='CAPTURED'
    except Exception as exc:
        receipt['status']='CAPTURE_FAILED';receipt['error']=str(exc)
        raise
    finally:
        dump(dest/'capture.json',receipt)
    return receipt

def verify_membership(dest,receipt):
    plan=json.loads((dest/'plan.json').read_bytes())
    if type(receipt.get('desktop')) is not bool or type(receipt.get('construction')) is not bool or receipt['construction'] and not receipt['desktop']:
        raise ValueError('invalid captured feature selection')
    declared=list(plan['units'])
    if receipt['desktop']:
        entry=dict(plan['desktop']);entry['directories']=entry['directories']+(entry.get('constructionDirectories',[]) if receipt['construction'] else [])
        if receipt['construction']:entry['dependencies']=entry['dependencies']+entry['constructionDependencies']
        declared.append(entry)
    labels=[x['name'] for x in declared]+[x['name'] for x in plan['fixtures']]
    reserved={'CON','PRN','AUX','NUL'}|{'COM'+str(i) for i in range(1,10)}|{'LPT'+str(i) for i in range(1,10)}
    if len(set(x.casefold() for x in labels))!=len(labels) or any(not re.fullmatch(r'[A-Za-z][A-Za-z0-9_]{0,63}',x) or x.upper() in reserved for x in labels):
        raise ValueError('invalid/colliding captured output names')
    by_name={x['name']:x for x in declared};reachable=set()
    def visit(name,active):
        if name in active or name not in by_name:raise ValueError('invalid declared dependency graph')
        if name in reachable:return
        for dependency in by_name[name]['dependencies']:visit(dependency,active|{name})
        reachable.add(name)
    roots=['Desktop'] if receipt['desktop'] else plan['roots']
    roots=roots+[d for f in plan['fixtures'] for d in f['dependencies']]
    if receipt.get('roots')!=sorted(set(roots)):raise ValueError('captured roots differ from plan')
    for name in roots:visit(name,set())
    expected=[x for x in declared if x['name'] in reachable]
    if [x['name'] for x in receipt['units']]!=[x['name'] for x in expected]:raise ValueError('captured unit membership differs from plan')
    if [x['name'] for x in receipt['fixtures']]!=[x['name'] for x in plan['fixtures']]:raise ValueError('captured fixture membership differs from plan')
    records={x['frozen']:x for x in receipt['sources']}
    if len(records)!=len(receipt['sources']):raise ValueError('duplicate frozen source identity')
    origins=[str(Path(x['original']).resolve()).casefold() for x in receipt['sources']]
    if len(set(origins))!=len(origins):raise ValueError('duplicate original source identity')
    for row in receipt['sources']:
        original=Path(row['original'])
        if not original.is_absolute() or original.suffix!='.cs' or str(original)!=str(original.resolve()):
            raise ValueError('noncanonical C# source identity')
        original.resolve().relative_to(ROOT.resolve())
    expected_probe=[ROOT/'unity/Tools/Integration/DesktopPlaytest/Closure/InstalledApiProbe.cs',ROOT/plan['context']]
    def gate_origins(items):
        if len(items)!=len(set(items)) or any(item not in records for item in items):raise ValueError('invalid API gate source identity')
        return [Path(records[item]['original']).resolve() for item in items]
    if gate_origins(receipt.get('probe',[]))!=[p.resolve() for p in expected_probe]:raise ValueError('required API probe omitted/substituted')
    if receipt['desktop'] and gate_origins([receipt.get('requiredTypes')])!=[(ROOT/'unity/Tools/Integration/DesktopPlaytest/Closure/DesktopRequiredTypes.cs').resolve()]:
        raise ValueError('required Desktop type gate omitted/substituted')
    def source_membership(actual,spec):
        source_list=actual.get('sources',[])
        if not source_list or len(set(source_list))!=len(source_list):raise ValueError('missing/duplicate member sources')
        required={(ROOT/p).resolve() for p in spec.get('files',[])}
        folders={(ROOT/p).resolve() for p in spec.get('directories',[])}
        actual_paths=set()
        for item in source_list:
            if item not in records:raise ValueError('member source has no frozen record')
            original=Path(records[item]['original']).resolve();actual_paths.add(original)
            if original.name in spec.get('exclude',{}) or original not in required and original.parent not in folders:raise ValueError('member source outside declared plan')
        if not required.issubset(actual_paths) or any(not any(p.parent==folder for p in actual_paths) for folder in folders):raise ValueError('required source membership omitted')
    for actual,spec in zip(receipt['units'],expected):
        if actual['dependencies']!=spec['dependencies'] or actual.get('excluded',{})!=spec.get('exclude',{}):raise ValueError('captured dependency/exclusion drift')
        source_membership(actual,spec)
    for actual,spec in zip(receipt['fixtures'],plan['fixtures']):
        if actual['dependencies']!=spec['dependencies'] or actual['main']!=spec['main']:raise ValueError('captured fixture contract drift')
        source_membership(actual,spec)

def verify(dest, receipt):
    if type(receipt.get('schema')) is not int or receipt['schema']!=1:raise ValueError('unsupported capture schema')
    if sha((dest/'plan.json').read_bytes())!=receipt.get('planSha256'):
        raise ValueError('captured plan drift')
    membership=dest/'membership.json'
    if not membership.is_file() or sha(membership.read_bytes())!=receipt.get('membershipSha256'):
        raise ValueError('capture-time source enumeration absent or changed; legacy capture needs explicit qualification')
    if json.loads(membership.read_bytes())!=membership_snapshot(receipt):raise ValueError('capture-time membership/gate identity changed')
    if sha((dest/'packages-lock.json').read_bytes())!=receipt['packageLock']['sha256']:
        raise ValueError('captured package lock drift')
    if sha((dest/'input-package.json').read_bytes())!=receipt['inputPackage']['sha256']:
        raise ValueError('captured Input package metadata drift')
    for row in receipt['sources']+receipt['references']:
        if not re.fullmatch(r'(sources|references)/[0-9]{3}-[^/\\]+',row['frozen']):raise ValueError('noncanonical frozen input identity')
        frozen=(dest/row['frozen']).resolve()
        if not frozen.is_relative_to(dest.resolve()):raise ValueError('frozen input escapes capture')
        if len(frozen.read_bytes())!=row['bytes'] or sha(frozen.read_bytes())!=row['sha256']:
            raise ValueError('frozen input drift: '+row['frozen'])
    if sha(CSC.read_bytes())!=receipt['compiler']['sha256']:
        raise ValueError('compiler identity differs')
    if sha((dest/'closure.py').read_bytes())!=receipt['tool']['sha256']:
        raise ValueError('captured tool drift')
    verify_membership(dest,receipt)

def compile_capture(dest, out, run_fixtures):
    out.mkdir(parents=True,exist_ok=False)
    results={'status':'RUNNING','compiles':[], 'tests':[], 'native':'UNRUN','build':'UNRUN','launch':'UNRUN',
             'runnerSha256':sha(Path(__file__).read_bytes()),'captureSha256':sha((dest/'capture.json').read_bytes())}
    try:
        receipt=json.loads((dest/'capture.json').read_bytes())
        if receipt['status']!='CAPTURED':
            raise ValueError('capture incomplete')
        verify(dest,receipt)
        refs=[dest/r['frozen'] for r in receipt['references']]
        artifacts={}
        unit_map={u['name']:u for u in receipt['units']}
        def build(name,sources,dependencies,main=None,guard=True):
            artifact=out/(name+('.exe' if main else '.dll'))
            lines=['/nologo','/nostdlib+','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if main else 'library'),'/out:"'+str(artifact)+'"']
            if guard: lines+=['/define:UNITY_6000_0_OR_NEWER'+(';DESKTOP_WITH_CONSTRUCTION' if receipt['construction'] else '')]
            if main: lines+=['/main:'+main]
            # Neutral fixtures run against framework references only, never load Unity.
            external=refs if guard else [p for p in refs if p.name.split('-',1)[-1] in ('mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll')]
            closure=set()
            def reference(name):
                if name in closure: return
                for dep in unit_map[name]['dependencies']: reference(dep)
                closure.add(name)
            for dependency in dependencies: reference(dependency)
            lines+=['/reference:"'+str(p)+'"' for p in external+[artifacts[d] for d in sorted(closure)]]
            lines+=['"'+str(dest/s)+'"' for s in sources]
            rsp=out/(name+'.rsp');rsp.write_text('\n'.join(lines)+'\n',encoding='utf-8')
            cmd=[str(CSC),'/noconfig','@'+str(rsp)]
            proc=subprocess.run(cmd,capture_output=True,timeout=60)
            log=proc.stdout+proc.stderr;(out/(name+'.log')).write_bytes(log)
            row={'name':name,'command':cmd,'exit':proc.returncode,'rspSha256':sha(rsp.read_bytes()),'logSha256':sha(log),'outputSha256':sha(artifact.read_bytes()) if artifact.exists() else None}
            results['compiles'].append(row)
            dump(out/'results.json',results)
            if proc.returncode:
                raise ValueError('actual compiler rejected '+name+'; see '+str(out/(name+'.log')))
            artifacts[name]=artifact
            return artifact
        # Native guard-ON source units are compiled separately; no merged-owner concealment.
        for unit in receipt['units']:
            sources=list(unit['sources'])
            if unit['name']=='Desktop': sources.append(receipt['requiredTypes'])
            build(unit['name'],sources,unit['dependencies'])
        build('InstalledApi',receipt['probe'],[])
        if run_fixtures:
            for fixture in receipt['fixtures']:
                artifact=build(fixture['name'],fixture['sources'],fixture['dependencies'],fixture['main'],False)
                proc=subprocess.run([str(artifact)],capture_output=True,timeout=60)
                data=proc.stdout+proc.stderr;(out/(fixture['name']+'-test.log')).write_bytes(data)
                results['tests'].append({'name':fixture['name'],'exit':proc.returncode,'logSha256':sha(data)})
                if proc.returncode: raise ValueError('actual fixture failed: '+fixture['name'])
        verify(dest,receipt)
        results['status']='COMPILE_PASS'
    except Exception as exc:
        results['status']='FAILED';results['error']=str(exc)
    finally:
        dump(out/'results.json',results)
    print(json.dumps(results,indent=2))
    return 0 if results['status']=='COMPILE_PASS' else 1

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['capture','replay'])
    parser.add_argument('--plan',type=Path,default=OWN/'minimum.json')
    parser.add_argument('--capture',required=True,type=Path)
    parser.add_argument('--out',type=Path)
    parser.add_argument('--desktop',action='store_true')
    parser.add_argument('--construction',action='store_true')
    parser.add_argument('--fixtures',action='store_true')
    args=parser.parse_args()
    dest=args.capture.resolve()
    # This tool can only write in the leaf-owned staging tree, even on root replay.
    dest.relative_to(STAGE)
    out=(args.out or dest/'compile').resolve();out.relative_to(STAGE)
    if args.action=='capture': capture(args.plan.resolve(),dest,args.desktop,args.construction)
    return compile_capture(dest,out,args.fixtures)

if __name__=='__main__':
    try: sys.exit(main())
    except Exception as error:
        print(str(error),file=sys.stderr);sys.exit(2)
