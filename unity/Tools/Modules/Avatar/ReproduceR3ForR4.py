from pathlib import Path
import json,hashlib,subprocess,shutil
R=Path(__file__).resolve().parents[4];S=R/'campaign-staging/20261104-three-chats/unity/AvatarR4';E=R/'docs/agent-runs/20261104-three-chats/unity/AvatarR4';P=R/'campaign-staging/20261104-three-chats/unity/reviews/Avatar-r3'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
# Reproduce sealed r3 on its own frozen inputs; never classify root's newer source as drift-free r3.
manifest=R/'docs/agent-runs/20261104-three-chats/unity/Avatar/phase03/candidate-manifest.json'
data=json.loads(manifest.read_text());(E/'r3-manifest-structure.txt').write_text(str(type(data))+' '+str(list(data)[:8]))
out=S/'historical-repro';out.mkdir(exist_ok=True)
for p in P.iterdir():
 if p.is_file() and p.suffix in ['.gltf','.glb','.bin','.expected','.closure']:shutil.copy2(p,out/p.name)
sources=[P/'frozen/Core.cs']+sorted((P/'frozen/unity/Assets/Ashfall/Modules/Avatar').rglob('*.cs'))
results=[]
for name in ['AvatarTests','AvatarR2Tests','AvatarR3Tests','IndependentTests','PriorR2Independent']:
 test=P/(name+'.cs') if (P/(name+'.cs')).exists() else P/'frozen/unity/Tools/Modules/Avatar'/(name+'.cs')
 exe=out/(name+'.exe');cmd=[r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+list(map(str,sources))+[str(test)]
 r=subprocess.run(cmd,capture_output=True,text=True);log='COMMAND '+' '.join('"'+a+'"' for a in cmd)+'\n'+r.stdout+r.stderr+'\nCOMPILE EXIT '+str(r.returncode)+'\n';assert r.returncode==0,log
 r=subprocess.run([str(exe)],cwd=out,capture_output=True,text=True);log+='COMMAND "'+str(exe)+'"\n'+r.stdout+r.stderr+'\nRUN EXIT '+str(r.returncode)+'\n';(E/('historical-'+name+'.log')).write_text(log);assert r.returncode==0,log;results.append(dict(name=name,exit=r.returncode));print('sealed-r3',name,r.stdout.splitlines()[-1])
(E/'historical-repro.json').write_text(json.dumps(results,indent=2))
# Validate exact sealed expected file identities with absolute/relative path normalization.
print(str(data)[:180])
