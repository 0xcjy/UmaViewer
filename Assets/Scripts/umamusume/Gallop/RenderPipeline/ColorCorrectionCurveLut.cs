using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Native ColorCorrectionPass LUT contract (256x4, linear ARGB32, no mips).
    /// Used by the recovered simple color pass. No generic Volume/LUT replacement.
    /// </summary>
    public sealed class ColorCorrectionCurveLut : IDisposable
    {
        public const int Width = 256;
        public const int Height = 4;
        readonly Color32[] _pixels = new Color32[Width * Height];
        public Texture2D Texture { get; private set; }

        public static byte Quantize(float value)
        {
            // Native FloatToByte uses clamp * 255 followed by cvttss2si, not round.
            if (float.IsNaN(value)) return 0;
            return (byte)(Mathf.Clamp01(value) * 255f);
        }

        static float Evaluate(AnimationCurve curve, float time)
        {
            // A missing curve is a defensive identity; shipped native code requires it.
            return curve == null || curve.length == 0 ? time : Mathf.Clamp01(curve.Evaluate(time));
        }

        public static void Fill(Color32[] pixels, AnimationCurve red, AnimationCurve green, AnimationCurve blue,
            AnimationCurve nextRed = null, AnimationCurve nextGreen = null, AnimationCurve nextBlue = null, float blend = 0f)
        {
            if (pixels == null || pixels.Length != Width * Height)
                throw new ArgumentException("Color correction LUT requires exactly 1024 pixels", nameof(pixels));
            blend = float.IsNaN(blend) ? 0f : Mathf.Clamp01(blend);
            for (int x = 0; x < Width; ++x)
            {
                float time = x / 255f;
                float r = Evaluate(red, time), g = Evaluate(green, time), b = Evaluate(blue, time);
                if (blend > 0f)
                {
                    r = Mathf.LerpUnclamped(r, Evaluate(nextRed, time), blend);
                    g = Mathf.LerpUnclamped(g, Evaluate(nextGreen, time), blend);
                    b = Mathf.LerpUnclamped(b, Evaluate(nextBlue, time), blend);
                }
                byte qr = Quantize(r), qg = Quantize(g), qb = Quantize(b);
                pixels[x] = new Color32(qr, qr, qr, 255);
                pixels[x + Width] = new Color32(qg, qg, qg, 255);
                pixels[x + Width * 2] = new Color32(qb, qb, qb, 255);
                // Unused row; keep deterministic zero initialization as native array.
                pixels[x + Width * 3] = default;
            }
        }

        public void Update(AnimationCurve red, AnimationCurve green, AnimationCurve blue,
            AnimationCurve nextRed = null, AnimationCurve nextGreen = null, AnimationCurve nextBlue = null, float blend = 0f)
        {
            if (Texture == null)
                Texture = new Texture2D(Width, Height, TextureFormat.ARGB32, false, true)
                {
                    name = "Recovered color correction curves",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    // This is a data lookup, not a surface texture. With the project's
                    // ForceEnable quality setting, default aniso=1 makes LUT lookup
                    // depend on neighboring scene colors (verified by spatial GPU test).
                    anisoLevel = 0
                };
            Fill(_pixels, red, green, blue, nextRed, nextGreen, nextBlue, blend);
            Texture.SetPixels32(_pixels);
            Texture.Apply(false, false);
        }

        public void Dispose()
        {
            CoreUtils.Destroy(Texture);
            Texture = null;
        }
    }
}
