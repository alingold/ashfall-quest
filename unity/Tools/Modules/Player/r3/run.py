from pathlib import Path
import subprocess,json,hashlib
r=Path('C:/Users/aling/Documents/ashfall'); o=r/'campaign-staging/20261104-three-chats/unity/Player/r3'; p=r/'unity/Assets/Ashfall/Modules/Player'; docs=r/'docs/agent-runs/20261104-three-chats/unity/Player/r3'
suites={'BuilderTests':o/'inputs/PlayerTests.cs','R2Tests':o/'inputs/R2Tests.cs','FreshTests':o/'inputs/FreshTests.cs','R3Tests':r/'unity/Tools/Modules/Player/r3/R3Tests.cs','Adversarial':o/'inputs/Adversarial.cs'}
results=[];commands=[]
for name,test in suites.items():
 sources=[o/'inputs/Contracts.cs']+sorted(p.glob('*.cs'))+[test]
 rsp=o/(name+'.rsp');exe=o/(name+'.exe')
 rsp.write_text('/nologo\n/langversion:5\n/warnaserror+\n/target:exe\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(f)+'"' for f in sources))
 cmd=['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','@'+str(rsp)];commands.append(cmd)
 c=subprocess.run(cmd,capture_output=True,text=True);(o/(name+'-compile.txt')).write_text(c.stdout+c.stderr)
 result={'name':name,'compileExit':c.returncode}
 if c.returncode==0:
  commands.append([str(exe)]);x=subprocess.run([str(exe)],capture_output=True,text=True);(o/(name+'-results.txt')).write_text(x.stdout+x.stderr);result.update(testExit=x.returncode,output=x.stdout)
 results.append(result)
(docs/'results.json').write_text(json.dumps(results,indent=2));(docs/'commands.json').write_text(json.dumps(commands,indent=2))
print("\n".join(x["name"]+": compile="+str(x["compileExit"])+", test="+str(x.get("testExit"))+"; "+x.get("output", "").splitlines()[-1] for x in results))
raise SystemExit(0 if all(x["compileExit"]==0 and x.get("testExit")==0 for x in results) else 1)
