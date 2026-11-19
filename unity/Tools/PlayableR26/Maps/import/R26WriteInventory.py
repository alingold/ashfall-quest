from pathlib import Path
import json
import re

tmp=Path(__file__).resolve().parent
def read(name):
    return json.loads((tmp/name).read_text())
s=read('R26MapsiteSurvey.json')
site=read('maps-index.json')
names={r['slug']:r['name'] for r in site}
selected=set(read('R26SelectedSlugs.json'))
batch={r['slug']:r for r in map(json.loads,(tmp/'R26ArchiveBatchResults.jsonl').read_text().splitlines())}
audit={r['slug']:r for r in read('R26MapsiteAudit.json')}
fixed=['banana-land-gods-pitdevil','g0dtham-gods-poplightning','cold-hearted-gods-pitdevil']
def read_current(slug):
    p=tmp/(slug+'-map.json')
    if not p.exists():p=tmp/'maps-banana-land-gods-pitdevil-map.json'
    return json.loads(p.read_text())
def status(slug):
    if slug in selected:
        return 'Imported'
    if slug in audit:
        return audit[slug]['reason']
    reason=batch[slug]['reason']
    files=re.findall(r'tex/[^, \n]+?\.(?:jpg|png)',reason)
    files=[f'`{f.removeprefix("tex/")}`' for f in files]
    if files:
        if len(files)>4:
            return 'Replacement slots missing for '+', '.join(files[:4])+f' and {len(files)-4} more'
        return 'Replacement slots missing for '+', '.join(files)
    return reason.replace('|','\\|')
def label(id,title=None):
    title=(title or names[id]).replace('|','\\|')
    return f'[{title}](https://maps.deliciouswines.org/m/{id}/)'
def row(lines,item,detail=None):
    x=item['id']
    col=[label(x),item['creator'].replace('|','\\|'),item['first'] or 'Unknown',item['last'] or 'Unknown']
    if detail is not None:col.append(detail)
    lines.append('| '+' | '.join(col)+' |')
d=s['originalUnityPayloadImport']
text=['# MAPSITE archive inventory','',
      'The snapshot has 215 map listings. 105 match an existing ASHFALL archive ID and 110 were absent. The original Unity archive conversion completed on 2026-11-05, using the 2026-10-27 source snapshot. The date fields are the public archive’s first and last captures. A later listing date alone cannot establish whether a recapture changed the map geometry.','',
      '## New since the Unity import','',
      '| Map | Creator | First capture | Last capture | r18 decision |','| --- | --- | --- | --- | --- |']
for item in s['new']:
    if item['first']>d:row(text,item,status(item['id']))
text+=['','## Absent maps first captured by the import date','',
       'These 53 maps predate the Unity conversion and were outside the import window used for r18. The R6 frozen source snapshot is earlier, so 45 of these were first captured after it.','',
       '| Map | Creator | First capture | Last capture |','| --- | --- | --- | --- |']
for item in s['new']:
    if item['first']<=d:row(text,item)
text+=['','## Later captures of existing imports','',
       'These 54 IDs have a newer archive capture date than the 2026-10-27 source snapshot, including 29 newer than the Unity conversion. Geometry changes were independently confirmed for Banana Land, Gotham and Cold Hearted. The Cold Hearted listing still shows 2026-10-26.','',
       '| Map | Creator | First capture | Last capture | r18 evidence |','| --- | --- | --- | --- | --- |']
for item in s['laterCaptures']:
    state='New geometry confirmed and installed' if item['id'] in fixed else 'Later capture listed'
    row(text,item,state)
text+=['','## Verified replacement of a map with an unchanged listing date','',
       '| Map | Creator | Last capture | r18 decision |','| --- | --- | --- | --- |']
sl=fixed[-1]
raw=read_current(sl)
text.append(f'| {label(sl)} | {raw["creator"]} | 2026-10-26 | New geometry confirmed and installed |')
(tmp/'R26MapsiteInventory.md').write_text('\n'.join(text)+'\n')
