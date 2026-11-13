Shader "Ashfall/R23LobbySky"
{
 Properties { _Night("Night",Float)=0 }
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
   float _Night;
   float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   struct Attributes {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vert(Attributes a) {Varyings o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.direction=a.positionOS.xyz;return o;}
   float3 R23WorkingColor(float3 rgb) {
    #ifdef UNITY_COLORSPACE_GAMMA
    return LinearToSRGB(rgb);
    #else
    return rgb;
    #endif
   }
   float3 R23SkyGradient(float y,float3 top,float3 horizon,float3 bottom) {
    // Actual generated ProceduralSkyMaterial shader. Source_color endpoints
    // have already been decoded to linear, including in Unity's Gamma project.
    float v_angle=clamp(y,-1.0,1.0);
    float3 sky=lerp(top,horizon,saturate(pow(1.0-v_angle,0.6/0.15)));
    float3 ground=lerp(bottom,horizon,saturate(pow(1.0+v_angle,0.6/0.02)));
    return lerp(ground,sky,step(0.0,v_angle));
   }
   half4 Frag(Varyings i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 direction=normalize(i.direction);
    if(_Night>.5){
     float3 d=direction;
     float3 c=lerp(float3(.025,.055,.075),float3(.003,.005,.018),saturate(d.y*1.8));
     c=lerp(float3(.004,.008,.014),c,smoothstep(-.08,.02,d.y));
     c+=float3(.055,.016,.005)*pow(saturate(1-abs(d.y)),12);
     float2 uv=float2(atan2(d.x,d.z)*.159155+.5,asin(d.y)*.31831+.5);
     float2 g=uv*float2(620,310),id=floor(g),f=frac(g)-.5;
     float star=step(.994,Hash(id))*smoothstep(.16,.015,length(f))*smoothstep(.02,.25,d.y);
     c+=star*(.4+.12*sin(_Time.y*.45+Hash(id)*20));
     float3 moon=normalize(float3(-.30,.78,1));float disk=smoothstep(.997,.9977,dot(d,moon));
     float2 lunar=(d.xy-moon.xy)*16;
     float crater=.68+.22*sqrt(saturate(1-dot(lunar,lunar)));
     crater-=.16*exp(-dot(lunar-float2(.25,.1),lunar-float2(.25,.1))*35);
     crater-=.12*exp(-dot(lunar-float2(-.35,-.2),lunar-float2(-.35,-.2))*65);
     c+=disk*float3(.55,.63,.67)*crater;
     c+=float3(.07,.1,.13)*pow(saturate(dot(d,moon)),170)*.25;
     float bands=sin(d.x*11+d.z*5+_Time.y*.013)+sin(d.z*19-d.x*7+_Time.y*.009);
     float cloud=smoothstep(.7,1.7,bands)*smoothstep(.02,.16,d.y)*(1-smoothstep(.2,.5,d.y));
     c=lerp(c,float3(.026,.034,.052),cloud*.42);
     return half4(R23WorkingColor(c),1);
    }
    float3 horizon=SRGBToLinear(float3(163,201,228)/255.0);
    float3 top=SRGBToLinear(float3(36,110,182)/255.0),bottom=SRGBToLinear(float3(53,72,89)/255.0);
    float3 skyColor=R23SkyGradient(direction.y,top,horizon,bottom);
    // Source sun has no authored angular_distance override: Light3D default is
    // 0 degrees, passed as radians to the sky. Do not invent a .5-degree disk.
    Light sun=GetMainLight();float angle=dot(direction,sun.direction);
    float size=1.0,limit=cos(radians(30.0));
    if(direction.y>=0) {
     if(angle>size)skyColor=sun.color;
     else if(angle>limit) {
      float c2=(size-angle)/(size-limit);
      float halo=saturate(pow(saturate(1.0-c2),1.6/pow(0.15,1.4)));
      skyColor=lerp(skyColor,sun.color,halo);
     }
    }
    return half4(R23WorkingColor(skyColor),1);
   }
   ENDHLSL
  }
 }
}
