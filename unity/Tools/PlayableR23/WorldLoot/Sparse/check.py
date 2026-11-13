from pathlib import Path
import hashlib,json,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];B=ROOT/'unity/Assets/Ashfall'
frozen=Path('D:/ashfall-r6-staging/frozen-source-r6-v001');manifest=(frozen/'SOURCE-MANIFEST.json').read_bytes();assert hashlib.sha256(manifest).hexdigest()=='b62578d419897f741f119eec885d199dc966824a21452bc60326d54a23e01111';pins={r['path']:r['sha256'] for r in json.loads(manifest)['files']}
for path in ['scripts/sparse_loot_r18.gd','scripts/archive_gameplay_r8.gd','scripts/archive_map1003.gd']:
 assert hashlib.sha256((frozen/'source'/path).read_bytes()).hexdigest()==pins[path]
files=[B/'Core/Contracts.cs',B/'Integration/Playable/World/Loot/R23SparseLoot.cs',HERE/'Cases.cs']
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll','/out:'+str(HERE/'Cases.exe')]+[str(p) for p in files];(HERE/'cases.rsp').write_text('\n'.join('"'+p+'"' for p in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(HERE/'cases.rsp')],capture_output=True,text=True);(HERE/'compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
r=subprocess.run([str(HERE/'Cases.exe')],capture_output=True,text=True);(HERE/'cases.log').write_text(r.stdout+r.stderr);print(r.stdout+r.stderr);assert r.returncode==0
runtime=(B/'Integration/Playable/World/Loot/R23WorldLoot.cs').read_text();bound=runtime.split('IEnumerator PrepareArchiveSites(',1)[1].split('void BuildFallbackGrid(',1)[0]
for exact in ['sourceWorld.ArchiveWorldFloor','sourceWorld.ArchiveWorldOutside','sourceWorld.ArchiveWorldHazard','sourceWorld.ArchiveWorldSafeSpawn','if(accepted.Count==0)','i+=3','accepted.Add(loot[i])','R23SparseLoot.AddSites','TryArchiveSourceToWorld']:
 assert exact in bound,exact
assert bound.index('R23SparseLoot.AddSites')>bound.index('if(accepted.Count==0)') and 'BuildFallbackGrid(' not in bound
print('3 frozen R6 source pins + exact shared-query/fallback-before-sparse/single-frame binding guards PASS.')
