"""Real source/payload geometry and pixel checks. No engine or runtime execution."""
from pathlib import Path
import json,math,struct
import numpy as np
from PIL import Image
from prepare import SRC,OUT,HERE,FOLDER,sha,glb
checks=0
def check(ok,label):
    global checks
    assert ok,label
    checks+=1
provenance=json.loads((OUT/'magnum-provenance.json').read_text());fit=json.loads((OUT/'magnum-fit.json').read_text());baseline=json.loads((HERE.parents[3]/'docs/agent-runs/20261104-three-chats/world/r24-integration/R23-BASELINE.json').read_text());pinned={r['path']:r['sha256'] for r in baseline['actualFiles']}
for relative,digest in provenance['inputs'].items():check(sha(SRC/relative)==digest==pinned[relative],'Unchanged pinned ORIGINAL R23 input '+relative)
for name,digest in provenance['outputs'].items():check(name.startswith('magnum') and sha(OUT/name)==digest,'Unique actual payload identity '+name)
for name,digest in json.loads((HERE/'preserved-existing.json').read_text()).items():check(sha(OUT/name)==digest,'Existing general hand/gun payload unchanged '+name)
loaded={};counts={}
for asset in provenance['assets']:
    name=asset['name'];manifest=json.loads((OUT/(name+'.json')).read_text());parts={};triangles=vertices=0
    with (OUT/(name+'.meshbin')).open('rb') as file:
        magic,count=struct.unpack('<ii',file.read(8));check(magic==0x5232334d and count==len(manifest['parts']),'R23Visuals magic/part count')
        for index in range(count):
            nv,ni,mi=struct.unpack('<iii',file.read(12));v=np.frombuffer(file.read(nv*32),'<f4').reshape(-1,8);i=np.frombuffer(file.read(ni*4),'<i4').reshape(-1,3);part=manifest['parts'][index]
            check(0<=mi<len(manifest['materials']) and ni%3==0 and np.isfinite(v).all() and i.min()>=0 and i.max()<nv,'Valid real geometry '+part)
            check(np.allclose(np.linalg.norm(v[:,3:6],axis=1),1,atol=3e-6),'Unit transformed normals '+part)
            faces=v[i,:3];cross=np.cross(faces[:,1]-faces[:,0],faces[:,2]-faces[:,0]);dot=(cross*v[i,3:6].mean(axis=1)).sum(axis=1)
            if name=='magnum':check(np.count_nonzero(dot< -1e-9)==1 and manifest['materials'][mi]['doubleSided'],'Preserved one tiny original body face/normal inconsistency; source double-sided finish')
            else:check(np.all(dot>=-1e-9),'Unity outward face winding '+part)
            parts[part]=(v,i,mi);triangles+=ni//3;vertices+=nv
        check(not file.read(),'Exact full binary consumed')
    check(triangles==asset['triangles'] and vertices==asset['vertices'],'Actual source recipe census '+name);loaded[name]=parts;counts[name]=dict(triangles=triangles,vertices=vertices,surfaces=count)
