using UnityEngine;

namespace Gallop.Live.Cutt
{
    /// <summary>
    /// Timeline adapter for the recovered ChromaticAberration effect.
    ///
    /// Native reference: LiveTimelineControl.AlterUpdate_ChromaticAberration (0x7ffffc84c980).
    /// It walks the authored group list, gates on the key-list Disable attribute and on the
    /// player mode, then lerps every scalar of the current/next key with the shared
    /// CalculateInterpolationValue ratio. The effect type is taken from the current key, never
    /// interpolated, because it selects the shader pass.
    /// </summary>
    public static class LiveChromaticAberrationTimeline
    {
        public static ChromaticAberrationUpdateInfo Evaluate(
            System.Collections.Generic.List<LiveTimelineChromaticAberrationData> groups,
            float frame, TimelinePlayerMode playMode)
        {
            var info = new ChromaticAberrationUpdateInfo();
            if (groups == null || groups.Count == 0) return info;

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null) continue;
                if (Evaluate(group.keys, frame, playMode, out var candidate)) return candidate;
            }
            return info;
        }

        public static bool Evaluate(LiveTimelineKeyChromaticAberrationDataList keys,
            float frame, TimelinePlayerMode playMode, out ChromaticAberrationUpdateInfo info)
        {
            info = new ChromaticAberrationUpdateInfo();
            if (keys == null || keys.Count == 0 ||
                keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(playMode) || frame < keys.thisList[0].frame)
                return false;

            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyChromaticAberrationData;
            if (a == null) return false;

            var b = next as LiveTimelineKeyChromaticAberrationData;
            float t = b != null ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;

            info.isEnable = a.isEnable;
            info.redOffset = Vector2.LerpUnclamped(a.redOffset, b.redOffset, t);
            info.greenOffset = Vector2.LerpUnclamped(a.greenOffset, b.greenOffset, t);
            info.blueOffset = Vector2.LerpUnclamped(a.blueOffset, b.blueOffset, t);
            info.power = Mathf.LerpUnclamped(a.power, b.power, t);
            info.clip = Mathf.LerpUnclamped(a.clip, b.clip, t);
            info.effectType = a.effectType;
            return true;
        }
    }
}
