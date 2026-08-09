using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyLensFlareData : LiveTimelineKeyWithInterpolate
    {
        public Vector3 offset;
        public Color color = Color.white;
        public float brightness = 1f;
        public float fadeSpeed = 1f;
        public int enableParameter;
        public int enableFlare;
        public int IsAutoBrightness;
        public int IsOverridePosition;
    }

    [Serializable]
    public class LiveTimelineKeyLensFlareDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLensFlareData> { }

    [Serializable]
    public class LiveTimelineLensFlareData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyLensFlareDataList keys;
    }

    [Serializable]
    public class LiveTimelineKeyStageEnvironmentData : LiveTimelineKey
    {
        public bool isValidMirror;
        public bool isMirror;
        public bool isBgMirror;
        public bool IsMirrorBg3d;
        public bool EnableCharacterMirrorExpandFaceBounds;
        public int characterMirror;
        public int CharacterMirrorHead;
        public int CharacterMirrorExpandFaceBounds;
        public float mirrorReflectionRate = 1f;
        public bool isValidShadow;
        public int characterShadow;
        public bool isSoftShadow;
        public bool IsToonMirror;
        public bool isValidWaterReflection;
        public float waterReflection;
        public float waveScale;
        public float waterDistortion;
        public float waterUCross;
        public float waterVCross;
        public float waterUSpeed;
        public float waterVSpeed;
        public float waterNormalPower;
        public float waveDistortionPower;
        public float waveClearly;
        public float waveDiffusion;
        public float waveDecline;
        public Color waterColor;
        public float waterVOffset;
        public bool IsValidStageFovShift;
        public float BaseFov;
        public float ShiftPower;
        public bool IsShiftY;
        public bool _isValidMirror;
        public int _mirror;
        public int _bgMirror;
        public int _characterMirror;
        public float _mirrorReflectionRate = -1f;

        public bool MirrorIsValid => isValidMirror || _isValidMirror;
        public bool MirrorEnabled => isMirror || isBgMirror || IsMirrorBg3d || _mirror != 0 || _bgMirror != 0;
        public float ReflectionRate => _mirrorReflectionRate >= 0f ? _mirrorReflectionRate : mirrorReflectionRate;
    }

    [Serializable]
    public class LiveTimelineKeyStageEnvironmentDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyStageEnvironmentData> { }

    [Serializable]
    public class LiveTimelineStageEnvironmentData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyStageEnvironmentDataList keys;
    }

    [Serializable]
    public class LiveTimelineKeyFacialToonData : LiveTimelineKeyWithInterpolate
    {
        public float CheekPretenseThreshold;
        public float NosePretenseThreshold;
        public float CylinderBlend;
        public float HairNormalBlend;
        public int UseOriginalDirectionalLight;
        public Vector3 OriginalDirectionalLightDir;
        public float EyeToonStep;
        public float EyeToonFeather;
        public float EyeSaturation;
    }

    [Serializable]
    public class LiveTimelineKeyFacialToonDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyFacialToonData> { }

    [Serializable]
    public class LiveTimelineFacialToonData
    {
        public LiveTimelineKeyFacialToonDataList centerKeys;
        public LiveTimelineKeyFacialToonDataList left1Keys;
        public LiveTimelineKeyFacialToonDataList right1Keys;
        public LiveTimelineKeyFacialToonDataList left2Keys;
        public LiveTimelineKeyFacialToonDataList right2Keys;
        public LiveTimelineKeyFacialToonDataList motion5Keys;
        public LiveTimelineKeyFacialToonDataList motion6Keys;
        public LiveTimelineKeyFacialToonDataList motion7Keys;
        public LiveTimelineKeyFacialToonDataList motion8Keys;
        public LiveTimelineKeyFacialToonDataList motion9Keys;
        public LiveTimelineKeyFacialToonDataList motion10Keys;
        public LiveTimelineKeyFacialToonDataList motion11Keys;
        public LiveTimelineKeyFacialToonDataList motion12Keys;
        public LiveTimelineKeyFacialToonDataList motion13Keys;
        public LiveTimelineKeyFacialToonDataList motion14Keys;
        public LiveTimelineKeyFacialToonDataList motion15Keys;
        public LiveTimelineKeyFacialToonDataList motion16Keys;
        public LiveTimelineKeyFacialToonDataList motion17Keys;
        public LiveTimelineKeyFacialToonDataList motion18Keys;
        public LiveTimelineKeyFacialToonDataList motion19Keys;

        public LiveTimelineKeyFacialToonDataList GetKeys(int index)
        {
            switch (index)
            {
                case 0: return centerKeys; case 1: return left1Keys; case 2: return right1Keys;
                case 3: return left2Keys; case 4: return right2Keys; case 5: return motion5Keys;
                case 6: return motion6Keys; case 7: return motion7Keys; case 8: return motion8Keys;
                case 9: return motion9Keys; case 10: return motion10Keys; case 11: return motion11Keys;
                case 12: return motion12Keys; case 13: return motion13Keys; case 14: return motion14Keys;
                case 15: return motion15Keys; case 16: return motion16Keys; case 17: return motion17Keys;
                case 18: return motion18Keys; case 19: return motion19Keys; default: return null;
            }
        }
    }

    [Serializable]
    public class LiveTimelineKeyCameraLayerData : LiveTimelineKeyWithInterpolate
    {
        public Vector3 offsetMaxPosition;
        public Vector3 offsetMinPosition;
    }

    [Serializable]
    public class LiveTimelineKeyCameraLayerDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCameraLayerData> { }

    [Serializable]
    public class LiveTimelineKeyPropsData : LiveTimelineKeyWithInterpolate
    {
        public int settingFlags;
        public int propsID;
        public int rendererEnable;
        public int IsVisibleAttachedCharaLinked;
        public int AutoSwitchLayerOnMirrorRendering;
        public Color color = Color.white;
        public Color rootColor = Color.white;
        public Color tipColor = Color.white;
        public float colorPower = 1f;
        public int IsApplyAnimation;
        public int IsApplyReserveWarming;
        public AnimationClip AnimationClip;
        public float StartAnimationTime;
        public int AnimationHeadFrame;
        public Color ToonDarkColor;
        public Color ToonBrightColor;
        public int IsCastShadow;
        public int IsCastShadowForced;
        public Vector3 _directionalLightAngle;
        public int IsUpdateOutline;
        public float OutlineWidth;
        public Color OutlineColor;
        public int IsEmissive;
        public Color EmissiveColor;
        public float EmissiveScrollTimeScale = 1f;
        public float EmissiveScrollEnergyScale = 1f;
    }

    [Serializable] public class LiveTimelineKeyPropsDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPropsData> { }
    [Serializable]
    public class LiveTimelinePropsData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyPropsDataList keys;
        public bool _applyVariation;
        public int _variationId;
    }

    [Serializable]
    public class LiveTimelineKeyPropsAttachData : LiveTimelineKeyWithInterpolate
    {
        public string _attachJointName;
        public int _attachJointHash;
        public string _copyPositionJointName;
        public int _copyPositionJointHash;
        public int _settingFlags;
        public int _propsId;
        public Vector3 _offsetPosition;
        public Vector3 OffsetRotate;
        public Vector3 OffsetScale = Vector3.one;
        public int IsLinkAttachBone;
        public int _attachType;
        public int _attachPropId = -1;
        public string _attachTargetPropNodeName;
    }

    [Serializable] public class LiveTimelineKeyPropsAttachDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyPropsAttachData> { }
    [Serializable]
    public class LiveTimelinePropsAttachData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyPropsAttachDataList keys;
        public bool _applyVariation;
        public int _variationId;
    }

    [Serializable]
    public class LiveTimelineKeyCharaFootLightData : LiveTimelineKeyWithInterpolate
    {
        public int positionFlag;
        public float[] hightMax = Array.Empty<float>();
        public Color[] lightColor = Array.Empty<Color>();
        public int[] LightBlendModeArray = Array.Empty<int>();
        public int[] EasingArray = Array.Empty<int>();
    }

    [Serializable] public class LiveTimelineKeyCharaFootLightDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCharaFootLightData> { }

    [Serializable]
    public class LiveTimelineKeyData_AdditionalLight : LiveTimelineKeyWithInterpolate
    {
        public Vector3 Position;
        public Vector3 Rotate;
        public int IsEnable;
        public int Type;
        public float Range;
        public int SpotAngle;
        public int IndirectMultiplier;
        public int ShadowType;
        public float Strength = 1f;
        public float Bias = 0.05f;
        public float NormalBias = 0.4f;
        public float NearPlane = 0.2f;
    }

    [Serializable] public class LiveTimelineKeyAdditionalLightList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyData_AdditionalLight> { }
    [Serializable]
    public class LiveTimelineAdditionalLight : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyAdditionalLightList keys;
    }

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

    [Serializable]
    public class LiveTimelineKeyLightProjectionData : LiveTimelineKeyWithInterpolate
    {
        public int IsEnable;
        public int OverrideIgnoreLayer;
        public LayerMask OverrideLayerMask;
        public int BacksideOff;
        public float MirrorBallBacksideFeather;
        public int ProjectionToCharacterModelOnly;
        public int TextureId;
        public Color Color = Color.white;
        public Vector3 Position;
        public Vector3 Angle;
        public Vector3 Scale = Vector3.one;
        public int Orthographic;
        public float OrthographicSize = 10f;
        public float NearClipPlane = 0.1f;
        public float FarClipPlane = 100f;
        public float FieldOfView = 60f;
        public float ColorPower = 1f;
        public int LightBlendMode;
        public int CharacterAttach;
        public int CharacterAttachPosition;
        public string BlinkLightName;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower = 1f;
        public int IsAdjustedBlinkLightColor;
        public Vector3 MirrorBallRotateAxis = Vector3.up;
        public float MirrorBallRotateValue;
        public float MirrorBallProjectionRadius = 20f;
        public float MirrorBallFallOffPower = 2f;
        public int MirrorBallIsLoopRotation;
        public float MirrorBallLoopRotationSpeed;
        public Vector2 MirrorBallUVOffset;
        public Vector2 MirrorBallUVScale = Vector2.one;
        public int MirrorBallUseCubeMap;
        public LiveTimelineLightProjectionAnimationParam AnimationParam;
    }

    [Serializable] public class LiveTimelineKeyLightProjectionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyLightProjectionData> { }
    [Serializable]
    public class LiveTimelineLightProjectionData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyLightProjectionDataList keys;
        public int ContentType;
    }
}
