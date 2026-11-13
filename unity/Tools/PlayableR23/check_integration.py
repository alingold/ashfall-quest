from pathlib import Path
import json,subprocess,re
import argparse
parser=argparse.ArgumentParser(description='Compile native API closures without launching Unity')
parser.add_argument('--output-root',type=Path)
args=parser.parse_args()
R=Path(__file__).resolve().parents[3];U=R/'unity';B=U/'Assets/Ashfall'
O=args.output_root.resolve() if args.output_root else R/'campaign-staging/20261104-three-chats/unity/playable-r23/compiler'
if args.output_root:
    assert not O.is_relative_to(U), 'Compiler output must stay outside Unity'
    assert not O.exists(), 'Use a fresh output directory'
O.mkdir(parents=True,exist_ok=True)
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data');P=R/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project';C=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
DOTNET=E/'NetCoreRuntime/dotnet.exe';ROSLYN=E/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
for meta in list((B/'Integration/Playable').rglob('*.meta'))+list((B/'Editor/Playable').rglob('*.meta')):
    assert re.search(r'^guid: [a-f0-9]{32}\s*$',meta.read_text(encoding='utf-8-sig'),re.M), 'Unity will skip script with invalid metadata: '+str(meta)
src=[R/p for p in (U/'Tools/PlayableR23/HandsWeapons/sources.txt').read_text().splitlines()]
src+=list((B/'Modules/Player').glob('*.cs'))+list((B/'Integration/Playable').rglob('*.cs'));src=sorted(set(src))
for module in ['Construction','ConstructionCandidate','Flight','Climbing','Pickups','Networking','Sessions','Modes','HealthStates']:
    src+=list((B/'Modules'/module).glob('*.cs'))
src+=[B/'Modules/InventoryItems/BuildChargeLedger.cs'];src=sorted(set(src))
src+=list((B/'Integration/Sessions').glob('*.cs'))+[B/'Modules/Networking/Engine/UnityTransportAdapter.cs'];src=sorted(set(src))
refs=[p for p in (E/'Managed/UnityEngine').glob('*.dll') if not p.name.startswith('UnityEditor')]+list((E/'NetStandard/ref/2.1.0').glob('*.dll'))+[P/'Library/ScriptAssemblies/Unity.InputSystem.dll']
refs+=[P/'Library/ScriptAssemblies/Unity.XR.OpenXR.dll']
refs+=[P/'Library/ScriptAssemblies/Unity.XR.Management.dll']
for name in ['Unity.Networking.Transport','Unity.Collections','Unity.Mathematics','Unity.Burst']:
    refs.append(P/('Library/ScriptAssemblies/'+name+'.dll'))
# URP17 public RenderGraph APIs contain readonly-ref metadata that legacy .NET
# Framework csc cannot consume. Use the installed Editor's compiler and actual
# package references, retaining warnings-as-errors and the same source closure.
for name in ['Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Core.Runtime.Shared','Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.Universal.Config.Runtime','Unity.RenderPipeline.Universal.ShaderLibrary','Unity.RenderPipelines.GPUDriven.Runtime']:
    refs.append(P/('Library/ScriptAssemblies/'+name+'.dll'))
# Relay package is imported in the active project, not the preserved pre-services probe.
for name in ['Unity.Services.Core', 'Unity.Services.Authentication', 'Unity.Services.Multiplayer']:
    refs.append(U/('Library/ScriptAssemblies/'+name+'.dll'))
results=[]
def compile(name,files,ref,defines):
    rsp=O/(name+'.rsp');out=O/(name+'.dll');args=['/nologo','/langversion:latest','/warn:4','/warnaserror+','/nostdlib+','/target:library','/out:'+str(out),'/define:'+defines]+['/reference:'+str(x) for x in ref]+[str(x) for x in files];rsp.write_text('\n'.join('"'+x+'"' for x in args),encoding='utf-8')
    cmd=[str(DOTNET),str(ROSLYN),'/noconfig','@'+str(rsp)];x=subprocess.run(cmd,capture_output=True,text=True,errors='replace');(O/(name+'.log')).write_text(x.stdout+x.stderr,encoding='utf-8');results.append(dict(name=name,command=cmd,exit=x.returncode,output=x.stdout+x.stderr));(O/'results.json').write_text(json.dumps(results,indent=2),encoding='utf-8');print(name,x.returncode,x.stdout+x.stderr);return out,x.returncode
runtime,code=compile('R23Playable.Runtime',src,refs,'UNITY_6000_0_OR_NEWER');assert code==0
editorRuntime,code=compile('R23Playable.EditorRuntime',src,refs,'UNITY_6000_0_OR_NEWER;UNITY_EDITOR');assert code==0
editorRefs=refs+[p for p in (E/'Managed/UnityEngine').glob('UnityEditor.*.dll')]+[editorRuntime,E/'NetStandard/compat/2.1.0/shims/netfx/mscorlib.dll']
for name in ['Unity.XR.Management','Unity.XR.Management.Editor','Unity.XR.OpenXR','Unity.XR.OpenXR.Editor','Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.GPUDriven.Runtime','Unity.RenderPipelines.Core.Editor','Unity.RenderPipelines.Universal.Editor']:
    editorRefs.append(P/('Library/ScriptAssemblies/'+name+'.dll'))
editorRefs.append(P/'Library/ScriptAssemblies/Unity.XR.OpenXR.Features.MetaQuestSupport.dll')
editorRefs.append(E/'PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll')
_,code=compile('R23Playable.BuildAndSmoke',sorted((B/'Editor/Playable').glob('*.cs')),editorRefs,'UNITY_6000_0_OR_NEWER;UNITY_EDITOR');assert code==0
print('Integrated runtime and build/smoke source compile. Engine and headset still unrun.')
