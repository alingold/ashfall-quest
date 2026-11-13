"""Record tests, actual API-edge necessity gates, replay and drift without apply."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import build_graph as bg
from ports import Assembly, Graph

R, S = bg.ROOT, bg.STAGE
D = R/'docs/agent-runs/20261104-three-chats/unity/BuildGraphR6'
summary = json.loads((D/'compiler-summary.json').read_text())
counts = {}
for name, entry in summary.items():
    counts[name] = {}
    for mode in ('closures','separate'):
        values = entry[mode]
        counts[name][mode] = {'pass':sum(v == 0 for v in values.values()),
            'fail':{k:v for k,v in values.items() if v != 0 and v != 'UNKNOWN-engine'},
            'engineUnknown':sum(v == 'UNKNOWN-engine' for v in values.values())}
bg.dump(D/'counts.json', counts)
print(json.dumps(counts, indent=2))

tests = []
for script, child in (('test_build_graph.py','retained-sealed'),('test_r4.py','r4-sealed'),('test_r6.py','r6-sealed')):
    cmd = [sys.executable, '-B', str(R/'unity/Tools/BuildGraph'/script), child]
    result = subprocess.run(cmd, capture_output=True)
    (D/(script+'.log')).write_bytes(result.stdout + result.stderr)
    tests.append({'command':subprocess.list2cmdline(cmd),'exit':result.returncode})
bg.dump(D/'tests.json', tests)
assert all(t['exit'] == 0 for t in tests)

# Each omitted RuntimeLoop public API edge must produce an actual DLL diagnostic.
base = S/'replay166'
raw = json.loads((base/'graph.json').read_text())
a = next(x for x in raw['assemblies'] if x['name'] == 'Ashfall.Integration.RuntimeLoop')
run = S/'api-necessity'; run.mkdir()
c = bg.ToolchainSnapshotCompiler(run)
gates = []
for i, omit in enumerate([None] + list(bg.PUBLIC_API_EDGES[a['name']])):
    refs = [base/('separate-'+ref)/(ref+'.dll') for ref in a['references'] if ref != omit]
    result = c.compile([base/'snapshot'/s for s in a['sources']],run/('separate-Case'+str(i)),refs)
    gates.append({'omitted':omit,'exit':result,'references':[str(x) for x in refs]})
    assert (result == 0) == (omit is None)
bg.dump(D/'api-necessity.json',gates)

drift = {}
for name in ('baseline-historical','baseline-r4-captured','replay166','current-cpu'):
    run = S/name
    graph = json.loads((run/'graph.json').read_text())
    entries = []
    for x in graph['inputs']:
        p = R/x['path']
        actual = bg.digest(p.read_bytes()) if p.is_file() else None
        if actual != x['sha256']:
            entries.append(dict(x, currentSha256=actual))
        assert bg.digest((run/'snapshot'/x['path']).read_bytes()) == x['sha256']
    known = {x['path'] for x in graph['inputs']}
    for p in bg.walk(R/'unity/Assets'):
        rel = p.relative_to(R).as_posix()
        if bg.allowed_input(rel) and rel not in known:
            entries.append({'path':rel,'change':'added','currentSha256':bg.digest(p.read_bytes())})
    drift[name] = entries
bg.dump(D/'input-drift-final.json',drift)

# Validate the sealed replay plan against its snapshot, then independently stage
# it a second time and require every payload byte to match.
g = Graph([Assembly(**a) for a in raw['assemblies']], raw['inputs'], raw['findings'])
plan = json.loads((base/'apply-plan.json').read_text())
assert bg.validate_plan(plan, base/'snapshot', base) == []
second = S/'deterministic-second'
bg.stage(g, second)
payloads = []
for op in plan['operations']:
    assert (base/op['payload']).read_bytes() == (second/op['payload']).read_bytes()
    payloads.append({'path':op['payload'],'sha256':bg.digest((base/op['payload']).read_bytes())})
bg.dump(D/'deterministic-payloads.json',payloads)
bg.dump(D/'live-plan-validation.json',{'errors':bg.validate_plan(plan,R,base)})

# All required receipts are read completely, parsed and hashed; record explicit
# acceptance scope without expanding their historic neutral claims.
reads = []
for name in bg.METADATA_INPUTS:
    p = R/name
    data = p.read_bytes()
    entry = {'path':name,'sha256':bg.digest(data),'bytes':len(data)}
    if p.suffix == '.json':
        obj = json.loads(data.decode('utf-8-sig'))
        entry['topLevelFields'] = list(obj) if isinstance(obj,dict) else ['list']
        if isinstance(obj,dict):
            entry['scope'] = {k:v for k,v in obj.items() if k in ('scope','status','M1')}
    reads.append(entry)
bg.dump(D/'metadata-read-inputs.json', reads)
print('Sealed tests, edge gates, deterministic payloads and drift complete')
