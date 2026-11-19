"""Focused topology/placement checks, no Unity/source payload mutation."""
import copy,json,sys
from pathlib import Path
import numpy as np
from R26ScoreboardsDetect import extract,template,ROOT,RESOURCE,OUT
from r23geom import MapData

def main():
 ref=template();m=MapData(str(ROOT/'Assets/StreamingAssets/PlayableR23/World'));rows=extract(m,ref);accepted=[r for r in rows if r['approved']];assert len(accepted)==1
 q=accepted[0];assert np.linalg.norm(np.array(q['center'])-[-3.9606,78.7428,-26.2366])<.002;assert abs(q['width']-7.0786)<.003;assert abs(q['height']-7.785)<.003
 broken=copy.copy(m);broken.materials=list(m.materials);broken.materials[broken.materials.index('p8ad59c41')]='unrelated';assert extract(broken,ref)==[]
 broken=copy.copy(m);broken.materials=list(m.materials);broken.materials[broken.materials.index('pd959969c')]='unrelated';assert extract(broken,ref)==[]
 transformed=copy.copy(m);transformed.chunks=[copy.copy(c) for c in m.chunks];a=np.deg2rad(37);matrix=np.array([[np.cos(a),0,-np.sin(a)],[0,1,0],[np.sin(a),0,np.cos(a)]])@np.diag([1.7,.8,1.7]);shift=np.array([41,5,-32])
 for c in transformed.chunks:c.positions=c.positions.astype(float)@matrix.T+shift
 transformed.manifest=dict(m.manifest)
 for key in ['spawn','warmupSpawn','gameSpawn']:transformed.manifest[key]=(np.array(m.manifest[key])@matrix.T+shift).tolist()
 moved=extract(transformed,ref);expected=np.array(q['center'])@matrix.T+shift;match=min(moved,key=lambda r:np.linalg.norm(np.array(r['center'])-expected));assert np.linalg.norm(np.array(match['center'])-expected)<.004;assert abs(match['width']-(q['width']+.006)*1.7+.006)<.005
 data=json.load(open(RESOURCE));report=json.load(open(OUT/'R26ScoreboardsAudit.json'))
 badlands=next(x for x in report['maps'] if x['id']=='archive:badlands-gods-pitdevil');assert len(badlands['rectangles'])==4
 for f in badlands['rectangles']:
  assert 1.2<f['topInset']<1.5 and 12.8<f['height']<13.1
  assert np.dot(np.array(f['center'])-np.mean([q['center'] for q in badlands['rectangles']],axis=0),f['normal'])>5
 assert len(data['maps'])==108;assert len({r['id'] for r in data['maps']})==108
 count=0
 for r in data['maps']:
  source=next(x for x in report['maps'] if x['id']==r['id']);assert r['sourceSha256']==source['sourceSha256'];assert r['geometrySha256']==source['geometrySha256']
  for f in r['rectangles']:
   assert f['approved'] and f['confidence']>=.9;assert all(np.isfinite(f[k]).all() for k in ['center','normal','right']);assert abs(np.dot(f['normal'],f['right']))<1e-6;assert abs(np.linalg.norm(f['normal'])-1)<1e-6;assert np.allclose(np.cross(f['normal'],[0,1,0]),f['right']);assert f['frontOffset']>f['depth']/2;assert f['width']>0 and f['height']>0;count+=1
 print('PASS Paintball aperture control; missing ring/trim rejected; yaw+translation+independent vertical scale recovered; 108 unique map provenance bindings;',count,'approved orthonormal fitted rectangles')
if __name__=='__main__':main()
