using Gallop.Live.Cutt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gallop.Live
{
    [Serializable]
    public class StageObjectUnit
    {
        public string UnitName;
        public GameObject[] ChildObjects;
        public string[] _childObjectNames;

        public bool IsEnabled { get; private set; }
        public int UnitNameHash { get; private set; }
        public int[] ChildObjectNameHashArray { get; private set; }

        public void Initialize()
        {
            IsEnabled = false;
            UnitNameHash = 0;
            ChildObjectNameHashArray = null;
            if (string.IsNullOrEmpty(UnitName)) return;

            UnitNameHash = FNVHash.Generate(UnitName);
            var hashes = new List<int>();
            if (ChildObjects != null)
                for (int i = 0; i < ChildObjects.Length; i++)
                    if (ChildObjects[i] != null)
                        hashes.Add(FNVHash.Generate(ChildObjects[i].name));
            if (_childObjectNames != null)
                for (int i = 0; i < _childObjectNames.Length; i++)
                    if (!string.IsNullOrEmpty(_childObjectNames[i]))
                        hashes.Add(FNVHash.Generate(_childObjectNames[i]));
            if (hashes.Count == 0) return;
            ChildObjectNameHashArray = hashes.ToArray();
            IsEnabled = true;
        }
    }

    [Serializable]
    public class BgColorBindingOverride
    {
        public string timelineName;
        public string[] rendererNameContains;
        public string[] materialNameContains;
    }

    [Serializable]
    public class NeonMaterialInfo
    {
        [SerializeField] private Material _mainMaterial;
        [SerializeField] private Material _backMaterial;

        public Material MainMaterial => _mainMaterial;
        public Material BackMaterial => _backMaterial;
    }

    public class StageController : MonoBehaviour
    {
        private enum BgColor2RuntimeKind
        {
            Wash,
            Laser,
            Foot,
            NeonMain,
            NeonBack,
            LegacyFallback
        }

        private sealed class BgColor2RuntimeGroup
        {
            public BgColor2RuntimeKind kind;
            public int sourceIndex;
            public Material sourceMaterial;
            public string sourceKey;
            public string sourceName;
            public readonly List<BgColor2RuntimeBinding> bindings = new List<BgColor2RuntimeBinding>(8);
            public readonly List<Renderer> renderers = new List<Renderer>(8);
        }

        private sealed class BgColor2RuntimeBinding
        {
            public Renderer renderer;
            public int materialIndex;
        }

        int _logEvery = 30;
        int _logCount = 0;
        public List<GameObject> _stageObjects;
        public StageObjectUnit[] _stageObjectUnits;
        public Dictionary<string, StageObjectUnit> StageObjectUnitMap = new Dictionary<string, StageObjectUnit>();
        public Dictionary<string, GameObject> StageObjectMap = new Dictionary<string, GameObject>();
        public Dictionary<string, Transform> StageParentMap = new Dictionary<string, Transform>();

        private sealed class StageObjectWorkInfo
        {
            public Transform transform;
            public Transform originalParent;
            public TransformBaseData startTransform;
            public Renderer[] renderers;
        }

        private readonly Dictionary<int, List<StageObjectWorkInfo>> _objectsWorkInfoDictionary =
            new Dictionary<int, List<StageObjectWorkInfo>>();
        private readonly Dictionary<int, List<Renderer>> _stageObjectRenderers =
            new Dictionary<int, List<Renderer>>();
        private readonly Dictionary<Renderer, int> _bgColor1RendererProperties = new Dictionary<Renderer, int>();
        private readonly List<Renderer> _ambientColorRenderers = new List<Renderer>();
        private UnityEngine.MaterialPropertyBlock _stageColorBlock;
        private static readonly int BgColor1MulColor0Id = Shader.PropertyToID("_MulColor0");
        private static readonly int BgColor1ColorPowerId = Shader.PropertyToID("_ColorPower");
        private static readonly int BgColor1AmbientColorId = Shader.PropertyToID("_AmbientColor");
        // Native static hash table +0x84 (global StageController.cctor): "BgBL", not "BG_BL".
        private static readonly int BgColor1AmbientTimelineHash = FNVHash.Generate("BgBL");
        private Color _currentAmbientColor;
        [SerializeField] private bool _autoAddBlinkDriver = true;

        [Header("Official-like BgColor2 material sources")]
        [SerializeField] private Material[] _washLightMaterials;
        [SerializeField]
        [Tooltip("レーザーオブジェクト")]
        private GameObject[] _laserObjects;

        private LaserController[] _laserControllerArray;

        [SerializeField]
        [Tooltip("レーザーマテリアル")]
        private Material[] _laserMaterials;

        
        private bool _laserSetupDone = false;
        private LiveTimelineControl _laserSetupTimelineControl;
        private readonly Dictionary<object, int> _laserDataIndexMap =
            new Dictionary<object, int>(ReferenceEqualityComparer.Instance);

        // 时间轴 LaserA/LaserB... 的字母实际对应 LiveTimelineLaserData._materialIndex。
        // 必须保存“材质索引 -> 该索引创建出的运行时 Renderer”，不能按材质名去重，
        // 否则多个同名的 Laser 实例会串色或只更新第一组。
        private readonly Dictionary<int, List<Renderer>> _laserRenderersByMaterialIndex =
            new Dictionary<int, List<Renderer>>();

        [SerializeField] private Material[] _footLightMaterials;
        [SerializeField] private NeonMaterialInfo[] _neonMaterialInfos;

        [Header("BgColor direct driver")]
        [SerializeField] private bool _enableBgColorDriver = true;
        [SerializeField] private bool _bgColorFallbackToAllEligible = true;
        [SerializeField] private bool _bgColorVerboseLog = false;
        [SerializeField] private float[] _bgColorExtraValueTable = new float[] { 1f };
        [SerializeField] private BgColorBindingOverride[] _bgColorBindingOverrides;

        private readonly Dictionary<string, List<Renderer>> _bgColorRendererCache = new Dictionary<string, List<Renderer>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Renderer> _bgColorAllEligibleRenderers = new List<Renderer>(256);
        private readonly HashSet<string> _bgColorMissingLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, List<BgColor2RuntimeGroup>> _bgColor2GroupCache = new Dictionary<string, List<BgColor2RuntimeGroup>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BgColor2RuntimeGroup> _bgColor2Groups = new List<BgColor2RuntimeGroup>(64);
        private readonly List<Renderer> _allStageRenderers = new List<Renderer>(256);
        private readonly Dictionary<Material, List<BgColor2RuntimeBinding>> _bindingsBySharedMaterialRef = new Dictionary<Material, List<BgColor2RuntimeBinding>>();
        private readonly Dictionary<string, List<BgColor2RuntimeBinding>> _bindingsBySharedMaterialName = new Dictionary<string, List<BgColor2RuntimeBinding>>(StringComparer.OrdinalIgnoreCase);

        [Header("Environment mirror update")]
        [SerializeField] private bool _enableEnvironmentMirrorUpdate = true;
        [SerializeField] private List<MirrorReflection> _environmentMirrorTargets = new List<MirrorReflection>(8);
        [SerializeField, HideInInspector] private float _currentMirrorReflectionRate = 1f;

        [Header("Mirror reflection specific update")]
        [SerializeField] private bool _enableMirrorReflectionSpecificUpdate = true;
        [SerializeField] private int _mirrorBgLayerMask = 0;
        [SerializeField] private int _mirror3dLayerMask = 0;
        [SerializeField] private bool _autoAttachMirrorReflection = true;
        [SerializeField] private bool _mirrorAutoAttachLog = true;
        private readonly Dictionary<int, MirrorReflection> _mirrorByTimelineHash = new Dictionary<int, MirrorReflection>();
        private LiveTimelineControl _boundTimelineControl;

        private void Awake()
        {
            _stageColorBlock = new UnityEngine.MaterialPropertyBlock();
            AutoAddDriver("StageBlinkLightDriver");
            AutoAddDriver("StageWashLightDriver");
            AutoAddDriver("StageUVScrollLightDriver");
            //AutoAddDriver("StageLaserDriver");

            InitializeStage();
            RebuildMirrorReflectionCache();
            RebuildBgColorCache();

            Debug.Log("[StageController] stage parts = " +
                string.Join(", ", _stageObjects.ConvertAll(o => o ? o.name : "<null>")));

            if (Director.instance)
                Director.instance._stageController = this;

            TryBindTimelineCallbacks();
        }

        private void OnEnable()
        {
            if (Director.instance)
                Director.instance._stageController = this;

            TryBindTimelineCallbacks();
        }

        // Laser 的 AlterUpdate 必须在 LiveTimelineControl 下发本帧 UpdateInfo 之后执行。
        // 由 Director.ApplyTimelineLateUpdate 显式调用，避免 MonoBehaviour.Update 顺序不确定。
        public void AlterUpdateLaserControllers()
        {
            if (_laserControllerArray == null)
                return;

            Transform currentCameraTransform = null;

            if (Director.instance != null)
            {
                currentCameraTransform = Director.instance.MainCameraTransform;
            }

            if (currentCameraTransform == null && Camera.main != null)
            {
                currentCameraTransform = Camera.main.transform;
            }

            for (int i = 0; i < _laserControllerArray.Length; i++)
            {
                LaserController controller =
                    _laserControllerArray[i];

                if (controller == null || !controller.IsInitialized)
                {
                    continue;
                }

                // 项目使用多台 Camera，并在切镜头时
                // 更换 MainCameraTransform 的引用
                controller.SetTargetCameraTransform(currentCameraTransform);

                controller.AlterUpdate();
            }
        }

        private void LateUpdate()
        {
            var dir = Director.instance;
            var ctl = dir ? dir._liveTimelineControl : null;

            if (_boundTimelineControl != ctl)
                TryBindTimelineCallbacks();

            if (!_laserSetupDone && _boundTimelineControl != null)
                TrySetupLaserObject(_boundTimelineControl);

            if (_laserControllerArray == null)
                return;

            for (int i = 0; i < _laserControllerArray.Length; i++)
            {
                LaserController controller = _laserControllerArray[i];
                if (controller != null && controller.IsInitialized)
                    controller.AlterLateUpdate();
            }
        }

        private void TryBindTimelineCallbacks()
        {
            var dir = Director.instance;
            if (!dir)
                return;

            dir._stageController = this;

            var ctl = dir._liveTimelineControl;
            if (ctl == null)
                return;

            if (_boundTimelineControl == ctl)
                return;

            UnbindTimelineCallbacks(_boundTimelineControl);
            _boundTimelineControl = ctl;

            ctl.OnUpdateTransform -= UpdateTransform;
            ctl.OnUpdateObject -= UpdateObject;
            ctl.OnUpdateBgColor1 -= UpdateBgColor1;
            ctl.OnUpdateBgColor2 -= UpdateBgColor2;
            ctl.OnEnvironmentMirror -= UpdateEnvironemntMirror;
            ctl.OnUpdateMirrorReflection -= UpdateMirrorReflection;
            ctl.OnUpdateLaser -= UpdateLaser;

            ctl.OnUpdateTransform += UpdateTransform;
            ctl.OnUpdateObject += UpdateObject;
            ctl.OnUpdateBgColor1 += UpdateBgColor1;
            ctl.OnUpdateBgColor2 += UpdateBgColor2;
            ctl.OnEnvironmentMirror += UpdateEnvironemntMirror;
            ctl.OnUpdateMirrorReflection += UpdateMirrorReflection;
            ctl.OnUpdateLaser += UpdateLaser;

            _laserSetupDone = false;
            _laserSetupTimelineControl = ctl;
            _laserDataIndexMap.Clear();

            TrySetupLaserObject(ctl);
        }
        private GameObject[] LoadDefaultLaserObjects()
        {
            string[] paths =
            {
                "3d/effect/live/pfb_eff_live_laser_01",
                "3d/effect/live/pfb_eff_live_laser_02",
                "3d/effect/live/pfb_eff_live_laser_03",
                "3d/effect/live/pfb_eff_live_laser_04",
            };

            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return Array.Empty<GameObject>();

            var list = new List<GameObject>();

            foreach (string path in paths)
            {
                if (!main.AbList.TryGetValue(path, out var entry) || entry == null)
                {
                    Debug.LogWarning("[StageController] missing laser object entry: " + path);
                    continue;
                }

                AssetBundle ab = UmaAssetManager.LoadAssetBundle(entry, neverUnload: true, isRecursive: true);
                if (ab == null)
                {
                    Debug.LogWarning("[StageController] load laser bundle failed: " + path);
                    continue;
                }

                string prefabName = path.Substring(path.LastIndexOf('/') + 1);
                GameObject prefab = null;

                foreach (string assetName in ab.GetAllAssetNames())
                {
                    if (assetName.IndexOf(prefabName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        prefab = ab.LoadAsset<GameObject>(assetName);
                        if (LiveRuntimeDiagnostics.Enabled)
                            LiveRuntimeDiagnostics.RecordAssetLoad(entry.Name, "LoadAsset", typeof(GameObject),
                                new UnityEngine.Object[] { prefab }, prefab);
                        if (prefab != null)
                            break;
                    }
                }

                if (prefab == null)
                {
                    GameObject[] allPrefabs = ab.LoadAllAssets<GameObject>();
                    if (LiveRuntimeDiagnostics.Enabled)
                        LiveRuntimeDiagnostics.RecordAssetLoad(entry.Name, "LoadAllAssets", typeof(GameObject), allPrefabs);
                    foreach (GameObject go in allPrefabs)
                    {
                        if (go != null && go.name.IndexOf(prefabName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            prefab = go;
                            break;
                        }
                    }
                }

                if (prefab != null)
                {
                    Debug.Log("[StageController] loaded laser object: " + prefab.name);
                    list.Add(prefab);
                }
                else
                {
                    Debug.LogWarning("[StageController] prefab not found inside bundle: " + path);
                }
            }

            return list.ToArray();
        }
        private void TrySetupLaserObject(LiveTimelineControl ctl)
        {
            if (ctl == null || ctl.data == null)
                return;

            SetupLaserObject(ctl.data);

            if (_laserControllerArray != null)
            {
                _laserSetupDone = true;
                Debug.Log("[StageController] Laser setup done. count=" + _laserControllerArray.Length);
            }
        }

        private void SetupLaserObject(LiveTimelineData liveTimelineData)
        {
            ReleaseLaserControllers();
            _laserControllerArray = null;
            _laserDataIndexMap.Clear();
            _laserRenderersByMaterialIndex.Clear();

            // 优先使用舞台/Live 资源加载流程已经设置好的专用 Laser prefab。
            // 没有专用资源时，才回退到多个 Live 共用的通用 Laser prefab。
            if (_laserObjects == null || _laserObjects.Length == 0)
                _laserObjects = LoadDefaultLaserObjects();

            if (_laserObjects == null || _laserObjects.Length == 0)
            {
                Debug.LogWarning("[StageController] no laser prefabs loaded");
                return;
            }

            Transform cameraTransform = ResolveLaserCameraTransform();
            if (cameraTransform == null)
            {
                Debug.LogWarning("[StageController] cameraTransform is null for laser");
                return;
            }

            IList worksheets = GetWorksheetList(liveTimelineData);
            if (worksheets == null || worksheets.Count == 0)
            {
                Debug.LogWarning("[StageController] worksheetList is empty for laser");
                return;
            }

            var result = new List<LaserController>();

            for (int wsIndex = 0; wsIndex < worksheets.Count; wsIndex++)
            {
                object worksheet = worksheets[wsIndex];
                if (worksheet == null)
                    continue;

                IList laserList = GetMemberValue(worksheet, "laserList") as IList;
                if (laserList == null || laserList.Count == 0)
                    continue;

                for (int i = 0; i < laserList.Count; i++)
                {
                    object laserData = laserList[i];
                    int objectIndex = GetLaserObjectIndex(laserData);

                    if (objectIndex < 0 || objectIndex >= _laserObjects.Length)
                    {
                        Debug.LogWarning($"[StageController] invalid laser prefab index={objectIndex}, prefabCount={_laserObjects.Length}; fallback to 0");
                        DumpLaserDataIntFields(laserData);
                        objectIndex = 0;
                    }

                    GameObject prefab = _laserObjects[objectIndex];
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[StageController] laser prefab is null. objectIndex={objectIndex}");
                        continue;
                    }

                    // _materialIndex 是时间轴颜色分组索引：LaserA/B/C -> 0/1/2。
                    // 它不能因为 _laserMaterials 模板数组长度不足而被 Clamp，
                    // 否则 B/C 会合并到同一颜色组。
                    int timelineMaterialIndex = Mathf.Max(
                        0,
                        GetLaserMaterialIndex(laserData, result.Count));

                    Material material = ResolveLaserSourceMaterial(
                        timelineMaterialIndex);

                    var controller = new LaserController();
                    try
                    {
                        controller.Initialize(prefab, transform, cameraTransform, material);
                    }
                    catch (Exception ex)
                    {
                        controller.Release();
                        Debug.LogException(ex);
                        continue;
                    }

                    if (!controller.IsInitialized)
                    {
                        controller.Release();
                        Debug.LogWarning($"[StageController] laser initialize failed. prefab={prefab.name}, objectIndex={objectIndex}");
                        continue;
                    }

                    int controllerIndex = result.Count;
                    if (laserData != null && !_laserDataIndexMap.ContainsKey(laserData))
                        _laserDataIndexMap.Add(laserData, controllerIndex);

                    if (!_laserRenderersByMaterialIndex.TryGetValue(timelineMaterialIndex, out var laserRenderers))
                    {
                        laserRenderers = new List<Renderer>();
                        _laserRenderersByMaterialIndex.Add(timelineMaterialIndex, laserRenderers);
                    }
                    controller.AppendRuntimeRenderers(laserRenderers);

                    result.Add(controller);

                    Debug.Log(
                        $"[StageController] instantiated laser index={controllerIndex}, " +
                        $"prefab={prefab.name}, objectIndex={objectIndex}, " +
                        $"timelineMaterialIndex={timelineMaterialIndex}, " +
                        $"sourceMaterial={(material != null ? material.name : "<prefab>")}");
                }
            }

            _laserControllerArray = result.ToArray();

            // Laser 在 Awake 之后动态实例化，必须重新建立 BgColor2 的
            // Renderer/Material 绑定，否则 LaserA、LaserB 等颜色轨不会生效。
            PopulateLaserMaterialsFromRuntimeIfNeeded();
            RebuildBgColorCache();

            Debug.Log("[StageController] SetupLaserObject complete. count=" + _laserControllerArray.Length);
        }

        private Material ResolveLaserSourceMaterial(int timelineMaterialIndex)
        {
            if (_laserMaterials == null || _laserMaterials.Length == 0)
                return null;

            // 有对应模板时直接使用。
            if (timelineMaterialIndex >= 0 &&
                timelineMaterialIndex < _laserMaterials.Length &&
                _laserMaterials[timelineMaterialIndex] != null)
            {
                return _laserMaterials[timelineMaterialIndex];
            }

            // 模板数组不完整时只回退 shader/material 模板，
            // 颜色分组仍保留原始 timelineMaterialIndex。
            for (int i = 0; i < _laserMaterials.Length; i++)
            {
                if (_laserMaterials[i] != null)
                    return _laserMaterials[i];
            }

            return null;
        }

        private void PopulateLaserMaterialsFromRuntimeIfNeeded()
        {
            if (_laserMaterials != null && _laserMaterials.Any(m => m != null))
                return;

            if (_laserControllerArray == null || _laserControllerArray.Length == 0)
                return;

            var runtimeMaterials = new List<Material>();
            for (int i = 0; i < _laserControllerArray.Length; i++)
            {
                LaserController controller = _laserControllerArray[i];
                controller?.AppendRuntimeMaterials(runtimeMaterials);
            }

            var unique = new List<Material>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                Material material = runtimeMaterials[i];
                if (material == null)
                    continue;

                string key = NormalizeKey(CleanMaterialName(material.name));
                if (string.IsNullOrEmpty(key))
                    key = material.GetInstanceID().ToString();

                if (seen.Add(key))
                    unique.Add(material);
            }

            _laserMaterials = unique.ToArray();

            if (_bgColorVerboseLog)
                Debug.Log($"[StageController] collected runtime laser materials={_laserMaterials.Length}");
        }

        private GameObject[] FindExistingStageLaserObjects()
        {
            Transform[] all = transform.GetComponentsInChildren<Transform>(true);
            var result = new List<GameObject>();

            for (int i = 0; i < all.Length; i++)
            {
                Transform candidate = all[i];
                if (candidate == null || candidate == transform)
                    continue;

                string n = candidate.name;
                if (string.IsNullOrEmpty(n) || n.IndexOf("_laser", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (FindChildRecursive(candidate, "lasercontroller") == null)
                    continue;

                bool nestedInAnotherLaser = false;
                Transform parent = candidate.parent;
                while (parent != null && parent != transform)
                {
                    if (parent.name.IndexOf("_laser", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        FindChildRecursive(parent, "lasercontroller") != null)
                    {
                        nestedInAnotherLaser = true;
                        break;
                    }
                    parent = parent.parent;
                }

                if (!nestedInAnotherLaser)
                    result.Add(candidate.gameObject);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
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

        private void UpdateLaser(ref LaserUpdateInfo updateInfo)
        {
            if (_laserControllerArray == null)
                return;

            int index =
                updateInfo.timelineIndex;

            if (index < 0 ||
                index >= _laserControllerArray.Length)
            {
                return;
            }

            LaserController controller =_laserControllerArray[index];

            controller?.UpdateInfo(ref updateInfo);
        }

        private void ReleaseLaserControllers()
        {
            if (_laserControllerArray == null)
                return;

            for (int i = 0; i < _laserControllerArray.Length; i++)
            {
                if (_laserControllerArray[i] != null)
                    _laserControllerArray[i].Release();
            }

            _laserControllerArray = null;
            _laserRenderersByMaterialIndex.Clear();
        }

        private Transform ResolveLaserCameraTransform()
        {
            if (Director.instance != null && Director.instance.MainCameraTransform != null)
                return Director.instance.MainCameraTransform;

            if (Camera.main != null)
                return Camera.main.transform;

            if (Director.instance != null)
            {
                Camera cam = Director.instance.GetComponentInChildren<Camera>(true);
                if (cam != null)
                    return cam.transform;
            }

            Camera anyCam = FindObjectOfType<Camera>();
            return anyCam != null ? anyCam.transform : null;
        }

        private static IList GetWorksheetList(LiveTimelineData data)
        {
            if (data == null)
                return null;

            object v = GetMemberValue(data, "worksheetList");
            return v as IList;
        }

        private int GetLaserUpdateIndex(LaserUpdateInfo info)
        {
            return info.timelineIndex;
        }

        private static int GetLaserObjectIndex(object laserData)
        {
            int v;

            // 官方 LiveTimelineLaserData 实际字段名。
            v = GetIntMember(laserData, "_objectIndex", int.MinValue);
            if (v != int.MinValue) return v;

            v = GetIntMember(laserData, "objectIndex", int.MinValue);
            if (v != int.MinValue) return v;

            v = GetIntMember(laserData, "laserObjectIndex", int.MinValue);
            if (v != int.MinValue) return v;

            v = GetIntMember(laserData, "prefabIndex", int.MinValue);
            if (v != int.MinValue) return v;

            v = GetIntMember(laserData, "laserPrefabIndex", int.MinValue);
            if (v != int.MinValue) return v;

            v = GetIntMember(laserData, "assetIndex", int.MinValue);
            if (v != int.MinValue) return v;

            // fallback：官方普通 live 默认从 pfb_eff_live_laser_01 开始
            return 0;
        }

        private int GetLaserMaterialIndex(object laserData, int fallbackOrder)
        {
            int v;

            // 官方 LiveTimelineLaserData 实际字段名。
            v = GetIntMember(laserData, "_materialIndex", int.MinValue);
            if (v != int.MinValue) return v;

            v = GetIntMember(laserData, "materialIndex", int.MinValue);
            if (v != int.MinValue) return v;

            v = GetIntMember(laserData, "laserMaterialIndex", int.MinValue);
            if (v != int.MinValue) return v;

            v = GetIntMember(laserData, "matIndex", int.MinValue);
            if (v != int.MinValue) return v;

            v = GetIntMember(laserData, "materialNo", int.MinValue);
            if (v != int.MinValue) return v;

            if (_laserMaterials != null && _laserMaterials.Length > 0)
                return Mathf.Clamp(fallbackOrder, 0, _laserMaterials.Length - 1);

            return 0;
        }

        private static int GetIntMember(object obj, string name, int fallback)
        {
            object v = GetMemberValue(obj, name);

            if (v is int i)
                return i;

            return fallback;
        }

        private static object GetMemberValue(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name))
                return null;

            Type t = obj.GetType();

            while (t != null)
            {
                var f = t.GetField(
                    name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                if (f != null)
                    return f.GetValue(obj);

                var p = t.GetProperty(
                    name,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);

                if (p != null)
                    return p.GetValue(obj, null);

                t = t.BaseType;
            }

            return null;
        }

        private static void DumpLaserDataIntFields(object data)
        {
            if (data == null)
                return;

            Debug.Log("[LaserDataDump] type=" + data.GetType().FullName);

            var fields = data.GetType().GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            foreach (var f in fields)
            {
                if (f.FieldType == typeof(int))
                    Debug.Log("[LaserDataDump] int " + f.Name + "=" + f.GetValue(data));
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return obj != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj) : 0;
            }
        }
        
        private void AutoAddDriver(string shortTypeName)
        {
            Type t = null;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType($"Gallop.Live.{shortTypeName}") ?? asm.GetType(shortTypeName);

                if (t != null)
                    break;
            }

            if (t == null)
            {
                Debug.LogWarning($"[StageController] AutoAddDriver type not found: {shortTypeName}");
                return;
            }

            if (!typeof(MonoBehaviour).IsAssignableFrom(t))
            {
                Debug.LogWarning($"[StageController] AutoAddDriver not MonoBehaviour: {shortTypeName}");
                return;
            }

            if (GetComponent(t) == null)
            {
                gameObject.AddComponent(t);
                Debug.Log($"[StageController] AutoAddDriver added: {t.FullName}");
            }
        }

        private void OnDisable()
        {
            UnbindTimelineCallbacks(_boundTimelineControl);
            _boundTimelineControl = null;
        }

        private void OnDestroy()
        {
            UnbindTimelineCallbacks(_boundTimelineControl);
            _boundTimelineControl = null;
        }

        private void UnbindTimelineCallbacks(LiveTimelineControl ctl)
        {
            if (ctl == null)
                return;

            ctl.OnUpdateTransform -= UpdateTransform;
            ctl.OnUpdateObject -= UpdateObject;
            ctl.OnUpdateBgColor1 -= UpdateBgColor1;
            ctl.OnUpdateBgColor2 -= UpdateBgColor2;
            ctl.OnEnvironmentMirror -= UpdateEnvironemntMirror;
            ctl.OnUpdateMirrorReflection -= UpdateMirrorReflection;
            ctl.OnUpdateLaser -= UpdateLaser;
        }

        private void AutoAttachMirrorReflectionComponents()
        {
            if (!_autoAttachMirrorReflection)
                return;

            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            int attachedCount = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!ShouldAutoAttachMirrorReflection(renderer))
                    continue;

                renderer.gameObject.AddComponent<MirrorReflection>();
                attachedCount++;

                if (_mirrorAutoAttachLog)
                    Debug.Log($"[StageController] Auto attached MirrorReflection -> {GetTransformPath(renderer.transform)}");
            }

            if (attachedCount > 0)
                Debug.Log($"[StageController] Auto attached {attachedCount} MirrorReflection component(s).");
        }

        private bool ShouldAutoAttachMirrorReflection(Renderer renderer)
        {
            if (renderer == null)
                return false;

            if (renderer.GetComponent<MirrorReflection>() != null)
                return false;

            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                return false;

            return HasRenderableMirrorMaterial(renderer) || HasMirrorLikeName(renderer.transform);
        }

        private bool HasRenderableMirrorMaterial(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return false;

            for (int i = 0; i < materials.Length; i++)
            {
                var mat = materials[i];
                if (mat == null)
                    continue;

                if (mat.HasProperty("_ReflectionRate") || mat.HasProperty("_ReflectionTex"))
                    return true;

                if (mat.name.IndexOf("mirror", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private bool HasMirrorLikeName(Transform tr)
        {
            while (tr != null && tr != transform)
            {
                string candidate = StripCloneSuffix(tr.name);
                if (candidate.IndexOf("mirror", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                tr = tr.parent;
            }

            return false;
        }

        private string GetTransformPath(Transform tr)
        {
            if (tr == null)
                return "<null>";

            var names = new List<string>(8);
            while (tr != null && tr != transform)
            {
                names.Add(tr.name);
                tr = tr.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private void RebuildMirrorReflectionCache()
        {
            _environmentMirrorTargets.Clear();
            _mirrorByTimelineHash.Clear();
            AutoAttachMirrorReflectionComponents();

            var localMirrors = GetComponentsInChildren<MirrorReflection>(true);
            if (localMirrors != null)
            {
                for (int i = 0; i < localMirrors.Length; i++)
                {
                    RegisterMirrorReflection(localMirrors[i]);
                }
            }

            if (_environmentMirrorTargets.Count == 0)
            {
                var allMirrors = FindObjectsOfType<MirrorReflection>(true);
                if (allMirrors != null)
                {
                    for (int i = 0; i < allMirrors.Length; i++)
                    {
                        RegisterMirrorReflection(allMirrors[i]);
                    }
                }
            }
        }

        private void RegisterMirrorReflection(MirrorReflection mirror)
        {
            if (mirror == null)
                return;

            if (!_environmentMirrorTargets.Contains(mirror))
                _environmentMirrorTargets.Add(mirror);

            RegisterMirrorReflectionAlias(mirror, mirror.name);

            Transform current = mirror.transform.parent;
            while (current != null && current != transform)
            {
                RegisterMirrorReflectionAlias(mirror, current.name);
                current = current.parent;
            }
        }

        private void RegisterMirrorReflectionAlias(MirrorReflection mirror, string alias)
        {
            RegisterMirrorReflectionHash(mirror, GenerateMirrorTimelineHash(alias));

            string normalizedAlias = StripCloneSuffix(alias);
            if (!string.Equals(normalizedAlias, alias, StringComparison.Ordinal))
                RegisterMirrorReflectionHash(mirror, GenerateMirrorTimelineHash(normalizedAlias));
        }

        private void RegisterMirrorReflectionHash(MirrorReflection mirror, int hash)
        {
            if (mirror == null || hash == 0 || _mirrorByTimelineHash.ContainsKey(hash))
                return;

            _mirrorByTimelineHash.Add(hash, mirror);
        }

        private static string StripCloneSuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return name.Replace("(Clone)", "");
        }

        private static int GenerateMirrorTimelineHash(string timelineName)
        {
            if (string.IsNullOrEmpty(timelineName))
                return 0;

            unchecked
            {
                const uint offset = 2166136261u;
                const uint prime = 16777619u;

                uint hash = offset;
                for (int i = 0; i < timelineName.Length; i++)
                {
                    hash ^= timelineName[i];
                    hash *= prime;
                }
                return (int)hash;
            }
        }

        private void UpdateEnvironemntMirror(ref EnvironmentMirrorUpdateInfo updateInfo)
        {
            if (!_enableEnvironmentMirrorUpdate)
                return;

            if (!updateInfo.isValid)
                return;

            if (_environmentMirrorTargets == null || _environmentMirrorTargets.Count == 0)
                RebuildMirrorReflectionCache();

            _currentMirrorReflectionRate = updateInfo.mirrorReflectionRate;

            for (int i = 0; i < _environmentMirrorTargets.Count; i++)
            {
                var mirror = _environmentMirrorTargets[i];
                if (mirror == null)
                    continue;

                mirror.SetActive(updateInfo.mirror || updateInfo.bgMirror || updateInfo.IsMirrorBg3d);
                mirror.SetLightMirrorShader(!updateInfo.IsToonMirror);
                mirror.MirrorReflectionRate = updateInfo.mirrorReflectionRate;
            }
        }

        private void UpdateMirrorReflection(in LiveTimelineControl.MirrorReflectionUpdateInfo updateInfo)
        {
            if (!_enableMirrorReflectionSpecificUpdate)
                return;

            if (_mirrorByTimelineHash.Count == 0)
                RebuildMirrorReflectionCache();

            if (!_mirrorByTimelineHash.TryGetValue(updateInfo.TimelineNameHash, out var mirror))
            {
                RebuildMirrorReflectionCache();
                if (!_mirrorByTimelineHash.TryGetValue(updateInfo.TimelineNameHash, out mirror))
                    return;
            }

            if (mirror == null)
                return;

            var mainCamera = Camera.main;
            if (mainCamera != null)
                mirror.SetBaseCamera(mainCamera);

            mirror.SetActive(updateInfo.EnableMirror);
            mirror.SetLightMirrorShader(!updateInfo.IsToonMirror);
            mirror.MirrorReflectionRate = updateInfo.MirrorReflectionRate;

            mirror.ResetCullingMask();

            if (_mirrorBgLayerMask != 0)
            {
                if (updateInfo.EnableBgLayer) mirror.AddCullingMask(_mirrorBgLayerMask);
                else mirror.RemoveCullingMask(_mirrorBgLayerMask);
            }

            if (_mirror3dLayerMask != 0)
            {
                if (updateInfo.Enable3dLayer) mirror.AddCullingMask(_mirror3dLayerMask);
                else mirror.RemoveCullingMask(_mirror3dLayerMask);
            }
        }

        public void InitializeStage()
        {
            foreach (GameObject stage_part in _stageObjects)
            {
                if (stage_part == null)
                {
                    Debug.LogWarning("[StageController] 跳过 _stageObjects 中的 null 条目");
                    continue;
                }

                var instance = Instantiate(stage_part, transform);
                LiveRuntimeDiagnostics.RecordInstantiation(stage_part.name, instance);
                int rootHash = FNVHash.Generate(stage_part.name);
                if (!_stageObjectRenderers.TryGetValue(rootHash, out var rootRenderers))
                {
                    rootRenderers = new List<Renderer>();
                    _stageObjectRenderers.Add(rootHash, rootRenderers);
                }
                rootRenderers.AddRange(instance.GetComponentsInChildren<Renderer>(true));

                int missingCount = CountMissingScripts(instance);
                if (missingCount > 0)
                {
                    Debug.LogWarning($"[StageController] '{stage_part.name}' 实例化后有 {missingCount} 个 missing script 组件");
                }

                foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                {
                    string objectName = child.name.Replace("(Clone)", "");
                    if (!StageObjectMap.ContainsKey(objectName))
                    {
                        if (child.name.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0)
                            child.gameObject.SetActive(true);
                        StageObjectMap.Add(objectName, child.gameObject);
                        StageParentMap.Add(objectName, child.parent);
                    }

                    int objectHash = FNVHash.Generate(objectName);
                    if (!_objectsWorkInfoDictionary.TryGetValue(objectHash, out var works))
                    {
                        works = new List<StageObjectWorkInfo>();
                        _objectsWorkInfoDictionary.Add(objectHash, works);
                    }
                    works.Add(new StageObjectWorkInfo
                    {
                        transform = child,
                        originalParent = child.parent,
                        startTransform = new TransformBaseData
                        {
                            position = child.localPosition,
                            rotation = child.localRotation,
                            scale = child.localScale
                        },
                        renderers = child.GetComponents<Renderer>()
                    });
                }
            }

            if (_stageObjectUnits != null)
                foreach (var unit in _stageObjectUnits)
                {
                    if (unit == null) continue;
                    unit.Initialize();
                    if (unit.IsEnabled && !StageObjectUnitMap.ContainsKey(unit.UnitName))
                        StageObjectUnitMap.Add(unit.UnitName, unit);
                }

            AutoAttachMirrorReflectionComponents();
            var washBeams = GetComponent<StageWashBeamDriver>();
            if (washBeams == null) washBeams = gameObject.AddComponent<StageWashBeamDriver>();
            washBeams.Initialize();
        }

        private StageObjectUnit FindStageObjectUnit(int hash)
        {
            if (_stageObjectUnits == null) return null;
            for (int i = 0; i < _stageObjectUnits.Length; i++)
            {
                var unit = _stageObjectUnits[i];
                if (unit != null && unit.IsEnabled && unit.UnitNameHash == hash)
                    return unit;
            }
            return null;
        }

        public void UpdateObject(ref ObjectUpdateInfo updateInfo)
        {
            if (updateInfo.data == null || string.IsNullOrEmpty(updateInfo.data.name)) return;
            int hash = updateInfo.data.nameHash;
            if (hash == 0) hash = FNVHash.Generate(updateInfo.data.name);
            var unit = FindStageObjectUnit(hash);
            if (unit != null)
            {
                var childHashes = unit.ChildObjectNameHashArray;
                for (int i = 0; i < childHashes.Length; i++)
                    UpdateObjectByHash(childHashes[i], ref updateInfo);
                return;
            }
            UpdateObjectByHash(hash, ref updateInfo);
        }

        private void UpdateObjectByHash(int hash, ref ObjectUpdateInfo updateInfo)
        {
            // Native has a prefab renderer-array branch and a per-transform work branch.
            if (_stageObjectRenderers.TryGetValue(hash, out var rootRenderers))
                for (int i = 0; i < rootRenderers.Count; i++)
                    if (rootRenderers[i] != null) rootRenderers[i].enabled = updateInfo.renderEnable;

            if (!_objectsWorkInfoDictionary.TryGetValue(hash, out var works)) return;
            for (int i = 0; i < works.Count; i++)
            {
                var work = works[i];
                var tr = work.transform;
                if (tr == null) continue;
                Transform parent = work.originalParent;
                var director = Director.instance;
                switch (updateInfo.AttachTarget)
                {
                    case AttachType.Character:
                        if (director != null && updateInfo.CharacterPosition >= 0 &&
                            updateInfo.CharacterPosition < director.CharaContainerScript.Count)
                        {
                            var chara = director.CharaContainerScript[updateInfo.CharacterPosition];
                            if (chara != null) parent = chara.transform;
                        }
                        break;
                    case AttachType.Camera:
                        if (director != null && director.MainCameraTransform != null)
                            parent = director.MainCameraTransform;
                        break;
                }
                if (tr.parent != parent) tr.SetParent(parent, false);

                var start = work.startTransform;
                Vector3 position = updateInfo.data.enablePosition ? updateInfo.updateData.position : start.position;
                Quaternion rotation = updateInfo.data.enableRotate ? updateInfo.updateData.rotation : start.rotation;
                Vector3 scale = updateInfo.data.enableScale ? updateInfo.updateData.scale : start.scale;
                if (updateInfo.OffsetType == OffsetType.Add)
                {
                    position += start.position;
                    rotation = start.rotation * rotation;
                    scale = Vector3.Scale(start.scale, scale);
                }
                tr.localPosition = position;
                tr.localRotation = rotation;
                tr.localScale = scale;
                for (int r = 0; r < work.renderers.Length; r++)
                    if (work.renderers[r] != null) work.renderers[r].enabled = updateInfo.renderEnable;
            }
        }

        public void UpdateTransform(ref TransformUpdateInfo updateInfo)
        {
            if (updateInfo.data == null || string.IsNullOrEmpty(updateInfo.data.name)) return;
            int hash = updateInfo.data.nameHash;
            if (hash == 0) hash = FNVHash.Generate(updateInfo.data.name.Replace("(Clone)", ""));
            var unit = FindStageObjectUnit(hash);
            if (unit != null)
            {
                var childHashes = unit.ChildObjectNameHashArray;
                for (int i = 0; i < childHashes.Length; i++)
                    UpdateTransformByHash(childHashes[i], ref updateInfo);
                return;
            }
            UpdateTransformByHash(hash, ref updateInfo);
        }

        private void UpdateTransformByHash(int hash, ref TransformUpdateInfo updateInfo)
        {
            if (!_objectsWorkInfoDictionary.TryGetValue(hash, out var works)) return;
            for (int i = 0; i < works.Count; i++)
            {
                var work = works[i];
                var tr = work.transform;
                if (tr == null) continue;
                tr.localPosition = updateInfo.data.enablePosition ? updateInfo.updateData.position : work.startTransform.position;
                tr.localRotation = updateInfo.data.enableRotate ? updateInfo.updateData.rotation : work.startTransform.rotation;
                tr.localScale = updateInfo.data.enableScale ? updateInfo.updateData.scale : work.startTransform.scale;
            }
        }

        private void UpdateBgColor1(ref BgColor1UpdateInfo updateInfo)
        {
            if (!_enableBgColorDriver) return;
            if (updateInfo.TimelineNameHash == BgColor1AmbientTimelineHash)
            {
                UpdateAmbientColor1(ref updateInfo);
                return;
            }
            if (!_objectsWorkInfoDictionary.TryGetValue(updateInfo.TimelineNameHash, out var works)) return;
            for (int i = 0; i < works.Count; i++)
            {
                var renderers = works[i].renderers;
                for (int r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null || !_bgColor1RendererProperties.TryGetValue(renderer, out int properties)) continue;
                    renderer.GetPropertyBlock(_stageColorBlock);
                    if ((properties & 1) != 0) _stageColorBlock.SetColor(BgColor1MulColor0Id, updateInfo.color);
                    if ((properties & 2) != 0) _stageColorBlock.SetFloat(BgColor1ColorPowerId, updateInfo.colorPower);
                    renderer.SetPropertyBlock(_stageColorBlock);
                }
            }
        }

        private void UpdateAmbientColor1(ref BgColor1UpdateInfo updateInfo)
        {
            _currentAmbientColor = updateInfo.color * updateInfo.colorPower;
            for (int i = 0; i < _ambientColorRenderers.Count; i++)
            {
                var renderer = _ambientColorRenderers[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(_stageColorBlock);
                _stageColorBlock.SetVector(BgColor1AmbientColorId, _currentAmbientColor);
                renderer.SetPropertyBlock(_stageColorBlock);
            }
        }

        private void UpdateBgColor2(ref BgColor2UpdateInfo updateInfo)
        {
            if (!_enableBgColorDriver)
                return;

            float extra = ResolveBgColorExtraValue(updateInfo.randomTableIndex);

            var groups = ResolveBgColor2Groups(updateInfo.TimelineName);
            if (groups != null && groups.Count > 0)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    var group = groups[i];
                    if (group == null) continue;
                    for (int j = 0; j < group.bindings.Count; j++)
                    {
                        var binding = group.bindings[j];
                        if (binding == null) continue;

                        var r = binding.renderer;
                        if (r == null) continue;

                        Material[] mats;
                        try { mats = r.materials; }
                        catch { continue; }
                        if (mats == null) continue;

                        if (binding.materialIndex < 0 || binding.materialIndex >= mats.Length)
                            continue;

                        var mat = mats[binding.materialIndex];
                        if (mat == null) continue;

                        ApplyBgColor2ToRuntimeGroup(group.kind, mat, ref updateInfo, extra);
                    }
                }
                return;
            }

            // 退回到旧的名字匹配，仅作为最后兜底。
            var legacyTargets = ResolveBgColorRenderers(updateInfo.TimelineName, wantBgColor2Style: true, allowAllEligibleFallback: false);
            var legacyRendererTargets = ResolveBgColorRenderers(updateInfo.TimelineName, wantBgColor2Style: true, allowAllEligibleFallback: false);
            if (legacyRendererTargets == null || legacyRendererTargets.Count == 0)
                return;

            for (int i = 0; i < legacyRendererTargets.Count; i++)
            {
                var r = legacyRendererTargets[i];
                if (r == null) continue;

                Material[] mats;
                try { mats = r.materials; }
                catch { continue; }
                if (mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;
                    ApplyBgColor2ToRuntimeGroup(BgColor2RuntimeKind.LegacyFallback, mat, ref updateInfo, extra);
                }
            }
        }

        private void ApplyBgColor2ToRuntimeGroup(BgColor2RuntimeKind kind, Material mat, ref BgColor2UpdateInfo updateInfo, float extra)
        {
            if (mat == null)
                return;

            switch (kind)
            {
                case BgColor2RuntimeKind.Wash:
                    ApplyOrdinaryBgColor2(mat, updateInfo.color1, updateInfo.color2, updateInfo.power, setExtraMultiply: false, extraValue: extra);
                    break;

                case BgColor2RuntimeKind.Laser:
                    ApplyOrdinaryBgColor2(mat, updateInfo.color1, updateInfo.color2, updateInfo.power, setExtraMultiply: true, extraValue: extra);
                    break;

                case BgColor2RuntimeKind.Foot:
                    ApplyOrdinaryBgColor2(mat, updateInfo.color1, updateInfo.color2, updateInfo.power, setExtraMultiply: false, extraValue: extra);
                    break;

                case BgColor2RuntimeKind.NeonMain:
                    ApplyNeonMainBgColor2(mat, updateInfo.color1, updateInfo.color2, updateInfo.power);
                    break;

                case BgColor2RuntimeKind.NeonBack:
                    ApplyNeonBackBgColor2(mat, updateInfo.color1, updateInfo.power);
                    break;

                default:
                    ApplyLegacyBgColor2(mat, updateInfo.color1, updateInfo.color2, updateInfo.power, extra);
                    break;
            }
        }

        private static void ApplyOrdinaryBgColor2(Material mat, Color color1, Color color2, float power, bool setExtraMultiply, float extraValue)
        {
            bool hasAny = false;

            if (mat.HasProperty("_MulColor0"))
            {
                mat.SetColor("_MulColor0", color1);
                hasAny = true;
            }
            if (mat.HasProperty("_MulColor1"))
            {
                mat.SetColor("_MulColor1", color2);
                hasAny = true;
            }
            if (mat.HasProperty("_BlinkLightColor") && !mat.HasProperty("_MulColor0") && !mat.HasProperty("_MulColor1"))
            {
                mat.SetColor("_BlinkLightColor", color1);
                hasAny = true;
            }
            if (mat.HasProperty("_ColorPower"))
            {
                mat.SetFloat("_ColorPower", power);
                hasAny = true;
            }
            if (setExtraMultiply && mat.HasProperty("_ColorPowerMultiply"))
            {
                mat.SetFloat("_ColorPowerMultiply", extraValue);
                hasAny = true;
            }

            if (!hasAny && mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color1);
            }
        }

        private static void ApplyNeonMainBgColor2(Material mat, Color color1, Color color2, float power)
        {
            bool wroteAny = false;

            if (mat.HasProperty("_MulColor0"))
            {
                mat.SetColor("_MulColor0", color2);
                wroteAny = true;
            }
            if (mat.HasProperty("_MulColor1"))
            {
                mat.SetColor("_MulColor1", color1);
                wroteAny = true;
            }
            if (mat.HasProperty("_BlinkLightColor") && !mat.HasProperty("_MulColor1"))
            {
                mat.SetColor("_BlinkLightColor", color1);
                wroteAny = true;
            }
            if (mat.HasProperty("_ColorPower"))
            {
                mat.SetFloat("_ColorPower", power);
                wroteAny = true;
            }

            if (!wroteAny && mat.HasProperty("_Color"))
                mat.SetColor("_Color", color1);
        }

        private static void ApplyNeonBackBgColor2(Material mat, Color color1, float power)
        {
            bool wroteAny = false;

            if (mat.HasProperty("_MulColor1"))
            {
                mat.SetColor("_MulColor1", color1);
                wroteAny = true;
            }
            else if (mat.HasProperty("_MulColor0"))
            {
                mat.SetColor("_MulColor0", color1);
                wroteAny = true;
            }

            if (mat.HasProperty("_BlinkLightColor"))
            {
                mat.SetColor("_BlinkLightColor", color1);
                wroteAny = true;
            }
            if (mat.HasProperty("_ColorPower"))
            {
                mat.SetFloat("_ColorPower", power);
                wroteAny = true;
            }

            if (!wroteAny && mat.HasProperty("_Color"))
                mat.SetColor("_Color", color1);
        }

        private static void ApplyLegacyBgColor2(Material mat, Color color1, Color color2, float power, float extra)
        {
            bool hasMul0 = mat.HasProperty("_MulColor0");
            bool hasMul1 = mat.HasProperty("_MulColor1");
            bool hasPower = mat.HasProperty("_ColorPower");
            bool hasMultiply = mat.HasProperty("_ColorPowerMultiply");
            bool hasBlinkColor = mat.HasProperty("_BlinkLightColor");

            if (!hasMul0 && !hasMul1 && !hasPower && !hasMultiply && !hasBlinkColor)
                return;

            if (hasMul0)
                mat.SetColor("_MulColor0", color1);
            if (hasMul1)
                mat.SetColor("_MulColor1", color2);
            if (hasBlinkColor && !hasMul0 && !hasMul1)
                mat.SetColor("_BlinkLightColor", color1);
            if (hasPower)
                mat.SetFloat("_ColorPower", power);
            if (hasMultiply)
                mat.SetFloat("_ColorPowerMultiply", extra);
        }

        private void RebuildBgColorCache()
        {
            _bgColorRendererCache.Clear();
            _bgColor2GroupCache.Clear();
            _bgColorMissingLogged.Clear();
            _bgColorAllEligibleRenderers.Clear();
            _allStageRenderers.Clear();
            _bgColor2Groups.Clear();
            _bindingsBySharedMaterialRef.Clear();
            _bindingsBySharedMaterialName.Clear();
            _bgColor1RendererProperties.Clear();
            _ambientColorRenderers.Clear();

            var all = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null) continue;
                _allStageRenderers.Add(r);
                IndexRendererSharedMaterials(r);
                int stageColorProperties = 0;
                bool hasAmbient = false;
                var materials = r.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    var material = materials[m];
                    if (material == null) continue;
                    if (material.HasProperty(BgColor1MulColor0Id)) stageColorProperties |= 1;
                    if (material.HasProperty(BgColor1ColorPowerId)) stageColorProperties |= 2;
                    if (material.HasProperty(BgColor1AmbientColorId)) hasAmbient = true;
                }
                if (stageColorProperties != 0) _bgColor1RendererProperties.Add(r, stageColorProperties);
                if (hasAmbient) _ambientColorRenderers.Add(r);

                if (RendererHasAnyBgColorProps(r))
                    _bgColorAllEligibleRenderers.Add(r);
            }

            BuildBgColor2RuntimeGroups();

            if (_bgColorVerboseLog)
            {
                Debug.Log($"[StageController] BgColor cache rebuilt. eligibleRenderers={_bgColorAllEligibleRenderers.Count}, materialGroups={_bgColor2Groups.Count}");
            }
        }

        private void IndexRendererSharedMaterials(Renderer r)
        {
            var mats = r.sharedMaterials;
            if (mats == null) return;

            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;

                var binding = new BgColor2RuntimeBinding
                {
                    renderer = r,
                    materialIndex = i
                };

                if (!_bindingsBySharedMaterialRef.TryGetValue(mat, out var byRef))
                {
                    byRef = new List<BgColor2RuntimeBinding>();
                    _bindingsBySharedMaterialRef[mat] = byRef;
                }
                byRef.Add(binding);

                string key = NormalizeKey(CleanMaterialName(mat.name));
                if (string.IsNullOrEmpty(key))
                    continue;

                if (!_bindingsBySharedMaterialName.TryGetValue(key, out var byName))
                {
                    byName = new List<BgColor2RuntimeBinding>();
                    _bindingsBySharedMaterialName[key] = byName;
                }
                byName.Add(binding);
            }
        }

        private void BuildBgColor2RuntimeGroups()
        {
            AddRuntimeGroupsFromMaterialArray(_washLightMaterials, BgColor2RuntimeKind.Wash);
            AddLaserRuntimeGroups();
            AddRuntimeGroupsFromMaterialArray(_footLightMaterials, BgColor2RuntimeKind.Foot);

            if (_neonMaterialInfos != null)
            {
                for (int i = 0; i < _neonMaterialInfos.Length; i++)
                {
                    var info = _neonMaterialInfos[i];
                    if (info == null) continue;
                    AddRuntimeGroup(info.MainMaterial, BgColor2RuntimeKind.NeonMain, i);
                    AddRuntimeGroup(info.BackMaterial, BgColor2RuntimeKind.NeonBack, i);
                }
            }
        }

        private void AddLaserRuntimeGroups()
        {
            if (_laserRenderersByMaterialIndex.Count == 0)
            {
                // 没有运行时映射时才使用旧的材质数组匹配。
                AddRuntimeGroupsFromMaterialArray(_laserMaterials, BgColor2RuntimeKind.Laser);
                return;
            }

            foreach (var pair in _laserRenderersByMaterialIndex.OrderBy(p => p.Key))
            {
                int sourceIndex = pair.Key;
                List<Renderer> sourceRenderers = pair.Value;
                if (sourceRenderers == null || sourceRenderers.Count == 0)
                    continue;

                Material sourceMaterial = FindFirstRendererMaterial(sourceRenderers);
                if (sourceMaterial == null &&
                    _laserMaterials != null &&
                    sourceIndex >= 0 &&
                    sourceIndex < _laserMaterials.Length)
                {
                    sourceMaterial = _laserMaterials[sourceIndex];
                }

                var group = new BgColor2RuntimeGroup
                {
                    kind = BgColor2RuntimeKind.Laser,
                    sourceIndex = sourceIndex,
                    sourceMaterial = sourceMaterial,
                    sourceName = sourceMaterial != null ? CleanMaterialName(sourceMaterial.name) : $"Laser{sourceIndex}",
                    sourceKey = sourceMaterial != null ? NormalizeKey(CleanMaterialName(sourceMaterial.name)) : $"laser{sourceIndex}"
                };

                var seenBindings = new HashSet<string>(StringComparer.Ordinal);
                var seenRenderers = new HashSet<int>();

                for (int r = 0; r < sourceRenderers.Count; r++)
                {
                    Renderer renderer = sourceRenderers[r];
                    if (renderer == null)
                        continue;

                    if (seenRenderers.Add(renderer.GetInstanceID()))
                        group.renderers.Add(renderer);

                    Material[] materials = renderer.sharedMaterials;
                    if (materials == null)
                        continue;

                    for (int m = 0; m < materials.Length; m++)
                    {
                        if (materials[m] == null)
                            continue;

                        string bindingKey = renderer.GetInstanceID().ToString() + ":" + m.ToString();
                        if (!seenBindings.Add(bindingKey))
                            continue;

                        group.bindings.Add(new BgColor2RuntimeBinding
                        {
                            renderer = renderer,
                            materialIndex = m
                        });
                    }
                }

                _bgColor2Groups.Add(group);

                if (_bgColorVerboseLog)
                {
                    Debug.Log($"[StageController] Laser BgColor2 group built. materialIndex={sourceIndex}, src='{group.sourceName}', bindings={group.bindings.Count}, renderers={group.renderers.Count}");
                }
            }
        }

        private static Material FindFirstRendererMaterial(List<Renderer> renderers)
        {
            if (renderers == null)
                return null;

            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                if (materials == null)
                    continue;

                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] != null)
                        return materials[m];
                }
            }

            return null;
        }

        private void AddRuntimeGroupsFromMaterialArray(Material[] sourceMaterials, BgColor2RuntimeKind kind)
        {
            if (sourceMaterials == null) return;
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                AddRuntimeGroup(sourceMaterials[i], kind, i);
            }
        }

        private void AddRuntimeGroup(Material sourceMaterial, BgColor2RuntimeKind kind, int sourceIndex)
        {
            if (sourceMaterial == null)
                return;

            var group = new BgColor2RuntimeGroup
            {
                kind = kind,
                sourceIndex = sourceIndex,
                sourceMaterial = sourceMaterial,
                sourceName = CleanMaterialName(sourceMaterial.name),
                sourceKey = NormalizeKey(CleanMaterialName(sourceMaterial.name))
            };

            var collected = CollectBindingsBySourceMaterial(sourceMaterial);
            if (collected != null && collected.Count > 0)
            {
                group.bindings.AddRange(collected);

                var uniqueRenderers = new HashSet<Renderer>();
                for (int i = 0; i < collected.Count; i++)
                {
                    var renderer = collected[i]?.renderer;
                    if (renderer != null && uniqueRenderers.Add(renderer))
                        group.renderers.Add(renderer);
                }
            }

            _bgColor2Groups.Add(group);

            if (_bgColorVerboseLog)
            {
                Debug.Log($"[StageController] BgColor2 runtime group built. kind={kind}, src='{group.sourceName}', bindings={group.bindings.Count}, renderers={group.renderers.Count}");
            }
        }

        private List<BgColor2RuntimeBinding> CollectBindingsBySourceMaterial(Material sourceMaterial)
        {
            var list = new List<BgColor2RuntimeBinding>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (sourceMaterial == null)
                return list;

            if (_bindingsBySharedMaterialRef.TryGetValue(sourceMaterial, out var byRef))
            {
                AddBindings(list, seen, byRef);
            }

            if (list.Count == 0)
            {
                string sourceKey = NormalizeKey(CleanMaterialName(sourceMaterial.name));
                if (!string.IsNullOrEmpty(sourceKey) && _bindingsBySharedMaterialName.TryGetValue(sourceKey, out var byName))
                {
                    AddBindings(list, seen, byName);
                }
            }

            return list;
        }

        private static void AddBindings(List<BgColor2RuntimeBinding> dst, HashSet<string> seen, List<BgColor2RuntimeBinding> src)
        {
            if (dst == null || seen == null || src == null)
                return;

            for (int i = 0; i < src.Count; i++)
            {
                var binding = src[i];
                if (binding == null || binding.renderer == null)
                    continue;

                string key = binding.renderer.GetInstanceID().ToString() + ":" + binding.materialIndex.ToString();
                if (!seen.Add(key))
                    continue;

                dst.Add(binding);
            }
        }

        private List<BgColor2RuntimeGroup> ResolveBgColor2Groups(string timelineName)
        {
            string key = NormalizeKey(timelineName);
            if (string.IsNullOrEmpty(key))
                return null;

            if (_bgColor2GroupCache.TryGetValue(key, out var cached))
                return cached;

            var resolved = TryResolveBgColor2GroupsByOverride(timelineName, key);
            if (resolved == null || resolved.Count == 0)
            {
                resolved = TryResolveBgColor2GroupsByTimelineIndex(key);
            }
            if (resolved == null || resolved.Count == 0)
            {
                var bestByKind = new Dictionary<BgColor2RuntimeKind, (int score, BgColor2RuntimeGroup group)>();
                for (int i = 0; i < _bgColor2Groups.Count; i++)
                {
                    var group = _bgColor2Groups[i];
                    if (group == null || group.sourceMaterial == null || group.bindings.Count == 0)
                        continue;

                    int score = ScoreTimelineAgainstSource(key, timelineName, group.sourceName, group.sourceKey);
                    if (score <= 0)
                        continue;

                    if (!bestByKind.TryGetValue(group.kind, out var best) || score > best.score)
                    {
                        bestByKind[group.kind] = (score, group);
                    }
                }

                resolved = bestByKind.Values
                                     .OrderBy(v => v.group.kind)
                                     .Select(v => v.group)
                                     .ToList();
            }

            if ((resolved == null || resolved.Count == 0) && _bgColorVerboseLog && _bgColorMissingLogged.Add($"bg2::{timelineName}"))
            {
                Debug.LogWarning($"[StageController] BgColor2 material group not found for timeline '{timelineName}'");
            }
            else if (_bgColorVerboseLog && resolved != null && resolved.Count > 0)
            {
                Debug.Log($"[StageController] BgColor2 '{timelineName}' resolved groups={string.Join(", ", resolved.Select(g => $"{g.kind}:{g.sourceName}:{g.renderers.Count}"))}");
            }

            _bgColor2GroupCache[key] = resolved;
            return resolved;
        }

        private List<BgColor2RuntimeGroup> TryResolveBgColor2GroupsByTimelineIndex(string normalizedTimelineKey)
        {
            if (!TryParseBgColor2TimelineIndex(normalizedTimelineKey, out var family, out var sourceIndex))
                return null;

            var list = new List<BgColor2RuntimeGroup>(2);
            for (int i = 0; i < _bgColor2Groups.Count; i++)
            {
                var group = _bgColor2Groups[i];
                if (group == null || group.sourceIndex != sourceIndex)
                    continue;

                switch (family)
                {
                    case "wash":
                        if (group.kind == BgColor2RuntimeKind.Wash)
                            list.Add(group);
                        break;
                    case "foot":
                        if (group.kind == BgColor2RuntimeKind.Foot)
                            list.Add(group);
                        break;
                    case "laser":
                        if (group.kind == BgColor2RuntimeKind.Laser)
                            list.Add(group);
                        break;
                    case "neon":
                        if (group.kind == BgColor2RuntimeKind.NeonMain || group.kind == BgColor2RuntimeKind.NeonBack)
                            list.Add(group);
                        break;
                }
            }

            return list.Count > 0 ? list : null;
        }

        private static bool TryParseBgColor2TimelineIndex(string normalizedTimelineKey, out string family, out int sourceIndex)
        {
            family = null;
            sourceIndex = -1;

            if (TryParseTimelineLetterIndex(normalizedTimelineKey, "bgwash", out sourceIndex))
            {
                family = "wash";
                return true;
            }
            if (TryParseTimelineLetterIndex(normalizedTimelineKey, "bgfoot", out sourceIndex))
            {
                family = "foot";
                return true;
            }
            if (TryParseTimelineLetterIndex(normalizedTimelineKey, "bgneon", out sourceIndex))
            {
                family = "neon";
                return true;
            }
            if (TryParseTimelineLetterIndex(normalizedTimelineKey, "laser", out sourceIndex) ||
                TryParseTimelineLetterIndex(normalizedTimelineKey, "bglaser", out sourceIndex))
            {
                family = "laser";
                return true;
            }

            return false;
        }

        private static bool TryParseTimelineLetterIndex(string normalizedTimelineKey, string prefix, out int sourceIndex)
        {
            sourceIndex = -1;
            if (string.IsNullOrEmpty(normalizedTimelineKey) || string.IsNullOrEmpty(prefix))
                return false;
            if (!normalizedTimelineKey.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            if (normalizedTimelineKey.Length != prefix.Length + 1)
                return false;

            char suffix = normalizedTimelineKey[normalizedTimelineKey.Length - 1];
            if (suffix < 'a' || suffix > 'z')
                return false;

            sourceIndex = suffix - 'a';
            return true;
        }

        private List<BgColor2RuntimeGroup> TryResolveBgColor2GroupsByOverride(string timelineName, string key)
        {
            if (_bgColorBindingOverrides == null || _bgColorBindingOverrides.Length == 0)
                return null;

            for (int i = 0; i < _bgColorBindingOverrides.Length; i++)
            {
                var rule = _bgColorBindingOverrides[i];
                if (rule == null) continue;
                if (NormalizeKey(rule.timelineName) != key) continue;

                var list = new List<BgColor2RuntimeGroup>();
                for (int g = 0; g < _bgColor2Groups.Count; g++)
                {
                    var group = _bgColor2Groups[g];
                    if (group == null || group.sourceMaterial == null) continue;

                    bool materialMatch = rule.materialNameContains == null || rule.materialNameContains.Length == 0;
                    if (!materialMatch)
                    {
                        for (int m = 0; m < rule.materialNameContains.Length; m++)
                        {
                            var token = rule.materialNameContains[m];
                            if (!string.IsNullOrEmpty(token) && group.sourceName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                materialMatch = true;
                                break;
                            }
                        }
                    }

                    bool rendererMatch = rule.rendererNameContains == null || rule.rendererNameContains.Length == 0;
                    if (!rendererMatch)
                    {
                        for (int r = 0; r < group.renderers.Count && !rendererMatch; r++)
                        {
                            var rr = group.renderers[r];
                            if (rr == null) continue;
                            var rn = rr.name ?? string.Empty;
                            for (int t = 0; t < rule.rendererNameContains.Length; t++)
                            {
                                var token = rule.rendererNameContains[t];
                                if (!string.IsNullOrEmpty(token) && rn.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    rendererMatch = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (materialMatch && rendererMatch)
                        list.Add(group);
                }

                if (list.Count > 0)
                    return list;
            }

            return null;
        }

        private static int ScoreTimelineAgainstSource(string normalizedTimelineKey, string timelineName, string sourceName, string sourceKey)
        {
            if (string.IsNullOrEmpty(normalizedTimelineKey) || string.IsNullOrEmpty(sourceKey))
                return 0;

            if (sourceKey == normalizedTimelineKey)
                return 1000;
            if (sourceKey.Contains(normalizedTimelineKey) || normalizedTimelineKey.Contains(sourceKey))
                return 700;

            int score = 0;
            var tokens = SplitNameTokens(timelineName);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = NormalizeKey(tokens[i]);
                if (string.IsNullOrEmpty(token)) continue;
                if (sourceKey.Contains(token))
                    score += 50;
            }

            if (!string.IsNullOrEmpty(sourceName))
            {
                string sourceLower = sourceName.ToLowerInvariant();
                if (sourceLower.Contains("wash") && normalizedTimelineKey.Contains("wash")) score += 80;
                if (sourceLower.Contains("laser") && normalizedTimelineKey.Contains("laser")) score += 80;
                if (sourceLower.Contains("foot") && normalizedTimelineKey.Contains("foot")) score += 80;
                if (sourceLower.Contains("neon") && normalizedTimelineKey.Contains("neon")) score += 80;
                if (sourceLower.Contains("led") && normalizedTimelineKey.Contains("led")) score += 30;
            }

            return score;
        }

        private List<Renderer> ResolveBgColorRenderers(string timelineName, bool wantBgColor2Style, bool allowAllEligibleFallback = true)
        {
            string key = NormalizeKey(timelineName);
            if (string.IsNullOrEmpty(key))
                return (_bgColorFallbackToAllEligible && allowAllEligibleFallback) ? _bgColorAllEligibleRenderers : null;

            if (_bgColorRendererCache.TryGetValue(key, out var cached))
                return cached;

            var set = new HashSet<Renderer>();

            if (TryAddRenderersFromStageObjectMap(timelineName, key, set) | TryAddRenderersFromUnitMap(timelineName, key, set))
            {
            }

            ApplyBindingOverrides(timelineName, key, set);

            var all = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null) continue;
                if (wantBgColor2Style && !RendererHasBgColor2Props(r)) continue;
                if (!wantBgColor2Style && !RendererHasBgColor1Props(r)) continue;
                if (RendererMatchesTimeline(r, timelineName, key))
                    set.Add(r);
            }

            var resolved = set.ToList();
            if (resolved.Count == 0 && _bgColorFallbackToAllEligible && allowAllEligibleFallback)
            {
                resolved = _bgColorAllEligibleRenderers.Where(r => r != null && (wantBgColor2Style ? RendererHasBgColor2Props(r) : RendererHasBgColor1Props(r))).ToList();
            }

            if (resolved.Count == 0)
            {
                if (_bgColorVerboseLog && !string.IsNullOrEmpty(timelineName) && _bgColorMissingLogged.Add(timelineName))
                    Debug.LogWarning($"[StageController] BgColor target not found for timeline '{timelineName}'");
            }
            else if (_bgColorVerboseLog)
            {
                Debug.Log($"[StageController] BgColor '{timelineName}' resolved renderers={resolved.Count}");
            }

            _bgColorRendererCache[key] = resolved;
            return resolved;
        }

        private bool TryAddRenderersFromStageObjectMap(string timelineName, string key, HashSet<Renderer> set)
        {
            bool added = false;
            foreach (var kv in StageObjectMap)
            {
                if (kv.Value == null) continue;
                string candidate = kv.Key ?? string.Empty;
                string candidateKey = NormalizeKey(candidate);
                if (candidateKey != key && !candidateKey.Contains(key) && !key.Contains(candidateKey))
                    continue;

                AddRenderersRecursive(kv.Value, set);
                added = true;
            }
            return added;
        }

        private bool TryAddRenderersFromUnitMap(string timelineName, string key, HashSet<Renderer> set)
        {
            bool added = false;
            foreach (var kv in StageObjectUnitMap)
            {
                if (kv.Value == null) continue;
                string candidateKey = NormalizeKey(kv.Key ?? string.Empty);
                if (candidateKey != key && !candidateKey.Contains(key) && !key.Contains(candidateKey))
                    continue;

                var children = kv.Value.ChildObjectNameHashArray;
                if (children == null) continue;
                for (int i = 0; i < children.Length; i++)
                {
                    if (!_objectsWorkInfoDictionary.TryGetValue(children[i], out var works)) continue;
                    for (int j = 0; j < works.Count; j++)
                    {
                        if (works[j].transform == null) continue;
                        AddRenderersRecursive(works[j].transform.gameObject, set);
                        added = true;
                    }
                }
            }
            return added;
        }

        private void ApplyBindingOverrides(string timelineName, string key, HashSet<Renderer> set)
        {
            if (_bgColorBindingOverrides == null || _bgColorBindingOverrides.Length == 0)
                return;

            var all = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _bgColorBindingOverrides.Length; i++)
            {
                var rule = _bgColorBindingOverrides[i];
                if (rule == null) continue;
                if (NormalizeKey(rule.timelineName) != key) continue;

                for (int j = 0; j < all.Length; j++)
                {
                    var r = all[j];
                    if (r == null) continue;
                    if (MatchRule(r, rule))
                        set.Add(r);
                }
            }
        }

        private static bool MatchRule(Renderer r, BgColorBindingOverride rule)
        {
            if (r == null || rule == null)
                return false;

            bool rendererMatched = rule.rendererNameContains == null || rule.rendererNameContains.Length == 0;
            if (!rendererMatched)
            {
                string rn = r.name ?? string.Empty;
                for (int i = 0; i < rule.rendererNameContains.Length; i++)
                {
                    string token = rule.rendererNameContains[i];
                    if (!string.IsNullOrEmpty(token) && rn.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        rendererMatched = true;
                        break;
                    }
                }
            }

            bool materialMatched = rule.materialNameContains == null || rule.materialNameContains.Length == 0;
            if (!materialMatched)
            {
                var mats = r.sharedMaterials;
                if (mats != null)
                {
                    for (int i = 0; i < mats.Length && !materialMatched; i++)
                    {
                        var m = mats[i];
                        if (m == null) continue;
                        string mn = m.name ?? string.Empty;
                        for (int j = 0; j < rule.materialNameContains.Length; j++)
                        {
                            string token = rule.materialNameContains[j];
                            if (!string.IsNullOrEmpty(token) && mn.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                materialMatched = true;
                                break;
                            }
                        }
                    }
                }
            }

            return rendererMatched && materialMatched;
        }

        private bool RendererMatchesTimeline(Renderer r, string timelineName, string normalizedKey)
        {
            if (r == null) return false;

            if (StringMatchesTimeline(r.name, normalizedKey)) return true;
            if (r.transform != null && StringMatchesTimeline(r.transform.root.name, normalizedKey)) return true;
            if (r.transform != null && r.transform.parent != null && StringMatchesTimeline(r.transform.parent.name, normalizedKey)) return true;

            var mats = r.sharedMaterials;
            if (mats != null)
            {
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m != null && StringMatchesTimeline(m.name, normalizedKey))
                        return true;
                }
            }

            if (!string.IsNullOrEmpty(timelineName))
            {
                var tokens = SplitNameTokens(timelineName);
                for (int i = 0; i < tokens.Length; i++)
                {
                    string token = tokens[i];
                    if (string.IsNullOrEmpty(token)) continue;
                    if (ContainsIgnoreCase(r.name, token)) return true;
                    if (r.transform != null && ContainsIgnoreCase(r.transform.root.name, token)) return true;
                    if (mats != null)
                    {
                        for (int j = 0; j < mats.Length; j++)
                        {
                            var m = mats[j];
                            if (m != null && ContainsIgnoreCase(m.name, token))
                                return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool StringMatchesTimeline(string candidate, string normalizedKey)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(normalizedKey))
                return false;

            string nk = NormalizeKey(candidate);
            return nk == normalizedKey || nk.Contains(normalizedKey) || normalizedKey.Contains(nk);
        }

        private static string[] SplitNameTokens(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
            return s.Replace("-", " ").Replace("_", " ")
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t.Length >= 3)
                    .ToArray();
        }

        private static bool ContainsIgnoreCase(string s, string token)
        {
            return !string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(token) && s.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            char[] tmp = new char[s.Length];
            int n = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c))
                    tmp[n++] = char.ToLowerInvariant(c);
            }
            return n > 0 ? new string(tmp, 0, n) : string.Empty;
        }

        private static string CleanMaterialName(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            string cleaned = s.Replace("(Instance)", string.Empty)
                              .Replace("(Clone)", string.Empty)
                              .Trim();
            return cleaned;
        }

        private void AddRenderersRecursive(GameObject go, HashSet<Renderer> set)
        {
            if (go == null) return;
            var rs = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i];
                if (r != null)
                    set.Add(r);
            }
        }

        private static bool RendererHasAnyBgColorProps(Renderer r)
        {
            return RendererHasBgColor1Props(r) || RendererHasBgColor2Props(r);
        }

        private static bool RendererHasBgColor1Props(Renderer r)
        {
            var mats = r != null ? r.sharedMaterials : null;
            if (mats == null) return false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (m.HasProperty("_CharaColor") || m.HasProperty("_ToonDarkColor") || m.HasProperty("_ToonBrightColor") || m.HasProperty("_OutlineColor") || m.HasProperty("_Saturation"))
                    return true;
            }
            return false;
        }

        private static bool RendererHasBgColor2Props(Renderer r)
        {
            var mats = r != null ? r.sharedMaterials : null;
            if (mats == null) return false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (m.HasProperty("_MulColor0") || m.HasProperty("_MulColor1") || m.HasProperty("_ColorPower") || m.HasProperty("_ColorPowerMultiply") || m.HasProperty("_BlinkLightColor"))
                    return true;
            }
            return false;
        }

        private float ResolveBgColorExtraValue(int index)
        {
            if (_bgColorExtraValueTable != null && index >= 0 && index < _bgColorExtraValueTable.Length)
                return _bgColorExtraValueTable[index];
            return 1f;
        }

        public static int CountMissingScripts(GameObject root)
        {
            if (root == null) return 0;
            int count = 0;

            var components = root.GetComponents<Component>();
            foreach (var c in components)
            {
                if (c == null) count++;
            }

            foreach (Transform child in root.transform)
            {
                count += CountMissingScripts(child.gameObject);
            }
            return count;
        }
    }
}
