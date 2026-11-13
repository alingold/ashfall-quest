Shader "Ashfall/R23LobbySurface"
{
 Properties { _BaseColor("Source color",Color)=(1,1,1,1) _Metallic("Source metallic",Float)=0 _Roughness("Source roughness",Float)=.48 _Emission("Source emission",Color)=(0,0,0,1) _Facade("Source facade recipe",Float)=0 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Pass {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.0
   #pragma multi_compile_instancing
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor,_Emission;float _Metallic,_Roughness,_Facade;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vert(Attributes a) {Varyings o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.normalOS);return o;}
   float3 Illuminate(float3 albedo,float3 n,float3 view,Light light) {
    float diffuse=saturate(dot(n,light.direction));float3 h=normalize(light.direction+view);
    float spec=pow(saturate(dot(n,h)),lerp(100,4,_Roughness));
    return (albedo*diffuse*(1-.35*_Metallic)+spec*lerp(.04,albedo,_Metallic))*light.color*light.distanceAttenuation*light.shadowAttenuation;
   }
   half4 Frag(Varyings i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 n=normalize(i.normalWS),albedo=_BaseColor.rgb,emission=_Emission.rgb;
    if(_Facade>.5) {
     // Source shader uses Godot world coordinates, including its actual +1000 Z lobby origin.
     float3 sourcePosition=i.positionWS*float3(1,1,-1);
     float horizontal=abs(n.x)>.5?sourcePosition.z:sourcePosition.x;
     float2 grid=float2(horizontal*.62,sourcePosition.y*.64),cell=frac(grid);
     float pane=step(.16,cell.x)*step(cell.x,.84)*step(.20,cell.y)*step(cell.y,.80)*(1-step(.5,abs(n.y)));
     float occupied=step(.3,frac(sin(dot(floor(grid),float2(12.9898,78.233)))*43758.5453));
     albedo=lerp(float3(.17,.22,.29),float3(.025,.15,.23),pane);
     emission=float3(.015,.11,.17)*pane*occupied;
    }
    float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);float3 result=albedo*max(SampleSH(n),float3(.08,.08,.08))+emission;
    result+=Illuminate(albedo,n,view,GetMainLight());
    #ifdef _ADDITIONAL_LIGHTS
    uint count=GetAdditionalLightsCount();for(uint index=0;index<count;index++)result+=Illuminate(albedo,n,view,GetAdditionalLight(index,i.positionWS));
    #endif
    return half4(result,1);
   }
   ENDHLSL
  }
 }
}
