using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Native layout (LiveTimelineKeyVortexData, TypeDefIndex 17479):
    // IsEnable +0x30, Area +0x34 (Vector4), RotVolume +0x44, DepthClip +0x48.
    [Serializable]
    public class LiveTimelineKeyVortexData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Vortex;

        public bool IsEnable;
        public Vector4 Area;
        public float RotVolume;
        public float DepthClip;
    }

    [Serializable]
    public class LiveTimelineKeyVortexDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyVortexData> { }
}
