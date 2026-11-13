from pathlib import Path
import subprocess,json,hashlib
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/AvatarR4';E=R/'docs/agent-runs/20261104-three-chats/unity/AvatarR4';F=S/'frozen';out=S/'frozen-validation';out.mkdir(exist_ok=True)
sources=[S/'consumed/unity/Assets/Ashfall/Core/Contracts.cs']+sorted((F/'unity/Assets/Ashfall/Modules/Avatar').rglob('*.cs'));results=[]
for name in ['AvatarTests','AvatarR2Tests','AvatarR3Tests','IndependentTests','PriorR2Independent','NullPoseAcceptance','AvatarR4Tests']:
 test=S/(name+'.cs') if (S/(name+'.cs')).exists() else F/'unity/Tools/Modules/Avatar'/(name+'.cs');exe=out/(name+'.exe')
 args=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+list(map(str,sources))+[str(test)]+([str(F/'unity/Tools/Modules/Avatar/AvatarR4Fixtures.cs')] if name=='AvatarR4Tests' else [])
 r=subprocess.run(args,capture_output=True,text=True);log='COMMAND '+' '.join('"'+a+'"' for a in args)+'\n'+r.stdout+r.stderr+'\nCOMPILE EXIT '+str(r.returncode)+'\n';assert r.returncode==0,log
 r=subprocess.run([str(exe)],cwd=S,capture_output=True,text=True);log+='COMMAND "'+str(exe)+'"\n'+r.stdout+r.stderr+'\nRUN EXIT '+str(r.returncode)+'\n';(E/('frozen-'+name+'.log')).write_text(log);assert r.returncode==0,log;results.append(dict(test=name,exit=r.returncode,result=r.stdout.splitlines()[-1]));print(name,results[-1]['result'])
(E/'frozen-results.json').write_text(json.dumps(results,indent=2))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
rows=[]
for p in (F/'unity/Assets/Ashfall/Modules/Avatar').rglob('*.cs'):
 current=R/p.relative_to(F);rows.append(dict(path=str(current),frozen=sha(p),current=sha(current),matches=sha(p)==sha(current)))
(E/'production-frozen-match.json').write_text(json.dumps(rows,indent=2));assert all(x['matches'] for x in rows)
