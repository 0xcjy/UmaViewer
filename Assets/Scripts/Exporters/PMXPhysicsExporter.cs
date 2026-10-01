using Gallop;
using LibMMD.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 将 CySpring 头发、耳朵、尾巴等物理骨骼链转换为 PMX 2.0 原生刚体与 Joint。
/// 严格基于骨骼局部坐标空间计算刚体姿态，消除全局增量误差与自旋偏差。
/// </summary>
internal static class PMXPhysicsExporter
{
    // 碰撞组定义：使用独立组并关闭组内碰撞，避免受 Joint 约束的相邻刚体互相挤压
    internal const int DynamicCollisionGroup = 3;
    internal const int TailBodyCollisionGroup = 2;
    internal const int SkirtCollisionGroup = 4;
    internal const int SkirtLegCollisionGroup = 5;

    internal const float DefaultRadius = 0.018f;
    internal const float MinimumRadius = 0.006f;
    internal const float MaximumRadius = 0.045f;
    internal const float MinimumSegmentLength = 0.004f;

    // 尾巴摆幅与弹簧参数
    private const float TailFreeBendDegrees = 75f;
    private const float TailFreeTwistDegrees = 30f;
    private const float TailRootBendSpring = 12f;
    private const float TailTipBendSpring = 4f;
    private const float TailRootTwistSpring = 4f;
    private const float TailTipTwistSpring = 1.5f;
    private const float TailColliderClearanceMultiplier = 1.5f;
    private const float TailColliderMinimumRadiusMultiplier = 1.25f;
    private const float TailColliderMaximumRadius = 0.12f;

    // 普通发束物理预设
    internal static readonly PhysicsPreset DefaultPreset = new PhysicsPreset
    {
        RootMass = 0.8f,
        TipMass = 0.25f,
        RootTranslateDamp = 0.68f,
        TipTranslateDamp = 0.88f,
        RootRotateDamp = 0.76f,
        TipRotateDamp = 0.92f,
        BendLimitDegrees = 18f,
        TwistLimitDegrees = 8f,
        BendSpring = 12f,
        TwistSpring = 5f
    };

    // 耳朵物理预设：硬附属骨，限制小角度快速回正
    internal static readonly PhysicsPreset EarPreset = new PhysicsPreset
    {
        RootMass = 0.65f,
        TipMass = 0.3f,
        RootTranslateDamp = 0.82f,
        TipTranslateDamp = 0.94f,
        RootRotateDamp = 0.88f,
        TipRotateDamp = 0.96f,
        BendLimitDegrees = 9f,
        TwistLimitDegrees = 4.5f,
        BendSpring = 20f,
        TwistSpring = 9f
    };

    // 尾巴物理预设
    internal static readonly PhysicsPreset TailPreset = new PhysicsPreset
    {
        RootMass = 0.9f,
        TipMass = 0.28f,
        RootTranslateDamp = 0.72f,
        TipTranslateDamp = 0.86f,
        RootRotateDamp = 0.72f,
        TipRotateDamp = 0.88f,
        BendLimitDegrees = TailFreeBendDegrees,
        TwistLimitDegrees = TailFreeTwistDegrees,
        BendSpring = TailRootBendSpring,
        TwistSpring = TailRootTwistSpring
    };

    // 裙摆物理预设
    internal static readonly PhysicsPreset SkirtPreset = new PhysicsPreset
    {
        RootMass = 0.7f,
        TipMass = 0.3f,
        RootTranslateDamp = 0.82f,
        TipTranslateDamp = 0.92f,
        RootRotateDamp = 0.86f,
        TipRotateDamp = 0.95f,
        BendLimitDegrees = 16f,
        TwistLimitDegrees = 6f,
        BendSpring = 22f,
        TwistSpring = 9f
    };

    internal sealed class PhysicsPreset
    {
        internal float RootMass;
        internal float TipMass;
        internal float RootTranslateDamp;
        internal float TipTranslateDamp;
        internal float RootRotateDamp;
        internal float TipRotateDamp;
        internal float BendLimitDegrees;
        internal float TwistLimitDegrees;
        internal float BendSpring;
        internal float TwistSpring;
    }

