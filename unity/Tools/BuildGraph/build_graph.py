"""BuildGraph/1: source snapshot, proposed asmdefs, validation and neutral probe.

Namespace inference is deliberately conservative, not a C# semantic compiler.
Recorded Toolchain compilations are the definitive neutral syntax/API check.
"""
import argparse
from dataclasses import asdict
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
from xml.sax.saxutils import escape
from ports import Assembly, Graph, SnapshotCompiler
from boundaries import index, owner, path_errors, key

ROOT = Path(__file__).resolve().parents[3]
STAGE = ROOT / 'campaign-staging/20261104-three-chats/unity/BuildGraphR7'
CSC = Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
ACCEPTED = ROOT / 'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json'
STACK = {'Unity': '6000.6.8f1', 'OpenXR': '1.18.0', 'XRManagement': '4.7.0',
         'Input': '1.20.1', 'URP': '17.6.0', 'Transport': '6.6.0'}

# Explicit public-signature edges established by r5 actual separate-DLL gates.
# This is a bounded evidence table, not a claim of semantic C# inference.
PUBLIC_API_EDGES = {
    'Ashfall.Integration.RuntimeLoop': ('Ashfall.Modules.Modes',
        'Ashfall.Modules.Combat', 'Ashfall.Modules.InventoryItems'),
    'Ashfall.Integration.CombatPublication': ('Ashfall.Modules.Combat',),
}
PLATFORMS = {'Editor', 'WindowsStandalone64', 'LinuxStandalone64', 'OSXStandalone', 'Android'}


def supported(node, errors):
    values = []
    for field in ('includePlatforms', 'excludePlatforms'):
        value = node.definition.get(field, [])
        if (not isinstance(value, list) or any(not isinstance(x, str) for x in value)):
            errors.append('invalid-platform-field:' + node.name + ':' + field)
            value = []
        if len(value) != len(set(value)) or set(value) - PLATFORMS:
            errors.append('unsupported-platform-constraints:' + node.name + ':' + field)
        values.append(set(value))
    include, exclude = values
    if include and exclude:
        errors.append('invalid-platform-constraints:' + node.name)
    return (include or PLATFORMS) - exclude


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def dependency_closure(nodes, name):
    """DLL load dependencies include indirect exposed signature types.

    This supplies compiler references, not inferred direct asmdef edges.
    """
    found = set()
    def visit(current):
        for ref in nodes[current].references:
            if ref in nodes and ref != name and ref not in found:
                found.add(ref)
                visit(ref)
    visit(name)
    return sorted(found)


def dump(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, indent=2, sort_keys=True) + '\n', encoding='utf-8', newline='\n')


def walk(root):
    """Never follow junctions, including ancestors; source assets are not traversed."""
    for ancestor in (root, *root.parents):
        if ancestor.exists() and (ancestor.is_symlink() or ancestor.is_junction()):
            raise ValueError('Link refused: ' + str(ancestor))
    for item in sorted(root.iterdir()):
        if item.is_symlink() or item.is_junction():
            raise ValueError('Link refused: ' + str(item))
        if item.is_dir():
            yield from walk(item)
        else:
            yield item


def code(text):
    # Preserve line boundaries, erase comments, ordinary/verbatim strings and chars.
    pattern = r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    return re.sub(pattern, lambda m: '\n' * m[0].count('\n') + ' ', text)


