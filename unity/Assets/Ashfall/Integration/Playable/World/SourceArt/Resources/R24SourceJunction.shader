// Fragment expressions translated from frozen alpha_junction_r24.gdshader.
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
  float2 q=float2(p.x,p.z);bool repaired=false;
if(i.sourcePosition.x>=-233.612972412 && i.sourcePosition.x<=-213.518909302 && i.sourcePosition.z>=-228.608455811 && i.sourcePosition.z<=-204.514240112 && i.sourcePosition.y>=20.4266797638 && i.sourcePosition.y<=20.5366797638){q=float2(.5,.5)+(i.sourcePosition.xz-float2(-223.565940857,-216.561347961))/float2(22.2489590115,26.693572998);repaired=true;}
else if(i.sourcePosition.x>=-158.495647583 && i.sourcePosition.x<=-146.365299072 && i.sourcePosition.z>=-68.7948571777 && i.sourcePosition.z<=-48.7010916138 && i.sourcePosition.y>=20.4382917023 && i.sourcePosition.y<=20.5482917023){q=float2(.5,.5)+(i.sourcePosition.xz-float2(-152.430473328,-58.7479743958))/float2(13.4003872342,22.2486284044);repaired=true;}
else if(i.sourcePosition.x>=-158.491924438 && i.sourcePosition.x<=-146.362094727 && i.sourcePosition.z>=-228.618175659 && i.sourcePosition.z<=-204.523563232 && i.sourcePosition.y>=20.441917572 && i.sourcePosition.y<=20.551917572){q=float2(.5,.5)+(i.sourcePosition.xz-float2(-152.427009583,-216.570869446))/float2(13.399810791,26.6940138075);repaired=true;}
else if(i.sourcePosition.x>=-92.0616418457 && i.sourcePosition.x<=-67.9669836426 && i.sourcePosition.z>=-68.8017160034 && i.sourcePosition.z<=-44.7069128418 && i.sourcePosition.y>=20.4241906738 && i.sourcePosition.y<=20.5341906738){q=float2(.5,.5)+(i.sourcePosition.xz-float2(-80.0143127441,-56.7543144226))/float2(26.6940646701,26.6942257351);repaired=true;}
else if(i.sourcePosition.x>=-15.908916626 && i.sourcePosition.x<=-0.10309967041 && i.sourcePosition.z>=-68.8022576904 && i.sourcePosition.z<=-44.7076834106 && i.sourcePosition.y>=20.4069081879 && i.sourcePosition.y<=20.5169081879){q=float2(.5,.5)+(i.sourcePosition.xz-float2(-8.00600814819,-56.7549705505))/float2(17.4842410617,26.693971422);repaired=true;}
else if(i.sourcePosition.x>=-15.8589402771 && i.sourcePosition.x<=-0.0524328613281 && i.sourcePosition.z>=-228.632244263 && i.sourcePosition.z<=-204.538379517 && i.sourcePosition.y>=20.4027196503 && i.sourcePosition.y<=20.5127196503){q=float2(.5,.5)+(i.sourcePosition.xz-float2(-7.95568656921,-216.58531189))/float2(17.4850082397,26.6931830512);repaired=true;}
else if(i.sourcePosition.x>=52.2008436584 && i.sourcePosition.x<=72.3096124268 && i.sourcePosition.z>=-224.649379883 && i.sourcePosition.z<=-204.515140381 && i.sourcePosition.y>=20.4699727631 && i.sourcePosition.y<=20.5799727631){q=float2(.5,.5)+(i.sourcePosition.xz-float2(62.2552280426,-214.582260132))/float2(22.2652986315,22.2935994466);repaired=true;}
float fade=1.-smoothstep(.003,.02,max(fwidth(q.x),fwidth(q.y)));
  float grain=hash(floor(q*512.));float3 asphalt=(repaired?float3(.26,.275,.28):float3(.115,.123,.128))*(.93+.14*grain*fade);
  float inner=repaired?.215:.07;float outer=repaired?.785:.93;
  float bars=step(.45,frac(q.x*12.))*step(inner,q.x)*step(q.x,outer);
  float crosswalk=bars*max(step(.13,q.y)*step(q.y,.21),step(.79,q.y)*step(q.y,.87));
  bars=step(.45,frac(q.y*12.))*step(inner,q.y)*step(q.y,outer);
  crosswalk=max(crosswalk,bars*max(step(.13,q.x)*step(q.x,.21),step(.79,q.x)*step(q.x,.87)));
  albedo=lerp(asphalt,float3(.68,.70,.67),crosswalk*.85);roughness=.97;
    float3 normal=normalize(i.normalWS);Light light=GetMainLight();float diffuse=saturate(dot(normal,light.direction));float3 ambient=max(SampleSH(normal),float3(.12,.14,.16));float3 h=normalize(light.direction+GetWorldSpaceNormalizeViewDir(i.positionWS));float spec=pow(saturate(dot(normal,h)),lerp(100,4,roughness));return half4(albedo*(ambient+light.color*diffuse)+spec*light.color*.04,1);
   }
   ENDHLSL
  }
 }
}
