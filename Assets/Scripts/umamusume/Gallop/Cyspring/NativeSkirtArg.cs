using System.Runtime.InteropServices;
using UnityEngine;

namespace Gallop
{
    [StructLayout(LayoutKind.Explicit, Size = 0x70)]
    public struct NativeSkirtArg
    {
        [FieldOffset(0x00)] public Vector3 KneeLPos;
        [FieldOffset(0x10)] public Vector3 KneeRPos;
        [FieldOffset(0x20)] public Vector3 AnkleLPos;
        [FieldOffset(0x30)] public Vector3 AnkleRPos;
        [FieldOffset(0x40)] public Vector3 CenterPos;
        [FieldOffset(0x50)] public Vector3 RootPos;

        [FieldOffset(0x60)] public float KneeColliderRadius;
        [FieldOffset(0x64)] public float AnkleColliderRadius;
        [FieldOffset(0x68)] public float InfluenceAngle;
        [FieldOffset(0x6C)] public float InfluenceMaxAngle;
    }
}