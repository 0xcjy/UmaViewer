using UnityEngine;

namespace Gallop.Live.Cutt
{
    public enum PostFilmMode
    {
        None = 0,
        Lerp = 1,
        Add = 2,
        Mul = 3,
        VignetteLerp = 4,
        VignetteAdd = 5,
        VignetteMul = 6,
        Monochrome = 7
    }

    public enum PostColorType
    {
        ColorAll = 0,
        Color2TopBottom = 1,
        Color2LeftRight = 2,
        Color4 = 3
    }


    [System.Serializable]
    public class LiveTimelineKeyPostFilmData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.PostFilm;
        public enum LayerMode
        {
            Color = 0,
            UVMovie = 1
        }

        public enum ColorBlend
        {
            None = 0,
            Lerp = 1,
            Additive = 2,
            Multiply = 3
        }

        public PostFilmMode filmMode; // 0x30
        public PostColorType colorType; // 0x34
        public float filmPower; // 0x38
        public Vector2 filmOffsetParam; // 0x3C
        public Vector4 filmOptionParam; // 0x44
        public Color color0; // 0x54
        public Color color1; // 0x64
        public Color color2; // 0x74
        public Color color3; // 0x84
        public float depthPower; // 0x94
        public float DepthClip; // 0x98
        public float RollAngle; // 0x9C
        public Vector2 FilmScale; // 0xA0

        // 以下顺序按 bundle TypeTree 的权威顺序排列
        // （tools/dump_cutt_typetree.py --tree postFilmKeys 1177）。
        // 原来的声明把 loop* 组提到了 layerMode 之前，且缺 BlinkLight* 五个字段。
        public LiveTimelineKeyPostFilmData.LayerMode layerMode;
        public int movieResId;
        public int movieFrameOffset;
        public float movieSpeed;
        public LiveTimelineKeyPostFilmData.ColorBlend colorBlend;
        public float colorBlendFactor;

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
    }

    [System.Serializable]
    public class LiveTimelineKeyPostFilmDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPostFilmData>
    {
    }
}
