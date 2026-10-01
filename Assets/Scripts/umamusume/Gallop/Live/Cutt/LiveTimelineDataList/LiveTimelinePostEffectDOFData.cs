using System;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyPostEffectDOFData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.PostEffectDOF;
        public float forcalSize;
        public float blurSpread;
        public int charactor;
        public int dofBlurType;
        public int dofQuality;
        public float dofForegroundSize;
        public float dofFocalPoint;
        public float dofSmoothness;
        public float BallBlurPowerFactor;
        public float BallBlurBrightnessThreshhold;
        public float BallBlurBrightnessIntensity;
        public float BallBlurSpread;

        /// <summary>
        /// `charactor` is a <see cref="LiveCharaPositionFlag"/>, not an index.
        /// Evidence: a scan of the authored keys of all 61 songs (4427 keys,
        /// tmp/dof-attr-scan.txt) never produces a value outside the 18 Place bits,
        /// and all but 6 keys use a *named* member of that enum -- Center=1
        /// (4252 of 4427 keys), Side=6, Left=2, Right=4, All=262143. The 6 remaining values
        /// are still sensible combinations (7 = Center|Left|Right, 5 = Center|Right,
        /// 66560 = Place11|Place17), and All=262143 is exactly 18 bits wide, matching
        /// liveCharaPositionMax.
        ///
        /// The mask is meaningful only in the character-target branch. Keys can
        /// retain Center while focusing the camera target or a metric distance.
        /// </summary>
        public LiveCharaPositionFlag FocusCharacters => (LiveCharaPositionFlag)charactor;

        // High `attribute` bits are per-track flags (same convention as
        // LiveTimelineMonitorData). Across the 4427 authored keys only bits 16..19
        // appear, in the combinations 0x50000 (3117), 0x70000 (923), 0x60000 (108),
        // 0x10000 (106), 0x40000 (77), 0x30000 (72), 0x20000 (22), 0x00000 (1) and
        // 0xF0000 (1). Bit 17 covaries with an authored dofFocalPoint: 95.1% of the
        // 3301 bit17-clear keys leave it at the 1.0 default, while 88.1% of the 1126
        // bit17-set keys carry a real distance. Bit 16 covaries the other way with
        // `charactor`: only 1.4% of the 4219 bit16-set keys move it off Center,
        // against 56.7% of the 208 bit16-clear keys. The flag names are still
        // unknown. Timeline mode tests the inferred bit17=metric / bit16=camera
        // target mapping; see docs/LIVE_DOF_TIMELINE_2026-09-06.md for pixel evidence.
    }

    [Serializable]
    public class LiveTimelineKeyPostEffectDOFDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPostEffectDOFData> { }
}
