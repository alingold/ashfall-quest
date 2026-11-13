import pathlib,hashlib,json,subprocess,sys,shutil
R=pathlib.Path(r'C:\Users\aling\Documents\ashfall'); S=R/'campaign-staging/20261104-three-chats/unity/Audio/r4'; E=R/'docs/agent-runs/20261104-three-chats/unity/Audio/r4'
CSC=pathlib.Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
commands=[]
def run(name,args):
 args=list(map(str,args));commands.append(' '.join('"'+a+'"' for a in args));(E/'commands.txt').write_text('\n'.join(commands))
 p=subprocess.run(args,cwd=str(R),capture_output=True,text=True);(E/(name+'.txt')).write_text(p.stdout+p.stderr);print(name,p.returncode,(p.stdout+p.stderr)[-1000:],flush=True)
 if p.returncode:raise RuntimeError(name)
def compile(name,base,test,persistence):
 sources=[S/'inputs/unity/Assets/Ashfall/Core/Contracts.cs']+sorted((base/'unity/Assets/Ashfall/Modules/Audio').rglob('*.cs'))
 if persistence:sources+=sorted((S/'inputs/unity/Assets/Ashfall/Modules/Persistence').glob('*.cs'))+sorted((base/'unity/Assets/Ashfall/Integration/AudioSettings').glob('*.cs'))
 exe=S/(name+'.exe');run(name+'-compile',[CSC,'/nologo','/langversion:5','/warnaserror+','/target:exe','/out:'+str(exe)]+sources+[test]);return exe
def main():
 if (E/'candidate-manifest.json').exists():raise RuntimeError('frozen; new run directory required')
 # Exact historical r3 production, independently pinned before editing.
 before=json.loads((E/'before.json').read_text());lookup={x['path']:x for x in before}
 historical=json.loads((R/'docs/agent-runs/20261104-three-chats/unity/Audio/r3/candidate-manifest.json').read_text())
 for x in historical['changed_files']:
  path=str(R/x['path'])
  if path in lookup:assert lookup[path]['sha256']==x['sha256']==sha(pathlib.Path(lookup[path]['snapshot']))
 deps=json.loads((E/'inputs.json').read_text());assert all(sha(pathlib.Path(x['path']))==x['sha256']==sha(pathlib.Path(x['snapshot'])) for x in deps)
 old=S/'before';tests=old/'unity/Tools/Modules/Audio';wavs=R/'campaign-staging/20261104-three-chats/unity/Audio/r3/source-wavs.txt'
 for label,base in [('frozen-r3',old),('current-r4',R)]:
  dest=S/label;dest.mkdir(exist_ok=False)
  exe=compile(label+'-baseline',base,tests/'AudioTests.cs',False);run(label+'-baseline',[exe,dest/'fixtures'])
  if label=='frozen-r3':
   exe=compile(label+'-r2',base,tests/'R2Tests.cs',False);run(label+'-r2',[exe,dest])
  exe=compile(label+'-r3',base,old/'unity/Tools/Integration/AudioSettings/AudioR3Tests.cs',True);run(label+'-r3',[exe,dest,wavs])
 dest=S/'r4-state-machine';dest.mkdir();run('r4-fixture',[compile('fixture',R,tests/'AudioTests.cs',False),dest/'fixtures'])
 run('r4-state-machine',[compile('AudioR4Tests',R,R/'unity/Tools/Integration/AudioSettings/AudioR4Tests.cs',True),dest])
 drift=[x for x in deps if sha(pathlib.Path(x['path']))!=x['sha256']];(E/'dependency-after.json').write_text(json.dumps(dict(records=len(deps),external_dependency_drift=drift),indent=2));assert not drift
 print('All suites complete; freeze separately after report')
if __name__=='__main__':main()
