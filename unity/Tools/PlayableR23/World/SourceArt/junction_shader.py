"""CPU translation of the exact frozen R24/R6 detached-junction fragment."""
from analyze import *

def transform_fragment(text):
 body=re.search(r'\}else\{\n(.*)\n \}\n\}',text,re.S).group(1)
 # GLSL vec2(scalar) splats; HLSL needs both constructor components. Convert
 # the fragment itself before hashing/embedding, so provenance describes the
 # actual emitted HLSL rather than a pre-fix intermediate representation.
 assert body.count('vec2(.5)')==7
 body=body.replace('vec2(.5)','vec2(.5,.5)')
 for a,b in [('vec2','float2'),('vec3','float3'),('mix(','lerp('),('fract(','frac('),('source_position','i.sourcePosition'),('ALBEDO','albedo'),('ROUGHNESS','roughness')]:body=body.replace(a,b)
 assert body.count('repaired=true;')==body.count('float2(.5,.5)')==7 and 'float2(.5)'not in body
 return body

def main():
 path=source('assets/materials/alpha_junction_r24.gdshader');body=transform_fragment(path.read_text())
 shader='''// Fragment expressions translated from frozen alpha_junction_r24.gdshader.
// Mesh positions retain source map-local coordinates; reflect local Z once here.
Shader "Ashfall/R24 Source Detached Junction"
{
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags{"LightMode"="UniversalForward"} Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   struct A{float4 p:POSITION;float3 n:NORMAL;float3 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 p:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;float3 uv:TEXCOORD2;float3 sourcePosition:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.n);o.uv=a.uv;o.sourcePosition=float3(a.p.x,a.p.y,-a.p.z);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   half4 Frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 p=i.uv,albedo;float roughness=1;
'''+body+'''
    float3 normal=normalize(i.normalWS);Light light=GetMainLight();float diffuse=saturate(dot(normal,light.direction));float3 ambient=max(SampleSH(normal),float3(.12,.14,.16));float3 h=normalize(light.direction+GetWorldSpaceNormalizeViewDir(i.positionWS));float spec=pow(saturate(dot(normal,h)),lerp(100,4,roughness));return half4(albedo*(ambient+light.color*diffuse)+spec*light.color*.04,1);
   }
   ENDHLSL
  }
 }
}
'''
 target=HERE/'staged/runtime/Resources/R24SourceJunction.shader';target.write_text(shader)
 (HERE/'junction-shader-source.json').write_text(json.dumps(dict(source='assets/materials/alpha_junction_r24.gdshader',sourceSha256=sha(path),transformedFragmentSha256=hashlib.sha256(body.encode()).hexdigest(),sourceProjectionZones=7,sourceScalarSplatExpansions=7,sourceCoordinateRule='Input mesh vertices are original map-local Unity coordinates; reflect local Z once. Map-root offset is not used in source projection.',lightingLimit='Existing SourceArt URP lighting approximation retained; no claim of Godot BRDF/shadow parity.',engineShaderCompile=False),indent=2)+'\n')
 print('Staged exact source seven-zone junction fragment; no family texture/Root binding required.')

if __name__=='__main__':main()
