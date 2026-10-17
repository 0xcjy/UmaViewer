using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Gallop
{
/// 此类负责管理官方的高层执行顺序：
/// Bind/Create -> SetNativeCollision -> SetNativeCloth -> ResetNativeCloth -> SetConnectedBone
/// GatherSpring -> UpdateSpring/UpdateSpringHasWind -> PostSpring
///
/// 每根骨骼对应的原生缓冲区布局由CySpringRootBone/CySpringBoneBase负责处理
/// 实际求解过程通过CySpringNative.UpdateNativeCloth委托给CySpringPlugin.dll
    public class CySpring
    {
        private const float HORIZONTAL_WIND_RATE = 0.005f;
        private const float VERTICAL_WIND_RATE = 0.015f;

        public const int CHARA_COLLISION_MAX_NUM = 10;
        private const int ENV_COLLISION_MAX_NUM = 3;

        private static float _dragForceRate = 1.0f;
        private static float _gravityRate = 1.0f;

        private float _stiffnessForceRate = 1.0f;

        protected string[] _ignoredSpringRootBoneArray;

        private CySpringParamDataAsset _springData;
        private ICySpringWindParamData _mainWindParamData;
        private WindControlData _mainWindControlData;

        private CySpringParamDataAsset _addData;
        private ICySpringWindParamData _addWindParamData;
        private WindControlData _addWindControlData;

        private float _blendRate = 1.0f;

        private List<CySpringCollisionRuntimeData> _charaCollisionList;
        private CySpringCollisionRuntimeData[] _charaCollisionArray;
        private Dictionary<string, int> _charaCollisionTableDic;

        private List<CySpringCollisionRuntimeData> _envCollisionList;
        private CySpringCollisionRuntimeData[] _envCollisionArray;
        private NativeClothCollision[] _nativeCollisionArray;

        private bool _isDeltaTimeZero = true;
        private RootParentWork _rootParentWork;
        private bool _validEnvCollision;

        private CySpringRootBone[] _rootBoneArray;
        private Transform _rootTransform;
        private GameObject _rootObject;

        public Vector3 CurrentCharaPosition;

        private float _windTime;
        private SkirtController _skirtController;
        private bool _isForceDisableHipMoveParam;

        private List<ConnectedBonePair> _connectedBoneList;
        private bool _isEnableConnectedBone = true;

        public CySpringParamDataAsset SpringData
        {
            get => _springData;
            set => _springData = value;
        }

        public ICySpringWindParamData MainWindParamData => _mainWindParamData;

        public CySpringParamDataAsset SpringAddData
        {
            get => _addData;
            set => _addData = value;
        }

        public ICySpringWindParamData AddWindParamData => _addWindParamData;

        public bool ExistAddWindParamData => _addWindParamData != null;

        public float BlendRate
        {
            get => _blendRate;
            set => _blendRate = value;
        }

        public ReadOnlyCollection<CySpringCollisionRuntimeData> CharaCollisionList
            => _charaCollisionList != null ? _charaCollisionList.AsReadOnly() : Array.AsReadOnly(Array.Empty<CySpringCollisionRuntimeData>());

        public ReadOnlyCollection<CySpringCollisionRuntimeData> EnvCollisionList
            => _envCollisionList != null ? _envCollisionList.AsReadOnly() : Array.AsReadOnly(Array.Empty<CySpringCollisionRuntimeData>());

        public bool IsDeltaTimeZero => _isDeltaTimeZero;

        public RootParentWork ParentWork
        {
            get => _rootParentWork;
            set => _rootParentWork = value;
        }

        public CySpringRootBone[] RootBoneArray => _rootBoneArray;
        public GameObject RootObject => _rootObject;
        public Transform RootTransform => _rootTransform;

        public bool IsForceDisableHipMoveParam
        {
            get => _isForceDisableHipMoveParam;
            set => _isForceDisableHipMoveParam = value;
        }

        public CySpring(GameObject rootObject)
        {
            Initialize(rootObject);
        }

        // 初始化所有内部列表和默认值
        private void Initialize(GameObject rootObject)
        {
            _rootObject = rootObject;
            _rootTransform = rootObject != null ? rootObject.transform : null;
            _charaCollisionList = new List<CySpringCollisionRuntimeData>(CHARA_COLLISION_MAX_NUM);
            _envCollisionList = new List<CySpringCollisionRuntimeData>(ENV_COLLISION_MAX_NUM);
            _connectedBoneList = new List<ConnectedBonePair>();
            _rootParentWork = new RootParentWork();
            _blendRate = 1.0f;
            _stiffnessForceRate = 1.0f;
            _isDeltaTimeZero = true;
            _validEnvCollision = false;
            _windTime = 0.0f;
        }

        public void SetStiffnessForceRate(float rate)
        {
            _stiffnessForceRate = rate;
        }

        public int FindCollisionComponentIndex(string name)
        {
            if (string.IsNullOrEmpty(name) || _charaCollisionTableDic == null)
                return -1;

            return _charaCollisionTableDic.TryGetValue(name, out int index) ? index : -1;
        }

        public void SetSkirtController(SkirtController ctrl)
        {
            _skirtController = ctrl;
        }

        //收集所有根骨骼和碰撞体的父级变换信息，并构建 ParentWork 索引
        //这些索引会在后续 GatherSpring 时用于将世界空间父级姿态传递给原生布料工作数组
        public void CollectRootParentInfo(RootParentInfo rootInfo)
        {
            if (rootInfo == null)
                return;

            // 1. RootBone -> NativeClothWorking.ParentWorkIndex
            if (_rootBoneArray != null)
            {
                for (int i = 0; i < _rootBoneArray.Length; i++)
                {
                    CySpringRootBone root = _rootBoneArray[i];
                    if (root == null || root.NativeArray == null)
                        continue;

                    Transform parent = root.Parent;
                    if (parent == null)
                        continue;

                    int parentIndex = rootInfo.GetOrAddParent(parent);
                    SetNativeClothParentWorkIndex(root.NativeArray, root.Index, parentIndex);
                }
            }

             // 2. 角色碰撞体的父节点 -> NativeClothCollision.ParentWorkIndex
            if (_charaCollisionArray != null && _nativeCollisionArray != null)
            {
                for (int i = 0; i < _charaCollisionArray.Length; i++)
                {
                    if ((uint)i >= (uint)_nativeCollisionArray.Length)
                        break;

                    CySpringCollisionRuntimeData runtime = _charaCollisionArray[i];
                    Transform t = runtime != null ? runtime.TargetTransform : null;
                    if (t == null)
                        continue;

                    Transform parent = t.parent;
                    if (parent == null)
                        continue;

                    int parentIndex = rootInfo.GetOrAddParent(parent);
                    _nativeCollisionArray[i].ParentWorkIndex = parentIndex;
                }
            }

            // 3. 环境碰撞体的父节点 -> NativeClothCollision.ParentWorkIndex
            if (_envCollisionArray != null && _nativeCollisionArray != null)
            {
                int charaCount = _charaCollisionArray != null ? _charaCollisionArray.Length : 0;

                for (int i = 0; i < _envCollisionArray.Length; i++)
                {
                    int nativeIndex = charaCount + i;
                    if ((uint)nativeIndex >= (uint)_nativeCollisionArray.Length)
                        break;

                    CySpringCollisionRuntimeData runtime = _envCollisionArray[i];
                    Transform t = runtime != null ? runtime.TargetTransform : null;
                    if (t == null)
                        continue;

                    int parentIndex = rootInfo.GetOrAddParent(t);
                    _nativeCollisionArray[nativeIndex].ParentWorkIndex = parentIndex;
                }
            }
        }

        private static void SetNativeClothParentWorkIndex(
            NativeClothWorking[] nativeArray,
            int nativeIndex,
            int parentWorkIndex)
        {
            if (nativeArray == null)
                return;

            if ((uint)nativeIndex >= (uint)nativeArray.Length)
                return;

            if (parentWorkIndex < 0)
                return;

            NativeClothWorking native = nativeArray[nativeIndex];
            native.ParentWorkIndex = parentWorkIndex;
            nativeArray[nativeIndex] = native;
        }

        

        public class RootParentWork
        {
            public int Num;
            public NativeRootParentWork[] NativeParentWorkArray;
            public Transform[] ParentTransformArray;

            public void GatherPosture()
            {
                if (NativeParentWorkArray == null || ParentTransformArray == null)
                    return;

                int count = Mathf.Min(Num, Mathf.Min(NativeParentWorkArray.Length, ParentTransformArray.Length));

                for (int i = 0; i < count; i++)
                {
                    Transform t = ParentTransformArray[i];
                    if (t == null)
                        continue;

                    NativeParentWorkArray[i].WorldPosition = t.position;
                    NativeParentWorkArray[i].WorldRotation = t.rotation;
                }
            }
        }

        public class RootParentInfo
        {
            private const int TableNum = 16;
            public Dictionary<Transform, int> ParentDic = new Dictionary<Transform, int>(TableNum);

            public int GetOrAddParent(Transform parent)
            {
                if (parent == null)
                    return -1;

                if (ParentDic.TryGetValue(parent, out int index))
                    return index;

                index = ParentDic.Count;
                ParentDic.Add(parent, index);
                return index;
            }

            public static RootParentWork MakeRootParentWork(RootParentInfo info)
            {
                RootParentWork work = new RootParentWork();

                if (info == null || info.ParentDic == null || info.ParentDic.Count == 0)
                {
                    work.Num = 0;
                    work.NativeParentWorkArray = Array.Empty<NativeRootParentWork>();
                    work.ParentTransformArray = Array.Empty<Transform>();
                    return work;
                }

                work.Num = info.ParentDic.Count;
                work.NativeParentWorkArray = new NativeRootParentWork[work.Num];
                work.ParentTransformArray = new Transform[work.Num];

                foreach (var pair in info.ParentDic)
                {
                    Transform t = pair.Key;
                    int index = pair.Value;

                    if ((uint)index >= (uint)work.Num)
                        continue;

                    work.ParentTransformArray[index] = t;

                    if (t != null)
                    {
                        work.NativeParentWorkArray[index].WorldPosition = t.position;
                        work.NativeParentWorkArray[index].WorldRotation = t.rotation;
                    }
                }

                return work;
            }
        }

        public void SetSuppression(bool enable, float length)
        {
            if (_rootBoneArray == null)
                return;

            for (int i = 0; i < _rootBoneArray.Length; i++)
            {
                var root = _rootBoneArray[i];
                if (root == null)
                    continue;

                root.IsSuppression = enable;
                root.SuppressionLength = length;
            }
        }

        /// <summary>
        /// Official initialization order reconstructed from CySpring.Bind.
        /// </summary>
        public void Bind(
            CySpringParamDataAsset springData,
            CySpringParamDataAsset addData,
            float legacyScale,
            float addLegacyScale,
            List<CySpringCollisionRuntimeData> runtimeCollisionList,
            string[] ignoredSpringRootBoneArray)
        {
            _springData = springData;
            _addData = addData;
            _ignoredSpringRootBoneArray = ignoredSpringRootBoneArray;

            Create(legacyScale, addLegacyScale, runtimeCollisionList);

            // Official helper: _mainWindParamData = _springData, _addWindParamData = _addData.
            SetWindData();

            SetNativeCollision();
            SetNativeCloth(legacyScale, addLegacyScale);

            ResetNativeCloth();
            SetConnectedBone();
        }

        public void Delete()
        {
            if (_rootBoneArray != null)
            {
                for (int i = 0; i < _rootBoneArray.Length; i++)
                    _rootBoneArray[i]?.Delete();
            }

            _rootBoneArray = null;
            _nativeCollisionArray = null;
            _charaCollisionArray = null;
            _envCollisionArray = null;
            _charaCollisionTableDic = null;
            _connectedBoneList?.Clear();
        }

        private void Create(float legacyScale, float addLegacyScale, List<CySpringCollisionRuntimeData> runtimeDataList)
        {
            _isDeltaTimeZero = true;

            _charaCollisionList = runtimeDataList ?? new List<CySpringCollisionRuntimeData>(CHARA_COLLISION_MAX_NUM);
            _charaCollisionArray = _charaCollisionList.ToArray();
            _charaCollisionTableDic = new Dictionary<string, int>(_charaCollisionArray.Length);

            for (int i = 0; i < _charaCollisionArray.Length; i++)
            {
                var runtime = _charaCollisionArray[i];
                if (runtime == null)
                    continue;

                string key = runtime.Name;
                if (!string.IsNullOrEmpty(key) && !_charaCollisionTableDic.ContainsKey(key))
                    _charaCollisionTableDic.Add(key, i);
            }

            _envCollisionList = new List<CySpringCollisionRuntimeData>(ENV_COLLISION_MAX_NUM);
            _envCollisionArray = _envCollisionList.ToArray();
            _validEnvCollision = _envCollisionArray != null && _charaCollisionArray != null;

            if (_springData == null)
                return;

            List<CySpringRootBone> rootBones = new List<CySpringRootBone>();

            IList<CySpringParamDataElement> mainElements = GetElementList(_springData);
            int mainCount = mainElements != null ? mainElements.Count : 0;

            for (int i = 0; i < mainCount; i++)
            {
                CySpringParamDataElement element = mainElements[i];
                float phase = mainCount > 0 ? ((float)i / mainCount) * Mathf.PI * 0.5f : 0.0f;
                CreateBone(rootBones, element, legacyScale, phase, false);
            }

            if (_addData != null)
            {
                IList<CySpringParamDataElement> addElements = GetElementList(_addData);
                int addCount = addElements != null ? addElements.Count : 0;

                for (int i = 0; i < addCount; i++)
                {
                    CySpringParamDataElement addElement = addElements[i];
                    if (addElement == null)
                        continue;

                    string addName = addElement.Name;
                    bool existsInMain = false;

                    for (int j = 0; j < mainCount; j++)
                    {
                        CySpringParamDataElement mainElement = mainElements[j];
                        if (mainElement != null && string.Equals(addName, mainElement.Name, StringComparison.Ordinal))
                        {
                            existsInMain = true;
                            break;
                        }
                    }

                    if (existsInMain)
                        continue;

                    float phase = addCount > 0 ? ((float)i / addCount) * Mathf.PI * 0.5f : 0.0f;
                    CreateBone(rootBones, addElement, addLegacyScale, phase, true);
                }
            }

            _rootBoneArray = rootBones.ToArray();
        }

        private void CreateBone(
            List<CySpringRootBone> boneList,
            CySpringParamDataElement element,
            float legacyScale,
            float windPhaseShift,
            bool isAdd)
        {
            if (boneList == null || element == null)
                return;

            CySpringRootBone rootBone = new CySpringRootBone();
            rootBone.ShouldReflectCalcResult = true;

            string rootName = element.Name;
            if (_ignoredSpringRootBoneArray != null && !string.IsNullOrEmpty(rootName))
            {
                for (int i = 0; i < _ignoredSpringRootBoneArray.Length; i++)
                {
                    string ignored = _ignoredSpringRootBoneArray[i];
                    if (!string.IsNullOrEmpty(ignored) && rootName.IndexOf(ignored, StringComparison.Ordinal) == 0)
                    {
                        rootBone.ShouldReflectCalcResult = false;
                        break;
                    }
                }
            }

            rootBone.CreateRoot(element, this, rootBone, null, legacyScale, _charaCollisionTableDic, isAdd);

            if (rootBone.Exist)
            {
                rootBone.WindPhaseShift = windPhaseShift;
                boneList.Add(rootBone);
            }
        }

        /// <summary>
        /// Rebuilds the native collision array in the official order:
        /// [0..charaCount-1] chara collisions, then [charaCount..] env collisions.
        /// </summary>
        private void InitNativeCollisions()
        {
            int charaCount = _charaCollisionArray != null ? _charaCollisionArray.Length : 0;
            int envCount = _envCollisionArray != null ? _envCollisionArray.Length : 0;

            _nativeCollisionArray = new NativeClothCollision[charaCount + envCount];

            NativeRootParentWork[] parentArray = _rootParentWork != null ? _rootParentWork.NativeParentWorkArray : null;

            for (int i = 0; i < charaCount; i++)
            {
                CySpringCollisionRuntimeData runtime = _charaCollisionArray[i];
                if (runtime == null)
                    continue;

                runtime.UpdateNativeCollision(
                    ref _nativeCollisionArray[i],
                    ref CurrentCharaPosition,
                    parentArray,
                    true,
                    1.0f);
            }

            for (int i = 0; i < envCount; i++)
            {
                CySpringCollisionRuntimeData runtime = _envCollisionArray[i];
                if (runtime == null)
                    continue;

                int nativeIndex = charaCount + i;
                runtime.UpdateNativeCollisionForEnv(
                    ref _nativeCollisionArray[nativeIndex],
                    ref CurrentCharaPosition,
                    parentArray);
            }
        }

        private void SetNativeCollision()
        {
            if (_rootBoneArray == null)
                return;

            InitNativeCollisions();

            for (int i = 0; i < _rootBoneArray.Length; i++)
                _rootBoneArray[i]?.SetNativeCollisions();
        }

        private void SetNativeCloth(float legacyScale, float addLegacyScale)
        {
            _isDeltaTimeZero = true;

            if (_rootBoneArray == null)
                return;

            for (int i = 0; i < _rootBoneArray.Length; i++)
            {
                CySpringRootBone root = _rootBoneArray[i];
                if (root == null)
                    continue;

                // CySpringRootBone in this reconstructed project currently keeps the scale argument.
                // Official CySpring.SetNativeCloth loops root.SetNativeCloth(root.RootDataElement).
                root.SetNativeCloth(root.RootDataElement, root.IsAddSpring ? addLegacyScale : legacyScale);
            }
        }

        public void ApplyEnvCollision(CySpringCollisionRuntimeData[] envCollisionArray)
        {
            _envCollisionList = envCollisionArray != null
                ? new List<CySpringCollisionRuntimeData>(envCollisionArray)
                : new List<CySpringCollisionRuntimeData>(ENV_COLLISION_MAX_NUM);

            _envCollisionArray = _envCollisionList.ToArray();
            _validEnvCollision = _envCollisionArray.Length > 0;
            SetNativeCollision();
        }

        public void UpdateNativeCollision(float legacyScale)
        {
            if (_charaCollisionArray == null || _nativeCollisionArray == null)
                return;

            NativeRootParentWork[] parentArray = _rootParentWork != null ? _rootParentWork.NativeParentWorkArray : null;

            for (int i = 0; i < _charaCollisionArray.Length; i++)
            {
                CySpringCollisionRuntimeData runtime = _charaCollisionArray[i];
                if (runtime == null || i >= _nativeCollisionArray.Length)
                    continue;

                runtime.UpdateNativeCollision(
                    ref _nativeCollisionArray[i],
                    ref CurrentCharaPosition,
                    parentArray,
                    true,
                    legacyScale);
            }
        }

        public void GatherSpring(float deltaTime, float scale, float legacyScale, float windTimeScale = 1f, bool isUpdateScale = false)
        {
            if (_rootBoneArray == null)
                return;

            // Gallop.Math.IsFloatEqualLight(deltaTime, 0.0f)
            _isDeltaTimeZero = Gallop.Math.IsFloatEqualLight(deltaTime, 0.0f);

            // 只有 flag == true 时才更新 Native Collision Env
            if (isUpdateScale)
            {
                UpdateEnvNativeCollisions();
            }

            NativeRootParentWork[] parentArray = _rootParentWork != null ? _rootParentWork.NativeParentWorkArray : null;

            if (parentArray == null)
                throw new NullReferenceException();

            for (int i = 0; i < _rootBoneArray.Length; i++)
            {
                if (_rootBoneArray[i] == null)
                    throw new NullReferenceException();

                _rootBoneArray[i].GatherRootSpring(parentArray, scale);
            }

            UpdateConnectedBone();

            _windTime += deltaTime * windTimeScale;
        }

        private void UpdateEnvNativeCollisions()
        {
            if (!_validEnvCollision || _envCollisionArray == null || _nativeCollisionArray == null)
                return;

            int charaCount = _charaCollisionArray != null ? _charaCollisionArray.Length : 0;
            NativeRootParentWork[] parentArray = _rootParentWork != null ? _rootParentWork.NativeParentWorkArray : null;

            for (int i = 0; i < _envCollisionArray.Length; i++)
            {
                int nativeIndex = charaCount + i;
                if ((uint)nativeIndex >= (uint)_nativeCollisionArray.Length)
                    continue;

                CySpringCollisionRuntimeData runtime = _envCollisionArray[i];
                if (runtime == null)
                    continue;

                runtime.UpdateNativeCollisionForEnv(
                    ref _nativeCollisionArray[nativeIndex],
                    ref CurrentCharaPosition,
                    parentArray);
            }
        }

        public void UpdateSpring(float hipMoveDistance, float timescale = 1f, float springRate = 1f, bool calc60FPS = false)
        {
            float mainHipMoveRate = GetHipMoveRate(_springData, hipMoveDistance);
            float addHipMoveRate = GetHipMoveRate(_addData, hipMoveDistance);

            if (_rootBoneArray == null)
                return;

            NativeRootParentWork[] parentArray = _rootParentWork != null ? _rootParentWork.NativeParentWorkArray : null;

            for (int i = 0; i < _rootBoneArray.Length; i++)
            {
                CySpringRootBone root = _rootBoneArray[i];
                if (root == null)
                    continue;

                CySpringNative.UpdateNativeCloth(
                    root.NativeArray,
                    root.TrueNativesCount,
                    _nativeCollisionArray,
                    root.LinkSkirtIndex,
                    _skirtController,
                    parentArray,
                    _stiffnessForceRate,
                    _dragForceRate,
                    _gravityRate,
                    0.0f,
                    0.0f,
                    0.0f,
                    0.0f,
                    true,
                    timescale,
                    calc60FPS,
                    mainHipMoveRate,
                    addHipMoveRate,
                    springRate);
            }
        }

        public void UpdateSpringHasWind(float hipMoveDistance, float windPowerRate, float timescale = 1f, bool calc60FPS = false, float springRate = 1f)
        {
            if (_rootBoneArray == null)
                return;

            windPowerRate = Mathf.Clamp01(windPowerRate);

            float mainHipMoveRate = GetHipMoveRate(_springData, hipMoveDistance);
            float addHipMoveRate = GetHipMoveRate(_addData, hipMoveDistance);

            bool hasAddWind = _addWindControlData != null && _addWindParamData != null;
            NativeRootParentWork[] parentArray = _rootParentWork != null ? _rootParentWork.NativeParentWorkArray : null;

            for (int i = 0; i < _rootBoneArray.Length; i++)
            {
                CySpringRootBone root = _rootBoneArray[i];
                if (root == null)
                    continue;

                WindControlData windData = (hasAddWind && root.IsAddSpring) ? _addWindControlData : _mainWindControlData;
                Vector3 wind = Vector3.zero;

                if (windData != null)
                    wind = CalculateWind(root, windData);

                CySpringNative.UpdateNativeCloth(
                    root.NativeArray,
                    root.TrueNativesCount,
                    _nativeCollisionArray,
                    root.LinkSkirtIndex,
                    _skirtController,
                    parentArray,
                    _stiffnessForceRate,
                    _dragForceRate,
                    _gravityRate,
                    wind.x,
                    wind.y,
                    wind.z,
                    windPowerRate,
                    true,
                    timescale,
                    calc60FPS,
                    mainHipMoveRate,
                    addHipMoveRate,
                    springRate);
            }
        }

        private Vector3 CalculateWind(CySpringRootBone root, WindControlData windData)
        {
            Vector3 raw = Vector3.zero;

            bool enableVertical = windData.EnableVerticalWind;
            bool enableHorizontal = windData.EnableHorizontalWind;

            if (!enableVertical && _springData != null)
                enableVertical = _springData.EnableVerticalWind;
            if (!enableHorizontal && _springData != null)
                enableHorizontal = _springData.EnableHorizontalWind;

            if (enableVertical)
            {
                float t = Mathf.Clamp01(WindWave(windData.VerticalWindPhase, root != null ? root.WindPhaseShift : 0.0f) * 0.5f + 0.5f);
                raw += Vector3.Lerp(windData.UpperWindDir, windData.DownerWindDir, t);
            }

            if (enableHorizontal)
            {
                float t = Mathf.Clamp01(WindWave(windData.HorizontalWindPhase, root != null ? root.WindPhaseShift : 0.0f) * 0.5f + 0.5f);
                raw += Vector3.Lerp(windData.RightWindDir, windData.LeftWindDir, t);
            }

            if (raw.sqrMagnitude <= float.Epsilon)
                return Vector3.zero;

            Vector3 normalized = raw.normalized;

            // Official UpdateSpringHasWind projects the normalized wind onto XZ,
            // normalizes that horizontal vector again, then uses raw normalized.y for vertical wind.
            Vector3 horizontal = new Vector3(normalized.x, 0.0f, normalized.z).normalized;
            float power = windData.WindPowerScale;

            return new Vector3(
                horizontal.x * power * HORIZONTAL_WIND_RATE,
                normalized.y * power * VERTICAL_WIND_RATE,
                horizontal.z * power * HORIZONTAL_WIND_RATE);
        }

        /// <summary>
        /// Official UpdateSpringHasWind calls the CRT/Unity sinf helper and then maps
        /// the result from [-1, 1] to [0, 1]. CreateWindControlData already bakes
        /// _windTime into the phase, so only the per-root phase shift is added here.
        /// </summary>
        private static float WindWave(float phase, float rootPhase)
        {
            return Mathf.Sin(phase + rootPhase);
        }

        private float GetHipMoveRate(CySpringParamDataAsset springData, float hipMoveDistance)
        {
            // Official behavior:
            // null / disabled / force-disabled returns -1.0f, not 1.0f.
            if (springData == null || !springData.IsEnableHipMoveParam || _isForceDisableHipMoveParam)
                return -1.0f;

            if (springData.HipMoveInfluenceDistance >= hipMoveDistance)
                return 1.0f;

            if (hipMoveDistance <= springData.HipMoveInfluenceMaxDistance)
            {
                if (_springData == null)
                    throw new NullReferenceException();

                return 1.0f - (
                    (springData.HipMoveInfluenceMaxDistance - hipMoveDistance) /
                    (springData.HipMoveInfluenceMaxDistance - _springData.HipMoveInfluenceDistance));
            }

            return 0.0f;
        }

        public void SetupWind(
            CySpringController.Parts parts,
            CySpringWindParam windParam,
            float centerWindAngle,
            CySpringWindParam addWindParam,
            float addCenterWindAngle)
        {
            _mainWindControlData = CreateWindControlData(windParam, parts, centerWindAngle);
            _addWindControlData = _addWindParamData != null
                ? CreateWindControlData(addWindParam, parts, addCenterWindAngle)
                : null;
        }

        private WindControlData CreateWindControlData(CySpringWindParam windParam, CySpringController.Parts parts, float centerWindAngle)
        {
            if (windParam == null)
                return null;

            Vector3 direction = windParam.Direction;
            Vector3 right = windParam.Right;

            Quaternion rootLocalRotation =
                _rootTransform != null ? _rootTransform.localRotation : Quaternion.identity;

            windParam.GetDirection(rootLocalRotation, ref direction, ref right);

            float center = -centerWindAngle;
            float vHalf = windParam.VerticalAngleWidth * 0.5f;
            float hHalf = windParam.HorizontalAngleWidth * 0.5f;

            Vector3 upperWindDir =
                Quaternion.AngleAxis(center + vHalf, right) * direction;

            Vector3 downerWindDir =
                Quaternion.AngleAxis(center - vHalf, right) * direction;

            Vector3 rightWindDir =
                Quaternion.AngleAxis(hHalf, Vector3.up) * direction;

            Vector3 leftWindDir =
                Quaternion.AngleAxis(-hHalf, Vector3.up) * direction;

            // Official code directly divides by the configured cycle. It does not apply
            // a zero/epsilon guard here; bad data should behave the same as the game.
            float verticalWindPhase = (_windTime * Mathf.PI * 2.0f) / windParam.VerticalCycle;
            float horizontalWindPhase = (_windTime * Mathf.PI * 2.0f) / windParam.HorizontalCycle;

            int partIndex = (int)parts;
            float windPowerScale = 1.0f;

            if (windParam.PowerScaleArray != null &&
                partIndex >= 0 &&
                partIndex < windParam.PowerScaleArray.Length)
            {
                windPowerScale = windParam.PowerScaleArray[partIndex];
            }

            return new WindControlData(
                upperWindDir,
                downerWindDir,
                rightWindDir,
                leftWindDir,
                verticalWindPhase,
                horizontalWindPhase,
                windPowerScale,
                windParam.IsEnableVertical,
                windParam.IsEnableHorizontal);
        }

        public void PostSpring()
        {
            if (_rootBoneArray == null)
                return;

            for (int i = 0; i < _rootBoneArray.Length; i++)
                _rootBoneArray[i]?.PostAllSpring();
        }

        public void ResetNativeCloth()
        {
            if (_rootBoneArray == null || _rootBoneArray.Length == 0)
                return;

            for (int i = 0; i < _rootBoneArray.Length; i++)
                _rootBoneArray[i]?.ResetNativeCloth();

            for (int i = 0; i < _rootBoneArray.Length; i++)
                _rootBoneArray[i]?.SetTrueNativesCount();
        }

        public void ResetScale(float bodyScale, float addScale, bool isUpdateScale)
        {
            if (_rootBoneArray == null)
                return;

            for (int i = 0; i < _rootBoneArray.Length; i++)
            {
                CySpringRootBone root = _rootBoneArray[i];
                if (root == null)
                    continue;

                root.ResetScale(root.IsAddSpring ? addScale : bodyScale, isUpdateScale);
            }
        }

        private void ResetTrueNativeCount()
        {
            if (_rootBoneArray == null)
                return;

            for (int i = 0; i < _rootBoneArray.Length; i++)
                _rootBoneArray[i]?.SetTrueNativesCount();
        }

        public void SetEnableCySpringBone(bool enable, string targetRoot, bool isPrefix = false)
        {
            CySpringRootBone[] roots = FindRootCySpringBones(targetRoot, isPrefix);
            if (roots == null)
                return;

            for (int i = 0; i < roots.Length; i++)
                roots[i]?.SetRootShouldReflectCalcResult(enable);
        }

        public void SetResetLinkCySpringBone(bool enable, string targetRootBoneName)
        {
            CySpringRootBone[] roots = FindRootCySpringBones(targetRootBoneName, false);
            if (roots == null)
                return;

            for (int i = 0; i < roots.Length; i++)
                roots[i]?.SetResetLink(enable);
        }

        public void DisableAllCySpringBones()
        {
            if (_rootBoneArray == null)
                return;

            for (int i = 0; i < _rootBoneArray.Length; i++)
                _rootBoneArray[i]?.SetRootShouldReflectCalcResult(false);
        }

        public void EnableAllCySpringBones()
        {
            if (_rootBoneArray == null)
                return;

            for (int i = 0; i < _rootBoneArray.Length; i++)
                _rootBoneArray[i]?.SetRootShouldReflectCalcResult(true);
        }

        public CySpringRootBone[] FindRootCySpringBones(string targetRoot, bool isPrefix = false)
        {
            if (_rootBoneArray == null || string.IsNullOrEmpty(targetRoot))
                return Array.Empty<CySpringRootBone>();

            List<CySpringRootBone> result = new List<CySpringRootBone>();

            for (int i = 0; i < _rootBoneArray.Length; i++)
            {
                CySpringRootBone root = _rootBoneArray[i];
                if (root == null)
                    continue;

                string name = root.BoneName;
                bool match = isPrefix
                    ? !string.IsNullOrEmpty(name) && name.IndexOf(targetRoot, StringComparison.Ordinal) == 0
                    : string.Equals(name, targetRoot, StringComparison.Ordinal);

                if (match)
                    result.Add(root);
            }

            return result.ToArray();
        }

        public CySpringRootBone[] FindRootCySpringBones(string[] targetRootArray, bool isPrefix = false)
        {
            if (targetRootArray == null || targetRootArray.Length == 0)
                return Array.Empty<CySpringRootBone>();

            List<CySpringRootBone> result = new List<CySpringRootBone>();

            for (int i = 0; i < targetRootArray.Length; i++)
                result.AddRange(FindRootCySpringBones(targetRootArray[i], isPrefix));

            return result.ToArray();
        }

        public CySpringBoneBase[] FindCySpringBone(Func<string, bool> checkFunc)
        {
            List<CySpringBoneBase> result = new List<CySpringBoneBase>();
            FindCySpringBone(result, checkFunc, false);
            return result.ToArray();
        }

        public void FindCySpringBone(List<CySpringBoneBase> resultBoneList, Func<string, bool> checkFunc, bool firstHitBreak = true)
        {
            if (resultBoneList == null || checkFunc == null || _rootBoneArray == null)
                return;

            for (int i = 0; i < _rootBoneArray.Length; i++)
            {
                _rootBoneArray[i]?.FindCySpringBone(resultBoneList, checkFunc, firstHitBreak);
                if (firstHitBreak && resultBoneList.Count > 0)
                    return;
            }
        }

        public void FindCySpringBoneForAll(List<CySpringBoneBase> resultBoneList, Func<string, bool> checkFunc, bool firstHitBreak = true)
        {
            FindCySpringBone(resultBoneList, checkFunc, firstHitBreak);
        }

        public void FineCySpringCollision(List<CySpringCollisionRuntimeData> resultList, Func<string, bool> checkFunc, bool firstHitBreak = true)
        {
            if (resultList == null || checkFunc == null || _charaCollisionList == null)
                return;

            for (int i = 0; i < _charaCollisionList.Count; i++)
            {
                CySpringCollisionRuntimeData c = _charaCollisionList[i];
                if (c == null)
                    continue;

                if (checkFunc(c.Name))
                {
                    resultList.Add(c);
                    if (firstHitBreak)
                        return;
                }
            }
        }

        public void SetEnableCySpringCollision(string name, bool isEnable)
        {
            int index = FindCollisionComponentIndex(name);
            if (index < 0 || _charaCollisionArray == null || index >= _charaCollisionArray.Length)
                return;
        }

        public void SetEnableCySpringBoneWind(bool enable, string targetRoot, bool isPrefix = false)
        {
            CySpringRootBone[] roots = FindRootCySpringBones(targetRoot, isPrefix);
            for (int i = 0; i < roots.Length; i++)
                roots[i]?.SetEnableCySpringBoneWindAll(enable);
        }

        private void SetWindData()
        {
            if (_springData != null)
            {
                _mainWindParamData = _springData;
                _addWindParamData = _addData;
            }
        }

        public static GameObject FindGameObject(GameObject rootObject, string objectName)
        {
            if (rootObject == null || string.IsNullOrEmpty(objectName))
                return null;

            if (rootObject.name == objectName)
                return rootObject;

            return FindGameObjectLoop(rootObject, objectName);
        }

        private static GameObject FindGameObjectLoop(GameObject rootObject, string objectName)
        {
            if (rootObject == null)
                return null;

            Transform root = rootObject.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == objectName)
                    return child.gameObject;

                GameObject found = FindGameObjectLoop(child.gameObject, objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        public void SetConnectedBone()
        {
            if (_springData == null || _springData.ConnectedBoneList == null)
                return;

            List<ConnectedBoneData> connectedBoneDataList = _springData.ConnectedBoneList;
            _connectedBoneList = new List<ConnectedBonePair>(connectedBoneDataList.Count);

            for (int i = 0; i < connectedBoneDataList.Count; i++)
            {
                ConnectedBoneData data = connectedBoneDataList[i];
                if (data == null)
                    continue;

                CySpringBoneBase bone1 = FindFirstCySpringBoneByName(data.Bone1);
                CySpringBoneBase bone2 = FindFirstCySpringBoneByName(data.Bone2);

                if (bone1 == null || bone2 == null)
                    continue;

                if (bone1.Transform == null || bone2.Transform == null)
                    continue;

                float firstDistance = Vector3.Distance(bone1.Transform.position, bone2.Transform.position);

                _connectedBoneList.Add(new ConnectedBonePair
                {
                    Bone1 = bone1,
                    Bone2 = bone2,
                    DefaultDistance = firstDistance,
                    Intensity = data.Intensity,
                    FirstDistance = firstDistance
                });
            }
        }        private CySpringBoneBase FindFirstCySpringBoneByName(string boneName)
        {
            if (string.IsNullOrEmpty(boneName) || _rootBoneArray == null)
                return null;

            List<CySpringBoneBase> found = new List<CySpringBoneBase>(1);
            FindCySpringBone(
                found,
                name => string.Equals(name, boneName, StringComparison.Ordinal),
                true);

            return found.Count > 0 ? found[0] : null;
        }

        public void UpdateScaleConnectBone(float legacyScale)
        {
            if (_connectedBoneList == null)
                return;

            for (int i = 0; i < _connectedBoneList.Count; i++)
            {
                ConnectedBonePair pair = _connectedBoneList[i];
                if (pair.Bone1 == null || pair.Bone2 == null || pair.Bone1.Transform == null)
                    continue;

                // Official logic directly uses legacyScale. It does not apply a zero/epsilon guard.
                pair.DefaultDistance = (pair.Bone1.Transform.lossyScale.y / legacyScale) * pair.FirstDistance;
                _connectedBoneList[i] = pair;
            }
        }        private void UpdateConnectedBone()
        {
            if (_connectedBoneList == null)
                return;

            int count = _connectedBoneList.Count;

            // Official order: first clear both sides for every pair.
            for (int i = 0; i < count; i++)
            {
                ConnectedBonePair pair = _connectedBoneList[i];

                if (pair.Bone1 != null)
                    pair.Bone1.ResetConnectedForce();

                if (pair.Bone2 != null)
                    pair.Bone2.ResetConnectedForce();
            }

            if (!_isEnableConnectedBone)
                return;

            for (int i = 0; i < count; i++)
            {
                ConnectedBonePair pair = _connectedBoneList[i];

                if (pair.Bone1 == null || pair.Bone2 == null)
                    continue;

                if (pair.Bone1.Transform == null || pair.Bone2.Transform == null)
                    continue;

                pair.Bone1.UpdateConnectedForce(
                    pair.Bone2.Transform.position,
                    pair.DefaultDistance,
                    pair.Intensity);

                pair.Bone2.UpdateConnectedForce(
                    pair.Bone1.Transform.position,
                    pair.DefaultDistance,
                    pair.Intensity);
            }
        }        private static IList<CySpringParamDataElement> GetElementList(CySpringParamDataAsset data)
        {
            return data != null ? data._elements : null;
        }

        private static bool IsFloatEqualLight(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.00001f;
        }

       

        private sealed class WindControlData
        {
            private Vector3 _upperWindDir;
            private Vector3 _downerWindDir;
            private Vector3 _rightWindDir;
            private Vector3 _leftWindDir;
            private float _verticalWindPhase;
            private float _horizontalWindPhase;
            private float _windPowerScale;
            private bool _enableVerticalWind;
            private bool _enableHorizontalWind;

            public Vector3 UpperWindDir => _upperWindDir;
            public Vector3 DownerWindDir => _downerWindDir;
            public Vector3 RightWindDir => _rightWindDir;
            public Vector3 LeftWindDir => _leftWindDir;
            public float VerticalWindPhase => _verticalWindPhase;
            public float HorizontalWindPhase => _horizontalWindPhase;
            public float WindPowerScale => _windPowerScale;
            public bool EnableVerticalWind => _enableVerticalWind;
            public bool EnableHorizontalWind => _enableHorizontalWind;

            public WindControlData(
                Vector3 upperWindDir,
                Vector3 downWindDir,
                Vector3 rightWindDir,
                Vector3 leftWindDir,
                float verticalWindPhase,
                float horizontalWindPhase,
                float windPowerScale,
                bool enableVerticalWind,
                bool enableHorizontalWind)
            {
                _upperWindDir = upperWindDir;
                _downerWindDir = downWindDir;
                _rightWindDir = rightWindDir;
                _leftWindDir = leftWindDir;
                _verticalWindPhase = verticalWindPhase;
                _horizontalWindPhase = horizontalWindPhase;
                _windPowerScale = windPowerScale;
                _enableVerticalWind = enableVerticalWind;
                _enableHorizontalWind = enableHorizontalWind;
            }
        }

        private struct ConnectedBonePair
        {
            public CySpringBoneBase Bone1;
            public CySpringBoneBase Bone2;
            public float DefaultDistance;
            public float Intensity;
            public float FirstDistance;
        }
    }
}