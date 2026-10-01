using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyWashLightData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.WashLight;
            }
        }

        private const int ATTR_ENABLE_RAYCAST = 65536;

        public float RaycastDistance;
        public float CameraProjectionSide;
        public float CameraProjectionColorPower;

        public bool IsEnabledRaycast
        {
            get
            {
                return (((int)attribute) & ATTR_ENABLE_RAYCAST) != 0;
            }
            private set
            {
                if (value)
                {
                    attribute = (LiveTimelineKeyAttribute)((int)attribute | ATTR_ENABLE_RAYCAST);
                }
                else
                {
                    attribute = (LiveTimelineKeyAttribute)((int)attribute & ~ATTR_ENABLE_RAYCAST);
                }
            }
        }

        public LiveTimelineKeyWashLightData()
        {
        }
    }

    [Serializable]
    public class LiveTimelineKeyWashLightDataList
        : LiveTimelineKeyDataListTemplate<LiveTimelineKeyWashLightData>
    {
    }

    [Serializable]
    public class LiveTimelineWashLightData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "WashLight";

        public LiveTimelineKeyWashLightDataList keys;

        public int _isAllSettings;

        public override ILiveTimelineKeyDataList GetKeyList()
        {
            return keys;
        }

        public LiveTimelineWashLightData()
            : base(default_name)
        {
            keys = new LiveTimelineKeyWashLightDataList();
        }
    }
}