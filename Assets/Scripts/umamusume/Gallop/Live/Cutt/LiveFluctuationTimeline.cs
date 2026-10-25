using UnityEngine;
using Gallop.RenderPipeline;

namespace Gallop.Live.Cutt
{
    /// <summary>
    /// Timeline adapter for the recovered Fluctuation effect.
    ///
    /// Native reference: LiveTimelineControl.SetupFluctuationUpdateInfo / AlterUpdate_Fluctuation
    /// (track 71). MoveDirection, MovePower, Power and DepthClip are lerped against the next key;
    /// IsEnable and the DisableGraphicSettings attribute come from the current key.
    /// FluctuationParam.IsValidity is `IsEnable &amp;&amp; (DisableGraphicSettings || GraphicSettings.IsFluctuation)`.
    /// </summary>
    public static class LiveFluctuationTimeline
    {
        public static FluctuationPass.Parameter Evaluate(LiveTimelineKeyFluctuationDataList keys,
            float frame, TimelinePlayerMode playMode)
        {
            var p = new FluctuationPass.Parameter();
            if (keys == null || keys.Count == 0 ||
                keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(playMode) || frame < keys.thisList[0].frame)
                return p;

            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyFluctuationData;
            if (a == null) return p;

            var b = next as LiveTimelineKeyFluctuationData;
            float t = b != null ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;

            p.IsEnable = a.IsEnable;
            p.DisableGraphicSettings = a.DisableGraphicSettings;
            p.MoveDirection = Vector2.LerpUnclamped(a.MoveDirection, b.MoveDirection, t);
            p.MovePower = Mathf.LerpUnclamped(a.MovePower, b.MovePower, t);
            p.Power = Mathf.LerpUnclamped(a.Power, b.Power, t);
            p.DepthClip = Mathf.LerpUnclamped(a.DepthClip, b.DepthClip, t);
            return p;
        }
    }
}
