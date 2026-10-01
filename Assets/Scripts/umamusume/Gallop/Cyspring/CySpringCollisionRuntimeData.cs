using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    /// <summary>
    /// Runtime collision data used by CySpring.
    ///
    /// Reconstructed from CySpringCollisionRuntimeData 
    /// Important native layout confirmed from IDA:
    /// NativeClothCollision:
    ///   +0x00 Vector3 Position
    ///   +0x10 Vector3 Position2 / CapsuleEnd
    ///   +0x20 Vector3 Normal / Forward
    ///   +0x30 int Type
    ///   +0x34 bool IsInner
    ///   +0x38 float Radius
    ///   +0x3C float Distance
    ///   +0x40 int ParentWorkIndex
    /// </summary>
    public class CySpringCollisionRuntimeData
    {
        private float _radius;
        private Vector3 _offset;
        private Vector3 _offset2;

        private float _defaultRadius;
        private Vector3 _defaultOffset;
        private Vector3 _defaultOffset2;

        private Transform _transform;

        public bool IsWorld;

        public float Radius
        {
            get => _radius;
            set => _radius = value;
        }

        public CySpringCollisionData.CollisionType CollisionType { get; set; }

        public bool IsInner { get; set; }

        

        public Vector3 Offset
        {
            get => _offset;
            set => _offset = value;
        }

        public Vector3 Offset2
        {
            get => _offset2;
            set => _offset2 = value;
        }

        public float Distance => _transform != null ? GetDistance(_transform.position, _transform.forward) : 0.0f;

        public Vector3 Normal => _transform != null ? _transform.forward : Vector3.forward;

        public Vector3 CapsuleAxis => GetScaledCapsuleAxis();

        private Vector3 CapsuleOffset => _offset2 - _offset;

        public bool FilterSpringNode { get; set; }

        public bool FilterSpringNodeExclusive { get; set; }

        public HashSet<CySpringBoneBase> FilterSpringHashSet { get; set; }

        public Transform TargetTransform => _transform;

        public string Name { get; set; }

        public bool ShouldApplyToBone(CySpringBoneBase bone)
        {
            // Official env-filter logic:
            // if (!FilterSpringNode) apply;
            // else apply when HashSet.Contains(bone) != FilterSpringNodeExclusive.
            if (!FilterSpringNode)
                return true;

            bool contains = FilterSpringHashSet != null && bone != null && FilterSpringHashSet.Contains(bone);
            return contains != FilterSpringNodeExclusive;
        }

        public void Initialize(Transform transform)
        {
            _transform = transform;

            if (_transform != null)
                Name = _transform.name;

            _defaultRadius = _radius;
            _defaultOffset = _offset;
            _defaultOffset2 = _offset2;
        }

        public void SetTransform(float distance, Vector3 normal)
        {
            // Official Plane setup:
            //   position = normal * distance
            //   rotation = Quaternion.FromToRotation(Vector3.up, normal)
            //   transform.SetPositionAndRotation(position, rotation)
            if (_transform == null)
                throw new NullReferenceException();

            Vector3 position = normal * distance;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
            _transform.SetPositionAndRotation(position, rotation);
        }

        private void UpdateNativeCollisionCommonParam(ref NativeClothCollision nativeCollision)
        {
            nativeCollision.Radius = _radius;
            nativeCollision.Type = (int)CollisionType;
            nativeCollision.IsInnerBool = IsInner;
        }

        public void UpdateNativeCollision(
    ref NativeClothCollision nativeCollision,
    ref Vector3 characterPosition,
    NativeRootParentWork[] rootParentWorkArray,
    bool isUpdateScale = false,
    float legacyScale = 1.0f)
        {
            UpdateNativeCollisionCommonParam(ref nativeCollision);


            switch (CollisionType)
            {
                case CySpringCollisionData.CollisionType.Sphere:
                    {
                        nativeCollision.Position = GetSpherePosition();
                        break;
                    }

                case CySpringCollisionData.CollisionType.Capsule:
                    {
                        Vector3 spherePosition = GetSpherePosition();
                        nativeCollision.Position = spherePosition;

                        if (isUpdateScale)
                            nativeCollision.Position2 = spherePosition + CapsuleOffset * legacyScale;
                        else
                            nativeCollision.Position2 = spherePosition + CapsuleAxis;

                        break;
                    }

                case CySpringCollisionData.CollisionType.Plane:
                    {
                        if (rootParentWorkArray == null)
                            return;

                        int parentWorkIndex = nativeCollision.ParentWorkIndex;
                        if ((uint)parentWorkIndex >= (uint)rootParentWorkArray.Length)
                            return;

                        Vector3 parentPos = rootParentWorkArray[parentWorkIndex].WorldPosition;
                        Vector3 forward = _transform != null ? _transform.forward : Vector3.forward;

                        nativeCollision.Distance = Vector3.Dot(parentPos - characterPosition, forward);
                        nativeCollision.Normal = forward;
                        break;
                    }

                case CySpringCollisionData.CollisionType.None:
                default:
                    {
                        break;
                    }
            }
        }

        private Vector3 GetSpherePosition()
        {
            // Official:
            // parentLossyScale = transform.parent.lossyScale
            // return Vector3.Scale(transform.localPosition, parentLossyScale)
            if (_transform == null || _transform.parent == null)
                throw new NullReferenceException();

            Vector3 parentScale = _transform.parent.lossyScale;
            Vector3 localPosition = _transform.localPosition;
            return new Vector3(
                parentScale.x * localPosition.x,
                parentScale.y * localPosition.y,
                parentScale.z * localPosition.z);
        }

        private Vector3 GetSpherePositionForEnv(
            in NativeClothCollision nativeCollision,
            in Vector3 characterPosition,
            NativeRootParentWork[] rootParentWorkArray)
        {
            // 官方:
            // if IsWorld:
            //   parentRot = rootParentWorkArray[nativeCollision.ParentWorkIndex + 1].WorldRotation
            //   return inverse(parentRot) * (-characterPosition)
            // else:
            //   return Vector3.zero
            if (IsWorld)
            {
                if (rootParentWorkArray == null)
                    throw new NullReferenceException();

                int arrayIndex = nativeCollision.ParentWorkIndex + 1;
                if ((uint)arrayIndex >= (uint)rootParentWorkArray.Length)
                    throw new IndexOutOfRangeException();

                Quaternion parentRotation = rootParentWorkArray[arrayIndex].WorldRotation;
                return Quaternion.Inverse(parentRotation) * (-characterPosition);
            }

            return Vector3.zero;
        }

        private void UpdateNativeCollisionEnv(
            ref NativeClothCollision nativeCollision,
            ref Vector3 characterPosition,
            NativeRootParentWork[] rootParentWorkArray)
        {
            switch (CollisionType)
            {
                case CySpringCollisionData.CollisionType.Sphere:
                {
                    UpdateNativeCollisionCommonParam(ref nativeCollision);
                    nativeCollision.Position = GetSpherePositionForEnv(
                        in nativeCollision,
                        in characterPosition,
                        rootParentWorkArray);
                    break;
                }

                case CySpringCollisionData.CollisionType.Capsule:
                {
                    UpdateNativeCollisionCommonParam(ref nativeCollision);
                    Vector3 spherePosition = GetSpherePositionForEnv(
                        in nativeCollision,
                        in characterPosition,
                        rootParentWorkArray);
                    nativeCollision.Position = spherePosition;

                    if (_transform == null || _transform.parent == null)
                        throw new NullReferenceException();

                    Vector3 parentScale = _transform.parent.lossyScale;
                    Vector3 capsuleOffset = _offset2 - _offset;
                    nativeCollision.Position2 = spherePosition + new Vector3(
                        capsuleOffset.x * parentScale.x,
                        capsuleOffset.y * parentScale.y,
                        capsuleOffset.z * parentScale.z);
                    break;
                }

                default:
                {
                    // Official fallback for non sphere/capsule env:
                    // UpdateNativeCollision(..., isUpdateScale=false, legacyScale=1.0f)
                    UpdateNativeCollision(
                        ref nativeCollision,
                        ref characterPosition,
                        rootParentWorkArray,
                        false,
                        1.0f);
                    break;
                }
            }
        }

        public void UpdateNativeCollisionForEnv(
            ref NativeClothCollision nativeCollision,
            ref Vector3 characterPosition,
            NativeRootParentWork[] rootParentWorkArray)
        {
            UpdateNativeCollisionEnv(ref nativeCollision, ref characterPosition, rootParentWorkArray);
        }

        public void ScaleParams(float legacyScale)
        {
            if (Gallop.Math.IsFloatEqualLight(legacyScale, 0.0f))
                legacyScale = 1.0f;

            if (_transform == null)
                throw new NullReferenceException();

            float lossyScaleY = _transform.lossyScale.y;

            if (CollisionType == CySpringCollisionData.CollisionType.Capsule)
            {
                ScaleParams_Capsule(lossyScaleY, legacyScale);
                return;
            }

            if (CollisionType == CySpringCollisionData.CollisionType.Sphere)
            {
                ScaleParams_Sphere(lossyScaleY, legacyScale);
                return;
            }

    // 官方逻辑：
    // CollisionType == None / Plane 时，只取了 lossyScale，然后直接 return。
    // 不做 _radius / _offset / _offset2 缩放，也不改 localPosition。
}

        private void ScaleParams_Sphere(float lossyScale, float legacyScale)
        {
            float scale = lossyScale / legacyScale;

            _radius = _defaultRadius * scale;
            _offset = _defaultOffset * scale;

            if (_transform == null)
                throw new NullReferenceException();

            // Official sphere path restores transform.localPosition to defaultOffset / legacyScale.
            _transform.localPosition = _defaultOffset / legacyScale;
        }

        private void ScaleParams_Capsule(float lossyScale, float legacyScale)
        {
            float scale = lossyScale / legacyScale;

            _radius = _defaultRadius * scale;
            _offset = _defaultOffset * scale;
            _offset2 = _defaultOffset2 * scale;

            if (_transform == null)
                throw new NullReferenceException();

            _transform.localPosition = _defaultOffset;
        }

        private static void ScaleParams_Plane()
        {
            // Official is empty.
        }

        private static float GetDistance(Vector3 position, Vector3 forward)
        {
            return Vector3.Dot(position, forward);
        }

        private Vector3 GetScaledCapsuleAxis()
        {
            // Official:
            // parentLossyScale = transform.parent.lossyScale
            // return Vector3.Scale(Offset2 - Offset, parentLossyScale)
            if (_transform == null || _transform.parent == null)
                throw new NullReferenceException();

            Vector3 parentScale = _transform.parent.lossyScale;
            Vector3 axis = _offset2 - _offset;
            return new Vector3(
                axis.x * parentScale.x,
                axis.y * parentScale.y,
                axis.z * parentScale.z);
        }

        public CySpringCollisionRuntimeData()
        {
            _radius = 0.0f;
            _offset = Vector3.zero;
            _offset2 = Vector3.zero;
            _defaultRadius = 0.0f;
            _defaultOffset = Vector3.zero;
            _defaultOffset2 = Vector3.zero;
            _transform = null;
            IsWorld = false;
            CollisionType = CySpringCollisionData.CollisionType.Sphere;
            IsInner = false;
            FilterSpringNode = false;
            FilterSpringNodeExclusive = false;
            FilterSpringHashSet = null;
            Name = null;
        }
    }
}
