using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    
    /// 替代游戏里缺失的 LensFlare MonoBehaviour：
    /// - 尽量从游戏 AssetBundle 里找到 flare 的 prefab/material/texture 并直接实例化/绑定（不“手搓”特效）
    /// - 如果找不到 prefab，则用游戏 flare 贴图创建最基础的 billboard quad，并跟随舞台 Light 强度开关/缩放
    /// 暂时还没修好
    public class StageLensFlareDriver : MonoBehaviour
    {
        [Header("Init")]
        public int waitFrames = 60;
        public bool preloadFromIndex = true;
        public bool verboseLog = true;

        [Header("Root")]
        public string lensFlareRootName = "LensFlare";
        public bool autoFindOrCreateRoot = true;

        [Header("Spawn from AB (优先使用游戏 prefab/material)")]
        public bool spawnFlarePrefabsFromStageBundle = true;
        public bool spawnFlarePrefabsFromEffectBundles = false;

        [Header("Billboard fallback（仅当找不到可用的 prefab/renderer 时启用）")]
        public bool createBillboardsForUnityLights = true;
        public float baseSize = 0.35f;
        public float sizePerIntensity = 0.25f;
        public float zOffset = 0.0f;
        public bool billboardToCamera = true;

        [Header("Intensity mapping (如果 timeline 里有 lensFlareSetting 就用它)")]
        public bool useTimelineThresholds = true;
        public float lightStandard = 1.0f;
        public float underLimit = 0.02f;

        [Header("Asset pick")]
        public string preferredTextureName = "tex_env_live_cmn_flare013";
        public string preferredMaterialNameContains = "flare";

        private LiveTimelineControl _ctl;
        private StageController _stage;
        private string _bgId;

        private GameObject _root;
        private Dictionary<string, Texture2D> _flareTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private Material _flareMaterial;

        private readonly Dictionary<int, LensFlareBillboard> _billboardByLightId = new Dictionary<int, LensFlareBillboard>();

        private bool _inited;

        private void OnEnable()
        {
            if (!_inited) StartCoroutine(CoInit());
        }

        private void OnDisable()
        {
            _inited = false;
            _billboardByLightId.Clear();
        }

        private IEnumerator CoInit()
        {
            _inited = true;

            // 等 Director / stage instantiate
            for (int i = 0; i < Mathf.Max(1, waitFrames); i++) yield return null;

            BindIfPossible();
            if (_ctl == null || _stage == null)
            {
                if (verboseLog) Debug.LogWarning("[StageLensFlareDriver] Director/Stage not ready yet. Will retry in LateUpdate.");
                yield break;
            }

            if (useTimelineThresholds && _ctl.data != null && _ctl.data.lensFlareSetting != null)
            {
                // 直接读取 cutt 数据（如果存在）
                lightStandard = _ctl.data.lensFlareSetting.lightStandard;
                underLimit = _ctl.data.lensFlareSetting.underLimit;
                if (verboseLog) Debug.Log($"[StageLensFlareDriver] timeline thresholds: lightStandard={lightStandard}, underLimit={underLimit}");
            }

            if (preloadFromIndex) PreloadFlareBundlesFromIndex();

            // 找/建 LensFlare root
            _root = FindOrCreateLensFlareRoot();

            // 优先：从 AB 里找 flare prefab 实例化（最贴近“只用游戏资源”）
            if (spawnFlarePrefabsFromStageBundle) TrySpawnFlarePrefabsFromStageBundle();
            if (spawnFlarePrefabsFromEffectBundles) TrySpawnFlarePrefabsFromEffectBundles();

            // 解析贴图/材质
            _flareTextures = ResolveFlareTexturesFromLoadedBundles();
            _flareMaterial = ResolveFlareMaterialFromLoadedBundles(_flareTextures);

            if (verboseLog)
            {
                Debug.Log($"[StageLensFlareDriver] flareTextures={_flareTextures.Count}, flareMat={( _flareMaterial ? _flareMaterial.name : "null")}, root={(_root? _root.name:"null")}");
            }

            // 如果 root 下已经有 renderer（例如 prefab 本身带粒子/mesh），那基本就 OK
            if (_root != null && HasAnyRenderer(_root))
            {
                // 确保 renderer enabled（有时被禁用）
                EnableAllRenderers(_root, true);
                yield break;
            }

            // fallback：给舞台上的 Unity Light 生成 billboard（仍然使用游戏贴图）
            if (createBillboardsForUnityLights)
            {
                BuildBillboardsForLights();
            }
            else
            {
                if (verboseLog) Debug.LogWarning("[StageLensFlareDriver] No flare renderer found and fallback disabled.");
            }
        }

        private void LateUpdate()
        {
            // 自愈：进入 live 后才挂上脚本时，可能会错过 init
            if (_ctl == null || _stage == null)
                BindIfPossible();

            // billboard 跟随 camera
            if (!billboardToCamera) return;

            Camera cam = GetActiveCamera();
            if (cam == null) return;

            foreach (var kv in _billboardByLightId)
            {
                var bb = kv.Value;
                if (bb != null) bb.FaceCamera(cam.transform);
            }
        }

        private void BindIfPossible()
        {
            var dir = Director.instance;
            if (!dir) return;

            _ctl = dir._liveTimelineControl;
            _stage = dir._stageController;
            _bgId = (dir.live != null) ? dir.live.BackGroundId : null;
        }

        private GameObject FindOrCreateLensFlareRoot()
        {
            if (_stage == null) return null;

            // 1) StageObjectMap 直接命中
            if (_stage.StageObjectMap != null)
            {
                foreach (var kv in _stage.StageObjectMap)
                {
                    if (string.Equals(kv.Key, lensFlareRootName, StringComparison.OrdinalIgnoreCase) && kv.Value != null)
                        return kv.Value;
                }
            }

            // 2) transform 深度查找
            var t = FindDeepChild(_stage.transform, lensFlareRootName);
            if (t != null) return t.gameObject;

            if (!autoFindOrCreateRoot) return null;

            // 3) 创建一个空 root
            var go = new GameObject(lensFlareRootName);
            go.transform.SetParent(_stage.transform, false);
            RegisterAllChildrenToStageMap(go);
            if (verboseLog) Debug.Log("[StageLensFlareDriver] created LensFlare root");
            return go;
        }

        private void PreloadFlareBundlesFromIndex()
        {
            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null) return;

            // 优先锁定：tex_env_live_cmn_flare / lensflare / flare
            var needles = new string[]
            {
                "tex_env_live_cmn_flare",
                "lensflare",
                "lens_flare",
                "_flare",
                "/flare",
                " flare"
            };

            var hits = new List<UmaDatabaseEntry>();

            foreach (var e in main.AbList.Values)
            {
                if (e == null || !e.IsAssetBundle || string.IsNullOrEmpty(e.Name)) continue;

                // 尽量少加载：只扫可能相关的目录
                bool inLikelyFolder =
                    e.Name.StartsWith("3d/env/live/", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.StartsWith("3d/effect/live/", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.StartsWith("3d/effect/common/", StringComparison.OrdinalIgnoreCase) ||
                    e.Name.StartsWith("3d/env/common/", StringComparison.OrdinalIgnoreCase);

                if (!inLikelyFolder) continue;

                bool matched = false;
                for (int i = 0; i < needles.Length; i++)
                {
                    if (e.Name.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched) continue;

                hits.Add(e);
                if (hits.Count >= 60) break; // 防止一次性 load 太多
            }

            if (verboseLog) Debug.Log($"[StageLensFlareDriver] preloadCandidates={hits.Count}");

            foreach (var e in hits)
            {
                UmaAssetManager.LoadAssetBundle(e, neverUnload: true, isRecursive: true);
            }
        }

        private void TrySpawnFlarePrefabsFromStageBundle()
        {
            if (string.IsNullOrEmpty(_bgId)) return;

            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null) return;

            string controllerKey = $"3d/env/live/live{_bgId}/pfb_env_live{_bgId}_controller000";
            if (!main.AbList.TryGetValue(controllerKey, out var stageEntry) || stageEntry == null)
            {
                if (verboseLog) Debug.LogWarning($"[StageLensFlareDriver] stage entry not found: {controllerKey}");
                return;
            }

            var bundle = UmaAssetManager.LoadAssetBundle(stageEntry, neverUnload: true, isRecursive: true);
            if (bundle == null) return;

            GameObject[] allGos;
            try { allGos = bundle.LoadAllAssets<GameObject>(); }
            catch { return; }
            if (LiveRuntimeDiagnostics.Enabled)
                LiveRuntimeDiagnostics.RecordAssetLoad(stageEntry.Name, "LoadAllAssets", typeof(GameObject), allGos);

            var flarePrefabs = allGos
                .Where(go => go != null && (go.name.IndexOf("flare", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            go.name.IndexOf("lensflare", StringComparison.OrdinalIgnoreCase) >= 0))
                .Distinct()
                .ToList();

            if (flarePrefabs.Count == 0)
            {
                if (verboseLog) Debug.Log($"[StageLensFlareDriver] no flare prefab found inside stage bundle (bgId={_bgId})");
                return;
            }

            int spawned = 0;
            foreach (var p in flarePrefabs)
            {
                // 防止重复
                if (_stage.transform.Find(p.name + "(Clone)") != null) continue;

                var inst = Instantiate(p, _stage.transform);
                RegisterAllChildrenToStageMap(inst);

                spawned++;
                if (_root == null && string.Equals(p.name, lensFlareRootName, StringComparison.OrdinalIgnoreCase))
                    _root = inst;
            }

            if (verboseLog) Debug.Log($"[StageLensFlareDriver] spawned {spawned} flare-prefabs from stage bundle");
        }

        private void TrySpawnFlarePrefabsFromEffectBundles()
        {
            var main = UmaViewerMain.Instance;
            if (main == null || main.AbEffect == null) return;

            int spawned = 0;
            foreach (var e in main.AbEffect)
            {
                if (e == null || string.IsNullOrEmpty(e.Name)) continue;
                if (e.Name.IndexOf("flare", StringComparison.OrdinalIgnoreCase) < 0 &&
                    e.Name.IndexOf("lensflare", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                // 只取 GameObject prefab
                try
                {
                    var prefab = e.Get<GameObject>(withDependencies: true);
                    if (prefab == null) continue;

                    if (_stage.transform.Find(prefab.name + "(Clone)") != null) continue;

                    var inst = Instantiate(prefab, _stage.transform);
                    RegisterAllChildrenToStageMap(inst);
                    spawned++;
                }
                catch { /* ignore */ }

                if (spawned >= 12) break;
            }

            if (verboseLog) Debug.Log($"[StageLensFlareDriver] spawned {spawned} flare-prefabs from effect bundles");
        }

        private Dictionary<string, Texture2D> ResolveFlareTexturesFromLoadedBundles()
        {
            var result = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

            foreach (var ab in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (!ab) continue;

                string[] names;
                try { names = ab.GetAllAssetNames(); }
                catch { continue; }

                foreach (var p in names)
                {
                    string file = Path.GetFileName(p); // 可能没有扩展名
                    if (string.IsNullOrEmpty(file)) continue;

                    // 你给的例子：tex_env_live_cmn_flare013
                    bool likely = file.StartsWith("tex_env_live_cmn_flare", StringComparison.OrdinalIgnoreCase) ||
                                  file.IndexOf("lensflare", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  file.IndexOf("_flare", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!likely) continue;

                    if (result.ContainsKey(file)) continue;

                    Texture2D tex = null;
                    try { tex = ab.LoadAsset<Texture2D>(p); }
                    catch { tex = null; }
                    if (LiveRuntimeDiagnostics.Enabled)
                        LiveRuntimeDiagnostics.RecordAssetLoad(ab.name, "LoadAsset", typeof(Texture2D),
                            new UnityEngine.Object[] { tex }, tex, p);

                    if (tex != null) result[file] = tex;
                }
            }

            return result;
        }

        private Material ResolveFlareMaterialFromLoadedBundles(Dictionary<string, Texture2D> texSet)
        {
            // 1) 先尝试：从已加载 bundle 里找 “flare” Material（完全使用游戏材质）
            foreach (var ab in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (!ab) continue;

                string[] names;
                try { names = ab.GetAllAssetNames(); }
                catch { continue; }

                foreach (var p in names)
                {
                    string file = Path.GetFileName(p);
                    if (string.IsNullOrEmpty(file)) continue;

                    // 材质在 uma 里通常叫 mtl_*
                    if (file.IndexOf("mtl", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (file.IndexOf(preferredMaterialNameContains, StringComparison.OrdinalIgnoreCase) < 0 &&
                        file.IndexOf("flare", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    Material mat = null;
                    try { mat = ab.LoadAsset<Material>(p); }
                    catch { mat = null; }
                    if (LiveRuntimeDiagnostics.Enabled)
                        LiveRuntimeDiagnostics.RecordAssetLoad(ab.name, "LoadAsset", typeof(Material),
                            new UnityEngine.Object[] { mat }, mat, p);

                    if (mat == null) continue;

                    // 如果能匹配到我们想要的贴图名就更好
                    if (!string.IsNullOrEmpty(preferredTextureName))
                    {
                        if (mat.mainTexture != null &&
                            mat.mainTexture.name.IndexOf(preferredTextureName, StringComparison.OrdinalIgnoreCase) >= 0)
                            return mat;
                    }

                    // 否则只要是 flare 材质也先用
                    return mat;
                }
            }

            // 2) fallback：用游戏贴图 + 一个通用 additive shader（不是“设计特效”，只是让资源可见）
            if (texSet == null || texSet.Count == 0) return null;

            Texture2D tex = PickPreferredTexture(texSet);
            if (tex == null) return null;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Additive");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
            {
                if (verboseLog) Debug.LogWarning("[StageLensFlareDriver] no suitable shader found for fallback material");
                return null;
            }

            var m = new Material(shader);
            m.name = "mat_fallback_lensflare";
            ApplyTextureToMaterial(m, tex);
            TrySetMaterialToAdditive(m);
            return m;
        }

        private Texture2D PickPreferredTexture(Dictionary<string, Texture2D> texSet)
        {
            if (texSet == null || texSet.Count == 0) return null;

            // prefer exact file name
            if (!string.IsNullOrEmpty(preferredTextureName))
            {
                foreach (var kv in texSet)
                {
                    if (kv.Key.IndexOf(preferredTextureName, StringComparison.OrdinalIgnoreCase) >= 0 && kv.Value != null)
                        return kv.Value;
                }
            }

            // then prefer anything containing "flare013"
            foreach (var kv in texSet)
            {
                if (kv.Key.IndexOf("flare013", StringComparison.OrdinalIgnoreCase) >= 0 && kv.Value != null)
                    return kv.Value;
            }

            // else first
            return texSet.Values.FirstOrDefault(t => t != null);
        }

        private void BuildBillboardsForLights()
        {
            if (_stage == null) return;
            if (_flareMaterial == null)
            {
                if (verboseLog) Debug.LogWarning("[StageLensFlareDriver] flare material not found; cannot build billboards.");
                return;
            }

            var lights = _stage.GetComponentsInChildren<Light>(true);
            if (lights == null || lights.Length == 0)
            {
                if (verboseLog) Debug.LogWarning("[StageLensFlareDriver] no Unity Light components found in stage.");
                return;
            }

            int created = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null) continue;

                // 过滤：只对点光/聚光做 flare（方向光一般不需要）
                if (l.type != LightType.Point && l.type != LightType.Spot) continue;

                int id = l.GetInstanceID();
                if (_billboardByLightId.ContainsKey(id)) continue;

                var go = CreateBillboardQuad(l.gameObject.name + "_lensflare", l.transform);
                if (go == null) continue;

                var bb = go.AddComponent<LensFlareBillboard>();
                bb.sourceLight = l;
                bb.material = _flareMaterial;
                bb.baseSize = baseSize;
                bb.sizePerIntensity = sizePerIntensity;
                bb.zOffset = zOffset;
                bb.lightStandard = Mathf.Max(0.0001f, lightStandard);
                bb.underLimit = Mathf.Max(0f, underLimit);

                _billboardByLightId[id] = bb;
                created++;
            }

            if (verboseLog) Debug.Log($"[StageLensFlareDriver] created billboards={created} for lights={lights.Length}");
        }

        private GameObject CreateBillboardQuad(string name, Transform parent)
        {
            // 用 Quad（不带 collider）
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            if (quad == null) return null;

            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0f, zOffset);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = Vector3.one * baseSize;

            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = quad.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = _flareMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
            return quad;
        }

        private static bool HasAnyRenderer(GameObject root)
        {
            if (root == null) return false;
            return root.GetComponentsInChildren<Renderer>(true).Any(r => r != null);
        }

        private static void EnableAllRenderers(GameObject root, bool enabled)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null) r.enabled = enabled;
            }
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;

            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                    return t;

                for (int i = 0; i < t.childCount; i++)
                    stack.Push(t.GetChild(i));
            }
            return null;
        }

        private static Camera GetActiveCamera()
        {
            var builder = UmaViewerBuilder.Instance;
            if (builder != null && builder.AnimationCamera != null && builder.AnimationCamera.enabled)
                return builder.AnimationCamera;

            var cam = Camera.main;
            if (cam != null) return cam;

            return GameObject.FindObjectsOfType<Camera>().FirstOrDefault(c => c != null && c.enabled);
        }

        private static void ApplyTextureToMaterial(Material mat, Texture2D tex)
        {
            if (mat == null || tex == null) return;

            // 常见属性名都写一遍，最大兼容
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
        }

        private static void TrySetMaterialToAdditive(Material mat)
        {
            if (mat == null) return;

            // 如果是 Unlit/Transparent 或其它 shader，可以尝试把混合模式调成 add（不保证所有 shader 都支持）
            if (mat.HasProperty("_SrcBlend") && mat.HasProperty("_DstBlend"))
            {
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.One);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
            }
        }

        private static void RegisterAllChildrenToStageMap(GameObject root)
        {
            var stage = Director.instance ? Director.instance._stageController : null;
            if (stage == null || stage.StageObjectMap == null || root == null) return;

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string key = t.name.Replace("(Clone)", "");
                if (!stage.StageObjectMap.ContainsKey(key))
                    stage.StageObjectMap.Add(key, t.gameObject);
            }
        }

        /// <summary> 一个极简 flare：把 quad billboard 朝向 camera，并用 Light.intensity 控制 alpha/scale </summary>
        private class LensFlareBillboard : MonoBehaviour
        {
            public Light sourceLight;
            public Material material;

            public float baseSize = 0.35f;
            public float sizePerIntensity = 0.25f;
            public float zOffset = 0f;

            public float lightStandard = 1.0f;
            public float underLimit = 0.02f;

            private MeshRenderer _mr;
            private MaterialPropertyBlock _mpb;
            private int _colorId = -1;
            private int _baseColorId = -1;

            private void Awake()
            {
                _mr = GetComponent<MeshRenderer>();
                _mpb = new MaterialPropertyBlock();

                if (_mr != null && material != null) _mr.sharedMaterial = material;

                _colorId = Shader.PropertyToID("_Color");
                _baseColorId = Shader.PropertyToID("_BaseColor");
            }

            private void LateUpdate()
            {
                if (sourceLight == null || _mr == null) return;

                // 跟随光强
                float norm = sourceLight.intensity / Mathf.Max(0.0001f, lightStandard);
                bool visible = norm >= underLimit;

                _mr.enabled = visible;
                if (!visible) return;

                float a = Mathf.Clamp01(norm);
                float s = baseSize + sizePerIntensity * a;

                transform.localPosition = new Vector3(0f, 0f, zOffset);
                transform.localScale = Vector3.one * s;

                // 写颜色 alpha（尽量不改变 RGB）
                _mr.GetPropertyBlock(_mpb);
                Color c = Color.white;
                c.a = a;

                _mpb.SetColor(_colorId, c);
                _mpb.SetColor(_baseColorId, c);
                _mr.SetPropertyBlock(_mpb);
            }

            public void FaceCamera(Transform cam)
            {
                if (cam == null) return;
                // 直接面对 camera
                transform.rotation = Quaternion.LookRotation(cam.position - transform.position, cam.up);
            }
        }
    }
}
