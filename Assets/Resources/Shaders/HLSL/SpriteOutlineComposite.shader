Shader "Hidden/SpriteOutlineComposite"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SpriteOutlineComposite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _OutlineColor;
            float _Thickness; // в пикселях экрана
            
            TEXTURE2D(_OccluderTex);
            float _UseOcclusion;        

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).a;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                float2 px = (_ScreenParams.zw - 1.0) * _Thickness; // 1/width, 1/height в пикселях

                half center = SampleAlpha(uv);

                half n = 0;
                n = max(n, SampleAlpha(uv + float2( px.x,  0)));
                n = max(n, SampleAlpha(uv + float2(-px.x,  0)));
                n = max(n, SampleAlpha(uv + float2( 0,  px.y)));
                n = max(n, SampleAlpha(uv + float2( 0, -px.y)));
                n = max(n, SampleAlpha(uv + float2( px.x,  px.y)));
                n = max(n, SampleAlpha(uv + float2(-px.x,  px.y)));
                n = max(n, SampleAlpha(uv + float2( px.x, -px.y)));
                n = max(n, SampleAlpha(uv + float2(-px.x, -px.y)));

                half outline = saturate(n - center);

                if (_UseOcclusion > 0.5)
                {
                    half occ = SAMPLE_TEXTURE2D(_OccluderTex, sampler_PointClamp, uv).a;
                    outline *= (1.0 - occ);
                }

                return half4(_OutlineColor.rgb, _OutlineColor.a * outline);
            }
            ENDHLSL
        }
    }
}
