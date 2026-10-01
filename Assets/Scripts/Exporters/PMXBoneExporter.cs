using LibMMD.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 将 Uma 的运行时骨架整理为 PMX 可用的标准骨架。
/// </summary>
internal static class PMXBoneExporter
{
    internal sealed class Result
    {
        public Bone[] Bones { get; set; }
        public Dictionary<Transform, int> BoneIndexes { get; set; }
    }

    private static readonly Dictionary<string, string> BoneNameMapping = new Dictionary<string, string>()
    {
        { "Spine", "上半身" }, { "Chest", "上半身2" }, { "Neck", "首" }, { "Head", "頭" },
        { "Shoulder_L", "左肩" }, { "Arm_L", "左腕" }, { "Elbow_L", "左ひじ" },
        { "ArmRoll_L", "左手捩" }, { "Wrist_L", "左手首" },
        { "Shoulder_R", "右肩" }, { "Arm_R", "右腕" }, { "Elbow_R", "右ひじ" },
        { "ArmRoll_R", "右手捩" }, { "Wrist_R", "右手首" },
        { "Thumb_01_L", "左親指０" }, { "Thumb_02_L", "左親指１" }, { "Thumb_03_L", "左親指２" },
        { "Index_01_L", "左人指１" }, { "Index_02_L", "左人指２" }, { "Index_03_L", "左人指３" },
        { "Middle_01_L", "左中指１" }, { "Middle_02_L", "左中指２" }, { "Middle_03_L", "左中指３" },
        { "Ring_01_L", "左薬指１" }, { "Ring_02_L", "左薬指２" }, { "Ring_03_L", "左薬指３" },
        { "Pinky_01_L", "左小指１" }, { "Pinky_02_L", "左小指２" }, { "Pinky_03_L", "左小指３" },
        { "Thumb_01_R", "右親指０" }, { "Thumb_02_R", "右親指１" }, { "Thumb_03_R", "右親指２" },
        { "Index_01_R", "右人指１" }, { "Index_02_R", "右人指２" }, { "Index_03_R", "右人指３" },
        { "Middle_01_R", "右中指１" }, { "Middle_02_R", "右中指２" }, { "Middle_03_R", "右中指３" },
        { "Ring_01_R", "右薬指１" }, { "Ring_02_R", "右薬指２" }, { "Ring_03_R", "右薬指３" },
        { "Pinky_01_R", "右小指１" }, { "Pinky_02_R", "右小指２" }, { "Pinky_03_R", "右小指３" },
        { "Thigh_L", "左足" }, { "Knee_L", "左ひざ" }, { "Ankle_L", "左足首" }, { "Toe_L", "左足先EX" },
        { "Thigh_R", "右足" }, { "Knee_R", "右ひざ" }, { "Ankle_R", "右足首" }, { "Toe_R", "右足先EX" },
        { "Eye_L", "左目" }, { "Eye_R", "右目" },
        { "Ear_01_L", "左耳" }, { "Ear_02_L", "左耳1" }, { "Ear_03_L", "左耳2" },
        { "Ear_01_R", "右耳" }, { "Ear_02_R", "右耳1" }, { "Ear_03_R", "右耳2" },
        { "Mouth", "口" }, { "Jaw", "顎" }
    };

    private static readonly string[] RequiredBoneNames =
    {
        "Hip", "Spine", "Chest", "Neck", "Head",
        "Shoulder_L", "Arm_L", "Elbow_L", "ArmRoll_L", "Wrist_L",
        "Shoulder_R", "Arm_R", "Elbow_R", "ArmRoll_R", "Wrist_R",
        "Hand_Attach_L", "Hand_Attach_R",
        "Thigh_L", "Knee_L", "Ankle_L", "Toe_L",
        "Thigh_R", "Knee_R", "Ankle_R", "Toe_R", "Eye_L", "Eye_R"
    };