def validate(graph, existing_guids=None):
    errors = list(graph.findings)
    by_name = {}
    guids = {k.lower(): v for k, v in (existing_guids or {}).items()}
    membership = {}
    errors += path_errors([s for a in graph.assemblies for s in a.sources])
    errors += path_errors([a.directory + "/" + a.name + ".asmdef" for a in graph.assemblies])
    folded = {}
    platforms_by_name = {a.name: supported(a, errors) for a in graph.assemblies}
    for a in graph.assemblies:
        if a.name.casefold() in folded and a.name not in by_name:
            errors.append("case-assembly-collision:" + a.name)
        folded[a.name.casefold()] = a.name
        if not re.fullmatch(r"[A-Za-z_][A-Za-z_0-9.\-]*", a.name):
            errors.append("invalid-assembly-name:" + a.name)
        if a.name in by_name:
            errors.append('duplicate-assembly:' + a.name)
        by_name[a.name] = a
        if not re.fullmatch('[0-9a-fA-F]{32}', a.guid):
            errors.append('invalid-guid:' + a.name)
        if a.guid.lower() in guids and guids[a.guid.lower()] != a.existing_path + ".meta":
            errors.append('duplicate-guid:' + a.name + ':' + guids[a.guid.lower()])
        guids[a.guid.lower()] = a.name
        for source in a.sources:
            if source in membership:
                errors.append('duplicate-membership:' + source)
            membership[source] = a.name
        for ref in a.references:
            if ref == a.name:
                errors.append('self-reference:' + a.name)
    state = {}
    def visit(name, chain):
        if state.get(name) == 1:
            errors.append('cycle:' + '->'.join(chain + [name]))
            return
        if state.get(name) == 2:
            return
        state[name] = 1
        a = by_name[name]
        for ref in a.references:
            if ref not in by_name:
                errors.append('missing-reference:' + name + ':' + ref)
            else:
                if a.kind != 'editor' and by_name[ref].kind == 'editor':
                    errors.append('editor-leak:' + name + ':' + ref)
                if not platforms_by_name[name].issubset(platforms_by_name[ref]):
                    errors.append('platform-reference-mismatch:' + name + ':' + ref)
                visit(ref, chain + [name])
        if a.kind != 'editor' and any(x.startswith('UnityEditor') for x in a.external):
            errors.append('editor-api-leak:' + name)
        if a.kind == 'neutral' and any(x.startswith('UnityEngine') for x in a.external):
            errors.append('engine-api-leak:' + name)
        state[name] = 2
    for name in sorted(by_name):
        visit(name, [])
    return sorted(set(errors))


def generate(source_root):
    files = list(walk(source_root / 'unity/Assets'))
    definitions, boundaries, boundary_errors = index(files, source_root, PACKAGE_NAMES)
    assemblies = {a.name: a for a in definitions}
    contents = {}
    existing_guids = {}
    findings = boundary_errors[:]
    for path in files:
        rel = path.relative_to(source_root).as_posix()
        if path.suffix == '.meta':
            match = re.search(r'^guid:\s*([a-fA-F0-9]{32})\s*$', path.read_text(encoding='utf-8-sig'), re.M)
            if match:
                guid = match[1].lower()
                if guid in existing_guids:
                    findings.append('existing-duplicate-guid:' + rel + ':' + existing_guids[guid])
                existing_guids[guid] = rel
        if path.suffix != '.cs':
            continue
        found, inherited = owner(rel, boundaries)
        if found:
            if inherited is not None:
                inherited.sources.append(rel)
                contents[rel] = code(path.read_text(encoding='utf-8-sig'))
                if inherited.kind != 'editor' and 'Editor' in path.parts:
                    findings.append('mixed-editor-player:' + rel)
            continue
        try:
            parts = path.relative_to(source_root / 'unity/Assets/Ashfall').parts
        except ValueError:
            findings.append('unassigned-source:' + rel)
            continue
        if parts[0] == 'Core':
            base = ('Core',)
        elif parts[0] == 'Modules' and len(parts) >= 3:
            base = parts[:2]
        elif parts[0] == 'Integration':
            base = parts[:-1] if len(parts) > 2 else ('Integration',)
        else:
            findings.append('unassigned-source:' + rel)
            continue
        special = next((i for i, x in enumerate(parts[:-1]) if x in ('Engine', 'Editor', 'Tests')), None)
        kind = 'editor' if 'Toolchain' in base or 'Editor' in parts[:-1] or 'Tests' in parts[:-1] else ('engine' if 'Engine' in parts[:-1] else 'neutral')
        directory = 'unity/Assets/Ashfall/' + '/'.join(parts[:special + 1] if special is not None else base)
        name = 'Ashfall.' + directory.split('Ashfall/')[1].replace('/', '.')
        if name in assemblies and (assemblies[name].existing_path or assemblies[name].directory != directory):
            findings.append('inferred-assembly-identity-collision:' + name + ':' + rel)
            continue
        a = assemblies.setdefault(name, Assembly(name, directory, kind))
        a.sources.append(rel)
        contents[rel] = code(path.read_text(encoding='utf-8-sig'))
    # Index actual declared namespaces, resolving qualified occurrences as well as using directives.
    namespaces = {}
    types = {}
    for a in assemblies.values():
        for source in a.sources:
            for ns in re.findall(r'\bnamespace\s+([\w.]+)', contents[source]):
                namespaces.setdefault(ns, set()).add(a.name)
            types.setdefault(a.name, set()).update(re.findall(r'\bpublic\s+(?:(?:sealed|static|abstract|partial)\s+)*(?:class|struct|interface|enum)\s+(\w+)', contents[source]))
    for a in assemblies.values():
        refs, external = set(a.references), set()
        for source in a.sources:
            text = re.sub(r'\bnamespace\s+[\w.]+', '', contents[source])
            tokens = set(re.findall(r'\b\w+\b', text))
            implicit = set()
            for declared in re.findall(r'\bnamespace\s+([\w.]+)', contents[source]):
                pieces = declared.split('.')
                implicit.update('.'.join(pieces[:i]) for i in range(1, len(pieces) + 1))
            for ns, owners in namespaces.items():
                if ns in implicit or re.search(r'\b' + re.escape(ns) + r'(?=\s*[.;]|\s*\*)', text):
                    refs.update(owner for owner in owners - {a.name}
                                if (assemblies[owner].kind != 'engine' and ns not in implicit) or types[owner] & tokens)
            for ns in re.findall(r'\b((?:Ashfall|UnityEngine|UnityEditor|Unity)(?:\.[\w]+)*)\b', text):
                if not any(ns == n or ns.startswith(n + '.') for n in namespaces):
                    external.add(ns)
        # Child adapters commonly refer to parent symbols without namespace qualification.
        parent = next((b for b in assemblies.values() if b.directory == a.directory.rsplit('/', 1)[0]), None)
        if parent and a.kind in ('engine', 'editor'):
            refs.add(parent.name)
        refs.update(PUBLIC_API_EDGES.get(a.name, ()))
        if a.existing_path and refs - set(a.references):
            findings.append('existing-missing-inferred-reference:' + a.name + ':' + ','.join(sorted(refs - set(a.references))))
        a.references = sorted(refs) if not a.existing_path else a.references
        a.external = sorted(external)
        if a.existing_path:
            continue
        a.guid = hashlib.sha256(('ASHFALL/BuildGraph/1/' + a.directory).encode()).hexdigest()[:32]
        for ns in external:
            if ns.startswith('Ashfall.'):
                findings.append('unresolved-namespace:' + a.name + ':' + ns)
    graph = Graph(sorted(assemblies.values(), key=lambda a: a.name), findings=findings)
    return graph, existing_guids


