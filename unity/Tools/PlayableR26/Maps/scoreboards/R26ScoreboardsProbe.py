"""Read-only prototype inventory. Run from the Unity project."""
from pathlib import Path
import sys,json,collections
import numpy as np
sys.path.insert(0,'/home/al/ashfall/tools/windows')
from r23geom import MapData

def components(tri):
    keys={};parent=list(range(len(tri)))
    def root(i):
        while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
        return i
    for i,t in enumerate(np.round(tri,4)):
        for p in t:
            k=tuple(p)
            if k in keys:
                a,b=root(i),root(keys[k]);parent[a]=b
            else:keys[k]=i
    groups=collections.defaultdict(list)
    for i in range(len(tri)):groups[root(i)].append(i)
    return [np.array(v) for v in groups.values()]

def folders():
    r=Path('Assets/StreamingAssets/PlayableR23')
    return [r/'World']+sorted((r/'Maps').glob('*/map.json'))+sorted((r/'Legacy/Maps').glob('*/map.json'))
if __name__=='__main__':
    for p in folders():
        p=p.parent if p.name=='map.json' else p
        m=MapData(str(p));chunks=[c for c in m.chunks if c.visible and m.materials[c.material]=='pd959969c']
        if not chunks:print(m.map_id,'NO material',flush=True);continue
        T=np.concatenate([c.triangles for c in chunks]);rows=[]
        for ix in components(T):
            a=T[ix];P=np.unique(np.round(a.reshape(-1,3),4),axis=0);e=P.max(0)-P.min(0)
            if len(ix)==16:rows.append((len(P),P.mean(0).round(2).tolist(),e.round(2).tolist()))
        print(m.map_id,len(T),'16-tri',rows,flush=True)
