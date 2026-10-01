using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // AlterUpdate_VolumeLight 0x1adc250; AlterUpdate_LightShafts 0x1ad3a20.
    public static class LiveVolumeLightTimeline
    {
        public struct VolumeUpdateInfo
        {
            public Vector3 SunPosition;
            public Color Color;
            public float Power, Komorebi, BlurRadius, ColorRate, ScreenColorPower, EffectColorPower;
            public bool Enable, BorderClear, BlurAlpha;
        }

        public struct ShaftsUpdateInfo
        {
            public bool Enable;
            public Vector4 Speed, Angle, Offset, Alpha, Alpha2, MaskAlpha;
            public float Scale;
        }

        public delegate bool BlinkColorResolver(LiveTimelineKeyVolumeLightData key, ref Color color);

        private static bool Active(ILiveTimelineKeyDataList keys, float frame, TimelinePlayerMode mode)
        {
            return keys != null && keys.Count > 0 && !keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) &&
                keys.EnablePlayModeTimeline(mode) && keys[0] != null && frame >= keys[0].frame;
        }

        public static bool TryEvaluateVolume(LiveTimelineKeyVolumeLightDataList keys, float frame,
            TimelinePlayerMode mode, BlinkColorResolver blinkColor, out VolumeUpdateInfo info)
        {
            info = default;
            if (!Active(keys, frame, mode)) return false;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyVolumeLightData;
            var b = next as LiveTimelineKeyVolumeLightData;
            if (a == null) return false;
            float t = b != null && b.IsInterpolateKey()
                ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;
            Color color = a.color1;
            if (a.IsSyncBlinkLight && blinkColor != null) blinkColor(a, ref color);
            // Native does NOT interpolate towards a next key that synchronizes blink color.
            info.Color = b.IsSyncBlinkLight ? color : Color.LerpUnclamped(color, b.color1, t);
            info.SunPosition = Vector3.LerpUnclamped(a.sunPosition, b.sunPosition, t);
            info.Power = Mathf.LerpUnclamped(a.power, b.power, t);
            info.BlurRadius = Mathf.LerpUnclamped(a.blurRadius, b.blurRadius, t);
            info.ScreenColorPower = Mathf.LerpUnclamped(a.ScreenColorPower, b.ScreenColorPower, t);
            info.EffectColorPower = Mathf.LerpUnclamped(a.EffectColorPower, b.EffectColorPower, t);
            info.Komorebi = a.komorebi;
            info.ColorRate = a.ColorRate;
            info.Enable = a.enable;
            info.BorderClear = a.isEnabledBorderClear;
            info.BlurAlpha = a.IsEnabledBlurAlpha;
            return true;
        }

        public static bool TryEvaluateShafts(LiveTimelineKeyLightShaftsDataList keys, float frame,
            TimelinePlayerMode mode, float timelineDeltaTimeRatio, out ShaftsUpdateInfo info)
        {
            info = default;
            if (!Active(keys, frame, mode)) return false;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyLightShaftsData;
            var b = next as LiveTimelineKeyLightShaftsData;
            if (a == null) return false;
            float t = b != null && b.IsInterpolateKey()
                ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;
            info.Enable = a.enabled;
            info.Speed = Vector4.LerpUnclamped(a.speed, b.speed, t) * timelineDeltaTimeRatio;
            info.Angle = Vector4.LerpUnclamped(a.angle, b.angle, t);
            info.Offset = Vector4.LerpUnclamped(a.offset, b.offset, t);
            info.Alpha = Vector4.LerpUnclamped(a.alpha, b.alpha, t);
            info.Alpha2 = Vector4.LerpUnclamped(a.alpha2, b.alpha2, t);
            info.MaskAlpha = Vector4.LerpUnclamped(a.maskAlpha, b.maskAlpha, t);
            info.Alpha.z = GetFlareAlpha(frame, a.frame, a.maskAnimTime, a.maskAlphaRange);
            // IsAdjustScale exists in serialized data but this native routine never reads it.
            info.Scale = Mathf.LerpUnclamped(a.scale, b.scale, t);
            return true;
        }

        // GetFlareAlpha 0x1ae13c0: period rounded to even at 60fps, triangular envelope.
        public static float GetFlareAlpha(float frame, float keyFrame, float animationTime, Vector2 range)
        {
            int period = (int)System.Math.Round(animationTime * 60f, MidpointRounding.ToEven);
            if (period <= 0) return range.x;
            float elapsed = frame - keyFrame;
            float phase = Mathf.Clamp(elapsed - Mathf.Floor(elapsed / period) * period, 0f, period);
            float half = period * 0.5f;
            float ratio = phase < half ? phase / half : 1f - (phase - half) / half;
            return range.x + (range.y - range.x) * ratio;
        }
    }
}
