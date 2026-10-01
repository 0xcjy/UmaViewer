using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyMobCyalumeControlData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get{return LiveTimelineKeyDataType.CyalumeControl;}
        }
        public Vector3 position;
        public Vector3 angle;
        public Vector3 scale = Vector3.one;

        [NonSerialized]
        private Quaternion _rotationCache = Quaternion.identity;

        [NonSerialized]
        private bool _hasRotationCache;

        public Quaternion GetRotation()
        {
            if (!_hasRotationCache)
            {
                _rotationCache = Quaternion.Euler(angle);
                _hasRotationCache = true;
            }

            return _rotationCache;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMobCyalumeControlDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMobCyalumeControlData>
    {
        public int unk48;
    }

    [Serializable]
    public class LiveTimelineMobCyalumeControlData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMobCyalumeControlDataList keys;
    }

    public delegate void MobCyalumeUpdateInfoDelegate(ref MobCyalumeUpdateInfo updateInfo);

    public struct MobCyalumeUpdateInfo
    {
        public LiveTimelineMobCyalumeControlData data;
        public int unk0;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public float currentFrame;
        public float currentLiveTime;
    }
}
