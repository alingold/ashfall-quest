from pathlib import Path
import subprocess,hashlib,json
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];B=ROOT/'unity/Assets/Ashfall';H=B/'Integration/Playable/HandsWeapons';W=B/'Integration/Playable/World/Loot'
files=set(ROOT/x for x in(ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines());files.update(H.glob('*.cs'));files.update([B/'Core/Contracts.cs',B/'Integration/Playable/Activities/R23BuildLightningRecipe.cs',B/'Integration/Playable/World/R24ArchiveMapPolicy.cs',W/'R23LootRecipe.cs',W/'R23BaselineLootRecipe.cs',HERE/'Cases.cs'])
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(HERE/'Cases.exe')]+[str(p)for p in sorted(files)];(HERE/'cases.rsp').write_text('\n'.join('"'+x+'"' for x in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(HERE/'cases.rsp')],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'Cases.exe')],capture_output=True,text=True);(HERE/'cases.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
frozen=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');raw=(frozen/'SOURCE-MANIFEST.json').read_bytes();assert hashlib.sha256(raw).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';pins={r['path']:r['sha256']for r in json.loads(raw)['files']}
for name in ['weapon_inventory','weapon_rack','archive_map1003']:
 p='scripts/'+name+'.gd';assert hashlib.sha256((frozen/'source'/p).read_bytes()).hexdigest()==pins[p]
managed=(H/'R23HandsWeapons.GroundComposition.cs').read_text().split('public void BindSourceGroundLayout(){',1)[1].split('public bool TryReadWorldWeaponSettle',1)[0]
assert 'foreach(var id in starterWorldAtHome)'in managed and 'droppedWeapons.Remove(id)'in managed and 'starterWorldAtHome.Clear()'in managed
for forbidden in ['round.','ownedWeapons.Clear','ownedWeapons.Remove','ResetRound','CreateWeaponInstance']:
 assert forbidden not in managed,forbidden
hands=(H/'R23HandsWeapons.cs').read_text();assert 'if(sourceGroundLayoutManaged||!sourceArtReady'in hands and 'holstered=sourceGroundLayoutManaged;starterPickup=!sourceGroundLayoutManaged;'in hands
world=(W/'R23WorldLoot.cs').read_text();assert 'weapons.BindSourceGroundLayout()'in world and 'SpawnBaselineArchive(loot);Activate(true)'in world
assert '!record.WallMounted' in (H/'R23HandsWeapons.GroundComposition.cs').read_text()
print('3 frozen source pins +legacy-only withdrawal/reset/mounted exclusion/cache separation source guards PASS; native/render not run.')
