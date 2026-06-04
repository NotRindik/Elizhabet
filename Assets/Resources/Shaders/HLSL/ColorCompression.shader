Shader "PostProcess/ColorCompression"
{
    Properties
    {
        _Params ("Color Res Multiplier, Color Res Divisor, Dithering factor, Pixels per unit", Vector) = (4,0.25,0.09,16)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Name "ColorCompression"
            ZTest Off   ZWrite Off   Cull Off   Blend Off

HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // _BlitTexture и sampler_LinearClamp уже объявлены в Blit.hlsl
            CBUFFER_START(UnityPerMaterial) float4 _Params; CBUFFER_END

            static const float bayer[16] = {
                -8,  0, -6,  2,
                 4, -4,  6, -2,
                -5,  3, -7,  1,
                 7, -1,  5, -3
            };

            float get_bayer(float2 pixelPos)
            {
                int2 p = int2(pixelPos * _Params.w) & 3;
                int index = (p.x << 2) + p.y;
                return bayer[index] * 0.125 * _Params.z;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                float3 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float3 dithered = col + get_bayer(_WorldSpaceCameraPos.xy + (uv - 0.5) * unity_OrthoParams.xy * 2);
                return float4(round(dithered * _Params.x) * _Params.y, 1.0);
            }
ENDHLSL
        }
    }
}