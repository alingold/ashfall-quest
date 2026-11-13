from pathlib import Path
import hashlib,importlib.util,json,math,shutil,uuid,struct
import numpy as np
T=Path(__file__).resolve().parent;R=T.parents[3];S=R/'quest-r24/source';O=R/'unity/Assets/StreamingAssets/PlayableR23/HandsWeapons'
manifest=R/'campaign-staging/20261104-three-chats/world/morning-r5/r5/delivery/SOURCE-CONTENT.json'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(manifest)=='d4fb1053acfbaa7f3eb285879472bfdd136e09bf2f7e1f2dc6478f23d8f12bc7'
pins={x['path']:x['sha256']for x in json.loads(manifest.read_text())['files']};inputs={}
def verify(name):
 p=S/name;digest=sha(p);assert pins[name]==digest,name;inputs[name]=digest;return p
for name in ['explosive_weapon','explosive_projectile','explosive_effect','explosive_finish_064','downloaded_heavy_085','smaw_tube_065','revive_paddles','paddle_visual_089','field_item_models','paddle_lightning097','revive_discharge_fx_091','weapon_rack','audio_bank','reference_audio_099','sound_cues099','model_scale_084','playtest_fit_088','weapon']:
 verify('scripts/'+name+'.gd')
for name in ['assets/ui/catalog094/weapon_27.png','assets/data/one_star_050.json','assets/data/weapon_rarity_r6.json','assets/models/download_smaw/model.glb','assets/models/download_smaw/asset-provenance.json','assets/models/download_smaw/ATTRIBUTION.txt']:
 verify(name)
spec=importlib.util.spec_from_file_location('primitive',T.parent/'MagnumAssets/prepare.py');p=importlib.util.module_from_spec(spec);spec.loader.exec_module(p);p.OUT=O
# Existing GLB converter applies node transforms and original texture routes, no decimation.
ns={'__file__':str(T.parent/'HandsWeapons/prepare.py')};exec((T.parent/'HandsWeapons/prepare.py').read_text().split('OUT.mkdir(parents=True')[0],ns);ns['SRC']=S;ns['OUT']=O;ns['record']=lambda path:verify(str(path.relative_to(S)).replace('\\','/'));ns['convert'](S/'assets/models/download_smaw/model.glb','smaw')
shutil.copyfile(S/'assets/models/download_smaw/ATTRIBUTION.txt',O/'smaw-ATTRIBUTION.txt');shutil.copyfile(S/'assets/ui/catalog094/weapon_27.png',O/'smaw-icon.png')
def box(size):
 h=np.array(size)*.5;v=[];n=[]
 for axis in range(3):
  for sign in [-1,1]:
   normal=np.eye(3)[axis]*sign;u=np.eye(3)[(axis+1)%3];w=np.eye(3)[(axis+2)%3];c=normal*h[axis];q=[c+u*h[(axis+1)%3]*a+w*h[(axis+2)%3]*b for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]]
   for j in ([0,2,1,0,3,2]if sign>0 else[0,1,2,0,2,3]):v.append(q[j]);n.append(normal)
 return np.array(v),np.array(n),np.zeros((len(v),2)),np.arange(len(v),dtype=np.int32)
def rounded(size,bevel):
 h=np.array(size)*.5;radius=min(max(bevel,.00001),min(h)*.8);inner=h-radius;v=[];n=[]
 for axis in range(3):
  u=(axis+1)%3;w=(axis+2)%3;ug=[-h[u],-h[u]+radius,h[u]-radius,h[u]];wg=[-h[w],-h[w]+radius,h[w]-radius,h[w]]
  for sign in [-1,1]:
   for a in range(3):
    for b in range(3):
     corners=[]
     for x,y in [(a,b),(a+1,b),(a+1,b+1),(a,b+1)]:
      q=np.zeros(3);q[axis]=sign*h[axis];q[u]=ug[x];q[w]=wg[y];core=np.clip(q,-inner,inner);normal=p.unit(q-core);corners.append((core+normal*radius,normal))
     for j in ([0,2,1,0,3,2]if sign>0 else[0,1,2,0,2,3]):v.append(corners[j][0]);n.append(corners[j][1])
 return np.array(v),np.array(n),np.zeros((len(v),2)),np.arange(len(v),dtype=np.int32)
