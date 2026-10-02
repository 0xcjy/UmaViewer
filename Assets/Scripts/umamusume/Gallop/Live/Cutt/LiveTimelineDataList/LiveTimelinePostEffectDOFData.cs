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

        // Actual game metadata and SetupDOFUpdateInfo RVA 0x726a610 confirm:
        // bit16 ATTR_USE_LOOKAT; bit17 ATTR_USE_FOCAL_POINT;
        // bit18 ATTR_ENABLE_DOF; bit19 ATTR_IS_POINT_BALL_BLUR.
        // Current-key flags select the mode; both endpoints resolve world focus
        // independently before the next key's unclamped interpolation is applied.
    }

    [Serializable]
    public class LiveTimelineKeyPostEffectDOFDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPostEffectDOFData> { }
}