    internal static Result Build(Transform skeletonRoot, Transform coordinateRoot, IEnumerable<Renderer> renderers,
        IEnumerable<Transform> additionalBones = null)
    {
        Transform[] hierarchy = skeletonRoot.GetComponentsInChildren<Transform>(true);
        HashSet<Transform> selected = CollectReferencedBones(renderers, skeletonRoot);

        // CySpring 的末端骨可能没有蒙皮权重，但 PMX 刚体仍需关联真实骨骼。
        if (additionalBones != null)
        {
            foreach (Transform bone in additionalBones)
            {
                if (bone != null && !IsRuntimeHelper(bone.name)) selected.Add(bone);
            }
        }

        // 某些标准骨可能没有直接权重，但仍是动画和父链所必需的。
        foreach (string boneName in RequiredBoneNames)
        {
            Transform bone = hierarchy.FirstOrDefault(t => t.name.Equals(boneName, StringComparison.OrdinalIgnoreCase));
            if (bone != null)
            {
                selected.Add(bone);
            }
        }

        Transform hip = Find(selected, "Hip");
        List<Bone> bones = new List<Bone>();
        Dictionary<Transform, int> indexes = new Dictionary<Transform, int>();
        Vector3 origin = coordinateRoot.InverseTransformPoint(skeletonRoot.position);
        Vector3 hipPosition = hip != null ? coordinateRoot.InverseTransformPoint(hip.position) : origin;

        int parentOfAll = AddVirtualBone(bones, "全ての親", "ParentOfAll", origin, -1, true);
        int center = AddVirtualBone(bones, "センター", "Center", origin, parentOfAll, true);
        int groove = AddVirtualBone(bones, "グルーブ", "Groove", hipPosition, center, true);
        int waist = AddVirtualBone(bones, "腰", "Waist", hipPosition, groove, false);
        int lowerBody = AddVirtualBone(bones, "下半身", "LowerBody", hipPosition, waist, false);

        if (skeletonRoot != null) indexes[skeletonRoot] = center;
        if (hip != null) indexes[hip] = waist;

        foreach (Transform transform in hierarchy)
        {
            if (!selected.Contains(transform) || transform == hip || IsRuntimeHelper(transform.name))
            {
                continue;
            }

            int parentIndex = ResolveParentIndex(transform, hip, selected, indexes, waist, lowerBody);
            Bone bone = CreateTransformBone(transform, coordinateRoot, parentIndex);
            indexes[transform] = bones.Count;
            bones.Add(bone);
        }

        int bothEyes = AddBothEyesControlBone(bones, indexes, coordinateRoot);
        ReparentTongueChain(bones, indexes);
        RebuildChildLinks(bones);
        AddFootIk(bones, indexes, coordinateRoot, parentOfAll, "L");
        AddFootIk(bones, indexes, coordinateRoot, parentOfAll, "R");
        RebuildChildLinks(bones);
        AlignElbowArmRollAndWrist(bones, indexes, coordinateRoot);
        // 必须在重建普通骨骼末端之后设置，否则挂点的显式垂直末端会被子骨关系覆盖。
        AddHandAttachmentCompatibilityBones(bones, indexes, coordinateRoot);
        SetBothEyesTailToNoseBridge(bones, bothEyes, indexes, coordinateRoot);
        Validate(bones);

        return new Result { Bones = bones.ToArray(), BoneIndexes = indexes };
    }

    private static int AddBothEyesControlBone(List<Bone> bones, Dictionary<Transform, int> indexes,
        Transform coordinateRoot)
    {
        Transform head = Find(indexes.Keys, "Head");
        Transform leftEye = Find(indexes.Keys, "Eye_L");
        Transform rightEye = Find(indexes.Keys, "Eye_R");
        if (head == null || (leftEye == null && rightEye == null)) return -1;

        // Preserve the standard both-eyes control chain while reparenting both eye bones.
        int bothEyes = AddVirtualBone(
            bones,
            "\u4e21\u76ee",
            "BothEyes",
            coordinateRoot.InverseTransformPoint(head.position),
            indexes[head],
            false);

        if (leftEye != null) bones[indexes[leftEye]].ParentIndex = bothEyes;
        if (rightEye != null) bones[indexes[rightEye]].ParentIndex = bothEyes;
        return bothEyes;
    }

