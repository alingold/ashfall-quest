"""R8 compiler-only proposal diagnostics. Never applies an operation to Assets.

All package DLL access is derived from the *explicit* proposal references.
Project public closure is promoted to explicit edges; package asmdef dependency
closure is recorded for identity review but never injected into compiler flags.
"""
import json
import re
import subprocess
import sys
from pathlib import Path
import build_graph as b

BASE = b.ROOT / 'campaign-staging/20261104-three-chats/unity/BuildGraphR8'
DOC = b.ROOT / 'docs/agent-runs/20261104-three-chats/unity/BuildGraphR8'
PROBE = b.ROOT / 'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project'
EDITOR = Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
PREFIXES = {'UnityEngine.InputSystem': 'Unity.InputSystem',
            'UnityEngine.XR.OpenXR': 'Unity.XR.OpenXR',
            'UnityEngine.XR.Management': 'Unity.XR.Management',
            'Unity.Collections': 'Unity.Collections',
            'Unity.Networking.Transport': 'Unity.Networking.Transport',
            'Unity.Mathematics': 'Unity.Mathematics', 'Unity.Burst': 'Unity.Burst'}
PROFILES = {'cpu': set(), 'windows': {'UNITY_6000_0_OR_NEWER', 'UNITY_STANDALONE_WIN'},
            'android': {'UNITY_6000_0_OR_NEWER', 'UNITY_ANDROID'},
            'editor': {'UNITY_6000_0_OR_NEWER', 'UNITY_EDITOR', 'UNITY_STANDALONE_WIN'},
            'tests': {'UNITY_6000_0_OR_NEWER', 'UNITY_EDITOR', 'UNITY_INCLUDE_TESTS'}}


def active_code(text, defines):
    """Bounded C# conditional evaluator. Unknown directives fail, never guess."""
    def condition(expr):
        if re.sub(r'[A-Za-z_]\w*|&&|\|\||[!()\s]', '', expr):
            raise ValueError('unsupported condition: ' + expr)
        expr = re.sub(r'\b[A-Za-z_]\w*\b', lambda m: str(m[0] == 'true' or m[0] in defines), expr)
        return bool(eval(expr.replace('&&', ' and ').replace('||', ' or ').replace('!', ' not '), {'__builtins__': {}}, {}))
    stack = []; enabled = True; output = []
    for line in text.splitlines():
        m = re.match(r'\s*#(if|elif|else|endif)\b(.*)', line)
        if not m:
            if enabled: output.append(line)
            continue
        op, expr = m.groups()
        if op == 'if':
            truth = condition(expr); stack.append([enabled, truth]); enabled = enabled and truth
        elif not stack: raise ValueError('unbalanced preprocessor')
        elif op == 'endif': enabled = stack.pop()[0]
        elif op == 'else':
            parent, seen = stack[-1]; enabled = parent and not seen; stack[-1][1] = True
        else:
            parent, seen = stack[-1]; truth = condition(expr)
            enabled = parent and not seen and truth; stack[-1][1] |= truth
    if stack: raise ValueError('unterminated preprocessor')
    return b.code('\n'.join(output))


def consumed(text, defines):
    code = active_code(text, defines)
    return sorted({name for ns, name in PREFIXES.items() if re.search(r'\b' + re.escape(ns) + r'\b', code)})


def eligible(definition, platform, defines):
    inc = definition.get('includePlatforms', []); exc = definition.get('excludePlatforms', [])
    if (inc and platform not in inc) or platform in exc: return False
    for constraint in definition.get('defineConstraints', []):
        if not any((v.strip()[1:] not in defines if v.strip().startswith('!') else v.strip() in defines)
                   for v in constraint.split('||')): return False
    return True


