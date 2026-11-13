from pathlib import Path
import json,hashlib,struct,mmap,collections,copy
import numpy as np
from prepare import ROOT,SRC,SLUGS,HERE,LIMIT,verified_sources,sha,FROZEN_SHA,CONTENT,source_triangles,triangle_keys,area

def expand(ranges,count):
 assert isinstance(ranges,list)and len(ranges)%2==0;result=[];end=0
 for start,length in zip(ranges[::2],ranges[1::2]):
  assert isinstance(start,int)and isinstance(length,int)and start>=end and length>0 and start+length<=count
  result.extend(range(start,start+length));end=start+length
 return result

def validate_partition(row,indices,owner_count):
 nt=len(indices);groups=[expand(row['staticTriangleRanges'],nt)]
 for part in row['parts']:
  assert 0<=part['owner']<owner_count;ids=expand(part['triangleRanges'],nt);assert len(ids)==part['triangles'];groups.append(ids)
 full=[i for g in groups for i in g];assert len(full)==nt and len(set(full))==nt and sorted(full)==list(range(nt))
 # Rebuilding owned and static draw indices only slices original triples. No new vertices,
 # normal/UV/color interpolation, winding reversal or hole-filling triangle is admitted.
 for ids in groups:assert np.array_equal(indices[ids],np.asarray([indices[i]for i in ids],dtype=np.int32))
 return groups

