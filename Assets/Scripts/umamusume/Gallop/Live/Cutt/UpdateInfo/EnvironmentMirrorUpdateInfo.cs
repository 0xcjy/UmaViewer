using System;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public struct EnvironmentMirrorUpdateInfo
    {
        public bool isValid;
        public bool mirror;
        public float mirrorReflectionRate;
        public bool bgMirror;
        public bool IsMirrorBg3d;
        public LiveCharaPositionFlag charaPositionFlag;
        public LiveCharaPositionFlag VisibleHeadFlag;
        public bool IsToonMirror;
        public bool IsEnabledCharacterMirrorHead;
        public bool EnableCharacterMirrorExpandFaceBounds;
        public LiveCharaPositionFlag CharacterMirrorExpandFaceBounds;
    }

    public delegate void EnvironmentMirrorDelegate(ref EnvironmentMirrorUpdateInfo updateInfo);
}
