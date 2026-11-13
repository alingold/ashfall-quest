"""Actual R23 r8/r9 panel component extraction; original source is read-only."""
from pathlib import Path
import collections,json,mmap,re,struct,hashlib
import numpy as np
SOURCE=Path(r'C:/Users/aling/Documents/Codex/2026-10-22/task-2/standalone1003-library/candidate/local_archive/maps/alfa-aglae-souza/source/map.glb')
HASH='0f0a7c9ee9b89314d7b3797fffd8344e8b310469b01e06a069fc9ac153bd2994'
def bounds(rows):return rows[:,:,:3].min(axis=(0,1)),rows[:,:,:3].max(axis=(0,1))
def center(panel):return (panel['low']+panel['high'])*.5
def size(panel):return panel['high']-panel['low']
def normal(rows):
    v=rows[:,:,:3];n=np.cross(v[:,1]-v[:,0],v[:,2]-v[:,0]);best=n[np.argmax(np.sum(n*n,axis=1))];return best/max(np.linalg.norm(best),1e-20)
def source_batches():
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()==HASH
    f=SOURCE.open('rb');raw=mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ);n=struct.unpack_from('<I',raw,12)[0];d=json.loads(raw[20:20+n]);offset=28+n
    def acc(i):
        a=d['accessors'][i];v=d['bufferViews'][a['bufferView']];dt=np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']]);w={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
        x=np.ndarray((a['count'],w),dt,buffer=raw,offset=offset+v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',w*dt.itemsize),dt.itemsize)).astype(np.float32)
        return np.maximum(-1,x/np.iinfo(dt).max)if a.get('normalized')else x
    groups=collections.OrderedDict()
    for node in d['nodes']:
        match=re.fullmatch(r'(world|roof)_(-?\d+)_(-?\d+)_(vc|s48)',node.get('name',''))
        if not match:continue
        assert not any(k in node for k in ['matrix','translation','scale','rotation','children'])
        kind,x,z,material=match.groups();key=(kind,material,int(x)//4,int(z)//4)
        for p in d['meshes'][node['mesh']]['primitives']:
            a=p['attributes'];pos=acc(a['POSITION']);norm=acc(a['NORMAL']);norm/=np.maximum(np.linalg.norm(norm,axis=1)[:,None],1e-20);uv=acc(a['TEXCOORD_0'])if'TEXCOORD_0'in a else np.zeros((len(pos),2),np.float32);color=acc(a['COLOR_0'])if'COLOR_0'in a else np.ones((len(pos),4),np.float32)
            if color.shape[1]==3:color=np.column_stack((color,np.ones(len(pos),np.float32)))
            idx=acc(p['indices']).astype(np.int32).ravel();rows=np.column_stack((pos,norm,uv,color))[idx].reshape(-1,3,12)
            bad=np.linalg.norm(rows[:,:,3:6],axis=2)<.5
            if bad.any():
                geometric=np.cross(rows[:,1,:3]-rows[:,0,:3],rows[:,2,:3]-rows[:,0,:3]);geometric/=np.maximum(np.linalg.norm(geometric,axis=1)[:,None],1e-20);rows[:,:,3:6][bad]=np.broadcast_to(geometric[:,None,:],rows[:,:,3:6].shape)[bad]
            groups.setdefault(key,[]).append(rows)
    return collections.OrderedDict((key,np.concatenate(parts))for key,parts in groups.items())
def components(rows):
    flat=rows[:,:,:3].reshape(-1,3);scaled=flat*np.float32(1000);keys=np.where(scaled>=0,np.floor(scaled+.5),np.ceil(scaled-.5)).astype(np.int64)
    unique,first,inverse=np.unique(keys,axis=0,return_index=True,return_inverse=True);parents=np.arange(len(unique),dtype=np.int32)
    def root(i):
        while parents[i]!=i:parents[i]=parents[parents[i]];i=int(parents[i])
        return i
    for tri in inverse.reshape(-1,3):
        for k in (1,2):parents[root(int(tri[k]))]=root(int(tri[0]))
    roots=np.array([root(i)for i in range(len(unique))],np.int32);tri_roots=roots[inverse.reshape(-1,3)[:,0]];result=[];point_groups=collections.OrderedDict();triangle_groups={}
    for point_id in np.argsort(first):point_groups.setdefault(int(roots[point_id]),[]).append(int(first[point_id]))
    for ti,group in enumerate(tri_roots):triangle_groups.setdefault(int(group),[]).append(ti)
    for group,points in point_groups.items():
        ids=np.array(triangle_groups[group]);vertices=flat[points]
        lo=vertices.min(axis=0);hi=vertices.max(axis=0);s=hi-lo;count=len(ids);door=False
        if count in (2,4,10,12)and 1.7<s[1]<3 and max(s[0],s[2])<2.1:
            for a in range(min(len(vertices),12)):
                for b in range(a+1,min(len(vertices),16)):
                    edge=vertices[b]-vertices[a];edge[1]=0
                    if np.linalg.norm(edge)<.03:continue
                    right=edge/np.linalg.norm(edge);n=np.array([-right[2],0,right[0]]);at=np.column_stack((vertices@right,vertices@n));extent=at.max(axis=0)-at.min(axis=0)
                    if .7<max(extent)<1.8 and min(extent)<.18:door=True;break
                if door:break
        result.append(dict(ids=ids,low=lo,high=hi,door=door))
    return result
def extract():
    batches=source_batches();panels=[];components_by_batch={};erased={}
    for key,rows in batches.items():
        groups=components(rows);components_by_batch[key]=groups;remove=np.zeros(len(rows),bool)
        for group in groups:
            s=group['high']-group['low'];count=len(group['ids']);glass=key[1]=='s48'
            selected=(glass and .6<max(s[0],s[2])<=8 and .9<=s[1]<5 and count<=48)or(key[1]=='vc'and((count in (2,4,10,12)and 1.7<s[1]<3 and min(s[0],s[2])<.18 and .7<max(s[0],s[2])<1.8)or group['door']))
            if not selected:continue
            part=rows[group['ids']];panels.append(dict(low=group['low'].copy(),high=group['high'].copy(),glass=glass,parts=[(key[1],part)],main=part));remove[group['ids']]=True
        erased[key]=remove
    # _merge_glass_r10: nearly coincident back/front panes share one owner.
    merged=[]
    for pane in panels:
        master=None
        if pane['glass']:
            for old in merged:
                if old['glass']and np.linalg.norm(center(old)-center(pane))<=.2 and np.linalg.norm(size(old)-size(pane))<=.4 and abs(np.dot(normal(old['main']),normal(pane['main'])))>=.98:master=old;break
        if master is None:merged.append(pane)
        else:master['parts']+=pane['parts'];master['low']=np.minimum(master['low'],pane['low']);master['high']=np.maximum(master['high'],pane['high'])
    panes=[p for p in merged if p['glass']]
    # Recognized door-shaped frame components belong to the matching pane.
    for panel in merged:
        if panel['glass']:continue
        for pane in panes:
            delta=np.abs(center(pane)-center(panel));difference=np.abs(size(pane)-size(panel))
            if np.linalg.norm(delta)<=.3 and difference[1]<=.35 and max(difference[0],difference[2])<=.4:
                pane['parts']+=panel['parts'];pane['low']=np.minimum(pane['low'],panel['low']);pane['high']=np.maximum(pane['high'],panel['high']);panel['merged']=True;break
    # Small remaining vc frame components near panes are original attachments.
    for key,groups in components_by_batch.items():
        if key[1]!='vc':continue
        rows=batches[key]
        for group in groups:
            if erased[key][group['ids']].any():continue
            s=group['high']-group['low'];at=(group['low']+group['high'])*.5
            for pane in panes:
                if np.linalg.norm(center(pane)-at)>.22 or min(s[0],s[2])>.16 or np.any(s>size(pane)+.15)or len(group['ids'])>120:continue
                pane['parts'].append(('vc',rows[group['ids']]));erased[key][group['ids']]=True;break
    assembled=[]
    for panel in merged:
        if panel.get('merged'):continue
        master=None
        if not panel['glass']:
            for old in assembled:
                if not old['glass']and np.linalg.norm(center(old)-center(panel))<.38:master=old;break
        if master is None:assembled.append(panel)
        else:master['parts']+=panel['parts'];master['low']=np.minimum(master['low'],panel['low']);master['high']=np.maximum(master['high'],panel['high'])
    return batches,erased,assembled
if __name__=='__main__':
    batches,erased,panels=extract();print('source batches',len(batches),'panels',len(panels),'glass',sum(p['glass']for p in panels),'doors',sum(not p['glass']for p in panels),'triangles',sum(len(r)for p in panels for k,r in p['parts']),flush=True)
