import json,concurrent.futures
from download1003 import A,INDEX,fetch,save
data=json.loads((A/'downloads.json').read_text())
def repair(row):
 slug=row['slug'];uri=INDEX[slug].get('thumb') or 'maps/'+slug+'/thumb.jpg'
 try:row['thumbnail']=fetch(uri,A/'maps'/slug/'source/thumb.jpg')
 except Exception as exc:row['thumbnail']={'warning':str(exc),'uri':uri}
 save(A/'maps'/slug/'download.json',row);return row
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:data['rows']=list(pool.map(repair,data['rows']))
save(A/'downloads.json',data);warnings=[r['slug'] for r in data['rows'] if 'warning' in r['thumbnail']];print('THUMBNAILS',len(data['rows'])-len(warnings),'/',len(data['rows']),'warnings',warnings)
