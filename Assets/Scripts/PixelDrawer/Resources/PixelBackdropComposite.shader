Shader "Hidden/PixelDrawer/BackdropComposite"
{
    Properties
    {
        _MainTex ("Previous", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../PixelBlend.hlsl"

            TEXTURE2D(_MainTex);
            TEXTURE2D(_SrcTex);
            TEXTURE2D(_ClipTex);
            TEXTURE3D(_CorrectionLut);
            SamplerState pd_point_clamp_sampler;
            SamplerState pd_linear_clamp_sampler;

            float4 _DstSize;
            float4x4 _DstToSrc;
            float4 _SrcSize;
            float4 _SrcUV;
            float4x4 _DstToClip;
            float4 _ClipSize;
            float4 _ClipUV;
            float _ClipOn;
            float _Opacity;
            float _Mode;
            float _CorrectionOn;
            float _CorrectionLutSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 SampleLayer(Texture2D tex, float4x4 dstToLocal, float4 size, float4 uvMap, float2 p)
            {
                float2 local = mul(dstToLocal, float4(p, 0.0, 1.0)).xy;
                if(local.x < 0.0 || local.y < 0.0 || local.x >= size.x || local.y >= size.y)
                    return float4(0, 0, 0, 0);
                return tex.SampleLevel(pd_point_clamp_sampler, uvMap.xy + local * uvMap.zw, 0);
            }

            float3 Correct(float3 rgb)
            {
                if(_CorrectionOn < 0.5)
                    return rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    rgb = LinearToSRGB(rgb);
                #endif
                float size = max(_CorrectionLutSize, 2.0);
                float3 uvw = saturate(rgb) * ((size - 1.0) / size) + 0.5 / size;
                rgb = _CorrectionLut.SampleLevel(pd_linear_clamp_sampler, uvw, 0).rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    rgb = SRGBToLinear(rgb);
                #endif
                return rgb;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * _DstSize.xy;
                float4 prev = _MainTex.SampleLevel(pd_point_clamp_sampler, input.uv, 0);

                float4 c = SampleLayer(_SrcTex, _DstToSrc, _SrcSize, _SrcUV, p);
                if(c.a <= 0.0)
                    return prev;

                c.rgb = Correct(c.rgb);
                float a = c.a * _Opacity;
                if(_ClipOn > 0.5)
                    a *= SampleLayer(_ClipTex, _DstToClip, _ClipSize, _ClipUV, p).a > (2.5 / 255.0) ? 1.0 : 0.0;
                if(a <= 0.0)
                    return prev;

                float3 s = c.rgb;
                int mode = (int)round(_Mode);
                if(mode != 0 && prev.a > 0.0)
                {
                    float3 mixed = PixelBlendLinear(mode, prev.rgb / prev.a, s);
                    s = (1.0 - prev.a) * s + prev.a * mixed;
                }

                return float4(s * a + prev.rgb * (1.0 - a), a + prev.a * (1.0 - a));
            }
            ENDHLSL
        }
    }
}
