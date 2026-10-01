using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Gallop
{
    /// CySpring 弹簧骨骼系统中的根骨骼控制
    /// - 创建根骨骼及其子骨骼链；
    /// - 初始化和更新原生布料模拟数据；
    /// - 注册角色碰撞体和环境碰撞体；
    /// - 应用弹性、阻力、重力、风力和旋转限制等参数；
    /// - 更新骨骼父节点信息、世界坐标和旋转结果；
    /// - 控制模拟结果是否应用到实际骨骼；
    /// - 重置、查找和释放整条弹簧骨骼链。
    public class CySpringRootBone : CySpringBoneBase
    {
        public const float DEFAULT_SUPPRESSION_CHECK_LENGTH = 1f;
        private const float DEFAULT_SUPPRESSION_CHECK_LENGTH_SQR = 1f;
        private const float DEFAULT_SUPPRESSION_LENGTH = 0.01f;
        private const float DEFAULT_SUPPRESSION_LENGTH_SQR = 0.0001f;

        private List<CySpringBoneBase> _childBoneList;

        public int TrueNativesCount { get; private set; }
        public float WindPhaseShift;
        public NativeClothWorking[] NativeArray;
        public Quaternion[] PrevFinalRotation;
        public int LinkSkirtIndex;

        private Transform _parent;
        private bool _isSuppression;
        private float _suppressionLength;

        public CySpringParamDataElement RootDataElement;

        public List<string> CollisionNameList => RootDataElement != null ? RootDataElement.CollisionNameList : null;
        public List<CySpringParamDataChildElement> ChildElementList => RootDataElement != null ? RootDataElement.ChildElementList : null;
        public List<CySpringBoneBase> ChildBoneList => _childBoneList;
        public Transform Parent => _parent;

        public bool IsSuppression
        {
            get => _isSuppression;
            set => _isSuppression = value;
        }

        public float SuppressionLength
        {
            get => _suppressionLength;
            set => _suppressionLength = value;
        }

        public CySpringRootBone()
        {
            _childBoneList = new List<CySpringBoneBase>();
            TrueNativesCount = 0;
            WindPhaseShift = 0.0f;
            LinkSkirtIndex = -1;
            _suppressionLength = DEFAULT_SUPPRESSION_LENGTH;
        }

        public void CreateRoot(
            CySpringParamDataElement element,
            CySpring root,
            CySpringRootBone rootBone,
            CySpringBoneBase parentBone,
            float legacyScale,
            Dictionary<string, int> collisionTable,
            bool isAdd)
        {
            RootDataElement = element;
            _boneName = element != null ? element.Name : null;

            if (element == null || root == null || root.RootTransform == null)
                return;

            Transform rootTransform = root.RootTransform;
            Vector3 oldRootPosition = rootTransform.position;
            Quaternion oldRootRotation = rootTransform.rotation;

            // Official CreateRoot temporarily places root transform at the default pose,
            // creates/initializes all bone matrices, then restores the original transform.
            rootTransform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            try
            {
                Create(root, rootBone, parentBone, isAdd);

                if (_transform != null)
                    _parent = _transform.parent;

                if (!Exist)
                    return;

                _childBoneList = new List<CySpringBoneBase>();
                CreateChildBonesRecursive(element, root, rootBone, collisionTable, isAdd);

                int nativeCount = _childBoneList.Count + 1;
                NativeArray = new NativeClothWorking[nativeCount];
                PrevFinalRotation = new Quaternion[nativeCount];

                for (int i = 0; i < nativeCount; i++)
                    PrevFinalRotation[i] = Quaternion.identity;

                // Official code initializes NativeArray[1..n] with defaults before Initialize().
                for (int i = 1; i < nativeCount; i++)
                    NativeArray[i] = CreateDefaultNative(isAdd);

                Initialize(0, false);

                for (int i = 0; i < _childBoneList.Count; i++)
                {
                    bool isLast = i == _childBoneList.Count - 1;
                    _childBoneList[i].Initialize(i + 1, isLast);
                }

                ApplyRootElementToNative(element, legacyScale, isAdd);
                PrevFinalRotation[0] = Quaternion.identity;

                SetTrueNativesCount();
            }
            finally
            {
                rootTransform.SetPositionAndRotation(oldRootPosition, oldRootRotation);
            }
        }

        private void CreateChildBonesRecursive(
            CySpringParamDataElement element,
            CySpring root,
            CySpringRootBone rootBone,
            Dictionary<string, int> collisionTable,
            bool isAdd)
        {
            if (_transform == null || element == null)
                return;

            List<CySpringParamDataChildElement> childElements = element.ChildElementList;
            if (childElements == null)
                return;

            int childCount = _transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = _transform.GetChild(i);
                if (child == null)
                    continue;

                string childName = child.name;
                if (collisionTable != null && collisionTable.ContainsKey(childName))
                    continue;

                if (!ContainsChildElement(childElements, childName))
                    continue;

                CySpringBoneBase childBone = new CySpringBoneBase(childName);
                childBone.ShouldReflectCalcResult = ShouldReflectCalcResult;

                _childBoneList.Add(childBone);
                childBone.CreateChild(root, rootBone, this, collisionTable, isAdd);
            }
        }

        private static bool ContainsChildElement(List<CySpringParamDataChildElement> elements, string name)
        {
            if (elements == null || string.IsNullOrEmpty(name))
                return false;

            for (int i = 0; i < elements.Count; i++)
            {
                CySpringParamDataChildElement element = elements[i];
                if (element != null && string.Equals(element.Name, name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static NativeClothWorking CreateDefaultNative(bool isAdd)
        {
            NativeClothWorking native = default;

            // Matches the fields written by official CreateRoot for NativeArray[1..n].
            // Other fields intentionally remain default(0) until CySpringBoneBase.Initialize fills them.
            native.StiffnessForce = 0.0001f;
            native.DragForce = 0.05f;
            native.CollisionRadius = 0.01f;
            native.BoneAxis = Vector3.up;
            native.Gravity = 0.0f;

            native.VerticalWindRateSlow = 1.0f;
            native.VerticalWindRateFast = 1.0f;
            native.HorizontalWindRateSlow = 1.0f;
            native.HorizontalWindRateFast = 1.0f;

            native.IsLimit = 0;
            native.LimitRotationMin = new Vector3(180f, 180f, 180f);
            native.LimitRotationMax = new Vector3(180f, 180f, 180f);
            native.MoveSpringApplyRate = 1.0f;
            native.IsAddSpring = isAdd ? 1 : 0;

            return native;
        }

        public override void Delete()
        {
            if (_childBoneList != null)
            {
                for (int i = 0; i < _childBoneList.Count; i++)
                    _childBoneList[i]?.Delete();
                _childBoneList.Clear();
            }

            NativeArray = null;
            PrevFinalRotation = null;
            RootDataElement = null;
            _parent = null;

            base.Delete();
        }

        public void SetNativeCollisions()
        {
            RegistCollisionToCloth();

            if (NativeArray != null && (uint)Index < (uint)NativeArray.Length)
                NativeArray[Index].CheckCharaCollision = 1;

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
                _childBoneList[i]?.RegistCollisionToCloth();
        }

        private new void RegistCollisionToCloth()
        {
            CreateCollisionIndexList();

            List<string> collisionNames = CollisionNameList;
            if (collisionNames != null)
            {
                for (int i = 0; i < collisionNames.Count; i++)
                    AddCollisionIndex(collisionNames[i]);
            }

            if (RootDataElement == null || !RootDataElement.CheckEnvCollision || _root == null)
                return;

            int charaCount = _root.CharaCollisionList != null ? _root.CharaCollisionList.Count : 0;
            ReadOnlyCollection<CySpringCollisionRuntimeData> envList = _root.EnvCollisionList;
            if (envList == null)
                return;

            for (int i = 0; i < envList.Count; i++)
            {
                CySpringCollisionRuntimeData env = envList[i];
                if (ShouldUseEnvCollision(env, this))
                    AddCollisionIndex(charaCount + i);
            }
        }

        private static bool ShouldUseEnvCollision(CySpringCollisionRuntimeData env, CySpringBoneBase bone)
        {
            if (env == null)
                return false;

            // Official RegistCollisionToCloth condition:
            //   if env filter flag (+0x59) is false -> apply.
            //   else apply when env filter set (+0x60).Contains(thisBone) != env filter mode (+0x5A).
            // This project keeps that offset-level rule inside CySpringCollisionRuntimeData.ShouldApplyToBone().
            return env.ShouldApplyToBone(bone);
        }

        public void SetNativeCloth(CySpringParamDataElement element, float legacyScale)
        {
            RootDataElement = element;
            ChangeValue(element, legacyScale);

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
                _childBoneList[i]?.ChangeValue(legacyScale);
        }

        public void ChangeValue(CySpringParamDataElement element, float legacyScale)
        {
            // Official ChangeValue only writes NativeArray[0]. It does not use Index here.
            if (element == null || NativeArray == null || NativeArray.Length == 0)
                return;

            ApplyRootElementToNative(element, legacyScale, _isAddSpring);
        }

        private void ApplyRootElementToNative(CySpringParamDataElement element, float legacyScale, bool isAdd)
        {
            if (element == null || NativeArray == null || NativeArray.Length == 0)
                return;

            NativeClothWorking native = NativeArray[0];

            native.StiffnessForce = element.StiffnessForce;
            native.DragForce = element.DragForce;
            native.Gravity = element.Gravity;

            float scaleY = _transform != null ? _transform.lossyScale.y : 1.0f;
            native.CollisionRadius = SafeScaleRadius(element.CollisionRadius, legacyScale, scaleY);

            // Official field order in memory:
            // +92 vertical slow, +100 vertical fast, +96 horizontal slow, +104 horizontal fast.
            native.VerticalWindRateSlow = element.VerticalWindRateSlow;
            native.VerticalWindRateFast = element.VerticalWindRateFast;
            native.HorizontalWindRateSlow = element.HorizontalWindRateSlow;
            native.HorizontalWindRateFast = element.HorizontalWindRateFast;

            native.IsLimit = ToByte(element.IsLimit);
            native.LimitRotationMin = element.LimitRotationMin;
            native.LimitRotationMax = element.LimitRotationMax;

            // Official root writes only the native field at +0x13C from element +0x54.
            // In your managed param class this is MoveSpringApplyRate.
            native.MoveSpringApplyRate = element.MoveSpringApplyRate;
            native.IsAddSpring = isAdd ? 1 : 0;

            NativeArray[0] = native;

            // Official copies element +0x58 back into RootDataElement +0x58.
            // In the dumps/user scripts this field is _needSimulateEndBone.
            if (RootDataElement != null)
                RootDataElement._needSimulateEndBone = element._needSimulateEndBone;
        }

        public override void ResetScale(float bodyScale, bool isUpdateScale)
        {
            base.ResetScale(bodyScale, isUpdateScale);

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
                _childBoneList[i]?.ResetScale(bodyScale, isUpdateScale);
        }

        public bool IsParentName(string target)
        {
            if (string.IsNullOrEmpty(target))
                return false;

            CySpringBoneBase p = ParentBone;
            while (p != null)
            {
                if (string.Equals(p.BoneName, target, StringComparison.Ordinal))
                    return true;
                p = p.ParentBone;
            }

            return false;
        }

        public void GatherRootSpring(NativeRootParentWork[] parentWork, float bodyScale)
        {
            UpdateNativeOldTransform(parentWork, bodyScale);
            SaveWorldRotation();

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
                _childBoneList[i]?.SaveWorldRotation();
        }

        public void CollectRootParentInfo(CySpring.RootParentInfo rootInfo)
        {
            if (rootInfo == null || NativeArray == null)
                return;

            Transform parent = _parent;
            if (parent == null && _transform != null)
                parent = _transform.parent;

            int rootParentIndex = rootInfo.GetOrAddParent(parent);
            SetNativeParentWorkIndex(Index, rootParentIndex);

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
            {
                CySpringBoneBase child = _childBoneList[i];
                if (child == null || child.Transform == null)
                    continue;

                Transform childParent = child.Transform.parent;
                int childParentIndex = rootInfo.GetOrAddParent(childParent);

                SetNativeParentWorkIndex(child.Index, childParentIndex);
            }
        }

        private void SetNativeParentWorkIndex(int nativeIndex, int parentWorkIndex)
        {
            if (NativeArray == null)
                return;

            if ((uint)nativeIndex >= (uint)NativeArray.Length)
                return;

            NativeClothWorking native = NativeArray[nativeIndex];
            native.ParentWorkIndex = parentWorkIndex;
            NativeArray[nativeIndex] = native;
        }

        private void UpdateNativeOldTransform(NativeRootParentWork[] parentWork, float bodyScale)
        {
            if (NativeArray == null || (uint)Index >= (uint)NativeArray.Length || parentWork == null)
                return;

            NativeClothWorking native = NativeArray[Index];
            int parentWorkIndex = native.ParentWorkIndex;

            if ((uint)parentWorkIndex >= (uint)parentWork.Length)
                return;

            NativeRootParentWork work = parentWork[parentWorkIndex];

            Vector3 localPosition = _transform != null ? _transform.localPosition : Vector3.zero;
            Vector3 worldPosition = work.WorldPosition + (work.WorldRotation * localPosition) * bodyScale;

            native.ParentRotation = work.WorldRotation;

            if (_isSuppression)
            {
                UpdateSuppression(ref worldPosition);
                _isSuppression = false;
            }

            native.SelfPosition = worldPosition;
            NativeArray[Index] = native;
        }

        private void UpdateSuppression(ref Vector3 worldPosition)
        {
            if (_transform == null)
                return;

            Vector3 current = _transform.position;
            Vector3 diff = worldPosition - current;

            if (diff.sqrMagnitude <= DEFAULT_SUPPRESSION_CHECK_LENGTH_SQR)
                return;

            float length = _suppressionLength > 0.0f ? _suppressionLength : DEFAULT_SUPPRESSION_LENGTH;
            if (length * length < DEFAULT_SUPPRESSION_LENGTH_SQR)
                length = DEFAULT_SUPPRESSION_LENGTH;

            worldPosition = current + diff.normalized * length;
        }

        public void PostAllSpring()
        {
            PostSpring();

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
                _childBoneList[i]?.PostSpring();
        }

        public void ResetNativeCloth()
        {
            if (_parent == null)
                return;

            Matrix4x4 matrix = _parent.localToWorldMatrix;
            ResetNativeCloth(ref matrix);

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
                _childBoneList[i]?.ResetNativeCloth(ref matrix);
        }

        public void SetResetLink(bool enable)
        {
            ResetLinkShouldReflectCalcResult = enable;

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
                _childBoneList[i].ResetLinkShouldReflectCalcResult = enable;
        }

        public void SetShouldReflectCalcResult(bool enable)
        {
            ShouldReflectCalcResult = enable;

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
                _childBoneList[i].ShouldReflectCalcResult = enable;
        }

        public void SetRootShouldReflectCalcResult(bool enable)
        {
            ShouldReflectCalcResult = enable;
        }

        public void SetEnableCySpringBoneWindAll(bool enable)
        {
            SetEnableCySpringBoneWind(enable);

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
                _childBoneList[i]?.SetEnableCySpringBoneWind(enable);
        }

        public override void SetEnableCySpringBoneWind(bool enable)
        {
            base.SetEnableCySpringBoneWind(enable);
        }

        public void SetTrueNativesCount()
        {
            if (NativeArray == null)
            {
                TrueNativesCount = 0;
                return;
            }

            if (_childBoneList != null && _childBoneList.Count > 0)
            {
                CySpringBoneBase last = _childBoneList[_childBoneList.Count - 1];
                if (last != null && !last.NeedSimulateEndBone)
                {
                    TrueNativesCount = NativeArray.Length - 1;
                    return;
                }
            }

            TrueNativesCount = NativeArray.Length;
        }

        public void FindCySpringBone(List<CySpringBoneBase> resultBoneList, Func<string, bool> checkFunc, bool firstHitBreak)
        {
            if (resultBoneList == null || checkFunc == null)
                return;

            if (checkFunc(BoneName))
            {
                resultBoneList.Add(this);
                if (firstHitBreak)
                    return;
            }

            if (_childBoneList == null)
                return;

            for (int i = 0; i < _childBoneList.Count; i++)
            {
                CySpringBoneBase child = _childBoneList[i];
                if (child == null)
                    continue;

                if (child.FindCySpringBone(resultBoneList, checkFunc, firstHitBreak) && firstHitBreak)
                    return;
            }
        }

        private static float SafeScaleRadius(float value, float legacyScale, float lossyScaleY)
        {
            // Official code directly does: (collisionRadius / legacyScale) * transform.lossyScale.y
            return (value / legacyScale) * lossyScaleY;
        }

        private static byte ToByte(bool value)
        {
            return value ? (byte)1 : (byte)0;
        }
    }
}
