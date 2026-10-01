using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityObject = UnityEngine.Object;

namespace Gallop.Live
{
    public interface ILiveFlashController
    {
        bool IsInitialized { get; }
        void SetParent(Transform parent, bool worldPositionStays);
        void Destroy();
        void AlterUpdate();
        void PlayLabel(string label, float currentTime, object curData = null);
        void UpdateOrientationLandscape();
        void UpdateOrientationPortrait();
        void Reset();
    }

    /// <summary>
    /// Official-like LiveFlashController bridge for UmaViewer-style projects.
    ///
    /// It does not require compile-time references to AnimateToUnity/FlashPlayer.
    /// If the real Flash runtime exists on the instantiated prefab, calls are forwarded by reflection.
    /// If it does not exist, it falls back to direct Unity hierarchy edits by object name.
    /// </summary>
    public class LiveFlashController : MonoBehaviour, ILiveFlashController
    {
        protected const string LABEL_IN00 = "in00";
        protected const string LABEL_IN = "in";
        protected const string LABEL_OUT = "out";
        protected const string LABEL_END = "end";

        private const int FLASH_POSITION_OFFSET_Z = 100;
        private const int FLASH_CAMERA_CLIP_NEAR = -20;
        private const int FLASH_CAMERA_CLIP_FAR = 70;
        private const string PARTICLE_ROOT_NAME = "root";
        private const float PARTICLE_ASPECT_RATIO_SCALE_MAX = 1f;
        private const float OVERRIDE_GRADATION_ALPHA_VALUE = 1f;

        [SerializeField] private Canvas _flashCanvas;
        [SerializeField] private Camera _flashCamera;
        [SerializeField] private Transform _cachedTransform;
        [SerializeField] private GameObject _flashRoot;

        // Official fields are FlashActionPlayer / FlashPlayer. Keep them as object so the project compiles
        // without AnimateToUnity assemblies.
        private object _flashActionPlayer;
        private object _flashPlayer;

        [SerializeField] private bool _isVertical;
        private object _currentTimelineKeyFlashPlayerData;
        [SerializeField] private bool _isFlashInitialized;
        [SerializeField] private bool _isInitialized;

        protected object FlashActionPlayer { get { return _flashActionPlayer; } }
        protected object FlashPlayer { get { return _flashPlayer; } }
        protected GameObject FlashRoot { get { return _flashRoot; } }
        protected Font CurrentFlashFont { get; private set; }
        public bool IsInitialized { get { return _isInitialized; } }

        private void OnDestroy()
        {
            Destroy();
        }

        public virtual void Initialize(string flashResourcePath)
        {
            if (_isInitialized)
                Destroy();

            _cachedTransform = transform;

            CreateFlashCamera(_cachedTransform);
            CreateFlashCanvas(_cachedTransform, _flashCamera);
            InitializeFlash(flashResourcePath, _flashCanvas != null ? _flashCanvas.transform : _cachedTransform);
            SetupFlashCameraCallback();

            _isFlashInitialized = _flashRoot != null;
            _isInitialized = _isFlashInitialized;
        }

