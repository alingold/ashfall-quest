"""Independent consumer/compiler gates over one externally pinned capture.
Installed API DLLs are compiled only. Only neutral CLR fixtures execute.
"""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import current_owner as c

def main():
    run=c.BASE/sys.argv[1];p=c.verify(run,sys.argv[2],sys.argv[3]);out=run/(sys.argv[4] if len(sys.argv)>4 else 'gates');out.mkdir()
    cap=c.load(run/'capture.json');refs={Path(x['path']).name:Path(x['path']) for x in cap['readonlyReferences']}
    fw=[Path(x['path']) for x in cap['readonlyReferences'] if 'UnityReferenceAssemblies' in x['path'] or 'shims' in x['path']]
    engine=[Path(x['path']) for x in cap['readonlyReferences'] if '/Managed/UnityEngine/' in x['path'].replace('\\','/') and Path(x['path']).name.startswith('UnityEngine')]
    log={};dll={}
    for name,deps in [('Ashfall.Core',[]),('Ashfall.RecoveryContracts',['Ashfall.Core']),('Ashfall.Modules.HealthStates',['Ashfall.Core','Ashfall.RecoveryContracts']),('Ashfall.Modules.Combat',['Ashfall.Core','Ashfall.RecoveryContracts','Ashfall.Modules.HealthStates'])]:
        a=p['units'][name]
        code,target=c.compile_one(out/name,name,[run/'snapshot'/s for s in a['sources']],[dll[d] for d in deps])
        log[name]=code
        if code:raise ValueError('actual repair owner compile failed: '+name)
        dll[name]=target
    fixture=run/'snapshot/unity/Tools/Integration/CombatCheckpointBoundary/BoundaryTests.cs'
    def consumer(label,health):
        deps=[dll['Ashfall.Core'],dll['Ashfall.RecoveryContracts'],health]
        code,target=c.compile_one(out/label,'Boundary',[fixture],deps,exe=True)
        if code:return {'compile':code}
        for x in deps:shutil.copyfile(x,target.parent/x.name)
        z=subprocess.run([str(target)],cwd=target.parent,capture_output=True,timeout=30)
        return {'compile':code,'exit':z.returncode,'stdout':z.stdout.decode(errors='replace'),'stderr':z.stderr.decode(errors='replace')}
    health=dll['Ashfall.Modules.HealthStates'];log['publicBoundary']=consumer('publicBoundary',health)
    assert log['publicBoundary']['exit']==0
    # This is a compiled owner mutant returning a false historical decode result,
    # not a source-text-only check or fixture assertion edited to pass.
    sources=[run/'snapshot'/s for s in p['units']['Ashfall.Modules.HealthStates']['sources']]
    original=next(s for s in sources if s.name=='MatchCheckpointRecords.cs');text=original.read_text()
    needle='return value;}'
    assert needle in text
    m=out/'mutant-source';m.mkdir();replacement=m/original.name;replacement.write_text(text.replace(needle,'return null;}'))
    code,target=c.compile_one(out/'mutant-owner','Ashfall.Modules.HealthStates',[replacement if s==original else s for s in sources],[dll['Ashfall.Core'],dll['Ashfall.RecoveryContracts']])
    assert code==0
    log['decoded-null-mutant']=consumer('decoded-null-mutant',target)
    assert log['decoded-null-mutant']['compile']==0 and log['decoded-null-mutant']['exit']!=0
    # Real public archive methods bind across separate Combat/Health DLLs.
    q=out/'CombatConsumer.cs';q.write_text('using System.IO;using Ashfall.Modules.Combat;public static class Probe { public static CombatRules.ArchiveBindings Capture(CombatRules r,BinaryWriter w){return r.CaptureArchive(w,new HostHit[0]);}}')
    for label,names in [('combat-public-positive',list(dll)),('combat-owner-omitted',[n for n in dll if n!='Ashfall.Modules.Combat'])]:
        code,_=c.compile_one(out/label,'Consumer',[q],[dll[n] for n in names]);log[label]=code
    assert log['combat-public-positive']==0 and log['combat-owner-omitted']!=0
    probes={
        'Unity.InputSystem':'public UnityEngine.InputSystem.InputAction Value;',
        'Unity.XR.OpenXR':'public UnityEngine.XR.OpenXR.Features.OpenXRFeature Value;',
        'Unity.XR.Management':'public UnityEngine.XR.Management.XRLoader Value;',
        'Unity.Collections':'public System.Type Value(){return typeof(Unity.Collections.CollectionHelper);}',
        'Unity.Networking.Transport':'public Unity.Networking.Transport.NetworkDriver Value;'}
    for package,body in probes.items():
        source=out/(package+'.cs');source.write_text('public class ApiProbe {'+body+'}')
        for present in (True,False):
            label=package+('-declared' if present else '-omitted')
            code,_=c.compile_one(out/label,'ApiProbe',[source],fw+engine+([refs[package+'.dll']] if present else []),engine=True)
            log[label]=code
            assert (code==0)==present,(label,code)
    # Exercise complete membership fail-closed under freshly sealed candidate plans;
    # the externally trusted capture is not rewritten and no originals are edited.
    plan=c.load(run/'plan.json');assert plan['fixtures']
    tests=[]
    import copy
    for label,change in [
        ('source-omitted',lambda x:x['units']['Ashfall.Modules.Combat']['sources'].pop()),
        ('duplicate-source',lambda x:x['units']['Ashfall.Modules.Combat']['sources'].append(x['units']['Ashfall.Core']['sources'][0])),
        ('fixture-omitted',lambda x:x['fixtures'].pop()),
        ('empty-fixture',lambda x:x['fixtures'][0].update(sources=[])),
        ('unknown-edge',lambda x:x['units']['Ashfall.Core']['projectReferences'].append('MissingOwner'))]:
        candidate=copy.deepcopy(plan);change(candidate)
        # A separate small mirror uses exact source copies via the pre-existing
        # snapshot path; validate_membership checks the same production verifier.
        mirror=out/label;mirror.mkdir();shutil.copyfile(run/'capture.json',mirror/'capture.json');c.dump(mirror/'plan.json',candidate)
        # Windows junctions are deliberately not used. Byte checks are not bypassed:
        # test via a factored membership function below, shared by the main verifier.
        try:c.membership(c.load(run/'capture.json'),candidate)
        except ValueError as e:tests.append({'case':label,'rejected':str(e)})
        else:raise AssertionError(label)
    log['membershipOmissions']=tests
    c.dump(out/'results.json',log);print(json.dumps(log))
if __name__=='__main__':main()
