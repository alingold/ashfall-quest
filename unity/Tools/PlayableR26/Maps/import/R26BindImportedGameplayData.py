from pathlib import Path
import json
import os

import R26ArchiveCandidate as c

root=c.REPO
tmp=c.TMP
sa=Path('Assets/StreamingAssets/PlayableR23')
caches=sa/'Loot/caches.json'
pads=sa/'JumpPads/pads.json'
stations=sa/'HealthStations/stations.json'
racks=Path('Assets/Ashfall/Integration/Playable/World/Loot/R24MountedWeaponRacks.cs')
selected=c.read(tmp/'R26SelectedSlugs.json')
changed={}
for path in (caches,pads,stations):
    target=root/path
    old=target.read_bytes()
    newline=b'\r\n' if old.endswith(b'\r\n') else b'\n'
    data=json.loads(old)
    rows={r['mapId']:r for r in data['maps']}
    assert len(rows)==len(data['maps'])
    for slug in selected:
        source=c.read(c.STAGE/(slug+'-recovery.json'))
        mid=source['id']
        row=dict(mapId=mid,sourceSha256=source['sourceSha256'])
        if path!=pads:
            row['cacheFingerprint']=source['cacheFingerprint']
        if path==pads:
            row['pads']=[]
        elif path==stations:
            row['stations']=[]
        else:
            row.update(preparedSha256='',sites=[])
            def reflect(p):return [p['x'],p['y'],-p['z']]
            for label in ('bounds','solid'):
                lo=reflect(source[label+'Min'])
                hi=reflect(source[label+'Max'])
                lo[2],hi[2]=hi[2],lo[2]
                row[label+'Min']=lo
                row[label+'Max']=hi
        rows[mid]=row
    data['maps']=list(rows.values())
    compact=old.count(b'\n')<=1
    if compact:
        rendered=json.dumps(data,ensure_ascii=False,separators=(',',':'),allow_nan=False)
    else:
        rendered=json.dumps(data,ensure_ascii=False,indent=2,allow_nan=False)
    changed[path]=rendered.encode().replace(b'\n',newline)+newline
old=(root/racks).read_bytes()
prev=b'{Failure="Source rack/map binding differs: "+mapId;return;}'
next=b'{LastRefusal="Source rack/map binding differs: "+mapId;row=null;return;}'
assert old.count(prev)==1
changed[racks]=old.replace(prev,next)
claims=root.parent/'campaign/claims.txt'
owners=dict(line.rsplit(' ',1) for line in claims.read_text().splitlines() if line)
assert all(owners.get(p.as_posix(),'MAPSITE')=='MAPSITE' for p in changed)
with claims.open('a') as f:
    for p in changed:
        if p.as_posix() not in owners:
            f.write(p.as_posix()+' MAPSITE\n')
sizes=c.read(tmp/'R26PublishedMapFiles.json')
for path,body in changed.items():
    copy=tmp/'publish'/path
    copy.parent.mkdir(exist_ok=True,parents=True)
    copy.write_bytes(body)
    dst=root/path
    before=dst.stat().st_size
    os.replace(copy,dst)
    sizes.append(dict(path=path.as_posix(),before=before,after=len(body),change=len(body)-before))
c.write(tmp/'R26PublishedMapFiles.json',sizes)
print('Loot cache, jump pad and station entries bound to all new sources. Rack rejection scoped to the mismatched map.')
