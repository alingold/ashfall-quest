"""Fresh retained/mutant/package negative gates, with writes confined to R8."""
import json
import runpy
import subprocess
import sys
from pathlib import Path
import build_graph as b
import package_graph_r8 as r


def preflight(run, live_root, trusted_plan, trusted_inventory):
    """Read-only exact-byte preflight, not an apply/lock/TOCTOU guarantee."""
    try:
        return _preflight(run, live_root, trusted_plan, trusted_inventory)
    except (OSError, ValueError, KeyError, TypeError) as exc:
        return ['invalid-proposal:'+str(exc)]


def _preflight(run, live_root, trusted_plan, trusted_inventory):
    errors = []
    list(b.walk(run))
    if b.digest((run/'proposal.json').read_bytes()) != trusted_plan: return ['trusted-plan-mismatch']
    if b.digest((run/'input-inventory.json').read_bytes()) != trusted_inventory: return ['trusted-inventory-mismatch']
    plan = json.loads((run/'proposal.json').read_bytes())
    if plan['preconditions'] != json.loads((run/'input-inventory.json').read_bytes()): return ['inventory-mismatch']
    errors += b.path_errors([x['target'] for x in plan['operations']])
    errors += b.path_errors([x['payload'] for x in plan['operations']])
    if errors: return errors
    for op in plan['operations']:
        target = live_root/op['target']; payload = run/op['payload']
        if not op['target'].startswith('unity/Assets/Ashfall/'): errors.append('scope:'+op['target']); continue
        for path in (target, *target.parents):
            if path.exists() and (path.is_symlink() or path.is_junction()): errors.append('link:'+str(path))
        if errors: continue
        if b.digest(payload.read_bytes()) != op['sha256']: errors.append('payload:'+op['target'])
        before = b.digest(target.read_bytes()) if target.exists() else None
        if before != op['expectedBefore']:
            errors.append('target-before:'+op['target']); continue
        rollback = op['rollback']
        if rollback['operation'] != ('remove-if-hash' if before is None else 'restore-if-hash'): errors.append('rollback-operation:'+op['target'])
        if rollback['sha256'] != op['sha256']: errors.append('rollback:'+op['target'])
        if before is not None and b.digest((run/rollback['beforePayload']).read_bytes()) != before: errors.append('before-payload:'+op['target'])
    for item in plan['preconditions']:
        p = live_root/item['path']
        # package metadata is verified at its original read-only source, not invented project paths.
        if item['path'].startswith('package-metadata/'): p = Path(item['source'])
        if not p.exists() or b.digest(p.read_bytes()) != item['sha256']: errors.append('input-drift:'+item['path'])
    return errors


