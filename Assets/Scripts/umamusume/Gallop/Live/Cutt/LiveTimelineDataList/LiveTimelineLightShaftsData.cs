using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyLightShaftsData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.LightShafts;
        public bool enabled;
        public Vector4 speed = new Vector4(-60f, -30f, -120f, -30f);
        public Vector4 angle = new Vector4(0f, 0f, 30f, 0f);
        public Vector4 offset;
        public Vector4 alpha = Vector4.one, alpha2 = Vector4.one, maskAlpha = Vector4.one;
        public float maskAnimTime = 0.75f;
        public Vector2 maskAlphaRange = new Vector2(0f, 1f);
        public float scale = 1f;
        public bool IsAdjustScale;
    }

    [Serializable]
    public class LiveTimelineKeyLightShaftsDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLightShaftsData> { }

    [Serializable]
    public class LiveTimelineLightShaftsData : ILiveTimelineGroupDataWithName
    {
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
        public LiveTimelineLightShaftsData() : base("LightShafts")
        {
            keys = new LiveTimelineKeyLightShaftsDataList();
        }
        public LiveTimelineKeyLightShaftsDataList keys;
    }
}
