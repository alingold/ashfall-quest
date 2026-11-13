"""Read-only proposal audit and retained security mutants. No Unity invocation."""
import json
import shutil
import subprocess
import sys
from pathlib import Path
import build_graph as b
import package_graph_r8 as r


def main():
    run = r.BASE/sys.argv[1]; out = run/'audit'; out.mkdir()
    catalog = json.loads((run/'package-catalog.json').read_bytes())
    definitions = {}
    for p in (run/'proposed').rglob('*.asmdef'):
        definitions[json.loads(p.read_bytes())['name']] = json.loads(p.read_bytes())
    identity = []; errors = []; guids = {}
    expected = {'com.unity.inputsystem':'1.20.1','com.unity.xr.openxr':'1.18.0','com.unity.xr.management':'4.7.0','com.unity.transport':'6.6.0','com.unity.render-pipelines.universal':'17.6.0'}
    versions = {x['package']:x['version'] for x in catalog.values()}
    for name, version in expected.items():
        if versions.get(name) != version: errors.append('stack-version:'+name+':'+str(versions.get(name)))
    for name, d in definitions.items():
        for ref in d['references']:
            if ref not in definitions and ref not in catalog: errors.append('missing-reference:'+name+':'+ref)
            if ref in catalog: identity.append({'consumer':name,'reference':ref,'guid':catalog[ref]['guid'],'package':catalog[ref]['package'],'version':catalog[ref]['version']})
    for p in (run/'proposed').rglob('*.asmdef.meta'):
        guid = p.read_text().split('guid: ')[1].strip()
        if guid in guids: errors.append('duplicate-proposal-guid:'+str(p))
        guids[guid] = str(p)
    b.dump(out/'assembly-identities.json', {'edges':identity,'errors':errors,'versions':{k:versions.get(k) for k in expected}})
    # Isolated mutations restore the two previously rejected security defects.
    mutants = {}
    for label in ('devices','inventory'):
        root = out/('mutant-'+label); root.mkdir()
        for source in (b.ROOT/'unity/Tools/BuildGraph').glob('*.py'): shutil.copyfile(source,root/source.name)
        if label == 'devices':
            p = root/'boundaries.py'; text = p.read_text(); text = text.replace('COM[1-9\\u00b9\\u00b2\\u00b3]|LPT[1-9\\u00b9\\u00b2\\u00b3]','COM[1-9]|LPT[1-9]'); p.write_text(text)
        else:
            p = root/'build_graph.py'; text = p.read_text(); text = text.replace("if plan['preconditions'] != sealed:","if False:  # security mutant"); p.write_text(text)
        code = 'import sys,runpy;sys.path.insert(0,'+repr(str(root))+');import build_graph as b;from pathlib import Path;b.ROOT=Path('+repr(str(b.ROOT))+');b.ACCEPTED=b.ROOT/"docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json";b.STAGE=Path('+repr(str(root))+');sys.argv=["test_r7.py","fresh"];runpy.run_path('+repr(str(root/'test_r7.py'))+',run_name="__main__")'
        cmd = [sys.executable,'-B','-c',code]; result = subprocess.run(cmd,capture_output=True,timeout=45)
        (root/'test.log').write_bytes(result.stdout+result.stderr); b.dump(root/'command.json',cmd)
        mutants[label] = {'exit':result.returncode,'assertionFailure':b'AssertionError' in result.stdout+result.stderr}
    b.dump(out/'mutants.json',mutants)
    # Source symbols which cause the concrete checkpoint direction reversal.
    symbols = []
    for p in (run/'snapshot/unity/Assets').rglob('*.cs'):
        for n,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1):
            if any(x in line for x in ('using Ashfall.Integration.InventoryRecovery','using Ashfall.Modules.InventoryItems','CheckpointWire','TryReadReserve','SharesReserveWith')):
                symbols.append({'path':p.relative_to(run/'snapshot').as_posix(),'line':n,'symbol':line.strip(),'sha256':b.digest(p.read_bytes())})
    b.dump(out/'source-symbols.json',symbols)
    # Explicit source membership additions/missing meta remain central preconditions.
    frozen = {p.relative_to(run/'snapshot').as_posix() for p in (run/'snapshot/unity/Assets').rglob('*.cs')}
    physical = {p.relative_to(b.ROOT).as_posix() for p in b.walk(b.ROOT/'unity/Assets') if p.suffix == '.cs'}
    b.dump(out/'physical-membership-drift.json', {'addedSincePhase15':sorted(physical-frozen),'missingNow':sorted(frozen-physical),
           'excludedActiveWriter':'SessionReplication and exact Integration/Sessions coordinator/authorization are intentionally not consumed'})
    print('identity errors',errors,'mutants',mutants)


if __name__ == '__main__': main()
