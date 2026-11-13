from pathlib import Path
import sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
checker=ROOT/'unity/Tools/PlayableR23/check_integration.py';text=checker.read_text()
text=text.replace("O=R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'","O=Path("+repr(str(HERE/'NativeApi'))+")")
if '--live'not in sys.argv:text=text.replace("sorted((B/'Editor/Playable').glob('*.cs'))","[p for p in sorted((B/'Editor/Playable').glob('*.cs')) if p.name!='R24GhostSmoke.cs']+list(Path("+repr(str(HERE/'NativeStaged'))+").glob('*.cs'))")
exec(compile(text,str(checker),'exec'),{'__file__':str(checker),'__name__':'__main__'})
