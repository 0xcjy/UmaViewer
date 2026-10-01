using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Gallop
{
    /// CySpring 的更新调度器
    ///
    /// 此类本身不负责弹簧骨骼的物理解算，
    /// 主要按照官方流程依次调度模拟的开始、结束、结果应用和后处理
    ///
    /// 执行顺序：
    /// BeginSimulation：
    ///   更新预热状态；
    ///   执行模拟开始前回调；
    ///   开始 CySpring 模拟或更新上一帧姿态；
    ///   执行模拟开始后回调
    ///
    /// EndSimulation：
    ///   执行模拟结束前回调；
    ///   结束 CySpring 模拟；
    ///   执行模拟结束后回调；
    ///   再次更新预热状态
    ///
    /// ApplySimulation：
    ///   将模拟结果应用到全部弹簧骨骼
    ///
    /// AfterEndSimulation：
    ///   处理跳帧状态；
    ///   更新裙摆；
    ///   执行眼神、Transform 修正和阴影等后处理
    [DisallowMultipleComponent]
    public class CySpringUpdater : MonoBehaviour
    {
        [Serializable]
        public struct Context
        {
            public CySpringController CySpring;
            public SkirtController Skirt;
        }

        private interface IUpdater
        {
            void BeforeBeginSimulation();
            void AfterBeginSimulation();
            void BeforeEndSimulation();
            void AfterEndSimulation();
        }

        [Header("Context")]
        [SerializeField] private CySpringController _cySpring;
        [SerializeField] private SkirtController _skirt;

        [Header("Standalone Update")]
        [Tooltip("Turn this on only if no external ModelController/Live wrapper calls Begin/End/Apply/AfterEnd.")]
        [SerializeField] private bool _autoUpdateInLateUpdate;

        [Tooltip("Matches official BeginSimulation(..., true). Your current CySpringController runs synchronously anyway.")]
        [SerializeField] private bool _useThread = true;

        [Tooltip("When Time.deltaTime <= this value, official code uses UpdatePrevousPosture instead of normal simulation.")]
        [SerializeField] private float _minDeltaTime = 0.000001f;

        [Header("Debug")]
        [SerializeField] private bool _debugLog;

        private Context _context;
        private IUpdater _updater;

        // Optional official post components. Stored as Component to avoid hard dependency
        // on incomplete restored classes.
        private Component _transformCanceler;
        private Component _shadowController;
        private Component _drivenKeyComponent;
        private Component _propHolder;
        private Component _eyeTraceController;
        private Component _miniMotionCtrl;

        // Official hidden backing field around offset 0x60.
        [SerializeField] private bool _isUpdateSkirt = true;

        private bool _isBeginSimulation;
        private bool _skipFrame;

        public bool IsUpdateSkirt
        {
            get { return _isUpdateSkirt; }
            set { _isUpdateSkirt = value; }
        }

        public bool IsBeginSimulation
        {
            get { return _isBeginSimulation; }
        }

        public CySpringController.SpringUpdateMode SpringUpdateMode
        {
            get
            {
                if (_cySpring == null)
                    return CySpringController.SpringUpdateMode.ModeNormal;

                return _cySpring.UpdateMode;
            }
            set
            {
                if (_cySpring != null)
                    _cySpring.UpdateMode = value;
            }
        }

        private void Awake()
        {
            AutoFindContextIfNeeded();
            OnInitialize();
        }

        private void OnEnable()
        {
            AutoFindContextIfNeeded();
        }

        private void LateUpdate()
        {
            if (!_autoUpdateInLateUpdate)
                return;

            BeginSimulation();
            EndSimulation();
            ApplySimulation();
            AfterEndSimulation();
        }

        public void SetContext(CySpringController cySpring, SkirtController skirt)
        {
            Context context = new Context
            {
                CySpring = cySpring,
                Skirt = skirt
            };

            Refresh(ref context);
        }

        public void OnInitialize()
        {
            _isBeginSimulation = false;

            _context.CySpring = _cySpring;
            _context.Skirt = _skirt;

            Refresh(ref _context);

            // Current project usually does not have official ModelComponentBase type routing,
            // so default to normal model updater.
            _updater = new UpdaterModel(this);

            RefreshUpdaterComponent();
        }

        public void Refresh(ref Context context)
        {
            _context = context;
            _cySpring = context.CySpring;
            _skirt = context.Skirt;

            // Official Refresh sets IsUpdateSkirt = skirt != null.
            _isUpdateSkirt = _skirt != null;

            if (_debugLog)
            {
                Debug.Log(
                    "[CySpringUpdater] Refresh cySpring=" + GetObjName(_cySpring) +
                    ", skirt=" + GetObjName(_skirt) +
                    ", isUpdateSkirt=" + _isUpdateSkirt,
                    this);
            }
        }

        public void RefreshUpdaterComponent()
        {
            // Official only refreshes EyeTrace / TransformCanceler / Shadow here.
            _eyeTraceController = FindComponentByTypeName("EyeTraceController");
            _transformCanceler = FindComponentByTypeName("TransformCanceler");
            _shadowController = FindComponentByTypeName("ShadowController");

            // These are initialized in OnInitialize in official code.
            // Keeping them refreshed here is safer for your reconstructed project.
            _drivenKeyComponent = FindComponentByTypeName("DrivenKeyComponent");
            _propHolder = FindComponentByTypeName("CharaPropHolder");
            _miniMotionCtrl = FindComponentByTypeName("MiniMotionSetController");
        }

        public void BeginSimulation()
        {
            if (_cySpring == null)
                return;

            int mode = (int)SpringUpdateMode;

            // Official:
            // if ((mode == 2 || mode == 3) && _skipFrame) return;
            if ((mode == 2 || mode == 3) && _skipFrame)
                return;

            _isBeginSimulation = false;

            _cySpring.TryUpdateWarmUp();

            if (_updater == null)
                _updater = new UpdaterModel(this);

            _updater.BeforeBeginSimulation();

            float dt = Time.deltaTime;

            if (dt > _minDeltaTime)
            {
                _cySpring.BeginSimulation(dt, _useThread);
                _isBeginSimulation = true;
            }
            else
            {
                // Official only does this when model controller says the update is valid.
                // In this standalone bridge, doing it here is the safer option:
                // it prevents a zero-delta / teleport frame from creating huge velocity.
                _cySpring.UpdatePrevousPosture();
                _isBeginSimulation = true;
            }

            _updater.AfterBeginSimulation();
        }

        public void EndSimulation()
        {
            if (_cySpring == null)
                return;

            int mode = (int)SpringUpdateMode;

            // Official mode 2 skip frame: do nothing.
            if (mode == 2 && _skipFrame)
                return;

            // Official mode 3 skip frame: only PostSpringAll.
            if (mode == 3 && _skipFrame)
            {
                _cySpring.PostSpringAll();
                return;
            }

            if (_isBeginSimulation)
            {
                if (_updater == null)
                    _updater = new UpdaterModel(this);

                _updater.BeforeEndSimulation();
                _cySpring.EndSimulation();
                _updater.AfterEndSimulation();
            }

            _cySpring.TryUpdateWarmUp();
        }

        public void ApplySimulation()
        {
            if (_cySpring == null)
                return;

            _cySpring.PostSpringAll();
        }

        public void AfterEndSimulation()
        {
            int mode = (int)SpringUpdateMode;

            // Official toggles skip frame here and returns early on skip frame.
            if (mode == 2 || mode == 3)
            {
                if (_skipFrame)
                {
                    _skipFrame = false;
                    return;
                }

                _skipFrame = true;
            }

            if (_skirt != null)
            {
                // Official sets a private skirt flag from cySpring[0x108] before UpdateSkirt.
                // Your current SkirtController exposes IsEnableSkirt as the global skirt switch,
                // so do NOT overwrite it here.
                if (_isUpdateSkirt)
                    _skirt.UpdateSkirt();
            }

            if (_eyeTraceController != null)
            {
                // Official calls a model-controller virtual before this.
                // In this bridge, only call the component hook if it exists.
                InvokeIfExists(_eyeTraceController, "AfterEndCySpringSimulation");
            }

            if (_transformCanceler != null)
                InvokeIfExists(_transformCanceler, "AfterEndCySpringSimulation");

            if (_shadowController != null)
            {
                // ShadowController official call is virtual. Try common names first,
                // then fall back to no-op if your restored class does not have them.
                if (!InvokeIfExists(_shadowController, "AfterEndCySpringSimulation"))
                    InvokeIfExists(_shadowController, "LateUpdateShadow");
            }
        }

        private void AutoFindContextIfNeeded()
        {
            if (_cySpring == null)
                _cySpring = GetComponent<CySpringController>() ?? GetComponentInChildren<CySpringController>(true);

            if (_skirt == null)
                _skirt = GetComponent<SkirtController>() ?? GetComponentInChildren<SkirtController>(true);

            _context.CySpring = _cySpring;
            _context.Skirt = _skirt;

            _isUpdateSkirt = _skirt != null;
        }

        private Component FindComponentByTypeName(string typeName)
        {
            Component[] components = GetComponentsInChildren<Component>(true);

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];

                if (component == null)
                    continue;

                Type type = component.GetType();

                if (type.Name == typeName || type.FullName != null && type.FullName.EndsWith("." + typeName, StringComparison.Ordinal))
                    return component;
            }

            return null;
        }

        private static bool InvokeIfExists(Component component, string methodName)
        {
            if (component == null || string.IsNullOrEmpty(methodName))
                return false;

            Type type = component.GetType();

            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (method == null)
                return false;

            try
            {
                method.Invoke(component, null);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    "[CySpringUpdater] Failed to invoke " + type.Name + "." + methodName + ": " + e.Message,
                    component);

                return false;
            }
        }

        private static string GetObjName(UnityEngine.Object obj)
        {
            return obj != null ? obj.name : "null";
        }

        private sealed class UpdaterModel : IUpdater
        {
            private readonly CySpringUpdater _owner;

            public UpdaterModel(CySpringUpdater owner)
            {
                _owner = owner;
            }

            public void BeforeBeginSimulation()
            {
                // Official UpdaterModel.BeforeBeginSimulation is effectively no-op.
            }

            public void AfterBeginSimulation()
            {
                // Official sends CySpring posture info to CharaPropController.BeginCySpringSimulate.
                // Your current controller bridge does not expose those private posture structs,
                // so this is intentionally kept as a safe no-op unless prop hooks are restored.
                _owner.TryInvokePropControllers("BeginCySpringSimulate");
            }

            public void BeforeEndSimulation()
            {
                _owner.TryInvokePropControllers("EndCySpringSimulation");
            }

            public void AfterEndSimulation()
            {
                if (_owner._drivenKeyComponent != null)
                    InvokeIfExists(_owner._drivenKeyComponent, "ApplyVirtualParentTransform");
            }
        }

        private sealed class UpdaterMini : IUpdater
        {
            private readonly CySpringUpdater _owner;

            public UpdaterMini(CySpringUpdater owner)
            {
                _owner = owner;
            }

            public void BeforeBeginSimulation()
            {
            }

            public void AfterBeginSimulation()
            {
                // Mini path placeholder. Safe no-op for now.
            }

            public void BeforeEndSimulation()
            {
                // Mini path placeholder. Safe no-op for now.
            }

            public void AfterEndSimulation()
            {
            }
        }

        private void TryInvokePropControllers(string methodName)
        {
            if (_propHolder == null)
                return;

            object list = TryGetMemberValue(_propHolder, "PropControllers")
                       ?? TryGetMemberValue(_propHolder, "_propControllers")
                       ?? TryGetMemberValue(_propHolder, "PropControllerList")
                       ?? TryGetMemberValue(_propHolder, "_propControllerList");

            IEnumerable enumerable = list as IEnumerable;

            if (enumerable == null)
                return;

            foreach (object item in enumerable)
            {
                Component component = item as Component;

                if (component == null)
                    continue;

                InvokeIfExists(component, methodName);
            }
        }

        private static object TryGetMemberValue(Component component, string memberName)
        {
            if (component == null || string.IsNullOrEmpty(memberName))
                return null;

            Type type = component.GetType();

            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null)
                return field.GetValue(component);

            PropertyInfo prop = type.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (prop != null && prop.CanRead)
                return prop.GetValue(component, null);

            return null;
        }
    }
}