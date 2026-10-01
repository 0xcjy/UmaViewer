using UnityEngine;
using Gallop.RenderPipeline;
namespace Gallop.Live.Cutt
{
    public static class LiveRadialBlurTimeline
    {
        public static RadialBlurPass.Parameter Evaluate(LiveTimelineKeyRadialBlurDataList keys,
            float frame, TimelinePlayerMode playMode)
        {
            var p = RadialBlurPass.Parameter.Default();
            if (keys == null || keys.Count == 0 || keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(playMode) || frame < keys.thisList[0].frame) return p;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyRadialBlurData;
            if (a == null) return p;
            var b = next as LiveTimelineKeyRadialBlurData;
            float t = b != null ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;
            p.Type = a.moveBlurType;
            p.Downsample = a.radialBlurDownsample;
            p.Iteration = a.radialBlurIteration;
            p.Offset = Vector2.LerpUnclamped(a.radialBlurOffset, b.radialBlurOffset, t);
            p.EllipseDir = Vector2.LerpUnclamped(a.radialBlurEllipseDir, b.radialBlurEllipseDir, t);
            p.StartArea = Mathf.LerpUnclamped(a.radialBlurStartArea, b.radialBlurStartArea, t);
            p.EndArea = Mathf.LerpUnclamped(a.radialBlurEndArea, b.radialBlurEndArea, t);
            p.Power = Mathf.LerpUnclamped(a.radialBlurPower, b.radialBlurPower, t);
            p.RollEulerAngles = Mathf.LerpUnclamped(a.radialBlurRollEulerAngles, b.radialBlurRollEulerAngles, t);
            p.DepthPowerFront = Mathf.LerpUnclamped(a.depthPowerFront, b.depthPowerFront, t);
            p.DepthPowerBack = Mathf.LerpUnclamped(a.depthPowerBack, b.depthPowerBack, t);
            p.DepthCancelRect = Vector4.LerpUnclamped(a.depthCancelRect, b.depthCancelRect, t);
            p.DepthCancelBlendLength = Mathf.LerpUnclamped(a.depthCancelBlendLength, b.depthCancelBlendLength, t);
            // Bit16 correlates with authored depth ranges in the shipped track.
            p.IsEnabledDepth = ((int)a.attribute & 0x10000) != 0;
            // Bits17/18 are NOT yet mapped: frame5989 has bit18 and a rectangle
            // without bit17. Guessing "enable/expand" would silently drop its mask.
            // The native renderer supports both controls, but timeline activation
            // remains conservative until the original key accessors are recovered.
            p.IsEnabledDepthCancelRect = false;
            p.IsExpandDepthCancelRect = false;
            return p;
        }
    }
}
