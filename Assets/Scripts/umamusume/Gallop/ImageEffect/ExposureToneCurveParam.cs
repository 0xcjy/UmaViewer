using System;
using UnityEngine;

namespace Gallop.ImageEffect
{
    [Serializable]
    public class ExposureParam
    {
        public bool IsEnable;
        public float DepthMask, Gain, Lift, MaskGain, MaskLift;

        public void Setup(ExposureParam src)
        {
            if (src != null) Set(src.IsEnable, src.DepthMask, src.Gain, src.Lift, src.MaskGain, src.MaskLift);
        }

        public void Set(bool enabled, float depthMask, float gain, float lift, float maskGain, float maskLift)
        {
            IsEnable = enabled;
            DepthMask = depthMask;
            Gain = gain;
            Lift = lift;
            MaskGain = maskGain;
            MaskLift = maskLift;
        }
    }

    [Serializable]
    public class ToneCurveParam
    {
        // Native camera block +0x550: bool, two curve references, four Color values, depth.
        public bool IsEnable;
        public AnimationCurve ToneCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve MaskToneCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public Color MinCorrectionLevel = Color.black, MaxCorrectionLevel = Color.white;
        public Color MaskMinCorrectionLevel = Color.black, MaskMaxCorrectionLevel = Color.white;
        public float DepthMask;
        public bool IsValidity => ToneCurve != null && MaskToneCurve != null;

        public void Setup(ToneCurveParam src)
        {
            if (src == null) return;
            IsEnable = src.IsEnable;
            ToneCurve = src.ToneCurve;
            MaskToneCurve = src.MaskToneCurve;
            MinCorrectionLevel = src.MinCorrectionLevel;
            MaxCorrectionLevel = src.MaxCorrectionLevel;
            MaskMinCorrectionLevel = src.MaskMinCorrectionLevel;
            MaskMaxCorrectionLevel = src.MaskMaxCorrectionLevel;
            DepthMask = src.DepthMask;
        }

        // Director 0x1a63ffb-0x1a64026 only writes enable and the two references.
        // Levels/depth stay at the parameter's authored/default values (0x1a45fb0).
        public void Set(bool enabled, AnimationCurve curve, AnimationCurve maskCurve)
        {
            IsEnable = enabled;
            if (!enabled) return;
            ToneCurve = curve;
            MaskToneCurve = maskCurve;
        }
    }
}
