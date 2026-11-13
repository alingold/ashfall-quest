from pathlib import Path
T=Path(__file__).resolve().parent;R=T.parents[3]
ns={'__file__':str(R/'unity/Tools/PlayableR23/check_integration.py')};code=(R/'unity/Tools/PlayableR23/check_integration.py').read_text().split("runtime,code=compile('R23Playable.Runtime'")[0];exec(code,ns)
ns['O']=T/'Compiler';ns['O'].mkdir(exist_ok=True);candidate={p.name:p for p in (T/'Candidate').glob('*.cs')};currentNames={p.name for p in ns['src']};sources=list(ns['src'])+[p for p in (T/'Staged').glob('*.cs')if p.name not in currentNames];sources=sorted(set(sources));ns['results']=[];compile=ns['compile'];refs=ns['refs'];E=ns['E'];P=ns['P']
runtime,exit=compile('KnifeCandidate.Runtime',sources,refs,'UNITY_6000_0_OR_NEWER');assert exit==0
editor,exit=compile('KnifeCandidate.EditorRuntime',sources,refs,'UNITY_6000_0_OR_NEWER;UNITY_EDITOR');assert exit==0
editorRefs=refs+[p for p in (E/'Managed/UnityEngine').glob('UnityEditor.*.dll')]+[editor,E/'NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll']
for name in ['Unity.XR.Management','Unity.XR.Management.Editor','Unity.XR.OpenXR','Unity.XR.OpenXR.Editor','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.GPUDriven.Runtime','Unity.RenderPipelines.Core.Editor','Unity.RenderPipelines.Universal.Editor']:editorRefs.append(P/('Library/ScriptAssemblies/'+name+'.dll'))
editorRefs+=[P/'Library/ScriptAssemblies/Unity.XR.OpenXR.Features.MetaQuestSupport.dll',E/'PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll']
_,exit=compile('KnifeCandidate.BuildAndSmoke',sorted((ns['B']/'Editor/Playable').glob('*.cs')),editorRefs,'UNITY_6000_0_OR_NEWER;UNITY_EDITOR');assert exit==0
print('PASS all3 staged candidate integration API checks; liveAssets17 untouched, no engine/device')
