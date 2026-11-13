from pathlib import Path
import hashlib,json,shutil,subprocess,sys
R=Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/PreparedCapsuleRouteR4';O=S/(sys.argv[1] if len(sys.argv)>1 else 'run01');O.mkdir(exist_ok=False);D=R/'docs/agent-runs/20261104-three-chats/unity/PreparedCapsuleRouteR4';inputs=[];results=[]
prior=json.loads((D/'inputs.json').read_text()) if (D/'inputs.json').exists() else []
def pin(p):
 p=Path(p);old=next((x for x in prior if x['source']==str(p)),None);b=Path(old['snapshot']).read_bytes() if old and 'PreparedCapsuleRoute' not in str(p) and 'MotionRoutes' not in str(p) and p.name!='ReservedCapsuleRoute.cs' else p.read_bytes();q=O/'f'/(hashlib.sha256(str(p).encode()).hexdigest()[:12]+'-'+p.name);q.parent.mkdir(exist_ok=True);q.write_bytes(b);inputs.append(dict(source=str(p),snapshot=str(q),sha256=hashlib.sha256(b).hexdigest()));return q
parts={}
for name,folder in [('Core','Core'),('Flight','Modules/Flight'),('World','Modules/World')]:parts[name]=[pin(p) for p in (R/'unity/Assets/Ashfall'/folder).glob('*.cs')]
parts['Routes']=[pin(R/p) for p in ['unity/Assets/Ashfall/Integration/FlightMotor/ReservedCapsuleRoute.cs','unity/Assets/Ashfall/Integration/MotionRoutes/SyntheticCapsuleRoutes.cs']]
test=pin(R/'unity/Tools/Integration/PreparedCapsuleRoute/PublicTests.cs');native=pin(R/'unity/Tools/Integration/PreparedCapsuleRoute/NativeFixture.cs')
retained=[pin(R/p) for p in ['unity/Tools/Modules/Flight/FlightTests.cs','unity/Tools/Modules/Flight/R2Tests.cs']]
context=[R/'docs'/p for p in ['TWO-CHAT-20-AGENT-OVERRIDE.md','HILL-CLIMB-ORCHESTRATION.md','OVERNIGHT-AGENT-PLAN-2026-11-03.md','AGENT-DISPATCH-BRIEFS-2026-11-03.md','agent-runs/20261104-0020/preflight.json']]
context+=list((R/'unity/Documentation').glob('*.md'))
base=R/'docs/agent-runs/20261104-three-chats/unity'
context+=list(base.glob('accepted-neutral-v*.json'))+[base/'GAMEPLAY-CHECKPOINT-v1.md',base/'phase21/ROOT-REPORT.md',base/'phase21/source-index.json',base/'phase27/ROOT-REPORT.md',base/'CertifiedMotionPublicationR4/API-PROPOSAL.md',base/'reviews/NativeRootOutcomeReviewR1/REPORT.md']
context+=list((R/'unity/Assets/Ashfall/Integration/WorldRuntime').rglob('*.cs'))
# Actual content owner lives in Modules/World/Engine, not an invented neutral lease API.
context+=list((R/'unity/Assets/Ashfall/Modules/World/Engine').glob('*.cs'))
for p in context:pin(p)
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');pin(C);pin(Path(sys.executable))
refs=[C.parent/x for x in ['System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll']]
for p in refs:pin(p)
(D/'inputs.json').write_text(json.dumps(inputs,indent=2))
def run(name,args):
 p=subprocess.run(list(map(str,args)),capture_output=True,text=True);out=p.stdout+p.stderr;(O/(name+'.log')).write_text(out);results.append(dict(name=name,argv=list(map(str,args)),exit=p.returncode,output=out));(D/'commands.json').write_text(json.dumps(results,indent=2));print(name,p.returncode,out[-600:]);return p.returncode
def compile(name,files,more=[],exe=False):
 out=O/(name+('.exe' if exe else '.dll'));rsp=O/(name+'.rsp');rsp.write_text('\n'.join(['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(out)+'"']+['/reference:"'+str(p)+'"' for p in refs+more]+['"'+str(p)+'"' for p in files]));assert run(name+'-compile',[C,'/noconfig','@'+str(rsp)])==0;return out
try:
 a=compile('Core',parts['Core']);b=compile('Flight',parts['Flight'],[a]);c=compile('World',parts['World'],[a]);r=compile('Routes',parts['Routes'],[a,b,c]);assert run('public',[compile('PublicTests',[test],[a,b,c,r],True)])==0
 for i,f in enumerate(retained):assert run('retained'+str(i),[compile('Retained'+str(i),[f],[a,b],True)])==0
 for name,old,new in [('Expiry','clock>r.Expires','false'),('Geometry','r.GeometryRevision!=geometryRevision','false'),('Actor','!Object.ReferenceEquals(a,r.Actor)','false'),('Bytes','bytes[i]!=canonical[i]','bytes[i]!=canonical[i] && bytes.Length==0'),('Hull','if(Math.Min(a.X,b.X)','if(s.Id==String.Empty && Math.Min(a.X,b.X)'),('Replay', 'if(seen.ContainsKey(c.CommandId))','if(c.CommandId==null)')]:
  files=list(parts['Routes']);flight=b
  if name=='Replay':
   orig=next(p for p in parts['Flight'] if p.name.endswith('-Capsules.cs'));q=O/(name+'.cs');text=orig.read_text();assert old in text;q.write_text(text.replace(old,new).replace('seen.Add(c.CommandId,c.Tick);','seen[c.CommandId]=c.Tick;'));flight=compile('Flight'+name,[p for p in parts['Flight'] if p!=orig]+[q],[a])
  else:
   orig=next(p for p in files if p.name.endswith('-SyntheticCapsuleRoutes.cs'));q=O/(name+'.cs');text=orig.read_text();assert old in text;q.write_text(text.replace(old,new));files=[p for p in files if p!=orig]+[q]
  m=compile('Routes'+name,files,[a,flight,c]);e=compile('Test'+name,[test],[a,flight,c,m],True);assert run('mutant'+name,[e])!=0
 engine=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\Managed\UnityEngine');nr=[engine/'UnityEngine.CoreModule.dll',engine.parent.parent/'UnityReferenceAssemblies/unity-4.8-api/Facades/netstandard.dll']
 for p in nr:pin(p)
 compile('NativeFixture-UNRUN',[native],[a,b,c,r]+nr)
finally:
 (D/'inputs.json').write_text(json.dumps(inputs,indent=2));(D/'drift.json').write_text(json.dumps([x for x in inputs if hashlib.sha256(Path(x['source']).read_bytes()).hexdigest()!=x['sha256']],indent=2))
 (D/'SHA256-manifest.json').write_text(json.dumps([dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in O.rglob('*') if p.is_file()],indent=2))



