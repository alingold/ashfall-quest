from pathlib import Path
ROOT=Path(__file__).resolve().parents[5]
setup=ROOT/'unity/Tools/PlayableR23/check_integration.py';ns={'__file__':str(setup)}
exec(compile(setup.read_text().split("runtime,code=compile('R23Playable.Runtime'",1)[0],'installed_api_setup','exec'),ns)
ns['O']=Path(__file__).resolve().parent/'compiler';ns['O'].mkdir(exist_ok=True)
_,code=ns['compile']('SparseLoot.EditorRuntime',ns['src'],ns['refs'],'UNITY_6000_0_OR_NEWER;UNITY_EDITOR')
assert code==0
