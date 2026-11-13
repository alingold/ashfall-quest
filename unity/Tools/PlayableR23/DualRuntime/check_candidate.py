from pathlib import Path
T=Path(__file__).resolve().parent;R=T.parents[3];C=T/'Candidate'
p=R/'unity/Tools/PlayableR23/check_integration.py';s=p.read_text()
s=s.replace("O=R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'","O=Path("+repr(str(T/'CandidateCompiler'))+")")
inject="candidate=Path("+repr(str(C))+");src=[candidate/x.relative_to(R) if (candidate/x.relative_to(R)).is_file() else x for x in src];src=sorted(set(src+list((candidate/'unity/Assets/Ashfall/Integration/Playable').rglob('*.cs'))));results=[]"
s=s.replace('results=[]',inject)
exec(compile(s,str(p),'exec'),{'__file__':str(p)})
