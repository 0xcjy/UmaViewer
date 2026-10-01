using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public struct LaserUpdateInfo
    {
        public int timelineIndex;
        public int ProgressFrame;
        public float ProgressTime;
        public Vector3 objectPosition;
        public Quaternion objectRotation;
        public Vector3 objectScale;
        public bool isEnabledRender;
        public LaserFormation formation;
        public Quaternion rotation;
        public float degRootYaw;
        public float degLaserPitch;
        public float posInterval;
        public LaserBlink blink;
        public float blinkPeroid;
        public bool IsDisabledRootLight;
        public bool IsEnabledRaycast;
        public float RaycastDistance;
    }

    public delegate void LaserUpdateInfoDelegate( ref LaserUpdateInfo updateInfo);
}