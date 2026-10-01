using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using Gallop.Live.Cutt;
namespace Gallop.Live
{
    public class StageBlinkLightDriver : MonoBehaviour
    {
        public bool verboseLog = false;

        public float emissionBoost = 1.1f;
        public float blinkSimpleUseNormalCorrection = 0f;
        public float localTimeScale = 1f;
        public float powerSmoothing = 0f;
        public float offEps = 0.0001f;

        public float onThreshold = 0f;
        public float offThreshold = 0f;
        public bool useForceRenderingOff = false;

        public bool continueWhenNoUpdate = false;
        public float maxCatchupSecondsPerFrame = 0.2f;

        public int uvSortingBias = 2048;
        public int rootSortingStride = 8192;
        public int stableSortStride = 4;

        public bool probeTimeline = false;
        public string probeTimelineNameContains = "";
        public bool probeTimelineOncePerFrame = true;

        public bool autoFixLightBlinkBlend = true;

        // 官方 LightBlinkBlend 常见 fallback 是 Multiply = DstColor / One
        public BlendMode lightBlinkBlendSrc = BlendMode.DstColor;
        public BlendMode lightBlinkBlendDst = BlendMode.One;
        public int lightBlinkBlendRenderQueue = -1;

        public bool useTimelineLightBlendMode = true;
        public LiveDefine.LightBlendMode fallbackLightBlendMode = LiveDefine.LightBlendMode.Multiply;

        private const int kMaxBlinkSlots = 10;
        private int _probeFrame = -1;

        private LiveTimelineControl _ctl;
        private StageController _stage;
        private MaterialPropertyBlock _mpb;
        private Func<float> _liveNowGetter;

        private float LiveNow()
        {
            return (_liveNowGetter != null) ? _liveNowGetter() : Time.time;
        }

        private static Func<float> BuildLiveNowGetter(LiveTimelineControl ctl)
        {
            if (ctl == null) return null;

            var t = ctl.GetType();
            var p = t.GetProperty("currentLiveTime") ?? t.GetProperty("CurrentLiveTime");
            if (p != null && p.PropertyType == typeof(float))
                return () => (float)p.GetValue(ctl, null);

            var f = t.GetField("currentLiveTime") ?? t.GetField("CurrentLiveTime");
            if (f != null && f.FieldType == typeof(float))
                return () => (float)f.GetValue(ctl);

            return null;
        }

        private struct CacheItem
        {
            public BlinkLightUpdateInfo updateInfo;
            public float currentLiveTime;
            public int updatedFrame;
        }

        private readonly Dictionary<string, CacheItem> _latest = new Dictionary<string, CacheItem>(256);
        private readonly Dictionary<string, int> _rootBase = new Dictionary<string, int>(256);
        private int _nextRootBase = 0;

        private bool _allOffInited = false;
        private readonly List<string> _tmpKeys = new List<string>(256);

        private struct GroundPick
        {
            public string root;
            public int frame;
        }

        private readonly Dictionary<string, GroundPick> _groundChosen = new Dictionary<string, GroundPick>(16);

        private sealed class LightContainerState
        {
            public int index;
            public int keyIndex;
            public float progressTime;
            public int pattern;

            public float basePower;
            public float powerMin;
            public float powerMax;
            public float powerDiff;

            public int loopCountMax;
            public float waitTime;   
            public float turnOnTime;
            public float turnOffTime;
            public float keepTime;
            public float intervalTime;  
            public float loopTime;

            public float currentPower;
            public float currentHRatio;
            public float currentSRatio;
            public float currentVRatio;
            public int loopCount;
        }

        private sealed class BlinkRootRuntime
        {
            public string runtimeKey;
            public LiveTimelineKeyBlinkLightData key;
            public float localTime;
            public float liveTime;
            public int lightBlendMode;
            public int pattern;
            public int colorType;

            public readonly Color[] color0 = new Color[kMaxBlinkSlots];
            public readonly Color[] color1 = new Color[kMaxBlinkSlots];
            public readonly bool[] isReverseHue = new bool[kMaxBlinkSlots];
            public readonly Vector4[] currentColors = new Vector4[kMaxBlinkSlots];
            public readonly LightContainerState[] slots = new LightContainerState[kMaxBlinkSlots];

            public BlinkRootRuntime(string runtimeKey)
            {
                this.runtimeKey = runtimeKey;
                for (int i = 0; i < kMaxBlinkSlots; i++)
                    slots[i] = new LightContainerState { index = i };
            }
        }

        private sealed class RootCache
        {
            public string rootName;
            public GameObject rootGo;
            public bool rootIsUv;
            public bool rootIsBeam;
            public bool dirty = true;

            public readonly List<RendererEntry> renderers = new List<RendererEntry>(64);
            public int slotCount;
            public int lastRendererCount;
            public bool washBlendModeInited;
            public bool lastUseWashLightBlendMode;
        }

        private readonly Dictionary<string, RootCache> _rootCache = new Dictionary<string, RootCache>(256);
        private readonly Dictionary<string, BlinkRootRuntime> _runtime = new Dictionary<string, BlinkRootRuntime>(256);

        private enum IndexMode
        {
            Invalid = -1,
            LightPrefixNumber,
            SuffixNumber,
            LastDigits
        }

        private struct IndexToken
        {
            public bool valid;
            public IndexMode mode;
            public int n;

            public int ToRawIndex()
            {
                return valid ? n : -1;
            }
        }

        private struct RendererEntry
        {
            public Renderer r;
            public int stableSlot;

            public string cachedChildName;
            public IndexToken token;
            public int rawIndex;
            public int colorIndex;

            public bool isBlinkSimple;
            public bool isUvAlphaMask;
            public bool isLightAdd1;
            public bool isLightBlinkBlend;
            public bool hasColorPowerMultiply;
            public bool wantsMpb;

            // 官方 RendererData 层
            public WashLightController washLightController;
            public UnityLensFlareController unityLensFlareController;
            public Material material;

            public bool isWashLight;
            public bool isWashLightProjection;

            public bool useLightBlendMode;
            public int rendererLightBlendMode;

            public bool blendConfigured;
            public int blendConfiguredMode;

            public Material[] cachedSharedMaterialsRef;
            public int cachedSharedMaterialsLen;

            public bool hasSmoothed;
            public float smoothedP;
            public bool renderOnState;
        }

