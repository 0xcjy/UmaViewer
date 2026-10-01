using UnityEngine;

namespace Gallop.Live.Cutt
{
    // AlterUpdate_LensFlare 0x1ad34f0: floats interpolate; switches remain on the current key.
    public static class LiveLensFlareTimeline
    {
        public static bool TryEvaluate(LiveTimelineLensFlareData group, float frame,
            TimelinePlayerMode mode, out LensFlareUpdateInfo info)
        {
            info = default;
            var keys = group != null ? group.keys : null;
            if (keys == null || keys.Count == 0 || keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(mode) || keys[0] == null || frame < keys[0].frame)
                return false;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyLensFlareData;
            var b = next as LiveTimelineKeyLensFlareData;
            if (a == null) return false;
            float t = b != null && b.IsInterpolateKey()
                ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;
            info.TimelineNameHash = group.nameHash;
            info.offset = Vector3.LerpUnclamped(a.offset, b.offset, t);
            info.color = Color.LerpUnclamped(a.color, b.color, t);
            info.brightness = Mathf.LerpUnclamped(a.brightness, b.brightness, t);
            info.fadeSpeed = Mathf.LerpUnclamped(a.fadeSpeed, b.fadeSpeed, t);
            info.enable = a.enableFlare;
            info.enableParameter = a.enableParameter;
            info.IsAutoBrightness = a.IsAutoBrightness;
            info.IsOverridePosition = a.IsOverridePosition;
            info.IsScreenFit = a.IsScreenFit;
            return true;
        }
    }
}
