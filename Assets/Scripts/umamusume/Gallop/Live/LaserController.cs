using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    public class LaserController
    {
        private const int RAY_CONTROLLER_MAX = 5;
        private const string ASSET_HOLDER_KEY_CONTROLLER = "Controller";
        private const string ASSET_HOLDER_KEY_RAY = "Ray";
        private const string ASSET_HOLDER_KEY_LIGHT = "Light";
        private const int RANDOM_BLINK_ARRAY_MAX = 256;

        public const float RAY_DISTANCE = 90f;
        private static readonly Dictionary<LaserFormation, int>
        FORMATION_ENABLE_RAY_COUNT =
            new Dictionary<LaserFormation, int>
            {
                { (LaserFormation)0,  0 },
                { (LaserFormation)6,  0 },

                { (LaserFormation)1,  1 },
                { (LaserFormation)2,  2 },
                { (LaserFormation)3,  3 },
                { (LaserFormation)4,  4 },
                { (LaserFormation)5,  5 },

                { (LaserFormation)8,  2 },
                { (LaserFormation)9,  3 },
                { (LaserFormation)10, 4 },
                { (LaserFormation)11, 5 }
            };
        

        private GameObject _instance;
        private Transform _transform;

        private GameObject _controllerObject;
        private Transform _controllerTransform;
        private Transform _targetCameraTransform;

        private RayController[] _rayControllerArray;
        private Renderer[] _lightRendererArray;

        private int _enableRayCount;
        private LaserFormation _formation;
        private LaserBlink _blink;
        private float _blinkPeriod;
        private int _blinkCount;

        private bool[] _randomBlinkArray;

        private bool _isDisabledRootLight;
        private bool _isEnabledRaycast;
        private float _raycastDistance;

        private bool _isInitialized;
        private bool _ownsInstance;

        public bool IsInitialized => _isInitialized && _instance != null;
        public void SetTargetCameraTransform(Transform cameraTransform)
        {
            if (cameraTransform != null)
                _targetCameraTransform = cameraTransform;
        }

        public void Initialize(GameObject prefab, Transform parentTransform, Transform cameraTransform, Material material)
        {
            Release();

            if (prefab == null)
            {
                Debug.LogWarning("[LaserController] prefab is null");
                return;
            }

            if (parentTransform == null)
            {
                Debug.LogWarning("[LaserController] parentTransform is null");
                return;
            }

            _instance = UnityEngine.Object.Instantiate(prefab, parentTransform);
            _ownsInstance = true;
            _instance.name = prefab.name + "(Clone)";
            _transform = _instance.transform;
            _targetCameraTransform = cameraTransform;

            Component assetHolder = FindAssetHolder(_instance);
            if (assetHolder == null)
            {
                Debug.LogWarning("[LaserController] AssetHolder not found");
                return;
            }

            //先检查并取得 "Controller"
            _controllerObject = HolderGetObject( assetHolder, ASSET_HOLDER_KEY_CONTROLLER);

            if (_controllerObject != null)
                _controllerTransform = _controllerObject.transform;

            // 兼容你的资源命名，但最终必须存在 Controller
            if (_controllerTransform == null)
            {
                Transform fallbackController =
                    FindChildRecursive(_transform, "lasercontroller");

                if (fallbackController != null)
                {
                    _controllerTransform = fallbackController;
                    _controllerObject = fallbackController.gameObject;
                }
            }

            if (_controllerTransform == null)
            {
                Debug.LogWarning("[LaserController] Controller not found");
                return;
            }

            _targetCameraTransform = cameraTransform;

            // 官方：AssetHolder.GetObjects<GameObject>("Ray")
            GameObject[] rayObjects = HolderGetObjects( assetHolder, ASSET_HOLDER_KEY_RAY);

            // 仅作为非官方资源兼容 fallback
            if (rayObjects == null || rayObjects.Length == 0)
            {
                rayObjects = GetIndexedObjects(assetHolder, "Ray", RAY_CONTROLLER_MAX);
            }

            InitRayControllers(rayObjects, material);

            // 官方：AssetHolder.GetObjects<GameObject>("Light")
            GameObject[] lightObjects = HolderGetObjects(assetHolder,ASSET_HOLDER_KEY_LIGHT);

            // 仅作为兼容 fallback
            if (lightObjects == null || lightObjects.Length == 0)
            {
                lightObjects = GetIndexedObjects(assetHolder,"Light", 8);
            }

            InitLightRenderers(lightObjects, material);
            

            // AssetHolder 数据缺失时，按实际 joint_a～joint_e 层级顺序读取。
            // 不能按 laser000_000～laser000_004 猜名字：正式资源的五条 Ray 是
            // laser000_000 / laser001_000 / laser002_000 / laser001_001 / laser002_001。
            if ((_rayControllerArray == null || _rayControllerArray.Length == 0) && _controllerTransform != null)
            {
                InitRayControllers(
                    FindRayObjectsFromHierarchy(_controllerTransform),
                    material);
            }

            if ((_lightRendererArray == null || _lightRendererArray.Length == 0) && _instance != null)
            {
                Transform lightTransform = FindChildRecursive(_transform, "light_000");
                Renderer lightRenderer = lightTransform != null ? lightTransform.GetComponent<Renderer>() : null;
                _lightRendererArray = lightRenderer != null ? new[] { lightRenderer } : Array.Empty<Renderer>();
            }

            _randomBlinkArray = new bool[RANDOM_BLINK_ARRAY_MAX];
            for (int i = 0; i < _randomBlinkArray.Length; i++)
                _randomBlinkArray[i] = UnityEngine.Random.Range(0, 2) == 0;

            _isDisabledRootLight = false;
            _isEnabledRaycast = false;
            _raycastDistance = 0f;
            _isInitialized = true;

            Debug.Log(
                $"[LaserController] initialized: {_instance.name}, " +
                $"rays={_rayControllerArray?.Length ?? 0}, " +
                $"lights={_lightRendererArray?.Length ?? 0}, " +
                $"rayNames={GetRayNameList()}");
        }
        private static GameObject[] GetIndexedObjects(Component holder,string prefix, int maxCount)
        {
            if (holder == null || maxCount <= 0)
                return Array.Empty<GameObject>();

            GameObject[] objects = new GameObject[maxCount];
            bool foundAny = false;

            for (int i = 0; i < maxCount; i++)
            {
                objects[i] = HolderGetObject(
                    holder,
                    prefix + i);

                if (objects[i] != null)
                    foundAny = true;
            }

            return foundAny
                ? objects
                : Array.Empty<GameObject>();
        }

        public void InitializeExisting(GameObject instance, Transform cameraTransform, Material material)
        {
            Release();

            if (instance == null)
            {
                Debug.LogWarning("[LaserController] existing instance is null");
                return;
            }

            _instance = instance;
            _ownsInstance = false;
            _transform = instance.transform;
            _targetCameraTransform = cameraTransform;

            Component assetHolder =FindAssetHolder(_instance);

            if (assetHolder == null)
            {
                Debug.LogWarning(
                    "[LaserController] AssetHolder not found");
                return;
            }

            _controllerObject =HolderGetObject(assetHolder, ASSET_HOLDER_KEY_CONTROLLER);

            if (_controllerObject != null)
                _controllerTransform = _controllerObject.transform;

            if (_controllerTransform == null)
            {
                Transform controller = FindChildRecursive(_transform, "lasercontroller");

                if (controller != null)
                {
                    _controllerTransform = controller;
                    _controllerObject = controller.gameObject;
                }
            }

            if (_controllerTransform == null)
            {
                Debug.LogWarning(
                    "[LaserController] Controller not found");
                return;
            }

            GameObject[] rayObjects = HolderGetObjects( assetHolder, ASSET_HOLDER_KEY_RAY);

            if (rayObjects == null ||
                rayObjects.Length == 0)
            {
                rayObjects = GetIndexedObjects( assetHolder, "Ray", RAY_CONTROLLER_MAX);
            }

            InitRayControllers(rayObjects, material);

            GameObject[] lightObjects =HolderGetObjects( assetHolder, ASSET_HOLDER_KEY_LIGHT);

            if (lightObjects == null ||
                lightObjects.Length == 0)
            {
                lightObjects = GetIndexedObjects( assetHolder, "Light", 8);
            }

            InitLightRenderers(
                lightObjects,
                material);

            if (_controllerTransform == null)
            {
                Transform controller = FindChildRecursive(_transform, "lasercontroller");
                if (controller != null)
                {
                    _controllerTransform = controller;
                    _controllerObject = controller.gameObject;
                }
            }

            if (_rayControllerArray == null || _rayControllerArray.Length == 0)
            {
                InitRayControllers(
                    FindRayObjectsFromHierarchy(_controllerTransform),
                    material);
            }

            if (_lightRendererArray == null || _lightRendererArray.Length == 0)
            {
                Transform lightTransform = FindChildRecursive(_transform, "light_000");
                Renderer lightRenderer = lightTransform != null ? lightTransform.GetComponent<Renderer>() : null;
                _lightRendererArray = lightRenderer != null ? new[] { lightRenderer } : Array.Empty<Renderer>();
            }

            _randomBlinkArray = new bool[RANDOM_BLINK_ARRAY_MAX];
            for (int i = 0; i < _randomBlinkArray.Length; i++)
                _randomBlinkArray[i] = UnityEngine.Random.Range(0, 2) == 0;

            _isDisabledRootLight = false;
            _isEnabledRaycast = false;
            _raycastDistance = 0f;
            _isInitialized = true;

            Debug.Log($"[LaserController] bound existing: {_instance.name}, rays={_rayControllerArray?.Length ?? 0}, lights={_lightRendererArray?.Length ?? 0}");
        }

        private static GameObject[] FindRayObjectsFromHierarchy(Transform controllerTransform)
        {
            if (controllerTransform == null)
                return Array.Empty<GameObject>();

            var result = new List<GameObject>(RAY_CONTROLLER_MAX);
            var seen = new HashSet<int>();

            // 官方 AssetHolder 的 Ray 数组顺序对应 joint 的 sibling 顺序。
            // 每个 joint 下只有一个真正的 laser mesh Renderer。
            for (int i = 0;
                i < controllerTransform.childCount && result.Count < RAY_CONTROLLER_MAX;
                i++)
            {
                Transform joint = controllerTransform.GetChild(i);
                if (joint == null)
                    continue;

                Renderer[] renderers = joint.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    Renderer renderer = renderers[r];
                    if (renderer == null)
                        continue;

                    Transform rayTransform = renderer.transform;
                    if (rayTransform == null ||
                        !rayTransform.name.StartsWith("laser", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int id = rayTransform.gameObject.GetInstanceID();
                    if (seen.Add(id))
                        result.Add(rayTransform.gameObject);

                    break;
                }
            }

            // 某些资源在 lasercontroller 与 Ray 之间还有额外容器；作为最后兜底，
            // 按 Transform 遍历顺序补齐，不按名称数字重新排序。
            if (result.Count == 0)
            {
                Renderer[] renderers = controllerTransform.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length && result.Count < RAY_CONTROLLER_MAX; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null ||
                        !renderer.name.StartsWith("laser", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int id = renderer.gameObject.GetInstanceID();
                    if (seen.Add(id))
                        result.Add(renderer.gameObject);
                }
            }

            return result.ToArray();
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                    return child;

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                    return found;
            }

            return null;
        }

       private void InitRayControllers(GameObject[] rayObjects, Material material)
        {
            _enableRayCount = 0;

            if (rayObjects == null || rayObjects.Length == 0)
            {
                _rayControllerArray =
                    Array.Empty<RayController>();
                return;
            }

            // 官方：数组长度完全等于 GetObjects("Ray") 返回长度
            _rayControllerArray = new RayController[rayObjects.Length];

            // 新初始化时通常 formation 为 0，因此全部关闭
            bool initialEnable = (int)_formation != 0;

            for (int i = 0; i < rayObjects.Length; i++)
            {
                GameObject rayObject = rayObjects[i];

                // 官方保留空槽，不压缩后面的索引
                if (rayObject == null)
                    continue;

                RayController ray = new RayController();
                ray.Initialize(rayObject.transform, material);
                ray.Enable = initialEnable;

                _rayControllerArray[i] = ray;

                if (initialEnable)
                    _enableRayCount++;
            }
        }

        private void InitLightRenderers(GameObject[] lightObjects, Material material)
        {
            var list = new List<Renderer>();

            if (lightObjects != null)
            {
                for (int i = 0; i < lightObjects.Length; i++)
                {
                    GameObject lightObject = lightObjects[i];
                    if (lightObject == null)
                        continue;

                    Renderer renderer = lightObject.GetComponent<Renderer>();
                    if (renderer == null)
                        continue;

                    if (material != null)
                        renderer.sharedMaterial = material;

                    list.Add(renderer);
                }
            }

            _lightRendererArray = list.ToArray();
        }

        public void UpdateInfo(ref LaserUpdateInfo updateInfo)
        {
            if (!_isInitialized || _instance == null)
                return;

            _instance.SetActive(updateInfo.isEnabledRender);

            if (!updateInfo.isEnabledRender)
                return;

            _transform.localPosition = updateInfo.objectPosition;
            _transform.localRotation = updateInfo.objectRotation;
            _transform.localScale = updateInfo.objectScale;

            _isDisabledRootLight =updateInfo.IsDisabledRootLight;

            _formation = updateInfo.formation;

            _enableRayCount =GetFormationRayCount(_formation);

            // 你的资源绑定还存在 fallback，暂时保留安全限制。
            if (_rayControllerArray != null)
            {
                _enableRayCount = Mathf.Clamp( _enableRayCount, 0, _rayControllerArray.Length);
            }

            for (int i = 0; _rayControllerArray != null && i < _rayControllerArray.Length; i++)
            {
                RayController ray = _rayControllerArray[i];

                if (ray == null)
                    continue;

                bool enable = i < _enableRayCount;

                ray.Enable = enable;

                if (enable)
                {
                    ray.UpdateFormation(  _formation, i);
                }
            }

            bool rootLightEnabled = !_isDisabledRootLight && (int)_formation != 0;

            if (_lightRendererArray != null)
            {
                for (int i = 0; i < _lightRendererArray.Length; i++)
                {
                    Renderer renderer = _lightRendererArray[i];

                    if (renderer != null)
                    {
                        renderer.enabled = rootLightEnabled;
                    }
                }
            }

            if (_enableRayCount > 0 &&
                _controllerTransform != null)
            {
                _controllerTransform.localRotation = updateInfo.rotation;

                _controllerTransform.Rotate( Vector3.up, updateInfo.degRootYaw, Space.Self);
            }

            for (int i = 0; _rayControllerArray != null && i < _enableRayCount; i++)
            {
                RayController ray = _rayControllerArray[i];

                if (ray == null)
                    continue;

                // 官方 UpdateInfo 直接把时间轴俯仰角交给 UpdatePitch。
                ray.UpdatePitch(updateInfo.degLaserPitch);

                ray.UpdatePositionInterval(updateInfo.posInterval);
            }

            _blink = updateInfo.blink;
            _blinkPeriod = updateInfo.blinkPeroid;

            ApplyBlink(updateInfo.ProgressTime,updateInfo.ProgressFrame);

            _isEnabledRaycast =updateInfo.IsEnabledRaycast;

            _raycastDistance =updateInfo.RaycastDistance;
        }

        public void AlterUpdate()
        {
            if (!_isInitialized || _rayControllerArray == null)
                return;

            // 官方遍历整个数组
            for (int i = 0; i < _rayControllerArray.Length; i++)
            {
                RayController ray = _rayControllerArray[i];

                if (ray != null)
                    ray.UpdateRenderEnabled();
            }

            if (_enableRayCount <= 0 || _targetCameraTransform == null)
            {
                return;
            }

            int count = Mathf.Min(_enableRayCount, _rayControllerArray.Length);

            for (int i = 0; i < count; i++)
            {
                RayController ray = _rayControllerArray[i];

                if (ray == null || !ray.Enable)
                    continue;

                Transform rayTransform = ray.CachedTransform;

                if (rayTransform == null)
                    continue;

                rayTransform.localRotation = Quaternion.identity;

                Vector3 rayAxis = rayTransform.rotation * Vector3.up;

                Vector3 cameraDirection = _targetCameraTransform.position - rayTransform.position;

                Vector3 projectedDirection = cameraDirection - Vector3.Dot(cameraDirection, rayAxis) * rayAxis;

                if (projectedDirection.sqrMagnitude > 0.000001f)
                {
                    rayTransform.rotation = Quaternion.LookRotation( projectedDirection, rayAxis);
                }
            }
        }

        public void AlterLateUpdate()
        {
            if (!_isInitialized || _rayControllerArray == null || _enableRayCount <= 0)
            {
                return;
            }

            int count = Mathf.Min( _enableRayCount, _rayControllerArray.Length);

            for (int i = 0; i < count; i++)
            {
                RayController ray = _rayControllerArray[i];
                if (ray == null)
                    continue;

                // 官方 AlterLateUpdate 逐条调用 RayController.Raycast
                ray.Raycast(  _isEnabledRaycast, RAY_DISTANCE, _raycastDistance);
            }
        }

        private void ApplyBlink(float localTime, int updateCount)
        {
            if (_rayControllerArray == null || _enableRayCount <= 0)
                return;

            int blink = (int)_blink;

            if (blink == 0)
            {
                for (int i = 0; i < _enableRayCount; i++)
                {
                    RayController ray = _rayControllerArray[i];

                    if (ray != null)
                        ray.Enable = true;
                }

                return;
            }

            // 官方是无符号比较：
            // (uint)(blink - 3) > 1
            // 因此 1、2、5、6 初始为开启，3、4 初始为关闭。
            bool initialEnable = (uint)(blink - 3) > 1u;

            for (int i = 0; i < _enableRayCount; i++)
            {
                RayController ray = _rayControllerArray[i];

                if (ray != null)
                    ray.Enable = initialEnable;
            }

            int blinkStepCount;

            if (_blinkPeriod > 0f)
                blinkStepCount = (int)(localTime / _blinkPeriod);
            else
                blinkStepCount = updateCount;

            _blinkCount = 0;

            for (int i = 0; i < blinkStepCount; i++)
                UpdateBlink(i);
        }

        private void UpdateBlink(int updateCount)
        {
            if (_rayControllerArray == null || _enableRayCount <= 0)
                return;

            switch ((int)_blink)
            {
                // 全部同时翻转
                case 1:
                {
                    for (int i = 0; i < _enableRayCount; i++)
                    {
                        RayController ray = _rayControllerArray[i];

                        if (ray != null)
                            ray.Enable = !ray.Enable;
                    }

                    break;
                }

                // 开启状态先关闭；关闭状态根据固定随机表决定是否开启
                case 2:
                {
                    for (int i = 0; i < _enableRayCount; i++)
                    {
                        RayController ray = _rayControllerArray[i];

                        if (ray == null)
                            continue;

                        if (ray.Enable)
                        {
                            ray.Enable = false;
                        }
                        else if (_randomBlinkArray != null && _randomBlinkArray.Length > 0)
                        {
                            int randomIndex = (i + _enableRayCount * updateCount) % _randomBlinkArray.Length;

                            ray.Enable = _randomBlinkArray[randomIndex];
                        }
                    }

                    break;
                }

                // 正序，只有一条开启
                case 3:
                {
                    for (int i = 0; i < _enableRayCount; i++)
                    {
                        RayController ray = _rayControllerArray[i];

                        if (ray != null)
                            ray.Enable = i == _blinkCount;
                    }

                    break;
                }

                // 倒序，只有一条开启
                case 4:
                {
                    for (int i = 0; i < _enableRayCount; i++)
                    {
                        int rayIndex = _enableRayCount - i - 1;
                        RayController ray = _rayControllerArray[rayIndex];

                        if (ray != null)
                            ray.Enable = i == _blinkCount;
                    }

                    break;
                }

                // 正序，只有一条关闭
                case 5:
                {
                    for (int i = 0; i < _enableRayCount; i++)
                    {
                        RayController ray = _rayControllerArray[i];

                        if (ray != null)
                            ray.Enable = i != _blinkCount;
                    }

                    break;
                }

                // 倒序，只有一条关闭
                case 6:
                {
                    for (int i = 0; i < _enableRayCount; i++)
                    {
                        int rayIndex = _enableRayCount - i - 1;
                        RayController ray = _rayControllerArray[rayIndex];

                        if (ray != null)
                            ray.Enable = i != _blinkCount;
                    }

                    break;
                }
            }

            if (_enableRayCount > 1)
                _blinkCount = (_blinkCount + 1) % _enableRayCount;
            else
                _blinkCount = 0;
        }

        public void AppendRuntimeRenderers(List<Renderer> destination)
        {
            if (destination == null)
                return;

            var seen = new HashSet<int>();

            if (_rayControllerArray != null)
            {
                for (int i = 0; i < _rayControllerArray.Length; i++)
                {
                    Renderer renderer = _rayControllerArray[i]?.Renderer;
                    if (renderer != null && seen.Add(renderer.GetInstanceID()))
                        destination.Add(renderer);
                }
            }

            if (_lightRendererArray != null)
            {
                for (int i = 0; i < _lightRendererArray.Length; i++)
                {
                    Renderer renderer = _lightRendererArray[i];
                    if (renderer != null && seen.Add(renderer.GetInstanceID()))
                        destination.Add(renderer);
                }
            }
        }

        public void AppendRuntimeMaterials(List<Material> destination)
        {
            if (destination == null)
                return;

            if (_rayControllerArray != null)
            {
                for (int i = 0; i < _rayControllerArray.Length; i++)
                {
                    Renderer renderer = _rayControllerArray[i]?.Renderer;
                    AppendRendererMaterials(renderer, destination);
                }
            }

            if (_lightRendererArray != null)
            {
                for (int i = 0; i < _lightRendererArray.Length; i++)
                    AppendRendererMaterials(_lightRendererArray[i], destination);
            }
        }

        private static void AppendRendererMaterials(Renderer renderer, List<Material> destination)
        {
            if (renderer == null || destination == null)
                return;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null)
                return;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material != null)
                    destination.Add(material);
            }
        }

        private string GetRayNameList()
        {
            if (_rayControllerArray == null || _rayControllerArray.Length == 0)
                return "<none>";

            var names = new List<string>(_rayControllerArray.Length);
            for (int i = 0; i < _rayControllerArray.Length; i++)
            {
                Transform transform = _rayControllerArray[i]?.CachedTransform;
                names.Add(transform != null ? transform.name : "<null>");
            }

            return string.Join(",", names);
        }

        public void Release()
        {
            if (_rayControllerArray != null)
            {
                for (int i = 0; i < _rayControllerArray.Length; i++)
                {
                    if (_rayControllerArray[i] != null)
                        _rayControllerArray[i].Release();
                }
            }

            if (_instance != null && _ownsInstance)
                UnityEngine.Object.Destroy(_instance);

            _instance = null;
            _transform = null;
            _controllerObject = null;
            _controllerTransform = null;
            _targetCameraTransform = null;
            _rayControllerArray = null;
            _lightRendererArray = null;
            _enableRayCount = 0;
            _isInitialized = false;
            _ownsInstance = false;
        }

       private static int GetFormationRayCount(LaserFormation formation)
        {
            if (!FORMATION_ENABLE_RAY_COUNT.TryGetValue(formation, out int enableRayCount))
            {
                enableRayCount = 0;
            }

            return enableRayCount;
        }

        private static Component FindAssetHolder(GameObject go)
        {
            if (go == null)
                return null;

            Component[] comps = go.GetComponents<Component>();

            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null)
                    continue;

                if (c.GetType().Name == "AssetHolder")
                    return c;
            }

            return null;
        }

        private static GameObject HolderGetObject(Component holder, string key)
        {
            if (holder == null)
                return null;

            Type t = holder.GetType();
            MethodInfo[] methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];

                if (m.Name != "Get")
                    continue;

                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != 1 || ps[0].ParameterType != typeof(string))
                    continue;

                try
                {
                    object result;

                    if (m.IsGenericMethodDefinition)
                        result = m.MakeGenericMethod(typeof(GameObject)).Invoke(holder, new object[] { key });
                    else
                        result = m.Invoke(holder, new object[] { key });

                    if (result is GameObject go)
                        return go;
                }
                catch
                {
                }
            }

            return null;
        }

        private static GameObject[] HolderGetObjects(Component holder, string key)
        {
            if (holder == null)
                return null;

            Type t = holder.GetType();
            MethodInfo[] methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];

                if (m.Name != "GetObjects")
                    continue;

                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length != 1 || ps[0].ParameterType != typeof(string))
                    continue;

                try
                {
                    object result;

                    if (m.IsGenericMethodDefinition)
                        result = m.MakeGenericMethod(typeof(GameObject)).Invoke(holder, new object[] { key });
                    else
                        result = m.Invoke(holder, new object[] { key });

                    if (result is GameObject[] arr)
                        return arr;
                }
                catch
                {
                }
            }

            return null;
        }

        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name))
                return null;

            Type t = obj.GetType();

            while (t != null)
            {
                FieldInfo f = t.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (f != null)
                    return f.GetValue(obj);

                PropertyInfo p = t.GetProperty(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (p != null)
                    return p.GetValue(obj, null);

                t = t.BaseType;
            }

            return null;
        }

        private static int GetIntMember(object obj, string name, int fallback)
        {
            object v = GetMemberValue(obj, name);

            if (v is int i)
                return i;

            if (v is Enum e)
                return Convert.ToInt32(e);

            return fallback;
        }

        private static float GetFloatMember(object obj, string name, float fallback)
        {
            object v = GetMemberValue(obj, name);

            if (v is float f)
                return f;

            if (v is int i)
                return i;

            return fallback;
        }

        private static bool GetBoolMember(object obj, string name, bool fallback)
        {
            object v = GetMemberValue(obj, name);

            if (v is bool b)
                return b;

            if (v is int i)
                return i != 0;

            return fallback;
        }

        private static Vector3 GetVector3Member(object obj, string name, Vector3 fallback)
        {
            object v = GetMemberValue(obj, name);

            if (v is Vector3 vec)
                return vec;

            return fallback;
        }

        private static Quaternion GetQuaternionOrEulerMember(object obj, string name, Quaternion fallback)
        {
            object v = GetMemberValue(obj, name);

            if (v is Quaternion q)
                return q;

            if (v is Vector3 euler)
                return Quaternion.Euler(euler);

            return fallback;
        }

        public class RayController
        {
            private const float DEG_CIRCLE_DIV_THREE = 120f;
            private const float DEG_CIRCLE_DIV_FIVE = 72f;
            private const float FACTOR_ONE_DIV_THREE = 0.33333334f;

            private static readonly int RAYCAST_LAYER_MASK;

            static RayController()
            {
                RAYCAST_LAYER_MASK =
                    Gallop.GraphicSettings.GetCullingLayer(
                        Gallop.GraphicSettings.LayerIndex.LayerCHAR) |
                    Gallop.GraphicSettings.GetCullingLayer(
                        Gallop.GraphicSettings.LayerIndex.LayerCharacter3D_NotReflect) |
                    Gallop.GraphicSettings.GetCullingLayer(
                        Gallop.GraphicSettings.LayerIndex.LayerCharacter3D_0) |
                    Gallop.GraphicSettings.GetCullingLayer(
                        Gallop.GraphicSettings.LayerIndex.LayerCharacter3D_1);
            }

            public static int RaycastLayerMask =>
                RAYCAST_LAYER_MASK;
            private Transform _parentTransform;
            private Transform _cachedTransform;
            private Renderer _renderer;
            private Material _material;

            private Vector3 _rotate;
            private Vector3 _dirForward;
            private Vector3 _dirRight;

            private float _distLevel;
            private float _rotationWeight;

            private bool _enable;

            public Transform ParentTransform => _parentTransform;
            public Transform CachedTransform => _cachedTransform;
            public Renderer Renderer => _renderer;

            public bool Enable
            {
                get => _enable;
                set => _enable = value;
            }
            public void Initialize(Transform transform, Material material)
            {
                _cachedTransform = transform;

                if (_cachedTransform == null)
                    return;

                _parentTransform = _cachedTransform.parent;

                _renderer = _cachedTransform.GetComponent<Renderer>();

                if (_renderer != null)
                {
                    // 官方：
                    // _material = RenderUtils.GetMaterial(_renderer);
                    // RenderUtils.CopyMaterial(material, _material, true);

                    _material = Gallop.RenderPipeline.RenderUtils.GetMaterial(_renderer);

                    if (material != null && _material != null)
                    {
                        Gallop.RenderPipeline.RenderUtils.CopyMaterial( material, _material, true);
                    }
                }
            }

            public void UpdateFormation(LaserFormation formation, int index)
            {
                UpdateFormation((int)formation, index);
            }

            private void UpdateFormation(int formation, int index)
            {
                //他吗的这里应该是MathDefine.VECTOR3_ZERO
                _dirForward = Vector3.zero;

                _distLevel = 1f;
                _rotationWeight = 1f;

                switch (formation)
                {
                    case 1:
                    {
                        _dirForward = Vector3.zero;
                        _distLevel = 0f;
                        break;
                    }

                    case 2:
                    {
                        _dirForward.x = index != 0 ? -1f : 1f;

                        _distLevel = 0.5f;
                        break;
                    }

                    case 3:
                    {
                        _dirForward.x = index - 1f;

                        break;
                    }

                    case 4:
                    {
                        _dirForward.x = (index & 1) != 0 ? -1f : 1f;

                        if (index > 1)
                        {
                            _distLevel = 1.5f;
                        }
                        else
                        {
                            _distLevel = 0.5f;
                            _rotationWeight = 0.33333334f;
                        }

                        break;
                    }

                    case 5:
                    {
                        if (index == 0)
                        {
                            _dirForward.x = 0f;
                            _distLevel = 0f;
                        }
                        else
                        {
                            _dirForward.x = index >= 3 ? 1f : -1f;

                            if ((index & 1) != 0)
                            {
                                _rotationWeight = 0.5f;
                            }
                            else
                            {
                                _distLevel = 2f;
                            }
                        }

                        break;
                    }

                    case 6:
                    case 7:
                    {
                        //保持 Vector3.zero
                        break;
                    }

                    case 8:
                    {
                        _dirForward.x = index != 0 ? -1f : 1f;

                        break;
                    }

                    case 9:
                    {
                        //Circle_3才显式使用forward
                        _dirForward.z = 1f;

                        float angle = index * 120f;

                        _dirForward = Quaternion.Euler( 0f, angle, 0f) * _dirForward;

                        _dirForward.Normalize();
                        break;
                    }

                    case 10:
                    {
                        float direction = index <= 1 ? -1f : 1f;

                        if ((index & 1) != 0)
                            _dirForward.z = direction;
                        else
                            _dirForward.x = direction;

                        break;
                    }

                    case 11:
                    {
                        //Circle_5才显式使用 orward
                        _dirForward.z = 1f;

                        float angle = index * 72f;

                        _dirForward = Quaternion.Euler( 0f, angle, 0f) * _dirForward;

                        _dirForward.Normalize();
                        break;
                    }
                }

                // 官方顺序
                _dirRight = Vector3.Cross(Vector3.up, _dirForward);
            }

            public void UpdatePitch(float degPitch)
            {
                if (_parentTransform == null)
                    return;

                _parentTransform.localRotation = Quaternion.AngleAxis(degPitch * _rotationWeight, _dirRight);
            }

            public void UpdatePositionInterval(float positionInterval)
            {
                if (_parentTransform == null)
                    return;

                float distance = positionInterval * _distLevel;

                _parentTransform.localPosition = _dirForward * distance;
            }

            public void UpdateRenderEnabled()
            {
                if (_renderer == null)
                    return;

                _renderer.enabled = _enable;
            }

            public void Raycast(bool enable, float rayDistance, float checkDistance)
            { 
                if (!_enable ||_renderer == null || !_renderer.enabled || _cachedTransform == null)
                {
                    return;
                }

                // 官方每帧先恢复基础长度。
                _cachedTransform.localScale = Vector3.one;

                if (!enable || checkDistance <= 0.000001f || rayDistance <= 0.000001f)
                {
                    return;
                }

                if (Physics.Raycast(_cachedTransform.position,_cachedTransform.up,out RaycastHit hit, checkDistance, RAYCAST_LAYER_MASK))
                {
                    Vector3 scale = _cachedTransform.localScale;
                    scale.y = hit.distance / rayDistance;
                    _cachedTransform.localScale = scale;
                }
            }

            public void Release()
            {
                Gallop.RenderPipeline.RenderUtils.Destroy(ref _material);

                _parentTransform = null;
                _cachedTransform = null;
                _renderer = null;

                _rotate = Vector3.zero;
                _dirForward = Vector3.zero;
                _dirRight = Vector3.zero;

                _distLevel = 0f;
                _rotationWeight = 0f;
                _enable = false;
            }
        }
        
    }
}