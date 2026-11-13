Shader "Ashfall/R23LobbySky"
{
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
  Pass {
   Cull Off ZWrite Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.0
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   struct Attributes {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vert(Attributes a) {Varyings o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.direction=a.positionOS.xyz;return o;}
   float3 LinearColor(float3 rgb) {return SRGBToLinear(rgb);}
   half4 Frag(Varyings i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float y=normalize(i.direction).y;
    // front_menu_091 ProceduralSkyMaterial authored colors, default .15/.02 curves.
    float3 horizon=LinearColor(float3(163,201,228)/255.0);
    float3 top=LinearColor(float3(36,110,182)/255.0),bottom=LinearColor(float3(53,72,89)/255.0);
    float sky=1-pow(1-saturate(y),.6/.15),ground=1-pow(1-saturate(-y),.6/.02);
    float3 skyColor=lerp(horizon,top,sky);Light sun=GetMainLight();
    float angle=dot(normalize(i.direction),sun.direction),size=cos(radians(.5)),limit=cos(radians(30.0));
    float c2=(size-angle)/(size-limit);float halo=pow(saturate(1-c2),1.6/pow(.15,1.4));
    if(angle>size)skyColor=sun.color;else if(angle>limit)skyColor=lerp(skyColor,sun.color,halo);
    return half4(y>=0?skyColor:lerp(horizon,bottom,ground),1);
   }
   ENDHLSL
  }
 }
}
