using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyMirrorReflectionData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType
        {
            get{return LiveTimelineKeyDataType.MirrorReflection;}
        }
        // Names below intentionally cover both official dummy names and the JSON field names seen in exported timeline data.
        public bool EnableKey;
        public bool EnableMirror;
        public bool EnableBgLayer;
        public bool Enable3dLayer;
        public bool IsToonMirror;
        public float MirrorReflectionRate = 1f;
        public LiveCharaPositionFlag TargetChara = LiveCharaPositionFlag.All;
        public bool EnableCharaHead;
        public LiveCharaPositionFlag TargetCharaHead = LiveCharaPositionFlag.All;

        // Exported JSON names
        public bool isValidMirror;
        public bool isMirror;
        public bool isBgMirror;
        public bool IsMirrorBg3d;
        public bool EnableCharacterMirrorExpandFaceBounds;
        public LiveCharaPositionFlag characterMirror = LiveCharaPositionFlag.All;
        public LiveCharaPositionFlag CharacterMirrorHead = LiveCharaPositionFlag.All;
        public LiveCharaPositionFlag CharacterMirrorExpandFaceBounds;
        public float mirrorReflectionRate = 1f;

        // Backing/export helper fields sometimes present in JSON
        public bool _isValidMirror;
        public int _mirror;
        public int _bgMirror;
        public LiveCharaPositionFlag _characterMirror;
        public float _mirrorReflectionRate = -1f;

        public bool GetIsValidMirror()
        {
            return isValidMirror || _isValidMirror || EnableKey;
        }

        public bool GetMirrorEnabled()
        {
            return isMirror || EnableMirror;
        }

        public bool GetBgMirrorEnabled()
        {
            return isBgMirror || EnableBgLayer;
        }

        public bool GetBg3dMirrorEnabled()
        {
            return IsMirrorBg3d || Enable3dLayer;
        }

        public float GetMirrorReflectionRate()
        {
            if (_mirrorReflectionRate >= 0f)
                return _mirrorReflectionRate;
            if (Mathf.Abs(mirrorReflectionRate) > 0f || Mathf.Approximately(MirrorReflectionRate, 0f))
                return mirrorReflectionRate;
            return MirrorReflectionRate;
        }

        public LiveCharaPositionFlag GetCharacterMirrorFlag()
        {
            if (characterMirror != 0)
                return characterMirror;
            if (_characterMirror != 0)
                return _characterMirror;
            return TargetChara;
        }

        public LiveCharaPositionFlag GetCharacterMirrorHeadFlag()
        {
            if (CharacterMirrorHead != 0)
                return CharacterMirrorHead;
            return TargetCharaHead;
        }

        public bool GetEnableCharacterMirrorHead()
        {
            return EnableCharaHead || CharacterMirrorHead != 0 || TargetCharaHead != 0;
        }
    }

    [Serializable]
    public class LiveTimelineKeyMirrorReflectionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMirrorReflectionData>
    {
    }

    [Serializable]
    public class LiveTimelineStageEnvironmentData : ILiveTimelineGroupDataWithName
    {
        private const string DEFAULT_NAME = "Environment";
        public LiveTimelineKeyMirrorReflectionDataList keys = new LiveTimelineKeyMirrorReflectionDataList();

        public LiveTimelineStageEnvironmentData()
        {
            if (string.IsNullOrEmpty(name))
                name = DEFAULT_NAME;
        }
    }
}
