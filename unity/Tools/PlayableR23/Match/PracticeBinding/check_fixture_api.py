from pathlib import Path
ROOT=Path(__file__).resolve().parents[5]
namespace={}
source=(ROOT/'unity/Tools/PlayableR23/check_integration.py').read_text().split("runtime,code=compile('R23Playable.Runtime'",1)[0]
namespace['__file__']=str(ROOT/'unity/Tools/PlayableR23/check_integration.py')
exec(compile(source,'installed_compile_setup','exec'),namespace)
base=namespace['O'];namespace['O']=Path(__file__).resolve().parent/'compiler';namespace['O'].mkdir(exist_ok=True)
E=namespace['E'];P=namespace['P']
runtime,code=namespace['compile']('R23Playable.EditorRuntime',namespace['src'],namespace['refs'],'UNITY_6000_0_OR_NEWER;UNITY_EDITOR')
assert code==0
refs=namespace['refs']+[p for p in (E/'Managed/UnityEngine').glob('UnityEditor.*.dll')]+[runtime,base/'R23Playable.BuildAndSmoke.dll',E/'NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll']
_,code=namespace['compile']('PracticeFixture',[ROOT/'unity/Assets/Ashfall/Editor/Playable/R24LocalMatchSmoke.cs'],refs,'UNITY_6000_0_OR_NEWER;UNITY_EDITOR')
assert code==0