def validate_asmdefs(root, external_names=()):
    """Validate concrete asmdefs/GUID refs against explicit external assembly allowlist."""
    files = list(walk(root))
    assemblies, boundaries, errors = index(files, root, external_names)
    for path in files:
        if path.suffix == '.cs':
            found, a = owner(path.relative_to(root).as_posix(), boundaries)
            if found and a:
                a.sources.append(path.relative_to(root).as_posix())
                if a.kind != 'editor' and 'Editor' in path.relative_to(root).parts:
                    errors.append('mixed-editor-player:' + str(path))
    return sorted(set(errors + validate(Graph(assemblies))))


def stage(graph, out):
    # Structural preflight is complete before even creating a directory.
    errors = validate(graph)
    targets = []
    for a in graph.assemblies:
        if a.existing_path:
            continue
        if not (a.directory == 'unity/Assets/Ashfall' or a.directory.startswith('unity/Assets/Ashfall/')):
            errors.append('unowned-proposal:' + a.directory)
        targets += ['proposed/' + a.directory + '/' + a.name + '.asmdef',
                    'proposed/' + a.directory + '/' + a.name + '.asmdef.meta.proposed',
                    'projects/' + a.name + '.csproj']
    errors += path_errors(targets)
    existing = [p.relative_to(out).as_posix() for p in walk(out)] if out.exists() else []
    errors += path_errors(existing + targets)
    occupied = {key(p) for p in existing}
    errors += ['existing-output:' + p for p in targets if key(p) in occupied or (out / p).exists()]
    if errors:
        raise ValueError(';'.join(sorted(set(errors))))
    for a in graph.assemblies:
        if a.existing_path:
            continue
        asm = {'name': a.name, 'references': a.references + package_refs(a),
               'includePlatforms': ['Editor'] if a.kind == 'editor' else [],
               'excludePlatforms': [], 'allowUnsafeCode': False, 'overrideReferences': False,
               'autoReferenced': True, 'noEngineReferences': a.kind == 'neutral'}
        target = out / 'proposed' / a.directory / (a.name + '.asmdef')
        dump(target, asm)
        target.with_suffix('.asmdef.meta.proposed').write_text('fileFormatVersion: 2\nguid: ' + a.guid + '\n', encoding='utf-8', newline='\n')
        # Exact source membership for an IDE review; engine refs intentionally unresolved.
        defines = 'UNITY_6000_0_OR_NEWER;UNITY_EDITOR' if a.kind == 'editor' else ('UNITY_6000_0_OR_NEWER' if a.kind == 'engine' else '')
        xml = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net472</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><LangVersion>5</LangVersion><DefineConstants>' + defines + '</DefineConstants></PropertyGroup><ItemGroup>'
        xml += ''.join('<Compile Include="' + escape(str(out / 'snapshot' / s), {'"': '&quot;'}) + '" />' for s in a.sources)
        xml += '</ItemGroup><ItemGroup>' + ''.join('<ProjectReference Include="' + ref + '.csproj" />' for ref in a.references) + '</ItemGroup></Project>\n'
        projects = out / 'projects'
        projects.mkdir(exist_ok=True)
        (projects / (a.name + '.csproj')).write_text(xml, encoding='utf-8')


