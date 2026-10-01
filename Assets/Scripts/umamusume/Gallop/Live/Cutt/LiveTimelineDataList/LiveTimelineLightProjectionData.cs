using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineLightProjectionAnimationParam
    {
        public int TextureId = -1;
        public int DivisionNumberX;
        public int DivisionNumberY;
        public int MaxCut;
        public float AnimationTime;
        public Vector2 ScaleUV = Vector2.one;
        public Vector2 OffsetUV;
    }

    // Native 0x1af6c90; bool widths also verified against the authored worksheet TypeTree.
    [Serializable]
    public class LiveTimelineKeyLightProjectionData : LiveTimelineKeyWithInterpolate
    {
        public const int ATTR_USE_MONITOR_MOVIE = 0x10000;
        public const int ATTR_USE_ANIMATION = 0x20000;
        public const int ATTR_SYNC_BLINKLIGHT = 0x40000;
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.LightProjection;

        public bool IsEnable = true;
        public bool OverrideIgnoreLayer;
        public LayerMask OverrideLayerMask;
        public bool BacksideOff;
        public float MirrorBallBacksideFeather = 0.15f;
        public bool ProjectionToCharacterModelOnly;
        public int TextureId;
        public Color Color = UnityEngine.Color.white;
        public Vector3 Position;
        public Vector3 Angle = new Vector3(90f, 0f, 0f);
        public Vector3 Scale = Vector3.one;
        public bool Orthographic;
        public float OrthographicSize = 10f;
        public float NearClipPlane = 0.1f;
        public float FarClipPlane = 100f;
        public float FieldOfView = 60f;
        public float ColorPower = 1f;
        public int LightBlendMode;
        public bool CharacterAttach;
        public int CharacterAttachPosition;
        public string BlinkLightName = string.Empty;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower = 1f;
        public bool IsAdjustedBlinkLightColor = true;
        public Vector3 MirrorBallRotateAxis = Vector3.up;
        public float MirrorBallRotateValue;
        public float MirrorBallProjectionRadius = 20f;
        public float MirrorBallFallOffPower = 2f;
        public bool MirrorBallIsLoopRotation;
        public float MirrorBallLoopRotationSpeed;
        public Vector2 MirrorBallUVOffset;
        public Vector2 MirrorBallUVScale = Vector2.one;
        public bool MirrorBallUseCubeMap;
        public LiveTimelineLightProjectionAnimationParam AnimationParam;
        [NonSerialized] public Quaternion Rotation;

        public bool UseMonitorMovie() => ((int)attribute & ATTR_USE_MONITOR_MOVIE) != 0;
        public bool UseAnimation() => ((int)attribute & ATTR_USE_ANIMATION) != 0;
        public bool IsSyncBlinkLight
        {
            get => ((int)attribute & ATTR_SYNC_BLINKLIGHT) != 0;
            set => attribute = (LiveTimelineKeyAttribute)(value
                ? (int)attribute | ATTR_SYNC_BLINKLIGHT
                : (int)attribute & ~ATTR_SYNC_BLINKLIGHT);
        }

        public override void OnLoad(LiveTimelineControl timelineControl)
        {
            // Native 0x1af6c30 caches Euler degrees as a quaternion before interpolation.
            Rotation = Quaternion.Euler(Angle);
            if (BlinkLightNameHash == 0 && !string.IsNullOrEmpty(BlinkLightName))
                BlinkLightNameHash = FNVHash.Generate(BlinkLightName);
        }
    }

    [Serializable]
    public class LiveTimelineKeyLightProjectionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLightProjectionData> { }

    [Serializable]
    public class LiveTimelineLightProjectionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyLightProjectionDataList keys = new LiveTimelineKeyLightProjectionDataList();
        // Preserve the serialized enum's Int32 representation without inventing mode names.
        public int ContentType;
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
        public LiveTimelineLightProjectionData() : base("projector_") { }
    }

    public delegate void LightProjectionUpdateInfoDelegate(ref LiveLightProjectionTimeline.UpdateInfo info);
}
