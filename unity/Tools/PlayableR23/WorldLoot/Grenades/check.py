from pathlib import Path
import hashlib,json,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];B=ROOT/'unity/Assets/Ashfall'
# Existing independent Python PCG/djb2 implementation, before any output/native work.
oracle={};old=HERE.parent/'check.py';oracle['__file__']=str(old)
exec(compile(old.read_text().split('maps=',1)[0],'independent-cache-oracle','exec'),oracle)
maps=['archive:alfa-aglae-souza','archive:classic-bigbox-vr','range','archive:magnum-chaosvr','archive:magnum-3-chaosvr']
lines=[]
for map_id in maps:
 for site in range(128):
  rows=oracle['cache'](map_id,site,map_id in maps[-2:]);lines.append(map_id+'|'+str(site)+'|'+','.join(str(x) for pair in rows for x in pair))
(HERE/'vectors.txt').write_text('\n'.join(lines)+'\n')
files=[B/'Core/Contracts.cs',B/'Integration/Playable/Activities/R23BuildLightningRecipe.cs',B/'Integration/Playable/World/Loot/R23LootRecipe.cs',B/'Integration/Playable/World/R24ArchiveMapPolicy.cs',B/'Integration/Playable/Explosives/R23GrenadeState.cs',B/'Integration/Playable/Explosives/R23GrenadeMechanics.cs']
# Real grenade clock calls the production ballistic owner; retain its actual closure.
files+=list(set(ROOT/p for p in(ROOT/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()))
files+=list((B/'Integration/Playable/HandsWeapons').glob('*.cs'))
files=sorted(set(files))+[HERE/'Cases.cs'];args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(HERE/'Cases.exe')]+[str(p) for p in files]
(HERE/'cases.rsp').write_text('\n'.join('"'+p+'"' for p in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(HERE/'cases.rsp')],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'Cases.exe'),str(HERE/'vectors.txt')],capture_output=True,text=True);(HERE/'cases.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
# Retain the shipped finite/default grenade flows over the same actual owners.
retained=[str(p)for p in files if p!=HERE/'Cases.cs']+[str(ROOT/'unity/Tools/PlayableR23/Explosives/Cases.cs')]
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(HERE/'Retained.exe')]+retained
(HERE/'retained.rsp').write_text('\n'.join('"'+p+'"' for p in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(HERE/'retained.rsp')],capture_output=True,text=True);(HERE/'retained-compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'Retained.exe')],capture_output=True,text=True);(HERE/'retained.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
frozen=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');raw=(frozen/'SOURCE-MANIFEST.json').read_bytes();assert hashlib.sha256(raw).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';pins={x['path']:x['sha256']for x in json.loads(raw)['files']}
scripts={}
for name in ['archive_gameplay_r8','archive_map1003','weapon_inventory','weapon_rack','weapon_rarity_r6','explosive_weapon','explosive_projectile','net_bridge1000']:
 p='scripts/'+name+'.gd';value=(frozen/'source'/p).read_bytes();assert hashlib.sha256(value).hexdigest()==pins[p];scripts[name]=value.decode('utf-8-sig')
assert 'catalog not in [14,15,16,26,28,29]' in scripts['weapon_rarity_r6']
assert 'if kind=="smaw":tuning.rocket_damage=' in scripts['explosive_projectile']
assert 'var catalog:int=pool[random.randi_range(0,pool.size()-1)]' in scripts['archive_gameplay_r8']
print('8 frozen source hashes/policy + exact non-tiered grenade damage distinction PASS; native/renderer not run.')
# The older cache export lacks both Magnum maps. Bind the small production
# fallback rows to the already source-pinned metadata, never Unity render bounds.
import re,struct
sem_path='assets/data/map_semantics_r14.json';sem_raw=(frozen/'source'/sem_path).read_bytes();assert hashlib.sha256(sem_raw).hexdigest()==pins[sem_path];sem=json.loads(sem_raw)['maps']
launch=json.loads((ROOT/'unity/Assets/StreamingAssets/PlayableR23/TeamLaunch/launch-maps.json').read_text())
archive=Path('C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive')
live=(B/'Integration/Playable/World/Loot/R23WorldLoot.cs').read_text()
def f32(x):return struct.pack('<f',x)
for slug in ['magnum-chaosvr','magnum-3-chaosvr']:
 p=archive/'maps'/slug/'converted/manifest.json';raw=p.read_bytes();m=json.loads(raw);row=next(x for x in launch['rows'] if x['id']=='archive:'+slug)
 assert hashlib.sha256(raw).hexdigest()==row['manifestSha256'];assert m['source_sha256']==sem[slug]['source_sha256']==row['sourceSha256'];assert m['cache_fingerprint']==sem[slug]['cache_fingerprint']==row['cacheFingerprint']
 payload=json.loads((ROOT/'unity/Assets/StreamingAssets/PlayableR23'/row['payloadFolder']/'map.json').read_text());assert payload['sourceSha256']==m['source_sha256'];assert row['sourceOrigin']==row['removedOrigin']==[0,0,4800]
 block=live.split('if(id=="archive:'+slug+'")return new MapRow{',1)[1].split('};',1)[0]
 for field,key,opposite in [('boundsMin','bounds_min','bounds_max'),('boundsMax','bounds_max','bounds_min'),('solidMin','solid_min','solid_max'),('solidMax','solid_max','solid_min')]:
  actual=[float(x.strip().removesuffix('f')) for x in re.search(field+r'=new\[\]\{([^}]+)',block).group(1).split(',')];expect=[m[key][0],m[key][1],-m[opposite][2]];assert [f32(x)for x in actual]==[f32(x)for x in expect],(slug,field)
 assert m['source_sha256'] in block and m['cache_fingerprint'] in block
assert 'if(row==null)row=MagnumFallbackRow(currentMap)' in live and 'c.gameObject.layer==0' in live
print('2 exact source-manifest/semantics/payload Magnum grid-bound rows +single origin reflection PASS; native floor validation remains pending.')
