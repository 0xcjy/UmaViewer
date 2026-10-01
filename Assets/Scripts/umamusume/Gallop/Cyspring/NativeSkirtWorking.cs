using System.Runtime.InteropServices;
using UnityEngine;

namespace Gallop
{
    [StructLayout(LayoutKind.Explicit, Size = 0x58)]
    public struct NativeSkirtWorking
    {
        [FieldOffset(0x00)] public Vector3 SkirtRootPos;
        [FieldOffset(0x10)] public Vector3 SkirtInitChildPos;
        [FieldOffset(0x20)] public Vector3 SkirtInitNormal;
        [FieldOffset(0x30)] public Vector3 RotationAxis;

        [FieldOffset(0x40)] public int IsCheckRightKnee;
        [FieldOffset(0x44)] public int IsCheckLeftKnee;
        [FieldOffset(0x48)] public int IsCheckRightAnkle;
        [FieldOffset(0x4C)] public int IsCheckLeftAnkle;

        [FieldOffset(0x50)] public float Evaluation;
        [FieldOffset(0x54)] public float OffsetAngle;
    }
}