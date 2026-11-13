from pathlib import Path
import hashlib,json,subprocess,shutil
ROOT=Path(r'C:\Users\aling\Documents\ashfall')
OUT=ROOT/'campaign-staging/20261104-three-chats/unity/NetworkingPeerR4/r5'
DOC=ROOT/'docs/agent-runs/20261104-three-chats/unity/NetworkingPeerR4/r5'
CSC=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def rec(p):return {'path':str(p),'sha256':sha(p)}
def main():
 OUT.mkdir(parents=True,exist_ok=True);DOC.mkdir(parents=True,exist_ok=True)
 attempt=OUT/('attempt-%02d'%(1+len(list(OUT.glob('attempt-*')))));attempt.mkdir()
 logs=DOC/attempt.name;logs.mkdir()
 prod=ROOT/'unity/Assets/Ashfall/Modules/Networking';tools=ROOT/'unity/Tools/Modules/Networking'
 shutil.copytree(prod,attempt/'production');shutil.copytree(tools,attempt/'tools')
 inputs=[ROOT/'unity/Assets/Ashfall/Core/Contracts.cs']
 for folder in ['unity/Assets/Ashfall/Modules/Sessions','unity/Assets/Ashfall/Integration/Sessions','unity/Assets/Ashfall/Integration/SessionReplication']:
  inputs+=list((ROOT/folder).rglob('*.cs'))
 inputs += [ROOT/'unity/Tools/Integration/Sessions/r5/Tests.cs']
 pinned=[]
 for p in inputs:
  dest=attempt/'inputs'/p.relative_to(ROOT);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest);pinned.append((p,dest))
 core=attempt/'inputs/unity/Assets/Ashfall/Core/Contracts.cs'
 current=[core]+sorted((attempt/'production').rglob('*.cs'))
 frozen=ROOT/'campaign-staging/20261104-three-chats/unity/Networking/r3/candidate'
 results=[]
 def run(name,sources):
  exe=attempt/(name+'.exe')
  args=[str(CSC),'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+list(map(str,sources))
  command=' '.join('/out:"'+x[5:]+'"' if x.startswith('/out:') else x if x.startswith('/') else '"'+x+'"' for x in args)
  build=subprocess.run(command,capture_output=True,text=True)
  result=subprocess.run('"'+str(exe)+'"',capture_output=True,text=True) if build.returncode==0 else None
  (logs/(name+'.txt')).write_text(command+'\nCOMPILE '+str(build.returncode)+'\n'+build.stdout+build.stderr+ ('\nRUN "'+str(exe)+'"\nEXIT '+str(result.returncode)+'\n'+result.stdout+result.stderr if result else ''))
  item={'name':name,'command':command,'compileExit':build.returncode,'runExit':result.returncode if result else None,'sources':[rec(p) for p in sources]};results.append(item);print(name,item['compileExit'],item['runExit'])
 for name in ['NetworkingTests','AdversarialR2','NetworkingR2Tests','NetworkingR3Tests']:
  run('Frozen-'+name,[frozen/'Contracts.cs']+sorted((frozen/'production').rglob('*.cs'))+[frozen/'tools'/(name+'.cs')])
  run('Current-'+name,current+[attempt/'tools'/(name+'.cs')])
 # Keep the historical source unchanged. Its implicit one-identity policy is superseded.
 # Re-express that SAME hard history-exhaustion assertion using the explicit budget.
 policy=attempt/'PolicyExplicitNetworkingTests.cs'
 text=(attempt/'tools/NetworkingTests.cs').read_text()
 text=text.replace('new AuthorityHost("test-session",Hash,3,1,1,1,10);','new AuthorityHost("test-session",Hash,3,1,1,1,10,1);')
 policy.write_text(text)
 run('PolicyExplicitNetworkingTests',current+[policy])
 run('PeerTests',current+[attempt/'tools/r5/PeerTests.cs'])
 sessionSources=sorted((attempt/'inputs/unity/Assets/Ashfall/Modules/Sessions').glob('*.cs'))+sorted((attempt/'inputs/unity/Assets/Ashfall/Integration/Sessions').glob('*.cs'))
 run('CurrentSessionsComposition',current+sessionSources+[attempt/'inputs/unity/Tools/Integration/Sessions/r5/Tests.cs'])
 (logs/'results.json').write_text(json.dumps(results,indent=2))
 (logs/'dependency-drift.json').write_text(json.dumps([dict(live=rec(p),snapshot=rec(d),match=sha(p)==sha(d)) for p,d in pinned],indent=2))
 (DOC/'latest.json').write_text(json.dumps({'attempt':str(attempt),'logs':str(logs)},indent=2))
 if any(r['compileExit']!=0 or r['runExit']!=0 for r in results if r['name']!='Current-NetworkingTests'):raise SystemExit(1)
if __name__=='__main__':main()
