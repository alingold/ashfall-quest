from pathlib import Path
import hashlib
import json
import os

import R26ArchiveCandidate as c

root=c.REPO
tmp=c.TMP
audit=root.parent/'campaign/deliverables/r18-maps2/imports'
sa=Path('Assets/StreamingAssets/PlayableR23')
recovery_path=sa/'TeamLaunch/ArchiveRecovery/maps.json'
cache_path=sa/'Loot/caches.json'
recovery=c.read(root/recovery_path)
cache=c.read(root/cache_path)
rec_rows={r['id']:r for r in recovery['rows']}
cache_rows={r['mapId']:r for r in cache['maps']}
rejected={}
changed={}
for slug in ('meliworld-gods-meli','cold-hearted-gods-pitdevil'):
    mid='archive:'+slug
    report=c.read(audit/(slug+'.json'))
    rec=rec_rows[mid]
    published_dir=root/sa/'Maps'/slug
    assert c.sha(published_dir/'geometry.bytes')==report['geometry']
    stage_proof=c.STAGE/slug/'spawn-proof.json'
    proof=c.read(stage_proof)
    matches=[(p,why) for p,why,_ in report['groups']['deathmatch'] if why!='clear']
    warm=[(p,why) for p,why,_ in report['groups']['warmup'] if why!='clear']
    assert all(why.startswith('inside:') for _,why in matches+warm)
    assert (len(matches),len(warm))==((7,0) if slug.startswith('meliworld') else (0,1))
    def world(p):
        return [p['x'],p['y'],-p['z']]
    def kept(p,blocked):
        return not any(max(abs(a-b) for a,b in zip(p,q))<=.0001 for q,_ in blocked)
    for field,blocked in [('deathmatch',matches),('recorded',matches),('warmup',warm)]:
        old=rec[field]
        rec[field]=[p for p in old if kept(world(p),blocked)]
        assert len(old)-len(rec[field])==len(blocked)
    row=cache_rows[mid]
    old_sites=row['sites']
    row['sites']=[s for s in old_sites if kept(s['position'],matches)]
    assert len(old_sites)-len(row['sites'])==len(matches)
    q=proof['finalRuntimeValidation']
    for key,internal,field,block in [('matchSites','verifiedMatchSites','deathmatch',matches),
                                     ('warmupSites','verifiedWarmupSites','warmup',warm)]:
        current=[world(x) for x in rec[field]]
        q[key]=current
        proof[internal]=current
    proof['excludedEnclosedSites']=[dict(position=p,reason=why) for p,why in matches+warm]
    c.write(stage_proof,proof)
    c.write(c.STAGE/(slug+'-recovery.json'),rec)
    receipt_path=c.STAGE/(slug+'-audit.json')
    receipt=c.read(receipt_path)
    receipt.update(matchSites=len(rec['deathmatch']),warmupSites=len(rec['warmup']))
    c.write(receipt_path,receipt)
    rejected[slug]=proof['excludedEnclosedSites']
    changed[(published_dir/'spawn-proof.json').relative_to(root)]=stage_proof.read_bytes()
oldrec=(root/recovery_path).read_bytes()
for path,data in ((recovery_path,recovery),(cache_path,cache)):
    old=(root/path).read_bytes()
    nl=b'\r\n' if old.endswith(b'\r\n') else b'\n'
    changed[path]=json.dumps(data,ensure_ascii=False,separators=(',',':'),allow_nan=False).encode()+nl
code=Path('Assets/Ashfall/Integration/Playable/Match/R24ArchiveRecoveryCatalog.cs')
raw=(root/code).read_bytes()
old_hash=hashlib.sha256(oldrec).hexdigest().encode()
new_hash=hashlib.sha256(changed[recovery_path]).hexdigest().encode()
assert raw.count(old_hash)==1
changed[code]=raw.replace(old_hash,new_hash)
owners=dict(line.rsplit(' ',1) for line in (root.parent/'campaign/claims.txt').read_text().splitlines() if line)
assert all(owners[p.as_posix()]=='MAPSITE' for p in changed)
sizes=c.read(tmp/'R26PublishedMapFiles.json')
for path,body in changed.items():
    staged=tmp/'publish'/path
    staged.write_bytes(body)
    os.replace(staged,root/path)
    row=next(r for r in sizes if r['path']==path.as_posix())
    row.update(after=len(body),change=len(body)-row['before'])
c.write(tmp/'R26PublishedMapFiles.json',sizes)
rollup=c.read(tmp/'R26MapsiteAudit.json')
for i,r in enumerate(rollup):
    if r['slug'] in rejected:
        rollup[i].update(**c.read(c.STAGE/(r['slug']+'-audit.json')))
c.write(tmp/'R26MapsiteAudit.json',rollup)
c.write(tmp/'R26RemovedEnclosedSpawnSites.json',rejected)
print('Pruned',sum(map(len,rejected.values())),'enclosed spawn sites and matching cache seeds.')
