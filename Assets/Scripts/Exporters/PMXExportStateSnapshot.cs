using System;
using Gallop;
using UnityEngine;

/// <summary>
/// 冻结 PMX 导出瞬间的角色姿势，并在导出结束后恢复运行时状态。
/// </summary>
internal sealed class PMXExportStateSnapshot : IDisposable
{
    private const float MmdAPoseArmAngle = 38.5f;

    private readonly UmaContainerCharacter _container;
    private readonly TransformState[] _transforms;
    private readonly MorphState[] _morphs;
    private readonly BindPropertyState[] _bindProperties;
    private readonly FaceOverrideData _faceOverrideData;
    private readonly bool _bodyAnimatorEnabled;
    private readonly bool _faceAnimatorEnabled;
    private readonly bool _physicsEnabled;
    private readonly bool _eyeTrackingEnabled;
    private readonly bool _animatorControlEnabled;
    private bool _disposed;

    private struct TransformState
    {
        internal Transform Transform;
        internal Vector3 LocalPosition;
        internal Quaternion LocalRotation;
        internal Vector3 LocalScale;
    }

    private struct MorphState
    {
        internal FacialMorph Morph;
        internal float Weight;
        internal OverrideType OverrideType;
        internal float OverrideWeight;
    }

    private struct BindPropertyState
    {
        internal BindProperty Property;
        internal float Value;
    }

    internal PMXExportStateSnapshot(UmaContainerCharacter container)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _bodyAnimatorEnabled = container.UmaAnimator != null && container.UmaAnimator.enabled;
        _faceAnimatorEnabled = container.UmaFaceAnimator != null && container.UmaFaceAnimator.enabled;
        _physicsEnabled = container.EnablePhysics;
        _eyeTrackingEnabled = container.EnableEyeTracking;
        _animatorControlEnabled = container.isAnimatorControl;
        _faceOverrideData = container.FaceOverrideData;

        Transform[] transforms = container.GetComponentsInChildren<Transform>(true);
        _transforms = new TransformState[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            _transforms[i] = new TransformState
            {
                Transform = transform,
                LocalPosition = transform.localPosition,
                LocalRotation = transform.localRotation,
                LocalScale = transform.localScale
            };
        }

        if (container.FaceDrivenKeyTarget != null)
        {
            var allMorphs = container.FaceDrivenKeyTarget.AllMorphs;
            _morphs = new MorphState[allMorphs.Count];
            for (int i = 0; i < allMorphs.Count; i++)
            {
                FacialMorph morph = allMorphs[i];
                _morphs[i] = new MorphState
                {
                    Morph = morph,
                    Weight = morph.weight,
                    OverrideType = morph.OverrideType,
                    OverrideWeight = morph.overrideWeight
                };
            }

            int propertyCount = 0;
            foreach (FacialOtherMorph morph in container.FaceDrivenKeyTarget.OtherMorphs)
            {
                propertyCount += morph.BindProperties.Count;
            }

            _bindProperties = new BindPropertyState[propertyCount];
            int propertyIndex = 0;
            foreach (FacialOtherMorph morph in container.FaceDrivenKeyTarget.OtherMorphs)
            {
                foreach (BindProperty property in morph.BindProperties)
                {
                    _bindProperties[propertyIndex++] = new BindPropertyState
                    {
                        Property = property,
                        Value = property.Value
                    };
                }
            }
        }
        else
        {
            _morphs = new MorphState[0];
            _bindProperties = new BindPropertyState[0];
        }

    }

    internal void FreezeForExport()
    {
        // 冻结放在导出 try 块内，任一步骤异常时都能进入 Dispose 恢复快照。
        if (_container.UmaAnimator != null) _container.UmaAnimator.enabled = false;
        if (_container.UmaFaceAnimator != null) _container.UmaFaceAnimator.enabled = false;
        _container.isAnimatorControl = false;
        _container.EnableEyeTracking = false;
        _container.SetDynamicBoneEnable(false);
        // 迷你角色会跳过 SetDynamicBoneEnable，仍需显式关闭运行时物理。
        _container.EnablePhysics = false;
    }

    internal void ApplyMmdReferencePose()
    {
        Transform leftArm = FindTransform("Arm_L");
        Transform rightArm = FindTransform("Arm_R");
        if (_container.InitBoneTransform == null || leftArm == null || rightArm == null)
        {
            throw new InvalidOperationException("无法生成 MMD A-pose：角色缺少初始骨骼数据或左右上臂骨骼。");
        }

        // 先回到角色初始化姿势，再按历史 VMD 导出基准将双臂各下压 38.5 度。
        _container.ResetBodyPose();
        _container.UpBodyReset();
        leftArm.Rotate(0f, 0f, -MmdAPoseArmAngle, Space.Self);
        rightArm.Rotate(0f, 0f, MmdAPoseArmAngle, Space.Self);
    }

    private Transform FindTransform(string name)
    {
        for (int i = 0; i < _transforms.Length; i++)
        {
            Transform transform = _transforms[i].Transform;
            if (transform != null && transform.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return transform;
            }
        }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            RestoreFacialState();
        }
        finally
        {
            try
            {
                RestoreTransforms();
            }
            finally
            {
                RestoreRuntimeComponents();
            }
        }
    }

    private void RestoreTransforms()
    {
        // 面部形态恢复会重新计算局部变换，随后用快照精确恢复完整层级。
        for (int i = 0; i < _transforms.Length; i++)
        {
            TransformState state = _transforms[i];
            if (state.Transform == null) continue;
            state.Transform.localPosition = state.LocalPosition;
            state.Transform.localRotation = state.LocalRotation;
            state.Transform.localScale = state.LocalScale;
        }
    }

    private void RestoreRuntimeComponents()
    {
        _container.isAnimatorControl = _animatorControlEnabled;
        _container.EnableEyeTracking = _eyeTrackingEnabled;
        _container.SetDynamicBoneEnable(_physicsEnabled);
        // 与冻结路径对称，确保迷你角色也恢复原始物理标记。
        _container.EnablePhysics = _physicsEnabled;
        if (_container.UmaFaceAnimator != null) _container.UmaFaceAnimator.enabled = _faceAnimatorEnabled;
        if (_container.UmaAnimator != null) _container.UmaAnimator.enabled = _bodyAnimatorEnabled;
    }

    private void RestoreFacialState()
    {
        _container.FaceOverrideData = _faceOverrideData;
        for (int i = 0; i < _morphs.Length; i++)
        {
            MorphState state = _morphs[i];
            state.Morph.weight = state.Weight;
            state.Morph.OverrideType = state.OverrideType;
            state.Morph.overrideWeight = state.OverrideWeight;
        }
        for (int i = 0; i < _bindProperties.Length; i++)
        {
            BindPropertyState state = _bindProperties[i];
            state.Property.Value = state.Value;
        }

        // 恢复材质、显隐和泪光等非 Transform 表情副作用。
        _container.FaceDrivenKeyTarget?.ChangeMorph();

        // ChangeMorph 可能重算覆盖字段，数据层仍应保持导出前的原值。
        for (int i = 0; i < _morphs.Length; i++)
        {
            MorphState state = _morphs[i];
            state.Morph.OverrideType = state.OverrideType;
            state.Morph.overrideWeight = state.OverrideWeight;
        }
    }
}
