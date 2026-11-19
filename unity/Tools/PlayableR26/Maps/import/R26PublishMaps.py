"""Publish only reviewed/audited candidates into the shared r18 checkout."""
from pathlib import Path
import hashlib
import json
import os
import re
import sys
import uuid

import R26ArchiveCandidate as c

ROOT=c.REPO
TMP=c.TMP
SA=Path('Assets/StreamingAssets/PlayableR23')
UI=Path('Assets/Ashfall/Integration/Playable/UI/Resources/R24MapThumbnails')
WORLD=Path('Assets/Ashfall/Integration/Playable/World')
CATALOG=WORLD/'Resources/R24ArchiveCatalog.json'
REC_CODE=Path('Assets/Ashfall/Integration/Playable/Match/R24ArchiveRecoveryCatalog.cs')
REC=SA/'TeamLaunch/ArchiveRecovery/maps.json'
LAUNCH=SA/'TeamLaunch/launch-maps.json'
LEGACY=SA/'Legacy/manifest.json'
EXCLUDED=WORLD/'R23MapCatalog.cs'
CLAIMS=TMP.parents[1]/'claims.txt'
FIXED={'banana-land-gods-pitdevil','g0dtham-gods-poplightning','cold-hearted-gods-pitdevil'}

def stable_meta(p, folder=False):
    guid=uuid.uuid5(uuid.NAMESPACE_URL,'ashfall-r18-mapsite/'+p.as_posix()).hex
    return ('fileFormatVersion: 2\nguid: '+guid+'\n'+('folderAsset: yes\n' if folder else '')).encode()

def read(p):
    return c.read(ROOT/p)

def flatten_json(path, value):
    old=(ROOT/path).read_bytes()
    newline=b'\r\n' if old.endswith(b'\r\n') else b'\n'
    return json.dumps(value,ensure_ascii=False,separators=(',',':'),allow_nan=False).encode()+newline

def pretty_json(path,value):
    old=(ROOT/path).read_bytes()
    newline='\r\n' if old.endswith(b'\r\n') else '\n'
    return (json.dumps(value,ensure_ascii=False,indent=2,allow_nan=False)+'\n').replace('\n',newline).encode()

def claim(paths):
    already=dict(line.rsplit(' ',1) for line in CLAIMS.read_text().splitlines() if line)
    for path in paths:
        if already.get(path.as_posix(),'MAPSITE')!='MAPSITE':
            raise ValueError(f'{path} is owned by {already[path.as_posix()]}')
    with CLAIMS.open('a') as file:
        file.writelines(f'{path} MAPSITE\n' for path in sorted(paths) if path.as_posix() not in already)

def update_count_or_hash(original,replacements):
    for before,after in replacements:
        assert original.count(before)==1, before
        original=original.replace(before,after)
    return original

