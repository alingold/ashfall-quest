Shader "Ashfall/R24KestrelSkin" {
 Properties { _Albedo("Federal albedo",2D)="white"{} _NormalMap("Federal normal",2D)="bump"{} _Occlusion("Federal AO",2D)="white"{} _Roughness("Federal roughness red",2D)="white"{} _Emission("Federal emission",2D)="black"{} _BaseMap("Shadowcaster base",2D)="white"{} _BaseColor("Shadowcaster color",Color)=(1,1,1,1) _Cull("Cull",Float)=2 }
 SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 Pass { Name "ForwardLit" Tags {"LightMode"="UniversalForward"} Cull Back
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
 #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_Albedo);SAMPLER(sampler_Albedo);TEXTURE2D(_NormalMap);SAMPLER(sampler_NormalMap);TEXTURE2D(_Occlusion);SAMPLER(sampler_Occlusion);TEXTURE2D(_Roughness);SAMPLER(sampler_Roughness);TEXTURE2D(_Emission);SAMPLER(sampler_Emission);
 struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;UNITY_VERTEX_OUTPUT_STEREO};
 Varyings Vert(Attributes a){Varyings o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(a.normalOS,a.tangentOS);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;o.tangentWS=half4(n.tangentWS,a.tangentOS.w*GetOddNegativeScale());o.uv=a.uv;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
 half4 Frag(Varyings v):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);half3 albedo=SAMPLE_TEXTURE2D(_Albedo,sampler_Albedo,v.uv).rgb;half3 normal=SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,v.uv).xyz*2-1;normal.xy*=.8h;normal=normalize(normal);half3 n=normalize(v.normalWS),t=normalize(v.tangentWS.xyz),b=cross(n,t)*v.tangentWS.w;InputData input=(InputData)0;input.positionWS=v.positionWS;input.positionCS=v.positionCS;input.normalWS=NormalizeNormalPerPixel(TransformTangentToWorld(normal,half3x3(t,b,n)));input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);input.shadowCoord=TransformWorldToShadowCoord(v.positionWS);input.bakedGI=SampleSH(input.normalWS);input.vertexLighting=VertexLighting(v.positionWS,input.normalWS);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);input.shadowMask=half4(1,1,1,1);SurfaceData s=(SurfaceData)0;s.albedo=albedo;s.alpha=1;s.metallic=.2h;s.specular=half3(.04h,.04h,.04h);s.smoothness=1-SAMPLE_TEXTURE2D(_Roughness,sampler_Roughness,v.uv).r;s.normalTS=normal;s.occlusion=SAMPLE_TEXTURE2D(_Occlusion,sampler_Occlusion,v.uv).r;
 // FederalSoldierSkin emission_operator1 MULTIPLY; exact source textures/sample product.
 s.emission=SAMPLE_TEXTURE2D(_Emission,sampler_Emission,v.uv).rgb*albedo*.4h;half4 color=UniversalFragmentPBR(input,s);color.rgb=MixFog(color.rgb,v.fog);return color;}
 ENDHLSL }
 UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
