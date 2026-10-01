using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyGlobalFogData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.GlobalFog;
        public bool isDistance;
        public float startDistance;
        public bool isHeight;
        public float height;
        public float heightDensity;
        public Color color;
        public int fogMode;
        public float expDensity;
        public float start;
        public float end;
        public bool useRadialDistance;
        public string BlinkLightName;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower;
        public bool IsAdjustedBlinkLightColor;
    }

    [Serializable]
    public class LiveTimelineKeyGlobalFogDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyGlobalFogData>
    {
    }

    [Serializable]
    public class LiveTimelineGlobalFogData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "GlobalFog";
        public LiveTimelineKeyGlobalFogDataList keys;
    }
}
