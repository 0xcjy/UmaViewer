using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // MonitorCameraLayer is a character-height-dependent positional range,
    // not a Unity culling-mask track (native AlterUpdate_MonitorCameraLayer).
    [Serializable]
    public class LiveTimelineKeyMonitorCameraLayerData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MonitorCameraLayer;
        public Vector3 offsetMaxPosition;
        public Vector3 offsetMinPosition;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorCameraLayerDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMonitorCameraLayerData> { }

    [Serializable]
    public class LiveTimelineMonitorCameraLayerData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMonitorCameraLayerDataList keys = new LiveTimelineKeyMonitorCameraLayerDataList();
        public LiveTimelineMonitorCameraLayerData() : base("MonitorCameraLayer") { }
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorCameraPositionData : LiveTimelineKeyCameraPositionData
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MonitorCameraPos;
        public Vector3 CharaPositionAtStartFrame;
        public bool IsUseCharaPositionAtPrevKeyStartFrame;
        public bool IsAttachedToProps;
        public int PropsIndex;
        public int PropsAttachNodeIndex;
        public bool enable;
        public float fov = 30f;
        public float roll;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorCameraPositionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMonitorCameraPositionData>
    {
    }

    [Serializable]
    public class LiveTimelineMonitorCameraPositionData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "MonitorCameraPos";
        public LiveTimelineKeyMonitorCameraPositionDataList keys;
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorCameraLookAtData : LiveTimelineKeyCameraLookAtData
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MonitorCameraLookAt;
        public Vector3 CharaPositionAtStartFrame;
        public bool IsUseCharaPositionAtPrevKeyStartFrame;
        public bool IsAttachedToProps;
        public int PropsIndex;
        public int PropsAttachNodeIndex;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorCameraLookAtDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMonitorCameraLookAtData>
    {
    }

    [Serializable]
    public class LiveTimelineMonitorCameraLookAtData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "MonitorCameraLookAt";
        public LiveTimelineKeyMonitorCameraLookAtDataList keys;
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }
}