PACKAGE_NAMES = ('Unity.InputSystem', 'Unity.Collections', 'Unity.Networking.Transport',
                 'Unity.XR.OpenXR', 'Unity.XR.Management', 'Unity.Mathematics', 'Unity.Burst')


def package_refs(a):
    # Candidate assembly names only, never package lock entries or resolved versions.
    result = set()
    mapping = {'Unity.Networking.Transport': 'Unity.Networking.Transport',
               'Unity.Collections': 'Unity.Collections', 'UnityEngine.InputSystem': 'Unity.InputSystem',
               'UnityEngine.XR.OpenXR': 'Unity.XR.OpenXR',
               'UnityEngine.XR.Management': 'Unity.XR.Management',
               'Unity.Mathematics': 'Unity.Mathematics', 'Unity.Burst': 'Unity.Burst'}
    for ns in a.external:
        for prefix, assembly in mapping.items():
            if ns == prefix or ns.startswith(prefix + '.'):
                result.add(assembly)
    return sorted(result)


def proposal_plan(graph, snapshot, staged):
    """No apply operation: coordinator must revalidate under its shared lane."""
    # Separate authority record: never reconstruct required inputs from the
    # caller-editable plan. Coordinator retains its digest out of band.
    inventory = staged / 'input-inventory.json'
    if inventory.exists():
        raise ValueError('Refuse sealed inventory replacement')
    dump(inventory, graph.inputs)
    operations = []
    for path in sorted((staged / 'proposed').rglob('*')):
        if not path.is_file():
            continue
        target = path.relative_to(staged / 'proposed').as_posix()
        if target.endswith('.meta.proposed'):
            target = target[:-len('.proposed')]
        if (snapshot / target).exists():
            raise ValueError('Refuse replacement:' + target)
        checksum = digest(path.read_bytes())
        operations.append({'target': target, 'payload': path.relative_to(staged).as_posix(),
                           'sha256': checksum, 'expectedBefore': None,
                           'rollback': {'operation': 'remove-if-hash', 'sha256': checksum}})
    return {'schema': 'BuildGraph/staged-plan/3', 'stack': STACK,
            'scope': 'unity/Assets/Ashfall',
            'authority': 'coordinator-only; no shared apply implemented',
            'preconditions': graph.inputs, 'operations': operations,
            'requires': ['exclusive coordinator host lane', 'fresh independent review',
                         'Unity package/API/import probe', 'input and payload revalidation'],
            'existingMembership': [asdict(a) for a in graph.assemblies if a.existing_path]}


def validate_plan(plan, live_root, staged, trusted_inventory_sha256=None):
    """Read-only immediate preflight; caller still owns exclusive serialization.

    Reconstruct membership and exact payload bytes from live scoped sources.
    Neither a caller-edited target nor a matching caller-edited checksum grants scope.
    """
    try:
        return _validate_plan(plan, live_root, staged, trusted_inventory_sha256)
    except (KeyError, TypeError, ValueError, AttributeError, OSError) as exc:
        return ['invalid-plan:' + str(exc)]