        private void CreateFlashCamera(Transform parent)
        {
            if (_flashCamera != null)
                return;

            GameObject cameraObject = new GameObject("FlashCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -FLASH_POSITION_OFFSET_Z);
            cameraObject.transform.localRotation = Quaternion.identity;
            cameraObject.transform.localScale = Vector3.one;

            _flashCamera = cameraObject.AddComponent<Camera>();
            _flashCamera.clearFlags = CameraClearFlags.Depth;
            _flashCamera.orthographic = true;
            _flashCamera.orthographicSize = 1f;
            _flashCamera.nearClipPlane = FLASH_CAMERA_CLIP_NEAR;
            _flashCamera.farClipPlane = FLASH_CAMERA_CLIP_FAR;
            _flashCamera.allowHDR = false;
            _flashCamera.allowMSAA = false;
            _flashCamera.depth = 100f;
        }

        private void SetupFlashCameraCallback()
        {
            // Official registers camera callbacks for Flash rendering.
            // In this bridge we keep the camera alive and let Unity/Flash runtime render normally.
        }

        private void CreateFlashCanvas(Transform parent, Camera flashCamera)
        {
            if (_flashCanvas != null)
                return;

            GameObject canvasObject = new GameObject("FlashCanvas");
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one;

            _flashCanvas = canvasObject.AddComponent<Canvas>();
            _flashCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            _flashCanvas.worldCamera = flashCamera;
            _flashCanvas.planeDistance = FLASH_POSITION_OFFSET_Z;

            CanvasScalerSafeAdd(canvasObject);
        }

        private void InitializeFlash(string flashResourcePath, Transform parent)
        {
            GameObject prefab = LiveFlashResourceUtility.LoadOnView<GameObject>(flashResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[LiveFlashController] Flash prefab not found: " + flashResourcePath);
                return;
            }

            _flashRoot = Instantiate(prefab, parent, false);
            _flashRoot.name = prefab.name;
            _flashRoot.transform.localPosition = Vector3.zero;
            _flashRoot.transform.localRotation = Quaternion.identity;
            _flashRoot.transform.localScale = Vector3.one;

            CacheFlashRuntimeComponents(_flashRoot);
        }

        protected bool TryGetRootGameObject(string name, out GameObject rootGameObject)
        {
            rootGameObject = null;
            if (string.IsNullOrEmpty(name) || _flashRoot == null)
                return false;

            Transform t = FindDeepChild(_flashRoot.transform, name);
            if (t == null)
                return false;

            rootGameObject = t.gameObject;
            return true;
        }

        protected void SetTextGradationColor(string targetObjectName, Color startColor, Color endColor, string rootObjectName = "")
        {
            GameObject target = FindTargetUnderRoot(rootObjectName, targetObjectName);
            if (target == null)
                return;

            Color color = new Color(startColor.r, startColor.g, startColor.b, OVERRIDE_GRADATION_ALPHA_VALUE);
            ApplyTextColor(target, color);
        }

        public void SetParent(Transform parent, bool worldPositionStays)
        {
            transform.SetParent(parent, worldPositionStays);
        }

        public void Destroy()
        {
            if (!_isInitialized && _flashRoot == null && _flashCanvas == null && _flashCamera == null)
                return;

            DestroySub();

            if (_flashRoot != null)
            {
                UnityObject.Destroy(_flashRoot);
                _flashRoot = null;
            }

            if (_flashCanvas != null)
            {
                UnityObject.Destroy(_flashCanvas.gameObject);
                _flashCanvas = null;
            }

            if (_flashCamera != null)
            {
                UnityObject.Destroy(_flashCamera.gameObject);
                _flashCamera = null;
            }

            _flashActionPlayer = null;
            _flashPlayer = null;
            _currentTimelineKeyFlashPlayerData = null;
            _isFlashInitialized = false;
            _isInitialized = false;
        }

        protected virtual void DestroySub()
        {
        }

        public void AlterUpdate()
        {
            if (!_isInitialized)
                return;

            TryInvokeFirst(_flashActionPlayer,
                new[] { "AlterUpdate", "ManualUpdate", "UpdateFlash", "Update" });

            TryInvokeFirst(_flashPlayer,
                new[] { "AlterUpdate", "ManualUpdate", "UpdateFlash" });

            UpdateParticle();
        }

        private void UpdateParticle()
        {
            if (_flashRoot == null)
                return;

            Transform particleRoot = FindDeepChild(_flashRoot.transform, PARTICLE_ROOT_NAME);
            if (particleRoot == null)
                return;

            ParticleSystem[] particles = particleRoot.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem ps = particles[i];
                if (ps != null && ps.gameObject.activeInHierarchy)
                    ps.Simulate(Time.deltaTime, true, false, false);
            }
        }

