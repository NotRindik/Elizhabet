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
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
 
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
 
            #include "UnityCG.cginc"

             sampler2D _MainTex;
             float4 _MainTex_ST;
             float4 _MainTex_TexelSize;
             float4 _Color;

             float _Outline;
             float4 _OutlineColor;

			struct v2f 
            {
                float4  pos : SV_POSITION;
                float2  uv : TEXCOORD0;
            };

            v2f vert (appdata_base IN)
            {
                v2f OUT;

                OUT.pos = UnityObjectToClipPos (IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.texcoord, _MainTex);

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
            	fixed4 col = tex2D(_MainTex, IN.uv) * _Color;

            	if ( _Outline > 0 && col.a == 0)
            	{
            		fixed4 pixelUp = tex2D( _MainTex, IN.uv + fixed2(0,_MainTex_TexelSize.y));
                	fixed4 pixelDown = tex2D( _MainTex, IN.uv - fixed2(0,_MainTex_TexelSize.y));
                	fixed4 pixelRight = tex2D( _MainTex, IN.uv + fixed2(_MainTex_TexelSize.x,0));
                	fixed4 pixelLeft = tex2D( _MainTex, IN.uv - fixed2(_MainTex_TexelSize.x,0));
                		 
                	if ( pixelUp.a != 0 || pixelDown.a != 0  || pixelRight.a != 0  || pixelLeft.a != 0)
                	{
                		col.rgba = _OutlineColor;
                	}
            	}

            	return col;
            }
            ENDCG
         }
		
	}
	FallBack "Diffuse"
}