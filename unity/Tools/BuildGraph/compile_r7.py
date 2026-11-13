"""Cycle-safe per-assembly diagnostic compiler. No closure fallback."""
import json,sys
from pathlib import Path
import build_graph as b

def compile_graph(run, graph, label):
    output=run/label; output.mkdir()
    # Probe bootstraps from exact accepted Toolchain sources in the snapshot.
    (output/'snapshot').mkdir()
    import shutil
    shutil.copytree(run/'snapshot',output/'snapshot',dirs_exist_ok=True)
    c=b.ToolchainSnapshotCompiler(output)
    nodes={a.name:a for a in graph.assemblies}; results={}
    def build(name,active):
        if name in results: return
        if name in active:
            results[name]='BLOCKED-cycle'; return
        a=nodes[name]
        if a.kind=='engine': results[name]='UNKNOWN-engine'; return
        for ref in a.references:
            if ref in nodes: build(ref,active|{name})
        if name in results: return
        bad=[r for r in a.references if results.get(r)!=0]
        if bad: results[name]={'blocked':bad}; return
        results[name]=c.compile([output/'snapshot'/s for s in a.sources],output/('separate-'+name),[output/('separate-'+r)/(r+'.dll') for r in b.dependency_closure(nodes,name)])
        b.dump(output/'results-progress.json',results)
    for name in nodes: build(name,set())
    b.dump(output/'results.json',results)
    print(label,json.dumps(results,indent=2))
    return results

if __name__=='__main__':
    from ports import Graph,Assembly
    run=b.STAGE/'phase12'; raw=json.loads((run/'graph.json').read_bytes())
    graph=Graph([Assembly(**a) for a in raw['assemblies']],raw['inputs'],raw['findings'])
    compile_graph(run,graph,sys.argv[1])