    internal sealed class Context
    {
        internal readonly List<Chain> Chains = new List<Chain>();
        internal readonly List<SkirtColumn> SkirtColumns = new List<SkirtColumn>();
        internal readonly HashSet<Transform> DynamicBones = new HashSet<Transform>();
        internal SkirtController SkirtController;
    }

    internal sealed class Chain
    {
        internal Transform Root;
        internal bool IsEar;
        internal bool IsTail;
        internal readonly HashSet<Transform> Bones = new HashSet<Transform>();
        internal readonly Dictionary<Transform, float> Radii = new Dictionary<Transform, float>();
    }

    internal sealed class SkirtColumn
    {
        internal Chain Chain;
        internal bool IsCheckRightLeg;
        internal bool IsCheckLeftLeg;
    }

    /// <summary>
    /// 解析角色中的物理链与裙摆数据。
    /// </summary>
    internal static Context Prepare(UmaContainerCharacter character, Transform skeletonRoot)
    {
        Context context = new Context();
        if (character == null || skeletonRoot == null || character.cySpringDataContainers == null)
            return context;

        Transform[] hierarchy = skeletonRoot.GetComponentsInChildren<Transform>(true);
        HashSet<Transform> claimedBones = new HashSet<Transform>();
        context.SkirtController = character.GetComponent<SkirtController>() ??
                                  character.GetComponentInChildren<SkirtController>(true);

        // 优先收集裙摆网格列
        PMXSkirtPhysicsExporter.CollectSkirtColumns(character, skeletonRoot, context, claimedBones);

        // 遍历 CySpring 容器收集头发、耳朵、尾巴链
        foreach (CySpringDataContainer container in character.cySpringDataContainers)
        {
            if (container == null || container.springParam == null) continue;

            foreach (CySpringParamDataElement element in container.springParam)
            {
                if (!IsLinearPhysicsElement(container, element)) continue;

                // 若该骨骼已被上游链或裙摆占用，跳过以避免生成重复重叠子链
                Transform root = FindRootTransform(element._boneName, hierarchy);
                if (root == null || claimedBones.Contains(root)) continue;

                Chain chain = BuildChain(element, root, claimedBones);
                if (chain == null || chain.Bones.Count == 0) continue;

                claimedBones.UnionWith(chain.Bones);
                context.Chains.Add(chain);
                context.DynamicBones.UnionWith(chain.Bones);
            }
        }

        return context;
    }

    /// <summary>
    /// 构建所有 PMX 刚体与 Joint。
    /// </summary>
    internal static void Build(Context context, Transform coordinateRoot, PMXBoneExporter.Result boneResult,
        RawMMDModel model)
    {
        List<MMDRigidBody> rigidBodies = new List<MMDRigidBody>();
        List<MMDJoint> joints = new List<MMDJoint>();
        Dictionary<Transform, int> dynamicRigidIndexes = new Dictionary<Transform, int>();

        foreach (Chain chain in context.Chains)
        {
            BuildChainPhysics(chain, coordinateRoot, boneResult, rigidBodies, joints, dynamicRigidIndexes);
        }

        PMXSkirtPhysicsExporter.BuildSkirtPhysics(
            context, coordinateRoot, boneResult, rigidBodies, joints, dynamicRigidIndexes);

        Validate(rigidBodies, joints, boneResult.Bones.Length);
        model.Rigidbodies = rigidBodies.ToArray();
        model.Joints = joints.ToArray();
    }

    private static Transform FindRootTransform(string boneName, Transform[] hierarchy)
    {
        if (string.IsNullOrEmpty(boneName)) return null;
        return hierarchy.FirstOrDefault(t => string.Equals(t.name, boneName, StringComparison.Ordinal));
    }

