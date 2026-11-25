Shader "PixelDrawer/SpriteEmissive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _EmissionTex ("Emission (R)", 2D) = "black" {}
        [PerRendererData] _EmissionIntensity ("Emission Intensity", Float) = 1
        [PerRendererData] _CorrectionLut ("Correction LUT", 3D) = "" {}
        [PerRendererData] _CorrectionOn ("Correction On", Float) = 0
        [PerRendererData] _CorrectionLutSize ("Correction LUT Size", Float) = 32
        [PerRendererData] _BackdropTex ("Backdrop", 2D) = "black" {}
        [PerRendererData] _BackdropST ("Backdrop ST", Vector) = (1,1,0,0)
        [PerRendererData] _LiveBlendOn ("Live Blend On", Float) = 0
        [PerRendererData] _LiveBlendMode ("Live Blend Mode", Float) = 0

        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _NeutralWhite ("Neutral White", Float) = 0
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 10
        [HideInInspector] _BlendOp ("Blend Op", Float) = 0
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
        Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
        BlendOp [_BlendOp], Add

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

            TEXTURE3D(_CorrectionLut);
            SAMPLER(sampler_CorrectionLut);
            float _CorrectionOn;
            float _CorrectionLutSize;

            half3 ApplyCorrection(half3 rgb)
            {
                if(_CorrectionOn < 0.5)
                    return rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    rgb = LinearToSRGB(rgb);
                #endif
                float size = max(_CorrectionLutSize, 2.0);
                float3 uvw = saturate(rgb) * ((size - 1.0) / size) + 0.5 / size;
                rgb = SAMPLE_TEXTURE3D(_CorrectionLut, sampler_CorrectionLut, uvw).rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    rgb = SRGBToLinear(rgb);
                #endif
                return rgb;
            }

            TEXTURE2D(_BackdropTex);
            SAMPLER(sampler_BackdropTex);
            float4 _BackdropST;
            float _LiveBlendOn;
            float _LiveBlendMode;

            float BlendScreen(float b, float s) { return b + s - b * s; }
            float BlendColorBurn(float b, float s) { return b >= 1.0 ? 1.0 : (s <= 0.0 ? 0.0 : 1.0 - min(1.0, (1.0 - b) / s)); }
            float BlendColorDodge(float b, float s) { return b <= 0.0 ? 0.0 : (s >= 1.0 ? 1.0 : min(1.0, b / (1.0 - s))); }
            float BlendHard(float b, float s) { return s <= 0.5 ? b * 2.0 * s : BlendScreen(b, 2.0 * s - 1.0); }

            float BlendSoft(float b, float s)
            {
                if(s <= 0.5) return b - (1.0 - 2.0 * s) * b * (1.0 - b);
                float d = b <= 0.25 ? ((16.0 * b - 12.0) * b + 4.0) * b : sqrt(b);
                return b + (2.0 * s - 1.0) * (d - b);
            }

            float BlendChannel(int mode, float b, float s)
            {
                float r = s;
                switch(mode)
                {
                    case 1: r = min(b, s); break;
                    case 2: r = b * s; break;
                    case 3: r = BlendColorBurn(b, s); break;
                    case 4: r = max(0.0, b + s - 1.0); break;
                    case 5: r = max(0.0, b - s); break;
                    case 6: r = max(b, s); break;
                    case 7: r = BlendScreen(b, s); break;
                    case 8:
                    case 9: r = BlendColorDodge(b, s); break;
                    case 10:
                    case 11: r = min(1.0, b + s); break;
                    case 12: r = BlendHard(s, b); break;
                    case 13: r = BlendSoft(b, s); break;
                    case 14: r = BlendHard(b, s); break;
                    case 15: r = abs(b - s); break;
                    case 16: r = s <= 0.5 ? BlendColorBurn(b, 2.0 * s) : BlendColorDodge(b, 2.0 * s - 1.0); break;
                    case 17: r = saturate(b + 2.0 * s - 1.0); break;
                    case 18: r = s <= 0.5 ? min(b, 2.0 * s) : max(b, 2.0 * s - 1.0); break;
                    case 19: r = b + s >= 1.0 ? 1.0 : 0.0; break;
                    case 20: r = b + s - 2.0 * b * s; break;
                    case 23: r = s <= 0.0 ? 1.0 : min(1.0, b / s); break;
                }
                return r;
            }

            float BlendLum(float3 c) { return 0.3 * c.x + 0.59 * c.y + 0.11 * c.z; }

            float3 BlendSetLum(float3 c, float l)
            {
                c += l - BlendLum(c);
                float lum = BlendLum(c);
                float n = min(c.x, min(c.y, c.z));
                float x = max(c.x, max(c.y, c.z));
                if(n < 0.0) c = lum + (c - lum) * (lum / max(0.0001, lum - n));
                if(x > 1.0) c = lum + (c - lum) * ((1.0 - lum) / max(0.0001, x - lum));
                return c;
            }

            float BlendSat(float3 c) { return max(c.x, max(c.y, c.z)) - min(c.x, min(c.y, c.z)); }

            float3 BlendSetSat(float3 c, float s)
            {
                float mx = max(c.x, max(c.y, c.z));
                float mn = min(c.x, min(c.y, c.z));
                float range = mx - mn;
                if(range <= 0.0001) return float3(0, 0, 0);
                return (c - mn) / range * s;
            }

            float3 BlendColor(int mode, float3 b, float3 s)
            {
                if(mode == 21) return BlendLum(s) < BlendLum(b) ? s : b;
                if(mode == 22) return BlendLum(s) > BlendLum(b) ? s : b;
                if(mode == 24) return BlendSetLum(BlendSetSat(s, BlendSat(b)), BlendLum(b));
                if(mode == 25) return BlendSetLum(BlendSetSat(b, BlendSat(s)), BlendLum(b));
                if(mode == 26) return BlendSetLum(s, BlendLum(b));
                if(mode == 27) return BlendSetLum(b, BlendLum(s));
                return float3(BlendChannel(mode, b.x, s.x), BlendChannel(mode, b.y, s.y), BlendChannel(mode, b.z, s.z));
            }

            half3 ApplyLiveBlend(half3 rgb, float2 uv)
            {
                if(_LiveBlendOn < 0.5)
                    return rgb;
                half4 back = SAMPLE_TEXTURE2D(_BackdropTex, sampler_BackdropTex, uv * _BackdropST.xy + _BackdropST.zw);
                if(back.a <= 0.0)
                    return rgb;
                float3 s = rgb;
                float3 b = back.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    s = LinearToSRGB(s);
                    b = LinearToSRGB(b);
                #endif
                float3 mixed = saturate(BlendColor((int)round(_LiveBlendMode), b, s));
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    mixed = SRGBToLinear(mixed);
                #endif
                return lerp(rgb, (half3)mixed, back.a);
            }

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _NeutralWhite;
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
                half4 sprite = SampleSprite(input.uv);
                sprite.rgb = ApplyCorrection(sprite.rgb);
                sprite.rgb = ApplyLiveBlend(sprite.rgb, input.uv);
                sprite *= input.color;

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

                half emission = SAMPLE_TEXTURE2D(
                    _EmissionTex,
                    sampler_EmissionTex,
                    input.uv).r;

                half alpha = litColor.a;
                litColor.rgb = litColor.rgb * alpha
                    + sprite.rgb * emission * _EmissionIntensity * alpha;
                litColor.rgb += (1.0 - alpha) * _NeutralWhite;

                return litColor;
            }

            ENDHLSL
        }
    }
}
