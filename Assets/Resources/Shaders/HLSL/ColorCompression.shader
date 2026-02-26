Shader "PostProcess/ColorCompression"
{
    Properties
    {
        [MainTexture] _MainTex ("Main Texture", 2D) = "white" {}
        _Params ("Color Res Multiplier, Color Res Divisor, Dithering factor, Pixels per unit", Vector) = (4,0.25,0.0900000036,16)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Name "ColorCompression"
            ZTest Off   ZWrite Off   Cull Off   Blend Off

HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial) float4 _Params; CBUFFER_END
            
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            static const float bayer[16] = {
                -8,  0, -6,  2,
                 4, -4,  6, -2,
                -5,  3, -7,  1,
                 7, -1,  5, -3};
            float get_bayer(float2 pixelPos)
            {
                int2 p = int2(pixelPos * _Params.w) & 3; //Типа мы очень крутые и это на самом деле % 4 B)) 
                int index = (p.x << 2) + p.y; //Типа мы слишком круты чтобы множить на 4
                return bayer[index] * 0.125 * _Params.z;
            }
            
            #pragma vertex vert
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }
            #pragma fragment frag
            float3 frag (Varyings i) : SV_Target
            {
                return round((SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb + get_bayer(_WorldSpaceCameraPos.xy + (i.uv - 0.5) * unity_OrthoParams.xy * 2)) * _Params.x) * _Params.y;
            }
ENDHLSL
        }
    }
}