"""Repair enumeration omissions in legacy graph using only already sealed bytes."""
import sys
import current_owner as c
run=c.BASE/sys.argv[1]
if (run/'legacy-inferred-plan.json').exists():raise ValueError('declaration already sealed; do not rewrite tested plan')
p=c.load(run/'plan.json');c.dump(run/'legacy-inferred-plan.json',p)
sources=[r['path'] for r in c.load(run/'capture.json')['files'] if r['path'].startswith('unity/Assets/Ashfall/RecoveryContracts/') and r['path'].endswith('.cs')]
p['units']['Ashfall.RecoveryContracts']={'sources':sources,'projectReferences':['Ashfall.Core'],'packageReferences':[],'kind':'neutral','directory':'unity/Assets/Ashfall/RecoveryContracts','existingAsmdef':None,'edgeBasis':'explicit root RECOVERY-ASSEMBLY-BOUNDARY-v1','extraPublicEdges':[]}
b=c.tools(run)
for name,a in p['units'].items():
    text='\n'.join(b.code((run/'snapshot'/s).read_text(encoding='utf-8-sig')) for s in a['sources'])
    if 'Ashfall.RecoveryContracts' in text and name!='Ashfall.RecoveryContracts':a['projectReferences']=sorted(set(a['projectReferences']+['Ashfall.RecoveryContracts']))
    if 'UnityEngine' in text and a['kind']=='neutral':a['kind']='engine'
# Explicit promotion, recorded in plan, never implicit on a compiler command.
for name,a in p['units'].items():
    found=set()
    def visit(n):
        for d in p['units'][n]['projectReferences']:
            if d!=name and d not in found:found.add(d);visit(d)
    visit(name)
    a['extraPublicEdges']=sorted(found-set(a['projectReferences']))
    a['projectReferences']=sorted(found)
c.membership(c.load(run/'capture.json'),p);c.dump(run/'plan.json',p)
# Preserve old proposal as historical. Corrected proposal includes source/header/
# meta identity for every explicit unit; no GUID invented and no apply operation.
proposal=c.load(run/'proposal.json');c.dump(run/'legacy-proposal.json',proposal)
ops=[]
for name,a in p['units'].items():
    target=a['existingAsmdef'] or a['directory']+'/'+name+'.asmdef'
    payload=run/'proposal-explicit'/target
    c.dump(payload,{'name':name,'references':a['projectReferences']+a['packageReferences'],'noEngineReferences':a['kind']=='neutral','includePlatforms':['Editor'] if a['kind']=='editor' else [],'autoReferenced':True})
    old=run/'snapshot'/target;meta=run/'snapshot'/(target+'.meta')
    ops.append({'target':target,'payload':str(payload.relative_to(run)),'sha256':c.sha(payload.read_bytes()),'before':c.sha(old.read_bytes()) if old.exists() else None,'meta':c.sha(meta.read_bytes()) if meta.exists() else None,'sources':[{'path':s,'sha256':c.sha((run/'snapshot'/s).read_bytes()),'headerSha256':c.sha((run/'snapshot'/s).read_bytes().split(b'\n',1)[0]),'metaSha256':c.sha((run/'snapshot'/(s+'.meta')).read_bytes()) if (run/'snapshot'/(s+'.meta')).exists() else None} for s in a['sources']],'preconditions':['external capture+plan+proposal pins','source and metadata exact equality','root allocates missing GUID/meta','cycles/API gates resolved; independent review'],'rollback':{'ifCurrentPayloadSha256':c.sha(payload.read_bytes()),'restoreFrom':str(old.relative_to(run)) if old.exists() else None,'otherwise':'remove newly created asmdef only; root removes only hash-matching allocated metadata'}})
c.dump(run/'proposal.json',{'applyImplemented':False,'operations':ops,'missingSourceMeta':proposal['missingSourceMeta']})
seal={'capture':c.sha((run/'capture.json').read_bytes()),'plan':c.sha((run/'plan.json').read_bytes()),'proposal':c.sha((run/'proposal.json').read_bytes())}
c.dump(run/'seal.json',seal);print(seal)
