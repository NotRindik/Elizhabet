Shader "Eliza/Sprite Lit"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Main", 2D) = "white" {}

        [Header(LUT)]
        [Toggle(_LUT_ON)] _UseLut ("Enable", Float) = 0
        [NoScaleOffset] _LUT ("LUT", 2D) = "black" {}
        [NoScaleOffset] _LUT1 ("LUT1", 2D) = "black" {}
        [NoScaleOffset] _LUT2 ("LUT2", 2D) = "black" {}
        [NoScaleOffset] _LUT3 ("LUT3", 2D) = "black" {}

        [Header(Outline)]
        [Toggle(_OUTLINE_ON)] _UseOutline ("Enable", Float) = 0
        [Toggle(CORNERS_ON)] _SampleCorners ("Sample Corners", Float) = 0
        _OutlineThickness ("Thickness", Float) = 1
        _OutlineColor ("Color", Color) = (0, 0, 0, 1)

        [Header(Damage Flash)]
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0

        [Header(Render)]
        _Cutoff ("Alpha Clip", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    #pragma shader_feature_local _LUT_ON
    #pragma shader_feature_local _OUTLINE_ON
    #pragma shader_feature_local CORNERS_ON

    TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
    TEXTURE2D(_LUT);   SAMPLER(sampler_LUT);
    TEXTURE2D(_LUT1);  SAMPLER(sampler_LUT1);
    TEXTURE2D(_LUT2);  SAMPLER(sampler_LUT2);
    TEXTURE2D(_LUT3);  SAMPLER(sampler_LUT3);

    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        float4 _OutlineColor;
        float _OutlineThickness;
        float _FlashAmount;
        float _Cutoff;
    CBUFFER_END

    float4 LutLayer(float4 main, TEXTURE2D_PARAM(lut, lutSampler))
    {
        float width;
        float height;
        lut.GetDimensions(width, height);
        float2 uv = (main.rg * 255.0 + 0.5) / float2(width, height);
        float4 look = SAMPLE_TEXTURE2D(lut, lutSampler, uv);
        return float4(look.rgb, main.a * look.a);
    }

    float4 PremultiplyByAlpha(float4 color, float opacity)
    {
        float alpha = color.a * opacity;
        return float4(color.rgb * alpha, alpha);
    }

    float4 PremultipliedBlend(float4 base, float4 blend)
    {
        return base * (1.0 - blend.a) + blend;
    }

    float4 LutLayerPremultiplied(float4 main, TEXTURE2D_PARAM(lut, lutSampler))
    {
        float4 layer = LutLayer(main, TEXTURE2D_ARGS(lut, lutSampler));
        return PremultiplyByAlpha(layer, layer.a);
    }

    float4 LutSurface(float4 main)
    {
        float4 result = PremultipliedBlend(
            LutLayerPremultiplied(main, TEXTURE2D_ARGS(_LUT, sampler_LUT)),
            LutLayerPremultiplied(main, TEXTURE2D_ARGS(_LUT1, sampler_LUT1)));
        result = PremultiplyByAlpha(result, result.a);
        result = PremultipliedBlend(result, LutLayerPremultiplied(main, TEXTURE2D_ARGS(_LUT2, sampler_LUT2)));
        result = PremultipliedBlend(result, LutLayerPremultiplied(main, TEXTURE2D_ARGS(_LUT3, sampler_LUT3)));
        return result;
    }

    float OutlineSampleAlpha(float2 uv, float2 texel, float2 direction)
    {
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + texel * _OutlineThickness * direction).a;
    }

    float OutlineAlpha(float2 uv)
    {
        float width;
        float height;
        _MainTex.GetDimensions(width, height);
        float2 texel = 1.0 / float2(width, height);

        float sum = OutlineSampleAlpha(uv, texel, float2(1, 0)) + OutlineSampleAlpha(uv, texel, float2(-1, 0))
                  + OutlineSampleAlpha(uv, texel, float2(0, 1)) + OutlineSampleAlpha(uv, texel, float2(0, -1));
#if defined(CORNERS_ON)
        sum += OutlineSampleAlpha(uv, texel, float2(1, 1)) + OutlineSampleAlpha(uv, texel, float2(-1, -1))
             + OutlineSampleAlpha(uv, texel, float2(1, -1)) + OutlineSampleAlpha(uv, texel, float2(-1, 1));
#endif
        return saturate(sum);
    }

    float4 SpriteSurface(float2 uv, out half3 normalTS)
    {
        float4 main = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

#if defined(_LUT_ON)
        float4 color = LutSurface(main);
        normalTS = half3(0, 0, 1);
#else
        float4 color = main;
        normalTS = half3(0, 0, 0);
#endif

#if defined(_OUTLINE_ON)
        if (main.a < 0.5)
            color = float4(_OutlineColor.rgb, OutlineAlpha(uv));
#endif

        color.rgb = lerp(color.rgb, 1.0 - color.rgb, _FlashAmount);
        return color;
    }

    ENDHLSL

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZTest [_ZTest]
            ZWrite Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                float4 screenPosition : TEXCOORD1;
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 _RendererColor;

            Varyings LitVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                o.positionCS = TransformObjectToHClip(input.positionOS);
#if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.normalWS = TransformObjectToWorldDir(input.normal);
#endif
                o.uv = input.uv;
                o.screenPosition = ComputeScreenPos(o.positionCS);
                o.color = input.color * _RendererColor * unity_SpriteColor;
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalTS;
                half4 color = (half4)SpriteSurface(input.uv, normalTS);
                clip(color.a - _Cutoff);
                color *= input.color;

                SurfaceData2D surfaceData;
                InputData2D inputData;
                InitializeSurfaceData(color.rgb, color.a, half4(1, 1, 1, 1), normalTS, surfaceData);
                InitializeInputData(input.uv, half2(input.screenPosition.xy / input.screenPosition.w), inputData);
#if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_DATA_2D(inputData, input.positionWS, input.positionCS);
                surfaceData.normalWS = input.normalWS;
#endif
                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering" }

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsVertex
            #pragma fragment NormalsFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"

            Varyings NormalsVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.normalWS = TransformObjectToWorldDir(input.normal);
                o.tangentWS = TransformObjectToWorldDir(input.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * input.tangent.w;
                o.color = input.color * unity_SpriteColor;
                return o;
            }

            half4 NormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalTS;
                half4 color = half4(1, 1, 1, SpriteSurface(input.uv, normalTS).a);
                clip(color.a - _Cutoff);

                return NormalsRenderingShared(color, normalTS, input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex ForwardVertex
            #pragma fragment ForwardFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            half4 _RendererColor;

            Varyings ForwardVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                o.positionCS = TransformObjectToHClip(input.positionOS);
#if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(input.positionOS);
#endif
                o.uv = input.uv;
                o.color = input.color * _RendererColor * unity_SpriteColor;
                return o;
            }

            half4 ForwardFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalTS;
                half4 color = (half4)SpriteSurface(input.uv, normalTS);
                color *= input.color;
                return color;
            }
            ENDHLSL
        }
    }
}