        public void PlayLabel(string label, float currentTime, object curData = null)
        {
            _currentTimelineKeyFlashPlayerData = curData;
            if (string.IsNullOrEmpty(label))
                return;

            if (TryInvokeAny(_flashActionPlayer,
                    new[] { "PlayLabel", "Play", "GotoAndPlay", "GotoLabel" },
                    new object[] { label, currentTime, curData }) ||
                TryInvokeAny(_flashPlayer,
                    new[] { "PlayLabel", "Play", "GotoAndPlay", "GotoLabel" },
                    new object[] { label, currentTime, curData }))
                return;

            // Direct fallback: enable a child with the label name if the converted Flash prefab exposes labels as GameObjects.
            Transform labelRoot = FindDeepChild(_flashRoot != null ? _flashRoot.transform : transform, label);
            if (labelRoot != null)
                labelRoot.gameObject.SetActive(true);
        }

        public void UpdateOrientationLandscape()
        {
            _isVertical = false;
            UpdateCanvasScale();
        }

        public void UpdateOrientationPortrait()
        {
            _isVertical = true;
            UpdateCanvasScale();
        }

        public void Reset()
        {
            TryInvokeFirst(_flashActionPlayer, new[] { "Reset", "Stop" });
            TryInvokeFirst(_flashPlayer, new[] { "Reset", "Stop" });
        }

        protected T LoadOnView<T>(string path) where T : UnityObject
        {
            return LiveFlashResourceUtility.LoadOnView<T>(path);
        }

        protected void SetCurrentFlashFont(Font font)
        {
            CurrentFlashFont = font;
        }

        protected bool ReplaceFlashFont(string fontName, Font font)
        {
            SetCurrentFlashFont(font);

            bool invoked = LiveFlashReflectionUtility.TryReplaceFont(fontName, font);
            if (!invoked && _flashRoot != null)
                ApplyFont(_flashRoot, font);
            return invoked;
        }

        protected string GetFlashRootObjectName(string rootName)
        {
            if (string.IsNullOrEmpty(rootName))
                return null;

            object obj = null;
            if (_flashPlayer != null)
            {
                obj = LiveFlashReflectionUtility.TryInvokeBest(
                    _flashPlayer,
                    "GetObj",
                    new object[] { rootName, false, false, false });
            }

            string objName = LiveFlashReflectionUtility.GetName(obj);
            if (!string.IsNullOrEmpty(objName))
                return objName;

            GameObject root;
            if (TryGetRootGameObject(rootName, out root) && root != null)
                return root.name;

            return null;
        }

        protected bool SetFlashText(string text, string targetObjectName, bool flag, string rootObjectName)
        {
            bool ok = false;
            if (_flashPlayer != null)
            {
                ok = LiveFlashReflectionUtility.TryInvokeAny(
                    _flashPlayer,
                    new[] { "SetText" },
                    new object[] { text, targetObjectName, flag, rootObjectName });
            }

            GameObject target = FindTargetUnderRoot(rootObjectName, targetObjectName);
            if (target != null)
            {
                ApplyText(target, text, CurrentFlashFont);
                ok = true;
            }

            return ok;
        }

        protected bool SetFlashTexture(string targetObjectName, Texture texture, int index, bool flag0, bool flag1, string rootObjectName)
        {
            bool ok = false;
            if (_flashPlayer != null)
            {
                ok = LiveFlashReflectionUtility.TryInvokeAny(
                    _flashPlayer,
                    new[] { "SetTexture" },
                    new object[] { targetObjectName, texture, index, flag0, flag1, rootObjectName, 0 });
            }

            GameObject target = FindTargetUnderRoot(rootObjectName, targetObjectName);
            if (target != null)
            {
                ApplyTexture(target, texture);
                ok = true;
            }

            return ok;
        }