    private static Chain BuildChain(CySpringParamDataElement element, Transform root, HashSet<Transform> claimedBones)
    {
        if (element == null || root == null) return null;

        Dictionary<string, float> configuredBones = new Dictionary<string, float>(StringComparer.Ordinal);
        configuredBones[element._boneName] = SanitizeRadius(element._collisionRadius);
        if (element._childElements != null)
        {
            foreach (CySpringParamDataChildElement child in element._childElements)
            {
                if (child != null && !string.IsNullOrEmpty(child._boneName))
                    configuredBones[child._boneName] = SanitizeRadius(child._collisionRadius);
            }
        }

        Chain chain = new Chain
        {
            Root = root,
            IsEar = configuredBones.Keys.Any(IsEarBoneName),
            IsTail = configuredBones.Keys.Any(IsTailBoneName)
        };

        CollectContinuousBones(root, configuredBones, chain, claimedBones);
        return chain.Bones.Count > 0 ? chain : null;
    }

    internal static void CollectContinuousBones(Transform bone, Dictionary<string, float> configuredBones,
        Chain chain, HashSet<Transform> claimedBones = null)
    {
        if (!configuredBones.TryGetValue(bone.name, out float radius)) return;
        if (claimedBones != null && claimedBones.Contains(bone)) return;

        chain.Bones.Add(bone);
        chain.Radii[bone] = radius;
        for (int i = 0; i < bone.childCount; i++)
        {
            Transform child = bone.GetChild(i);
            if (configuredBones.ContainsKey(child.name))
                CollectContinuousBones(child, configuredBones, chain, claimedBones);
        }
    }

    private static void BuildChainPhysics(Chain chain, Transform coordinateRoot, PMXBoneExporter.Result boneResult,
        List<MMDRigidBody> rigidBodies, List<MMDJoint> joints,
        Dictionary<Transform, int> dynamicRigidIndexes)
    {
        if (!boneResult.BoneIndexes.ContainsKey(chain.Root)) return;

        PhysicsPreset preset = chain.IsEar ? EarPreset : chain.IsTail ? TailPreset : DefaultPreset;
        Transform anchorBone = FindNearestExportedParentTransform(chain.Root.parent, boneResult.BoneIndexes);
        int anchorBoneIndex = anchorBone != null ? boneResult.BoneIndexes[anchorBone] : 0;
        int anchorIndex = rigidBodies.Count;

        // 提取发根相对于模型基准坐标系的旋转
        Quaternion rootBoneWorldRot = Quaternion.Inverse(coordinateRoot.rotation) * chain.Root.rotation;
        Transform firstChild = GetChainChildren(chain.Root, chain.Bones).FirstOrDefault();
        Vector3 localDirToChild = firstChild != null
            ? chain.Root.InverseTransformPoint(firstChild.position)
            : Vector3.down;
        if (localDirToChild.sqrMagnitude < 0.000001f) localDirToChild = Vector3.down;

        Quaternion anchorRotation = rootBoneWorldRot * Quaternion.FromToRotation(Vector3.up, localDirToChild.normalized);
        rigidBodies.Add(CreateAnchorBody(chain, coordinateRoot, anchorBoneIndex, ConvertUnityRotationToWriterEuler(anchorRotation)));

        if (chain.IsTail)
        {
            MMDRigidBody bodyCollider = CreateTailBodyCollider(chain, coordinateRoot, anchorBone, anchorBoneIndex);
            if (bodyCollider != null) rigidBodies.Add(bodyCollider);
        }

        List<Transform> orderedBones = chain.Bones.OrderBy(GetDepth).ToList();
        int maximumDepth = orderedBones.Count > 0 ? orderedBones.Max(GetDepth) - GetDepth(chain.Root) : 0;
        float jointBendLimit = preset.BendLimitDegrees;
        float jointTwistLimit = preset.TwistLimitDegrees;

        foreach (Transform bone in orderedBones)
        {
            if (!boneResult.BoneIndexes.TryGetValue(bone, out int boneIndex) || dynamicRigidIndexes.ContainsKey(bone))
                continue;

            List<Transform> children = GetChainChildren(bone, chain.Bones);
            Transform singleChild = children.Count > 0 ? children[0] : null;

            float depthRatio = maximumDepth > 0
                ? Mathf.Clamp01((GetDepth(bone) - GetDepth(chain.Root)) / (float)maximumDepth)
                : 1f;
            int rigidIndex = rigidBodies.Count;

            // 严格基于骨骼自身局部空间计算胶囊体刚体朝向与中心
            MMDRigidBody dynamicBody = CreateDynamicBodyLocalSpace(
                bone, singleChild, chain.Radii[bone], depthRatio, coordinateRoot, boneIndex, preset, chain.Bones);
            rigidBodies.Add(dynamicBody);
            dynamicRigidIndexes[bone] = rigidIndex;

            int parentRigidIndex = (bone == chain.Root)
                ? anchorIndex
                : FindParentRigidIndex(bone.parent, chain.Bones, dynamicRigidIndexes, anchorIndex);

            float bendSpring = chain.IsTail
                ? Mathf.Lerp(TailRootBendSpring, TailTipBendSpring, depthRatio)
                : preset.BendSpring;
            float twistSpring = chain.IsTail
                ? Mathf.Lerp(TailRootTwistSpring, TailTipTwistSpring, depthRatio)
                : preset.TwistSpring;

            // Joint 锚点严格对齐骨骼节点位置，旋转与刚体局部空间物理姿态精确同步
            joints.Add(CreateJoint(
                bone, coordinateRoot, parentRigidIndex, rigidIndex,
                dynamicBody.Rotation, jointBendLimit, jointTwistLimit, bendSpring, twistSpring));
        }
    }

