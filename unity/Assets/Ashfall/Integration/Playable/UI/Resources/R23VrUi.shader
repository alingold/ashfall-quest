Shader "Ashfall/R23VrUi"
{
    Properties { [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth test",Float)=8 _UiClipBounds("Catalog clip min/max/enabled",Vector)=(0,0,0,0) _UiClipY("Object-to-panel Y",Vector)=(0,1,0,0) _MainTex ("Font atlas", 2D) = "white" {} _Color ("Tint",Color) = (1,1,1,1) _Font ("Alpha-only font atlas",Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Overlay+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZTest [_ZTest] ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR;float panelY:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex; float4 _Color;float _Font;float4 _UiClipY,_UiClipBounds;
            v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;o.panelY=dot(float4(v.vertex.xyz,1),_UiClipY);return o; }
            half4 frag(v2f i):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);if(_UiClipBounds.z>.5)clip(min(i.panelY-_UiClipBounds.x,_UiClipBounds.y-i.panelY));half4 pixel=tex2D(_MainTex,i.uv);pixel.rgb=lerp(pixel.rgb,half3(1,1,1),_Font);return pixel*i.color; }
            ENDHLSL
        }
    }
}