        protected GameObject FindTargetUnderRoot(string rootObjectName, string targetObjectName)
        {
            if (_flashRoot == null)
                return null;

            Transform searchRoot = _flashRoot.transform;
            if (!string.IsNullOrEmpty(rootObjectName))
            {
                Transform root = FindDeepChild(_flashRoot.transform, rootObjectName);
                if (root != null)
                    searchRoot = root;
            }

            if (string.IsNullOrEmpty(targetObjectName))
                return searchRoot.gameObject;

            Transform target = FindDeepChild(searchRoot, targetObjectName);
            return target != null ? target.gameObject : null;
        }

        protected static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform result = FindDeepChild(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void CacheFlashRuntimeComponents(GameObject root)
        {
            _flashActionPlayer = null;
            _flashPlayer = null;

            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null)
                    continue;

                Type t = mb.GetType();
                string name = t.Name;
                string fullName = t.FullName ?? name;

                if (_flashActionPlayer == null &&
                    (string.Equals(name, "FlashActionPlayer", StringComparison.OrdinalIgnoreCase) ||
                     fullName.EndsWith(".FlashActionPlayer", StringComparison.OrdinalIgnoreCase)))
                {
                    _flashActionPlayer = mb;
                }

                if (_flashPlayer == null &&
                    (string.Equals(name, "FlashPlayer", StringComparison.OrdinalIgnoreCase) ||
                     fullName.EndsWith(".FlashPlayer", StringComparison.OrdinalIgnoreCase)))
                {
                    _flashPlayer = mb;
                }
            }

            if (_flashPlayer == null)
            {
                _flashPlayer = LiveFlashReflectionUtility.TryGetMemberValue(_flashActionPlayer, "FlashPlayer") ??
                               LiveFlashReflectionUtility.TryGetMemberValue(_flashActionPlayer, "flashPlayer") ??
                               LiveFlashReflectionUtility.TryGetMemberValue(_flashActionPlayer, "_flashPlayer");
            }
        }

        private static void CanvasScalerSafeAdd(GameObject canvasObject)
        {
            Type scalerType = FindType("UnityEngine.UI.CanvasScaler");
            if (scalerType == null)
                return;

            if (canvasObject.GetComponent(scalerType) == null)
                canvasObject.AddComponent(scalerType);
        }

