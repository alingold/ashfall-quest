"""Additional actual-source mutant and installed public-inheritance edge gates."""
import sys
import shutil
import subprocess
from pathlib import Path
import current_owner as c
run=c.BASE/sys.argv[1];plan=c.verify(run,sys.argv[2],sys.argv[3]);out=run/'extra-gates';out.mkdir()
cap=c.load(run/'capture.json');refs={Path(r['path']).name:Path(r['path']) for r in cap['readonlyReferences']}
fw=[Path(r['path']) for r in cap['readonlyReferences'] if 'UnityReferenceAssemblies' in r['path'] or 'shims' in r['path']]
engine=[Path(r['path']) for r in cap['readonlyReferences'] if '/Managed/UnityEngine/' in r['path'].replace('\\','/') and Path(r['path']).name.startswith('UnityEngine')]
def dll(n):return run/'validation02/cpu'/n/(n+'.dll')
sources=[run/'snapshot'/s for s in plan['units']['Ashfall.Modules.HealthStates']['sources']]
original=next(s for s in sources if s.name=='Ports.cs');text=original.read_text();needle='public sealed class HealthSnapshot\n    {';assert needle in text
replacement=out/'Ports.cs';replacement.write_text(text.replace(needle,needle+'\n        public readonly int FutureArchiveField=1;'))
code,health=c.compile_one(out/'owner','Ashfall.Modules.HealthStates',[replacement if s==original else s for s in sources],[dll('Ashfall.Core'),dll('Ashfall.RecoveryContracts')]);assert code==0
code,target=c.compile_one(out/'consumer','Boundary',[run/'snapshot/unity/Tools/Integration/CombatCheckpointBoundary/BoundaryTests.cs'],[dll('Ashfall.Core'),dll('Ashfall.RecoveryContracts'),health],exe=True);assert code==0
for p in [dll('Ashfall.Core'),dll('Ashfall.RecoveryContracts'),health]:shutil.copyfile(p,target.parent/p.name)
z=subprocess.run([str(target)],cwd=target.parent,capture_output=True,timeout=30);assert z.returncode!=0
log={'unknown-owner-field-mutant':{'compile':code,'exit':z.returncode,'stdout':z.stdout.decode(errors='replace'),'stderr':z.stderr.decode(errors='replace')}}
feature=run/'snapshot/unity/Assets/Ashfall/Modules/Player/Engine/PlayerOpenXrLifecycleFeature.cs'
for present in (True,False):
    label='actual-openxr-feature-'+('declared' if present else 'omitted')
    code,_=c.compile_one(out/label,'ActualFeature',[feature],fw+engine+[dll('Ashfall.Core'),dll('Ashfall.Modules.Player')]+([refs['Unity.XR.OpenXR.dll']] if present else []),engine=True)
    log[label]=code;assert (code==0)==present
# Metadata inheritance creates a real CS0012 dependency, beyond namespace scans.
source=out/'PublicLoader.cs';source.write_text('public sealed class PublicLoader : UnityEngine.XR.OpenXR.OpenXRLoader {}')
for present in (True,False):
    label='openxr-public-management-'+('declared' if present else 'omitted')
    code,_=c.compile_one(out/label,'PublicLoader',[source],fw+engine+[refs['Unity.XR.OpenXR.dll']]+([refs['Unity.XR.Management.dll']] if present else []),engine=True)
    log[label]=code;assert (code==0)==present
c.dump(out/'results.json',log);print(log)
