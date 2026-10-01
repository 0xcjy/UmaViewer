using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyVolumeLightData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.VolumeLight;
        public Vector3 sunPosition;
        public Color color1 = Color.white;
        public float power, komorebi, blurRadius, ColorRate;
        public float ScreenColorPower = 1f, EffectColorPower = 1f;
        public bool enable, isEnabledBorderClear;
        public string BlinkLightName = string.Empty;
        public int BlinkLightNameHash, BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower = 1f;
        public bool IsAdjustedBlinkLightColor = true;
        public bool IsEnabledBlurAlpha => ((int)attribute & 0x10000) != 0;
        public bool IsSyncBlinkLight => ((int)attribute & 0x20000) != 0;
    }

    [Serializable]
    public class LiveTimelineKeyVolumeLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyVolumeLightData> { }

    [Serializable]
    public class LiveTimelineVolumeLightData : ILiveTimelineGroupDataWithName
    {
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
        public LiveTimelineVolumeLightData() : base("VolumeLight")
        {
            keys = new LiveTimelineKeyVolumeLightDataList();
        }
        public LiveTimelineKeyVolumeLightDataList keys;
    }
}
