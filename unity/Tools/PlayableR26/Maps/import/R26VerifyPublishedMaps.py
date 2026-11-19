"""Validate the repository payloads and bindings after publication."""
from collections import Counter
from pathlib import Path
import hashlib
import json
import re
import subprocess

import R26ArchiveCandidate as c
from PIL import Image

root=c.REPO
tmp=c.TMP
sa=root/'Assets/StreamingAssets/PlayableR23'
spec=sa/'TeamLaunch/ArchiveRecovery/maps.json'
catalog=root/'Assets/Ashfall/Integration/Playable/World/Resources/R24ArchiveCatalog.json'
thumbs=root/'Assets/Ashfall/Integration/Playable/UI/Resources/R24MapThumbnails'
selected=c.read(tmp/'R26SelectedSlugs.json')
base='5ff2f87f'
def baseline(path):
    return json.loads(subprocess.check_output(['git','show',base+':'+str(path.relative_to(root))],cwd=root))
def index(data,key):
    assert len(set(row['id'] for row in data[key]))==len(data[key])
    return {r['id']:r for r in data[key]}
post=index(c.read(catalog),'maps')
pre=index(baseline(catalog),'maps')
legacy=index(c.read(sa/'Legacy/manifest.json'),'maps')
launch_file=sa/'TeamLaunch/launch-maps.json'
launch=index(c.read(launch_file),'rows')
recovery=index(c.read(spec),'rows')
for file, key, expected in [(launch_file,'rows',launch),(spec,'rows',recovery),(catalog,'maps',post)]:
    rows=index(baseline(file),key)
    for mid,v in rows.items():
        if mid not in {'archive:'+s for s in selected}:
            assert expected[mid]==v,(file,mid)
assert set(post)-set(pre)=={'archive:'+s for s in selected if s!='cold-hearted-gods-pitdevil'}
assert len(post)<=105
assert not (set(post)&set(legacy))
code=(root/'Assets/Ashfall/Integration/Playable/Match/R24ArchiveRecoveryCatalog.cs').read_text()
assert f'ExpectedSha256="{c.sha(spec)}"' in code
assert f'data.rows.Length!={len(recovery)}' in code
assert 'archive:cold-hearted-gods-pitdevil' not in (root/'Assets/Ashfall/Integration/Playable/World/R23MapCatalog.cs').read_text()
def map_rows(file):
    data=c.read(file)['maps']
    assert len({r['mapId'] for r in data})==len(data)
    return {r['mapId']:r for r in data}
cache=map_rows(sa/'Loot/caches.json')
pads=map_rows(sa/'JumpPads/pads.json')
stations=map_rows(sa/'HealthStations/stations.json')
loot_code=root/'Assets/Ashfall/Integration/Playable/World/Loot/R23WorldLoot.cs'
assert f'data.maps.Length!={len(cache)}' in loot_code.read_text()
policy=index(c.read(catalog.with_name('R26MapPodPolicy.json')),'maps')
peer_audit=root.parent/'campaign/deliverables/r18-maps2/imports'
sites=[]
for slug in selected:
    id='archive:'+slug
    live=sa/post[id]['relativeFolder']
    stage=c.STAGE/slug
    assert post[id]['creator']==c.INDEX[slug]['creator']
    assert post[id]['title']==c.read(live/'map.json')['title']
    old_notice=stage/'SOURCE-LICENSE.txt'
    new_notice=live/'SOURCE-LICENSE.txt'
    assert f'Creator: {post[id]["creator"]}' in new_notice.read_text()
    assert c.ARCHIVE in new_notice.read_text()
    for n in stage.iterdir():
        if not n.is_file() or n==old_notice:
            continue
        assert c.sha(live/n.name)==c.sha(n), f'Published bytes differ: {live/n.name}'
        assert (live/(n.name+'.meta')).is_file()
    thumbnail=thumbs/(slug+'.jpg')
    with Image.open(thumbnail) as im:
        im.load()
        assert im.width>=200 and im.height>=200
    verified=c.read(c.STAGE/(slug+'-audit.json'))
    for row in [recovery[id],launch[id]]:
        assert row['manifestSha256']==c.sha(live/'map.json')
        assert row['cacheFingerprint']==c.read(live/'provenance.json')['cacheFingerprint']
        assert row['sourceSha256']==verified['sourceSha256']
    combined=cache[id]
    assert all(t[id]['sourceSha256']==verified['sourceSha256'] for t in (cache,pads,stations))
    assert combined['cacheFingerprint']==stations[id]['cacheFingerprint']==recovery[id]['cacheFingerprint']
    assert not pads[id]['pads'] and not stations[id]['stations']
    assert [site['position'] for site in combined['sites']]==[[p['x'],p['y'],-p['z']] for p in recovery[id]['deathmatch']]
    peer=c.read(peer_audit/(slug+'.json'))
    assert peer['geometry']==c.sha(live/'geometry.bytes')
    def independently_clear(field,xyz):
        return any(state=='clear' and max(abs(a-b) for a,b in zip(xyz,row))<=.0001
                   for row,state,_ in peer['groups'][field])
    for phases in ('recorded','deathmatch','warmup'):
        assert all(independently_clear(phases,[r['x'],r['y'],-r['z']]) for r in recovery[id][phases])
    if id in policy:
        if slug in ('banana-land-gods-pitdevil','cold-hearted-gods-pitdevil'):
            assert policy[id]['verdict']=='OPEN' and policy[id]['pods']
        else:
            assert policy[id]['verdict']=='BOXED' and not policy[id]['pods']
            assert all(independently_clear('deathmatch',[r['x'],r['y'],-r['z']]) for r in policy[id]['spawns'])
        assert policy[id]['sourceSha256']==verified['sourceSha256']
    n_match=len(recovery[id]['deathmatch'])
    n_warmup=len(recovery[id]['warmup'])
    assert n_match==verified['matchSites'] and n_warmup==verified['warmupSites']
    map_data=c.read(live/'map.json')
    for field,sites_key in [('gameSpawn','deathmatch'),('warmupSpawn','warmup')]:
        point=recovery[id][sites_key][0]
        assert map_data[field]==[point['x'],point['y'],-point['z']]
        assert independently_clear(field,map_data[field])
    sites.append([n_match,n_warmup])