def catalog(cache):
    cache = Path('\\\\?\\' + str(cache.resolve()))
    entries = {}; guids = {}; errors = []; reads = []
    for path in sorted(cache.rglob('*.asmdef')):
        data = path.read_bytes(); d = json.loads(data); meta = Path(str(path) + '.meta')
        match = re.search(r'^guid:\s*([0-9a-f]{32})\s*$', meta.read_text(), re.M) if meta.exists() else None
        guid = match[1] if match else ''
        package = next((p for p in path.parents if (p/'package.json').exists()), None)
        pd = json.loads((package/'package.json').read_bytes()) if package else {}
        name = d['name']
        if name in entries or (guid and guid in guids): errors.append('duplicate-package-identity:' + name)
        if not guid: errors.append('missing-package-guid:' + name)
        entries[name] = {'path': str(path), 'guid': guid, 'definition': d,
                         'package': pd.get('name'), 'version': pd.get('version')}
        guids[guid] = name
        for p in (path, meta, package/'package.json' if package else path):
            if p.exists(): reads.append({'path': str(p), 'sha256': b.digest(p.read_bytes())})
    for name, entry in entries.items():
        resolved = []
        for ref in entry['definition'].get('references', []):
            target = guids.get(ref[5:]) if ref.startswith('GUID:') else ref
            if target not in entries: errors.append('unresolved-package-reference:' + name + ':' + ref)
            else: resolved.append(target)
        entry['resolvedReferences'] = resolved
    return entries, errors, reads


def accessible(definition, project_dlls, packages):
    """No transitive or global package fallback. Missing names are errors."""
    result = []
    for name in definition['references']:
        if name in project_dlls: result.append(project_dlls[name])
        elif name in packages: result.append(packages[name])
        else: raise ValueError('inaccessible reference:' + name)
    return result


def compile_one(out, name, sources, refs, profile, engine):
    out.mkdir(parents=True); artifact = out/(name+'.dll')
    lines = ['/nologo', '/langversion:5', '/warn:4', '/warnaserror+', '/target:library', '/out:"'+str(artifact)+'"']
    if engine: lines.append('/nostdlib+')
    if PROFILES[profile]: lines.append('/define:'+ ';'.join(sorted(PROFILES[profile])))
    lines += ['"'+str(p)+'"' for p in sources]
    lines += ['/reference:"'+str(p)+'"' for p in refs]
    rsp = out/'compile.rsp'; rsp.write_text('\n'.join(lines), encoding='utf-8')
    cmd = [str(b.CSC), '/noconfig', '@'+str(rsp)]
    p = subprocess.run(cmd, capture_output=True, timeout=45)
    (out/'compiler.log').write_bytes(p.stdout+p.stderr)
    b.dump(out/'command.json', cmd)
    b.dump(out/'references.json', [{'path': str(x), 'sha256': b.digest(x.read_bytes())} for x in refs])
    return p.returncode


