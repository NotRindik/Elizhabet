// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Sprite/Outline" 
{
	Properties 
	{
	  	_MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        _Outline ("Outline", Float) = 0
        _OutlineColor ("Outline Color", Color ) = (1,1,1,1)
	}

	SubShader 
    {
 
        Tags 
        { 
        	"Queue"="Transparent" 
        	"IgnoreProjector"="True" 
        	"RenderType" = "Transparent" 
        	"PreviewType"="Plane"
        	"CanUseSpriteAtlas"="True"
            "RenderPipeline" = "UniversalPipeline"
        }

		Cull Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha
		 
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
			float4 _Color;
			float1 _Outline;
			float4 _OutlineColor;
			float4 _MainTex_TexelSize;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        struct VertexInput
        {
	        float4 pos : POSITION;
        	float2 uv : TEXCOORD0;
        };
        
		struct VertexOutput
		{
		    float4 pos       : SV_POSITION;
		    float2 uv        : TEXCOORD0;
		    float2 lightingUV: TEXCOORD1;
		};
		ENDHLSL
        Pass
        {
	        Tags {
	        	"LightMode"="Universal2D"
	        }

        	HLSLPROGRAM
        	
        	#pragma vertex vert
            #pragma fragment frag

        	

            VertexOutput vert(VertexInput IN)
            {
                VertexOutput OUT;
                OUT.pos = TransformObjectToHClip(IN.pos.xyz);
                OUT.uv = IN.uv;
			
                OUT.lightingUV = IN.pos.xy * 0.5 + 0.5;
                return OUT;
            }

            half4 frag(VertexOutput IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;

                if (_Outline > 0 && col.a < 0.01)
                {
                    half4 up    = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(0,_MainTex_TexelSize.y));
                    half4 down  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(0,_MainTex_TexelSize.y));
                    half4 right = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv + float2(_MainTex_TexelSize.x,0));
                    half4 left  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv - float2(_MainTex_TexelSize.x,0));

                    if (up.a > 0.01 || down.a > 0.01 || right.a > 0.01 || left.a > 0.01)
                        col = _OutlineColor;
                }
                return col;
            }
        	
			ENDHLSL
        }
		
	}
}