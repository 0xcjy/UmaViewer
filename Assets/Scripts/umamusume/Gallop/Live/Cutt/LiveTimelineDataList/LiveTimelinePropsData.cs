using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Authored son1093_camera TypeTree; UInt8 flags are bool, unlike the master5 reconstruction.
    [Serializable]
    public class LiveTimelineKeyPropsData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Props;

        public int settingFlags;
        public int propsID;
        public bool rendererEnable;
        public bool IsVisibleAttachedCharaLinked;
        public bool AutoSwitchLayerOnMirrorRendering;
        public Color color = Color.white;
        public Color rootColor = Color.white;
        public Color tipColor = Color.white;
        public float colorPower = 1f;
        public bool IsApplyAnimation;
        public bool IsApplyReserveWarming;
        public AnimationClip AnimationClip;
        public float StartAnimationTime;
        public int AnimationHeadFrame;
        public Color ToonDarkColor;
        public Color ToonBrightColor;
        public bool IsCastShadow;
        public bool IsCastShadowForced;
        public Vector3 _directionalLightAngle;
        public bool IsUpdateOutline;
        public float OutlineWidth;
        public Color OutlineColor;
        public bool IsEmissive;
        public Color EmissiveColor;
        public float EmissiveScrollTimeScale = 1f;
        public float EmissiveScrollEnergyScale = 1f;
    }

    [Serializable]
    public class LiveTimelineKeyPropsDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPropsData> { }

    [Serializable]
    public class LiveTimelinePropsData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyPropsDataList keys = new LiveTimelineKeyPropsDataList();
        public bool _applyVariation;
        public int _variationId;
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }

    [Serializable]
    public class LiveTimelineKeyPropsAttachData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.PropsAttach;

        public string _attachJointName;
        public int _attachJointHash;
        public string _copyPositionJointName;
        public int _copyPositionJointHash;
        public int _settingFlags;
        public int _propsId;
        public Vector3 _offsetPosition;
        public Vector3 OffsetRotate;
        public Vector3 OffsetScale = Vector3.one;
        public bool IsLinkAttachBone;
        public int _attachType;
        public int _attachPropId = -1;
        public string _attachTargetPropNodeName;
    }

    [Serializable]
    public class LiveTimelineKeyPropsAttachDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPropsAttachData> { }

    [Serializable]
    public class LiveTimelinePropsAttachData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyPropsAttachDataList keys = new LiveTimelineKeyPropsAttachDataList();
        public bool _applyVariation;
        public int _variationId;
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }
}
