from pathlib import Path
import hashlib,json,subprocess
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];ASSETS=ROOT/'unity/Assets/Ashfall'
raw=ROOT/'docs/agent-runs/20261104-three-chats/unity/playable-r23/floor-20261105-181006';sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
historical={p.name:sha(p)for p in [raw/'ghost-reload.json',raw/'editor.log',raw/'smoke-ghost-actual-source-body-hands.png']}
groups=json.loads(json.loads((raw/'ghost-reload.json').read_text())['ghostJson'])['groups']
assert sum(g['passed']for g in groups)==5 and len(groups)==7
assert 'Fresh neutral did not rearm'in groups[0]['error']
assert 'R24GhostSmoke.cs:109'in groups[1]['error'] and 'Fixture site lacks actual native map floor'in groups[1]['error']
fixture=ASSETS/'Editor/Playable/R24GhostSmoke.cs';text=fixture.read_text()
assert 'for(int i=0;i<5;i++)loop.Rig.SmokeXrMove(Vector2.zero,.02f)'in text
assert 'FloorExists(feet,1)'in text # unchanged walking/dummy floor guard
assert 'DropFromSupport(floor,3,Vector3.forward)'in text
assert 'MapRay(floor+Vector3.up*height,Vector3.down,height+.05f,out native)'in text
assert 'startY-loop.Rig.transform.position.y>2.8f'in text and 'loop.Rig.Supported'in text
assert 'wall.normal.x<-.8f'in text and 'before<-.5f&&after>.5f'in text
assert 'UnityEditor.ShaderUtil.ShaderHasError(sourceShader)'in text
assert text.count('R23SmokeControl.Capture(loop.Rig.Head,"ghost-actual-source-body-hands");art.AssertGhost(loop);')==1
# Actual Range authored/native BoxShape3D support, not a new plane. A 3m drop
# cannot satisfy the original walking helper's1m ray; its explicit3.05m ray can.
data=json.loads((ROOT/'unity/Assets/StreamingAssets/PlayableR23/Maps/range/map.json').read_text())
floor=next(b for b in data['boxes']if b['size'][1]<1);top=floor['center'][1]+floor['size'][1]/2
assert top==0 and floor['center'][2]==-600 and floor['size']==[90,.4,100]
assert 3+.3-top>1 and 3-top<=3+.05
pkg=Path('C:/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data/Resources/PackageManager/BuiltInPackages')
color=pkg/'com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl';core=pkg/'com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl'
shader=ASSETS/'Integration/Playable/Match/Resources/R24SourceGhost.shader';hlsl=shader.read_text()
coreInclude='#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"'
colorInclude='#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"'
assert coreInclude in hlsl and colorInclude in hlsl and hlsl.index(coreInclude)<hlsl.index(colorInclude)
assert 'Color.hlsl'not in core.read_text() # concrete missing binding in old shader
for name in ['SRGBToLinear','LinearToSRGB']:assert 'real3 '+name+'(real3 c)'in color.read_text()
for formula in ['dot(sampled.rgb,float3(.2126,.7152,.0722))','saturate(.17+luminance*.69+rim*rim*.14)','_GhostOpacity*sampled.a','Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Back']:
 assert formula in hlsl,formula
files=[ASSETS/'Core/Contracts.cs',ASSETS/'Modules/Player/PlayerPorts.cs',ASSETS/'Modules/Player/PlayerR3Ports.cs',ASSETS/'Modules/Player/PlayerR5Ports.cs',ASSETS/'Modules/Player/PlayerInput.cs',HERE/'NativeRepairCases.cs']
exe=HERE/'NativeRepairCases.exe';p=subprocess.run(['C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe','/nologo','/warn:4','/warnaserror+','/out:'+str(exe)]+[str(p)for p in files],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
p=subprocess.run([str(exe)],capture_output=True,text=True);print(p.stdout+p.stderr);assert p.returncode==0
assert historical=={p:sha(raw/p)for p in historical}
result=dict(historicalUnchanged=historical,priorNativeGroupsPassed=5,priorNativeGroupsTotal=7,neutralOwnerOutput=p.stdout.strip(),nativeFloorSource=floor,shaderColorLibrary=str(color),shaderColorLibrarySha256=sha(color),shaderNativeCompileRerun=False,changedAssets={str(p.relative_to(ROOT)):sha(p)for p in [fixture,shader]})
(HERE/'native-repair-checks.json').write_text(json.dumps(result,indent=2)+'\n')
print('PASS exact authored Range support/drop-ray control, unchanged collision assertions, explicit installed shader-function binding/formula; old raw evidence unchanged. NEW NATIVE/SHADER COMPILE UNRUN.')