def publish(slugs):
    staged={s:c.STAGE/s for s in slugs}
    assert FIXED <= set(staged)
    changes={}
    detached={}
    data={key:read(key) for key in (CATALOG,REC,LAUNCH,LEGACY)}
    catalogs={key:{r['id']:r for r in d['maps' if key in (CATALOG,LEGACY) else 'rows']}
              for key,d in data.items()}
    for slug,src in staged.items():
        row=c.read(c.STAGE/(slug+'-audit.json'))
        p=c.read(src/'provenance.json')
        assert row['mapId']==p['mapId'] and row['manifestSha256']==c.sha(src/'map.json')
        target=SA/'Maps'/slug
        current=ROOT/target
        if current.exists():
            assert slug in ('g0dtham-gods-poplightning','cold-hearted-gods-pitdevil')
        else:
            changes[Path(str(target)+'.meta')]=stable_meta(target,True)
        for item in sorted(src.iterdir()):
            if not item.is_file():
                continue
            dst=target/item.name
            if item.name=='SOURCE-LICENSE.txt':
                public=c.ARCHIVE+'m/'+slug+'/'
                payload=c.ARCHIVE+'maps/'+slug+'/map.json'
                if (ROOT/dst).is_file():
                    old=(ROOT/dst).read_bytes()
                    newline=b'\r\n' if b'\r\n' in old else b'\n'
                    assert f'Creator: {p["creator"]}'.encode() in old
                    changes[dst]=old+f'Public archive page: {public}'.encode()+newline
                else:
                    changes[dst]=(f'{c.NOTICE}\nSource: {public}\nCreator: {p["creator"]}\nArchive payload: {payload}\n').encode()
            elif not (ROOT/dst).is_file() or c.sha(ROOT/dst)!=c.sha(item):
                changes[dst]=item
            side=Path(str(dst)+'.meta')
            if not (ROOT/side).is_file():
                changes[side]=stable_meta(dst)
        # The old cooked solid-volume table is bound to the superseded GLB.
        if current.is_dir():
            for name in ('solid-volumes-r25.json','solid-volumes-r25.json.meta'):
                item=target/name
                if (ROOT/item).is_file():
                    detached[item]=TMP/'superseded'/item
        thumb=UI/(slug+'.jpg')
        changes[thumb]=TMP/'thumbs'/(slug+'.jpg')
        assert changes[thumb].is_file()
        thumb_meta=Path(str(thumb)+'.meta')
        if not (ROOT/thumb_meta).is_file():
            template=(ROOT/UI/'g0dtham-gods-poplightning.jpg.meta').read_text()
            guid=stable_meta(thumb).decode().splitlines()[1]
            changes[thumb_meta]=re.sub(r'^guid: [0-9a-f]{32}$',guid,template,count=1,flags=re.M).encode()
        id=row['mapId']
        catalogs[CATALOG][id]=dict(id=id,title=p['title'],relativeFolder='Maps/'+slug,creator=p['creator'])
        for catalog,label in ((REC,'recovery'),(LAUNCH,'launch')):
            entry=c.read(c.STAGE/(slug+'-'+label+'.json'))
            assert entry['manifestSha256']==row['manifestSha256'] and entry['sourceSha256']==row['sourceSha256']
            catalogs[catalog][id]=entry
    del catalogs[LEGACY]['archive:banana-land-gods-pitdevil']
    for key,d in data.items():
        name='maps' if key in (CATALOG,LEGACY) else 'rows'
        # Retain the ordering of frozen rows and append extension rows. Main
        # map selection follows the archive catalog's slug order.
        rows=catalogs[key]
        d[name]=(sorted(rows.values(),key=lambda r:r['id']) if key==CATALOG else list(rows.values()))
        if key==LEGACY:
            d['mapCount']=len(d[name])
        if key==CATALOG:
            assert len(rows)<=105
        changes[key]=(pretty_json(key,d) if key in (CATALOG,LEGACY) else flatten_json(key,d))
    old_data=(ROOT/REC).read_bytes()
    old_sha=hashlib.sha256(old_data).hexdigest()
    new_sha=hashlib.sha256(changes[REC]).hexdigest()
    old_count=len(read(REC)['rows'])
    new_count=len(data[REC]['rows'])
    # r19: R24ArchiveRecoveryCatalog.cs no longer pins the data hash or the row count, and the Cold Hearted
    # exclusion was a one-off (r18); nothing in C# changes when maps are added.
    claim(set(changes)|set(detached))
    sizes=[]
    for path,source in changes.items():
        if isinstance(source,Path):
            after=source.stat().st_size
        else:
            after=len(source)
        before=(ROOT/path).stat().st_size if (ROOT/path).is_file() else 0
        sizes.append(dict(path=path.as_posix(),before=before,after=after,change=after-before))
    for path in detached:
        before=(ROOT/path).stat().st_size
        sizes.append(dict(path=path.as_posix(),before=before,after=0,change=-before,movedTo=str(detached[path])))
    # Stage all exact bytes before changing the shared checkout.
    stage=TMP/'publish'
    for path,source in changes.items():
        dest=stage/path
        dest.parent.mkdir(parents=True,exist_ok=True)
        if isinstance(source,Path):
            with source.open('rb') as read_from, dest.open('wb') as write_to:
                import shutil
                shutil.copyfileobj(read_from,write_to)
        else:
            dest.write_bytes(source)
    for path,dest in detached.items():
        dest.parent.mkdir(parents=True,exist_ok=True)
        assert not dest.exists()
        os.replace(ROOT/path,dest)
    for path in changes:
        dest=ROOT/path
        dest.parent.mkdir(parents=True,exist_ok=True)
        os.replace(stage/path,dest)
    c.write(TMP/'R26PublishedMapFiles.json',sizes)
    assert all((ROOT/path).is_file() for path in changes)
    print(f'Published {len(staged)} maps and {len(changes)} files; recovery rows {old_count} → {new_count}')
    print(f'Net added bytes in map change set: {sum(r["change"] for r in sizes)}')

if __name__=='__main__':
    publish(c.read(sys.argv[1]))
