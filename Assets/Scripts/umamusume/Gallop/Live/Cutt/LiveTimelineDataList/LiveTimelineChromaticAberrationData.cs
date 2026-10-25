using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Native layout (LiveTimelineKeyChromaticAberrationData, TypeDefIndex 17376):
    // bool +0x30, redOffset +0x34, greenOffset +0x3C, blueOffset +0x44, power +0x4C,
    // clip +0x50, effectType +0x54. `effectType` is also the shader pass index.
    [Serializable]
    public class LiveTimelineKeyChromaticAberrationData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.ChromaticAberration;

        public bool isEnable;
        public Vector2 redOffset;
        public Vector2 greenOffset;
        public Vector2 blueOffset;
        public float power;
        public float clip;
        public int effectType;
    }

    [Serializable]
    public class LiveTimelineKeyChromaticAberrationDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyChromaticAberrationData> { }

    [Serializable]
    public class LiveTimelineChromaticAberrationData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyChromaticAberrationDataList keys =
            new LiveTimelineKeyChromaticAberrationDataList();

        public override ILiveTimelineKeyDataList GetKeyList() => keys;

        public LiveTimelineChromaticAberrationData() : base("ChromaticAberration") { }
    }

    // Native update payload (0x28 bytes) handed from the Control to the Director.
    public struct ChromaticAberrationUpdateInfo
    {
        public bool isEnable;
        public Vector2 redOffset;
        public Vector2 greenOffset;
        public Vector2 blueOffset;
        public float power;
        public float clip;
        public int effectType;
    }

    public delegate void ChromaticAberrationUpdateInfoDelegate(ref ChromaticAberrationUpdateInfo info);
}
