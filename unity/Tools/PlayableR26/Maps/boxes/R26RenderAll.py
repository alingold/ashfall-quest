import sys,json,concurrent.futures
sys.path.insert(0,'/home/al/ashfall/tools/boxes');import R26BoxesReview as b
def render(r):
 m,ch=b.maps.load(str(b.SA/r['folder']));b.render(r,ch);return r['mapId']
if __name__=='__main__':
 rows=json.loads((b.OUT/'analysis.json').read_bytes())
 with concurrent.futures.ProcessPoolExecutor(max_workers=3) as pool:
  for mid in pool.map(render,rows):print(mid,flush=True)
 b.sheets(rows)