def main():
    run = BASE / sys.argv[1]
    if run.parent != BASE or run.exists(): raise ValueError('fresh direct child required')
    list(b.walk(b.ROOT/'unity/Tools/BuildGraph'))  # ancestor/link guard
    run.mkdir(parents=True); DOC.mkdir(parents=True, exist_ok=True)
    snapshot = run/'snapshot'; inputs = []
    if len(sys.argv) > 2:
        import shutil
        seed = BASE/sys.argv[2]
        if seed.parent != BASE: raise ValueError('seed direct child required')
        shutil.copytree(seed/'snapshot', snapshot)
        inputs = json.loads((seed/'input-inventory.json').read_bytes())
        entries = json.loads((seed/'package-catalog.json').read_bytes())
        package_errors = json.loads((seed/'package-errors.json').read_bytes())
        for path in ('package-catalog.json','package-errors.json','historical-session-fallback.json'):
            shutil.copyfile(seed/path, run/path)
        propose(run,snapshot,inputs,entries,package_errors)
        return
    def pin(source, target):
        data = source.read_bytes(); dest = snapshot/target
        dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(data)
        inputs.append({'path': target, 'source': str(source), 'sha256': b.digest(data)})
    docs = b.ROOT/'docs/agent-runs/20261104-three-chats/unity'
    # Every requested receipt is parsed in full; large lists stay in the pinned copy.
    read_paths = list((b.ROOT/'unity/Documentation').glob('*.md'))
    read_paths += [docs/('accepted-neutral-v%d.json'%i) for i in range(1,7)]
    read_paths += list((docs/'phase15').glob('*.json')) + [docs/'phase13/root-validation.json', docs/'phase12/root-validation.json', docs/'GAMEPLAY-CHECKPOINT-v1.md']
    read_paths += list((docs/'reviews/BuildGraphReviewR7').glob('*.json')) + [docs/'reviews/BuildGraphReviewR7/REPORT.md']
    for source in read_paths:
        if source.suffix == '.json': json.loads(source.read_bytes())
        else: source.read_text(encoding='utf-8-sig')
        pin(source, source.relative_to(b.ROOT).as_posix())
    rows = json.loads((docs/'phase15/source-index.json').read_bytes())
    originals = {x['original'] for x in rows}
    # The ongoing Sessions writer is never consumed. Historical fallback is labelled.
    fallback = [x for x in json.loads((docs/'phase13/source-index.json').read_bytes())
                if x['original'] not in originals and '/Integration/Sessions/' in x['original']]
    b.dump(run/'historical-session-fallback.json', fallback)
    for row in rows + fallback:
        source = b.ROOT/row['snapshot']; assert b.digest(source.read_bytes()) == row['sha256'].upper()
        pin(source, row['original'])
    # Physical membership/meta/package inputs are captured once, without live source reads.
    for source in b.walk(b.ROOT/'unity/Assets'):
        if source.suffix in ('.meta', '.asmdef', '.asmref'):
            pin(source, source.relative_to(b.ROOT).as_posix())
    for source in (b.ROOT/'unity/Packages').glob('*.json'): pin(source, source.relative_to(b.ROOT).as_posix())
    entries, package_errors, package_reads = catalog(PROBE/'Library/PackageCache')
    for item in package_reads:
        source = Path(item['path']); pin(source, 'package-metadata/'+b.digest(str(source).encode())+source.suffix)
    b.dump(run/'package-catalog.json', entries); b.dump(run/'package-errors.json', package_errors)
    propose(run,snapshot,inputs,entries,package_errors)


