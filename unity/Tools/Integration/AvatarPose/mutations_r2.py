import pathlib,subprocess,json
R=pathlib.Path.cwd();S=R/'campaign-staging/20261104-three-chats/unity/AvatarLifecycleBindingR2';D=R/'docs/agent-runs/20261104-three-chats/unity/AvatarLifecycleBindingR2';source=S/'candidate/unity/Assets/Ashfall/Integration/AvatarPose/PoseFrame.cs'
mutants=[('allow-legacy','if(connection!=null&&','if(connection==null)return true;\n            if(connection!=null&&'),('ignore-connection','Object.ReferenceEquals(connection,session.GetConnectionIdentity(identity.Actor))','true'),('ignore-session-object','Object.ReferenceEquals(boundSession,session)','true')]
results=[]
for name,old,new in mutants:
 folder=S/name;folder.mkdir(exist_ok=True);mut=folder/'PoseFrame.cs';assert old in source.read_text();mut.write_text(source.read_text().replace(old,new))
 rsp=folder/'compile.rsp';rsp.write_text((S/'neutral.rsp').read_text().replace(str(source),str(mut)).replace(str(S/'neutral.exe'),str(folder/'test.exe')))
 cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/noconfig','@'+str(rsp)];c=subprocess.run(cmd,capture_output=True,text=True);log=c.stdout+c.stderr;record={'name':name,'compile':c.returncode,'command':cmd}
 if c.returncode==0:
  run=subprocess.run([str(folder/'test.exe')],cwd=R/'campaign-staging/20261104-three-chats/unity/AvatarR4',capture_output=True,text=True);record['run']=run.returncode;log+=run.stdout+run.stderr
 (D/(name+'.log')).write_text(log);results.append(record)
(D/'mutants.json').write_text(json.dumps(results,indent=2));print(json.dumps(results,indent=2))
