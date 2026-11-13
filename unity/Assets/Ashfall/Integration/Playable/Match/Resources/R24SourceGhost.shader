Shader "Ashfall/R24SourceGhost"
{
 Properties
 {
  _SourceTint("Source linear albedo",Vector)=(.65,.65,.65,1)
  _SourceAlbedo("Source albedo",2D)="white"{} _HasAlbedo("Has source texture",Float)=0
  _SourceUV("Source texture scale and offset",Vector)=(1,1,0,0)
  _VertexAlbedo("Source vertex albedo",Float)=0 _GhostOpacity("Source opacity",Float)=.42
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+12"}
  Pass
  {
   Name "SourceGhost" Tags{"LightMode"="UniversalForward"}
   Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Back
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
   TEXTURE2D(_SourceAlbedo);SAMPLER(sampler_SourceAlbedo);
   CBUFFER_START(UnityPerMaterial)
   float4 _SourceTint,_SourceUV;float _HasAlbedo,_VertexAlbedo,_GhostOpacity;
   CBUFFER_END
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a)
   {
    V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionWS=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);
    o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.uv=a.uv*_SourceUV.xy+_SourceUV.zw;o.color=a.color;return o;
   }
   half4 Frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float4 sampled=_SourceTint;
    if(_HasAlbedo>.5)
    {
     float4 image=SAMPLE_TEXTURE2D(_SourceAlbedo,sampler_SourceAlbedo,i.uv);
     #if defined(UNITY_COLORSPACE_GAMMA)
     image.rgb=SRGBToLinear(image.rgb);
     #endif
     sampled*=image;
    }
    if(_VertexAlbedo>.5)sampled*=i.color;
    float luminance=dot(sampled.rgb,float3(.2126,.7152,.0722));
    float facing=abs(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)));
    float rim=1-facing;float gray=saturate(.17+luminance*.69+rim*rim*.14);
    float3 rgb=float3(gray,gray,gray);
    #if defined(UNITY_COLORSPACE_GAMMA)
    rgb=LinearToSRGB(rgb);
    #endif
    return half4(rgb,_GhostOpacity*sampled.a);
   }
   ENDHLSL
  }
 }
}
