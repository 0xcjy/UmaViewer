using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Native layout 0x1af6b40: four adjacent serialized bools, not master5's ints.
    [Serializable]
    public class LiveTimelineKeyLensFlareData : LiveTimelineKeyWithInterpolate
    {
        public const int ATTRIBUTE_BIT_SCREEN_FIT = 0x10000;
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.LensFlare;
        public Vector3 offset;
        public Color color = Color.white;
        public float brightness = 1f;
        public float fadeSpeed = 1f;
        public bool enableParameter;
        public bool enableFlare = true;
        public bool IsAutoBrightness = true;
        public bool IsOverridePosition = true;

        public bool IsScreenFit
        {
            get => ((int)attribute & ATTRIBUTE_BIT_SCREEN_FIT) != 0;
            set => attribute = (LiveTimelineKeyAttribute)(value
                ? (int)attribute | ATTRIBUTE_BIT_SCREEN_FIT
                : (int)attribute & ~ATTRIBUTE_BIT_SCREEN_FIT);
        }
    }

    [Serializable]
    public class LiveTimelineKeyLensFlareDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLensFlareData> { }

    [Serializable]
    public class LiveTimelineLensFlareData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyLensFlareDataList keys = new LiveTimelineKeyLensFlareDataList();
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
        public LiveTimelineLensFlareData() : base("LensFlare") { }
    }

    public struct LensFlareUpdateInfo
    {
        public int TimelineNameHash;
        public Vector3 offset;
        public Color color;
        public float brightness;
        public float fadeSpeed;
        public bool enable;
        public bool enableParameter;
        public bool IsAutoBrightness;
        public bool IsOverridePosition;
        public bool IsScreenFit;
    }

    public delegate void LensFlareUpdateInfoDelegate(ref LensFlareUpdateInfo info);
}
