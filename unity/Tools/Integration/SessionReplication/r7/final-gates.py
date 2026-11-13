import pathlib,json,subprocess,hashlib
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall');S=R/'campaign-staging/20261104-three-chats/unity/SessionReplicationR7';D=R/'docs/agent-runs/20261104-three-chats/unity/SessionReplicationR7';O=S/'run04';O.mkdir();L=D/'run04';L.mkdir();commands=[]
def suite(name,files):
 exe=O/(name+'.exe');cmd='"C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe" /nologo /langversion:5 /warnaserror+ /out:"'+str(exe)+'" '+' '.join('"'+str(p)+'"' for p in files)
 for phase,c in [('compile',cmd),('run','"'+str(exe)+'"')]:
  r=subprocess.run(c,capture_output=True,text=True,timeout=120);(L/(name+'-'+phase+'.txt')).write_text(r.stdout+r.stderr);commands.append(dict(suite=name,phase=phase,command=c,exit=r.returncode));print(name,phase,r.returncode,r.stdout+r.stderr)
  if phase=='compile' and r.returncode:break
fixture=S/'run03/tests/Fixture.cs';p=R/'unity/Tools/Integration/SessionReplication/r7/Tests.cs';q=O/'R7Tests.cs';q.write_bytes(p.read_bytes())
suite('R7Final',sorted((S/'run03/source').rglob('*.cs'))+[fixture,q])
suite('PreservedBeforeHoldouts',list((S/'before/unity/Assets/Ashfall').rglob('*.cs'))+[fixture,S/'run03/tests/Holdouts.cs'])
(L/'commands.json').write_text(json.dumps(commands,indent=2));assert commands[1]['exit']==0 and commands[-1]['exit']==1