check(counts=={'magnum':{'triangles':5382,'vertices':16146,'surfaces':1},'magnum-cylinder':{'triangles':2462,'vertices':2501,'surfaces':18},'magnum-offer':{'triangles':6384,'vertices':4088,'surfaces':28}},'Full body5382/cylinder2462/offer6384; no decimation')
source_mesh,material,png=glb();v=loaded['magnum']['Body'][0];expected=source_mesh[0].copy();expected[:,2]*=-1
check(np.array_equal(v[:,:3],expected.astype('<f4')),'Every original fitted body vertex reflected once, no scale/pose rewrite')
check((OUT/'magnum-body-baseColor.png').read_bytes()==png,'Original embedded body PNG bytes retained')
check(np.array_equal(np.asarray(Image.open(OUT/'magnum-drum-finish.png')),np.asarray(Image.open(OUT/'magnum-body-baseColor.png'))[8:248,5:271]),'Exact source magnum_finish Rect2i(5,8,266,240) pixels; no resampling')
low=v[:,:3].min(0);high=v[:,:3].max(0);source_low=np.array(provenance['sourceBodyBoundsMin']);source_high=np.array(provenance['sourceBodyBoundsMax']);check(np.allclose(low,[source_low[0],source_low[1],-source_high[2]],atol=1e-7) and np.allclose(high,[source_high[0],source_high[1],-source_low[2]],atol=1e-7),'Recorded original receiver/barrel/grip bounds retained')
drum=loaded['magnum-cylinder'];offered=loaded['magnum-offer'];body=drum['DrumBody'][0];center=np.asarray(fit['cylinderCenter']);radial=np.linalg.norm(body[:,:2]-center[:2],axis=1)
check(abs(radial.max()-.043*.66)<2e-8 and abs(body[:,2].min()-.083)<2e-8 and abs(body[:,2].max()-.137)<2e-8,'Actual fitted21-side drum radius/depth/center')
check(len([p for p in drum if p.startswith('LiveRound') and '.Primer'not in p])==7 and len([p for p in drum if p.endswith('.Primer')])==7,'Seven independently visible live rounds and seven true primer children')
for index in range(7):
    home=np.asarray(fit['liveRoundHomes'][index]);round_v=drum['LiveRound'+str(index)][0];primer=drum['LiveRound'+str(index)+'.Primer'][0];xy=np.linalg.norm(round_v[:,:2]-home[:2],axis=1)
    check(abs(xy.max()-.008*.66)<2e-8 and abs((round_v[:,2].min()+round_v[:,2].max())*.5-home[2])<2e-8,'Live source chamber home/radius '+str(index))
    check(abs(primer[:,2].min()-.0767)<2e-8 and abs(primer[:,2].max()-.077)<2e-8,'Nested cartridge quaternion correctly composed for primer '+str(index))
    prefix='OfferRound'+str(index);check(all(prefix+'.'+p in offered for p in ['Case','Tip','Rim','TailRim']),'Complete offered cartridge/rim source parts '+str(index))
    case=offered[prefix+'.Case'][0];tip=offered[prefix+'.Tip'][0];check(abs(case[:,2].min())<1e-8 and abs(case[:,2].max()-.045)<1e-8 and abs(tip[:,2].min()-.045)<1e-8 and abs(tip[:,2].max()-.06)<1e-8,'Source forward case/tip length preserved '+str(index))
    check(abs(np.linalg.norm(home[:2]-center[:2])-.027*.66)<2e-8,'Seven source chambers fitted on exact ring '+str(index))
check(fit['modelScale']==.84 and fit['muzzle']==[0,.065,.392] and fit['cylinderOpenRoot']==[-.1,0,0] and fit['offerRestRoot']==[-.18,.095,-.13],'Original unbaked scale/muzzle/open/offer landmarks')
check(fit['offerHeldWorldScale']==1 and fit['offerRestWorldScale']==.84,'Actual source held-offer global basis versus resting model scale')
rear=np.array(fit['rearSight']);front=np.array(fit['frontSight']);target=np.array(fit['muzzle'])+[0,0,45];line=(front-rear)/(front[2]-rear[2]);check(np.linalg.norm(np.cross(line,target-rear))<1e-9,'Original sight references zero onto unchanged bore ray at45m')
check(fit['ejector'] is None,'No invented moving ejector; source operation intentionally inert')
report=dict(passed=checks,source='ORIGINAL Opus R23',assets=counts,totalTriangles=sum(x['triangles']for x in counts.values()),capacity=7,modelScale=.84,sourceFinishPixelCrop=True,existingPayloadFilesPreserved=len(json.loads((HERE/'preserved-existing.json').read_text())),engineRun=False,blenderRun=False,gpuRun=False,runtimeEdited=False)
(HERE/'checks.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
