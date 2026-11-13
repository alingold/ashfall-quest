from pathlib import Path
import json,urllib.request,urllib.parse,hashlib,time,concurrent.futures,shutil
R=Path(__file__).resolve().parents[3];D=R/'research';index=json.loads((D/'index.json').read_text());base='https://maps.deliciouswines.org/'
def get(url,path):
 if path.exists():return path.read_bytes()
 with urllib.request.urlopen(url,timeout=40) as response:data=response.read(8*1024*1024)
 path.write_bytes(data);return data
get(base+'tex/tex.json',D/'tex.json')
def inspect(row):
 slug=row['slug'];assert slug and '/' not in slug and '..' not in slug
 out={'slug':slug,'title':row.get('name',slug),'creator':row.get('creator','Unknown'),'official':row.get('official',False)}
 try:
  m=json.loads(get(base+'maps/'+urllib.parse.quote(slug)+'/map.json',D/(slug+'.json')));out['metadata']=m
  request=urllib.request.Request(base+'maps/'+urllib.parse.quote(slug)+'/map.glb',headers={'Range':'bytes=0-19','User-Agent':'ASHFALL-local-map-adapter/1003 (polite archive integrity check)'})
  with urllib.request.urlopen(request,timeout=40) as response:
   header=response.read(20);out['status']=response.status;out['etag']=response.headers.get('ETag');out['content_range']=response.headers.get('Content-Range');out['content_length']=response.headers.get('Content-Length');out['glb_bytes']=int.from_bytes(header[8:12],'little') if header[:4]==b'glTF' else None
  out['triangles']=m.get('layers',{}).get('world_triangles');out['spawns']=len(m.get('spawns',[]));out['ok']=out['glb_bytes'] is not None
 except Exception as e:out.update(ok=False,error=str(e))
 time.sleep(.15);return out
rows=[]
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
 for row in pool.map(inspect,index):
  rows.append(row);print(len(rows),'/',len(index),row['slug'],row.get('glb_bytes'),row.get('error',''),flush=True)
  (D/'inventory-progress.json').write_text(json.dumps(rows,indent=2)+'\n')
free=shutil.disk_usage(R).free;size=sum(r.get('glb_bytes') or 0 for r in rows);estimate=size*6+3*1024**3
result={'entries':len(index),'ok':sum(r['ok'] for r in rows),'rawGlbBytes':size,'estimatedPeakAdditionalBytes':estimate,'estimatePolicy':'Conservative 6x raw GLB for source + float expansion + scene meshes/collision + temporary conversion, plus 3 GiB shared/game/import overhead. Per-stage free-space guard also required.','freeBytes':free,'reserveBytes':50*1024**3,'safe':free-estimate>=50*1024**3,'rows':rows}
(D/'INVENTORY-1003.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k!='rows'},indent=2))
