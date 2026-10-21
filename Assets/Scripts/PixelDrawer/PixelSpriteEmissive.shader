Shader "PixelDrawer/SpriteEmissive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _EmissionTex ("Emission (R)", 2D) = "black" {}
        [PerRendererData] _EmissionIntensity ("Emission Intensity", Float) = 1

        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0

        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)

        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Tags
            {
                "LightMode" = "Universal2D"
            }

            HLSLPROGRAM

            #pragma vertex CombinedShapeLightVertex
            #pragma fragment CombinedShapeLightFragment
            #pragma target 2.0

            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/SurfaceData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/InputData2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_EmissionTex);
            SAMPLER(sampler_EmissionTex);

            TEXTURE2D(_AlphaTex);
            SAMPLER(sampler_AlphaTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _EmissionIntensity;
                float _EnableExternalAlpha;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 lightingUV : TEXCOORD1;
                float3 positionWS : TEXCOORD2;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings CombinedShapeLightVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);

                #ifdef PIXELSNAP_ON
                    output.positionCS = UnityPixelSnap(output.positionCS);
                #endif

                output.uv = input.uv;
                output.color = input.color * _Color;
                output.lightingUV = ComputeScreenPos(output.positionCS).xy;

                return output;
            }

            half4 SampleSprite(float2 uv)
            {
                half4 color = SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, uv);

                #if ETC1_EXTERNAL_ALPHA
                    half alpha = SAMPLE_TEXTURE2D(
                        _AlphaTex, sampler_AlphaTex, uv).r;

                    color.a = lerp(
                        color.a, alpha, _EnableExternalAlpha);
                #endif

                return color;
            }

            half4 CombinedShapeLightFragment(Varyings input) : SV_Target
            {
                half4 sprite = SampleSprite(input.uv) * input.color;

                // Base color and alpha for URP 2D lighting.
                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(
                    sprite.rgb,
                    sprite.a,
                    surfaceData);

                InitializeInputData(
                    input.uv,
                    inputData);

                inputData.lightingUV = input.lightingUV;

                // Apply actual URP 2D lights.
                half4 litColor = CombinedShapeLightShared(
                    surfaceData,
                    inputData);

                // Emission mask uses the red channel.
                half emission = SAMPLE_TEXTURE2D(
                    _EmissionTex,
                    sampler_EmissionTex,
                    input.uv).r;

                // Preserve premultiplied alpha blending.
                litColor.rgb += sprite.rgb
                    * emission
                    * _EmissionIntensity
                    * sprite.a;

                return litColor;
            }

            ENDHLSL
        }
    }
}
