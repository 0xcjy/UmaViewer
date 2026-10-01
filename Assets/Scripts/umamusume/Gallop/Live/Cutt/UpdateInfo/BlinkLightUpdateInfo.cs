using System.Runtime.InteropServices;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [StructLayout(LayoutKind.Sequential)]
    public struct BlinkLightUpdateInfo
    {
        // +0x00
        public float progressTime;

        // +0x04
        public int keyIndex;

        // +0x08
        public LiveDefine.LightBlendMode LightBlendMode;

        // +0x10
        public Color[] color0Array;

        // +0x18
        public Color[] color1Array;

        // +0x20
        public float[] powerArray;

        // +0x28
        public bool[] isReverseHueArray;

        // +0x30
        public BlinkLightPattern pattern;

        // +0x34
        public BlinkLightColorType colorType;

        // +0x38
        public float powerMin;

        // +0x3C
        public float powerMax;

        // +0x40
        public int loopCount;

        // +0x44
        public float waitTime;

        // +0x48
        public float turnOnTime;

        // +0x4C
        public float turnOffTime;

        // +0x50
        public float keepTime;

        // +0x54
        public float intervalTime;

        // +0x58
        public bool UseWashLightBlendMode;
    }

    // 官方 UpdateInfo 本身不带 data/rootName/currentLiveTime，
    // 所以uma viewer的timeline event需要把root数据作为额外参数传给driver
    public delegate void BlinkLightUpdateInfoDelegate(
        LiveTimelineBlinkLightData data,
        ref BlinkLightUpdateInfo updateInfo,
        float currentLiveTime
    );
}