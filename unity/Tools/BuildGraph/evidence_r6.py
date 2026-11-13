"""Fresh immutable CPU compiler evidence; execute under bundled Python -B.

Historical graphs use captured tool bytes, current graphs use candidate APIs.
Compile-only diagnostics may run with validation findings; never stage such graphs.
"""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import shutil
import sys
import build_graph as bg
from ports import Assembly, Graph

R = bg.ROOT
S = bg.STAGE
D = R / 'docs/agent-runs/20261104-three-chats/unity/BuildGraphR6'


def compile_graph(module, run, graph):
    c = module.ToolchainSnapshotCompiler(run)
    nodes = {a.name:a for a in graph.assemblies}
    closures, separate = {}, {}
    def deps(name):
        found = {name}
        for ref in nodes[name].references:
            found.update(deps(ref))
        return found
    def build(name):
        if name in separate:
            return
        a = nodes[name]
        if a.kind == 'engine':
            separate[name] = 'UNKNOWN-engine'
            return
        for ref in a.references:
            build(ref)
        if any(separate[ref] != 0 for ref in a.references):
            separate[name] = 'BLOCKED-dependency'
            return
        separate[name] = c.compile([run/'snapshot'/s for s in a.sources], run/('separate-'+name),
            [run/('separate-'+ref)/(ref+'.dll') for ref in a.references])
    for a in graph.assemblies:
        if a.kind == 'engine':
            continue
        closures[a.name] = c.compile(sorted({run/'snapshot'/s for n in deps(a.name) for s in nodes[n].sources}), run/('compile-'+a.name))
        build(a.name)
    bg.dump(run/'neutral-results.json', closures)
    bg.dump(run/'separate-results.json', separate)
    return {'closures':closures, 'separate':separate}


def sealed_copy(seed, run):
    run.mkdir()
    old = json.loads((seed/'graph.json').read_text(encoding='utf-8-sig'))
    for x in old['inputs']:
        source = seed/'snapshot'/x['path']
        assert bg.digest(source.read_bytes()) == x['sha256']
        target = run/'snapshot'/x['path']
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
    return old


def main():
    summary = {}
    spec = importlib.util.spec_from_file_location('frozen_bg', S/'before/build_graph.py')
    oldmodule = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(oldmodule)
    oldmodule.ROOT = R
    oldmodule.ACCEPTED = bg.ACCEPTED
    for label, seedname in (('historical','frozen-final'), ('r4-captured','current-final')):
        seed = R/'campaign-staging/20261104-three-chats/unity/BuildGraphR4/r5'/seedname
        run = S/('baseline-'+label)
        raw = sealed_copy(seed, run)
        g, _ = oldmodule.generate(run/'snapshot')
        g.inputs = raw['inputs']
        assert bg.asdict(g) == raw
        bg.dump(run/'graph.json', raw)
        summary[label] = compile_graph(oldmodule, run, g)
        bg.dump(D/'compiler-summary-progress.json', summary)
    # Exercise public factory replay on the exact 166-input seed, not reconstructed metadata.
    seed = R/'campaign-staging/20261104-three-chats/unity/BuildGraphR4/r5/current-final'
    run = S/'replay166'
    with (D/'replay166.log').open('w') as log, contextlib.redirect_stdout(log):
        result = bg.factory(run, snapshot_from=seed)
    raw = json.loads((run/'graph.json').read_text())
    original = json.loads((seed/'graph.json').read_text())
    assert raw['inputs'] == original['inputs']
    assert len(raw['inputs']) == 166
    summary['replay166'] = {'factoryExit':result, 'inputCount':166,
        'completeSealedInventoryEqual':True,
        'closures':json.loads((run/'neutral-results.json').read_text()),
        'separate':json.loads((run/'separate-results.json').read_text())}
    # CPU syntax/API diagnostics for complete pinned current input; findings stay OPEN.
    run = S/'current-cpu'
    raw = sealed_copy(S/'current01', run)
    g, ids = bg.generate(run/'snapshot'); g.inputs = raw['inputs']
    bg.dump(run/'graph.json', bg.asdict(g))
    summary['current'] = compile_graph(bg, run, g)
    summary['current']['validationFindings'] = bg.validate(g, ids)
    summary['current']['inputs'] = len(g.inputs)
    summary['current']['sources'] = sum(len(a.sources) for a in g.assemblies)
    bg.dump(D/'compiler-summary.json', summary)
    print('Fresh graph and compiler evidence complete')


if __name__ == '__main__':
    main()