def main():
 inputs=verified_sources();catalog=json.loads((SRC/'assets/data/map_fixtures_r18.json').read_text());checks=0;records=[];output_bytes=0
 for slug in SLUGS:
  folder=ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps'/slug;path=folder/'tower-fixtures-r5.json';data=json.loads(path.read_text());output_bytes+=path.stat().st_size
  assert data['version']==1 and data['geometryVersion']==1 and data['sourceContentId']==CONTENT and data['sourceManifestSha256']==FROZEN_SHA
  assert {r['path']:r['sha256']for r in data['sourceInputs']}==inputs;checks+=len(inputs)
  manifest=json.loads((folder/'map.json').read_text());assert sha(folder/'geometry.bytes')==data['baseGeometrySha256'];assert manifest['chunks']==data['baseChunks'];assert [r['name']for r in manifest['materials']]==data['baseMaterialKeys'];assert len(manifest.get('destructibles',[]))==data['basePanels']
  owners=np.asarray(manifest.get('chunkPanelIds',[-1]*manifest['chunks']),dtype='<i4');assert hashlib.sha256(owners.tobytes()).hexdigest()==data['baseOwnershipSha256'];checks+=5
  original=json.loads((SRC/'assets/data/map_fixtures_r18.json').read_text())['maps'][slug];specs,rawpath,recoveries=source_triangles(slug,original);assert sha(rawpath)==data['sourceGlbSha256']==original['source_sha256'];assert data['rawComponentAdaptations']==recoveries
  expected=collections.Counter();expected_owner=collections.defaultdict(collections.Counter);source_areas=collections.defaultdict(float)
  for si,s in enumerate(specs):
   tri=s['triangles'].copy();tri[:,:,2]*=-1
   for k in triangle_keys(tri):expected[(s['key'],bytes(k))]+=1;expected_owner[s['owner']][(s['key'],bytes(k))]+=1
   source_areas[s['key']]+=area(tri)
  bychunk={row['chunk']:row for row in data['chunks']};assert len(bychunk)==len(data['chunks']);seen_render=collections.Counter();seen_collision=collections.Counter();owner_render=collections.defaultdict(collections.Counter);owner_collision=collections.defaultdict(collections.Counter);ownerpoints=collections.defaultdict(list);areas_render=collections.defaultdict(float);areas_collision=collections.defaultdict(float);unchanged=patched=0;negative_checked=False
  f=(folder/'geometry.bytes').open('rb');raw=mmap.mmap(f.fileno(),0,access=mmap.ACCESS_READ);magic,count=struct.unpack_from('<ii',raw);cursor=8;assert count==data['baseChunks']
  for ci in range(count):
   mid,coll,vis,nv,ni=struct.unpack_from('<iiiii',raw,cursor);cursor+=20;vstart=cursor;vertex=np.ndarray((nv,12),'<f4',buffer=raw,offset=cursor);cursor+=nv*48;istart=cursor;indices=np.ndarray((ni//3,3),'<i4',buffer=raw,offset=cursor);cursor+=ni*4
   if ci not in bychunk:unchanged+=1;continue
   row=bychunk[ci];assert owners[ci]<0;assert (row['material'],row['collisionFlags'],row['visible'],row['vertices'],row['indices'])==(mid,coll,vis,nv,ni);assert row['materialKey']==manifest['materials'][mid]['name'];assert hashlib.sha256(raw[vstart:istart]).hexdigest()==row['vertexSha256']and hashlib.sha256(raw[istart:cursor]).hexdigest()==row['indexSha256'];checks+=5
   groups=validate_partition(row,indices,len(data['definitions']));checks+=1
   if not negative_checked:
    # Mutation oracles test runtime-relevant range contracts using a real patched chunk.
    for change in ['overlap','outside','unknown-owner','missing']:
     bad=copy.deepcopy(row)
     if change=='overlap':bad['parts'][0]['triangleRanges']=bad['staticTriangleRanges'][:2];bad['parts'][0]['triangles']=bad['staticTriangleRanges'][1]
     elif change=='outside':bad['parts'][0]['triangleRanges']=[len(indices),1];bad['parts'][0]['triangles']=1
     elif change=='unknown-owner':bad['parts'][0]['owner']=len(data['definitions'])
     else:bad['staticTriangleRanges']=[]
     refused=False
     try:validate_partition(bad,indices,len(data['definitions']))
     except AssertionError:refused=True
     assert refused,change;checks+=1
    negative_checked=True
   for part,ids in zip(row['parts'],groups[1:]):
    tri=vertex[indices[ids],:3];key=row['materialKey'];oi=part['owner'];ownerpoints[oi].append(tri)
    for keybytes in triangle_keys(tri):
     identity=(key,bytes(keybytes))
     if vis:seen_render[identity]+=1;owner_render[oi][identity]+=1
     if coll:seen_collision[identity]+=1;owner_collision[oi][identity]+=1
    if vis:areas_render[key]+=area(tri)
    if coll:areas_collision[key]+=area(tri)
   patched+=1
  assert cursor==len(raw);assert seen_render==seen_collision==expected;assert owner_render==owner_collision==expected_owner;checks+=4
  for key,value in source_areas.items():assert abs(areas_render[key]-value)<1e-8 and abs(areas_collision[key]-value)<1e-8;checks+=1
  assert len(data['definitions'])==18;assert len(set(d['id']for d in data['definitions']))==18
  for oi,(definition,assembly)in enumerate(zip(data['definitions'],original['tower_gate_grates'])):
   assert definition['fixtureKind']==assembly['fixture_kind']and definition['openingId']==assembly['opening_id']and not definition['glass']and definition['health']==1
   assert definition['triangles']==assembly['total_triangles']==sum(owner_render[oi].values());assert definition['sound']=='res://assets/audio/sfx/r18/metal_grate.wav'
   points=np.concatenate(ownerpoints[oi]);lo=points.min(axis=(0,1));hi=points.max(axis=(0,1));assert np.allclose((lo+hi)*.5,definition['center'],atol=1e-5)and np.allclose(hi-lo,definition['size'],atol=1e-5)
   probe=np.array(definition['probe']);normal=np.array(definition['normal']);centroids=points.mean(axis=1);on=np.flatnonzero(np.linalg.norm(centroids-probe,axis=1)<1e-4);assert len(on)>0 and abs(np.linalg.norm(normal)-1)<1e-5
   cross=np.cross(points[on,1]-points[on,0],points[on,2]-points[on,0]);cross/=np.maximum(np.linalg.norm(cross,axis=1)[:,None],1e-20);assert (cross@normal<-.999).any(),'probe normal must be reflected source normal, not reflected winding cross';checks+=6
  record=dict(map=slug,owners=18,sourceMembers=159,renderTriangles=sum(seen_render.values()),collisionTriangles=sum(seen_collision.values()),patchedChunks=patched,unchangedChunks=unchanged,existingOwnersPreserved=data['basePanels'],bytes=path.stat().st_size,sha256=sha(path));records.append(record);print('PASS',slug,record)
 assert output_bytes<=LIMIT
 result=dict(passed=True,checks=checks,maps=records,deltaBytes=output_bytes,sourceContentId=CONTENT,engineRun=False,deviceRun=False,geometryWrites=False,materialWrites=False)
 (HERE/'checks.json').write_text(json.dumps(result,indent=2)+'\n');print('PASS',checks,'source/index/material/owner/conservation and real-delta negative cases;',output_bytes,'bytes total')
if __name__=='__main__':main()