        private static readonly Regex ReLightPrefix = new Regex(@"^light(\d+)(?:_|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ReSuffixNumber = new Regex(@"_(\d+)$", RegexOptions.Compiled);
        private static readonly Regex ReLastDigits = new Regex(@"(\d+)(?!.*\d)", RegexOptions.Compiled);
        private readonly List<Material> _sharedMaterialScratch = new List<Material>(4);

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            BindIfPossible();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void BindIfPossible()
        {
            var dir = Director.instance;
            _ctl = dir ? dir._liveTimelineControl : null;
            _stage = dir ? dir._stageController : null;

            if (_ctl == null || _stage == null) return;

            _liveNowGetter = BuildLiveNowGetter(_ctl);
            _ctl.OnUpdateBlinkLight += OnBlinkLight;

            if (verboseLog) Debug.Log("[StageBlinkLightDriver] bound");
        }

        private void Unbind()
        {
            if (_ctl != null) _ctl.OnUpdateBlinkLight -= OnBlinkLight;

            _ctl = null;
            _stage = null;
            _liveNowGetter = null;

            _latest.Clear();
            _rootCache.Clear();
            _runtime.Clear();

            _rootBase.Clear();
            _nextRootBase = 0;
            _allOffInited = false;

            _tmpKeys.Clear();
            _groundChosen.Clear();
        }

        private static bool IsBlinkRootName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.IndexOf("_spotlight3d_controller", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            if (name.IndexOf("ledlight", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return name.IndexOf("_blinklight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("_blinkbeamlight", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBeamRootName(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                   name.IndexOf("_blinkbeamlight", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsProtectedLedName(string name)
        {
            return !string.IsNullOrEmpty(name) &&
                   name.IndexOf("ledlight", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IndexToken InvalidToken()
        {
            return new IndexToken { valid = false, mode = IndexMode.Invalid, n = 0 };
        }

        private static IndexToken ParseIndexToken(string childName)
        {
            if (string.IsNullOrEmpty(childName))
                return InvalidToken();

            var m = ReLightPrefix.Match(childName);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int lightN))
                return new IndexToken { valid = true, mode = IndexMode.LightPrefixNumber, n = lightN };

            m = ReSuffixNumber.Match(childName);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int suffixN))
                return new IndexToken { valid = true, mode = IndexMode.SuffixNumber, n = suffixN };

            m = ReLastDigits.Match(childName);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int lastN))
                return new IndexToken { valid = true, mode = IndexMode.LastDigits, n = lastN };

            return InvalidToken();
        }

        private static bool TryParseLightOwnerIndex(string name, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(name)) return false;

            var m = ReLightPrefix.Match(name);
            if (!m.Success) return false;

            return int.TryParse(m.Groups[1].Value, out index);
        }

        private string ResolveIndexedOwnerName(Transform t, Transform root)
        {
            while (t != null && t != root)
            {
                string n = t.name ?? "";
                if (TryParseLightOwnerIndex(n, out _))
                    return n;

                t = t.parent;
            }
            return null;
        }

        private static bool TryReadBoolMember(object obj, out bool value, params string[] names)
        {
            value = false;
            if (obj == null) return false;

            var t = obj.GetType();
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            for (int i = 0; i < names.Length; i++)
            {
                var f = t.GetField(names[i], flags);
                if (f != null && f.FieldType == typeof(bool))
                {
                    value = (bool)f.GetValue(obj);
                    return true;
                }

                var p = t.GetProperty(names[i], flags);
                if (p != null && p.PropertyType == typeof(bool) && p.GetIndexParameters().Length == 0)
                {
                    value = (bool)p.GetValue(obj, null);
                    return true;
                }
            }

            return false;
        }

        private static bool ReadUseWashLightBlendMode(ref BlinkLightUpdateInfo info)
        {
            // 官方 UpdateInfo 里读的是 updateInfo + 0x58。
            // 你项目字段名如果不同，就把真实字段名加到这里。
            object boxed = info;

            bool v;
            if (TryReadBoolMember(
                    boxed,
                    out v,
                    "useWashLightBlendMode",
                    "UseWashLightBlendMode",
                    "useWashLightBlend",
                    "UseWashLightBlend",
                    "isUseWashLightBlendMode",
                    "IsUseWashLightBlendMode"))
            {
                return v;
            }

            return false;
        }

        private static bool TryGetProjectionMeshRendererSafe(WashLightController wash, out Renderer renderer)
        {
            renderer = null;
            if (wash == null) return false;

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            var m = wash.GetType().GetMethod("TryGetProjectionMeshRenderer", flags);
            if (m == null) return false;

            var ps = m.GetParameters();
            if (ps.Length != 1) return false;

            object[] args = { null };

            try
            {
                object ret = m.Invoke(wash, args);
                if (ret is bool ok && ok)
                {
                    renderer = args[0] as Renderer;
                    return renderer != null;
                }
            }
            catch
            {
                renderer = null;
            }

            return false;
        }

        private void OnBlinkLight(LiveTimelineBlinkLightData data,ref BlinkLightUpdateInfo info,float currentLiveTime)
        {
            string rootName = (data != null) ? data.name : null;
            if (string.IsNullOrEmpty(rootName)) return;
            if (!IsBlinkRootName(rootName)) return;

            if (probeTimeline)
            {
                bool ok = string.IsNullOrEmpty(probeTimelineNameContains) ||
                        rootName.IndexOf(probeTimelineNameContains, StringComparison.OrdinalIgnoreCase) >= 0;

                if (ok)
                {
                    int f = Time.frameCount;
                    if (!probeTimelineOncePerFrame || _probeFrame != f)
                    {
                        _probeFrame = f;
                        Debug.Log(
                            $"[TL] frame={f} root={rootName} " +
                            $"progressTime={info.progressTime:F6} keyIndex={info.keyIndex} " +
                            $"blend={info.LightBlendMode} washBlend={info.UseWashLightBlendMode}"
                        );
                    }
                }
            }

            info.progressTime = Mathf.Max(0f, info.progressTime);

            _latest[rootName] = new CacheItem
            {
                updateInfo = info,
                currentLiveTime = currentLiveTime,
                updatedFrame = Time.frameCount
            };
        }

        private void LateUpdate()
        {
            if (_ctl == null || _stage == null)
                BindIfPossible();

            if (_ctl == null || _stage == null || _stage.StageObjectMap == null)
                return;

            if (!_allOffInited)
            {
                InitAllStageLightsOff();
                _allOffInited = true;
            }

            if (_latest.Count == 0)
                return;


            if (continueWhenNoUpdate)
            {
                float dt = Mathf.Min(Time.deltaTime, maxCatchupSecondsPerFrame);
                if (dt > 0f)
                {
                    _tmpKeys.Clear();
                    foreach (var k in _latest.Keys)
                        _tmpKeys.Add(k);

                    for (int i = 0; i < _tmpKeys.Count; i++)
                    {
                        string key = _tmpKeys[i];
                        var item = _latest[key];

                        if (item.updatedFrame != Time.frameCount)
                        {
                            item.updateInfo.progressTime += dt;
                            _latest[key] = item;
                        }
                    }
                }
            }

            foreach (var kv in _latest)
            {
                string rootName = kv.Key;
                if (!IsBlinkRootName(rootName))
                    continue;

                var item = kv.Value;

                BlinkLightUpdateInfo updateInfo = item.updateInfo;
                updateInfo.progressTime = Mathf.Max(0f, updateInfo.progressTime) * Mathf.Max(0.0001f, localTimeScale);

                float liveNow = item.currentLiveTime;

                if (_stage.StageObjectUnitMap.TryGetValue(rootName, out var unit) &&
                    unit != null && unit.ChildObjects != null && unit.ChildObjects.Length > 0)
                {
                    for (int i = 0; i < unit.ChildObjects.Length; i++)
                    {
                        var childPrefab = unit.ChildObjects[i];
                        if (childPrefab == null)
                            continue;

                        if (_stage.StageObjectMap.TryGetValue(childPrefab.name, out var realGo) && realGo != null)
                        {
                            string runtimeKey = rootName + "__U" + i;
                            ApplyToRootCached(runtimeKey, realGo, updateInfo, liveNow);
                        }
                    }
                }
                else if (_stage.StageObjectMap.TryGetValue(rootName, out var rootGo) && rootGo != null)
                {
                    ApplyToRootCached(rootName, rootGo, updateInfo, liveNow);
                }
            }

            // Exclusivity must be enforced AFTER the cached timeline update:
            // otherwise ApplyToRootCached immediately turns the losing panel on again.
            EnforceGroundPanelExclusive();
        }