    /// <summary>
    /// 严格基于骨骼本地坐标系构建刚体，消除跨骨段增量误差，并支持末端叶骨切线平滑延伸。
    /// </summary>
    private static MMDRigidBody CreateDynamicBodyLocalSpace(Transform bone, Transform child, float configuredRadius,
        float depthRatio, Transform coordinateRoot, int boneIndex, PhysicsPreset preset, HashSet<Transform> chainBones)
    {
        Vector3 start = coordinateRoot.InverseTransformPoint(bone.position);
        Quaternion boneRotInCoord = Quaternion.Inverse(coordinateRoot.rotation) * bone.rotation;

        if (child != null)
        {
            // 有子骨段：在骨骼局部空间求出指向子骨的局部方向
            Vector3 localChildPos = bone.InverseTransformPoint(child.position);
            float length = localChildPos.magnitude;
            Vector3 localDir = length > 0.000001f ? localChildPos / length : Vector3.down;

            Quaternion localAlign = Quaternion.FromToRotation(Vector3.up, localDir);
            Quaternion capsuleRot = boneRotInCoord * localAlign;

            Vector3 position = coordinateRoot.InverseTransformPoint(bone.TransformPoint(localChildPos * 0.5f));
            float radius = Mathf.Clamp(configuredRadius, MinimumRadius, MaximumRadius);
            if (length > MinimumSegmentLength)
                radius = Mathf.Min(radius, Mathf.Max(MinimumRadius, length * 0.28f));

            float cylinderLength = GetCapsuleCylinderLength(length, radius);
            Vector3 rotationEuler = ConvertUnityRotationToWriterEuler(capsuleRot);

            return CreateCapsuleBody(bone.name, boneIndex, radius, cylinderLength, position, rotationEuler, depthRatio, preset);
        }

        // 末端叶骨（无子骨）：沿父骨到当前骨的局部切线方向延伸包裹发梢
        if (bone.parent != null && chainBones.Contains(bone.parent))
        {
            Transform parentBone = bone.parent;
            Vector3 worldDirFromParent = bone.position - parentBone.position;
            float parentDist = worldDirFromParent.magnitude;

            Vector3 localDir = bone.InverseTransformDirection(worldDirFromParent);
            if (localDir.sqrMagnitude < 0.000001f) localDir = Vector3.down;
            else localDir.Normalize();

            Quaternion localAlign = Quaternion.FromToRotation(Vector3.up, localDir);
            Quaternion capsuleRot = boneRotInCoord * localAlign;

            float length = Mathf.Clamp(parentDist * 0.6f, MinimumSegmentLength, MaximumRadius * 2.5f);
            float radius = Mathf.Clamp(configuredRadius, MinimumRadius, MaximumRadius);
            radius = Mathf.Min(radius, Mathf.Max(MinimumRadius, length * 0.32f));

            Vector3 centerWorld = bone.position + (worldDirFromParent.normalized * (length * 0.5f));
            Vector3 position = coordinateRoot.InverseTransformPoint(centerWorld);
            float cylinderLength = GetCapsuleCylinderLength(length, radius);
            Vector3 rotationEuler = ConvertUnityRotationToWriterEuler(capsuleRot);

            return CreateCapsuleBody(bone.name, boneIndex, radius, cylinderLength, position, rotationEuler, depthRatio, preset);
        }

        // 单节点孤立骨骼：使用球体刚体
        float sphereRadius = Mathf.Clamp(configuredRadius, MinimumRadius, MaximumRadius);
        Vector3 sphereRotation = ConvertUnityRotationToWriterEuler(boneRotInCoord);
        return new MMDRigidBody
        {
            Name = bone.name + "_physics",
            NameEn = bone.name + "_physics",
            AssociatedBoneIndex = boneIndex,
            CollisionGroup = DynamicCollisionGroup,
            CollisionMask = CreateCollisionMaskExcludingGroups(
                DynamicCollisionGroup, SkirtCollisionGroup, SkirtLegCollisionGroup),
            Shape = MMDRigidBody.RigidBodyShape.RigidShapeSphere,
            Dimemsions = new Vector3(sphereRadius, 0, 0),
            Position = start,
            Rotation = sphereRotation,
            Mass = Mathf.Lerp(preset.RootMass, preset.TipMass, depthRatio),
            TranslateDamp = Mathf.Lerp(preset.RootTranslateDamp, preset.TipTranslateDamp, depthRatio),
            RotateDamp = Mathf.Lerp(preset.RootRotateDamp, preset.TipRotateDamp, depthRatio),
            Restitution = 0,
            Friction = 0,
            Type = MMDRigidBody.RigidBodyType.RigidTypePhysics
        };
    }

