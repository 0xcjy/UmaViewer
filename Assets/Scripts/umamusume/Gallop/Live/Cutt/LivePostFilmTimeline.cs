using UnityEngine;
using System;
using Overlay = Gallop.ImageEffect.ScreenOverlay.Overlay;

namespace Gallop.Live.Cutt
{
    // Color-track adapter for the recovered PostBlit shader, not a second post-process.
    public static class LivePostFilmTimeline
    {
        // Returns the current stage container's raw RGB; the key's own brightness/adjust flags
        // (not the container's returned color power) control PostFilm's conversion.
        public delegate bool BlinkColorResolver(string name, int nameHash, int containerIndex, out Color rawColor);
        private const int SyncColor0 = 0x200000;
        private const int SyncColor1 = 0x400000;
        private const int SyncColor2 = 0x800000;
        private const int SyncColor3 = 0x1000000;

        public static void Evaluate(ILiveTimelineKeyDataList keys, float frame,
            TimelinePlayerMode playMode, Overlay destination, BlinkColorResolver blinkColor = null)
        {
            Reset(destination);
            if (keys == null || keys.Count == 0 || keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(playMode) || frame < keys.At(0).frame) return;
            LiveTimelineControl.FindTimelineKey(out var currentBase, out var nextBase, keys, frame);
            var a = currentBase as LiveTimelineKeyPostFilmData;
            if (a == null || a.layerMode != LiveTimelineKeyPostFilmData.LayerMode.Color) return;
            // UV movies require a texture/time owner. Do not render them as a solid color.
            var b = nextBase as LiveTimelineKeyPostFilmData;
            float t = b != null ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;
            destination.postFilmMode = (Overlay.PostFilmMode)a.filmMode;
            destination.postFilmPower = Mathf.LerpUnclamped(a.filmPower, b.filmPower, t);
            destination.depthPower = Mathf.LerpUnclamped(a.depthPower, b.depthPower, t);
            destination.DepthClip = Mathf.LerpUnclamped(a.DepthClip, b.DepthClip, t);
            destination.postFilmOffsetParam = Vector2.LerpUnclamped(a.filmOffsetParam, b.filmOffsetParam, t);
            destination.postFilmOptionParam = Vector4.LerpUnclamped(a.filmOptionParam, b.filmOptionParam, t);
            // Native key getter RVA 0x1af62d0: bit20 selects the inverse pass, NOT depth disable.
            destination.inverseVignette = ((int)a.attribute & 0x100000) != 0;
            // SetupPostFilmUpdateDataInfo RVA 0x1ae70c0: current-key sync replaces RGB
            // on flagged corners, preserving each authored alpha. When the NEXT key's
            // corner is synced, the native path holds the current corner instead of
            // interpolating toward the next key's authored color.
            Color aColor0 = a.color0, aColor1 = a.color1, aColor2 = a.color2, aColor3 = a.color3;
            int flags = (int)a.attribute;
            Color raw;
            if (blinkColor != null && (flags & (SyncColor0 | SyncColor1 | SyncColor2 | SyncColor3)) != 0 &&
                blinkColor(a.BlinkLightName, a.BlinkLightNameHash, a.BlinkLightContainerIndex, out raw))
            {
                raw = AdjustBlinkRgb(raw, a.BlinkLightBrightnessPower, a.IsAdjustedBlinkLightColor);
                if ((flags & SyncColor0) != 0) aColor0 = WithRgb(a.color0, raw);
                if ((flags & SyncColor1) != 0) aColor1 = WithRgb(a.color1, raw);
                if ((flags & SyncColor2) != 0) aColor2 = WithRgb(a.color2, raw);
                if ((flags & SyncColor3) != 0) aColor3 = WithRgb(a.color3, raw);
            }
            int nextFlags = (int)b.attribute;
            var c0 = (nextFlags & SyncColor0) != 0 ? aColor0 : Color.LerpUnclamped(aColor0, b.color0, t);
            var c1 = (nextFlags & SyncColor1) != 0 ? aColor1 : Color.LerpUnclamped(aColor1, b.color1, t);
            var c2 = (nextFlags & SyncColor2) != 0 ? aColor2 : Color.LerpUnclamped(aColor2, b.color2, t);
            var c3 = (nextFlags & SyncColor3) != 0 ? aColor3 : Color.LerpUnclamped(aColor3, b.color3, t);
            // Native SetupPostFilmUpdateDataInfo RVA 0x1ae70c0, branches 0x1ae78d7..0x1ae7a21.
            destination.postFilmColor0 = c0;
            destination.postFilmColor1 = a.colorType == PostColorType.ColorAll || a.colorType == PostColorType.Color2TopBottom ? c0 : c1;
            destination.postFilmColor2 = a.colorType == PostColorType.ColorAll || a.colorType == PostColorType.Color2LeftRight ? c0 : a.colorType == PostColorType.Color2TopBottom ? c1 : c2;
            destination.postFilmColor3 = a.colorType == PostColorType.ColorAll ? c0 : a.colorType == PostColorType.Color4 ? c3 : c1;
            destination.colorBlendFactor = Mathf.LerpUnclamped(a.colorBlendFactor, b.colorBlendFactor, t);
            // Native setup forwards RollAngle without a degrees conversion.
            destination.SetRollAngle(Mathf.LerpUnclamped(a.RollAngle, b.RollAngle, t));
            var scale = Vector2.LerpUnclamped(a.FilmScale, b.FilmScale, t);
            destination.SetScale(new Vector2(SafeScale(scale.x), SafeScale(scale.y)));
        }

