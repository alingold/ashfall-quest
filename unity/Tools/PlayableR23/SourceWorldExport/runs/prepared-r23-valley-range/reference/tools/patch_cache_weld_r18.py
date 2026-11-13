"""Aggregate two proven source-marker parts matching the observed R10 weld family.
Existing rows/assets are retained. No scenery/color heuristic or source pack edit.
"""
from pathlib import Path
import json,math
ROOT=Path(__file__).resolve().parents[1]
p=ROOT/'assets/data/map_caches_r17.json'
d=json.loads(p.read_text(encoding='utf-8'));summary={}
for slug,row in d['maps'].items():
 parts=row['marker_parts'];staged=[]
 for counts,expected in [((69,35),(3.0614,.035,2.5370)),((61,27),(3.0612,.035,.9678))]:
  for a in parts:
   if a['key']!='p2a61c272' or a['triangles']!=counts[0]:continue
   for b in parts:
    if b['key']!=a['key'] or b['triangles']!=counts[1]:continue
    if any(a['min'][i]>b['max'][i]+.0045 or b['min'][i]>a['max'][i]+.0045 for i in range(3)):continue
    lo=[min(a['min'][i],b['min'][i]) for i in range(3)];hi=[max(a['max'][i],b['max'][i]) for i in range(3)];size=[hi[i]-lo[i] for i in range(3)]
    if any(abs(size[i]-expected[i])>.0045 for i in range(3)):continue
    center=[(lo[i]+hi[i])*.5 for i in range(3)];n=sum(counts)
    if any(x['triangles']==n and math.dist(x['center'],center)<.0045 for x in parts+staged):continue
    staged.append({'key':a['key'],'triangles':n,'center':center,'min':lo,'max':hi,'source_members_r18':[{'triangles':a['triangles'],'center':a['center']},{'triangles':b['triangles'],'center':b['center']}]})
 if staged:parts.extend(staged);summary[slug]=len(staged)
p.write_text(json.dumps(d,separators=(',',':')),encoding='utf-8')
print(json.dumps(summary,indent=2))
