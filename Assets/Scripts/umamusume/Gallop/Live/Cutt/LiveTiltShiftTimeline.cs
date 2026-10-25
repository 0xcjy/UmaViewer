using UnityEngine;
using Gallop.RenderPipeline;

namespace Gallop.Live.Cutt
{
    /// <summary>
    /// Timeline adapter for the recovered TiltShift effect.
    ///
    /// Native reference: LiveTimelineControl.SetupTiltShiftUpdateInfo (0x7ffffc85c9f0), called by
    /// AlterUpdate_TiltShift (0x7ffffc85cb60).
    ///  - mode/quality/downsample are taken from the current key and never interpolated.
    ///  - blurArea, maxBlurSize, offset and roll are lerped against the next key with the shared
    ///    CalculateInterpolationValue ratio; with no next key the current key's values are used.
    /// The list is gated by the Disable attribute and by the player mode before anything is read.
    /// </summary>
    public static class LiveTiltShiftTimeline
    {
        public static TiltShiftPass.Parameter Evaluate(LiveTimelineKeyTiltShiftDataList keys,
            float frame, TimelinePlayerMode playMode)
        {
            var p = new TiltShiftPass.Parameter();
            if (keys == null || keys.Count == 0 ||
                keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(playMode) || frame < keys.thisList[0].frame)
                return p;

            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyTiltShiftData;
            if (a == null) return p;

            var b = next as LiveTimelineKeyTiltShiftData;

            p.Mode = a.mode;
            p.Quality = a.quality;
            p.Downsample = a.downsample;

            if (b == null)
            {
                p.BlurArea = a.blurArea;
                p.MaxBlurSize = a.maxBlurSize;
                p.Offset = a.offset;
                p.Roll = a.roll;
                return p;
            }

            float t = LiveTimelineControl.CalculateInterpolationValue(a, b, frame);
            p.BlurArea = Mathf.LerpUnclamped(a.blurArea, b.blurArea, t);
            p.MaxBlurSize = Mathf.LerpUnclamped(a.maxBlurSize, b.maxBlurSize, t);
            p.Offset = Vector2.LerpUnclamped(a.offset, b.offset, t);
            p.Roll = Mathf.LerpUnclamped(a.roll, b.roll, t);
            return p;
        }
    }
}