root_licenses=['LICENSE','LICENSE-ASSETS.md','THIRD-PARTY-NOTICES.md']
assert all((root/p).is_file() for p in root_licenses)
assert 'Copyright (c) 2026 the ASHFALL authors' in (root/'LICENSE').read_text()
assert 'CC0 1.0 Universal' in (root/'LICENSE-ASSETS.md').read_text()
package_table=(root/'THIRD-PARTY-NOTICES.md').read_text()
for name,row in c.read(root/'Packages/packages-lock.json')['dependencies'].items():
    if not name.startswith('com.unity.modules.'):
        assert f"| `{name}` | {row['version']} |" in package_table

maintain=[
'Assets/Ashfall/Integration/Playable/Rendering/Resources/GODOT-LICENSE.txt',
'Assets/Ashfall/Integration/Playable/UI/Resources/R23GodotUI-LICENSE.txt',
'Assets/Ashfall/Integration/Playable/UI/Resources/R23InventoryUI-LICENSE.txt',
'Assets/StreamingAssets/PlayableR23/HandsWeapons/LICENSE.txt',
'Assets/StreamingAssets/PlayableR23/TraversalAudio/LICENSE.txt',
'Assets/StreamingAssets/PlayableR23/Construction/Audio/LICENSE',
'Assets/StreamingAssets/PlayableR23/MatchZone/LICENSE.txt',
'Packages/packages-lock.json',
'Packages/manifest.json',
]
for f in maintain:
    assert (root/f).read_bytes()==subprocess.check_output(['git','show',base+':'+f],cwd=root),f
r=subprocess.run(['git','grep','-I','-l','-i','-F','ASHFALL Apache-2.0'],cwd=root,capture_output=True,text=True)
assert r.returncode==1,r.stdout
all_notices=list((sa/'Maps').glob('*/SOURCE-LICENSE.txt'))+list((sa/'Legacy/Maps').glob('*/SOURCE-LICENSE.txt'))+[sa/'World/SOURCE-LICENSE.txt']
for notice in all_notices:
    text=notice.read_text()
    assert not re.search(r'LOCAL ONLY|Excluded from .+redistribution',text,re.I),notice
    assert re.search(r'^Source: https://',text,re.M) and re.search(r'^Creator: \S',text,re.M),notice
    try:
        original=subprocess.check_output(['git','show',base+':'+str(notice.relative_to(root))],cwd=root,stderr=subprocess.DEVNULL).decode('utf-8')
    except subprocess.CalledProcessError:
        original=''
    if original:
        for key in ('Source','Creator'):
            assert re.search(f'^{key}: .+?\r?$',original,re.M).group().strip() in text, notice
changes=set((tmp/'license-file-list.txt').read_text().splitlines())|{x['path'] for x in c.read(tmp/'R26PublishedMapFiles.json')}
claims={line.rsplit(' ',1)[0]:line.rsplit(' ',1)[1] for line in (root.parent/'campaign/claims.txt').read_text().splitlines() if line}
for path in changes:
    assert claims[path]=='MAPSITE',path
diff=subprocess.run(['git','-c','core.whitespace=cr-at-eol','diff','--check','--',*sorted(changes)],cwd=root,capture_output=True,text=True)
assert diff.returncode==0,(diff.stdout+diff.stderr)[:3000]
repo_doc=dict(status='PASS',maps=len(selected),catalog=len(post),recovery=len(recovery),launch=len(launch),
              sourceNotices=len(all_notices),ownedFiles=len(changes),cacheRows=len(cache),
              minimumMatchSites=min(x[0] for x in sites),minimumWarmupSites=min(x[1] for x in sites),
              combinedMatchSites=sum(x[0] for x in sites),combinedWarmupSites=sum(x[1] for x in sites))
c.write(tmp/'R26MapsiteRepositoryVerification.json',repo_doc)
(tmp/'R26AllChangedFiles.txt').write_text(''.join(f'{root/p}\n' for p in sorted(changes)))
print(json.dumps(repo_doc,indent=2))
