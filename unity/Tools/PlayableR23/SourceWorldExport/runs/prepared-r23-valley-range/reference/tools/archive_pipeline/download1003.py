"""Resumable public archive data download. Two workers, shared texture cache, no code execution."""
from pathlib import Path
import json,urllib.request,urllib.parse,hashlib,time,concurrent.futures,threading,shutil,struct,os
R=Path(__file__).resolve().parents[3];A=R/'candidate/local_archive';A.mkdir(parents=True,exist_ok=True)
IN=json.loads((R/'research/INVENTORY-1003.json').read_text());assert IN['safe']
INDEX={row['slug']:row for row in json.loads((R/'research/index.json').read_text())}
BASE='https://maps.deliciouswines.org/';RESERVE=50*1024**3;lock=threading.Lock()
def sha(path):
 h=hashlib.sha256()
 with path.open('rb') as f:
  for b in iter(lambda:f.read(1024*1024),b''):h.update(b)
 return h.hexdigest()
def save(path,data):
 path.parent.mkdir(parents=True,exist_ok=True);temp=path.with_suffix(path.suffix+'.tmp');temp.write_text(json.dumps(data,indent=2)+'\n');os.replace(temp,path)
def fetch(uri,path,expected=None):
 parsed=urllib.parse.urlsplit(uri);assert not parsed.scheme and not parsed.netloc and '..' not in Path(uri).parts and not uri.startswith('/')
 receipt=path.with_suffix(path.suffix+'.receipt.json')
 if path.exists() and receipt.exists():
  old=json.loads(receipt.read_text())
  if old.get('uri')==uri and path.stat().st_size==old['bytes'] and (expected is None or path.stat().st_size==expected) and sha(path)==old['sha256']:return old
 if shutil.disk_usage(A).free<RESERVE+max(expected or 16*1024**2,64*1024**2):raise RuntimeError('50 GiB disk reserve reached')
 part=path.with_suffix(path.suffix+'.part');path.parent.mkdir(parents=True,exist_ok=True)
 for attempt in range(3):
  try:
   offset=part.stat().st_size if part.exists() else 0
   headers={'User-Agent':'ASHFALL-local-archive-adapter/1003 (2-worker resumable cache)'}
   if offset:headers['Range']=f'bytes={offset}-'
   with urllib.request.urlopen(urllib.request.Request(BASE+urllib.parse.quote(uri,safe='/'),headers=headers),timeout=60) as response:
    append=offset>0 and response.status==206
    if append:assert response.headers.get('Content-Range','').startswith(f'bytes {offset}-')
    with part.open('ab' if append else 'wb') as f:
     while True:
      block=response.read(1024*1024)
      if not block:break
      f.write(block)
      if shutil.disk_usage(A).free<RESERVE:raise RuntimeError('50 GiB disk reserve reached')
    etag=response.headers.get('ETag')
   if expected is not None and part.stat().st_size!=expected:raise ValueError(f'length {part.stat().st_size} != header {expected}')
   os.replace(part,path);row={'uri':uri,'bytes':path.stat().st_size,'sha256':sha(path),'etag':etag};save(receipt,row);time.sleep(.12);return row
  except Exception:
   if attempt==2:raise
   time.sleep(1.5*(attempt+1))
def inspect_glb(path):
 with path.open('rb') as f:
  header=f.read(20);magic,version,size,n,kind=struct.unpack('<IIIII',header)
  assert magic==0x46546c67 and version==2 and size==path.stat().st_size and kind==0x4e4f534a
  return json.loads(f.read(n))
tex=json.loads((R/'research/tex.json').read_text());save(A/'tex-index.json',tex)
def download(row):
 slug=row['slug'];D=A/'maps'/slug/'source';D.mkdir(parents=True,exist_ok=True)
 result={k:row[k] for k in ['slug','title','creator','official']};result['map_id']='archive:'+slug
 try:
  meta=row['metadata'];save(D/'map.json',meta)
  glb=fetch('maps/'+slug+'/map.glb',D/'map.glb',row['glb_bytes']);j=inspect_glb(D/'map.glb')
  keys=sorted({m['name'].split('_',3)[3] for m in j.get('meshes',[]) if m.get('name','').startswith(('world_','roof_'))})
  materials={};dependencies=set()
  for key in keys:
   if key in meta.get('materials',{}):material=meta['materials'][key].copy()
   elif key.startswith('t') and key[1:].isdigit() and int(key[1:])<len(tex):
    i=int(key[1:]);t=tex[i];material={'c':f'tex/{i}_c.jpg','name':t.get('name','')}
    if t.get('n'):material['n']=f'tex/{i}_n.jpg'
   elif key=='vc':material={'vertex_color':True}
   else:raise ValueError('unresolved material '+key)
   for field in ['c','n']:
    if field in material:dependencies.add(material[field])
   materials[key]=material
  # Per-map workers share the lock only while committing required texture caches.
  for uri in sorted(dependencies):
   assert uri.startswith('tex/'),uri
   with lock:fetch(uri,A/'shared'/uri)
  thumb_uri=INDEX[slug].get('thumb') or 'maps/'+slug+'/thumb.jpg'
  try:thumb=fetch(thumb_uri,D/'thumb.jpg')
  except Exception as exc:thumb={'warning':str(exc)}
  result.update(status='downloaded',bytes=glb['bytes'],sha256=glb['sha256'],texture_uris=sorted(dependencies),materials=materials,source_meshes=len(j.get('meshes',[])),extensions=j.get('extensionsRequired',[]),thumbnail=thumb)
  save(D/'provenance.json',{'source':BASE+'maps/'+slug+'/','license':'UNKNOWN REUSE LICENSE — LOCAL ONLY. Excluded from ASHFALL Apache-2.0 and redistribution.','retrieved':'2026-10-27','title':row['title'],'creator':row['creator'],'official':row['official'],'glb':glb,'materials':materials,'texture_uris':sorted(dependencies)})
 except Exception as e:result.update(status='download_failed',error=repr(e))
 save(A/'maps'/slug/'download.json',result);return result
def main():
 rows=[]
 with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
  for row in pool.map(download,IN['rows']):
   rows.append(row);save(A/'downloads.json',{'total':len(IN['rows']),'completed':len(rows),'rows':rows});print(len(rows),'/',len(IN['rows']),row['slug'],row['status'],row.get('error',''),flush=True)
 save(A/'downloads.json',{'total':len(IN['rows']),'completed':len(rows),'rows':rows});print('DOWNLOAD_MATRIX',len(rows),'successful',sum(r['status']=='downloaded' for r in rows),'free',shutil.disk_usage(A).free)
if __name__=='__main__':main()
