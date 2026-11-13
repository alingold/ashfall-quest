import sys,json,shutil
from pathlib import Path
sys.path.insert(0,str(Path('unity/Tools/BuildGraph').resolve()))
import build_graph as b
from compile_r7 import compile_graph
D=b.ROOT/'docs/agent-runs/20261104-three-chats/unity/BuildGraphR7'
seed=b.STAGE/'phase12';run=b.STAGE/'assembly-proposal02';run.mkdir();shutil.copytree(seed/'snapshot',run/'snapshot')
raw=json.loads((seed/'graph.json').read_bytes()); nodes={a['name']:a for a in raw['assemblies']}
a=nodes['Ashfall.Integration'];child=nodes['Ashfall.Integration.CombatPublication']
refs=sorted((set(a['references'])|set(child['references']))-{'Ashfall.Integration','Ashfall.Integration.CombatPublication'})
files={
'unity/Assets/Ashfall/Integration/Ashfall.Integration.asmdef':json.dumps({'name':a['name'],'references':refs,'noEngineReferences':True},sort_keys=True,indent=2)+'\n',
'unity/Assets/Ashfall/Integration/Ashfall.Integration.asmdef.meta':'fileFormatVersion: 2\nguid: '+a['guid']+'\n',
'unity/Assets/Ashfall/Integration/CombatPublication/CombatPublication.asmref':'{"reference":"Ashfall.Integration"}\n',
'unity/Assets/Ashfall/Modules/HealthStates/AssemblyVisibility.cs':'using System.Runtime.CompilerServices;\n[assembly: InternalsVisibleTo("Ashfall.Modules.Combat")]\n[assembly: InternalsVisibleTo("Ashfall.Integration")]\n',
'unity/Assets/Ashfall/Modules/Combat/AssemblyVisibility.cs':'using System.Runtime.CompilerServices;\n[assembly: InternalsVisibleTo("Ashfall.Integration")]\n'}
# Every inferred boundary must be concrete: a parent asmdef otherwise absorbs
# descendants lacking their own boundary. Preserve all original inferred splits.
for name,node in nodes.items():
    if name=='Ashfall.Integration.CombatPublication': continue
    references=refs if name=='Ashfall.Integration' else sorted(set('Ashfall.Integration' if r=='Ashfall.Integration.CombatPublication' else r for r in node['references']))
    references=[r for r in references if r!=name]
    path=node['directory']+'/'+name+'.asmdef'
    files[path]=json.dumps({'name':name,'references':references,'noEngineReferences':node['kind']=='neutral','includePlatforms':['Editor'] if node['kind']=='editor' else [],'excludePlatforms':[]},sort_keys=True,indent=2)+'\n'
    files[path+'.meta']='fileFormatVersion: 2\nguid: '+node['guid']+'\n'
ops=[]
for target,text in files.items():
    assert not (run/'snapshot'/target).exists()
    data=text.encode(); dest=run/'snapshot'/target;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    payload=run/'proposed'/target;payload.parent.mkdir(parents=True,exist_ok=True);payload.write_bytes(data)
    ops.append({'target':target,'expectedBefore':None,'payload':str(payload.relative_to(run)),'sha256':b.digest(data),'rollback':{'operation':'remove-if-hash','sha256':b.digest(data)}})
b.dump(run/'central-proposal.json',{'schema':'BuildGraph/central-assembly-proposal/1','authority':'ROOT ONLY, UNREVIEWED; not applied','preconditions':raw['inputs'],'operations':ops,'missingMeta':'Coordinator must allocate remaining new file meta GUIDs before import; no guessed GUIDs.'})
g,ids=b.generate(run/'snapshot');b.dump(run/'graph.json',b.asdict(g));b.dump(run/'validation.json',b.validate(g,ids))
print(b.validate(g,ids));compile_graph(run,g,'separate01')
