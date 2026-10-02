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
        public Vector3 Position;
        public Vector3 Angle;
        public Vector3 Scale = Vector3.one;

        [NonSerialized]
        private Quaternion _rotationCache = Quaternion.identity;

        [NonSerialized]
        private bool _hasRotationCache;

        public Quaternion GetRotation()
        {
            if (!_hasRotationCache)
            {
                _rotationCache = Quaternion.Euler(Angle);
                _hasRotationCache = true;
            }

            return _rotationCache;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMobCyalumeControlDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMobCyalumeControlData>
    {
    }

    [Serializable]
    public class LiveTimelineMobCyalumeControlData : ILiveTimelineGroupDataWithName
    {
        public int GroupIndex;
        public LiveTimelineKeyMobCyalumeControlDataList Keys;
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
