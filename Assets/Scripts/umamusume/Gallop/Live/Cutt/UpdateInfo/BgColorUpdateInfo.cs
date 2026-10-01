using System;
using UnityEngine;
using static Gallop.Live.Cutt.LiveTimelineDefine;

namespace Gallop.Live.Cutt
{
    public struct BgColor1UpdateInfo
    {
        public string TimelineName;
        public int TimelineNameHash;
        public int[] TargetCharaIdArray;
        public int[] TargetDressIdArray;
        public Color color;
        public float colorPower;
        public float scale;
        public float Saturation;
        public int flags;
        public Color toonDarkColor;
        public Color toonBrightColor;
        public float vertexColorToonPower;
        public float outlineWidthPower;
        public Color outlineColor;
        public OutlineColorBlend outlineColorBlend;
        public LightBlendMode LightBlendMode;
        public bool IsSilhouette;
        public bool IsProjector;
        public ColorType CurrentColorType;
        public Color CurrentColor;
        public int CurrentFlags;
        public ColorType NextColorType;
        public Color NextColor;
        public int NextFlags;
        public float InterpolateRatio;
        public bool IsSyncBlinkLight;
        public bool IsSyncBlinkLightNext;
        public bool IsAdjustedBlinkLightColor;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower;
    }

    public delegate void BgColor1UpdateInfoDelegate(ref BgColor1UpdateInfo updateInfo);

    public struct BgColor2UpdateInfo
    {
        public string TimelineName;
        public int TimelineNameHash;
        public Color color1;
        public Color color2;
        public float power;
        public int randomTableIndex;

        // 兼容旧字段名
        public float value
        {
            get => power;
            set => power = value;
        }

        public int rndValueIdx
        {
            get => randomTableIndex;
            set => randomTableIndex = value;
        }
    }

    public delegate void BgColor2UpdateInfoDelegate(ref BgColor2UpdateInfo updateInfo);
}
