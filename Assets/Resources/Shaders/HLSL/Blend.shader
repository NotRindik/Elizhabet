Shader "Custom/LayerBlend"
{
    Properties
    {
        _MainTex  ("Current Composite", 2D) = "clear" {}
        _NewLayer ("New Layer",         2D) = "clear" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        ZWrite Off
        ZTest Always
        Cull Off
        Blend One Zero // полностью замен€ем пиксель результатом, без накоплени€ альфы

        Pass
        {
            Name "LayerBlend"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // URP-way: TEXTURE2D + SAMPLER вместо sampler2D из Built-in RP
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NewLayer);
            SAMPLER(sampler_NewLayer);

            // ќб€зательный cbuffer дл€ SRP Batcher
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NewLayer_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 base  = SAMPLE_TEXTURE2D(_MainTex,  sampler_MainTex,  IN.uv);
                half4 layer = SAMPLE_TEXTURE2D(_NewLayer, sampler_NewLayer, IN.uv);

                // 1 там где у базы есть пиксель, 0 там где пусто
                float mask = step(0.01, base.a);

                half4 result;
                // новый слой перекрывает базу только в пределах еЄ силуэта
                result.rgb = lerp(base.rgb, layer.rgb, layer.a * mask);
                // альфа (силуэт/форма) Ч всегда от базы, новый слой еЄ не мен€ет
                result.a   = base.a;

                return result;
            }
            ENDHLSL
        }
    }
}