from pathlib import Path
import hashlib,json,subprocess,datetime
root=Path.cwd(); stage=root/'campaign-staging/20261104-three-chats/unity/Presentation/r3'; evidence=root/'docs/agent-runs/20261104-three-chats/unity/Presentation/r3'
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
inputs=list((stage/'inputs').rglob('*.cs'))
accepted=json.loads((root/'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json').read_text(encoding='utf-8-sig'))
for row in accepted['sourceChecks']:
 if row['name']=='Persistence':
  p=root/'unity/Assets/Ashfall/Modules/Persistence'/Path(row['path'].replace('\\','/')).name
  assert sha(p)==row['expected'],str(p)
source=[root/'quest-r22/source/scripts/menu.gd',root/'game/scripts/menu.gd',root/'quest-r22/source/scripts/player.gd',root/'game/scripts/player.gd',root/'unity/Documentation/CONTRACTS-v1.md',root/'unity/Documentation/CONTRACTS-v2.md',root/'docs/agent-runs/20261104-three-chats/unity/accepted-neutral-v1.json']
for p in source:
 dest=stage/'inputs'/('source-'+p.parent.parent.name+'-'+p.name if p.name.endswith('.gd') else p.name)
 if not dest.exists():dest.write_bytes(p.read_bytes())
production=list((root/'unity/Assets/Ashfall/Modules/Presentation').rglob('*.cs'))+list((root/'unity/Assets/Ashfall/Integration/PersonalSettings').rglob('*.cs'))
for p in production:
 dest=stage/'candidate'/p.relative_to(root/'unity');dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(p.read_bytes())
neutral=[p for p in (stage/'candidate').rglob('*.cs') if 'Engine' not in p.parts]
core=stage/'inputs/Contracts.cs'
results=[]
for name,test in [('R3',root/'unity/Tools/Integration/PersonalSettings/r3/PersonalSettingsTests.cs'),('Builder',root/'unity/Tools/Modules/Presentation/PresentationTests.cs'),('R2',root/'unity/Tools/Modules/Presentation/PresentationR2Tests.cs'),('Independent',root/'campaign-staging/20261104-three-chats/unity/reviews/Presentation-r2/IndependentTests.cs')]:
 sources=([core]+neutral+list((stage/'inputs/Persistence').glob('*.cs'))+[stage/'inputs/PlayerPorts.cs',stage/'inputs/PlayerInput.cs']+[test]) if name=='R3' else [core]+[p for p in neutral if 'PersonalSettings' not in p.parts]+[test]
 exe=stage/(name+'.exe');rsp=stage/(name+'.rsp')
 rsp.write_text('/nologo\n/target:exe\n/langversion:5\n/warnaserror+\n/out:"'+str(exe)+'"\n'+'\n'.join('"'+str(p)+'"' for p in sources))
 command=[str(csc),'@'+str(rsp)];c=subprocess.run(command,capture_output=True,text=True);(evidence/(name+'-compile.log')).write_text(c.stdout+c.stderr)
 result={'suite':name,'compileCommand':command,'compileExit':c.returncode}
 if c.returncode==0:
  cmd=[str(exe)]+([str(stage/('data-'+datetime.datetime.now().strftime('%H%M%S%f')))] if name=='R3' else [])
  r=subprocess.run(cmd,capture_output=True,text=True);(evidence/(name+'.log')).write_text(r.stdout+r.stderr);result.update(runCommand=cmd,exit=r.returncode,output=r.stdout,exeSha256=sha(exe))
 results.append(result)
(evidence/'results.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
(evidence/'input-manifest.json').write_text(json.dumps([{'path':str(p.relative_to(root)),'sha256':sha(p)} for p in inputs+source],indent=2))
