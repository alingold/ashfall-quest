import sys,json,subprocess
from pathlib import Path
import build_graph as b
from ports import Assembly
run=b.STAGE/'assembly-proposal02'; neutral=run/'separate01'; out=run/'native-player06';out.mkdir()
nodes={a['name']:Assembly(**a) for a in json.loads((run/'graph.json').read_bytes())['assemblies']}
results=json.loads((neutral/'results.json').read_bytes())
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
ext=list((editor/'Managed/UnityEngine').glob('UnityEngine*.dll'))+[editor/'Managed/UnityEngine.dll',editor/'NetStandard/compat/2.1.0/shims/netfx/netstandard.dll']
packages=b.ROOT/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/ScriptAssemblies'
ext += [packages/(name+'.dll') for name in ('Unity.InputSystem','Unity.Collections','Unity.Networking.Transport','Unity.Mathematics','Unity.Burst','Unity.XR.OpenXR','Unity.XR.Management','Unity.XR.CoreUtils')]
ext += list((editor/'Managed').glob('Unity*.dll'))
# Real installed facade, never synthesized stubs.
ext=[p for p in ext if p.is_file()] + list((editor/'UnityReferenceAssemblies/unity-4.8-api/Facades').glob('*.dll')) + list((editor/'UnityReferenceAssemblies/unity-4.8-api').glob('*.dll'))
ext=list({p.name:p for p in ext if p.name not in ('UnityEngine.dll','UnityEditor.dll')}.values())
records=[{'path':str(p),'sha256':b.digest(p.read_bytes())} for p in ext];b.dump(out/'external-refs.json',records)
def build(name,active):
    a=nodes[name]
    if a.kind!='engine': return
    if name in native: return
    if name in active:native[name]='BLOCKED-cycle';return
    for ref in a.references:build(ref,active|{name})
    refs=[];bad=[]
    for ref in b.dependency_closure(nodes,name):
        if nodes[ref].kind=='engine':
            if native.get(ref)!=0:bad.append(ref)
            refs.append(out/('separate-'+ref)/(ref+'.dll'))
        else:
            if results.get(ref)!=0:bad.append(ref)
            refs.append(neutral/('separate-'+ref)/(ref+'.dll'))
    if bad:native[name]={'blocked':bad};return
    dest=out/('separate-'+name);dest.mkdir();artifact=dest/(name+'.dll')
    lines=['/nostdlib+','/nologo','/langversion:5','/warn:4','/warnaserror+','/target:library','/define:UNITY_6000_0_OR_NEWER','/out:"'+str(artifact)+'"']
    lines+=['"'+str(run/'snapshot'/s)+'"' for s in a.sources]
    lines+=['/reference:"'+str(p)+'"' for p in refs+ext]
    rsp=dest/'compile.rsp';rsp.write_text('\n'.join(lines),encoding='utf-8')
    cmd=[str(b.CSC),'/noconfig','@'+str(rsp)];p=subprocess.run(cmd,capture_output=True,timeout=40)
    (dest/'compiler.log').write_bytes(p.stdout+p.stderr);b.dump(dest/'command.json',cmd);native[name]=p.returncode
    b.dump(out/'results-progress.json',native)
native={}
for name in nodes:build(name,set())
b.dump(out/'results.json',native)
assert all(b.digest(Path(x['path']).read_bytes())==x['sha256'] for x in records)
print(json.dumps(native,indent=2))
