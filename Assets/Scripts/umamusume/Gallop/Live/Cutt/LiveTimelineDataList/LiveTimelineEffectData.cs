using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Native LiveTimelineKeyEffectData (0x1af61a0), including fields missing in master5.
    [Serializable]
    public class LiveTimelineKeyEffectData : LiveTimelineKeyWithInterpolate
    {
        public const int FLAG_LOOP = 0x40000;
        public const int FLAG_PLAY = 0x80000;
        public const int FLAG_CLEAR = 0x100000;
        public const int FLAG_TRANSPARENT_FX = 0x400000;
        public const int FLAG_COLOR_POWER_RGB = 0x800000;
        public const int FLAG_PARTICLE_PARAM = 0x1000000;
        public const int FLAG_CANCEL_OWNER_SCALE_X = 0x2000000;
        public const int FLAG_CANCEL_OWNER_SCALE_Y = 0x4000000;
        public const int FLAG_CANCEL_OWNER_SCALE_Z = 0x8000000;
        public const int FLAG_SIMULATE = 0x10000000;
        public const int FLAG_IGNORE_COLOR_CORRECTION = 0x20000000;
        public const int FLAG_TIMESCALE_DISABLED = 0x40000000;

        [Serializable]
        public class ParticleParam
        {
            public string ObjectName = string.Empty;
            public int ObjectNameHash;
            public bool IsEnabled = true;
            public float MainDuration = 1f;
            public bool MainLooping = true;
            public bool IsUpdateMainSimulationSpace = true;
            public bool IsSetAllMainSimulationSpace = true;
            public int MainSimulationSpace;
            public Color MainStartColorMin = Color.white;
            public Color MainStartColorMax = Color.white;
            public float EmissionRateOverTimeMultiplier = 1f;
            public Vector3 ShapeScale = Vector3.one;
            public bool IsUpdateRandomSeed;
            public bool IsSetAllRandomSeed = true;
            public int RandomSeed;
        }

        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Effect;
        public Color color = Color.white;
        public float colorPower = 1f;
        public int ColorProperty;
        public int owner = 18;
        public int occurrenceSpot = 9;
        public string ParentStageObjectName = string.Empty;
        public int ParentStageObjectNameHash { get; private set; }
        public bool IsAttachProps;
        public int PropsIndex;
        public Vector3 offset;
        public Vector3 offsetAngle;
        public Vector3 offsetScale = Vector3.one;
        public ParticleParam[] ParticleParamArray = Array.Empty<ParticleParam>();
        public string BlinkLightName = string.Empty;
        public int BlinkLightNameHash;
        public int BlinkLightContainerIndex;
        public float BlinkLightBrightnessPower = 1f;
        public bool IsAdjustedBlinkLightColor = true;
        public bool IsSyncBlinkLight;
        public bool IsLinkOwnerPositionX = true;
        public bool IsLinkOwnerPositionY = true;
        public bool IsLinkOwnerPositionZ = true;
        public bool IsLinkOwnerRotate = true;

        public bool IsLoop => HasAttribute(FLAG_LOOP);
        public bool IsPlay => HasAttribute(FLAG_PLAY);
        public bool IsClear => HasAttribute(FLAG_CLEAR);
        public bool IsTransparentFX => HasAttribute(FLAG_TRANSPARENT_FX);
        public bool IsColorPowerRGB => HasAttribute(FLAG_COLOR_POWER_RGB);
        public bool IsParticleParam => HasAttribute(FLAG_PARTICLE_PARAM);
        public bool IsSimulate => HasAttribute(FLAG_SIMULATE);
        public bool IsIgnoreColorCorrection => HasAttribute(FLAG_IGNORE_COLOR_CORRECTION);
        public bool IsTimescaleDisabled => HasAttribute(FLAG_TIMESCALE_DISABLED);
        public bool HasAttribute(int mask) => ((int)attribute & mask) != 0;

        public override void OnLoad(LiveTimelineControl control)
        {
            base.OnLoad(control);
            ParentStageObjectNameHash = FNVHash.Generate(ParentStageObjectName ?? string.Empty);
            BlinkLightNameHash = FNVHash.Generate(BlinkLightName ?? string.Empty);
            if (ParticleParamArray == null) return;
            for (int i = 0; i < ParticleParamArray.Length; i++)
                if (ParticleParamArray[i] != null)
                    ParticleParamArray[i].ObjectNameHash = FNVHash.Generate(ParticleParamArray[i].ObjectName ?? string.Empty);
        }
    }


    [Serializable]
    public class LiveTimelineKeyEffectDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyEffectData> { }

    [Serializable]
    public class LiveTimelineEffectData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyEffectDataList keys = new LiveTimelineKeyEffectDataList();
        [SerializeField] private bool _applyVariation;
        [SerializeField] private int _variationId;
        [SerializeField] private string _folder = string.Empty;
        [NonSerialized] public int updatedKeyFrame;
        public bool applyVariation => _applyVariation;
        public int variationId => _variationId;
        public string folder => _folder;
        public LiveTimelineEffectData() : base("Effect") { }
    }
}
