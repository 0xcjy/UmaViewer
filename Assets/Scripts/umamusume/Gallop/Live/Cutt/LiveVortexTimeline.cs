using UnityEngine;
using Gallop.RenderPipeline;

namespace Gallop.Live.Cutt
{
    /// <summary>
    /// Timeline adapter for the recovered Vortex effect.
    ///
    /// Native reference: VortexParam.Lerp (0x7ffffc989f60) and VortexParam.get_IsValidity
    /// (0x7ffff8b11560, which is just IsEnable). Area, RotVolume and DepthClip are lerped against
    /// the next key with the shared CalculateInterpolationValue ratio; IsEnable comes from the
    /// current key. The list is gated by the Disable attribute and by the player mode.
    /// </summary>
    public static class LiveVortexTimeline
    {
        public static VortexPass.Parameter Evaluate(LiveTimelineKeyVortexDataList keys,
            float frame, TimelinePlayerMode playMode)
        {
            var p = new VortexPass.Parameter();
            if (keys == null || keys.Count == 0 ||
                keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(playMode) || frame < keys.thisList[0].frame)
                return p;

            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyVortexData;
            if (a == null) return p;

            var b = next as LiveTimelineKeyVortexData;
            float t = b != null ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;

            p.IsEnable = a.IsEnable;
            p.Area = Vector4.LerpUnclamped(a.Area, b.Area, t);
            p.RotVolume = Mathf.LerpUnclamped(a.RotVolume, b.RotVolume, t);
            p.DepthClip = Mathf.LerpUnclamped(a.DepthClip, b.DepthClip, t);
            return p;
        }
    }
}