        private static Type FindType(string fullName)
        {
            Type t = Type.GetType(fullName);
            if (t != null)
                return t;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    t = assemblies[i].GetType(fullName, false);
                    if (t != null)
                        return t;
                }
                catch { }
            }
            return null;
        }

        private void UpdateCanvasScale()
        {
            if (_flashCanvas == null)
                return;

            Transform t = _flashCanvas.transform;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }

        private static bool TryInvokeFirst(object target, string[] methodNames)
        {
            if (target == null || methodNames == null)
                return false;

            for (int i = 0; i < methodNames.Length; i++)
            {
                if (LiveFlashReflectionUtility.TryInvokeAny(target, new[] { methodNames[i] }, Array.Empty<object>()))
                    return true;
            }
            return false;
        }

        private static bool TryInvokeAny(object target, string[] methodNames, object[] args)
        {
            return LiveFlashReflectionUtility.TryInvokeAny(target, methodNames, args);
        }

        private static void ApplyFont(GameObject root, Font font)
        {
            if (root == null || font == null)
                return;

            TextMesh[] textMeshes = root.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < textMeshes.Length; i++)
                textMeshes[i].font = font;

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                    continue;

                Type t = c.GetType();
                if (t.FullName == "UnityEngine.UI.Text" || t.FullName == "TMPro.TextMeshPro" || t.FullName == "TMPro.TextMeshProUGUI")
                    LiveFlashReflectionUtility.TrySetMemberValue(c, "font", font);
            }
        }

        private static void ApplyText(GameObject target, string text, Font font)
        {
            if (target == null)
                return;

            TextMesh[] textMeshes = target.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                textMeshes[i].text = text ?? string.Empty;
                if (font != null)
                    textMeshes[i].font = font;
            }

            Component[] components = target.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                    continue;

                Type t = c.GetType();
                if (t.FullName == "UnityEngine.UI.Text" || t.FullName == "TMPro.TextMeshPro" || t.FullName == "TMPro.TextMeshProUGUI")
                {
                    LiveFlashReflectionUtility.TrySetMemberValue(c, "text", text ?? string.Empty);
                    if (font != null)
                        LiveFlashReflectionUtility.TrySetMemberValue(c, "font", font);
                }
            }
        }

        private static void ApplyTextColor(GameObject target, Color color)
        {
            TextMesh[] textMeshes = target.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < textMeshes.Length; i++)
                textMeshes[i].color = color;

            Component[] components = target.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                    continue;

                Type t = c.GetType();
                if (t.FullName == "UnityEngine.UI.Text" || t.FullName == "TMPro.TextMeshPro" || t.FullName == "TMPro.TextMeshProUGUI")
                    LiveFlashReflectionUtility.TrySetMemberValue(c, "color", color);
            }
        }

        private static void ApplyTexture(GameObject target, Texture texture)
        {
            if (target == null || texture == null)
                return;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null)
                    continue;

                Material[] mats = r.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null)
                        continue;

                    TrySetMaterialTexture(mat, texture, "_MainTex");
                    TrySetMaterialTexture(mat, texture, "_BaseMap");
                    TrySetMaterialTexture(mat, texture, "_Texture");
                    TrySetMaterialTexture(mat, texture, "_Tex");
                    try { mat.mainTexture = texture; } catch { }
                }
            }

            Component[] components = target.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null)
                    continue;

                Type t = c.GetType();
                if (t.FullName == "UnityEngine.UI.RawImage")
                    LiveFlashReflectionUtility.TrySetMemberValue(c, "texture", texture);
            }
        }

        private static void TrySetMaterialTexture(Material mat, Texture texture, string propertyName)
        {
            try
            {
                if (mat.HasProperty(propertyName))
                    mat.SetTexture(propertyName, texture);
            }
            catch { }
        }
    }

    internal static class LiveFlashResourceUtility
    {
        public static T LoadOnView<T>(string path) where T : UnityObject
        {
            if (string.IsNullOrEmpty(path))
                return null;

            T res = Resources.Load<T>(path);
            if (res != null)
                return res;

            UmaDatabaseEntry entry = FindEntry(path);
            if (entry == null)
                return null;

            AssetBundle bundle = null;
            try
            {
                bundle = UmaAssetManager.LoadAssetBundle(entry, neverUnload: true, isRecursive: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LiveFlashResourceUtility] LoadAssetBundle failed: " + path + "\n" + ex.Message);
            }

            if (bundle == null)
                return null;

            return LoadAssetFromBundle<T>(bundle, path);
        }

        private static UmaDatabaseEntry FindEntry(string path)
        {
            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return null;

            UmaDatabaseEntry entry;
            if (main.AbList.TryGetValue(path, out entry) && entry != null)
                return entry;

            string normalizedPath = Normalize(path);
            string fileName = Normalize(Path.GetFileName(path));

            foreach (KeyValuePair<string, UmaDatabaseEntry> kv in main.AbList)
            {
                if (kv.Value == null)
                    continue;

                string key = Normalize(kv.Key);
                string name = Normalize(kv.Value.Name);
                string nameFile = Normalize(Path.GetFileName(kv.Value.Name ?? string.Empty));

                if (key == normalizedPath || name == normalizedPath)
                    return kv.Value;

                if (key.EndsWith("/" + normalizedPath, StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("/" + normalizedPath, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;

                if (!string.IsNullOrEmpty(fileName) &&
                    (nameFile == fileName || key.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase) || name.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase)))
                    return kv.Value;
            }

            return null;
        }

        private static T LoadAssetFromBundle<T>(AssetBundle bundle, string requestedPath) where T : UnityObject
        {
            string requested = Normalize(requestedPath);
            string requestedFile = Normalize(Path.GetFileNameWithoutExtension(requestedPath));

            string[] names = null;
            try { names = bundle.GetAllAssetNames(); } catch { }

            if (names != null)
            {
                string best = null;
                for (int i = 0; i < names.Length; i++)
                {
                    string n = Normalize(names[i]);
                    string nf = Normalize(Path.GetFileNameWithoutExtension(n));

                    if (n == requested || n.EndsWith("/" + requested, StringComparison.OrdinalIgnoreCase) || nf == requestedFile)
                    {
                        best = names[i];
                        break;
                    }
                }

                if (string.IsNullOrEmpty(best))
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        string n = Normalize(names[i]);
                        string nf = Normalize(Path.GetFileNameWithoutExtension(n));
                        if ((!string.IsNullOrEmpty(requestedFile) && nf.Contains(requestedFile)) || n.Contains(requested))
                        {
                            best = names[i];
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(best))
                {
                    try
                    {
                        T asset = bundle.LoadAsset<T>(best);
                        if (LiveRuntimeDiagnostics.Enabled)
                            LiveRuntimeDiagnostics.RecordAssetLoad(bundle.name, "LoadAsset", typeof(T),
                                new UnityObject[] { asset }, asset, best);
                        if (asset != null)
                            return asset;
                    }
                    catch { }
                }
            }

            try
            {
                T[] all = bundle.LoadAllAssets<T>();
                if (LiveRuntimeDiagnostics.Enabled)
                    LiveRuntimeDiagnostics.RecordAssetLoad(bundle.name, "LoadAllAssets", typeof(T), all);
                if (all != null && all.Length > 0)
                    return all[0];
            }
            catch { }

            return null;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Replace('\\', '/').Trim().TrimStart('/').ToLowerInvariant();
        }
    }

    internal static class LiveFlashReflectionUtility
    {
        public static bool TryReplaceFont(string fontName, Font font)
        {
            object manager = GetSingleton("AnimateToUnity.AnRootManager", "AnRootManager");
            if (manager == null)
                return false;

            return TryInvokeAny(manager, new[] { "ReplaceFont" }, new object[] { fontName, font });
        }

        public static object GetSingleton(params string[] typeNames)
        {
            Type type = null;
            for (int i = 0; i < typeNames.Length; i++)
            {
                type = FindType(typeNames[i]);
                if (type != null)
                    break;
            }
            if (type == null)
                return null;

            object has = TryGetStaticMemberValue(type, "HasInstance");
            if (has is bool && !(bool)has)
                return null;

            object instance = TryGetStaticMemberValue(type, "Instance") ?? TryGetStaticMemberValue(type, "instance");
            return instance;
        }

        public static Type FindType(string fullOrShortName)
        {
            Type t = Type.GetType(fullOrShortName);
            if (t != null)
                return t;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    t = assemblies[i].GetType(fullOrShortName, false);
                    if (t != null)
                        return t;

                    Type[] types = assemblies[i].GetTypes();
                    for (int j = 0; j < types.Length; j++)
                    {
                        if (string.Equals(types[j].Name, fullOrShortName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(types[j].FullName, fullOrShortName, StringComparison.OrdinalIgnoreCase))
                            return types[j];
                    }
                }
                catch { }
            }
            return null;
        }

        public static bool TryInvokeAny(object target, string[] methodNames, object[] args)
        {
            if (target == null || methodNames == null)
                return false;

            for (int i = 0; i < methodNames.Length; i++)
            {
                object result;
                if (TryInvoke(target, methodNames[i], args, out result))
                    return true;
            }
            return false;
        }

        public static object TryInvokeBest(object target, string methodName, object[] args)
        {
            object result;
            if (TryInvoke(target, methodName, args, out result))
                return result;
            return null;
        }

        private static bool TryInvoke(object target, string methodName, object[] args, out object result)
        {
            result = null;
            if (target == null)
                return false;

            Type type = target.GetType();
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];
                if (!string.Equals(m.Name, methodName, StringComparison.Ordinal))
                    continue;

                ParameterInfo[] ps = m.GetParameters();
                object[] converted;
                if (!TryBuildArgs(ps, args, out converted))
                    continue;

                try
                {
                    result = m.Invoke(m.IsStatic ? null : target, converted);
                    return true;
                }
                catch { }
            }

            return false;
        }

        private static bool TryBuildArgs(ParameterInfo[] ps, object[] source, out object[] converted)
        {
            converted = null;
            source = source ?? Array.Empty<object>();

            if (ps.Length > source.Length)
                return false;

            converted = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                object value = i < source.Length ? source[i] : Type.Missing;
                Type targetType = ps[i].ParameterType;

                if (value == null)
                {
                    converted[i] = null;
                    continue;
                }

                Type valueType = value.GetType();
                if (targetType.IsAssignableFrom(valueType))
                {
                    converted[i] = value;
                    continue;
                }

                try
                {
                    if (targetType == typeof(string))
                    {
                        converted[i] = value.ToString();
                        continue;
                    }
                    if (targetType == typeof(bool))
                    {
                        converted[i] = Convert.ToBoolean(value);
                        continue;
                    }
                    if (targetType == typeof(int))
                    {
                        converted[i] = Convert.ToInt32(value);
                        continue;
                    }
                    if (targetType == typeof(float))
                    {
                        converted[i] = Convert.ToSingle(value);
                        continue;
                    }
                    if (targetType.IsEnum)
                    {
                        converted[i] = Enum.ToObject(targetType, Convert.ToInt32(value));
                        continue;
                    }
                }
                catch
                {
                    return false;
                }

                return false;
            }

            return true;
        }

        public static object TryGetMemberValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            Type t = target.GetType();
            PropertyInfo p = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null)
            {
                try { return p.GetValue(target, null); } catch { }
            }

            FieldInfo f = t.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                try { return f.GetValue(target); } catch { }
            }

            return null;
        }

        public static bool TrySetMemberValue(object target, string memberName, object value)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return false;

            Type t = target.GetType();
            PropertyInfo p = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null && p.CanWrite)
            {
                try
                {
                    object converted = ConvertForType(value, p.PropertyType);
                    p.SetValue(target, converted, null);
                    return true;
                }
                catch { }
            }

            FieldInfo f = t.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
            {
                try
                {
                    object converted = ConvertForType(value, f.FieldType);
                    f.SetValue(target, converted);
                    return true;
                }
                catch { }
            }

            return false;
        }

        private static object TryGetStaticMemberValue(Type type, string memberName)
        {
            PropertyInfo p = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (p != null)
            {
                try { return p.GetValue(null, null); } catch { }
            }

            FieldInfo f = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null)
            {
                try { return f.GetValue(null); } catch { }
            }

            return null;
        }

        private static object ConvertForType(object value, Type targetType)
        {
            if (value == null)
                return null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            if (targetType == typeof(string))
                return value.ToString();

            if (targetType == typeof(bool))
                return Convert.ToBoolean(value);

            if (targetType == typeof(int))
                return Convert.ToInt32(value);

            if (targetType == typeof(float))
                return Convert.ToSingle(value);

            return value;
        }

        public static string GetName(object obj)
        {
            if (obj == null)
                return null;

            object value = TryGetMemberValue(obj, "name") ??
                           TryGetMemberValue(obj, "Name") ??
                           TryGetMemberValue(obj, "_name") ??
                           TryGetMemberValue(obj, "objectName") ??
                           TryGetMemberValue(obj, "ObjectName");

            return value != null ? value.ToString() : null;
        }
    }
}