materials=[p.material('909b9d',.86,.28),p.material('323c4b',.48,.45),p.material('ce3443',.48,.36),p.material('10191d',.05,.88)]
paddle=p.Asset('revive-paddle',materials);rot=p.rx(math.pi/2)
def add(name,mesh,mi,at=(0,0,0),r=None):
 r=np.eye(3)if r is None else r;paddle.add(name,mesh,mi,rot@r,np.array(at)@rot.T,fit=(np.array([1.3]*3),np.zeros(3)),recipe='R5 paddle_visual_089 +field_item_models exact authored primitive')
def rb(name,size,at,mi,bevel):add(name,rounded(size,bevel),mi,at)
for row in [('Housing',(.096,.028,.135),(0,0,0),1,.012),('Electrode',(.087,.006,.125),(0,-.016,0),0,.008),('Status',(.067,.003,.010),(0,.015,-.044),2,.002),('Band',(.099,.006,.138),(0,-.010,0),2,.010),('Handle',(.080,.025,.035),(0,.078,.018),3,.010)]:rb(*row)
for side in [-1,1]:
 rb('Support'+str(side),(.012,.055,.035),(side*.034,.044,.018),1,.005);rb('Frame'+str(side),(.009,.045,.072),(side*.043,.035,-.008),1,.004)
 for name,radius,height,at,mi in [('Button',.010,.003,(side*.031,.059,-.024),2),('Inset',.005,.004,(side*.031,.061,-.024),0)]:add(name+str(side),p.cylinder(radius,height,24),mi,at)
 for z in [-.048,.048]:add('Fastener',p.cylinder(.003,.002,24),0,(side*.037,.015,z))
for i in range(9):rb('Rib'+str(i),(.002,.026,.038),(-.032+i*.008,.078,.018),1,.001)
add('Grommet',p.torus(.0065,.0095,24,6),1,(0,.078,.038),p.rx(math.pi/2));report=[paddle.write()]
rocket=p.Asset('smaw-round',[p.material('39483e',.65,.58),p.material('d23f86',.5,.43),p.material('29363b',.4,.60)])
ry=np.diag([-1,1,-1])
def rr(name,mesh,mi,at=(0,0,0),r=None):
 r=np.eye(3)if r is None else r;rocket.add(name,mesh,mi,ry@r,np.array(at)@ry.T,recipe='R5 explosive_finish_064.rocket; root RyPI baked once')
rr('Body',p.cylinder(.032,.23,16),0,(0,0,-.005),p.rx(math.pi/2))
for z in [-.10,-.015,.085]:rr('Ring',p.torus(.0305,.0345,20,6),1,(0,0,z),p.rx(math.pi/2))
# Godot cone: rings1, side degenerates retained, zero-radius top has NO cap.
v,n,uv,idx=p.cylinder(.032,.10,16);fr=(v[:,1]+.05)/.1;isSide=np.arange(len(v))<3*17;v[isSide,0]*=(1-fr[isSide]);v[isSide,2]*=(1-fr[isSide]);v[~isSide & (v[:,1]>0),0]=0;v[~isSide & (v[:,1]>0),2]=0;n[isSide]=np.array([p.unit([x,.32,z])for x,y,z in n[isSide]])
idx=np.concatenate((idx[:16*4*3],idx[(16*4+16)*3:]));rr('Nose',(v,n,uv,idx),1,(0,0,.16),p.rx(math.pi/2))
for i in range(4):a=math.tau*i/4;rr('Fin'+str(i),box((.018,.003,.065)),1,(math.cos(a)*.034,math.sin(a)*.034,-.10),p.rz(a))
rr('Tail',p.cylinder(.023,.003,16),2,(0,0,-.1225),p.rx(math.pi/2));rr('TailRing',p.torus(.022,.030,20,6),1,(0,0,-.124),p.rx(math.pi/2));report.append(rocket.write())
# Arrow rods keep source weapon._rod default rings4; actual front-loading cue.
def align_up(direction):
 d=p.unit(direction);a=np.cross(np.array([0.,1.,0.]),d);c=d[1]
 if c<-.999999:return p.rx(math.pi)
 k=np.array([[0,-a[2],a[1]],[a[2],0,-a[0]],[-a[1],a[0],0]])
 return np.eye(3)+k+k@k/(1+c)