        private BlinkRootRuntime GetOrCreateRuntime(string runtimeKey)
        {
            if (!_runtime.TryGetValue(runtimeKey, out var rt) || rt == null)
            {
                rt = new BlinkRootRuntime(runtimeKey);
                _runtime[runtimeKey] = rt;
            }
            return rt;
        }

        private RootCache GetOrBuildRootCache(string rootName, GameObject rootGo)
        {
            if (!_rootCache.TryGetValue(rootName, out var rc) || rc == null)
            {
                rc = new RootCache { rootName = rootName };
                _rootCache[rootName] = rc;
            }

            if (rc.rootGo != rootGo)
            {
                rc.rootGo = rootGo;
                rc.rootIsUv = rootName.IndexOf("_uv", StringComparison.OrdinalIgnoreCase) >= 0;
                rc.rootIsBeam = IsBeamRootName(rootName);
                rc.dirty = true;
            }

            if (rc.dirty)
                BuildRootCache(rc);

            return rc;
        }

        private void BuildRootCache(RootCache rc)
        {
            rc.renderers.Clear();
            rc.slotCount = 0;

            if (rc.rootGo == null || !IsBlinkRootName(rc.rootName))
            {
                rc.dirty = false;
                return;
            }

            rc.rootIsUv = rc.rootName.IndexOf("_uv", StringComparison.OrdinalIgnoreCase) >= 0;
            rc.rootIsBeam = IsBeamRootName(rc.rootName);

            var pendingRenderers = new List<RendererEntry>(64);
            int maxColorIndex = -1;

            var transforms = rc.rootGo.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;

                var go = t.gameObject;
                if (go == null || go == rc.rootGo) continue;

                string directName = go.name ?? "";
                if (IsProtectedLedName(directName)) continue;

                string childName = ResolveIndexedOwnerName(t, rc.rootGo.transform);
                if (string.IsNullOrEmpty(childName)) continue;

                if (!TryParseLightOwnerIndex(childName, out int colorIndex))
                    continue;

                var token = new IndexToken
                {
                    valid = true,
                    mode = IndexMode.LightPrefixNumber,
                    n = colorIndex
                };

                if (colorIndex < 0 || colorIndex >= kMaxBlinkSlots)
                    continue;

                maxColorIndex = Mathf.Max(maxColorIndex, colorIndex);

                var r = t.GetComponent<Renderer>();
                if (r != null)
                {
                    bool isBlinkSimple, isUvAlphaMask, isLightAdd1, hasColorPowerMultiply, isLightBlinkBlend;
                    DetectType(r, out isBlinkSimple, out isUvAlphaMask, out isLightAdd1, out hasColorPowerMultiply, out isLightBlinkBlend);

                    var wash = go.GetComponent<WashLightController>();
                    var flare = go.GetComponent<UnityLensFlareController>();
                    var runtimeMat = r.material;
                    var mats = r.sharedMaterials;
                    pendingRenderers.Add(new RendererEntry
                    {
                        r = r,
                        stableSlot = pendingRenderers.Count,
                        cachedChildName = childName,
                        token = token,
                        rawIndex = colorIndex,
                        colorIndex = colorIndex,

                        isBlinkSimple = isBlinkSimple,
                        isUvAlphaMask = isUvAlphaMask,
                        isLightAdd1 = isLightAdd1,
                        isLightBlinkBlend = isLightBlinkBlend,
                        hasColorPowerMultiply = hasColorPowerMultiply,
                        wantsMpb = (isBlinkSimple || isUvAlphaMask || isLightAdd1),

                        washLightController = wash,
                        unityLensFlareController = flare,
                        material = runtimeMat,
                        isWashLight = false,
                        isWashLightProjection = false,
                        useLightBlendMode = false,
                        rendererLightBlendMode = 0,

                        blendConfigured = false,
                        blendConfiguredMode = int.MinValue,
                        cachedSharedMaterialsRef = mats,
                        cachedSharedMaterialsLen = (mats != null) ? mats.Length : 0,
                        hasSmoothed = false,
                        smoothedP = 0f,
                        renderOnState = false
                    });

                }
            }

            rc.slotCount = Mathf.Clamp(maxColorIndex + 1, 0, kMaxBlinkSlots);

            for (int i = 0; i < pendingRenderers.Count; i++)
                rc.renderers.Add(pendingRenderers[i]);

            rc.lastRendererCount = rc.renderers.Count;
            SetupWashLightParam(rc);
            rc.dirty = false;

            if (verboseLog)
            {
                Debug.Log($"[StageBlinkLightDriver] cache built root={rc.rootName} beam={rc.rootIsBeam} slots={rc.slotCount} R={rc.renderers.Count}");

                for (int i = 0; i < rc.renderers.Count; i++)
                {
                    var e = rc.renderers[i];
                    string n = (e.r != null) ? e.r.name : "<null>";
                    Debug.Log($"[BlinkMap] root={rc.rootName} renderer={n} owner={e.cachedChildName} rawIndex={e.rawIndex} colorIndex={e.colorIndex}");
                }
            }
        }

        private void SetupWashLightParam(RootCache rc)
        {
            if (rc == null) return;

            var projectionRenderers = new List<Renderer>(16);

            for (int i = 0; i < rc.renderers.Count; i++)
            {
                var e = rc.renderers[i];

                e.isWashLight = false;
                e.isWashLightProjection = false;

                var wash = e.washLightController;
                if (wash != null)
                {
                    // Native +0x151 is IsInitialized, not Behaviour.enabled.
                    // Initialize before identifying the projection renderer so cache
                    // construction does not depend on driver MonoBehaviour order.
                    if (!wash.IsInitialized)
                        wash.Initialize();

                    if (wash.IsInitialized)
                    {
                        e.isWashLight = true;

                        Renderer proj;
                        if (TryGetProjectionMeshRendererSafe(wash, out proj) && proj != null)
                            projectionRenderers.Add(proj);
                    }
                }

                rc.renderers[i] = e;
            }

            for (int i = 0; i < rc.renderers.Count; i++)
            {
                var e = rc.renderers[i];

                if (e.r != null && projectionRenderers.Contains(e.r))
                    e.isWashLightProjection = true;

                rc.renderers[i] = e;
            }

            if (verboseLog)
            {
                for (int i = 0; i < rc.renderers.Count; i++)
                {
                    var e = rc.renderers[i];
                    if (e.isWashLight || e.isWashLightProjection)
                    {
                        Debug.Log(
                            $"[BlinkWash] root={rc.rootName} r={(e.r ? e.r.name : "<null>")} " +
                            $"wash={e.isWashLight} projection={e.isWashLightProjection}"
                        );
                    }
                }
            }
        }

