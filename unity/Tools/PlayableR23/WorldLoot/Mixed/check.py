from pathlib import Path
import subprocess,json
here=Path(__file__).resolve().parent;root=here.parents[4];b=root/'unity/Assets/Ashfall'
out=Path('D:/ashfall-r6-staging/mixed-cache-next/cpu');out.mkdir(parents=True,exist_ok=True)
files=[b/'Core/Contracts.cs']+list((b/'RecoveryContracts').glob('*.cs'))+list((b/'Modules/Pickups').glob('*.cs'))+[b/'Integration/Playable/Consumables/R23ConsumableState.cs',b/'Integration/Playable/Consumables/LocalPickupRelocation.cs',b/'Integration/Playable/Activities/R23BuildLightningRecipe.cs',b/'Integration/Playable/World/Loot/R23LootRecipe.cs',b/'Integration/Playable/World/Loot/R24MixedLootRecipe.cs',b/'Integration/Playable/World/R24ArchiveMapPolicy.cs',here/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(out/'Cases.exe')]+[str(p)for p in files]
rsp=out/'cases.rsp';rsp.write_text('\n'.join('"'+p+'"'for p in args))
run=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(out/'compile.log').write_text(run.stdout+run.stderr);assert run.returncode==0,run.stdout+run.stderr
run=subprocess.run([str(out/'Cases.exe')],capture_output=True,text=True);(out/'cases.log').write_text(run.stdout+run.stderr);print(run.stdout+run.stderr);assert run.returncode==0
(out/'result.json').write_text(json.dumps(dict(compilePassed=True,casesPassed=True,nativeExecuted=False,output=run.stdout),indent=2))
# Existing food effect/stack regressions over the changed actual state, D-only.
state_files=files[:files.index(b/'Integration/Playable/Activities/R23BuildLightningRecipe.cs')]+[b/'Integration/Playable/Consumables/R23BananaPeelGeometry.cs']
for name in ['Mechanics','SlotReplacementCases']:
    test=root/'unity/Tools/PlayableR23/Consumables'/(name+'.cs')
    args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(out/(name+'.exe'))]+[str(p)for p in state_files+[test]]
    rsp=out/(name+'.rsp');rsp.write_text('\n'.join('"'+p+'"'for p in args))
    compile_run=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(out/(name+'-compile.log')).write_text(compile_run.stdout+compile_run.stderr);assert compile_run.returncode==0,compile_run.stdout+compile_run.stderr
    run=subprocess.run([str(out/(name+'.exe'))],capture_output=True,text=True);(out/(name+'.log')).write_text(run.stdout+run.stderr);assert run.returncode==0,run.stdout+run.stderr
    print(name+' retained food/stack checks PASS')
