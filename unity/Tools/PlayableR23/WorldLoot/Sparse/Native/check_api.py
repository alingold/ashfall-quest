from pathlib import Path
ROOT=Path(__file__).resolve().parents[6];setup=ROOT/'unity/Tools/PlayableR23/check_integration.py';ns={'__file__':str(setup)}
exec(compile(setup.read_text().split("runtime,code=compile('R23Playable.Runtime'",1)[0],'installed_api_setup','exec'),ns)
base=ns['O'];ns['O']=Path(__file__).resolve().parent/'compiler';ns['O'].mkdir(exist_ok=True)
refs=ns['refs']+[base/'R23Playable.EditorRuntime.dll']
_,code=ns['compile']('SparseWorld.NativeHelper',[ROOT/'unity/Assets/Ashfall/Editor/Playable/R24SparseWorldSmoke.cs'],refs,'UNITY_6000_0_OR_NEWER;UNITY_EDITOR');assert code==0