    private static MMDRigidBody CreateCapsuleBody(string boneName, int boneIndex, float radius,
        float cylinderLength, Vector3 position, Vector3 rotationEuler, float depthRatio, PhysicsPreset preset)
    {
        return new MMDRigidBody
        {
            Name = boneName + "_physics",
            NameEn = boneName + "_physics",
            AssociatedBoneIndex = boneIndex,
            CollisionGroup = DynamicCollisionGroup,
            CollisionMask = CreateCollisionMaskExcludingGroups(
                DynamicCollisionGroup, SkirtCollisionGroup, SkirtLegCollisionGroup),
            Shape = MMDRigidBody.RigidBodyShape.RigidShapeCapsule,
            Dimemsions = new Vector3(radius, cylinderLength, 0),
            Position = position,
            Rotation = rotationEuler,
            Mass = Mathf.Lerp(preset.RootMass, preset.TipMass, depthRatio),
            TranslateDamp = Mathf.Lerp(preset.RootTranslateDamp, preset.TipTranslateDamp, depthRatio),
            RotateDamp = Mathf.Lerp(preset.RootRotateDamp, preset.TipRotateDamp, depthRatio),
            Restitution = 0,
            Friction = 0,
            Type = MMDRigidBody.RigidBodyType.RigidTypePhysics
        };
    }

    private static MMDRigidBody CreateAnchorBody(Chain chain, Transform coordinateRoot, int boneIndex, Vector3 rotation)
    {
        float radius = Mathf.Max(MinimumRadius, chain.Radii[chain.Root] * 0.75f);
        return new MMDRigidBody
        {
            Name = chain.Root.name + "_anchor",
            NameEn = chain.Root.name + "_anchor",
            AssociatedBoneIndex = boneIndex,
            CollisionGroup = DynamicCollisionGroup,
            CollisionMask = CreateCollisionMaskExcludingGroups(
                DynamicCollisionGroup, SkirtCollisionGroup, SkirtLegCollisionGroup),
            Shape = MMDRigidBody.RigidBodyShape.RigidShapeSphere,
            Dimemsions = new Vector3(radius, 0, 0),
            Position = coordinateRoot.InverseTransformPoint(chain.Root.position),
            Rotation = rotation,
            Mass = 0,
            TranslateDamp = 1,
            RotateDamp = 1,
            Restitution = 0,
            Friction = 0,
            Type = MMDRigidBody.RigidBodyType.RigidTypeKinematic
        };
    }

