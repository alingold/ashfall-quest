from pathlib import Path
import hashlib,json,math,struct,subprocess
import numpy as np
import prepare as p
T=p.T;R=p.R;O=p.O
def slerp(a,b,t):
 a=np.array(a,float);a/=np.linalg.norm(a);b=np.array(b,float);b/=np.linalg.norm(b);dot=float(a@b)
 if dot<0:b=-b;dot=-dot
 if dot>.9995:q=a+(b-a)*t;return q/np.linalg.norm(q)
 angle=math.acos(max(-1,min(1,dot)));return (a*math.sin((1-t)*angle)+b*math.sin(t*angle))/math.sin(angle)
def expected(data,rotations):
 worlds=[]
 for i,node in enumerate(data['nodes']):
  m=np.eye(4);m[:3,:3]=p.rotation(rotations.get(node['name'],node['rotation']))@np.diag(node['scale']);m[:3,3]=node['position'];par=node['parent'];worlds.append(m if par<0 else worlds[par]@m)
 matrices=[worlds[j['node']]@np.array(j['inverseBind']).reshape(4,4) for j in data['joints']];bake=np.array(data['bake']).reshape(4,4)
 vertices=[];normals=[];min_det=1.
 for part in data['parts']:
  v=np.array(part['positions']).reshape(-1,3);n=np.array(part['normals']).reshape(-1,3);weights=np.array(part['weights']).reshape(-1,4);joints=np.array(part['jointIndices']).reshape(-1,4)
  # Scalar per-influence skin oracle, independent of exporter's vectorized blend.
  for vi in range(len(v)):
   point=np.zeros(4);normal=np.zeros(3);combined=np.zeros((4,4))
   for joint,weight in zip(joints[vi],weights[vi]):
    m=matrices[joint];point+=weight*(m@np.r_[v[vi],1]);normal+=weight*(m[:3,:3]@n[vi]);combined+=weight*m
   point=bake@point;normal=bake[:3,:3]@normal;normal/=np.linalg.norm(normal);vertices.append(point[:3]);normals.append(normal);min_det=min(min_det,float(np.linalg.det(combined[:3,:3])))
 assert min_det>0 # No tangent handedness change under authored deformation.
 return np.array(vertices),np.array(normals),worlds
cases=0;oracles=[]
for side in ['left','right']:
 data,encoded,row=p.build(side,publish=False);assert (O/(side+'-articulation.json')).read_bytes()==encoded
 raw=(O/(side+'.meshbin')).read_bytes();nv,ni,_=struct.unpack_from('<iii',raw,8);baseline=np.frombuffer(raw,'<f4',nv*8,20).reshape(nv,8)
 rest,normal,_=expected(data,{})
 assert np.max(abs(rest-baseline[:,:3]))<1e-7;cases+=len(rest)
 controls=[('freeOpen','',False,False,0,0),('freeGrip','',False,False,1,0),('freeTrigger','',False,False,0,1),('rifle','mk18',False,True,.1,.9),('pistol','px4',False,True,0,0),('revolver','magnum',False,True,0,0),('sword','cyberblade',True,True,0,0),('knife','knife',True,True,0,0),('matadors','matadors',False,True,0,0)]
 wrist=next(i for i,n in enumerate(data['nodes']) if n['name'].startswith('Wrist_'));rest_wrist=np.array(data['bake']).reshape(4,4)@expected(data,{})[2][wrist]
 for name,id,sword,holding,grip,trigger in controls:
  rotations={}
  closed='shaft' if sword else 'pistol' if id in ['px4','1911','tec9','mp9','magnum','matadors'] else 'grip'
  for n in data['nodes']:
   if not n['animated']:continue
   index=n['index'];requested=trigger if index else grip
   amount=requested if not holding else (.80 if index else 1.05) if id=='matadors' else (.86 if index else 1.12) if sword or id in ['magnum','tec9'] else (.80 if index else 1.05) if id=='knife' else (.62 if index else .85) if id in ['px4','1911','mp9'] else (.54 if index else .74)
   rotations[n['name']]=slerp(n['opened'],n[closed],amount)
  vertices,normals,worlds=expected(data,rotations)
  assert np.isfinite(vertices).all() and np.allclose(np.linalg.norm(normals,axis=1),1,atol=1e-7)
  assert np.max(abs(np.array(data['bake']).reshape(4,4)@worlds[wrist]-rest_wrist))<1e-10
  oracles.append(dict(name=name,hand=0 if side=='left' else 1,profile=id,sword=sword,holding=holding,grip=grip,trigger=trigger,wristNode=wrist,wristWorld=worlds[wrist].reshape(-1).tolist(),positions=vertices.reshape(-1).tolist(),normals=normals.reshape(-1).tolist()))
  cases+=len(vertices)*2+1
 # Finger trigger and grip affect different chains; neither moves wrist.
 opens=next(x for x in oracles if x['hand']==(0 if side=='left' else 1) and x['name']=='freeOpen');grips=next(x for x in oracles if x['hand']==opens['hand'] and x['name']=='freeGrip')
 assert np.max(abs(np.array(opens['positions'])-grips['positions']))>.03
out=Path('D:/ashfall-r6-staging/hand-articulation-next');out.mkdir(parents=True,exist_ok=True)
(out/'oracles.json').write_text(json.dumps(dict(cases=oracles),separators=(',',':'))+'\n')
print('PASS',cases,'source scalar skin/normal/wrist/rest observations;18 reference pose oracles generated.')
editor=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:exe','/out:'+str(out/'RulesCases.exe'),str(T/'RulesCases.cs'),str(R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons/R23GlovePoseRules.cs')]
rsp=out/'rules.rsp';rsp.write_text('\n'.join('"'+a+'"' for a in args));q=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(out/'rules-compile.log').write_text(q.stdout+q.stderr);assert q.returncode==0,q.stdout+q.stderr
q=subprocess.run([str(out/'RulesCases.exe')],capture_output=True,text=True);(out/'rules-cases.log').write_text(q.stdout+q.stderr);assert q.returncode==0,q.stdout+q.stderr;print(q.stdout)
for rel,sha in p.inputs.items():assert p.sha(p.S/rel)==sha
(out/'cpu-result.json').write_text(json.dumps(dict(sourceSkinObservations=cases,oracleCases=len(oracles),oracleSha256=hashlib.sha256((out/'oracles.json').read_bytes()).hexdigest(),nativeExecuted=False,sourceInputsUnchanged=True),indent=2)+'\n')
