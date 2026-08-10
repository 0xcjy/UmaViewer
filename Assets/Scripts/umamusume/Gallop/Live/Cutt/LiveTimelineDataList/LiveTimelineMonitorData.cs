using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineMonitorDressCondition
    {
        public int IsCheck;
        public int CharaId;
        public int DressId;
        public bool IsEnabled => IsCheck != 0;
    }

    [Serializable]
    public class LiveTimelineMonitorChangeUVSetting
    {
        public int IsChangeUVSetting;
        public int DispID = -1;
        public LiveTimelineMonitorDressCondition[] ConditionArray = Array.Empty<LiveTimelineMonitorDressCondition>();
        public bool IsEnabled => IsChangeUVSetting != 0;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorControlData : LiveTimelineKeyWithInterpolate
    {
        public Vector2 position;
        public Vector2 size = Vector2.one;
        public int dispID;
        public float speed = 1f;
        public string outputTextureLabel = string.Empty;
        public int playStartOffsetFrame;
        public float blendFactor = 1f;
        public Color colorFade = Color.clear;
        public Color BaseColor = Color.clear;
        public int SrcBlendMode = 1;
        public int DstBlendMode;
        public int RenderQueueNo = 2000;
        public int IsRenderQueue;
        public int DispID2 = -1;
        public float CrossFadeRate;
        public int LightImageNo;
        public int LightImageNo2;
        public float FilterTexScale = 1f;
        public LiveTimelineMonitorChangeUVSetting[] ChangeUVSettingArray = Array.Empty<LiveTimelineMonitorChangeUVSetting>();
        public int extraContent;

        private const int AttrMulti = 65536;
        private const int AttrPlayReverse = 131072;
        private const int AttrUseMonitorCamera = 262144;
        private const int AttrForcedUseMonitorCamera = 524288;
        private const int AttrEnableBlendMode = 1048576;

        public bool IsMultiFlag() => (((int)attribute) & AttrMulti) != 0;
        public bool IsReversePlayFlag() => (((int)attribute) & AttrPlayReverse) != 0;
        public bool IsMonitorCameraFlag() => (((int)attribute) & AttrUseMonitorCamera) != 0;
        public bool IsForcedUseMonitorCamera => (((int)attribute) & AttrForcedUseMonitorCamera) != 0;
        public bool IsEnabledBlendMode => (((int)attribute) & AttrEnableBlendMode) != 0;
    }

    // Monitor-camera tracks carry these fields in addition to the shared camera schema.
    // Names are case-sensitive and must match the Cutt TypeTree.
    [Serializable]
    public class LiveTimelineKeyMonitorCameraPositionData : LiveTimelineKeyCameraPositionData
    {
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
    public class LiveTimelineKeyMonitorControlDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMonitorControlData> { }

    [Serializable]
    public class LiveTimelineMonitorControlData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMonitorControlDataList keys;
        public string SafeName => string.IsNullOrWhiteSpace(name) ? "Monitor" : name;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorCameraPositionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMonitorCameraPositionData> { }

    [Serializable]
    public class LiveTimelineMonitorCameraPositionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMonitorCameraPositionDataList keys;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorCameraLookAtData : LiveTimelineKeyCameraLookAtData
    {
        public Vector3 CharaPositionAtStartFrame;
        public bool IsUseCharaPositionAtPrevKeyStartFrame;
        public bool IsAttachedToProps;
        public int PropsIndex;
        public int PropsAttachNodeIndex;
    }

    [Serializable]
    public class LiveTimelineKeyMonitorCameraLookAtDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMonitorCameraLookAtData> { }

    [Serializable]
    public class LiveTimelineMonitorCameraLookAtData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMonitorCameraLookAtDataList keys;
    }
}
