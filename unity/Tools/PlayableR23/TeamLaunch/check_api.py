from pathlib import Path
T=Path(__file__).resolve().parent;R=T.parents[3]
p=R/'unity/Tools/PlayableR23/check_integration.py';ns={'__file__':str(p)};exec(p.read_text().split("runtime,code=compile('R23Playable.Runtime'")[0],ns)
ns['O']=T/'Compiler';ns['O'].mkdir(exist_ok=True);ns['results']=[]
_,code=ns['compile']('TeamLaunch.API',ns['src']+[p for p in (T/'Staged').glob('*.cs')if not (R/'unity/Assets/Ashfall/Integration/Playable/TeamLaunch'/p.name).exists()],ns['refs'],'UNITY_6000_0_OR_NEWER;UNITY_EDITOR');assert code==0
print('PASS staged source TeamLaunch installed Unity API; no engine/native/render/device')
