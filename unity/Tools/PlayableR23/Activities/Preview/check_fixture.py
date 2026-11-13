from pathlib import Path
import subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
runtime=ROOT/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler/R23Playable.Runtime.dll'
refs=[p for p in(E/'Managed/UnityEngine').glob('*.dll')if not p.name.startswith('UnityEditor')]+list((E/'NetStandard/ref/2.1.0').glob('*.dll'))+[runtime]
args=['/nologo','/langversion:5','/warn:4','/warnaserror+','/nostdlib+','/target:library','/define:UNITY_6000_0_OR_NEWER','/out:'+str(HERE/'R23ConstructionPreview.Fixture.dll')]+['/reference:'+str(p)for p in refs]+[str(HERE/'Staged/R23ConstructionPreviewSmoke.cs')]
rsp=HERE/'fixture.rsp';rsp.write_text('\n'.join('"'+x+'"'for x in args));r=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/noconfig','@'+str(rsp)],capture_output=True,text=True);(HERE/'fixture-compile.log').write_text(r.stdout+r.stderr);assert r.returncode==0,r.stdout+r.stderr
print('PASS staged construction-preview fixture API compile; native rendering fixture UNRUN.')
