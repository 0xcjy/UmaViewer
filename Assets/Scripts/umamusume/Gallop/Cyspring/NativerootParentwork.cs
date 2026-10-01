using System.Runtime.InteropServices;
using UnityEngine;

namespace Gallop
{
    [StructLayout(LayoutKind.Explicit, Size = 0x20)]
    public struct NativeRootParentWork
    {
        [FieldOffset(0x00)] public Vector3 WorldPosition;
        [FieldOffset(0x10)] public Quaternion WorldRotation;
    }
}