from pathlib import Path
import hashlib,json,subprocess
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/PreparedCapsuleRouteR4';O=S/'run05';D=R/'docs/agent-runs/20261104-three-chats/unity/PreparedCapsuleRouteR4';C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');res=[]
for name,body in [('RouteCtor03','new Ashfall.Integration.FlightMotor.ReservedCapsuleRoute()'),('ParticipantCtor03','new Ashfall.Integration.MotionRoutes.PreparedRouteUpdate()')]:
 p=O/(name+'.cs');p.write_text('class Attack {static object Try(){return '+body+';}}');rsp=O/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warnaserror+','/target:library','/out:"'+str(O/(name+'.dll'))+'"']+['/reference:"'+str(O/(n+'.dll'))+'"' for n in ['Core','Flight','World','Routes']]+['"'+str(p)+'"']));v=subprocess.run([str(C),'/noconfig','@'+str(rsp)],capture_output=True,text=True);text=v.stdout+v.stderr;(O/(name+'.log')).write_text(text);assert v.returncode!=0 and ('CS1729' in text or 'CS0122' in text or 'CS0143' in text);res.append(dict(name=name,exit=v.returncode,output=text))
(D/'negative-api.json').write_text(json.dumps(res,indent=2))
inputs=json.loads((D/'inputs.json').read_text());drift=[x for x in inputs if hashlib.sha256(Path(x['source']).read_bytes()).hexdigest()!=x['sha256']];(D/'final-drift.json').write_text(json.dumps(drift,indent=2));assert not drift
# Keep final commands/inputs and evidence by revision as well as the handoff names.
for name in ['commands.json','inputs.json','drift.json','SHA256-manifest.json']:(O/name).write_bytes((D/name).read_bytes())
policies=[]
for x in inputs:
 p=Path(x['snapshot']);src=x['source']
 if p.suffix in ['.md','.json']:
  raw=p.read_bytes().decode('utf-8-sig',errors='replace');row=dict(source=src,sha256=x['sha256'],characters=len(raw))
  if p.suffix=='.json':
   data=json.loads(raw);row['jsonKind']=type(data).__name__;row['entries']=len(data)
  policies.append(row)
(D/'coordination-read.json').write_text(json.dumps(policies,indent=2))
paths=[R/'unity/Assets/Ashfall/Integration/FlightMotor/ReservedCapsuleRoute.cs']+list((R/'unity/Assets/Ashfall/Integration/MotionRoutes').rglob('*'))+list((R/'unity/Tools/Integration/PreparedCapsuleRoute').rglob('*'))+list(S.rglob('*'))
(D/'final-SHA256-manifest.json').write_text(json.dumps([dict(path=str(p.relative_to(R)),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in paths if p.is_file()],indent=2))
print('Negative public construction gates 2 PASS; final pinned drift 0; seal complete')


