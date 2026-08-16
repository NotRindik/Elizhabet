Shader "Hidden/PixelPerfectSubpixel"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _SubpixelOffset ("Subpixel Offset (UV space)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float2 _SubpixelOffset;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;

                float4 pos = v.vertex;
                // сдвигаем саму геометрию квада (как VERTEX += cam_offset в Godot)
                pos.xy += _SubpixelOffset * 2.0; // *2, т.к. clip space -1..1, а offset у нас в UV-масштабе (0..1)

                o.vertex = UnityObjectToClipPos(pos);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }
}