def propose(run,snapshot,inputs,entries,package_errors):
    tools = run/'tool-source'; tools.mkdir()
    for source in (b.ROOT/'unity/Tools/BuildGraph').glob('*'):
        if source.is_file(): (tools/source.name).write_bytes(source.read_bytes())
    graph, guids = b.generate(snapshot); b.dump(run/'original-graph.json', b.asdict(graph))
    b.dump(run/'original-validation.json', b.validate(graph,guids))
    nodes = {a.name: a for a in graph.assemblies}
    # Preserve R7's explicit split-partial repair as a root-only policy proposal.
    parent = nodes.get('Ashfall.Integration'); child = nodes.pop('Ashfall.Integration.CombatPublication', None)
    if child and parent:
        parent.sources += child.sources; parent.references = sorted(set(parent.references+child.references)-{parent.name, child.name})
        for a in nodes.values(): a.references = sorted({'Ashfall.Integration' if r == child.name else r for r in a.references}-{a.name})
    definitions = {}; membership = {}; edge_reasons = {}
    for name, a in nodes.items():
        refs = set(a.references)
        refs.update(b.dependency_closure(nodes, name))
        edge_reasons[name] = {'publicProjectClosurePromoted': sorted(refs-set(a.references)), 'profiles': {}}
        for profile, defines in PROFILES.items():
            packages = sorted({r for s in a.sources for r in consumed((snapshot/s).read_text(encoding='utf-8-sig'), defines)})
            # Do not infer XR Management solely from OpenXRFeature: actual
            # negative compilation proves its base chain does not require it.
            edge_reasons[name]['profiles'][profile] = packages
            refs.update(packages)
        d = {'name': name, 'references': sorted(refs), 'includePlatforms': ['Editor'] if a.kind == 'editor' else [],
             'excludePlatforms': [], 'noEngineReferences': a.kind == 'neutral' and not any('UnityEngine' in x for x in a.external),
             'autoReferenced': True, 'allowUnsafeCode': False, 'overrideReferences': False}
        definitions[name] = d
        if name.endswith('.Tests'):
            d['optionalUnityReferences'] = ['TestAssemblies']
            d['defineConstraints'] = ['UNITY_INCLUDE_TESTS']
        membership[name] = {'sources': sorted(a.sources), 'kind': a.kind, 'profiles': {p: eligible(d, 'Editor' if p in ('editor','tests') else ('Android' if p == 'android' else 'WindowsStandalone64'), defines) for p, defines in PROFILES.items()},
                            'testAssembly': name.endswith('.Tests'), 'testGate': 'no NUnit/test runner execution; membership only'}
    b.dump(run/'edge-reasons.json', edge_reasons); b.dump(run/'membership.json', membership)
    payloads = {}
    for name, a in nodes.items():
        target = a.existing_path or a.directory+'/'+name+'.asmdef'
        payloads[target] = (json.dumps(definitions[name], indent=2, sort_keys=True)+'\n').encode()
        if not a.existing_path: payloads[target+'.meta'] = ('fileFormatVersion: 2\nguid: '+a.guid+'\n').encode()
    if child:
        payloads[child.directory+'/CombatPublication.asmref'] = b'{"reference":"Ashfall.Integration"}\n'
    for module, friends in [('HealthStates', ['Ashfall.Modules.Combat','Ashfall.Integration']), ('Combat', ['Ashfall.Integration'])]:
        path = 'unity/Assets/Ashfall/Modules/'+module+'/AssemblyVisibility.cs'
        payloads[path] = ('using System.Runtime.CompilerServices;\n'+''.join('[assembly: InternalsVisibleTo("'+f+'")]\n' for f in friends)).encode()
        # Compile proposed source policy with the exact separate assembly.
        nodes['Ashfall.Modules.'+module].sources.append(path)
    operations = []
    for target, data in payloads.items():
        dest = run/'proposed'/target; dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(data)
        before = b.ROOT/target
        old = before.read_bytes() if before.exists() else None
        backup = run/'before'/target
        if old is not None: backup.parent.mkdir(parents=True, exist_ok=True); backup.write_bytes(old)
        operations.append({'target': target, 'expectedBefore': b.digest(old) if old is not None else None, 'payload': dest.relative_to(run).as_posix(), 'sha256': b.digest(data),
                           'rollback': {'operation': 'restore-if-hash' if old is not None else 'remove-if-hash', 'sha256': b.digest(data), 'beforePayload': backup.relative_to(run).as_posix() if old is not None else None}})
    b.dump(run/'input-inventory.json', inputs)
    plan = {'schema': 'BuildGraph/package-proposal/8', 'authority': 'ROOT ONLY; UNREVIEWED; NO APPLY', 'stack': b.STACK, 'preconditions': inputs, 'operations': operations,
            'required': ['fresh independent review', 'central meta allocation', 'current source drift reconciliation', 'Unity assembly import', 'friend policy review'],
            'trustedInventorySha256': b.digest((run/'input-inventory.json').read_bytes())}
    b.dump(run/'proposal.json', plan)
    b.dump(run/'physical-preconditions.json', {'missingSourceMeta': [s for a in nodes.values() for s in a.sources if not (snapshot/(s+'.meta')).exists()], 'rootOnlyFriendPolicy': True})
    for profile in PROFILES:
        compile_profile(run, snapshot, nodes, definitions, entries, profile)
    # One final comparison, no polling and no rebase onto moving writers.
    drift = []
    for row in inputs:
        original = b.ROOT/row['path']
        source = Path(row['source'])
        if source.exists() and b.digest(source.read_bytes()) != row['sha256']: drift.append({'path': str(source), 'kind': 'consumed-input'})
        if row['path'].startswith('unity/Assets/') and (not original.exists() or b.digest(original.read_bytes()) != row['sha256']): drift.append({'path': row['path'], 'kind': 'current-source'})
    b.dump(run/'drift.json', drift)
    b.dump(DOC/(run.name+'-summary.json'), {'run': str(run), 'assemblies': len(nodes), 'operations': len(operations), 'drift': len(drift), 'packageErrors': package_errors, 'M1': 'OPEN'})
    print(run)


