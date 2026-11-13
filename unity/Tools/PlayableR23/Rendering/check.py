"""Bounded installed-API compile + pure C# source-domain math execution, no engine."""
from pathlib import Path
import hashlib,json,re,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[3]
E=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data')
P=ROOT/'campaign-staging/20261104-three-chats/unity/editor-probes/install-6000.6.8f1/project/Library/ScriptAssemblies'
C=Path('C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe')
DOTNET=E/'NetCoreRuntime/dotnet.exe';ROSLYN=E/'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'
O=HERE/'compiled';O.mkdir(exist_ok=True)
def compile(name,files,refs,target='library'):
    output=O/(name+('.exe'if target=='exe'else'.dll'));rsp=O/(name+'.rsp')
    args=['/nologo','/langversion:latest'if refs else'/langversion:5','/warn:4','/warnaserror+','/target:'+target,'/out:'+str(output)]+['/reference:'+str(r)for r in refs]+[str(f)for f in files]
    if refs:args+=['/nostdlib+']
    rsp.write_text('\n'.join('"'+a+'"'for a in args),encoding='utf-8')
    compiler=[str(DOTNET),str(ROSLYN)]if refs else[str(C)]
    r=subprocess.run(compiler+['/noconfig','@'+str(rsp)],capture_output=True,text=True);(O/(name+'.log')).write_text(r.stdout+r.stderr,encoding='utf-8')
    print(name,r.returncode,r.stdout+r.stderr,flush=True);assert r.returncode==0;return output
def main():
    refs=[p for p in (E/'Managed/UnityEngine').glob('*.dll')if not p.name.startswith('UnityEditor')]+list((E/'NetStandard/ref/2.1.0').glob('*.dll'))
    names=['Unity.RenderPipelines.Core.Runtime','Unity.RenderPipelines.Core.Runtime.Shared','Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.Universal.Config.Runtime','Unity.RenderPipeline.Universal.ShaderLibrary','Unity.RenderPipelines.GPUDriven.Runtime']
    refs += [P/(name+'.dll')for name in names];assert all(p.is_file()for p in refs)
    runtime=compile('R23Rendering',list((HERE/'Staged').glob('*.cs')),refs)
    tests=compile('R23FilmicMathCases',[HERE/'Staged/R23GodotFilmicMath.cs',HERE/'MathCases.cs'],[],target='exe')
    run=subprocess.run([str(tests)],capture_output=True,text=True);print(run.stdout+run.stderr);assert run.returncode==0
    shader=(HERE/'Staged/Resources/R23GodotFilmic.shader').read_text(encoding='utf-8')
    for token in ['SAMPLE_TEXTURE2D_X','UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX','STEREO_MULTIVIEW_ON','USE_FULL_PRECISION_BLIT_TEXTURE','const float exposure_bias=2.0','mapped/mappedWhite','radiance*_R23FilmicParameters.x','1.055*pow','ExactSrgbDecode(display)']:assert token in shader,token
    code=(HERE/'Staged/R23GodotFilmicFeature.cs').read_text(encoding='utf-8')
    assert 'cmd.Blit'not in code and 'CommandBuffer.Blit'not in code and 'resources.cameraColor = destination'in code and 'requiresIntermediateTexture = true'in code and 'parameters.propertyBlock = block'in code
    (HERE/'runtime-references.txt').write_text('\n'.join(str(P/(n+'.dll'))for n in names)+'\n',encoding='utf-8')
    (HERE/'checks.json').write_text(json.dumps(dict(installedApiCompile=True,pureMathExit=run.returncode,pureMathOutput=run.stdout.strip(),shaderStaticChecks=True,shaderCompile=False,engineRun=False,gpuRun=False,xrRun=False,references=names),indent=2)+'\n',encoding='utf-8')
    print('Installed Unity6000.6.8 / URP17.6 API compile and pure math PASS. Shader compilation/render/XR unrun.')
if __name__=='__main__':main()