def main():
    run = r.BASE/sys.argv[1]; out = run/('verification' if len(sys.argv)<3 else sys.argv[2]); out.mkdir()
    commands = []; test_results = {}
    for script in ('test_build_graph.py','test_r4.py','test_r6.py','test_r7.py','test_package_graph_r8.py'):
        # Rebind only output root; preserve the actual retained tests and implementation.
        label = script.replace('.py','')+'-fresh'
        code = "import sys,runpy;sys.path.insert(0,"+repr(str(b.ROOT/'unity/Tools/BuildGraph'))+");import build_graph as b;from pathlib import Path;b.STAGE=Path("+repr(str(out))+");sys.argv=["+repr(script)+","+repr(label)+"];runpy.run_path("+repr(str(b.ROOT/'unity/Tools/BuildGraph'/script))+",run_name='__main__')"
        if script == 'test_package_graph_r8.py': code = code.replace(','+repr(label),'')
        cmd = [sys.executable,'-B','-c',code]; p = subprocess.run(cmd,capture_output=True,timeout=120)
        (out/(script+'.log')).write_bytes(p.stdout+p.stderr); commands.append(cmd); test_results[script]=p.returncode
    b.dump(out/'tests.json', test_results)
    # Real public package API, compiled once with explicit proposal then each edge removed.
    frameworks = json.loads(next((run/'windows').glob('*/references.json')).read_bytes())
    framework_refs = [Path(x['path']) for x in frameworks if 'ScriptAssemblies' not in x['path'] and not x['path'].startswith(str(run))]
    fixtures = {'Unity.InputSystem':'UnityEngine.InputSystem.InputAction', 'Unity.XR.OpenXR':'UnityEngine.XR.OpenXR.Features.OpenXRFeature',
                'Unity.XR.Management':'UnityEngine.XR.Management.XRLoader', 'Unity.Collections':'Unity.Collections.CollectionHelper',
                'Unity.Networking.Transport':'Unity.Networking.Transport.NetworkDriver'}
    package_dlls = {n: r.PROBE/'Library/ScriptAssemblies'/(n+'.dll') for n in fixtures}
    results = {}
    for name, symbol in fixtures.items():
        source = out/(name+'.cs'); source.write_text('public static class PackageGate { public static System.Type Read() { return typeof('+symbol+'); } }')
        # Public base metadata of OpenXR needs Management; explicitly declare it.
        refs = [name]+(['Unity.XR.Management'] if name == 'Unity.XR.OpenXR' else [])
        d = {'references':refs}
        for missing in (False,True):
            definition = {'references':[x for x in d['references'] if not missing or x != name]}
            result = r.compile_one(out/(name+('-missing' if missing else '-declared')), 'PackageGate', [source],
                                   r.accessible(definition,{},package_dlls)+framework_refs, 'windows', True)
            results[name+('-missing' if missing else '-declared')] = result
    b.dump(out/'actual-package-gates.json', results)
    source = out/'OpenXRPublicClosure.cs'
    source.write_text('public sealed class PublicClosureGate : UnityEngine.XR.OpenXR.OpenXRLoader {}')
    for missing in (False, True):
        definition = {'references':['Unity.XR.OpenXR']+([] if missing else ['Unity.XR.Management'])}
        results['loaderClosure-'+('missingManagement' if missing else 'declared')] = r.compile_one(out/('loaderClosure-'+str(missing)), 'LoaderClosure',[source],r.accessible(definition,{},package_dlls)+framework_refs,'windows',True)
    b.dump(out/'actual-package-gates.json', results)
    # The required XR Management public-base edge is tested separately.
    source = run/'snapshot/unity/Assets/Ashfall/Modules/Player/Engine/PlayerOpenXrLifecycleFeature.cs'
    # Exact directive-free CPU dependencies are shared by the profile compiler.
    local = [run/'cpu/Ashfall.Modules.Player/Ashfall.Modules.Player.dll',run/'cpu/Ashfall.Core/Ashfall.Core.dll']
    for missing in (False, True):
        d = {'references':['Unity.XR.OpenXR']+([] if missing else ['Unity.XR.Management'])}
        results['actualFeature-'+('missingManagement' if missing else 'declared')] = r.compile_one(out/('actualFeature-'+str(missing)), 'ActualFeature',[source],local+r.accessible(d,{},package_dlls)+framework_refs,'windows',True)
    results['actualFeature-missingOpenXR'] = r.compile_one(out/'actualFeature-noOpenXR','ActualFeature',[source],local+framework_refs,'windows',True)
    b.dump(out/'actual-package-gates.json', results)
    closure = out/'public-closure'; closure.mkdir()
    sources = {'A':'public sealed class AType {}', 'B':'public sealed class BType { public AType Read() { return new AType(); } }',
               'C':'public static class CType { public static object Read(BType value) { return value.Read(); } }'}
    for name,text in sources.items(): (closure/(name+'.cs')).write_text(text)
    cr = {}; dlls = {}
    for name in ('A','B'):
        refs = [] if name == 'A' else [dlls['A']]
        cr[name] = r.compile_one(closure/name,name,[closure/(name+'.cs')],refs,'cpu',False)
        dlls[name] = closure/name/(name+'.dll')
    for refs in (['B'],['A','B']):
        label='C-'+str(len(refs)); cr[label]=r.compile_one(closure/label,'C',[closure/'C.cs'],r.accessible({'references':refs},dlls,{}),'cpu',False)
    b.dump(out/'public-api-closure.json', cr)
    inventory_sha = b.digest((run/'input-inventory.json').read_bytes()); plan_sha = b.digest((run/'proposal.json').read_bytes())
    b.dump(out/'preflight-live.json', preflight(run,b.ROOT,plan_sha,inventory_sha))
    b.dump(out/'commands.json',commands)
    print(test_results); print(results)


if __name__ == '__main__': main()
