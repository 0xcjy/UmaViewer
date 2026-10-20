Shader "Hidden/UmaViewer/LiveDownscale"
{
    //Reducción de caja para el supermuestreo del grabador del live.
    //
    //POR QUÉ
    //-------
    //El grabador renderiza el frame a un RenderTexture más grande que la salida (supermuestreo) y hay
    //que reducirlo. Antes lo hacía FFmpeg: el frame ENTERO (p. ej. 132 MB a 4K con 200 %) viajaba a la
    //CPU y luego se reescalaba allí. Aquí se reduce en la GPU, así que a la CPU solo va el frame del
    //tamaño de salida (33 MB a 4K): 4x menos transferencia, y FFmpeg ya no reescala.
    //
    //CÓMO
    //----
    //Promedia el bloque de texels de origen que cae dentro de cada píxel de destino. Con factor 2x son
    //exactamente 4 muestras (2x2), que es la definición de un SSAA 200 %.
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DstSize ("Destination Size", Vector) = (1920, 1080, 0, 0)
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;   // xy = 1/tamaño de origen, zw = tamaño de origen
            float4 _DstSize;             // tamaño de destino (salida del vídeo)

            fixed4 frag (v2f_img i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float2 ratio = _MainTex_TexelSize.zw / max(_DstSize.xy, float2(1, 1));

                //Semiancho del bloque de origen que cubre el píxel de destino (en UV). Con 2x queda en
                //medio texel, que es justo el centro de los 4 texels del bloque.
                float2 o = 0.5 * (ratio - 1.0) * texel;

                fixed4 color =
                      tex2D(_MainTex, i.uv + float2(-o.x, -o.y))
                    + tex2D(_MainTex, i.uv + float2( o.x, -o.y))
                    + tex2D(_MainTex, i.uv + float2(-o.x,  o.y))
                    + tex2D(_MainTex, i.uv + float2( o.x,  o.y));

                return color * 0.25;
            }
            ENDCG
        }
    }

    Fallback Off
}