def validate_authorized_plan(plan, live_root, staged, trusted_inventory_sha256):
    """Authorization preflight requires an out-of-band coordinator-held identity."""
    if not isinstance(trusted_inventory_sha256, str) or not re.fullmatch('[0-9A-F]{64}', trusted_inventory_sha256):
        return ['missing-trusted-inventory-identity']
    return validate_plan(plan, live_root, staged, trusted_inventory_sha256)


def _validate_plan(plan, live_root, staged, trusted_inventory_sha256=None):
    if (plan['schema'] != 'BuildGraph/staged-plan/3' or plan['stack'] != STACK or
            plan['scope'] != 'unity/Assets/Ashfall'):
        return ['plan-schema-stack-scope-mismatch']
    errors = path_errors([x['target'] for x in plan['operations']])
    errors += path_errors([x['payload'] for x in plan['operations']])
    errors += path_errors([x['path'] for x in plan['preconditions']])
    if errors:
        return errors  # Never read an escaping/unvalidated path.
    # Reject links before reading payloads or precondition files.
    list(walk(staged))
    sealed_bytes = (staged / 'input-inventory.json').read_bytes()
    if trusted_inventory_sha256 is not None and digest(sealed_bytes) != trusted_inventory_sha256:
        return ['trusted-inventory-identity-mismatch']
    sealed = json.loads(sealed_bytes)
    if plan['preconditions'] != sealed:
        return ['sealed-precondition-inventory-mismatch']
    for x in plan['preconditions']:
        if not allowed_input(x['path']):
            errors.append('unowned-precondition:' + x['path'])
        for ancestor in (live_root / x['path'], *(live_root / x['path']).parents):
            if ancestor.exists() and (ancestor.is_symlink() or ancestor.is_junction()):
                errors.append('precondition-link:' + x['path'])
    if errors:
        return sorted(set(errors))
    graph, guids = generate(live_root)
    errors += validate(graph, guids)
    expected_ops = {}
    for a in graph.assemblies:
        if a.existing_path:
            continue
        target = a.directory + '/' + a.name + '.asmdef'
        if not target.startswith('unity/Assets/Ashfall/'):
            errors.append('unowned-proposal:' + target)
            continue
        asm = {'name': a.name, 'references': a.references + package_refs(a),
               'includePlatforms': ['Editor'] if a.kind == 'editor' else [],
               'excludePlatforms': [], 'allowUnsafeCode': False, 'overrideReferences': False,
               'autoReferenced': True, 'noEngineReferences': a.kind == 'neutral'}
        expected_ops[target] = ('proposed/' + target, digest((json.dumps(asm, indent=2, sort_keys=True) + '\n').encode('utf-8')))
        expected_ops[target + '.meta'] = ('proposed/' + target + '.meta.proposed',
            digest(('fileFormatVersion: 2\nguid: ' + a.guid + '\n').encode('utf-8')))
    if set(expected_ops) != {x['target'] for x in plan['operations']}:
        errors.append('proposal-target-set-mismatch')
    for x in plan['preconditions']:
        p = live_root / x['path']
        if not p.is_file() or digest(p.read_bytes()) != x['sha256']:
            errors.append('precondition-drift:' + x['path'])
    expected = {x['path'] for x in plan['preconditions']}
    assets = live_root / 'unity/Assets'
    actual = [p.relative_to(live_root).as_posix() for p in walk(assets)] if assets.exists() else []
    errors += path_errors(actual)
    for p in actual:
        if Path(p).suffix in ('.cs', '.asmdef', '.asmref', '.meta') and p not in expected:
            errors.append('precondition-added:' + p)
    occupied = {key(p) for p in actual}
    for x in plan['operations']:
        if expected_ops.get(x['target']) != (x['payload'], x['sha256']):
            errors.append('target-payload-binding:' + x['target'])
        if x['expectedBefore'] is not None or key(x['target']) in occupied or (live_root / x['target']).exists():
            errors.append('target-exists:' + x['target'])
        p = staged / x['payload']
        if not p.is_file() or digest(p.read_bytes()) != x['sha256']:
            errors.append('payload-drift:' + x['payload'])
        if x['rollback'] != {'operation': 'remove-if-hash', 'sha256': x['sha256']}:
            errors.append('rollback-mismatch:' + x['target'])
    return sorted(set(errors))


