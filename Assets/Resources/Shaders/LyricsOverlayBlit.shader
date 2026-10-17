// (Grabación del live) Blit de superposición con mezcla alfa, para quemar la letra en el
// RenderTexture de captura. La letra se renderiza aparte (en su propio RenderTexture, con fondo
// transparente) y luego se compone sobre el frame del show con este material.
//
// Por qué no se dibuja la letra con una cámara sobre el propio RenderTexture de captura: en URP,
// una cámara que renderiza a una RenderTexture recibe un "attachment" intermedio de color y lo
// vuelca al final sobre el target. Ese volcado reemplaza el contenido del target, así que se
// perdería lo que hubiera debajo (en este caso, la composición multicámara del show). Haciendo el
// volcado nosotros con mezcla alfa, el frame del show se conserva y la letra se superpone.
Shader "UmaViewer/LyricsOverlayBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            fixed4 frag(v2f_img i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
    }

    Fallback Off
}
