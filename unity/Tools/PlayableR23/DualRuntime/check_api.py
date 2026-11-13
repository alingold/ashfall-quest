from pathlib import Path
T=Path(__file__).resolve().parent;R=T.parents[3]
p=R/'unity/Tools/PlayableR23/check_integration.py';s=p.read_text()
s=s.replace("O=R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'","O=Path("+repr(str(T/'Compiler'))+")")
s=s.replace("results=[]","src+=sorted(Path("+repr(str(T/'Staged'))+").glob('*.cs'));results=[]")
exec(compile(s,str(p),'exec'),{'__file__':str(p)})
