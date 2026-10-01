using UnityEngine;

namespace Gallop.Live.Cutt
{
    // AlterLateUpdate_LightProjection 0x1ac8960 selects keys on integer frames.
    public static class LiveLightProjectionTimeline
    {
        public struct UpdateInfo
        {
            public LiveTimelineLightProjectionData group;
            // Null current means clear this group's projection, including reverse seek before its first key.
            public LiveTimelineKeyLightProjectionData current;
            public LiveTimelineKeyLightProjectionData next;
            public float progress;
            // Native ProgressTime: elapsed seconds since the current key, not absolute live time.
            public float time;
            // Native blink synchronization is applied before color interpolation, preserving alpha.
            public Color color;
        }

        public static bool TryEvaluate(LiveTimelineLightProjectionData group, float frame,
            TimelinePlayerMode mode, out UpdateInfo info,
            LivePostFilmTimeline.BlinkColorResolver blinkColor = null)
        {
            info = default;
            info.group = group;
            var keys = group != null ? group.keys : null;
            int nativeFrame = Mathf.FloorToInt(frame);
            if (keys == null || keys.Count == 0 || keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(mode) || keys[0] == null || nativeFrame < keys[0].frame)
                return false;

            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, nativeFrame);
            var a = current as LiveTimelineKeyLightProjectionData;
            var b = next as LiveTimelineKeyLightProjectionData;
            if (a == null) return false;
            info.current = a;
            info.next = b;
            info.progress = b != null && b.IsInterpolateKey()
                ? LiveTimelineControl.CalculateInterpolationValue(a, b, nativeFrame) : 0f;
            info.time = (nativeFrame - a.frame) / 60f;
            var color = a.Color;
            if (a.IsSyncBlinkLight && blinkColor != null &&
                blinkColor(a.BlinkLightName, a.BlinkLightNameHash, a.BlinkLightContainerIndex, out var raw))
            {
                raw = LivePostFilmTimeline.AdjustBlinkRgb(raw, a.BlinkLightBrightnessPower, a.IsAdjustedBlinkLightColor);
                color.r = raw.r;
                color.g = raw.g;
                color.b = raw.b;
            }
            // 0x1ac9006: a synchronized NEXT key suppresses color interpolation entirely.
            info.color = b != null && b.IsInterpolateKey() && !b.IsSyncBlinkLight
                ? Color.LerpUnclamped(color, b.Color, info.progress) : color;
            return true;
        }
    }
}
