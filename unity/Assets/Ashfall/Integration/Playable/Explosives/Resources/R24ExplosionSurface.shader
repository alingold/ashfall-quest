Shader "Ashfall/R24 Source Explosion"
{
 Properties{_Age("Source age",Float)=0 _Mode("Fire Smoke Pressure Flat",Float)=0 _Hot("Hot core",Float)=0 _Color("Source tint",Color)=(1,1,1,1) _DstBlend("Destination blend",Float)=10}
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
  Pass
  {
   Tags{"LightMode"="UniversalForward"}
   ZTest LEqual ZWrite Off Cull Off Blend SrcAlpha [_DstBlend]
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial) float _Age,_Mode,_Hot;half4 _Color;CBUFFER_END
   float hash3(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
   float cloud_noise(float3 p)
   {
    float3 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(lerp(hash3(cell),hash3(cell+float3(1,0,0)),f.x),lerp(hash3(cell+float3(0,1,0)),hash3(cell+float3(1,1,0)),f.x),f.y),lerp(lerp(hash3(cell+float3(0,0,1)),hash3(cell+float3(1,0,1)),f.x),lerp(hash3(cell+float3(0,1,1)),hash3(cell+float3(1,1,1)),f.x),f.y),f.z);
   }
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 p:SV_POSITION;float3 source:TEXCOORD0;float3 normalWS:TEXCOORD1;float3 positionWS:TEXCOORD2;float2 uv:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a)
   {
    V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 p=a.p.xyz*float3(1,1,-1),n=a.n*float3(1,1,-1);
    if(_Mode<.5)p+=n*(cloud_noise(p*4.3+float3(0,-_Age*1.7,2))-.35)*.24;
    else if(_Mode<1.5)p+=n*(cloud_noise(p*5+float3(0,-_Age*.4,3))-.35)*.30;
    o.source=p;o.positionWS=TransformObjectToWorld(p*float3(1,1,-1));o.p=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.n);o.uv=a.uv;return o;
   }
   half4 Frag(V i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    if(_Mode>2.5)return _Color;
    if(_Mode>1.5){float r=length(i.uv*2-1),band=smoothstep(.72,.86,r)*(1-smoothstep(.88,1,r)),dust=.65+.35*sin(atan2(i.uv.y-.5,i.uv.x-.5)*19+r*35);return half4(.65,.30,.10,band*dust*max(0,1-_Age/.55)*.30);}
    float facing=saturate(dot(normalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)));
    if(_Mode<.5)
    {
     float3 flow=i.source*4.8+float3(0,-_Age*3.5,0);float cloud=cloud_noise(flow)*.7+cloud_noise(flow*2.1)*.3;
     float heat=saturate(1.12-_Age*1.65+cloud*.42+_Hot*.32-(1-facing)*.24);
     float3 color=lerp(float3(.32,.045,.008),float3(1,.24,.018),smoothstep(.12,.60,heat));color=lerp(color,float3(1,.72,.20),smoothstep(.58,.88,heat));color=lerp(color,float3(1,.94,.72),smoothstep(.88,1,heat));
     float envelope=smoothstep(0,.025,_Age)*(1-smoothstep(.22,.82-_Hot*.20,_Age));return half4(color*(1+heat*.7),envelope*smoothstep(.02,.40,facing)*(.70+cloud*.30));
    }
    float cloud=cloud_noise(i.source*5.7+float3(0,-_Age*.7,0));float3 normalVS=normalize(mul((float3x3)UNITY_MATRIX_V,normalize(i.normalWS)));float shade=saturate(dot(normalVS,normalize(float3(-.4,.8,.2)))*.30+.55);float3 color=lerp(float3(.045,.040,.035),float3(.22,.205,.18),cloud*shade);color+=float3(.16,.045,.006)*max(0,1-_Age*1.6)*cloud;
    return half4(color,smoothstep(.045,.25,_Age)*(1-smoothstep(1.05,2.45,_Age))*smoothstep(.015,.42,facing)*(.40+cloud*.32));
   }
   ENDHLSL
  }
 }
}
