Shader "Ashfall/R24 Matched Source Art"
{
 Properties{_Style("Source R10/R14/R17/R19 profile",Float)=0 _Art("Original source artwork",2D)="white"{}}
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
   TEXTURE2D(_Art);SAMPLER(sampler_Art);CBUFFER_START(UnityPerMaterial)float _Style;CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;float3 uv:TEXCOORD0;float4 c:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V{float4 p:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;float3 uv:TEXCOORD2;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
   V Vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionWS=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(a.n);o.uv=a.uv;o.color=a.c;return o;}
   half4 Frag(V i,bool front:SV_IsFrontFace):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 p=i.uv,albedo;float roughness=1,metallic=0;
    if(_Style<.5)
    {
     float side=step(.95,abs(p.z-.5)*2),rib=.5+.5*cos(p.x*6.2831853*40);float3 paint=float3(.23,.35,.39)*lerp(.72,1,smoothstep(.05,.4,rib)*side+1-side);
     float border=1-smoothstep(.008,.018,min(min(p.y,1-p.y),min(p.x,1-p.x))),end=step(.97,abs(p.x-.5)*2),bars=end*(1-smoothstep(.015,.025,min(abs(p.z-.25),abs(p.z-.75))));paint=lerp(paint,float3(.12,.17,.18),border*.7);paint=lerp(paint,float3(.48,.51,.49),bars);
     float stripe=side*step(.65,p.y)*step(p.y,.69)*step(.07,p.x)*step(p.x,.21);albedo=lerp(paint,float3(.81,.77,.56),stripe);roughness=.84;metallic=.35;
    }
    else if(_Style<1.5)
    {
     float3 n=normalize(cross(ddx(p),ddy(p)));float side=step(.8,abs(n.z)),ends=step(.8,abs(n.x));float3 paint=float3(.34,.48,.55)*(.91+.09*smoothstep(.12,.9,p.y));
     float cab=max(step(.72,p.x)*step(p.x,.95),step(.05,p.x)*step(p.x,.28)),window=side*cab*step(.51,p.y)*step(p.y,.87);window=max(window,ends*step(.58,p.y)*step(p.y,.88));
     float wheel=step(p.y,.29)*max(1-step(.13,abs(p.x-.19)),1-step(.13,abs(p.x-.8))),bumper=ends*step(p.y,.2),seam=side*(1-smoothstep(.002,.005,min(abs(p.x-.64),abs(p.y-.37))));
     albedo=lerp(paint,float3(.10,.16,.19),seam*.65);albedo=lerp(albedo,float3(.025,.065,.085),window);albedo=lerp(albedo,float3(.028,.032,.037),max(wheel,bumper));float lamp=ends*step(.3,p.y)*step(p.y,.42)*step(.29,abs(p.z-.5));albedo=lerp(albedo,lerp(float3(.7,.035,.02),float3(.92,.84,.58),step(.5,p.x)),lamp);roughness=lerp(.72,.22,window);metallic=.2;
    }
    else if(_Style<2.5){albedo=float3(23,28,33)/255;roughness=.94;}
    else if(_Style<4.5)
    {
     float board=frac(p.x*10),seam=1-smoothstep(.009,.025,min(board,1-board)),grain=.5+.5*sin(p.y*75+sin(p.x*93)*2),fade=1-smoothstep(.018,.055,max(fwidth(p.x),fwidth(p.y)));
     if(_Style>3.5){float knot=sin(p.y*18+sin(p.x*29)*1.8);albedo=i.color.rgb*(.88+.12*grain*fade+.035*knot*fade)*(1-.48*seam*fade);roughness=.95;}
     else{float rib=.5+.5*cos(p.x*6.2831853*10),edge=1-smoothstep(.01,.025,min(p.y,1-p.y));albedo=i.color.rgb*(.83+.17*smoothstep(.12,.5,rib)*fade)*(1-.35*edge);float lock=1-smoothstep(.008,.016,min(abs(p.x-.25),abs(p.x-.75)));albedo=lerp(albedo,float3(.3,.32,.31),lock*.65*fade);roughness=.83;metallic=.35;}
    }
    else if(_Style<7.5)
    {
     float3 n=normalize(cross(ddx(p),ddy(p)));float side=step(.65,abs(n.z)),ends=step(.65,abs(n.x));
     if(_Style>6.5){float tread=.5+.5*sin((p.x+p.z)*90),fade=1-smoothstep(.01,.04,max(fwidth(p.x),fwidth(p.z)));albedo=float3(.035,.041,.045)*(.86+.14*tread*fade);roughness=.96;}
     else{float3 paint=_Style>5.5?float3(.21,.32,.39):float3(.30,.43,.49);float window=side*step(.53,p.y)*step(p.y,.89)*max(step(.7,p.x),step(p.x,.29));window=max(window,ends*step(.56,p.y)*step(p.y,.90)*step(.12,p.z)*step(p.z,.88));float band=side*step(.34,p.y)*step(p.y,.38),seam=side*(1-smoothstep(.002,.006,min(abs(p.x-.62),abs(p.y-.42))));albedo=paint*(.88+.12*p.y);albedo=lerp(albedo,float3(.08,.12,.15),seam*.8);albedo=lerp(albedo,float3(.69,.72,.67),band*.85);albedo=lerp(albedo,float3(.018,.055,.075),window);albedo=lerp(albedo,float3(.08,.09,.1),ends*step(p.y,.21));float lamp=ends*step(.25,p.y)*step(p.y,.40)*step(.3,abs(p.z-.5));albedo=lerp(albedo,lerp(float3(.57,.018,.012),float3(.92,.83,.56),step(.5,p.x)),lamp);metallic=.30;roughness=lerp(.65,.18,window);}
    }
    else if(_Style<8.5)
    {
     float3 n=abs(normalize(cross(ddx(p),ddy(p))));float2 q=float2(n.z>n.x?p.x:p.z,p.y);float fade=1-smoothstep(.012,.04,max(fwidth(q.x),fwidth(q.y))),grain=.5+.5*sin(q.y*160+sin(q.x*35)*3+sin(q.y*13));float board=frac(q.x*8),seam=1-smoothstep(.008,.024,min(board,1-board));float3 base=lerp(float3(.24,.12,.055),float3(.42,.28,.14),saturate(i.color.r));albedo=base*(.88+.16*grain*fade)*(1-.35*seam*fade);roughness=.94;
    }
    else
    {
     // R19 already set source UV.y=1-profile.y; reflect only image lookup V.
     // Existing loader's reflected geometry/winding retains the physical source front.
     float u=_Style>9.5?(front?p.x:1-p.x):1-p.x;albedo=SAMPLE_TEXTURE2D(_Art,sampler_Art,float2(u,1-p.y)).rgb;roughness=.83;
    }
    float3 normal=normalize(i.normalWS);Light light=GetMainLight();float diffuse=saturate(dot(normal,light.direction));float3 ambient=max(SampleSH(normal),float3(.12,.14,.16));float3 h=normalize(light.direction+GetWorldSpaceNormalizeViewDir(i.positionWS));float spec=pow(saturate(dot(normal,h)),lerp(100,4,roughness));return half4(albedo*(ambient+light.color*diffuse)*(1-.35*metallic)+spec*light.color*lerp(.04,albedo,metallic),1);
   }
   ENDHLSL
  }
 }
}
