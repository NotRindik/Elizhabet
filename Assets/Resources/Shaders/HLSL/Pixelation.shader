Shader "PostProcess/Pixelation"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            

            float4 _PixelParams;
            // x = pixelSize (в экранных пикселях)
            // y = aspect
            // z = screenWidth
            // w = screenHeight

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 PixelateUV(float2 uv)
            {
                float pixelSize = _PixelParams.x;
                float aspect = _PixelParams.y;

                float2 resolution = _PixelParams.zw;

                float2 pixelSizeXY = float2(pixelSize, pixelSize * aspect);

                // 🔥 переводим в screen пиксели
                float2 pixelCoord = uv * resolution;

                // 🔥 жёсткий snap
                pixelCoord = floor(pixelCoord / pixelSizeXY) * pixelSizeXY;

                // обратно в UV
                return pixelCoord / resolution;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 uv = PixelateUV(i.uv);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }

            ENDHLSL
        }
    }
}