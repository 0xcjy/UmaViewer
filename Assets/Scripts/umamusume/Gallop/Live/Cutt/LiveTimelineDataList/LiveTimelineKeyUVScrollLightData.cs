using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyUVScrollLightData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get{return LiveTimelineKeyDataType.UVScrollLight;}
        }
        public Color mulColor0 = Color.white;
        public Color mulColor1 = Color.white;
        public float colorPower = 1f;

        public float scrollOffsetX;
        public float scrollOffsetY;
        public float scrollSpeedX;
        public float scrollSpeedY;

        public Texture2D texture;

        public LiveTimelineDefine.ColorType ColorType0;
        public LiveTimelineDefine.ColorType ColorType1;
        public int CharacterIndex0;
        public int CharacterIndex1;
        public bool IsColorBlend0;
        public bool IsColorBlend1;
        public float ColorBlendRate0;
        public float ColorBlendRate1;
        public Color AltCharaColor0 = Color.white;
        public Color AltCharaColor1 = Color.white;

        public LiveTimelineKeyLoopType loopType;
        public int loopCount;
        public int loopExecutedCount;
        public int loopIntervalFrame;
        public bool isPasteLoopUnit = true;
        public bool isChangeLoopInterpolate = true;

        private const int ENABLE_TEXTURE = 65536;

        // 这里先按当前逻辑实现
        // 原版逻辑是从某个 flags 位来判断 ENABLE_TEXTURE
        // 但目前项目里没有那个 flags 字段，因此这里先保留现有的等价处理
        public bool IsEnabledTexture
        {
            get => texture != null;
            private set { }
        }

        public override void OnLoad(LiveTimelineControl timelineControl)
        {
            // 预留原版逻辑中需要使用的 ColorType、CharacterIndex、Blend 等参数
            // 等后续颜色相关资源准备完成后再接入。
        }
    }

    [Serializable]
    public class LiveTimelineKeyUVScrollLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyUVScrollLightData>
    {
    }

    [Serializable]
    public class LiveTimelineUVScrollLightData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "UVScrollLight";

        public LiveTimelineKeyUVScrollLightDataList keys;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineUVScrollLightData()
            : base(default_name)
        {
            keys = new LiveTimelineKeyUVScrollLightDataList();
        }
    }
}
