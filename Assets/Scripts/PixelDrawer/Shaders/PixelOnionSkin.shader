Shader "Hidden/PixelDrawer/OnionSkin"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 0, 0, 0.5)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                
                float luminance = dot(
                    tex.rgb,
                    float3(0.2126, 0.7152, 0.0722)
                );
                
                float contrast = (luminance - 0.5) * 1.6 + 0.5;
                contrast = saturate(contrast);
                
                float3 shadowColor = _Color.rgb * 0.25;
                float3 highlightColor = lerp(
                    _Color.rgb,
                    float3(1.0, 1.0, 1.0),
                    0.65
                );

                float3 finalColor = lerp(
                    shadowColor,
                    highlightColor,
                    contrast
                );

                return fixed4(
                    finalColor,
                    tex.a * _Color.a
                );
            }
            ENDHLSL
        }
    }
}