using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyBlinkLightData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.BlinkLight;
            }
        }

        public int LightBlendMode;
        public Color[] color0Array;
        public Color[] color1Array;
        public float[] powerArray;

        public int[] isReverseHueArray;
        public int[] CmnColorType0Array;
        public int[] CmnColorType1Array;
        public int[] CharacterIndex0Array;
        public int[] CharacterIndex1Array;
        public int[] IsColorBlend0Array;
        public int[] IsColorBlend1Array;
        public float[] ColorBlendRate0Array;
        public float[] ColorBlendRate1Array;
        public Color[] AltCharaColor0Array;
        public Color[] AltCharaColor1Array;

        public int pattern;
        public int colorType;
        public float powerMin;
        public float powerMax;
        public int loopCount;
        public float waitTime;
        public float turnOnTime;
        public float turnOffTime;
        public float keepTime;
        public float intervalTime;
    }

    [Serializable]
    public class LiveTimelineKeyBlinkLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyBlinkLightData>
    {
    }

    [Serializable]
    public class LiveTimelineBlinkLightData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyBlinkLightDataList keys;
    }
}