        private void SetWashLightBlendMode(RootCache rc, bool useWashLightBlendMode)
        {
            if (rc == null) return;

            if (rc.washBlendModeInited && rc.lastUseWashLightBlendMode == useWashLightBlendMode)
                return;

            rc.washBlendModeInited = true;
            rc.lastUseWashLightBlendMode = useWashLightBlendMode;

            for (int i = 0; i < rc.renderers.Count; i++)
            {
                var e = rc.renderers[i];

                if (e.isWashLight)
                {
                    e.useLightBlendMode = useWashLightBlendMode;
                    e.rendererLightBlendMode = (int)LiveDefine.LightBlendMode.Addition;
                    e.blendConfigured = false;
                    e.blendConfiguredMode = int.MinValue;
                }

                if (e.isWashLightProjection)
                {
                    e.useLightBlendMode = useWashLightBlendMode;
                    e.rendererLightBlendMode = (int)LiveDefine.LightBlendMode.Multiply;
                    e.blendConfigured = false;
                    e.blendConfiguredMode = int.MinValue;
                }

                rc.renderers[i] = e;
            }
        }
        private void InitAllStageLightsOff()
        {
            foreach (var kv in _stage.StageObjectMap)
            {
                string name = kv.Key;
                var go = kv.Value;
                if (go == null) continue;
                if (!IsBlinkRootName(name)) continue;

                ApplyRootOffCached(name, go);
            }

            if (verboseLog) Debug.Log("[StageBlinkLightDriver] InitAllStageLightsOff done");
        }

        private void EnforceGroundPanelExclusive()
        {
            _groundChosen.Clear();

            foreach (var kv in _latest)
            {
                string rootName = kv.Key;
                string g = GetGroundPanelGroupBase(rootName);
                if (g == null) continue;

                var item = kv.Value;
                if (!_groundChosen.TryGetValue(g, out var cur) || item.updatedFrame > cur.frame)
                    _groundChosen[g] = new GroundPick { root = rootName, frame = item.updatedFrame };
            }

            if (_groundChosen.Count == 0) return;

            foreach (var kv in _stage.StageObjectMap)
            {
                string name = kv.Key;
                var go = kv.Value;
                if (go == null) continue;

                string g = GetGroundPanelGroupBase(name);
                if (g == null) continue;

                if (_groundChosen.TryGetValue(g, out var pick) && name != pick.root)
                {
                    ApplyRootOffCached(name, go);
                    // A black MPB is not an occlusion/depth disable (and the
                    // non-MPB path explicitly resets forceRenderingOff=false).
                    // Keep the non-selected coplanar panel out of the depth pass.
                    var cache = GetOrBuildRootCache(name, go);
                    for (int i = 0; i < cache.renderers.Count; i++)
                    {
                        var renderer = cache.renderers[i].r;
                        if (renderer != null) renderer.forceRenderingOff = true;
                    }
                }
            }
        }

        private static string GetGroundPanelGroupBase(string rootName)
        {
            int i = rootName.IndexOf("_blinklight_ground_panel", StringComparison.Ordinal);
            if (i < 0) return null;
            return rootName.Substring(0, i + "_blinklight_ground_panel".Length);
        }

        private int GetRootBase(string rootName)
        {
            if (_rootBase.TryGetValue(rootName, out int b)) return b;
            b = _nextRootBase;
            _nextRootBase += rootSortingStride;
            _rootBase[rootName] = b;
            return b;
        }

        private void ApplyToRootCached(string runtimeKey,GameObject rootGo,BlinkLightUpdateInfo updateInfo,float liveNow)
        {
            var rc = GetOrBuildRootCache(runtimeKey, rootGo);
            if (rc.slotCount <= 0) return;

            var rt = GetOrCreateRuntime(runtimeKey);
            ApplyUpdateInfoToRuntime(rt, rc.slotCount, updateInfo, liveNow);
            BuildCurrentColors(rt, rc.slotCount);
            SetWashLightBlendMode(rc, updateInfo.UseWashLightBlendMode);
            int baseOrder = GetRootBase(runtimeKey);
            int stride = Mathf.Max(1, stableSortStride);

            for (int i = 0; i < rc.renderers.Count; i++)
            {
                var e = rc.renderers[i];
                var r = e.r;
                if (r == null)
                {
                    rc.dirty = true;
                    continue;
                }

                string childName = ResolveIndexedOwnerName(r.transform, rc.rootGo.transform) ?? "";

                if (!ReferenceEquals(childName, e.cachedChildName) && childName != e.cachedChildName)
                {
                    e.cachedChildName = childName;

                    if (TryParseLightOwnerIndex(childName, out int idx))
                    {
                        e.token = new IndexToken
                        {
                            valid = true,
                            mode = IndexMode.LightPrefixNumber,
                            n = idx
                        };
                        e.rawIndex = idx;
                        e.colorIndex = idx;
                    }
                    else
                    {
                        e.token = InvalidToken();
                        e.rawIndex = -1;
                        e.colorIndex = -1;
                    }

                    rc.dirty = true;
                }

                int slotIndex = e.colorIndex;
                if (slotIndex < 0 || slotIndex >= rc.slotCount)
                {
                    rc.renderers[i] = e;
                    continue;
                }

                r.GetSharedMaterials(_sharedMaterialScratch);
                int matsLen = _sharedMaterialScratch.Count;
                bool materialsChanged = matsLen != e.cachedSharedMaterialsLen;
                if (!materialsChanged)
                    for (int m = 0; m < matsLen; m++)
                        if (_sharedMaterialScratch[m] != e.cachedSharedMaterialsRef[m])
                        {
                            materialsChanged = true;
                            break;
                        }
                if (materialsChanged)
                {
                    bool isBlinkSimple, isUvAlphaMask, isLightAdd1, hasColorPowerMultiply, isLightBlinkBlend;
                    DetectType(r, out isBlinkSimple, out isUvAlphaMask, out isLightAdd1, out hasColorPowerMultiply, out isLightBlinkBlend);

                    e.isBlinkSimple = isBlinkSimple;
                    e.isUvAlphaMask = isUvAlphaMask;
                    e.isLightAdd1 = isLightAdd1;
                    e.isLightBlinkBlend = isLightBlinkBlend;
                    e.hasColorPowerMultiply = hasColorPowerMultiply;
                    e.wantsMpb = (isBlinkSimple || isUvAlphaMask || isLightAdd1);
                    e.blendConfigured = false;
                    e.blendConfiguredMode = int.MinValue;

                    e.cachedSharedMaterialsRef = _sharedMaterialScratch.ToArray();
                    e.cachedSharedMaterialsLen = matsLen;
                }

                Vector4 slotColor = rt.currentColors[slotIndex];
                float pRaw = Mathf.Max(0f, slotColor.w);

                float p = pRaw;
                if (powerSmoothing > 0f)
                {
                    float a = 1f - Mathf.Exp(-powerSmoothing * Time.deltaTime);
                    if (!e.hasSmoothed)
                    {
                        e.hasSmoothed = true;
                        e.smoothedP = pRaw;
                    }
                    else
                    {
                        e.smoothedP = Mathf.Lerp(e.smoothedP, pRaw, a);
                    }
                    p = e.smoothedP;
                }

                Color currentColor = new Color(slotColor.x, slotColor.y, slotColor.z, 1f);

                int uvBias = (rc.rootIsUv || e.isUvAlphaMask) ? uvSortingBias : 0;
                r.sortingOrder = baseOrder + uvBias + e.stableSlot * stride;

                EnsureLightBlinkBlendState(r, ref e, rt.lightBlendMode);

                if (e.wantsMpb)
                {
                    if (useForceRenderingOff)
                    {
                        float onTh = Mathf.Max(0f, onThreshold);
                        float offTh = Mathf.Clamp(offThreshold, 0f, onTh);

                        if (!e.renderOnState && p >= onTh) e.renderOnState = true;
                        else if (e.renderOnState && p <= offTh) e.renderOnState = false;

                        r.forceRenderingOff = !e.renderOnState;
                    }
                    else
                    {
                        r.forceRenderingOff = false;
                    }

                    ApplyMpbToRenderer(
                        r, currentColor, p, rt.liveTime,
                        e.isBlinkSimple, e.isUvAlphaMask, e.isLightAdd1, e.hasColorPowerMultiply);
                }
                else
                {
                    r.forceRenderingOff = false;
                }

                rc.renderers[i] = e;
            }
        }