METADATA_INPUTS = (
    'unity/Documentation/CONTRACTS-v1.md', 'unity/Documentation/CONTRACTS-v2.md',
    'unity/Documentation/CONTRACTS-v3.md', 'unity/Documentation/COLLISION-LAYERS-v1.md',
    'unity/Documentation/NETWORK-LIFECYCLE-v4.md', 'unity/Documentation/DAMAGE-POLICY-v4.md',
    'unity/Documentation/HEALTH-TRANSACTIONS-v4.md', 'unity/Documentation/MATCH-TRANSACTIONS-v4.md',
    'unity/Documentation/AUDIO-DEPLOYMENT-v2.md',
) + tuple('docs/agent-runs/20261104-three-chats/unity/' + name for name in
    ('ownership.json', 'phase03-ownership.json', 'phase05-ownership.json',
     'phase08-ownership.json', 'phase09-ownership.json',
     'accepted-neutral-v1.json', 'accepted-neutral-v2.json', 'accepted-neutral-v3.json',
     'accepted-neutral-v4.json', 'accepted-neutral-v5.json', 'accepted-neutral-v6.json'))


def allowed_input(path):
    return path in METADATA_INPUTS or (path.startswith('unity/Assets/') and
        Path(path).suffix in ('.cs', '.meta', '.asmdef', '.asmref'))


class ToolchainSnapshotCompiler(SnapshotCompiler):
    def __init__(self, run):
        self.run = run
        tool = ROOT / 'unity/Tools/BuildGraph'
        accepted_path = run / 'snapshot' / ACCEPTED.relative_to(ROOT)
        accepted = json.loads((accepted_path if accepted_path.exists() else ACCEPTED).read_text(encoding='utf-8-sig'))
        entries = [x for x in accepted['sourceChecks'] if x['name'] == 'Toolchain']
        sources = []
        for entry in entries:
            original = Path(entry['path'])
            frozen = run / 'snapshot' / original.relative_to(ROOT)
            data = (frozen if frozen.exists() else original).read_bytes()
            if digest(data) != entry['expected']:
                raise ValueError('Accepted Toolchain drift: ' + str(original))
            dest = run / 'accepted' / original.name
            dest.parent.mkdir(exist_ok=True)
            dest.write_bytes(data)
            sources.append(dest)
        dump(run / 'accepted-inputs.json', entries)
        probe = run / 'accepted/CompilerProbe.cs'
        probe.write_bytes((tool / 'CompilerProbe.cs').read_bytes())
        sources.append(probe)
        exe = run / 'compiler-probe.exe'
        rsp = run / 'bootstrap.rsp'
        lines = ['/nologo', '/langversion:5', '/warn:4', '/warnaserror+', '/target:exe', '/out:"' + str(exe) + '"']
        for name in ('System', 'System.Core'):
            lines.append('/reference:"' + str(CSC.parent / (name + '.dll')) + '"')
        lines += ['"' + str(s) + '"' for s in sources]
        rsp.write_text('\n'.join(lines), encoding='utf-8')
        result = subprocess.run([str(CSC), '/noconfig', '@' + str(rsp)], capture_output=True, timeout=40)
        (run / 'bootstrap.log').write_bytes(result.stdout + result.stderr)
        (run / 'bootstrap-command.txt').write_text('"' + str(CSC) + '" /noconfig @"' + str(rsp) + '"', encoding='utf-8')
        if result.returncode:
            raise RuntimeError('Bootstrap failed: ' + result.stdout.decode(errors='replace'))
        self.exe = exe

    def compile(self, sources, fresh_output, references=None):
        listing = self.run / (fresh_output.name + '.sources')
        listing.write_text('\n'.join(str(p) for p in sources), encoding='utf-8')
        args = [str(self.exe), str(self.run), str(fresh_output), str(listing)]
        if references is not None:
            ref_file = self.run / (fresh_output.name + '.references')
            ref_file.write_text('\n'.join(str(p) for p in references), encoding='utf-8')
            args.append(str(ref_file))
        result = subprocess.run(args, capture_output=True, timeout=40)
        (self.run / (fresh_output.name + '.log')).write_bytes(result.stdout + result.stderr)
        return result.returncode


