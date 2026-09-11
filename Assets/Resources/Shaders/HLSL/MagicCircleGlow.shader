Shader "Custom/MagicCircleGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EmissionIntensity ("Emission Intensity", Float) = 1
        _RevealProgress ("Reveal Progress", Range(0,1)) = 1
        _RevealMode ("Reveal Mode (0 Up, 1 Down, 2 Radial Out, 3 Radial In)", Float) = 0
        _RevealSoftness ("Reveal Softness", Range(0.001,1)) = 0.05
        _RevealMinY ("Reveal Min Y", Float) = -1
        _RevealMaxY ("Reveal Max Y", Float) = 1
        _RevealMaxRadius ("Reveal Max Radius", Float) = 1
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionOS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float _EmissionIntensity;
            float _RevealProgress;
            float _RevealMode;
            float _RevealSoftness;
            float _RevealMinY;
            float _RevealMaxY;
            float _RevealMaxRadius;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float4 col = tex * IN.color;
                col.rgb *= _EmissionIntensity;

                float value;
                if (_RevealMode < 0.5)
                    value = (IN.positionOS.y - _RevealMinY) / max(_RevealMaxY - _RevealMinY, 0.0001);
                else if (_RevealMode < 1.5)
                    value = (_RevealMaxY - IN.positionOS.y) / max(_RevealMaxY - _RevealMinY, 0.0001);
                else if (_RevealMode < 2.5)
                    value = length(IN.positionOS.xy) / max(_RevealMaxRadius, 0.0001);
                else
                    value = 1 - length(IN.positionOS.xy) / max(_RevealMaxRadius, 0.0001);

                float mask = 1 - smoothstep(_RevealProgress - _RevealSoftness, _RevealProgress + _RevealSoftness, value);
                col.a *= mask;

                return col;
            }
            ENDHLSL
        }
    }
}
