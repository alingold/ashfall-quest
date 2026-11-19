"""Merge reviewed r18 map pod-policy rows. For the MAPS2 owner to run."""
from pathlib import Path
import json

root=Path.cwd()
tmp=Path(__file__).resolve().parent
resource=root/'Assets/Ashfall/Integration/Playable/World/Resources/R26MapPodPolicy.json'
data=json.loads(resource.read_text())
rows={row['id']:row for row in data['maps']}
coverage={}
for line in (tmp/'pod-coverage.log').read_text().splitlines():
    slug,a,b,n,d=line.split()
    coverage[slug]=(a,b,n,d)
open_maps={'banana-land-gods-pitdevil','cold-hearted-gods-pitdevil'}
closed={'g0dtham-gods-poplightning','ice-and-fire-tdm-go-kc',
        'garden-of-eden-gods-crazytrain','meliworld-gods-meli','dark-void-gods-alamo-hank',
        'meli-cafe-gods-meli','army-of-pop1-gods-thor','eternal-gods-pitdevil'}
for slug in sorted(open_maps|closed):
    plan=json.loads((tmp/'stage'/(slug+'-recovery.json')).read_text())
    row=rows.get(plan['id'])
    if row is None:
        row={'id':plan['id']}
        data['maps'].append(row)
    covered,total,area,all_area=coverage[slug]
    row.update(pods=slug in open_maps,verdict='OPEN' if slug in open_maps else 'BOXED',
               sourceSha256=plan['sourceSha256'], source='MAPSITE r18 source capture',
               evidence=f'Collision roof above {covered}/{total} qualified match sites and {area}/{all_area} central arena probes. Reviewed overhead and cutaway renders.',
               hasWarmupLocal=False,hasGameLocal=False,
               spawns=plan['deathmatch'] if slug in closed else [])
    for field in ('warmupLocal','gameLocal'):
        row.pop(field,None)
baseline=resource.read_bytes()
newline=b'\r\n' if baseline.endswith(b'\r\n') else b'\n'
out=(json.dumps(data,ensure_ascii=False,indent=2)+'\n').encode().replace(b'\n',newline)
resource.write_bytes(out)
print(f'Updated {len(open_maps|closed)} MAPSITE pod rows.')
