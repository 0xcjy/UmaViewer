using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyBgColor2Data : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get{return LiveTimelineKeyDataType.BgColor2;}
        }
        public Color color1;
        public Color color2;
        public float power;
        public string BlinkLightName;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower;
        public bool IsSyncBlinkLightToColor1;
        public bool IsSyncBlinkLightToColor2;
        public bool IsAdjustedBlinkLightColor;
        public LiveTimelineKeyLoopType loopType;
        public int loopCount;
        public int loopExecutedCount;
        public int loopIntervalFrame;
        public bool isPasteLoopUnit;
        public bool isChangeLoopInterpolate;
        public float f32;
        public LiveTimelineKeyLoopType _loopType;
        public int _loopCount;
        public int _loopExecutedCount;
        public int _loopIntervalFrame;
        public bool _isChangeLoopInterpolate;

        public virtual int RandomTableIndex()
        {
            return 0;
        }

        public override void OnLoad(LiveTimelineControl timelineControl)
        {
            if (!string.IsNullOrEmpty(BlinkLightName) && BlinkLightNameHash == 0)
                BlinkLightNameHash = Animator.StringToHash(BlinkLightName);
        }
    }

    [Serializable]
    public class LiveTimelineKeyBgColor2DataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyBgColor2Data>
    {
    }

    [Serializable]
    public class LiveTimelineBgColor2Data : ILiveTimelineGroupDataWithName
    {
        private const string DEFAULT_NAME = "BgColor2";
        public LiveTimelineKeyBgColor2DataList keys = new LiveTimelineKeyBgColor2DataList();

        public LiveTimelineBgColor2Data()
        {
            if (string.IsNullOrEmpty(name))
                name = DEFAULT_NAME;
        }
    }
}
