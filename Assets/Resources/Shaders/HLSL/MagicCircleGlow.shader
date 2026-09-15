Shader "Custom/MagicCircleGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EdgeTex ("Edge Texture", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 reveal : TEXCOORD1;
                float4 revealExtra : TEXCOORD2;
                float4 edgeParams : TEXCOORD3;
                float4 edgeColorAndNoise : TEXCOORD4;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionOS : TEXCOORD1;
                float4 reveal : TEXCOORD2;
                float4 revealExtra : TEXCOORD3;
                float4 edgeParams : TEXCOORD4;
                float4 edgeColorAndNoise : TEXCOORD5;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex); float4 _MainTex_ST;
            TEXTURE2D(_EdgeTex); SAMPLER(sampler_EdgeTex);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.reveal = IN.reveal;
                OUT.revealExtra = IN.revealExtra;
                OUT.edgeParams = IN.edgeParams;
                OUT.edgeColorAndNoise = IN.edgeColorAndNoise;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float revealProgress = IN.reveal.x;
                float revealMode = IN.reveal.y;
                float revealMinY = IN.reveal.z;
                float revealMaxY = IN.reveal.w;

                float revealSoftness = IN.revealExtra.x;
                float revealMaxRadius = IN.revealExtra.y;
                float emissionIntensity = IN.revealExtra.z;
                float edgeWidth = IN.revealExtra.w;

                float edgeIntensity = IN.edgeParams.x;
                float edgeDistortion = IN.edgeParams.y;
                float2 edgeScrollSpeed = IN.edgeParams.zw;

                float3 edgeColor = IN.edgeColorAndNoise.rgb;
                float edgeNoiseScale = IN.edgeColorAndNoise.a;

                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float4 col = tex * IN.color;
                col.rgb *= emissionIntensity;

                float value;
                if (revealMode < 0.5) value = (IN.positionOS.y - revealMinY) / max(revealMaxY - revealMinY, 0.0001);
                else if (revealMode < 1.5) value = (revealMaxY - IN.positionOS.y) / max(revealMaxY - revealMinY, 0.0001);
                else if (revealMode < 2.5) value = length(IN.positionOS.xy) / max(revealMaxRadius, 0.0001);
                else value = 1 - length(IN.positionOS.xy) / max(revealMaxRadius, 0.0001);

                float mainMask = 1 - smoothstep(revealProgress - revealSoftness, revealProgress + revealSoftness, value);

                float dist = abs(value - revealProgress);
                float edgeMask = 1 - smoothstep(0, max(edgeWidth, 0.0001), dist);

                float2 distortOffset = float2(sin(IN.uv.y * 20 + _Time.y * 3), cos(IN.uv.x * 20 + _Time.y * 3)) * edgeDistortion * 0.1;
                float2 edgeUV = IN.uv * edgeNoiseScale + _Time.y * edgeScrollSpeed + distortOffset;
                float4 edgeTex = SAMPLE_TEXTURE2D(_EdgeTex, sampler_EdgeTex, edgeUV);

                col.rgb = lerp(col.rgb, edgeColor * edgeTex.rgb * edgeIntensity, edgeMask * edgeTex.a);
                col.rgb += edgeColor * edgeMask * edgeTex.a * edgeIntensity;

                col.a = max(mainMask, edgeMask * edgeTex.a) * tex.a * IN.color.a;

                return col;
            }
            ENDHLSL
        }
    }
}