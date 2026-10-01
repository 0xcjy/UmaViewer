using UnityEngine;

namespace Gallop.Live.Cutt
{
    // 子系统 A7b：Exposure/ToneCurve 轨道求值器。
    //
    // 原生对照（Gallop_Live_disasm.txt）：
    //  - AlterUpdate_Exposure  RVA=0x1acfde0（调用点 0x1ac9e0f，sheet+0x1A8 键表）
    //  - AlterUpdate_ToneCurve RVA=0x1adace0（调用点 0x1ac9e24，sheet+0x1B0 键表）
    //  两者结构一致：键表空/Disable/播放模式不匹配时直接返回默认值；
    //  用 CalculateInterpolationValue(0x1ade000) 求 t 后逐字段 Lerp
    //  （Exposure 0x1ad012b-0x1ad01ac：DepthMask/Gain/Lift/MaskGain/MaskLift 五个
    //   float；ToneCurve 0x1adb07b-0x1adb226：四组 4-float 曲线采样 + DepthMask），
    //  最后把 updateInfo 结构体以 in 引用交给 OnUpdateExposure/OnUpdateToneCurve
    //  委托（0x1ad01c9-0x1ad01e1：rax+28h/rax+40h 事件字段，call [rax+18h]）。
    public static class LiveExposureToneCurveTimeline
    {
        public struct ExposureUpdateInfo
        {
            public bool IsEnable;
            public float DepthMask;
            public float Gain;
            public float Lift;
            public float MaskGain;
            public float MaskLift;
        }

        public struct ToneCurveUpdateInfo
        {
            public bool IsEnable;
            public float DepthMask;
            // ToneAnimationCurve 原生以引用随 updateInfo 传递（0x1adb013 拷贝
            // [rbx+38h]），插值只作用于分级控制点；这里按原生语义整条传递。
            public AnimationCurve ToneCurve;
            public Color MinCorrectionLevel;
            public Color MaxCorrectionLevel;
            public AnimationCurve MaskToneCurve;
            public Color MaskMinCorrectionLevel;
            public Color MaskMaxCorrectionLevel;
        }

        public static ExposureUpdateInfo EvaluateExposure(
            LiveTimelineKeyExposureDataList keys, float frame, TimelinePlayerMode playMode)
        {
            var info = default(ExposureUpdateInfo);
            if (!IsTrackActive(keys, frame, playMode))
                return info;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyExposureData;
            if (a == null)
                return info;
            var b = next as LiveTimelineKeyExposureData;
            float t = b != null
                ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame)
                : 0f;
            b = b ?? a;
            info.IsEnable = a.IsEnable;
            info.DepthMask = Mathf.LerpUnclamped(a.DepthMask, b.DepthMask, t);
            info.Gain = Mathf.LerpUnclamped(a.Gain, b.Gain, t);
            info.Lift = Mathf.LerpUnclamped(a.Lift, b.Lift, t);
            info.MaskGain = Mathf.LerpUnclamped(a.MaskGain, b.MaskGain, t);
            info.MaskLift = Mathf.LerpUnclamped(a.MaskLift, b.MaskLift, t);
            return info;
        }

        public static ToneCurveUpdateInfo EvaluateToneCurve(
            LiveTimelineKeyToneCurveDataList keys, float frame, TimelinePlayerMode playMode)
        {
            var info = default(ToneCurveUpdateInfo);
            if (!IsTrackActive(keys, frame, playMode))
                return info;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeyToneCurveData;
            if (a == null)
                return info;
            var b = next as LiveTimelineKeyToneCurveData;
            float t = b != null
                ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame)
                : 0f;
            b = b ?? a;
            info.IsEnable = a.IsEnable;
            info.DepthMask = Mathf.LerpUnclamped(a.DepthMask, b.DepthMask, t);
            // 曲线对象不插值：原生 updateInfo 只携带当前 key 的曲线引用
            // （0x1adb013/0x1adb020 两次引用拷贝），形态变化由相邻 key 的
            // 分量 Lerp 体现。
            info.ToneCurve = a.ToneAnimationCurve;
            info.MinCorrectionLevel = Color.LerpUnclamped(a.MinCorrectionLevel, b.MinCorrectionLevel, t);
            info.MaxCorrectionLevel = Color.LerpUnclamped(a.MaxCorrectionLevel, b.MaxCorrectionLevel, t);
            info.MaskToneCurve = a.MaskToneCurve;
            info.MaskMinCorrectionLevel = Color.LerpUnclamped(a.MaskMinCorrectionLevel, b.MaskMinCorrectionLevel, t);
            info.MaskMaxCorrectionLevel = Color.LerpUnclamped(a.MaskMaxCorrectionLevel, b.MaskMaxCorrectionLevel, t);
            return info;
        }

        // 与 LiveRadialBlurTimeline.Evaluate 的门条件一致：
        // 空表 / Disable / 播放模式不匹配 / 早于首帧 → 默认值。
        private static bool IsTrackActive(ILiveTimelineKeyDataList keys, float frame, TimelinePlayerMode playMode)
        {
            return keys != null && keys.Count > 0 &&
                   !keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) &&
                   keys.EnablePlayModeTimeline(playMode) &&
                   keys[0] != null && frame >= keys[0].frame;
        }
    }
}