def factory(out, compile_neutral=True, snapshot_from=None):
    # Every output must be a new owned child. No outputs beside central sources.
    out = out.resolve()
    out.relative_to(STAGE)
    if out == STAGE or out.exists():
        raise ValueError('Use a fresh child of ' + str(STAGE))
    for ancestor in (out.parent, *out.parents):
        if ancestor.exists() and (ancestor.is_symlink() or ancestor.is_junction()):
            raise ValueError('Output link refused')
    inputs = []
    # Snapshot only small project source/metadata; never library/cache/source assets.
    input_root = ROOT
    frozen_entries = None
    if snapshot_from is not None:
        snapshot_from = snapshot_from.resolve()
        if not any(snapshot_from.is_relative_to(p) for p in (STAGE, ROOT / 'campaign-staging/20261104-three-chats/unity/BuildGraphR6', ROOT / 'campaign-staging/20261104-three-chats/unity/BuildGraphR4/r5', ROOT / 'campaign-staging/20261104-three-chats/unity/BuildGraph/phase03', ROOT / 'campaign-staging/20261104-three-chats/unity/reviews/BuildGraph-r3', ROOT / 'campaign-staging/20261104-three-chats/unity/reviews/BuildGraph-r5')):
            raise ValueError('Unowned snapshot seed')
        input_root = snapshot_from / 'snapshot'
        frozen_entries = json.loads((snapshot_from / 'graph.json').read_text())['inputs']
    inventory = [p for p in walk(input_root / 'unity/Assets') if p.suffix in ('.cs', '.meta', '.asmdef', '.asmref')]
    if frozen_entries is not None:
        names = [x['path'] for x in frozen_entries]
        if path_errors(names) or any(not allowed_input(p) for p in names):
            raise ValueError('Unsafe sealed input inventory')
        if {p.relative_to(input_root).as_posix() for p in inventory} != {p for p in names if p.startswith('unity/Assets/')}:
            raise ValueError('Frozen source inventory mismatch')
        list(walk(input_root))
        paths = [input_root / p for p in names]
        if any(digest((input_root / x['path']).read_bytes()) != x['sha256'] for x in frozen_entries):
            raise ValueError('Frozen snapshot checksum mismatch')
    else:
        paths = inventory + [input_root / p for p in METADATA_INPUTS]
    preflight = path_errors([p.relative_to(input_root).as_posix() for p in paths])
    if preflight:
        raise ValueError(';'.join(preflight))
    out.mkdir(parents=True)
    for p in sorted(paths):
        data = p.read_bytes()
        rel = p.relative_to(input_root)
        dest = out / 'snapshot' / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(data)
        inputs.append({'path': rel.as_posix(), 'sha256': digest(data)})
    if frozen_entries is not None and sorted(inputs, key=lambda x: x['path']) != sorted(frozen_entries, key=lambda x: x['path']):
        raise ValueError('Frozen snapshot manifest mismatch')
    candidate_sources = []
    for path in sorted(Path(__file__).parent.iterdir()):
        if path.is_file() and path.suffix in ('.py', '.cs', '.md'):
            target = out / 'candidate-source' / path.name
            target.parent.mkdir(exist_ok=True)
            target.write_bytes(path.read_bytes())
            candidate_sources.append({'path': path.name, 'sha256': digest(target.read_bytes())})
    dump(out / 'candidate-source.json', candidate_sources)
    graph, guids = generate(out / 'snapshot')
    graph.inputs = inputs
    errors = validate(graph, guids) + validate_asmdefs(out / 'snapshot/unity/Assets',
                                                    ('Unity.InputSystem', 'Unity.Collections', 'Unity.Networking.Transport'))
    dump(out / 'graph.json', asdict(graph))
    dump(out / 'validation.json', {'errors': errors, 'stack': STACK, 'packageResolution': 'UNKNOWN', 'engineCompile': 'UNKNOWN', 'namespaceInference': 'conservative candidate; neutral compiler is definitive'})
    symbols = []
    for a in graph.assemblies:
        for source in a.sources:
            text = code((out / 'snapshot' / source).read_text(encoding='utf-8-sig'))
            symbols.append({'source': source, 'assembly': a.name, 'kind': a.kind,
                            'namespaces': re.findall(r'\bnamespace\s+([\w.]+)', text),
                            'types': re.findall(r'\b(?:class|struct|interface|enum)\s+(\w+)', text)})
    dump(out / 'symbols.json', symbols)
    guid_plan = [{'target': a.directory + '/' + a.name + '.asmdef', 'guid': a.guid, 'status': 'proposal-only'} for a in graph.assemblies if not a.existing_path]
    for source in sorted(s for a in graph.assemblies for s in a.sources):
        if not (out / 'snapshot' / (source + '.meta')).exists():
            guid_plan.append({'target': source, 'guid': hashlib.sha256(('ASHFALL/BuildGraph/1/' + source).encode()).hexdigest()[:32], 'status': 'missing-meta-proposal-only'})
    reserved = dict(guids)
    for item in guid_plan:
        if item['guid'] in reserved:
            errors.append('guid-plan-collision:' + item['target'] + ':' + reserved[item['guid']])
        reserved[item['guid']] = item['target']
    dump(out / 'guid-plan.json', guid_plan)
    dump(out / 'source-checks.json', {'errors': errors, 'guidPlan': 'collision-checked-against-snapshot',
                                     'existingAsmdefs': 'validated against snapshot only',
                                     'sourceSymbols': 'symbols.json', 'engineAPI': 'UNKNOWN'})
    if not errors:
        stage(graph, out)
        dump(out / 'apply-plan.json', proposal_plan(graph, out / 'snapshot', out))
    results = {}
    if compile_neutral and not errors:
        compiler = ToolchainSnapshotCompiler(out)
        by_name = {a.name: a for a in graph.assemblies}
        # Compile each neutral root with its neutral/editor transitive source closure.
        # This tests API compatibility, not separate DLL boundaries (reported explicitly).
        for a in graph.assemblies:
            if a.kind == 'engine':
                continue
            closure = set()
            def collect(name):
                if name in closure:
                    return
                closure.add(name)
                for ref in by_name[name].references:
                    if ref in by_name:
                        collect(ref)
            collect(a.name)
            sources = sorted({out / 'snapshot' / s for name in closure for s in by_name[name].sources})
            results[a.name] = compiler.compile(sources, out / ('compile-' + a.name))
        dump(out / 'neutral-results.json', results)
        separate = {}
        def build(name, active):
            if name in separate:
                return
            if name in active:
                separate[name] = 'BLOCKED-cycle'
                return
            a = by_name[name]
            if a.kind == 'engine':
                separate[name] = 'UNKNOWN-engine'
                return
            for ref in a.references:
                if ref in by_name:
                    build(ref, active | {name})
            if any(separate.get(ref) != 0 for ref in a.references):
                separate[name] = 'BLOCKED-dependency'
                return
            separate[name] = compiler.compile([out / 'snapshot' / s for s in a.sources], out / ('separate-' + name),
                                             [out / ('separate-' + ref) / (ref + '.dll') for ref in dependency_closure(by_name, name)])
        for a in graph.assemblies:
            if a.kind != 'engine':
                build(a.name, set())
        dump(out / 'separate-results.json', separate)
        results.update({'separate:' + k: v for k, v in separate.items()})
    drift = [{'path': x['path'], 'snapshotSha256': x['sha256'],
              'currentSha256': digest((ROOT / x['path']).read_bytes()) if (ROOT / x['path']).exists() else None}
             for x in inputs if not (ROOT / x['path']).exists() or digest((ROOT / x['path']).read_bytes()) != x['sha256']]
    known = {x['path'] for x in inputs}
    for path in walk(ROOT / 'unity/Assets'):
        if path.suffix in ('.cs', '.meta', '.asmdef', '.asmref') and path.relative_to(ROOT).as_posix() not in known:
            drift.append({'path': path.relative_to(ROOT).as_posix(), 'snapshotSha256': None, 'currentSha256': digest(path.read_bytes()), 'change': 'added'})
    dump(out / 'drift.json', drift)
    print(json.dumps({'output': str(out), 'assemblies': len(graph.assemblies), 'sourceCount': sum(len(a.sources) for a in graph.assemblies), 'errors': errors, 'neutralExitCodes': results, 'drift': drift}, indent=2))
    return 1 if errors or drift or any(results.values()) else 0


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('output', type=Path)
    parser.add_argument('--no-compile', action='store_true')
    parser.add_argument('--from-snapshot', type=Path)
    args = parser.parse_args()
    sys.exit(factory(args.output, not args.no_compile, args.from_snapshot))