        private void ApplyUpdateInfoToRuntime(
    BlinkRootRuntime rt,
    int slotCount,
    BlinkLightUpdateInfo u,
    float liveNow)
        {
            rt.localTime = Mathf.Max(0f, u.progressTime);
            rt.liveTime = liveNow;
            rt.lightBlendMode = (int)u.LightBlendMode;
            rt.pattern = (int)u.pattern;
            rt.colorType = (int)u.colorType;

            ApplyBlinkColors(rt, u);
            CopyReverseHue(rt.isReverseHue, u.isReverseHueArray);

            int keyIndexProxy = u.keyIndex;

            float rawWaitTime = u.waitTime;
            float rawIntervalTime = Mathf.Max(0f, u.intervalTime);

            for (int i = 0; i < slotCount; i++)
            {
                var s = rt.slots[i];
                s.index = i;
                s.keyIndex = keyIndexProxy;
                s.progressTime = rt.localTime;
                s.pattern = rt.pattern;

                s.basePower = Mathf.Max(0f, PickFloat(u.powerArray, i, 1f));
                s.powerMin = Mathf.Max(0f, u.powerMin);
                s.powerMax = Mathf.Max(0f, u.powerMax);
                s.powerDiff = Mathf.Max(0f, s.powerMax - s.powerMin);

                s.loopCountMax = Mathf.Max(0, u.loopCount);

                s.waitTime = 0f;
                s.turnOnTime = Mathf.Max(0.05f, u.turnOnTime);
                s.turnOffTime = Mathf.Max(0.05f, u.turnOffTime);
                s.keepTime = Mathf.Max(0f, u.keepTime);
                s.intervalTime = rawIntervalTime;

                if (s.pattern == (int)BlinkLightPattern.None)
                {
                    s.waitTime = 0f;
                }
                else if (s.pattern == (int)BlinkLightPattern.Random)
                {
                    float r = GetOfficialStyleRatio01(s.index, s.keyIndex);
                    float randomizedInterval = r * rawIntervalTime;
                    s.intervalTime = randomizedInterval;
                    s.waitTime = randomizedInterval;
                }
                else if (s.pattern == (int)BlinkLightPattern.Ascend)
                {
                    s.waitTime = i * rawWaitTime;
                }
                else if (s.pattern == (int)BlinkLightPattern.Descend)
                {
                    s.waitTime = (slotCount - i - 1) * rawWaitTime;
                }

                s.loopTime = s.turnOnTime + s.keepTime + s.turnOffTime + s.intervalTime;
                UpdateSlotState(s);
            }

            for (int i = slotCount; i < kMaxBlinkSlots; i++)
            {
                var s = rt.slots[i];
                s.index = i;
                s.keyIndex = keyIndexProxy;
                s.progressTime = 0f;
                s.pattern = 0;
                s.basePower = 0f;
                s.powerMin = 0f;
                s.powerMax = 0f;
                s.powerDiff = 0f;
                s.loopCountMax = 0;
                s.waitTime = 0f;
                s.turnOnTime = 0.05f;
                s.turnOffTime = 0.05f;
                s.keepTime = 0f;
                s.intervalTime = 0f;
                s.loopTime = 0f;
                s.currentPower = 0f;
                s.currentHRatio = 0f;
                s.currentSRatio = 0f;
                s.currentVRatio = 0f;
                s.loopCount = 0;
                rt.currentColors[i] = Vector4.zero;
            }
        }

        private static int GetKeyIndexProxy(LiveTimelineKeyBlinkLightData k)
        {
            if (k == null) return 0;

            var t = k.GetType();

            var p = t.GetProperty("keyIndex") ??
                    t.GetProperty("KeyIndex") ??
                    t.GetProperty("index") ??
                    t.GetProperty("Index");
            if (p != null && p.PropertyType == typeof(int))
            {
                try { return (int)p.GetValue(k, null); }
                catch { }
            }

            var f = t.GetField("keyIndex") ??
                    t.GetField("KeyIndex") ??
                    t.GetField("index") ??
                    t.GetField("Index");
            if (f != null && f.FieldType == typeof(int))
            {
                try { return (int)f.GetValue(k); }
                catch { }
            }

            return k.frame;
        }