arrow=p.Asset('smaw-arrow',[p.material('ffd17f',.1,.55)])
for name,a,b in [('Stem',[0,0,0],[0,0,.24]),('Left',[0,0,.24],[-.025,0,.20]),('Right',[0,0,.24],[.025,0,.20])]:
 a=np.array(a);b=np.array(b);arrow.add(name,p.cylinder(.0025,float(np.linalg.norm(b-a)),8,4),0,align_up(b-a),(a+b)*.5,recipe='R5 weapon._rod front-loading cue')
report.append(arrow.write())
# Source default CylinderMesh plume: radial64/rings4, .012 top/.09 bottom.
v,n,uv,idx=p.cylinder(.09,.8,64,4);sideCount=6*65;fraction=(.4-v[:,1])/.8;radius=.012+(.09-.012)*fraction
v[:sideCount,0]*=radius[:sideCount]/.09;v[:sideCount,2]*=radius[:sideCount]/.09
v[sideCount:sideCount+66,0]*=.012/.09;v[sideCount:sideCount+66,2]*=.012/.09
n[:sideCount]=np.array([p.unit([x,(.09-.012)/.8,z])for x,y,z in n[:sideCount]])
plume=p.Asset('smaw-plume',[p.material('ffd978',0,1)]);plume.add('Source unlit flight plume',(v,n,uv,idx),0,p.rx(math.pi/2),(0,0,.35),recipe='R5 explosive_projectile exact plume dimensions; rendered unlit by presenter');report.append(plume.write())
for attribution in ['reference099','reference0992','reference_r21']:
 shutil.copyfile(verify('assets/audio/'+attribution+'/ATTRIBUTION.txt'),O/('missing-items-audio-'+attribution+'-ATTRIBUTION.txt'))
audios={'smaw-launch':'assets/audio/sfx/rocket_launch_00.wav','smaw-flight':'assets/audio/sfx/rocket_flight_00.wav','smaw-impact':'assets/audio/sfx/rocket_impact_00.wav','revive-charge':'assets/audio/reference_r21/revive_charge_full.wav','revive-ready':'assets/audio/reference0992/revive_ready_full0992.wav','revive-discharge':'assets/audio/reference099/isolated_revive_discharge_00.wav'}
for name,src in audios.items():shutil.copyfile(verify(src),O/(name+'.wav'))
(O/'smaw-fit.json').write_text(json.dumps(dict(version=1,modelScale=1.6,modelOffset=[0,.111,-.905],muzzle=[0,.045,.85],grip=[0,-.105,.55],glovePhysicalDelta=[0,.0254,-.0508],offer=[0,.045,1.15-.3048/1.6],insert=[0,.045,.96],rocketSpeed=200,initialLoaded=1,localReloads=2,rarityDamage=[60,84,109,134,160]),indent=2))
(O/'missing-items-provenance.json').write_text(json.dumps(dict(frozenManifestSha256=sha(manifest),inputs=inputs,assets=report,audio=audios,limits='CPU translation only; no native/GPU/headset evidence. Source filmic/PBR appearance not claimed pixel-identical.'),indent=2))
for file in O.iterdir():
 if file.name.startswith(('smaw','revive-','missing-items-'))and file.suffix!='.meta'and not file.with_name(file.name+'.meta').exists():file.with_name(file.name+'.meta').write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
print([(a['name'],a['triangles'])for a in report]);print('Verified frozen R5 inputs:',len(inputs))
