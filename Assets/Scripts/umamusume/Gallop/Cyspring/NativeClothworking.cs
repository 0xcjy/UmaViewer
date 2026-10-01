using System.Runtime.InteropServices;
using UnityEngine;

namespace Gallop
{
    [StructLayout(LayoutKind.Explicit, Size = 0x168)]
    public struct NativeClothWorking
    {
        [FieldOffset(0x000)] public Quaternion InitLocalRotation;
        [FieldOffset(0x010)] public Quaternion ParentRotation;
        [FieldOffset(0x020)] public Quaternion FinalRotation;

        [FieldOffset(0x030)] public Vector3 BoneAxis;
        [FieldOffset(0x040)] public Vector3 TargetPosition;
        [FieldOffset(0x050)] public Vector3 PrevTargetPosition;
        [FieldOffset(0x060)] public Vector3 Force;
        [FieldOffset(0x070)] public Vector3 AimVector;
        [FieldOffset(0x080)] public Vector3 Diff;
        [FieldOffset(0x090)] public Vector3 SelfPosition;
        [FieldOffset(0x0A0)] public Vector3 LimitRotationMin;
        [FieldOffset(0x0B0)] public Vector3 LimitRotationMax;

        [FieldOffset(0x0C0)] public float InitBoneDistance;
        [FieldOffset(0x0C4)] public float StiffnessForce;
        [FieldOffset(0x0C8)] public float DragForce;
        [FieldOffset(0x0CC)] public float CollisionRadius;
        [FieldOffset(0x0D0)] public float Gravity;

        [FieldOffset(0x0D4)] public float VerticalWindRateSlow;
        [FieldOffset(0x0D8)] public float VerticalWindRateFast;
        [FieldOffset(0x0DC)] public float HorizontalWindRateSlow;
        [FieldOffset(0x0E0)] public float HorizontalWindRateFast;

        // dummy 里是 bool，但实际偏移按 4 字节对齐。
        // 这里用 int，保证 blittable，方便 GCHandle pinned array 直接传 DLL。
        [FieldOffset(0x0E4)] public int CheckCharaCollision;
        [FieldOffset(0x0E8)] public int IsSkip;
        [FieldOffset(0x0EC)] public int IsLimit;

        [FieldOffset(0x0F0)] public int ActiveCollision;

        [FieldOffset(0x0F4)] public short CIndex0;
        [FieldOffset(0x0F6)] public short CIndex1;
        [FieldOffset(0x0F8)] public short CIndex2;
        [FieldOffset(0x0FA)] public short CIndex3;
        [FieldOffset(0x0FC)] public short CIndex4;
        [FieldOffset(0x0FE)] public short CIndex5;
        [FieldOffset(0x100)] public short CIndex6;
        [FieldOffset(0x102)] public short CIndex7;

        [FieldOffset(0x104)] public float DynamicRatio;
        [FieldOffset(0x108)] public Quaternion AnimationRotation;

        [FieldOffset(0x118)] public Vector3 SkirtKneeNormal;
        [FieldOffset(0x128)] public Vector3 SkirtNormalPos;

        // dummy 里是 bool，偏移 0x138，后面 0x13C 是 float。
        [FieldOffset(0x138)] public int IsCheckSkirtKnee;

        [FieldOffset(0x13C)] public float MoveSpringApplyRate;
        [FieldOffset(0x140)] public int ParentWorkIndex;

        [FieldOffset(0x144)] public Vector3 ConnectedForce;

        [FieldOffset(0x154)] public short CIndex8;
        [FieldOffset(0x156)] public short CIndex9;
        [FieldOffset(0x158)] public short CIndex10;
        [FieldOffset(0x15A)] public short CIndex11;
        [FieldOffset(0x15C)] public short CIndex12;
        [FieldOffset(0x15E)] public short CIndex13;
        [FieldOffset(0x160)] public short CIndex14;
        [FieldOffset(0x162)] public short CIndex15;

        [FieldOffset(0x164)] public int IsAddSpring;
    }
}