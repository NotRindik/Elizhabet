// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/Sirenix/Editor/ExtractSprite"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Rect("Rect", Vector) = (0,0,0,0)
        _TexelSize("TexelSize", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _Rect;

            VertexOutput vert(appdata v) {
                VertexOutput o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(VertexOutput i) : SV_Target {
                float2 uv = i.uv;
                uv *= _Rect.zw;
                uv += _Rect.xy;
                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}