        private static Color WithRgb(Color authored, Color raw) => new Color(raw.r, raw.g, raw.b, authored.a);

        internal static Color AdjustBlinkRgb(Color raw, float brightness, bool adjusted)
        {
            if (!Gallop.Math.IsFloatEqualLight(brightness, 1f))
            {
                Color.RGBToHSV(raw, out float h, out float s, out float v);
                raw = Color.HSVToRGB(h, s, v * brightness, true);
            }
            if (adjusted) { raw.r += 1f; raw.g += 1f; raw.b += 1f; }
            return raw;
        }

        private static float SafeScale(float value) => float.IsNaN(value) || float.IsInfinity(value) || Mathf.Abs(value) < 0.0001f ? 1f : value;

        public static void Reset(Overlay value)
        {
            value.postFilmMode = Overlay.PostFilmMode.None;
            value.postFilmPower = value.depthPower = 0f;
            value.DepthClip = 2f;
            value.postFilmOffsetParam = Vector2.zero;
            value.postFilmOptionParam = Vector4.zero;
            value.postFilmColor0 = value.postFilmColor1 = value.postFilmColor2 = value.postFilmColor3 = Color.black;
            value.inverseVignette = false;
            value.layerMode = Overlay.LayerMode.Color;
            value.colorBlend = Overlay.ColorBlend.None;
            value.movieResId = 0;
            value.colorBlendFactor = 0f;
            value.IsEnableDepth = true;
            value.RollParameter = new Vector4(0f, 1f, 0f, 0f);
            value.ScaleParameter = Vector4.one;
            value.SetMovieInfo(null, null, Vector2.one, Vector2.zero);
        }

        public static void Copy(Overlay source, Overlay target)
        {
            target.postFilmMode = source.postFilmMode;
            target.postFilmPower = source.postFilmPower;
            target.depthPower = source.depthPower;
            target.DepthClip = source.DepthClip;
            target.postFilmOffsetParam = source.postFilmOffsetParam;
            target.postFilmOptionParam = source.postFilmOptionParam;
            target.postFilmColor0 = source.postFilmColor0;
            target.postFilmColor1 = source.postFilmColor1;
            target.postFilmColor2 = source.postFilmColor2;
            target.postFilmColor3 = source.postFilmColor3;
            target.inverseVignette = source.inverseVignette;
            target.layerMode = source.layerMode;
            target.colorBlend = source.colorBlend;
            target.movieResId = source.movieResId;
            target.colorBlendFactor = source.colorBlendFactor;
            target.IsEnableDepth = source.IsEnableDepth;
            target.RollParameter = source.RollParameter;
            target.ScaleParameter = source.ScaleParameter;
            target.SetMovieInfo(source.MovieTexture, source.MovieMaskTexture, source.MovieTextureScale, source.MovieTextureOffset);
        }
    }
}
