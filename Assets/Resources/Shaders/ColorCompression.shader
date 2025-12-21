Shader "PostProcess/ColorCompression"
{
    Properties
    {
        [MainTexture] _MainTex ("Main Texture", 2D) = "white" {}
        _ColorResolution ("Color Resolution", Float) = 8
        _DitherSpread ("Dither Spread", Float) = 0.5
        _PPU ("Pixels Per Unit", Vector) = (16,16,0,0) 
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }

        Pass
        {
            Name "FullscreenRetro"
            ZTest Off ZWrite Off Cull Off Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_MainTex);
            SAMPLER(sampler_MainTex);

            float _ColorResolution;
            float _DitherSpread;
            float2 _PPU;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv    : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 4×4 Bayer matrix
            static const float Bayer4x4[16] =
            {
                -8,  0, -6,  2,
                 4, -4,  6, -2,
                -5,  3, -7,  1,
                 7, -1,  5, -3
            };

            float Dither4x4(float2 pixelPos)
            {
                int2 p = int2(pixelPos * _PPU) & 3;
                int index = p.x * 4 + p.y;
                return Bayer4x4[index] / 17.0;
            }

            float4 Frag (Varyings i) : SV_Target
            {
                float3 color =
                    SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb 
                    + Dither4x4(-_WorldSpaceCameraPos.xy - (i.uv + 0.5) * unity_OrthoParams.xy * 2) * _DitherSpread;
                
                return float4(floor(color * _ColorResolution + 0.5) / _ColorResolution, 1.0);

            }
            ENDHLSL
        }
    }
}