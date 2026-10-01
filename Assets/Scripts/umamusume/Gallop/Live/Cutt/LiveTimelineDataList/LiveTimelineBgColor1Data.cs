using System;
using UnityEngine;
using static Gallop.Live.Cutt.LiveTimelineDefine;

namespace Gallop.Live.Cutt
{
    public enum LiveTimelineKeyLoopType
    {
        None = 0,
        CopyStart = 1,
        CopyEnd = 2,
        Paste = 3,
        Max = 4
    }

    [Serializable]
    public class LiveTimelineKeyBgColor1Data : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.BgColor1;
            }
        }
        private const int ATTR_SILHOUETTE = 1 << 15;
        private const int ATTR_SYNC_BLINKLIGHT = 1 << 16;

        public Color color;
        public float power;
        public float scale;
        public int flags;
        public ColorType ColorType;
        public float Saturation;
        public Color toonDarkColor;
        public Color toonBrightColor;
        public float vertexColorToonPower;
        public float outlineWidthPower;
        public Color outlineColor;
        public OutlineColorBlend outlineColorBlend;
        public LightBlendMode LightBlendMode;
        public bool IsProjector;
        public string BlinkLightName;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower;
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

        public bool IsSilhouette => (flags & ATTR_SILHOUETTE) != 0;
        public bool IsSyncBlinkLight => (flags & ATTR_SYNC_BLINKLIGHT) != 0;
    }

    [Serializable]
    public class LiveTimelineKeyBgColor1DataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyBgColor1Data>
    {
    }

    [Serializable]
    public class LiveTimelineBgColor1Data : ILiveTimelineGroupDataWithName
    {
        private const string DEFAULT_NAME = "BgColor1";
        public LiveTimelineKeyBgColor1DataList keys = new LiveTimelineKeyBgColor1DataList();

        [SerializeField] private int[] _targetCharaIdArray;
        [SerializeField] private int[] _targetDressIdArray;

        public int[] TargetCharaIdArray => _targetCharaIdArray;
        public int[] TargetDressIdArray => _targetDressIdArray;

        public LiveTimelineBgColor1Data()
        {
            if (string.IsNullOrEmpty(name))
                name = DEFAULT_NAME;
        }
    }

    
}
