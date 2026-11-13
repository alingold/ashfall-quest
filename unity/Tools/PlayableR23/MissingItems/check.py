from pathlib import Path
import subprocess
T=Path(__file__).resolve().parent;R=T.parents[3];P=R/'unity/Assets/Ashfall/Integration/Playable/HandsWeapons'
CSC='C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe'
sources=sorted(set([R/p for p in (R/'unity/Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]+list(P.glob('*.cs'))))
def compile(name,files,opts):
 args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/out:'+str(T/name)]+opts+[str(p)for p in files];rsp=T/(name+'.rsp');rsp.write_text('\n'.join('"'+a+'"'for a in args));x=subprocess.run([CSC,'/noconfig','@'+str(rsp)],capture_output=True,text=True);(T/(name+'.log')).write_text(x.stdout+x.stderr);assert x.returncode==0,x.stdout+x.stderr
base=['/target:exe','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.dll','/reference:C:/Windows/Microsoft.NET/Framework64/v4.0.30319/System.Core.dll']
compile('Cases.exe',sources+[T/'Cases.cs',T/'IntegrationCases.cs'],base)
x=subprocess.run([str(T/'Cases.exe')],capture_output=True,text=True);print(x.stdout+x.stderr);assert x.returncode==0
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');refs=list((E/'Managed/UnityEngine').glob('*.dll'))+list((E/'NetStandard/ref/2.1.0').glob('*.dll'))
for defines,name in [('UNITY_6000_0_OR_NEWER','Runtime.dll'),('UNITY_6000_0_OR_NEWER;UNITY_EDITOR','Editor.dll')]:
 compile(name,sources,['/nostdlib+','/target:library','/define:'+defines]+['/reference:'+str(p)for p in refs]);print('PASS installed API',name)

for label,files in [('Instances222',[R/'unity/Tools/PlayableR23/InstanceCopies/Cases.cs']),('Smg71',[R/'unity/Tools/PlayableR23/Smg4/Cases.cs']),('Catalog100',[R/'unity/Tools/PlayableR23/Catalog5/Cases.cs'])]:
 compile(label+'.exe',sources+files,base);x=subprocess.run([str(T/(label+'.exe'))],capture_output=True,text=True);assert x.returncode==0,x.stdout+x.stderr;(T/(label+'-results.txt')).write_text(x.stdout+x.stderr);print('PASS retained',label)