    private static MMDRigidBody CreateTailBodyCollider(Chain chain, Transform coordinateRoot,
        Transform anchorBone, int anchorBoneIndex)
    {
        if (anchorBone == null) return null;

        Vector3 rootPosition = coordinateRoot.InverseTransformPoint(chain.Root.position);
        Vector3 anchorPosition = coordinateRoot.InverseTransformPoint(anchorBone.position);
        float rootDistance = Vector3.Distance(rootPosition, anchorPosition);
        float tailRadius = chain.Radii[chain.Root];
        if (rootDistance <= MinimumSegmentLength) return null;

        float radius = Mathf.Clamp(
            rootDistance - tailRadius * TailColliderClearanceMultiplier,
            tailRadius * TailColliderMinimumRadiusMultiplier,
            TailColliderMaximumRadius);

        return new MMDRigidBody
        {
            Name = chain.Root.name + "_body_blocker",
            NameEn = chain.Root.name + "_body_blocker",
            AssociatedBoneIndex = anchorBoneIndex,
            CollisionGroup = TailBodyCollisionGroup,
            CollisionMask = CreateCollisionMaskExcludingGroups(
                TailBodyCollisionGroup, SkirtCollisionGroup, SkirtLegCollisionGroup),
            Shape = MMDRigidBody.RigidBodyShape.RigidShapeSphere,
            Dimemsions = new Vector3(radius, 0, 0),
            Position = anchorPosition,
            Rotation = Vector3.zero,
            Mass = 0,
            TranslateDamp = 1,
            RotateDamp = 1,
            Restitution = 0,
            Friction = 0.5f,
            Type = MMDRigidBody.RigidBodyType.RigidTypeKinematic
        };
    }

    private static MMDJoint CreateJoint(Transform bone, Transform coordinateRoot,
        int parentRigidIndex, int rigidIndex, Vector3 jointRotation, float bendLimitDegrees,
        float twistLimitDegrees, float bendSpring, float twistSpring)
    {
        float bend = bendLimitDegrees * Mathf.Deg2Rad;
        float twist = twistLimitDegrees * Mathf.Deg2Rad;
        Vector3 rotationLowLimit = new Vector3(-bend, -twist, -bend);
        Vector3 rotationHiLimit = new Vector3(bend, twist, bend);

        return new MMDJoint
        {
            Name = bone.name + "_joint",
            NameEn = bone.name + "_joint",
            AssociatedRigidBodyIndex = new[] { parentRigidIndex, rigidIndex },
            Position = coordinateRoot.InverseTransformPoint(bone.position),
            Rotation = jointRotation,
            PositionLowLimit = Vector3.zero,
            PositionHiLimit = Vector3.zero,
            RotationLowLimit = rotationLowLimit,
            RotationHiLimit = rotationHiLimit,
            SpringTranslate = Vector3.zero,
            SpringRotate = new Vector3(bendSpring, twistSpring, bendSpring)
        };
    }

    internal static float GetCapsuleCylinderLength(float endpointDistance, float radius)
    {
        return Mathf.Max(MinimumSegmentLength, endpointDistance - radius * 2f);
    }

    internal static ushort CreateCollisionMaskExcludingGroups(params int[] groups)
    {
        ushort mask = 0;
        foreach (int group in groups) mask |= (ushort)(1 << group);
        return mask;
    }

    private static bool IsEarBoneName(string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return false;
        string name = boneName.ToLowerInvariant();
        return name.Contains("ear") || name.Contains("mimi") || name.Contains("耳");
    }

    private static bool IsTailBoneName(string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return false;
        string name = boneName.ToLowerInvariant();
        return name.Contains("tail") || name.Contains("shippo") || name.Contains("尻尾") || name.Contains("尾");
    }

