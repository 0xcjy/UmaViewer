using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop
{
    /// <summary>
    /// 弹簧骨骼链中的一个骨骼节点。
    /// - 通过骨骼名称解析 GameObject/Transform
    /// - 存储初始姿态数据
    /// - 在数组索引 Index 处写入 NativeClothWorking 数据
    /// - 注册最多 16 个碰撞体索引
    /// - 执行重置/结果姿态回写操作
    /// </summary>
    public class CySpringBoneBase
    {
        private const int DEFAULT_COLLISION_NUM = 16;
        private const float MIN_BONE_DISTANCE = 0.0001f;

        protected string _boneName;
        protected CySpring _root;
        protected CySpringRootBone _rootBone;
        protected CySpringBoneBase _parentBone;
        protected GameObject _gameObject;
        protected Transform _transform;

        protected bool _exist;
        public int Index;

        public bool ShouldReflectCalcResult;
        public bool ResetLinkShouldReflectCalcResult;

        public Vector3 FirstLocalPosition;
        public Matrix4x4 FirstLocalMatrix;
        public Vector3 FirstPosition;
        public Vector3 FirstParentPosition;

        protected float _initialGravity;
        protected CySpringParamDataChildElement _dataChildElement;

        protected bool _isAddSpring;
        protected bool _isOverrideCheckEnvCollision;
        protected bool _isEnvCollision;

        private Quaternion _initRotationInv = Quaternion.identity;
        private bool _needSimulateEndBone = true;
        private List<int> _collisionIndexList;

        public string BoneName => _boneName;
        public CySpringBoneBase ParentBone => _parentBone;
        public Transform Transform => _transform;
        public GameObject GameObject => _gameObject;
        public bool Exist { get => _exist; set => _exist = value; }
        public bool IsAddSpring => _isAddSpring;
        public bool NeedSimulateEndBone => _needSimulateEndBone;
        public List<int> CollisionIndexList => _collisionIndexList;

        public float StiffnessForce => GetNative().StiffnessForce;
        public float DragForce => GetNative().DragForce;
        public float CollisionRadius => GetNative().CollisionRadius;
        public float Gravity => GetNative().Gravity;
        public float VerticalWindRateSlow => GetNative().VerticalWindRateSlow;
        public float VerticalWindRateFast => GetNative().VerticalWindRateFast;
        public float HorizontalWindRateSlow => GetNative().HorizontalWindRateSlow;
        public float HorizontalWindRateFast => GetNative().HorizontalWindRateFast;
        public float InitBoneDistance
        {
            get => GetNative().InitBoneDistance;
            set
            {
                if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                    return;

                NativeClothWorking native = _rootBone.NativeArray[Index];
                native.InitBoneDistance = value;
                _rootBone.NativeArray[Index] = native;
            }
        }

        public Quaternion InitRotationInv { get => _initRotationInv; set => _initRotationInv = value; }

        public Vector3 BoneAxis
        {
            get => GetNative().BoneAxis;
            set
            {
                if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                    return;

                NativeClothWorking native = _rootBone.NativeArray[Index];
                native.BoneAxis = value;
                _rootBone.NativeArray[Index] = native;
            }
        }

        public bool IsLimit => GetNative().IsLimit != 0;
        public Vector3 LimitAngleMin => GetNative().LimitRotationMin;
        public Vector3 LimitAngleMax => GetNative().LimitRotationMax;

        public float DynamicsRatio
        {
            get => GetNative().DynamicRatio;
            set
            {
                if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                    return;

                NativeClothWorking native = _rootBone.NativeArray[Index];
                native.DynamicRatio = value;
                _rootBone.NativeArray[Index] = native;
            }
        }

        public bool CheckCharaCollision
        {
            get => GetNative().CheckCharaCollision != 0;
            set
            {
                if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                    return;

                NativeClothWorking native = _rootBone.NativeArray[Index];
                native.CheckCharaCollision = value ? (byte)1 : (byte)0;
                _rootBone.NativeArray[Index] = native;
            }
        }

        public bool CheckEnvCollision
        {
            get
            {
                if (_isOverrideCheckEnvCollision)
                    return _isEnvCollision;
                return _dataChildElement != null && _dataChildElement.CheckEnvCollision;
            }
            set
            {
                _isOverrideCheckEnvCollision = true;
                _isEnvCollision = value;
            }
        }

        public CySpringBoneBase()
        {
            ShouldReflectCalcResult = true;
            ResetLinkShouldReflectCalcResult = false;
            _collisionIndexList = new List<int>(DEFAULT_COLLISION_NUM);
        }

        public CySpringBoneBase(string name) : this()
        {
            _boneName = name;
        }
        // 官方 Create：先绑定 root/rootBone/parent，确认 rootBone.ChildElementList 存在，
        // 再从 parent/root object 下按 _boneName 查找 GameObject，最后在 ChildElementList 里匹配当前参数元素。
        public void Create(CySpring root, CySpringRootBone rootBone, CySpringBoneBase parentBone, bool isAdd)
        {
            _root = root;
            _rootBone = rootBone;
            _parentBone = parentBone;
            _exist = false;

            if (_root == null || _rootBone == null)
                return;

            List<CySpringParamDataChildElement> childElementList = _rootBone.ChildElementList;
            if (childElementList == null)
                return;

            GameObject searchRoot = parentBone != null ? parentBone.GameObject : _root.RootObject;
            _gameObject = CySpring.FindGameObject(searchRoot, _boneName);

            if (_gameObject == null)
            {
                _exist = false;
                return;
            }

            _dataChildElement = FindChildElement(childElementList, _boneName);
            _transform = _gameObject.transform;
            _isAddSpring = isAdd;
            _exist = true;
        }
        /// 递归创建子骨骼链, 遍历 Transform 的所有子级,如果子级名称在配置的子元素列表中且不是碰撞体,则创建新的骨骼节点
        public void CreateChild(
            CySpring root,
            CySpringRootBone rootBone,
            CySpringBoneBase parentBone,
            Dictionary<string, int> collisionTableDic,
            bool isAdd)
        {
            Create(root, rootBone, parentBone, isAdd);

            if (!_exist || _transform == null || rootBone == null || rootBone.RootDataElement == null)
                return;

            List<CySpringParamDataChildElement> childElements = rootBone.RootDataElement.ChildElementList;
            if (childElements == null)
                return;

            int childCount = _transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = _transform.GetChild(i);
                if (child == null)
                    continue;

                string childName = child.name;
                if (collisionTableDic != null && collisionTableDic.ContainsKey(childName))
                    continue;

                if (FindChildElement(childElements, childName) == null)
                    continue;

                CySpringBoneBase childBone = new CySpringBoneBase(childName);
                childBone.ShouldReflectCalcResult = rootBone.ShouldReflectCalcResult;

                rootBone.ChildBoneList.Add(childBone);
                childBone.CreateChild(root, rootBone, this, collisionTableDic, isAdd);
            }
        }

        public virtual void Initialize(int idx, bool isEndBone)
        {
            Index = idx;

            if (_transform == null || _rootBone == null || _rootBone.NativeArray == null || (uint)idx >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[idx];

            _initRotationInv = Quaternion.Inverse(_transform.rotation);
            native.InitLocalRotation = _transform.localRotation;

            Vector3 worldPosition = _transform.position;
            native.TargetPosition = worldPosition;
            native.PrevTargetPosition = worldPosition;

            native.CheckCharaCollision = 1;
            native.DynamicRatio = 1.0f;
            native.LimitRotationMin = new Vector3(180f, 180f, 180f);
            native.LimitRotationMax = new Vector3(180f, 180f, 180f);

            FirstLocalPosition = Vector3.zero;
            FirstLocalMatrix = Matrix4x4.TRS(_transform.localPosition, _transform.localRotation, _transform.localScale);
            FirstPosition = worldPosition;

            // 官方 Initialize 是分段直接写 NativeArray。
            // 这里必须先把当前 bone 的基础字段写回，后面的 parent/leaf 分支会继续写 parent 或当前 native。
            _rootBone.NativeArray[idx] = native;

            if (_parentBone == null)
            {
                native = _rootBone.NativeArray[idx];
                native.BoneAxis = Vector3.up;
                native.InitBoneDistance = 1.0f;
                _rootBone.NativeArray[idx] = native;
            }
            else
            {
                InitializeParentBoneAxisAndDistance();
            }

            if (isEndBone)
                _needSimulateEndBone = _rootBone.RootDataElement.NeedSimulateEndBone;
            else
                _needSimulateEndBone = true;
        }

        private void InitializeParentBoneAxisAndDistance()
        {
            if (_parentBone == null || _parentBone.GameObject == null || _parentBone._rootBone == null)
                return;

            NativeClothWorking[] nativeArray = _parentBone._rootBone.NativeArray;
            if (nativeArray == null || (uint)_parentBone.Index >= (uint)nativeArray.Length)
                return;

            Transform parentTransform = _parentBone.GameObject.transform;
            if (parentTransform == null || _transform == null)
                return;

            Vector3 parentPosition = parentTransform.position;
            Vector3 selfPosition = _transform.position;
            Vector3 delta = selfPosition - parentPosition;

            FirstParentPosition = parentPosition;

            NativeClothWorking parentNative = nativeArray[_parentBone.Index];
            float distance = delta.magnitude;
            parentNative.InitBoneDistance = distance;
            _parentBone.FirstLocalPosition = _transform.localPosition;

            if (distance < MIN_BONE_DISTANCE)
            {
                _parentBone.Exist = false;
                _exist = false;
            }
            else
            {
                Vector3 axis = (_parentBone.InitRotationInv * delta).normalized;
                parentNative.BoneAxis = axis;
            }

            nativeArray[_parentBone.Index] = parentNative;

            if (_transform.childCount == 0 && _rootBone != null && _rootBone.NativeArray != null && (uint)Index < (uint)_rootBone.NativeArray.Length)
            {
                NativeClothWorking native = _rootBone.NativeArray[Index];
                native.BoneAxis = parentNative.BoneAxis;
                native.InitBoneDistance = parentNative.InitBoneDistance;
                _rootBone.NativeArray[Index] = native;
            }
        }

        public void UpdateScale(float scale)
        {
            // Official UpdateScale only updates the stored first-pose target positions and bone distances.
            // It does not re-apply the whole parameter set; ResetScale recalculates CollisionRadius separately.
            if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            Vector3 scaledFirstPosition = FirstPosition * scale;

            NativeClothWorking native = _rootBone.NativeArray[Index];
            native.TargetPosition = scaledFirstPosition;
            native.PrevTargetPosition = scaledFirstPosition;
            _rootBone.NativeArray[Index] = native;

            if (_parentBone == null)
            {
                native = _rootBone.NativeArray[Index];
                native.InitBoneDistance = scale;
                _rootBone.NativeArray[Index] = native;
                return;
            }

            if (_parentBone._rootBone == null || _parentBone._rootBone.NativeArray == null)
                return;

            NativeClothWorking[] parentNativeArray = _parentBone._rootBone.NativeArray;
            if ((uint)_parentBone.Index >= (uint)parentNativeArray.Length)
                return;

            Vector3 scaledFirstParentPosition = FirstParentPosition * scale;
            float distance = (scaledFirstPosition - scaledFirstParentPosition).magnitude;

            NativeClothWorking parentNative = parentNativeArray[_parentBone.Index];
            parentNative.InitBoneDistance = distance;
            parentNativeArray[_parentBone.Index] = parentNative;

            if (_transform != null && _transform.childCount == 0)
            {
                native = _rootBone.NativeArray[Index];
                native.InitBoneDistance = parentNative.InitBoneDistance;
                _rootBone.NativeArray[Index] = native;
            }
        }

        public virtual void Delete()
        {
            _root = null;
            _rootBone = null;
            _parentBone = null;
            _gameObject = null;
            _transform = null;
            _dataChildElement = null;
            _collisionIndexList?.Clear();
            _exist = false;
        }

        public void CreateCollisionIndexList()
        {
            _collisionIndexList = new List<int>(DEFAULT_COLLISION_NUM);

            if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];
            native.ActiveCollision = 0;

            // Official code only resets ActiveCollision here.
            // It does not clear CIndex0..CIndex15 to -1 in CreateCollisionIndexList().
            _rootBone.NativeArray[Index] = native;
        }

        protected void AddCollisionIndex(string colName)
        {
            if (_root == null || string.IsNullOrEmpty(colName))
                return;

            int index = _root.FindCollisionComponentIndex(colName);
            if (index != -1)
                AddCollisionIndex((short)index);
        }

        protected void AddCollisionIndex(int i)
        {
            if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            if (_collisionIndexList == null)
                _collisionIndexList = new List<int>(DEFAULT_COLLISION_NUM);

            // Official List<int> keeps every added id. The native struct only has 16 short slots,
            // but ActiveCollision still increments even after slot 15.
            _collisionIndexList.Add(i);

            NativeClothWorking native = _rootBone.NativeArray[Index];
            int slot = native.ActiveCollision;

            if (slot >= 0 && slot < DEFAULT_COLLISION_NUM)
                SetNativeCollisionIndex(ref native, slot, (short)i);

            native.ActiveCollision = slot + 1;
            _rootBone.NativeArray[Index] = native;
        }

        public void RegistCollisionToCloth()
        {
            CreateCollisionIndexList();

            if (_rootBone == null || _rootBone.ChildElementList == null)
                return;

            if (_dataChildElement != null)
            {
                List<string> collisionNames = _dataChildElement.CollisionNameList;
                if (collisionNames != null && _root != null)
                {
                    for (int i = 0; i < collisionNames.Count; i++)
                        AddCollisionIndex(collisionNames[i]);
                }

                bool checkEnv = _dataChildElement.CheckEnvCollision;
                if (_isOverrideCheckEnvCollision)
                    checkEnv = _isEnvCollision;

                // Official returns here when env collision is disabled. It does not inherit root
                // collision indices in the normal child-data path.
                if (!checkEnv || _root == null)
                    return;

                int charaCount = _root.CharaCollisionList != null ? _root.CharaCollisionList.Count : 0;
                var envList = _root.EnvCollisionList;
                if (envList == null)
                    return;

                for (int i = 0; i < envList.Count; i++)
                {
                    CySpringCollisionRuntimeData env = envList[i];
                    if (env != null && env.ShouldApplyToBone(this))
                        AddCollisionIndex(charaCount + i);
                }

                return;
            }

            // Official fallback path for bones without a child element: inherit root collision list.
            // When Index != 0 it first validates that ChildBoneList[Index - 1] exists.
            if (Index != 0)
            {
                List<CySpringBoneBase> childList = _rootBone.ChildBoneList;
                int childIndex = Index - 1;
                if (childList == null || (uint)childIndex >= (uint)childList.Count || childList[childIndex] == null)
                    return;
            }

            List<int> rootCollisionList = _rootBone.CollisionIndexList;
            if (rootCollisionList == null)
                return;

            for (int i = 0; i < rootCollisionList.Count; i++)
                AddCollisionIndex(rootCollisionList[i]);
        }

        public void SaveWorldRotation()
        {
            if (!_exist)
                return;

            if (_rootBone == null || _rootBone.NativeArray == null || _transform == null)
                return;

            if ((uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];
            native.AnimationRotation = _transform.localRotation;
            _rootBone.NativeArray[Index] = native;
        }

        public void PostSpring()
        {
            if (!_needSimulateEndBone || _root == null || _root.IsDeltaTimeZero || !ShouldReflectCalcResult)
                return;

            if (_transform == null || _rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];

            // 官方：先取 NativeClothWorking.FinalRotation
            Quaternion finalRotation = native.FinalRotation;

            // 官方：只有 BlendRate < 1.0f 时才 Lerp，并且 IsLimit 也在这个分支里面
            if (_root.BlendRate < 1.0f)
            {
                if (_rootBone.PrevFinalRotation != null &&
                    (uint)Index < (uint)_rootBone.PrevFinalRotation.Length)
                {
                    finalRotation = Quaternion.Lerp(_rootBone.PrevFinalRotation[Index],finalRotation,_root.BlendRate);
                }

                // 注意：官方 IsLimit 分支在 BlendRate < 1.0f 里面
                if (native.IsLimit != 0)
                {
                    finalRotation = ApplyLimitRotation(native, finalRotation);
                }
            }

            // 官方：写回 PrevFinalRotation
            if (_rootBone.PrevFinalRotation != null &&
                (uint)Index < (uint)_rootBone.PrevFinalRotation.Length)
            {
                _rootBone.PrevFinalRotation[Index] = finalRotation;
            }

            // 官方：Transform.set_rotation(finalRotation)
            _transform.rotation = finalRotation;
        }

        private static Quaternion ApplyLimitRotation(NativeClothWorking native, Quaternion finalRotation)
        {
            // Official PostSpring:
            // relative = final * inverse(parentRotation * initLocalRotation)
            // euler = RoundAngle(MakePositive(relative Euler))
            // clamp each axis by [-LimitRotationMin, LimitRotationMax]
            // final = parentRotation * (initLocalRotation * Euler(clamped))
            Quaternion baseRotation = native.ParentRotation * native.InitLocalRotation;
            Quaternion relative = finalRotation * Quaternion.Inverse(baseRotation);

            Vector3 euler = CySpringNative.RoundAngle(relative.eulerAngles);

            euler.x = Mathf.Clamp(euler.x, -native.LimitRotationMin.x, native.LimitRotationMax.x);
            euler.y = Mathf.Clamp(euler.y, -native.LimitRotationMin.y, native.LimitRotationMax.y);
            euler.z = Mathf.Clamp(euler.z, -native.LimitRotationMin.z, native.LimitRotationMax.z);

            euler = CySpringNative.RoundAngle(euler);
            Quaternion limitedLocal = Quaternion.Euler(euler);
            return native.ParentRotation * (native.InitLocalRotation * limitedLocal);
        }

        public virtual void ResetNativeCloth(ref Matrix4x4 matrix)
        {
            if (_transform == null)
                return;

            if (ResetLinkShouldReflectCalcResult && !ShouldReflectCalcResult)
                return;

            matrix = matrix * FirstLocalMatrix;

            Vector3 worldPosition = matrix.MultiplyPoint3x4(FirstLocalPosition);
            Quaternion localRotation = FirstLocalMatrix.rotation;
            Quaternion parentRotation = _transform.parent != null ? _transform.parent.rotation : Quaternion.identity;
            Quaternion finalRotation = parentRotation * localRotation;

            _transform.localRotation = localRotation;

            if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];
            native.SelfPosition = worldPosition;
            native.TargetPosition = worldPosition;
            native.PrevTargetPosition = worldPosition;
            native.ParentRotation = parentRotation;
            native.FinalRotation = finalRotation;
            native.AimVector = finalRotation * native.BoneAxis;
            _rootBone.NativeArray[Index] = native;
        }

        public CySpringSnapshotBoneData GetSnapshotData()
        {
            CySpringSnapshotBoneData snapshot = new CySpringSnapshotBoneData();
            snapshot.BoneName = _boneName;
            snapshot.Position = _transform != null ? _transform.position : Vector3.zero;
            snapshot.Rotation = _transform != null ? _transform.rotation : Quaternion.identity;
            snapshot.LocalRotation = _transform != null ? _transform.localRotation : Quaternion.identity;
            return snapshot;
        }

        public void SetSnapshotData(CySpringSnapshotBoneData snapshotData)
        {
            if (snapshotData == null || _transform == null)
                return;

            _transform.position = snapshotData.Position;
            _transform.rotation = snapshotData.Rotation;
        }

        public void MulGravityScale(float scale)
        {
            if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];
            native.Gravity = _initialGravity * scale;
            _rootBone.NativeArray[Index] = native;
        }

        public void AddGravityValue(float value)
        {
            if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];
            native.Gravity = _initialGravity + value;
            _rootBone.NativeArray[Index] = native;
        }

        public void SetGravityValue(float value)
        {
            if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];
            native.Gravity = value;
            _rootBone.NativeArray[Index] = native;
        }

        public void ChangeValue(float legacyScale)
        {
            if (_dataChildElement == null || _rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];
            native.CheckCharaCollision = 1;

            native.StiffnessForce = _dataChildElement.StiffnessForce;
            native.DragForce = _dataChildElement.DragForce;
            native.Gravity = _dataChildElement.Gravity;

            float scaleY = _transform != null ? _transform.lossyScale.y : 1.0f;
            native.CollisionRadius = (_dataChildElement.CollisionRadius / legacyScale) * scaleY;

            native.VerticalWindRateSlow = _dataChildElement.VerticalWindRateSlow;
            native.VerticalWindRateFast = _dataChildElement.VerticalWindRateFast;
            native.HorizontalWindRateSlow = _dataChildElement.HorizontalWindRateSlow;
            native.HorizontalWindRateFast = _dataChildElement.HorizontalWindRateFast;

            native.IsLimit = _dataChildElement.IsLimit ? (byte)1 : (byte)0;
            native.LimitRotationMin = _dataChildElement.LimitRotationMin;
            native.LimitRotationMax = _dataChildElement.LimitRotationMax;
            native.MoveSpringApplyRate = _dataChildElement.MoveSpringApplyRate;
            native.IsAddSpring = _isAddSpring ? 1 : 0;

            _initialGravity = _dataChildElement.Gravity;
            _rootBone.NativeArray[Index] = native;
        }

        public virtual void SetEnableCySpringBoneWind(bool enable)
        {
            if (_dataChildElement == null || _rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];

            native.VerticalWindRateSlow = enable ? _dataChildElement.VerticalWindRateSlow : 0.0f;
            native.VerticalWindRateFast = enable ? _dataChildElement.VerticalWindRateFast : 0.0f;
            native.HorizontalWindRateSlow = enable ? _dataChildElement.HorizontalWindRateSlow : 0.0f;
            native.HorizontalWindRateFast = enable ? _dataChildElement.HorizontalWindRateFast : 0.0f;

            _rootBone.NativeArray[Index] = native;
        }

        public virtual void ResetScale(float bodyScale, bool isUpdateScale)
        {
            if (_rootBone == null || _rootBone.ChildElementList == null || _dataChildElement == null)
                return;

            if (isUpdateScale)
                UpdateScale(bodyScale);

            if (_rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length || _transform == null)
                return;

            NativeClothWorking native = _rootBone.NativeArray[Index];
            native.CollisionRadius = (_dataChildElement.CollisionRadius / bodyScale) * _transform.lossyScale.y;
            _rootBone.NativeArray[Index] = native;
        }

        public void ResetConnectedForce()
        {
            // Official writes connected force to the previous native slot, not this bone's own slot.
            if (Index == 0 || _rootBone == null || _rootBone.NativeArray == null)
                return;

            int targetIndex = Index - 1;
            if ((uint)targetIndex >= (uint)_rootBone.NativeArray.Length)
                return;

            NativeClothWorking native = _rootBone.NativeArray[targetIndex];
            native.ConnectedForce = Vector3.zero;
            _rootBone.NativeArray[targetIndex] = native;
        }

        public void UpdateConnectedForce(Vector3 pairPos, float defaultDistance, float intensity)
        {
            // Official accumulates the force into the previous native slot (Index - 1).
            if (Index == 0 || _transform == null || _rootBone == null || _rootBone.NativeArray == null)
                return;

            int targetIndex = Index - 1;
            if ((uint)targetIndex >= (uint)_rootBone.NativeArray.Length)
                return;

            Vector3 delta = pairPos - _transform.position;
            float distance = delta.magnitude;
            Vector3 force = delta.normalized * ((distance - defaultDistance) * intensity);

            NativeClothWorking native = _rootBone.NativeArray[targetIndex];
            native.ConnectedForce += force;
            _rootBone.NativeArray[targetIndex] = native;
        }

        public bool FindCySpringBone(List<CySpringBoneBase> resultBoneList, Func<string, bool> checkFunc, bool firstHitBreak)
        {
            if (resultBoneList == null || checkFunc == null)
                return false;

            if (checkFunc(_boneName))
            {
                resultBoneList.Add(this);
                return true;
            }

            return false;
        }

        private NativeClothWorking GetNative()
        {
            if (_rootBone == null || _rootBone.NativeArray == null || (uint)Index >= (uint)_rootBone.NativeArray.Length)
                return default;
            return _rootBone.NativeArray[Index];
        }

        private static CySpringParamDataChildElement FindChildElement(List<CySpringParamDataChildElement> list, string name)
        {
            if (list == null || string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                CySpringParamDataChildElement element = list[i];
                if (element != null && string.Equals(element.Name, name, StringComparison.Ordinal))
                    return element;
            }

            return null;
        }

        private static void SetNativeCollisionIndex(ref NativeClothWorking native, int slot, short value)
        {
            switch (slot)
            {
                case 0: native.CIndex0 = value; break;
                case 1: native.CIndex1 = value; break;
                case 2: native.CIndex2 = value; break;
                case 3: native.CIndex3 = value; break;
                case 4: native.CIndex4 = value; break;
                case 5: native.CIndex5 = value; break;
                case 6: native.CIndex6 = value; break;
                case 7: native.CIndex7 = value; break;
                case 8: native.CIndex8 = value; break;
                case 9: native.CIndex9 = value; break;
                case 10: native.CIndex10 = value; break;
                case 11: native.CIndex11 = value; break;
                case 12: native.CIndex12 = value; break;
                case 13: native.CIndex13 = value; break;
                case 14: native.CIndex14 = value; break;
                case 15: native.CIndex15 = value; break;
            }
        }
    }
}