        private static void UpdateSlotState(LightContainerState s)
        {
            int pattern = s.pattern;
            if (pattern >= 1 && pattern <= 3)
            {
                if (s.waitTime > 0f && s.waitTime > s.progressTime)
                {
                    s.currentPower = 0f;
                    s.currentHRatio = 0f;
                    s.currentSRatio = 0f;
                    s.currentVRatio = 0f;
                    s.loopCount = 0;
                    return;
                }

                float loopTime = Mathf.Max(0.0001f, s.loopTime);
                float t = s.progressTime - s.waitTime;
                int loopCount = Mathf.FloorToInt(t / loopTime);
                s.loopCount = loopCount;

                bool forceOff = false;
                bool isLastLoop = false;
                if (s.loopCountMax > 0)
                {
                    if (loopCount < s.loopCountMax)
                    {
                        isLastLoop = (s.loopCount == s.loopCountMax - 1);
                    }
                    else
                    {
                        forceOff = true;
                        s.loopCount = s.loopCountMax - 1;
                    }
                }

                s.currentHRatio = GetOfficialStyleRatio01(s.index, s.keyIndex + 3 * s.loopCount + 0);
                s.currentSRatio = GetOfficialStyleRatio01(s.index, s.keyIndex + 3 * s.loopCount + 1);
                s.currentVRatio = GetOfficialStyleRatio01(s.index, s.keyIndex + 3 * s.loopCount + 2);

                if (forceOff)
                {
                    s.currentPower = 0f;
                    return;
                }

                float phase = (s.progressTime - s.waitTime) - (s.loopCount * loopTime);

                if (phase < s.turnOnTime)
                {
                    float u = phase / Mathf.Max(0.0001f, s.turnOnTime);
                    if (s.loopCount > 0)
                        s.currentPower = (u * s.powerDiff) + s.powerMin;
                    else
                        s.currentPower = u * s.powerMax;
                    return;
                }

                if (phase < s.turnOnTime + s.keepTime)
                {
                    s.currentPower = s.powerMax;
                    return;
                }

                if (phase >= s.turnOnTime + s.keepTime + s.turnOffTime)
                {
                    s.currentPower = isLastLoop ? 0f : s.powerMin;
                    return;
                }

                float u2 = 1f - ((phase - (s.turnOnTime + s.keepTime)) / Mathf.Max(0.0001f, s.turnOffTime));
                if (isLastLoop)
                    s.currentPower = u2 * s.powerMax;
                else
                    s.currentPower = (u2 * s.powerDiff) + s.powerMin;
            }
            else
            {
                s.currentPower = s.basePower;
                s.currentHRatio = 0f;
                s.currentSRatio = 0f;
                s.currentVRatio = 0f;
                s.loopCount = 0;
            }
        }

        private static void BuildCurrentColors(BlinkRootRuntime rt, int slotCount)
        {
            for (int i = 0; i < slotCount; i++)
            {
                var s = rt.slots[i];
                float p = Mathf.Max(0f, s.currentPower);
                Color outColor;

                switch (rt.colorType)
                {
                    case 1:
                        {
                            int step = PositiveMod(s.loopCount, slotCount);
                            int src = (i + slotCount - step) % slotCount;
                            outColor = SafeColor(rt.color0, src, Color.black);
                            break;
                        }

                    case 2:
                        {
                            int step = PositiveMod(s.loopCount, slotCount);
                            int src = (i + step) % slotCount;
                            outColor = SafeColor(rt.color0, src, Color.black);
                            break;
                        }

                    case 3:
                        {
                            Color c0 = SafeColor(rt.color0, i, Color.black);
                            Color c1 = SafeColor(rt.color1, i, c0);
                            outColor = ((s.loopCount & 1) != 0) ? c1 : c0;
                            break;
                        }

                    default:
                        {
                            Color c0 = SafeColor(rt.color0, i, Color.black);
                            Color c1 = SafeColor(rt.color1, i, c0);

                            if (rt.pattern == 0)
                            {
                                outColor = c0;
                            }
                            else
                            {
                                outColor = GetBlinkColor(
                                    c0,
                                    c1,
                                    s.currentHRatio,
                                    s.currentSRatio,
                                    s.currentVRatio,
                                    rt.isReverseHue[i]);
                            }
                            break;
                        }
                }

                rt.currentColors[i] = new Vector4(outColor.r, outColor.g, outColor.b, p);
            }
        }

        private void ApplyRootOffCached(string rootName, GameObject rootGo)
        {
            var rc = GetOrBuildRootCache(rootName, rootGo);

            for (int i = 0; i < rc.renderers.Count; i++)
            {
                var e = rc.renderers[i];
                var r = e.r;
                if (r == null)
                {
                    rc.dirty = true;
                    continue;
                }


                EnsureLightBlinkBlendState(r, ref e,(int)fallbackLightBlendMode);

                if (e.wantsMpb)
                {
                    r.forceRenderingOff = useForceRenderingOff;
                    e.renderOnState = false;
                    e.hasSmoothed = false;
                    e.smoothedP = 0f;

                    r.GetPropertyBlock(_mpb);

                    if (e.isUvAlphaMask)
                    {
                        _mpb.SetColor("_MulColor0", Color.black);
                        _mpb.SetColor("_MulColor1", Color.black);
                        _mpb.SetFloat("_ColorPower", 0f);
                        _mpb.SetFloat("_ColorPowerMultiply", emissionBoost);
                        _mpb.SetFloat("_AppTime", LiveNow());
                    }
                    else if (e.isLightAdd1)
                    {
                        _mpb.SetColor("_MulColor0", Color.black);
                        _mpb.SetColor("_MulColor1", Color.black);
                        _mpb.SetFloat("_ColorPower", 0f);
                        if (e.hasColorPowerMultiply) _mpb.SetFloat("_ColorPowerMultiply", emissionBoost);
                    }
                    else if (e.isBlinkSimple)
                    {
                        _mpb.SetColor("_BlinkLightColor", Color.black);
                        _mpb.SetFloat("_ColorPower", 0f);
                        _mpb.SetFloat("_UseNormalCorrection", blinkSimpleUseNormalCorrection);
                    }

                    r.SetPropertyBlock(_mpb);
                }
                else
                {
                    r.forceRenderingOff = false;
                }

                rc.renderers[i] = e;
            }
        }

