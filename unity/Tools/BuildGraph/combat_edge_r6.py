"""Fresh separate-DLL gate on pinned r5 live CombatPublication source closure."""
import json
import shutil
import build_graph as bg

seed = bg.ROOT/'campaign-staging/20261104-three-chats/unity/reviews/BuildGraph-r5/live-v4'
run = bg.STAGE/'combat-edge'
run.mkdir()
graph, ids = bg.generate(seed/'snapshot')
nodes = {a.name:a for a in graph.assemblies}
target = 'Ashfall.Integration.CombatPublication'
needed = set()
def visit(name):
    if name in needed:
        return
    needed.add(name)
    for ref in nodes[name].references:
        visit(ref)
visit(target)
raw = json.loads((seed/'graph.json').read_text())
sources = {s for name in needed for s in nodes[name].sources}
sources.update(x['path'] for x in raw['inputs'] if '/Toolchain/' in x['path'])
sources.add(bg.ACCEPTED.relative_to(bg.ROOT).as_posix())
inputs = []
for name in sorted(sources):
    src = seed/'snapshot'/name
    expected = next(x['sha256'] for x in raw['inputs'] if x['path'] == name)
    assert bg.digest(src.read_bytes()) == expected
    dest = run/'snapshot'/name
    dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(src,dest)
    inputs.append({'path':name,'sha256':expected})
c = bg.ToolchainSnapshotCompiler(run)
results = {}
def build(name):
    if name in results:
        return
    a = nodes[name]
    for ref in a.references:
        build(ref)
    assert all(results[r] == 0 for r in a.references)
    results[name] = c.compile([run/'snapshot'/s for s in a.sources],run/('separate-'+name),
        [run/('separate-'+r)/(r+'.dll') for r in a.references])
build(target)
assert results[target] == 0
a = nodes[target]
omit = c.compile([run/'snapshot'/s for s in a.sources],run/'separate-WithoutCombat',
    [run/('separate-'+r)/(r+'.dll') for r in a.references if r != 'Ashfall.Modules.Combat'])
assert omit != 0
bg.dump(run/'inputs.json', inputs)
bg.dump(bg.ROOT/'docs/agent-runs/20261104-three-chats/unity/BuildGraphR6/combat-edge.json',
    {'seed':str(seed),'inputs':inputs,'builds':results,'withoutCombatExit':omit,
     'scope':'Exact small captured closure; no current full-tree or behavior approval'})
print('CombatPublication full refs PASS; removing Combat FAIL')
