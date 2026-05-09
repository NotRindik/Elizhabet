Shader "PostFX/PixelateWorld"
{
    Properties
    {
        [MainTexture] _MainTex ("Main Texture", 2D) = "white" {}
        _PPU ("World Pixel Size", Float) = 0.0625
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Name "PixelateWorld"
            ZTest Off ZWrite Off Cull Off Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _PPU;
            CBUFFER_END

            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(v.vertex);
                o.uv  = v.uv;
                return o;
            }


            float4 frag(Varyings i) : SV_Target
            {
                float pixelSize = 1.0 / _PPU;
                float2 viewSize = unity_OrthoParams.xy * 2.0;

                // НЕ снапаем camPos — камера уже снапнута на C# стороне
                float2 camPos = _WorldSpaceCameraPos.xy;

                float2 worldXY  = camPos + (i.uv - 0.5) * viewSize;
                float2 snapped  = floor(worldXY / pixelSize + 0.5) * pixelSize;
                float2 snappedUV = (snapped - camPos) / viewSize + 0.5;

                snappedUV = clamp(snappedUV, 0.0001, 0.9999);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, snappedUV);
            }
            ENDHLSL
        }
    }
}