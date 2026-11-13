Shader "Ashfall/R25StyleSky"
{
 Properties
 {
  _Zenith("Zenith",Color)=(0,0,0,1)
  _Horizon("Horizon",Color)=(0,0,0,1)
  _Nadir("Nadir",Color)=(0,0,0,1)
  _ToSun("Direction to sun",Vector)=(0,1,0,0)
  _SunColor("Sun colour",Color)=(1,1,1,1)
  _SunGlow("Glow",Float)=0
 }
 SubShader
 {
  Tags {"Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox"}
  Pass
  {
   Cull Off ZWrite Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.0
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _Zenith,_Horizon,_Nadir,_ToSun,_SunColor;
   float _SunGlow;
   CBUFFER_END
   struct A {float4 position:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 position:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a)
   {
    V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.position=TransformObjectToHClip(a.position.xyz);o.direction=a.position.xyz;return o;
   }
   half4 Frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 d=normalize(i.direction);
    float u=pow(saturate(d.y),.55);
    float dn=saturate(-d.y*3.0);
    float3 sky=lerp(lerp(_Horizon.rgb,_Zenith.rgb,u),_Nadir.rgb,dn);
    float s=saturate(dot(d,normalize(_ToSun.xyz)));
    sky+=_SunColor.rgb*_SunGlow*(.6*pow(s,24)+pow(s,400));
    return half4(sky,1);
   }
   ENDHLSL
  }
 }
}
