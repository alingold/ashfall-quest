from pathlib import Path
import runpy,subprocess,json,sys
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4]
LIVE=ROOT/'unity/Assets/Ashfall/Integration/Playable';STAGE=HERE/'Staged'
# Reuse the installed compiler's exact current source/reference closure, replacing
# only this candidate's files in memory. No Assets/rsp shared-output mutation.
checker=ROOT/'unity/Tools/PlayableR23/check_integration.py'
text=checker.read_text()
text=text.replace("O=R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'","O=Path("+repr(str(HERE/'Api'))+")")
if '--live'not in sys.argv:text=text.replace("runtime,code=compile('R23Playable.Runtime',src,refs,'UNITY_6000_0_OR_NEWER');assert code==0",'''
live=R/'unity/Assets/Ashfall/Integration/Playable'
stage=Path('''+repr(str(STAGE))+''')
overrides={live/p.relative_to(stage):p for p in stage.rglob('*.cs')}
src=sorted(set(overrides.get(p,p) for p in src)|set(overrides.values()))
runtime,code=compile('R23Playable.Runtime',src,refs,'UNITY_6000_0_OR_NEWER');assert code==0
''')
exec(compile(text,str(checker),'exec'),{'__file__':str(checker),'__name__':'__main__'})
