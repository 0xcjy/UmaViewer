using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyLaserData : LiveTimelineKeyWithInterpolate, ITransformCPBundleData
    {
        private const int ATTR_ENABLE_RENDER = 0x00010000;

        private const int ATTR_ENABLE_RAYCAST = 0x00020000;

        private const int ATTR_DISABLE_ROOT_LIGHT = 0x00040000;

        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Laser;

        public Vector3 objectPosition;
        public Vector3 objectRotate;
        public Vector3 objectScale = Vector3.one;

        public LaserFormation formation;
        public Vector3 rotate;

        public float degRootYaw;
        public float degLaserPitch;
        public float posInterval;

        public LaserBlink blink;
        public float blinkPeriod;
        public float RaycastDistance;

        public bool IsEnabledRender => (((int)attribute) & ATTR_ENABLE_RENDER) != 0;

        public bool IsEnabledRaycast => (((int)attribute) & ATTR_ENABLE_RAYCAST) != 0;

        public bool IsDisabledRootLight => (((int)attribute) & ATTR_DISABLE_ROOT_LIGHT) != 0;

        // 临时兼容旧 LaserController。
        public bool IsEnabled => IsEnabledRender;
    }

    [Serializable]
    public class LiveTimelineKeyLaserDataList : LiveTimelineKeyDataListTemplate< LiveTimelineKeyLaserData>
    {
        
    }

    [Serializable]
    public class LiveTimelineLaserData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "Laser";

        public LiveTimelineKeyLaserDataList keys;

        public int _objectIndex;
        public int _materialIndex;

        public override ILiveTimelineKeyDataList
            GetKeyList()
        {
            return keys;
        }

        public LiveTimelineLaserData() : base(default_name)
        {
            keys = new LiveTimelineKeyLaserDataList();
        }
    }
}