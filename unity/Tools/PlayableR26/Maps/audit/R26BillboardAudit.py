from R26MapAudit import *
# Probe source panels with 25 short rays directed through each aperture.
# Existing source artwork excluded from scoreboard display remains in its source mesh.
candidates=index(read(root.parent/'campaign/scoreboards3/R26Candidates.json')['maps'])
def depth(scene,p,n,d):
 a=p-n*d;b=p+n*d;idx=scene.query(np.minimum(a,b)-.001,np.maximum(a,b)+.001);t=scene.t[idx].astype(float)
 e=t[:,1]-t[:,0];f=t[:,2]-t[:,0];h=np.cross(n,f);den=(e*h).sum(1);inv=1/np.where(abs(den)>1e-8,den,1)
 q=a-t[:,0];u=(q*h).sum(1)*inv;c=np.cross(q,e);v=(c*n).sum(1)*inv;ht=(f*c).sum(1)*inv
 ok=(abs(den)>1e-8)&(u>=-1e-7)&(v>=-1e-7)&(u+v<=1.000001)&(ht>=0)&(ht<=d*2)
 return [round(float(h-d),3) for h in ht[ok]]
result={}
for k,ids in {'archive:classic-bw-187norvis':['R26-frame-1'],'archive:ga-fenix-ga-hikesa':['R26-frame-'+str(i) for i in (1,2,3,4,5)]+['R26-stock-19','R26-stock-20']}.items():
 folder=sa/catalog[k];scene=Scene(folder,read(folder/'map.json'));result[k]={}
 for r in candidates[k]['candidates']:
  if r['id'] not in ids:continue
  center=np.array(r['center']);right=np.array(r['right']);normal=np.array(r['normal']);up=np.cross(right,normal)
  hits=[depth(scene,center+x*right*r['width']+y*up*r['height'],normal,max(1,r['frontOffset']+1)) for x in (-.4,-.2,0,.2,.4) for y in (-.4,-.2,0,.2,.4)]
  result[k][r['id']]=dict(covered=sum(bool(h) for h in hits),samples=len(hits),center=hits[12])
print(json.dumps(result,indent=1));(out/'R26OccupiedBoards.json').write_text(json.dumps(result,indent=1))
