"""Derive source relaxed lobby wrists through the existing baked glove conversion."""
import json
from pathlib import Path
import numpy as np
SRC=Path('C:/Users/aling/Documents/Codex/2026-11-03/ashfall-r23/source')
def rotation(a,b):
    a=a/np.linalg.norm(a);b=b/np.linalg.norm(b);v=np.cross(a,b);c=np.dot(a,b)
    k=np.array([[0,-v[2],v[1]],[v[2],0,-v[0]],[-v[1],v[0],0]])
    assert c>-.999999
    return np.eye(3)+k+k@k/(1+c)
def zrotation(a):
    c,s=np.cos(a),np.sin(a);return np.array([[c,-s,0],[s,c,0],[0,0,1]])
def pose(side):
    left=side<0;suffix='_L' if left else '_R'
    doc=json.loads((SRC/'assets/hands/xrtools086'/('Hand_Glove_low'+suffix+'.gltf')).read_text())
    parents={child:i for i,node in enumerate(doc['nodes']) for child in node.get('children',[])}
    def world(i):
        n=doc['nodes'][i]
        if 'matrix' in n:local=np.array(n['matrix']).reshape(4,4).T
        else:
            x,y,z,w=n.get('rotation',[0,0,0,1]);r=np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],[2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],[2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]])
            local=np.eye(4);local[:3,:3]=r@np.diag(n.get('scale',[1,1,1]));local[:3,3]=n.get('translation',[0,0,0])
        return world(parents[i])@local if i in parents else local
    def bone(name):return world(next(i for i,n in enumerate(doc['nodes']) if n.get('name')==name))[:3,3]
    wrist=bone('Wrist'+suffix);knuckle=bone('Middle_Proximal'+suffix)
    canonical=zrotation(-side*.2);relaxed=np.array([0,-1,-.08]);relaxed/=np.linalg.norm(relaxed)
    basis=rotation(canonical@(knuckle-wrist),relaxed)@canonical
    location=np.array([side*.26,.97,-.04])-basis@wrist
    baked=zrotation(side*np.pi/2);translation=np.array([0,.032,0])-baked@((wrist+knuckle)*.5)
    reflect=np.diag([1,1,-1]);unity=reflect@basis@baked.T@reflect
    position=reflect@(location-basis@baked.T@translation)
    actual=unity@reflect@baked@(knuckle-wrist);actual/=np.linalg.norm(actual)
    expected=reflect@relaxed
    assert np.dot(actual,expected)>1-1e-12
    actual_wrist=position+unity@reflect@(baked@wrist+translation)
    assert np.linalg.norm(actual_wrist-np.array([side*.26,.97,.04]))<1e-12
    assert abs(np.linalg.det(unity)-1)<1e-12
    return dict(side='left' if left else 'right',position=position.tolist(),forward=unity[:,2].tolist(),up=unity[:,1].tolist(),fingers=actual.tolist(),wrist=actual_wrist.tolist())
if __name__=='__main__':print(json.dumps([pose(-1),pose(1)],indent=2))
