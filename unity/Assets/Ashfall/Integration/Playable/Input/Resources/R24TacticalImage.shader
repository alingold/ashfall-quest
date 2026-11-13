Shader "Ashfall/R24TacticalImage"
{
    Properties { _MainTex("Actual opaque north-up overview",2D)="white"{} }
    SubShader
    {
        Tags {"Queue"="Overlay+111" "RenderType"="Transparent"}
        Pass
        {
            ZTest Always ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct A {float4 p:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            sampler2D _MainTex;
            V vert(A a){V v;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(v);v.p=UnityObjectToClipPos(a.p);v.uv=a.uv;v.color=a.color;return v;}
            // Godot overview viewport is opaque. URP scene color alpha can be
            // zero even over opaque world geometry; it is not UI coverage.
            half4 frag(V v):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);return half4(tex2D(_MainTex,v.uv).rgb*v.color.rgb,v.color.a);}
            ENDHLSL
        }
    }
}