        private bool IsTargetBlinkBlendMaterial(Material m)
        {
            if (m == null) return false;

            string sn = (m.shader != null) ? (m.shader.name ?? "") : "";

            if (sn.IndexOf("LightBlinkBlend", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (sn.IndexOf("StageBeamLightCutoff", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            bool hasBlend = m.HasProperty("_SrcBlend") && m.HasProperty("_DstBlend");
            bool hasPower = m.HasProperty("_ColorPower");
            bool hasBlinkColor = m.HasProperty("_BlinkLightColor");
            bool hasMulColors = m.HasProperty("_MulColor0") && m.HasProperty("_MulColor1");

            return hasBlend && hasPower && (hasBlinkColor || hasMulColors);
        }

        private static bool TryConvertLightBlendMode(int raw, out LiveDefine.LightBlendMode mode)
        {
            if (raw >= (int)LiveDefine.LightBlendMode.Addition &&
                raw <= (int)LiveDefine.LightBlendMode.Multiply2x)
            {
                mode = (LiveDefine.LightBlendMode)raw;
                return true;
            }

            mode = LiveDefine.LightBlendMode.Multiply;
            return false;
        }

        private void EnsureLightBlinkBlendState(Renderer r, ref RendererEntry e, int lightBlendMode)
        {
            if (!autoFixLightBlinkBlend) return;
            if (!e.isLightBlinkBlend) return;
            if (r == null) return;

            int effectiveMode;

            // 官方逻辑：
            // RendererData.UseLightBlendMode == true 时，用 RendererData.LightBlendMode
            // 否则用 BlinkLightController._lightBlendMode，也就是 timeline key 的 LightBlendMode
            if (e.useLightBlendMode)
            {
                effectiveMode = e.rendererLightBlendMode;
            }
            else
            {
                effectiveMode = lightBlendMode;

                if (!useTimelineLightBlendMode)
                    effectiveMode = (int)fallbackLightBlendMode;
            }

            if (!TryConvertLightBlendMode(effectiveMode, out var mode))
                mode = fallbackLightBlendMode;

            // 官方 LightBlendMode:
            // Addition     = One / One
            // Multiply     = DstColor / One
            // SoftAddition = OneMinusDstColor / One
            // AlphaBlend   = SrcAlpha / OneMinusSrcAlpha
            // Multiply0    = DstColor / Zero
            // Multiply2x   = DstColor / SrcColor

            int modeId = (int)mode;

            // 不要再提前 return。
            // 官方 Update 末尾每帧都会 TrySetLightBlendModeMaterialProperty。
            // 你这里如果 cache return，材质被 timeline / prefab / 其他脚本改回 One/One 后就不会再修。
            // if (e.blendConfigured && e.blendConfiguredMode == modeId)
            //     return;

            var mats = r.materials;
            if (mats == null || mats.Length == 0)
                return;

            bool touched = false;

            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (!IsTargetBlinkBlendMaterial(m))
                    continue;

                bool ok = LiveDefine.TrySetLightBlendModeMaterialProperty(mode, m);

                // fallback：防止 LiveDefine 没写全，或者材质只暴露 _SrcBlend / _DstBlend
                if (!ok && m.HasProperty("_SrcBlend") && m.HasProperty("_DstBlend"))
                {
                    switch (mode)
                    {
                        case LiveDefine.LightBlendMode.Addition:
                            m.SetFloat("_SrcBlend", (float)BlendMode.One);
                            m.SetFloat("_DstBlend", (float)BlendMode.One);
                            ok = true;
                            break;

                        case LiveDefine.LightBlendMode.Multiply:
                            m.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
                            m.SetFloat("_DstBlend", (float)BlendMode.One);
                            ok = true;
                            break;

                        case LiveDefine.LightBlendMode.SoftAddition:
                            m.SetFloat("_SrcBlend", (float)BlendMode.OneMinusDstColor);
                            m.SetFloat("_DstBlend", (float)BlendMode.One);
                            ok = true;
                            break;

                        case LiveDefine.LightBlendMode.AlphaBlend:
                            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                            ok = true;
                            break;

                        case LiveDefine.LightBlendMode.Multiply0:
                            m.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
                            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                            ok = true;
                            break;

                        case LiveDefine.LightBlendMode.Multiply2x:
                            m.SetFloat("_SrcBlend", (float)BlendMode.DstColor);
                            m.SetFloat("_DstBlend", (float)BlendMode.SrcColor);
                            ok = true;
                            break;
                    }
                }

                if (!ok)
                    continue;

                touched = true;

                if (lightBlinkBlendRenderQueue >= 0)
                    m.renderQueue = lightBlinkBlendRenderQueue;

                if (verboseLog && m.HasProperty("_SrcBlend") && m.HasProperty("_DstBlend"))
                {
                    Debug.Log(
                        $"[StageBlinkLightDriver] LightBlinkBlend {r.name}/{m.name} " +
                        $"wash={e.isWashLight} projection={e.isWashLightProjection} " +
                        $"override={e.useLightBlendMode} timelineMode={lightBlendMode} " +
                        $"effectiveMode={mode} src={m.GetFloat("_SrcBlend")} dst={m.GetFloat("_DstBlend")}"
                    );
                }
            }

            if (touched)
            {
                e.blendConfigured = true;
                e.blendConfiguredMode = modeId;
            }
        }

        private void ApplyMpbToRenderer(
            Renderer r,
            Color currentColor,
            float p,
            float liveNow,
            bool isBlinkSimple,
            bool isUvAlphaMask,
            bool isLightAdd1,
            bool hasColorPowerMultiply)
        {
            r.GetPropertyBlock(_mpb);

            if (isUvAlphaMask)
            {
                _mpb.SetColor("_MulColor0", currentColor);
                _mpb.SetColor("_MulColor1", currentColor);
                _mpb.SetFloat("_ColorPower", p);
                _mpb.SetFloat("_ColorPowerMultiply", emissionBoost);
                _mpb.SetFloat("_AppTime", liveNow);
            }
            else if (isLightAdd1)
            {
                _mpb.SetColor("_MulColor0", currentColor);
                _mpb.SetColor("_MulColor1", currentColor);
                _mpb.SetFloat("_ColorPower", p);
                if (hasColorPowerMultiply) _mpb.SetFloat("_ColorPowerMultiply", emissionBoost);
            }
            else if (isBlinkSimple)
            {
                _mpb.SetColor("_BlinkLightColor", currentColor);
                _mpb.SetFloat("_ColorPower", p * emissionBoost);
                _mpb.SetFloat("_UseNormalCorrection", blinkSimpleUseNormalCorrection);
            }

            r.SetPropertyBlock(_mpb);
        }

        private void DetectType(
            Renderer r,
            out bool isBlinkSimple,
            out bool isUvAlphaMask,
            out bool isLightAdd1,
            out bool hasColorPowerMultiply,
            out bool isLightBlinkBlend)
        {
            isBlinkSimple = false;
            isUvAlphaMask = false;
            isLightAdd1 = false;
            hasColorPowerMultiply = false;
            isLightBlinkBlend = false;

            var mats = r.sharedMaterials;
            if (mats == null) return;

            bool hasBlinkColor = false;
            bool hasColorPower = false;
            bool hasMul0 = false;
            bool hasMul1 = false;
            bool hasMultiply = false;
            bool hasSrcBlend = false;
            bool hasDstBlend = false;

            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;

                var sh = m.shader;
                string sn = sh ? (sh.name ?? "") : "";

                if (sn.IndexOf("UVAlphaMask", StringComparison.OrdinalIgnoreCase) >= 0)
                    isUvAlphaMask = true;

                if (sn.IndexOf("LightBlinkSimple", StringComparison.OrdinalIgnoreCase) >= 0)
                    isBlinkSimple = true;

                if (!isUvAlphaMask && sn.IndexOf("LightAdd1", StringComparison.OrdinalIgnoreCase) >= 0)
                    isLightAdd1 = true;

                if (sn.IndexOf("LightBlinkBlend", StringComparison.OrdinalIgnoreCase) >= 0)
                    isLightBlinkBlend = true;

                if (sn.IndexOf("StageBeamLightCutoff", StringComparison.OrdinalIgnoreCase) >= 0)
                    isLightBlinkBlend = true;

                if (m.HasProperty("_BlinkLightColor")) hasBlinkColor = true;
                if (m.HasProperty("_ColorPower")) hasColorPower = true;
                if (m.HasProperty("_MulColor0")) hasMul0 = true;
                if (m.HasProperty("_MulColor1")) hasMul1 = true;
                if (m.HasProperty("_ColorPowerMultiply")) hasMultiply = true;
                if (m.HasProperty("_SrcBlend")) hasSrcBlend = true;
                if (m.HasProperty("_DstBlend")) hasDstBlend = true;
            }

            if (!isBlinkSimple && hasBlinkColor && hasColorPower)
                isBlinkSimple = true;

            if (!isUvAlphaMask && hasMul0 && hasMul1 && hasColorPower && hasMultiply)
                isUvAlphaMask = true;

            if (!isLightAdd1 && !isUvAlphaMask && hasMul0 && hasMul1 && hasColorPower)
                isLightAdd1 = true;

            if (!isLightBlinkBlend &&
                hasColorPower &&
                hasSrcBlend &&
                hasDstBlend &&
                (hasBlinkColor || (hasMul0 && hasMul1)))
            {
                isLightBlinkBlend = true;
            }

            hasColorPowerMultiply = hasMultiply;
        }

        private static float GetOfficialStyleRatio01(int index, int keyIndex)
        {
            return GetDeterministicRatio01(index, keyIndex);
        }

        private static float GetDeterministicRatio01(int a, int b)
        {
            unchecked
            {
                uint x = 2166136261u;
                x = (x ^ (uint)(a + 1)) * 16777619u;
                x = (x ^ (uint)(b + 1)) * 16777619u;
                x ^= x >> 13;
                x *= 1274126177u;
                x ^= x >> 16;
                return (x & 0x00FFFFFFu) / 16777215.0f;
            }
        }

        private static Color GetBlinkColor(Color color0, Color color1, float hueRatio, float saturationRatio, float valueRatio, bool isReverseHue)
        {
            // Native BlinkLightController.GetColor 0x1a40ac0 compares RGBA,
            // interpolates hue directly (reverse subtracts one from h1), and requests HDR output.
            const float colorCompareEpsilon = 1e-10f;
            Vector4 a = color0;
            Vector4 b = color1;
            bool firstWhite = (a - Vector4.one).sqrMagnitude < colorCompareEpsilon;
            if ((a - b).sqrMagnitude < colorCompareEpsilon && (!isReverseHue || firstWhite))
                return color0;
            Color.RGBToHSV(color0, out float h0, out float s0, out float v0);
            Color.RGBToHSV(color1, out float h1, out float s1, out float v1);
            if (firstWhite) h0 = h1;
            if ((b - Vector4.one).sqrMagnitude < colorCompareEpsilon) h1 = h0;
            if (isReverseHue) h1 -= 1f;
            float hue = Mathf.Lerp(h0, h1, hueRatio);
            if (hue < 0f) hue += 1f;
            return Color.HSVToRGB(hue, Mathf.Lerp(s0, s1, saturationRatio),
                Mathf.Lerp(v0, v1, valueRatio), true);
        }

        private static int PositiveMod(int a, int b)
        {
            if (b <= 0) return 0;
            int m = a % b;
            return (m < 0) ? (m + b) : m;
        }

        private static void ApplyBlinkColors(BlinkRootRuntime rt, BlinkLightUpdateInfo u)
        {
            if (u.color0Array != null && u.color0Array.Length > 0)
            {
                CopyColorsKeepPrevious(rt.color0, u.color0Array);
            }

            if (u.color1Array != null && u.color1Array.Length > 0)
            {
                CopyColorsKeepPrevious(rt.color1, u.color1Array);
            }
            else if (IsColorArrayUninitialized(rt.color1))
            {
                for (int i = 0; i < kMaxBlinkSlots; i++)
                    rt.color1[i] = rt.color0[i];
            }
        }

        private static void CopyColorsKeepPrevious(Color[] dst, Color[] src)
        {
            if (dst == null || dst.Length == 0) return;
            if (src == null || src.Length == 0) return;

            bool wasUninitialized = IsColorArrayUninitialized(dst);
            int count = Mathf.Min(dst.Length, src.Length);

            for (int i = 0; i < count; i++)
                dst[i] = src[i];

            if (wasUninitialized && count > 0)
            {
                Color last = dst[count - 1];
                for (int i = count; i < dst.Length; i++)
                    dst[i] = last;
            }
        }

        private static bool IsColorArrayUninitialized(Color[] arr)
        {
            if (arr == null || arr.Length == 0) return true;

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i].r != 0f || arr[i].g != 0f || arr[i].b != 0f || arr[i].a != 0f)
                    return false;
            }
            return true;
        }

        private static void CopyReverseHue(bool[] dst, bool[] src)
        {
            bool last = false;

            for (int i = 0; i < kMaxBlinkSlots; i++)
            {
                if (src != null && i < src.Length)
                    last = src[i];

                dst[i] = last;
            }
        }

        private static Color SafeColor(Color[] arr, int idx, Color fallback)
        {
            if (arr == null || arr.Length == 0) return fallback;
            idx = Mathf.Clamp(idx, 0, arr.Length - 1);
            return arr[idx];
        }

        private static float PickFloat(float[] arr, int idx, float fallback)
        {
            if (arr == null || arr.Length == 0) return fallback;
            idx = Mathf.Clamp(idx, 0, arr.Length - 1);
            return arr[idx];
        }

        private static string NormalizeRuntimeRootName(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey)) return runtimeKey;
            int split = runtimeKey.IndexOf("__U", StringComparison.Ordinal);
            return split >= 0 ? runtimeKey.Substring(0, split) : runtimeKey;
        }

        public bool TryGetCurrentBlinkColor(string rootName, int rootNameHash, int containerIndex, out Color color, out float colorPower)
        {
            color = Color.black;
            colorPower = 0f;

            if (containerIndex < 0)
                return false;

            foreach (var kv in _runtime)
            {
                string candidateName = NormalizeRuntimeRootName(kv.Key);
                if (string.IsNullOrEmpty(candidateName))
                    continue;

                bool matched = false;
                if (!string.IsNullOrEmpty(rootName) && string.Equals(candidateName, rootName, StringComparison.Ordinal))
                    matched = true;
                else if (rootNameHash != 0 && Animator.StringToHash(candidateName) == rootNameHash)
                    matched = true;

                if (!matched)
                    continue;

                var rt = kv.Value;
                if (rt == null || rt.currentColors == null || containerIndex >= rt.currentColors.Length)
                    continue;

                Vector4 slot = rt.currentColors[containerIndex];
                color = new Color(slot.x, slot.y, slot.z, 1f);
                colorPower = slot.w;
                return true;
            }

            return false;
        }

    }
}