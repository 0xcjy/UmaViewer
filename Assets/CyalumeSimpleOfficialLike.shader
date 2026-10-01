Shader "Custom/CyalumeSimpleOfficialLike"
{
    Properties
    {
        _MainTex ("Flipbook", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Tint;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                // ���� discard������ alpha clip
                fixed3 rgb = tex.rgb * _Tint.rgb * _Intensity;

                // additive �� alpha ��������Ҫ���� 1 ����
                return fixed4(rgb, 1.0);
            }
            ENDCG
        }
    }
}