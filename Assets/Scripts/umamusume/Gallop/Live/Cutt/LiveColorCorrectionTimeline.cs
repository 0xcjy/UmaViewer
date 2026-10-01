using System.Collections.Generic;
using Gallop.RenderPipeline;

namespace Gallop.Live.Cutt
{
    /// <summary>Single main-camera, simple/nonselective authored color track.</summary>
    public static class LiveColorCorrectionTimeline
    {
        public static SimpleColorCorrectionPass.Parameter Evaluate(List<LiveTimelineColorCorrectionData> groups,
            float frame, TimelinePlayerMode playMode)
        {
            // Multiple groups/masks have not been recovered. Do not silently choose one.
            if (groups == null || groups.Count != 1 || float.IsNaN(frame) || float.IsInfinity(frame)) return default;
            var keys = groups[0]?.keys;
            if (keys == null || keys.Count == 0 || keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(playMode) || frame < keys.thisList[0].frame) return default;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var key = current as LiveTimelineKeyColorCorrectionData;
            // Actual Live1176 keys have attribute0, mode0, selective0 and no interpolation fields.
            // Do not infer masks, depth correction, or a cross-key blend from blendCurve.
            if (key == null || key.enable == 0 || key.mode != 0 || key.selective != 0 || (int)key.attribute != 0)
                return default;
            return new SimpleColorCorrectionPass.Parameter {
                Enabled = true, Red = key.redCurve, Green = key.greenCurve, Blue = key.blueCurve,
                Saturation = key.saturation
            };
        }
    }
}