    internal static Vector3 ConvertUnityRotationToWriterEuler(Quaternion unityRotation)
    {
        // 坐标基 S=diag(-1, 1, -1)，完整旋转按 R_pmx=S*R_unity*S^-1 转换
        Quaternion pmxRotation = new Quaternion(
            -unityRotation.x, unityRotation.y, -unityRotation.z, unityRotation.w);
        Vector3 pmxEuler = ExtractPmxEulerDegrees(pmxRotation);

        // Writer 落盘时执行 (x,y,z)->(-x,y,-z)，此处返回内存坐标约定
        return new Vector3(
            NormalizeDegrees(-pmxEuler.x),
            NormalizeDegrees(pmxEuler.y),
            NormalizeDegrees(-pmxEuler.z));
    }

    private static Vector3 ExtractPmxEulerDegrees(Quaternion rotation)
    {
        Matrix4x4 matrix = Matrix4x4.Rotate(rotation);
        // PMX 与 mmd_tools 使用 YXZ 欧拉旋转顺序（R = Ry * Rx * Rz）
        float x = Mathf.Asin(Mathf.Clamp(-matrix.m12, -1f, 1f));
        float cosX = Mathf.Cos(x);
        float y;
        float z;

        if (Mathf.Abs(cosX) > 0.00001f)
        {
            y = Mathf.Atan2(matrix.m02, matrix.m22);
            z = Mathf.Atan2(matrix.m10, matrix.m11);
        }
        else
        {
            y = Mathf.Atan2(matrix.m01, matrix.m00);
            z = 0f;
        }

        return new Vector3(x, y, z) * Mathf.Rad2Deg;
    }

    private static float NormalizeDegrees(float value)
    {
        return value > 180f ? value - 360f : value;
    }

    internal static List<Transform> GetChainChildren(Transform bone, HashSet<Transform> chainBones)
    {
        List<Transform> result = new List<Transform>();
        for (int i = 0; i < bone.childCount; i++)
        {
            Transform child = bone.GetChild(i);
            if (chainBones.Contains(child)) result.Add(child);
        }
        return result;
    }

    internal static Transform GetSingleChainChild(Transform bone, HashSet<Transform> chainBones)
    {
        Transform result = null;
        for (int i = 0; i < bone.childCount; i++)
        {
            Transform child = bone.GetChild(i);
            if (!chainBones.Contains(child)) continue;
            if (result != null) return null;
            result = child;
        }
        return result;
    }

    private static int FindParentRigidIndex(Transform parent, HashSet<Transform> chainBones,
        Dictionary<Transform, int> indexes, int fallback)
    {
        Transform current = parent;
        while (current != null && chainBones.Contains(current))
        {
            if (indexes.TryGetValue(current, out int index)) return index;
            current = current.parent;
        }
        return fallback;
    }

    internal static Transform FindNearestExportedParentTransform(Transform parent,
        Dictionary<Transform, int> boneIndexes)
    {
        for (Transform current = parent; current != null; current = current.parent)
            if (boneIndexes.ContainsKey(current)) return current;
        return null;
    }

    private static int GetDepth(Transform transform)
    {
        int depth = 0;
        for (Transform current = transform; current != null; current = current.parent) depth++;
        return depth;
    }

    internal static float SanitizeRadius(float radius)
    {
        return IsFinite(radius) && radius > 0 ? Mathf.Clamp(radius, MinimumRadius, MaximumRadius) : DefaultRadius;
    }

    private static bool IsLinearPhysicsElement(CySpringDataContainer container, CySpringParamDataElement element)
    {
        if (container == null || element == null) return false;

        string boneText = (element._boneName ?? string.Empty).ToLowerInvariant();
        if (element._childElements != null)
        {
            foreach (CySpringParamDataChildElement child in element._childElements)
                if (child != null) boneText += " " + (child._boneName ?? string.Empty).ToLowerInvariant();
        }

        // 排除裙摆与胸部/披风/袖子
        if (ContainsAny(boneText, "skirt", "cloth", "dress", "bust", "breast", "mune", "sleeve", "cape", "mskirt"))
            return false;

        // 匹配头发、耳朵、马尾、呆毛及尾巴
        if (ContainsAny(boneText, "sp_he_", "sp_hi_tail", "hair", "ear", "tail", "head", "mimi", "shippo", "ponytail", "twin", "ahoge", "bang", "side", "back", "front"))
            return true;

        string path = GetTransformPath(container.transform).ToLowerInvariant();
        if (ContainsAny(path, "skirt", "cloth", "dress", "bust", "breast", "mune", "sleeve", "cape", "mskirt"))
            return false;
        return ContainsAny(path, "sp_he_", "head", "hair", "ear", "tail", "mimi", "shippo");
    }

