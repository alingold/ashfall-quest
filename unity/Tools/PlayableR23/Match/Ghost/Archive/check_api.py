from pathlib import Path
import sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[5]
checker=ROOT/'unity/Tools/PlayableR23/check_integration.py';t=checker.read_text().replace("O=R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'","O=Path("+repr(str(HERE/'Api'))+")")
if '--live'not in sys.argv:
 t=t.replace("runtime,code=compile('R23Playable.Runtime',src,refs,'UNITY_6000_0_OR_NEWER');assert code==0",'stage=Path('+repr(str(HERE/'Staged'))+');live=R/"unity/Assets/Ashfall/Integration/Playable"\noverrides={live/p.relative_to(stage):p for p in stage.rglob("*.cs")}\nsrc=sorted(set(overrides.get(p,p)for p in src)|set(overrides.values()))\nruntime,code=compile("R23Playable.Runtime",src,refs,"UNITY_6000_0_OR_NEWER");assert code==0')
 t=t.replace("sorted((B/'Editor/Playable').glob('*.cs'))","[p for p in sorted((B/'Editor/Playable').glob('*.cs'))if p.name!='R24ArchiveGhostRecoverySmoke.cs']+list(Path("+repr(str(HERE/'NativeStaged'))+").glob('*.cs'))")
exec(compile(t,str(checker),'exec'),{'__file__':str(checker),'__name__':'__main__'})
