using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Native layout (LiveTimelineKeyFluctuationData, TypeDefIndex 17405):
    // IsEnable +0x30, MoveDirection +0x34 (Vector2), MovePower +0x3C, Power +0x40, DepthClip +0x44,
    // plus ATTR_DISABLE_GRAPHICSETTINGS = 0x10000 on the key attribute.
    [Serializable]
    public class LiveTimelineKeyFluctuationData : LiveTimelineKeyWithInterpolate
    {
        public const int ATTR_DISABLE_GRAPHICSETTINGS = 65536;

        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Fluctuation;

        public bool IsEnable;
        public Vector2 MoveDirection;
        public float MovePower;
        public float Power;
        public float DepthClip;

        public bool DisableGraphicSettings
        {
            get => ((int)attribute & ATTR_DISABLE_GRAPHICSETTINGS) != 0;
            set
            {
                int attr = (int)attribute;
                if (value) attr |= ATTR_DISABLE_GRAPHICSETTINGS;
                else attr &= ~ATTR_DISABLE_GRAPHICSETTINGS;
                attribute = (LiveTimelineKeyAttribute)attr;
            }
        }
    }

    [Serializable]
    public class LiveTimelineKeyFluctuationDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyFluctuationData> { }
}
