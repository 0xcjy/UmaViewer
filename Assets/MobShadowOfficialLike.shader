Shader "Gallop/3D/Live/Cyalume/MobShadowOfficialLike" 
{
    Properties
    {
        _MainTex ("Mob Shadow Texture", 2D) = "white" {}
        _ClipThreshold ("Clip Threshold", Range(0,1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="AlphaTest"
            "RenderType"="Opaque"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _ClipThreshold;
            CBUFFER_END

            float4 _MobColor;
            float4 _MaskPosArray[11];
            float4x4 _CyalumeGroupMatrix[11];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.worldPos = posInputs.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float ComputeMaskAttenuation(int idx, float3 worldPos)
            {
                float4 m = _MaskPosArray[idx];
                if (m.z <= 0.0001)
                    return 1.0;

                float2 d = worldPos.xz - m.xy;
                float dist = length(d);

                float radius = m.z;
                float softness = max(m.w, 0.0001);

                return 1.0 - smoothstep(radius - softness, radius, dist);
            }

            float3 WorldToGroupLocal(float4x4 m, float3 worldPos)
            {
                float3 translation = float3(m[0][3], m[1][3], m[2][3]);

                float3 axisX = float3(m[0][0], m[1][0], m[2][0]);
                float3 axisY = float3(m[0][1], m[1][1], m[2][1]);
                float3 axisZ = float3(m[0][2], m[1][2], m[2][2]);

                float3 d = worldPos - translation;

                float sx2 = max(dot(axisX, axisX), 1e-8);
                float sy2 = max(dot(axisY, axisY), 1e-8);
                float sz2 = max(dot(axisZ, axisZ), 1e-8);

                return float3(
                    dot(d, axisX) / sx2,
                    dot(d, axisY) / sy2,
                    dot(d, axisZ) / sz2
                );
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float bestAlpha = 0.0;
                float3 bestRgb = 1.0.xxx;

                [unroll]
                for (int i = 0; i < 11; i++)
                {
                    float4x4 groupMtx = _CyalumeGroupMatrix[i];
                    float3 localPos = WorldToGroupLocal(groupMtx, IN.worldPos);

                    float2 projUV = localPos.xz + 0.5;

                    bool inside =
                        projUV.x >= 0.0 && projUV.x <= 1.0 &&
                        projUV.y >= 0.0 && projUV.y <= 1.0;

                    if (!inside)
                        continue;

                    float4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, projUV);

                    float maskAtten = ComputeMaskAttenuation(i, IN.worldPos);
                    float a = texCol.a * maskAtten;

                    if (a > bestAlpha)
                    {
                        bestAlpha = a;
                        bestRgb = texCol.rgb;
                    }
                }

                float4 finalColor;
                finalColor.rgb = bestRgb * _MobColor.rgb;
                finalColor.a = bestAlpha * _MobColor.a;

                clip(finalColor.a - _ClipThreshold);
                return finalColor;
            }
            ENDHLSL
        }
    }
}