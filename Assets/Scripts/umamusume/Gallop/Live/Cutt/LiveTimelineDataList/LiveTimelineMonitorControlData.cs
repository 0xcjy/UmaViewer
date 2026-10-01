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
        public override LiveTimelineKeyDataType dataType
        {
            get{return LiveTimelineKeyDataType.MonitorControl;}
        }
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

        private const int ATTR_MULTI = 65536;
        private const int ATTR_PLAY_REVERSE = 131072;
        private const int ATTR_USE_MONITOR_CAMERA = 262144;
        private const int ATTR_FORCED_USE_MONITOR_CAMERA = 524288;
        private const int ATTR_ENABLE_BLEND_MODE = 1048576;

        public override void OnLoad(LiveTimelineControl timelineControl)
        {
            if (ChangeUVSettingArray == null)
                ChangeUVSettingArray = Array.Empty<LiveTimelineMonitorChangeUVSetting>();

            for (int i = 0; i < ChangeUVSettingArray.Length; i++)
            {
                if (ChangeUVSettingArray[i] == null)
                    ChangeUVSettingArray[i] = new LiveTimelineMonitorChangeUVSetting();

                if (ChangeUVSettingArray[i].ConditionArray == null)
                    ChangeUVSettingArray[i].ConditionArray = Array.Empty<LiveTimelineMonitorDressCondition>();
            }
        }

        public bool IsMultiFlag()
        {
            return (((int)attribute) & ATTR_MULTI) != 0;
        }

        public bool IsReversePlayFlag()
        {
            return (((int)attribute) & ATTR_PLAY_REVERSE) != 0;
        }

        public bool IsMonitorCameraFlag()
        {
            return (((int)attribute) & ATTR_USE_MONITOR_CAMERA) != 0;
        }

        public bool IsForcedUseMonitorCamera
        {
            get { return ((((int)attribute) & ATTR_FORCED_USE_MONITOR_CAMERA) != 0); }
        }

        public bool IsEnabledBlendMode
        {
            get { return ((((int)attribute) & ATTR_ENABLE_BLEND_MODE) != 0); }
        }
    }

    [Serializable]
    public class LiveTimelineKeyMonitorControlDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMonitorControlData>
    {
    }

    [Serializable]
    public class LiveTimelineMonitorControlData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "Monitor";
        public LiveTimelineKeyMonitorControlDataList keys;


        /// 不要!在这里重新声明名为 `name` 的序列化字段或属性。
  
        // 基类已经包含这个成员；重复声明会导致 AssetBundle 的 TypeTree 反序列化失败。
        public string SafeName
        {
            get { return string.IsNullOrWhiteSpace(name) ? default_name : name; }
        }
    }
}