    private static string GetTransformPath(Transform transform)
    {
        string path = string.Empty;
        for (Transform current = transform; current != null; current = current.parent)
            path = current.name + "/" + path;
        return path;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static void Validate(List<MMDRigidBody> rigidBodies, List<MMDJoint> joints, int boneCount)
    {
        for (int i = 0; i < rigidBodies.Count; i++)
        {
            MMDRigidBody body = rigidBodies[i];
            if (body.AssociatedBoneIndex < 0 || body.AssociatedBoneIndex >= boneCount)
                throw new InvalidOperationException("PMX 刚体骨骼索引无效: " + body.Name);
            if (body.CollisionGroup < 0 || body.CollisionGroup > 15)
                throw new InvalidOperationException("PMX 刚体碰撞组无效: " + body.Name);
            if (body.Type != MMDRigidBody.RigidBodyType.RigidTypeKinematic &&
                body.Type != MMDRigidBody.RigidBodyType.RigidTypePhysics &&
                body.Type != MMDRigidBody.RigidBodyType.RigidTypePhysicsStrict)
                throw new InvalidOperationException("PMX 导出不允许 Type 3 刚体: " + body.Name);
            if (!IsFinite(body.Position) || !IsFinite(body.Rotation) || !IsFinite(body.Dimemsions))
                throw new InvalidOperationException("PMX 刚体包含 NaN 或 Infinity: " + body.Name);
            if (body.Dimemsions.x <= 0f ||
                (body.Shape != MMDRigidBody.RigidBodyShape.RigidShapeSphere && body.Dimemsions.y <= 0f) ||
                (body.Shape == MMDRigidBody.RigidBodyShape.RigidShapeBox && body.Dimemsions.z <= 0f))
                throw new InvalidOperationException("PMX 刚体尺寸无效: " + body.Name);
        }

        foreach (MMDJoint joint in joints)
        {
            int a = joint.AssociatedRigidBodyIndex[0];
            int b = joint.AssociatedRigidBodyIndex[1];
            if (a < 0 || a >= rigidBodies.Count || b < 0 || b >= rigidBodies.Count)
                throw new InvalidOperationException("PMX Joint 刚体索引无效: " + joint.Name);
            if (rigidBodies[a].Type == MMDRigidBody.RigidBodyType.RigidTypeKinematic &&
                rigidBodies[b].Type == MMDRigidBody.RigidBodyType.RigidTypeKinematic)
                throw new InvalidOperationException("PMX Joint 不能连接两个 Type 0 刚体: " + joint.Name);
            if (!IsFinite(joint.Position) || !IsFinite(joint.Rotation) ||
                !IsFinite(joint.PositionLowLimit) || !IsFinite(joint.PositionHiLimit) ||
                !IsFinite(joint.RotationLowLimit) || !IsFinite(joint.RotationHiLimit) ||
                !IsFinite(joint.SpringTranslate) || !IsFinite(joint.SpringRotate))
                throw new InvalidOperationException("PMX Joint 包含 NaN 或 Infinity: " + joint.Name);
            if (joint.PositionLowLimit.x > joint.PositionHiLimit.x ||
                joint.PositionLowLimit.y > joint.PositionHiLimit.y ||
                joint.PositionLowLimit.z > joint.PositionHiLimit.z)
                throw new InvalidOperationException("PMX Joint 平移上下限倒置: " + joint.Name);
            if (joint.RotationLowLimit.x > joint.RotationHiLimit.x ||
                joint.RotationLowLimit.y > joint.RotationHiLimit.y ||
                joint.RotationLowLimit.z > joint.RotationHiLimit.z)
                throw new InvalidOperationException("PMX Joint 旋转上下限倒置: " + joint.Name);
        }
    }

    internal static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
