"""New phase05 evidence; historical sources, oracles and binaries are never overwritten."""
from pathlib import Path
import subprocess,json,shutil,hashlib,sys
R=Path(__file__).resolve().parents[4]
S=R/'campaign-staging/20261104-three-chats/unity/MapStreamingR4/phase05'
O=R/'docs/agent-runs/20261104-three-chats/unity/MapStreamingR4/phase05'
C=Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe')
P=R/'unity/Assets/Ashfall/Modules/MapStreaming'
T=R/'unity/Tools/Modules/MapStreaming'
H=R/'campaign-staging/20261104-three-chats/unity/MapStreaming/phase03'
D=R/'docs/agent-runs/20261104-three-chats/unity/MapStreaming/phase03'
if len(sys.argv)>1:
 S=S/sys.argv[1];O=O/sys.argv[1];S.mkdir(exist_ok=False);O.mkdir(exist_ok=False)
 parent=S.parent
 shutil.copytree(parent/'dependencies',S/'dependencies');shutil.copyfile(parent/'Lifecycle.cs',S/'Lifecycle.cs')
 for name in ['holdouts.json','adapter.mapref.json','world.json','world-wrong.json']:shutil.copyfile(parent/name,S/name)
log=[]
def run(cmd):
 p=subprocess.run(list(map(str,cmd)),capture_output=True,text=True,cwd=R)
 log.append(dict(command=subprocess.list2cmdline(list(map(str,cmd))),exitCode=p.returncode,stdout=p.stdout,stderr=p.stderr))
 (O/'commands.json').write_text(json.dumps(log,indent=2));print(p.stdout,p.stderr,flush=True)
 if p.returncode:raise SystemExit(p.returncode)
def compile(name,sources,web=False,profile=False,library=False):
 refs=[Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319')/x for x in ['mscorlib.dll','System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Xml.Linq.dll']]
 if web:refs.append(Path(r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Web.Extensions.dll'))
 if profile:refs=[Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\NetStandard\ref\2.1.0\netstandard.dll')]
 target=S/(name+('.dll' if library else '.exe'))
 args=['/nologo','/nostdlib+','/langversion:5','/warn:4','/warnaserror+','/target:'+('library' if library else 'exe'),'/out:"'+str(target)+'"']+['/reference:"'+str(x)+'"' for x in refs]+['"'+str(x)+'"' for x in sources]
 rsp=S/(name+'.rsp');rsp.write_text('\n'.join(args));run([C,'/noconfig','@'+str(rsp)]);return target
def main():
 (S/'candidate').mkdir(exist_ok=False)
 for f in P.glob('*.cs'):shutil.copyfile(f,S/'candidate'/f.name)
 prod=list((S/'candidate').glob('*.cs'))+list((S/'dependencies').rglob('*.cs'))
 compile('Restricted',prod,library=True)
 compile('UnityNetStandard21',prod,profile=True,library=True)
 # Test-only historical oracle reader needs Web; production compiles above explicitly exclude it.
 for label,src in ([('Candidate',prod)] if len(sys.argv)>1 else [('FrozenR3',list((S/'before').glob('*.cs'))),('Candidate',prod)]):
  oldprod=[f for f in src if f.name in ['Ports.cs','MapStream.cs']] if label=='FrozenR3' else src
  test=compile(label+'Original',oldprod+[T/'MapStreamingTests.cs'],web=True)
  run([test,H/'alfa-aglae-souza.mapref.json',H/'battlefield-paintball-gods-pitdevil.mapref.json'])
  run([test,*json.loads((D/'fixture-paths.json').read_text())])
  adv=compile(label+'Adversarial',oldprod+[T/'AdversarialTests.cs'],web=True)
  run([adv,H/'fixture-5121-5120.mapref.json',H/'fixture-5121-5120.glb',*json.loads((D/'malformed-paths.json').read_text())])
 life=compile('Lifecycle',prod+[S/'Lifecycle.cs']);run([life,H/'fixture-5121-5120.mapref.json'])
 hold=compile('Holdouts',prod+[T/'R4Holdouts.cs']);run([hold,S/'holdouts.json'])
if __name__=='__main__':main()
