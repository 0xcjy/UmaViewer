using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct AnimationUpdateInfo
    {
        public float progressTime;
        public LiveTimelineAnimationData data;
        public int animationId;
        public WrapMode wrapMode;
        public float speed;
        public float offsetTime;
    }

    public delegate void AnimationUpdateInfoDelegate(ref AnimationUpdateInfo updateInfo);
}