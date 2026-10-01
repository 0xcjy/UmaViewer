using System.Runtime.InteropServices;
using UnityEngine;

namespace Gallop
{

    [StructLayout(LayoutKind.Explicit, Size = 0x4C)]
    public struct NativeClothCollision
    {
        [FieldOffset(0x00)] public Vector3 Position;
        [FieldOffset(0x10)] public Vector3 Position2;
        [FieldOffset(0x20)] public Vector3 Normal;

        [FieldOffset(0x30)] public int Type;

        // Official dummy calls this bool; native layout is one byte.
        [FieldOffset(0x34)] public int IsInner;

        [FieldOffset(0x38)] public float Radius;
        [FieldOffset(0x3C)] public float Distance;

        [FieldOffset(0x40)] public int ParentWorkIndex;
        [FieldOffset(0x44)] public int IsEnable;
        [FieldOffset(0x48)] public int IsCharaCollision;

        public bool IsInnerBool
        {
            readonly get => IsInner != 0;
            set => IsInner = value ? 1 : 0;
        }

        public bool IsEnableBool
        {
            readonly get => IsEnable != 0;
            set => IsEnable = value ? 1 : 0;
        }

        public bool IsCharaCollisionBool
        {
            readonly get => IsCharaCollision != 0;
            set => IsCharaCollision = value ? 1 : 0;
        }

        public static NativeClothCollision CreateDefault()
        {
            return new NativeClothCollision
            {
                Position = Vector3.zero,
                Position2 = Vector3.zero,
                Normal = Vector3.up,
                Type = 0,
                IsInner = 0,
                Radius = 0f,
                Distance = 0f,
                ParentWorkIndex = 0,
                IsEnable = 0,
                IsCharaCollision = 0
            };
        }
    }
}
