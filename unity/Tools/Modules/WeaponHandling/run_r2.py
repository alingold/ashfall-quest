from pathlib import Path
import subprocess,json,hashlib,sys
root=Path(__file__).resolve().parents[4]
stage=root/'campaign-staging/20261104-three-chats/unity/WeaponHandling'
out=stage/'r2'/sys.argv[1]
out.mkdir(exist_ok=False)
logs=[]
csc=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
core=root/'unity/Assets/Ashfall/Core/Contracts.cs'
prod=root/'unity/Assets/Ashfall/Modules/WeaponHandling'
tools=root/'unity/Tools/Modules/WeaponHandling'
def run(args):
 p=subprocess.run([str(a) for a in args],capture_output=True,text=True)
 logs.append({'args':[str(a) for a in args],'exitCode':p.returncode,'stdout':p.stdout,'stderr':p.stderr})
 (out/'commands-and-results.json').write_text(json.dumps(logs,indent=2))
 if p.returncode: print(p.stdout,p.stderr);raise SystemExit(p.returncode)
 return p.stdout
def build(name,sources,library=False):
 exe=out/(name+('.dll' if library else '.exe'))
 run([csc,'/nologo','/langversion:5','/warnaserror+','/target:'+('library' if library else 'exe'),'/out:'+str(exe)]+list(sources))
 if not library:
  result=run([exe]);(out/(name+'.txt')).write_text(result);print(name+': '+result.splitlines()[-1])
sources=[core]+sorted(prod.rglob('*.cs'))
build('WeaponHandling',sources,True)
for name in ['BehaviorTests','ReviewerRegressions','R2Tests']:
 build(name,sources+[tools/(name+'.cs'),tools/'TestSpawn.cs'])
before=stage/'revisions/r1-before-r2'
build('BeforeProbe',[core]+sorted((before/'production').rglob('*.cs'))+[tools/'BeforeProbe.cs'])
build('BeforeBehaviorTests',[core]+sorted((before/'production').rglob('*.cs'))+[before/'tools/BehaviorTests.cs'])
print('All compile/run commands succeeded; exact arguments and transcripts in '+str(out))