def compile_profile(run, snapshot, nodes, definitions, catalog_entries, profile):
    out = run/profile; out.mkdir(); results = {}; artifacts = {}; reused = {}
    engine = profile != 'cpu'
    framework = list((EDITOR/'UnityReferenceAssemblies/unity-4.8-api').glob('*.dll')) + list((EDITOR/'UnityReferenceAssemblies/unity-4.8-api/Facades').glob('*.dll')) if engine else [b.CSC.parent/(n+'.dll') for n in ('System','System.Core','System.Xml','System.Xml.Linq','System.Runtime.Serialization')]
    if engine:
        framework += list((EDITOR/'Managed/UnityEngine').glob('UnityEngine*.dll')) + [EDITOR/'NetStandard/compat/2.1.0/shims/netfx/netstandard.dll']
        if profile in ('editor','tests'):
            framework += list((EDITOR/'Managed/UnityEngine').glob('UnityEditor.*.dll'))
    framework = list({p.name: p for p in framework if p.exists()}.values())
    package_dlls = {name: PROBE/'Library/ScriptAssemblies'/(name+'.dll') for name in catalog_entries}
    platform = 'Editor' if profile in ('editor','tests') else ('Android' if profile == 'android' else 'WindowsStandalone64')
    def build(name, active):
        if name in results: return
        if name in active: results[name] = {'blockedCycle': active+[name]}; return
        d = definitions[name]; a = nodes[name]
        if not eligible(d, platform, PROFILES[profile]): results[name] = {'excludedMembership': platform}; return
        if not engine and a.kind == 'engine': results[name] = {'deferred': 'CPU guard-off is not native evidence'}; return
        local = [r for r in d['references'] if r in nodes]
        for r in local: build(r, active+[name])
        if name in results: return
        bad = [r for r in local if results.get(r) != 0]
        if bad: results[name] = {'blockedDependencies': bad}; return
        packages = [r for r in d['references'] if r not in nodes]
        invalid = [r for r in packages if r not in catalog_entries or not package_dlls[r].exists() or not eligible(catalog_entries[r]['definition'], platform, PROFILES[profile])]
        if invalid: results[name] = {'unsupportedPackages': invalid}; return
        refs = accessible(d, artifacts, package_dlls)
        sources = [(run/'proposed'/s if (run/'proposed'/s).exists() else snapshot/s) for s in a.sources]
        # Reuse only source with literally no preprocessor directives and no
        # package/engine dependencies. This is explicit CPU DLL evidence, not
        # a fresh native compile or a wider compiler reference fallback.
        cpu_results = json.loads((run/'cpu/results.json').read_bytes()) if engine else {}
        if engine and d['noEngineReferences'] and not packages and cpu_results.get(name) == 0 and all('#' not in p.read_text(encoding='utf-8-sig') for p in sources):
            artifact = run/'cpu'/name/(name+'.dll')
            artifacts[name] = artifact; results[name] = 0
            reused[name] = {'cpuArtifact':str(artifact),'sha256':b.digest(artifact.read_bytes()),'reason':'unchanged directive-free neutral source; no native recompilation claimed'}
            return
        available_framework = framework
        if d['noEngineReferences']:
            available_framework = [p for p in framework if 'UnityEngine' not in p.name and 'UnityEditor' not in p.name]
        result = compile_one(out/name, name, sources, refs+available_framework, profile, engine)
        results[name] = result
        if result == 0: artifacts[name] = out/name/(name+'.dll')
    for name in sorted(nodes): build(name, [])
    b.dump(out/'results.json', results)
    b.dump(out/'reused-neutral.json', reused)


if __name__ == '__main__': main()
