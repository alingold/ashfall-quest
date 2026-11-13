from pathlib import Path
import hashlib,json,subprocess
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/NativeRootOutcomeR1';D=R/'docs/agent-runs/20261104-three-chats/unity/NativeRootOutcomeR1'
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');E=Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
# Capture once; never reselect dependencies during this run.
F=S/'capture05';F.mkdir(exist_ok=False);D=D/'run05';D.mkdir(exist_ok=False);records=[]
def freeze(p):
 t=F/'sources'/p.relative_to(R);t.parent.mkdir(parents=True,exist_ok=True);old=S/'capture01/sources'/p.relative_to(R); data=old.read_bytes() if old.exists() and '/Tools/Integration/NativeRootOutcome/' not in p.as_posix() and p.name!='NativeRootPublication.cs' else p.read_bytes();t.write_bytes(data);records.append(dict(path=str(p),snapshot=str(t),sha256=sha(t),bytes=t.stat().st_size));return t
player=R/'unity/Assets/Ashfall/Modules/Player'
sources=[freeze(R/'unity/Assets/Ashfall/Core/Contracts.cs')]+[freeze(p) for p in [player/p.name for p in sorted((S/'capture01/sources/unity/Assets/Ashfall/Modules/Player').glob('*.cs'))]]
publication=freeze(player/'Engine/NativeRootPublication.cs');motor=freeze(player/'Engine/UnityExclusivePlayerMotor.cs');test=freeze(R/'unity/Tools/Integration/NativeRootOutcome/AdmissionTests.cs');native=freeze(R/'unity/Tools/Integration/NativeRootOutcome/NativeFixtures.cs');freeze(Path(__file__))
refs=[]
for p in [E/'NetStandard/ref/2.1.0/netstandard.dll']+[E/('Managed/UnityEngine/UnityEngine.'+n+'Module.dll') for n in ['Core','Physics','Mathematics']]:
 t=F/'references'/p.name;t.parent.mkdir(exist_ok=True);oldref=S/'capture01/references'/p.name;t.write_bytes(oldref.read_bytes() if oldref.exists() else p.read_bytes());refs.append(t);records.append(dict(path=str(p),snapshot=str(t),sha256=sha(t),bytes=t.stat().st_size))
(D/'inputs.json').write_text(json.dumps(records,indent=2));commands=[];results=[]
def compile(name,src,reference=(),engine=False,exe=False):
 out=F/(name+('.exe' if exe else '.dll'));rsp=F/(name+'.rsp')
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/target:'+('exe' if exe else 'library'),'/out:"'+str(out)+'"']
 if engine:args+=['/nostdlib+','/define:UNITY_6000_0_OR_NEWER']
 args+=['/reference:"'+str(p)+'"' for p in reference]+['"'+str(p)+'"' for p in src];rsp.write_text('\n'.join(args));cmd=[str(C),'/noconfig','@'+str(rsp)];commands.append(cmd)
 c=subprocess.run(cmd,capture_output=True,text=True);(F/(name+'.compile.log')).write_text(c.stdout+c.stderr);results.append(dict(name=name,compile=c.returncode));print(name,c.returncode,c.stdout[-1500:])
 if c.returncode:raise RuntimeError(name)
 return out
def run(name,p,expected=0):
 commands.append([str(p)]);c=subprocess.run([str(p)],capture_output=True,text=True);(F/(name+'.run.log')).write_text(c.stdout+c.stderr);results.append(dict(name=name,exit=c.returncode,output=c.stdout+c.stderr,expected=expected));print(name,c.returncode,c.stdout[-200:]);return c.returncode
try:
 core=compile('Core',[sources[0]]);owner=compile('Player',sources[1:]+[publication],[core]);consumer=compile('PublicAdmission',[test],[core,owner],exe=True);assert run('PublicAdmission',consumer)==0
 # Each mutant is compiled and exercised by the unchanged external public consumer.
 original=publication.read_text()
 mutants={'expiry-seconds':('seconds>.1f','seconds>1f'),'forged-pivot':('expected.PivotCorrection.X==p.PivotCorrection.X','true'),'translation-bound':('Length(p.Translation)<=10','Length(p.Translation)<=100'),'external-owner':('!RootMotionSafety.Approve(p,false,null).Accepted','!RootMotionSafety.Approve(new RootMotionProposal { RootPosition=p.RootPosition, HeadPosition=p.HeadPosition, Translation=p.Translation, PivotCorrection=p.PivotCorrection, YawRadians=p.YawRadians, OwnsTranslation=true },false,null).Accepted')}
 for name,(a,b) in mutants.items():
  assert a in original
  m=F/('mutant-'+name+'.cs');m.write_text(original.replace(a,b,1));dll=compile('Mutant-'+name,sources[1:]+[m],[core]);x=compile('Consumer-'+name,[test],[core,dll],exe=True);assert run('Mutant-'+name,x)!=0
 nativecore=compile('NativeCore',[sources[0]],refs,engine=True)
 compile('NativeOwner',sources[1:]+[publication,motor],refs+[nativecore],engine=True)
 compile('NativeFixtures',[native],refs+[nativecore,F/'NativeOwner.dll'],engine=True)
finally:
 (D/'commands.json').write_text(json.dumps(commands,indent=2));(D/'results.json').write_text(json.dumps(results,indent=2))
 drift=[dict(path=e['path'],expected=e['sha256'],actual=sha(Path(e['path']))) for e in records if sha(Path(e['path']))!=e['sha256']];(D/'drift.json').write_text(json.dumps(drift,indent=2))