    private static void SetBothEyesTailToNoseBridge(List<Bone> bones, int bothEyes,
        Dictionary<Transform, int> indexes, Transform coordinateRoot)
    {
        if (bothEyes < 0) return;

        Transform head = Find(indexes.Keys, "Head");
        Transform leftEye = Find(indexes.Keys, "Eye_L");
        Transform rightEye = Find(indexes.Keys, "Eye_R");
        if (head == null) return;

        Transform nose = FindNoseBone(head);
        Vector3 noseBridgePosition;
        if (nose != null)
        {
            // Prefer a real nose bone so the endpoint follows each character face shape.
            noseBridgePosition = coordinateRoot.InverseTransformPoint(nose.position);
        }
        else if (leftEye != null && rightEye != null)
        {
            // Fall back to the eye midpoint instead of accidentally using the left eye as the endpoint.
            noseBridgePosition = coordinateRoot.InverseTransformPoint((leftEye.position + rightEye.position) * 0.5f);
        }
        else
        {
            return;
        }

        Bone controlBone = bones[bothEyes];
        controlBone.ChildBoneVal.ChildUseId = false;
        controlBone.ChildBoneVal.Offset = noseBridgePosition - controlBone.Position;
    }

    private static Transform FindNoseBone(Transform head)
    {
        Transform[] descendants = head.GetComponentsInChildren<Transform>(true);
        Transform exactMatch = descendants.FirstOrDefault(t => t.name.Equals("Nose", StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null) return exactMatch;

        // Uma nose bones may include numeric suffixes; prefer the root nose bone over facial detail bones.
        return descendants.FirstOrDefault(t =>
            t.name.StartsWith("Nose_00", StringComparison.OrdinalIgnoreCase) ||
            t.name.StartsWith("Nose_01", StringComparison.OrdinalIgnoreCase) ||
            t.name.StartsWith("Nose", StringComparison.OrdinalIgnoreCase));
    }

    private static void AlignElbowArmRollAndWrist(List<Bone> bones,
        Dictionary<Transform, int> indexes, Transform coordinateRoot)
    {
        AlignElbowArmRollAndWrist(bones, indexes, coordinateRoot, "L");
        AlignElbowArmRollAndWrist(bones, indexes, coordinateRoot, "R");
    }

    private static void AlignElbowArmRollAndWrist(List<Bone> bones,
        Dictionary<Transform, int> indexes, Transform coordinateRoot, string side)
    {
        Transform elbow = Find(indexes.Keys, "Elbow_" + side);
        Transform armRoll = Find(indexes.Keys, "ArmRoll_" + side);
        Transform wrist = Find(indexes.Keys, "Wrist_" + side);
        if (elbow == null || armRoll == null || wrist == null ||
            !indexes.TryGetValue(elbow, out int elbowIndex) ||
            !indexes.TryGetValue(armRoll, out int armRollIndex) ||
            !indexes.TryGetValue(wrist, out int wristIndex)) return;

        // ArmRoll 根部是正确的腕部连接坐标：肘末端、ArmRoll 根部、手首根部必须三点重合。
        // 仅校正 PMX 输出坐标与末端指向，不改 Unity 原骨架的动画层级。
        Vector3 connectionPosition = coordinateRoot.InverseTransformPoint(armRoll.position);
        bones[armRollIndex].Position = connectionPosition;
        bones[wristIndex].Position = connectionPosition;
        bones[elbowIndex].ChildBoneVal.ChildUseId = true;
        bones[elbowIndex].ChildBoneVal.Index = wristIndex;
    }

    private static void AddHandAttachmentCompatibilityBones(List<Bone> bones,
        Dictionary<Transform, int> indexes, Transform coordinateRoot)
    {
        AddHandAttachmentCompatibilityBone(bones, indexes, coordinateRoot, "L");
        AddHandAttachmentCompatibilityBone(bones, indexes, coordinateRoot, "R");
    }

    private static void AddHandAttachmentCompatibilityBone(List<Bone> bones,
        Dictionary<Transform, int> indexes, Transform coordinateRoot, string side)
    {
        Transform handAttach = Find(indexes.Keys, "Hand_Attach_" + side);
        if (handAttach == null || !indexes.TryGetValue(handAttach, out int handAttachIndex)) return;

        Vector3 attachmentPosition = coordinateRoot.InverseTransformPoint(handAttach.position);
        Vector3 tailOffset = CalculateHandAttachmentTailOffset(
            indexes.Keys, handAttach, coordinateRoot, side);

        // 原 Hand_Attach 的位置就是掌心根部；90 度修正只作用于骨骼末端，不能移动根部。
        Bone attachmentBone = bones[handAttachIndex];
        attachmentBone.Position = attachmentPosition;
        attachmentBone.ChildBoneVal.ChildUseId = false;
        attachmentBone.ChildBoneVal.Offset = tailOffset;

        string dummyName = side == "L" ? "ダミー.L" : "ダミー.R";
        if (bones.Any(b => b.Name.Equals(dummyName, StringComparison.OrdinalIgnoreCase))) return;

        // 保留游戏内部 Hand_Attach，并补出外部手持场景按名称查找的兼容挂点。
        // 两骨同根、同向；父子关系仍用于继承手部运动，但不再用子骨位置定义末端。
        int dummyIndex = AddVirtualBone(
            bones,
            dummyName,
            "Dummy." + side,
            attachmentPosition,
            handAttachIndex,
            false);
        bones[dummyIndex].ChildBoneVal.ChildUseId = false;
        bones[dummyIndex].ChildBoneVal.Offset = tailOffset;
    }

    private static Vector3 CalculateHandAttachmentTailOffset(IEnumerable<Transform> transforms,
        Transform handAttach,
        Transform coordinateRoot, string side)
    {
        Transform wrist = Find(transforms, "Wrist_" + side);
        Transform elbow = Find(transforms, "Elbow_" + side);
        if (wrist == null || elbow == null) return Vector3.up * 0.1f;

        Transform armRoll = Find(transforms, "ArmRoll_" + side);
        // 手首导出坐标已经对齐 ArmRoll；挂点计算必须使用同一坐标源，不能继续读取旧 Wrist Transform。
        Vector3 wristPosition = coordinateRoot.InverseTransformPoint(
            armRoll != null ? armRoll.position : wrist.position);
        Vector3 elbowPosition = coordinateRoot.InverseTransformPoint(elbow.position);
        Vector3 attachmentPosition = coordinateRoot.InverseTransformPoint(handAttach.position);
        Vector3 wristToAttachment = attachmentPosition - wristPosition;
        if (wristToAttachment.sqrMagnitude < 0.000001f) return Vector3.up * 0.1f;

        Vector3 wristBoneDirection = wristPosition - elbowPosition;
        if (wristBoneDirection.sqrMagnitude < 0.000001f) return Vector3.up * wristToAttachment.magnitude;

        // 挂点骨以掌心为根部，末端直接垂直于手首骨（Elbow -> Wrist），而不是垂直于世界坐标。
        // 只沿模型平面旋转方向，并继续使用原 Wrist -> Hand_Attach 距离作为辅助骨长度。
        float attachmentLength = wristToAttachment.magnitude;
        Vector3 positiveCandidate =
            (Quaternion.AngleAxis(90f, Vector3.forward) * wristBoneDirection).normalized * attachmentLength;
        Vector3 negativeCandidate =
            (Quaternion.AngleAxis(-90f, Vector3.forward) * wristBoneDirection).normalized * attachmentLength;

        string[] fingerRootNames =
        {
            "Index_01_" + side,
            "Middle_01_" + side,
            "Ring_01_" + side,
            "Pinky_01_" + side
        };
        List<Transform> fingerRoots = fingerRootNames
            .Select(name => Find(transforms, name))
            .Where(root => root != null)
            .ToList();
        if (fingerRoots.Count == 0)
        {
            // 缺少手指骨时，以原挂点相对手首的位置判断掌心侧，再选择相反方向。
            float positiveAlignment = Vector3.Dot(positiveCandidate.normalized, wristToAttachment.normalized);
            float negativeAlignment = Vector3.Dot(negativeCandidate.normalized, wristToAttachment.normalized);
            return positiveAlignment <= negativeAlignment ? positiveCandidate : negativeCandidate;
        }

        Vector3 knuckleCenter = Vector3.zero;
        foreach (Transform fingerRoot in fingerRoots)
        {
            knuckleCenter += coordinateRoot.InverseTransformPoint(fingerRoot.position);
        }
        knuckleCenter /= fingerRoots.Count;

        // 手调正确结果的末端背离手指，因此选择与“掌心根部 -> 四指根中心”夹角更大的候选。
        Vector3 attachmentToKnuckles = knuckleCenter - attachmentPosition;
        float positiveTowardFingers = Vector3.Dot(positiveCandidate.normalized, attachmentToKnuckles.normalized);
        float negativeTowardFingers = Vector3.Dot(negativeCandidate.normalized, attachmentToKnuckles.normalized);
        return positiveTowardFingers <= negativeTowardFingers ? positiveCandidate : negativeCandidate;
    }

    private static HashSet<Transform> CollectReferencedBones(IEnumerable<Renderer> renderers, Transform skeletonRoot)
    {
        HashSet<Transform> weightedBones = new HashSet<Transform>();
        foreach (SkinnedMeshRenderer renderer in renderers.OfType<SkinnedMeshRenderer>())
        {
            Mesh mesh = renderer.sharedMesh;
            Transform[] rendererBones = renderer.bones;
            if (mesh == null || rendererBones == null) continue;

            // renderer.bones 还会包含脸部占位骨；只有顶点实际使用且权重大于零时才算蒙皮引用。
            foreach (BoneWeight weight in mesh.boneWeights)
            {
                AddWeightedBone(weightedBones, rendererBones, weight.boneIndex0, weight.weight0);
                AddWeightedBone(weightedBones, rendererBones, weight.boneIndex1, weight.weight1);
                AddWeightedBone(weightedBones, rendererBones, weight.boneIndex2, weight.weight2);
                AddWeightedBone(weightedBones, rendererBones, weight.boneIndex3, weight.weight3);
            }
        }

        HashSet<Transform> result = new HashSet<Transform>();
        foreach (Transform weightedBone in weightedBones)
        {
            // 无直接权重的 offset/连接骨仍可能是有效父链，必须补齐到骨架根节点。
            Transform current = weightedBone;
            while (current != null)
            {
                // skeletonRoot 已映射到虚拟“センター”，不能再作为普通骨重复导出。
                if (current == skeletonRoot) break;
                if (!IsRuntimeHelper(current.name)) result.Add(current);
                current = current.parent;
            }
        }

        return result;
    }

    private static void AddWeightedBone(HashSet<Transform> result, Transform[] bones, int boneIndex, float weight)
    {
        const float MinimumWeight = 0.000001f;
        if (weight <= MinimumWeight || boneIndex < 0 || boneIndex >= bones.Length) return;

        Transform bone = bones[boneIndex];
        if (bone != null && !IsRuntimeHelper(bone.name)) result.Add(bone);
    }

    private static bool IsRuntimeHelper(string name)
    {
        return name.StartsWith("Col_", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_Handle", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_Pole", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_Target", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_Ctrl", StringComparison.OrdinalIgnoreCase) ||
               name.IndexOf("locator", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Transform Find(IEnumerable<Transform> bones, string name)
    {
        return bones.FirstOrDefault(t => t.name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static int ResolveParentIndex(Transform transform, Transform hip, HashSet<Transform> selected,
        Dictionary<Transform, int> indexes, int waist, int lowerBody)
    {
        Transform parent = transform.parent;
        while (parent != null && parent != hip)
        {
            if (selected.Contains(parent) && indexes.TryGetValue(parent, out int parentIndex)) return parentIndex;
            parent = parent.parent;
        }

        if (parent == hip)
        {
            // 上半身控制链中的实际骨挂腰，其余髋部支链（腿、裙摆、尾巴）挂下半身。
            return IsUpperBodyBranch(transform, hip) ? waist : lowerBody;
        }
        return waist;
    }

    private static bool IsUpperBodyBranch(Transform transform, Transform hip)
    {
        Transform current = transform;
        while (current != null && current != hip)
        {
            if (current.name.Equals("Spine", StringComparison.OrdinalIgnoreCase) ||
                current.name.Equals("Waist", StringComparison.OrdinalIgnoreCase) ||
                current.name.Equals("UpBody_Ctrl", StringComparison.OrdinalIgnoreCase)) return true;
            current = current.parent;
        }
        return false;
    }

    private static Bone CreateTransformBone(Transform transform, Transform coordinateRoot, int parentIndex)
    {
        string mappedName;
        BoneNameMapping.TryGetValue(transform.name, out mappedName);
        return new Bone
        {
            Name = mappedName ?? transform.name,
            NameEn = transform.name,
            Position = coordinateRoot.InverseTransformPoint(transform.position),
            ParentIndex = parentIndex,
            TransformLevel = 0,
            Rotatable = true,
            Movable = false,
            Visible = true,
            Controllable = true,
            ChildBoneVal = new Bone.ChildBone { ChildUseId = false, Offset = Vector3.up * 0.1f }
        };
    }

    private static int AddVirtualBone(List<Bone> bones, string name, string nameEn, Vector3 position, int parent, bool movable)
    {
        bones.Add(new Bone
        {
            Name = name,
            NameEn = nameEn,
            Position = position,
            ParentIndex = parent,
            TransformLevel = 0,
            Rotatable = true,
            Movable = movable,
            Visible = true,
            Controllable = true,
            ChildBoneVal = new Bone.ChildBone { ChildUseId = false, Offset = Vector3.up * 0.1f }
        });
        return bones.Count - 1;
    }

    private static void RebuildChildLinks(List<Bone> bones)
    {
        for (int i = 0; i < bones.Count; i++)
        {
            int child = bones.FindIndex(b => b.ParentIndex == i);
            if (child >= 0)
            {
                bones[i].ChildBoneVal.ChildUseId = true;
                bones[i].ChildBoneVal.Index = child;
            }
            else
            {
                bones[i].ChildBoneVal.ChildUseId = false;
                bones[i].ChildBoneVal.Offset = Vector3.up * 0.1f;
            }
        }
    }

    private static void ReparentTongueChain(List<Bone> bones, Dictionary<Transform, int> indexes)
    {
        // 保留原导出器的舌骨兼容关系，避免拆分骨架模块后出现既有功能回退。
        Transform chin = Find(indexes.Keys, "Chin") ?? indexes.Keys.FirstOrDefault(t => t.name.StartsWith("Jaw"));
        Transform tongue = Find(indexes.Keys, "Tongue");
        Transform tongueOut01 = Find(indexes.Keys, "Tongue_Out_01");
        Transform tongueOut02 = Find(indexes.Keys, "Tongue_Out_02");
        if (chin == null) return;

        if (tongue != null)
        {
            bones[indexes[tongue]].ParentIndex = indexes[chin];
            if (tongueOut01 != null) bones[indexes[tongueOut01]].ParentIndex = indexes[tongue];
            if (tongueOut01 != null && tongueOut02 != null) bones[indexes[tongueOut02]].ParentIndex = indexes[tongueOut01];
            return;
        }

        foreach (Transform candidate in indexes.Keys.Where(t => t.name.StartsWith("Tongue") && !t.name.Contains("Out")))
        {
            bones[indexes[candidate]].ParentIndex = indexes[chin];
        }
    }

    private static void AddFootIk(List<Bone> bones, Dictionary<Transform, int> indexes,
        Transform coordinateRoot, int parentOfAll, string side)
    {
        Transform thigh = Find(indexes.Keys, "Thigh_" + side);
        Transform knee = Find(indexes.Keys, "Knee_" + side);
        Transform ankle = Find(indexes.Keys, "Ankle_" + side);
        Transform toe = Find(indexes.Keys, "Toe_" + side);
        if (thigh == null || knee == null || ankle == null) return;

        string prefix = side == "L" ? "左" : "右";
        Vector3 anklePosition = coordinateRoot.InverseTransformPoint(ankle.position);
        Vector3 ikParentPosition = new Vector3(anklePosition.x, 0, anklePosition.z);
        int ikParent = AddVirtualBone(bones, prefix + "足IK親", "FootIKParent_" + side, ikParentPosition, parentOfAll, true);
        int footIk = AddVirtualBone(bones, prefix + "足ＩＫ", "FootIK_" + side, anklePosition, ikParent, true);
        bones[footIk].HasIk = true;
        bones[footIk].TransformLevel = 1;
        bones[footIk].IkInfoVal = new Bone.IkInfo
        {
            IkTargetIndex = indexes[ankle],
            CcdIterateLimit = 40,
            CcdAngleLimit = 2.0f,
            IkLinks = new[]
            {
                // 按 PMX 文件坐标语义使用负 X 区间，限制膝盖只向预期方向弯曲。
                new Bone.IkLink { LinkIndex = indexes[knee], HasLimit = true, LoLimit = new Vector3(-Mathf.PI, 0, 0), HiLimit = new Vector3(-0.0087f, 0, 0) },
                new Bone.IkLink { LinkIndex = indexes[thigh], HasLimit = false }
            }
        };

        if (toe == null) return;
        int toeIk = AddVirtualBone(bones, prefix + "つま先ＩＫ", "ToeIK_" + side,
            coordinateRoot.InverseTransformPoint(toe.position), footIk, true);
        bones[toeIk].HasIk = true;
        bones[toeIk].TransformLevel = 1;
        bones[toeIk].IkInfoVal = new Bone.IkInfo
        {
            IkTargetIndex = indexes[toe],
            CcdIterateLimit = 3,
            CcdAngleLimit = 1.0f,
            IkLinks = new[] { new Bone.IkLink { LinkIndex = indexes[ankle], HasLimit = false } }
        };
    }

    private static void Validate(List<Bone> bones)
    {
        for (int i = 0; i < bones.Count; i++)
        {
            Bone bone = bones[i];
            if (bone.ParentIndex < -1 || bone.ParentIndex >= bones.Count || bone.ParentIndex == i)
                throw new InvalidOperationException("PMX 骨骼父索引无效: " + bone.Name);
            if (bone.ChildBoneVal.ChildUseId && (bone.ChildBoneVal.Index < 0 || bone.ChildBoneVal.Index >= bones.Count))
                throw new InvalidOperationException("PMX 骨骼尾端索引无效: " + bone.Name);
            if (bone.HasIk && (bone.IkInfoVal == null || bone.IkInfoVal.IkTargetIndex < 0 || bone.IkInfoVal.IkTargetIndex >= bones.Count))
                throw new InvalidOperationException("PMX IK 目标索引无效: " + bone.Name);
            if (!bone.HasIk) continue;

            foreach (Bone.IkLink link in bone.IkInfoVal.IkLinks)
            {
                if (link.LinkIndex < 0 || link.LinkIndex >= bones.Count)
                    throw new InvalidOperationException("PMX IK 链接索引无效: " + bone.Name);
                if (!link.HasLimit) continue;
                ValidateIkLimit(link.LoLimit, link.HiLimit, bone.Name);
            }
        }
    }

    private static void ValidateIkLimit(Vector3 lower, Vector3 upper, string boneName)
    {
        if (!IsFinite(lower) || !IsFinite(upper))
            throw new InvalidOperationException("PMX IK 角度限制包含 NaN 或 Infinity: " + boneName);
        if (lower.x > upper.x || lower.y > upper.y || lower.z > upper.z)
            throw new InvalidOperationException("PMX IK 角度限制上下限倒置: " + boneName);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
