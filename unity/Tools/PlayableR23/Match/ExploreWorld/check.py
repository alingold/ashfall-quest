from pathlib import Path
import hashlib,json,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];A=ROOT/'unity/Assets/Ashfall';STAGE=HERE/'Staged';S=Path('D:/ashfall-r6-staging/frozen-source-r6-v001/source')
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest();manifest=S.parent/'SOURCE-MANIFEST.json';assert sha(manifest)=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';pins={r['path']:r['sha256']for r in json.loads(manifest.read_text())['files']}
for p in ['scripts/map_semantics_r14.gd','scripts/archive_map1003.gd']:assert sha(S/p)==pins[p]
source=(S/'scripts/map_semantics_r14.gd').read_text();assert 'absf(delta.y)<=4.0 and Vector2(delta.x,delta.z).length_squared()<=8.0*8.0'in source
root=(STAGE/'R23PlayableLoop.ArchiveGhostRecovery.cs').read_text()
assert 'ActualMatchRules==null&&archiveSpawnPlan!=null&&archiveSpawnPlan.ContainsWarmup(point)'in root
assert 'while(ArchiveWorldContextCurrent&&ArchiveWorldGeneration==generation'in root and 'while(MatchActive'not in root
for name in ['ArchiveWorldReady','ArchiveWorldGeneration','ArchiveWorldMapId','ArchiveWorldFailure','ArchiveWorldPlan','ArchiveWorldInfo','PrepareArchiveWorld(bool actualWorldActive)','TryArchiveSourceToWorld','TryArchiveWorldToSource','ArchiveWorldFloor','ArchiveWorldRoom','ArchiveWorldOutside','ArchiveWorldHazard','ArchiveWorldSafeSpawn']:assert name in root,name
assert 'Map.WarmupSpawn'not in root
for banned in ['ResetRig(','.Spawn(','.ResetRound(','.ConfigureLocalMatchHealth(','transform.position=']:assert banned not in root,banned
files=[A/'Core/Contracts.cs',A/'Integration/Playable/World/R24ArchiveMapPolicy.cs',STAGE/'Match/R24ArchiveRecoveryState.cs',STAGE/'Match/R24ArchiveWorldContext.cs',HERE/'Cases.cs'];exe=HERE/'Cases.exe'
p=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warn:4','/warnaserror+','/out:'+str(exe)]+[str(p)for p in files],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
p=subprocess.run([str(exe)],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
# Retain the same actual105-map bounds/launch/Magnum and planner branch cases,
# compiling them against this changed shared planner, not the old published one.
prior=ROOT/'unity/Tools/PlayableR23/Match/Ghost/Archive/ArchiveCases.cs';exe=HERE/'RetainedArchiveCases.exe'
p=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warn:4','/warnaserror+','/out:'+str(exe)]+[str(x)for x in files[:-2]]+[str(prior)],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
p=subprocess.run([str(exe)],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
(HERE/'checks.json').write_text(json.dumps(dict(newCasesOutput=subprocess.run([str(HERE/'Cases.exe')],capture_output=True,text=True,check=True).stdout.strip(),retainedArchiveOutput=p.stdout.strip(),sourceInputs={k:pins[k]for k in ['scripts/map_semantics_r14.gd','scripts/archive_map1003.gd']},nativeExecuted=False),indent=2)+'\n')
