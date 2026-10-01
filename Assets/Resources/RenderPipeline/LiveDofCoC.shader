Shader "Hidden/UmaViewer/LiveDofCoC"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _MainTex_TexelSize;
            float4 _CurveParams;
            float4 _GlobalScreenUVScrollParam;
            float _GallopDofFocalStart;
            float _dofForegroundSize;
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float2 depthUV : TEXCOORD1; };
            Varyings Vert(appdata_img input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.depthUV = input.texcoord;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) output.depthUV.y = 1 - output.depthUV.y;
                #endif
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float2 depthUV = frac(input.depthUV + _GlobalScreenUVScrollParam.xy);
                float depth = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, depthUV));
                float background = saturate((depth - _CurveParams.z) * _CurveParams.y);
                // Separate near edge, rather than the bundled shader's far edge.
                float foreground = saturate((_GallopDofFocalStart - depth) * _CurveParams.x);
                float coc = saturate(max(background, foreground * _dofForegroundSize));
                return float4(tex2D(_MainTex, input.uv).rgb, coc);
            }
            ENDHLSL
        }
    }
}
