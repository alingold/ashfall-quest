from pathlib import Path
import subprocess,shutil,json
R=Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/FlightMotorR6';D=R/'docs/agent-runs/20261104-three-chats/unity/FlightMotorR6';base=S/'run02';CSC=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe');records=[]
for label,file,old,new in [('heading','PreparedMotion.cs','f.Forward=Rotate(f.Forward,yaw);','f.Forward=Rotate(f.Forward,0);'),('gravity','FlightMachine.cs','velocity.Y-16.2f*f.DeltaSeconds','velocity.Y-32.4f*f.DeltaSeconds')]:
 q=S/('mutation-'+label);q.mkdir(exist_ok=False)
 for p in base.glob('*.dll'):shutil.copyfile(p,q/p.name)
 sources=[]
 for p in (base/'snapshot/unity/Assets/Ashfall/Modules/Flight').glob('*.cs'):
  t=q/p.name;s=p.read_text();
  if p.name==file:assert old in s;s=s.replace(old,new)
  t.write_text(s);sources.append(t)
 args=[str(CSC),'/nologo','/langversion:5','/warnaserror+','/target:library','/out:'+str(q/'Ashfall.Flight.dll'),'/reference:'+str(q/'Ashfall.Core.dll')]+list(map(str,sources))
 p=subprocess.run(args,capture_output=True,text=True);assert p.returncode==0,p.stdout+p.stderr
 shutil.copyfile(base/'R6.exe',q/'R6.exe');r=subprocess.run([str(q/'R6.exe')],capture_output=True,text=True)
 (D/('mutation-'+label+'.log')).write_text(subprocess.list2cmdline(args)+'\n'+p.stdout+p.stderr+'\nTEST EXIT '+str(r.returncode)+'\n'+r.stdout+r.stderr)
 records.append(dict(mutation=label,compile=p.returncode,test=r.returncode,detected=r.returncode==1));print(label,r.returncode,r.stdout[-250:]);assert r.returncode==1
(D/'mutations.json').write_text(json.dumps(records,indent=2))
