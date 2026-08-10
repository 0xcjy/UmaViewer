using System;
using System.Collections.Generic;
using System.Reflection;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live
{
    public class StageMonitorDriver : MonoBehaviour
    {
        [Header("绑定与调试")]
        public bool verboseLog = true;
        public bool includeInactiveRenderers = true;
        public bool rebuildCacheOnEnable = true;
        public bool rebuildCacheWhenTargetMissing = true;
        public bool autoInitializeProvider = true;

        [Header("播放与匹配")]
        public bool assignMaskTextureToFilterTex = false;
        public bool clearFadeTextureWhenUnused = true;
        public bool applyRenderQueue = true;
        public bool applyBlendModeProperties = true;
        public bool autoDiagnosticLog = true;
        public bool showDiagnosticOverlay = true;
        public bool suppressMonitorLightOverlay = false;
        [Range(0f, 1f)] public float monitorLightIntensity = 0.25f;
        [Range(0.25f, 10f)] public float diagnosticIntervalSeconds = 2f;
        public string monitorShaderName = "Gallop/3D/Live/Stage/Monitor";

        [Header("着色器属性名称")]
        public string mainTexProperty = "_MainTex";
        public string filterTexProperty = "_FilterTex";
        public string fadeTexProperty = "_FadeTex";
        public string alphaProperty = "_Alpha";
        public string colorFadeProperty = "_ColorFade";
        public string baseColorProperty = "_BaseColor";
        [Range(0f, 1f)] public float brightnessMultiplier = 1f;
        public string monitorWidthProperty = "_MonitorWidth";
        public string monitorHeightProperty = "_MonitorHeight";
        public string crossFadeRateProperty = "_CrossFadeRate";
        public string srcBlendModeProperty = "_SrcBlendMode";
        public string dstBlendModeProperty = "_DstBlendMode";
        public string srcBlendProperty = "_SrcBlend";
        public string dstBlendProperty = "_DstBlend";

        private sealed class MonitorMaterialBinding
        {
            public Renderer renderer;
            public Material material;
            public int materialIndex;
            public string rendererKey;
            public string materialKey;
            public string rendererCompact;
            public string materialCompact;
            public string groupKey;
            public Vector2 baseFilterScale = Vector2.one;
            public Vector2 baseFilterOffset = Vector2.zero;
            public float baseAlpha = 1f;
            public Color baseColor = Color.white;
            public Color baseColorFade = Color.clear;
            public float baseColorPower = 1f;
            public bool hasSrcBlendMode;
            public float baseSrcBlendMode;
            public bool hasDstBlendMode;
            public float baseDstBlendMode;
            public bool hasAppliedState;
            public MonitorShaderState appliedState;
        }

        private struct MonitorTextureState
        {
            public Texture texture;
            public Texture2D maskTexture;
            public int imageIndex;
            public Vector2 offset;
            public Vector2 scale;
        }

        private struct MonitorShaderState
        {
            public MonitorTextureState main;
            public MonitorTextureState fade;
            public Texture2D filterTexture;
            public float alpha;
            public Color colorFade;
            public Color baseColor;
            public float width;
            public float height;
            public float crossFadeRate;
            public float filterTexScale;
            public int srcBlendMode;
            public int dstBlendMode;
            public int renderQueue;
            public bool hasRenderQueue;
            public bool hasMainTexture;
            public bool hasFadeTexture;
            public bool useBlendMode;
            public bool useBaseColor;
            public bool useMonitorCamera;
        }

        private LiveTimelineControl _ctl;
        private StageController _stage;
        private MonitorUvMovieProvider _provider;
        private StageMonitorCameraDriver _cameraDriver;
        private bool _hasBuiltCache;
        private bool _hasMonitorTimelineData;
        private bool _providerContextReady;
        private int _lastPreparedMusicId = -1;
        private int _lastPreparedStageInstanceId = int.MinValue;
        private float _lastBrightnessMultiplier = float.NaN;
        private float _nextDiagnosticTime;
        private bool _initialDiagnosticDumped;
        private string _statusText = "Monitor: waiting";
        private MaterialPropertyBlock _propertyBlock;

        private readonly List<MonitorMaterialBinding> _bindings = new List<MonitorMaterialBinding>(32);
        private readonly Dictionary<string, List<MonitorMaterialBinding>> _bindingCache =
            new Dictionary<string, List<MonitorMaterialBinding>>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingBindingLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingClipLogged =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _rebuildAttemptedForMissingBinding =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<MonitorMaterialBinding> _resolveBuffer = new List<MonitorMaterialBinding>(8);
        private static readonly List<MonitorMaterialBinding> EmptyBindingList = new List<MonitorMaterialBinding>(0);

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _cameraDriver = GetComponent<StageMonitorCameraDriver>();
            if (_cameraDriver == null)
                _cameraDriver = gameObject.AddComponent<StageMonitorCameraDriver>();
        }

        private void OnEnable()
        {
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            BindIfPossible();
            if (rebuildCacheOnEnable)
                RebuildCache();
        }

        private void OnDisable()
        {
            Unbind();
            ClearCaches();
        }

        private void OnGUI()
        {
            if (!showDiagnosticOverlay || !_hasMonitorTimelineData)
                return;

            int oldDepth = GUI.depth;
            GUI.depth = -100;
            GUI.Label(new Rect(12f, 12f, 760f, 24f), _statusText);
            GUI.depth = oldDepth;
        }

        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F9))
                DumpDiagnostics();

            float clampedBrightness = Mathf.Clamp01(brightnessMultiplier);
            if (!Mathf.Approximately(_lastBrightnessMultiplier, clampedBrightness))
            {
                brightnessMultiplier = clampedBrightness;
                _lastBrightnessMultiplier = clampedBrightness;
                InvalidateAppliedStates();
                if (verboseLog)
                    Debug.Log($"[MonitorDiag] brightness={brightnessMultiplier:F3}, bindings={_bindings.Count}");
            }

            if (_ctl == null || _stage == null || _provider == null)
                BindIfPossible();

            if (_ctl == null || _stage == null || _provider == null || !_hasMonitorTimelineData)
                return;

            if (!EnsureProviderReady())
                return;

            if (!_hasBuiltCache)
                RebuildCache();

            if (_cameraDriver != null)
                _cameraDriver.UpdateCapture(_ctl, _ctl.currentLiveTime * LiveTimelineControl.kTargetFpsF, _ctl.data);

            ApplyMonitorTimeline();
            ApplyMonitorLightOverlay();
        }

        private void BindIfPossible()
        {
            Director dir = Director.instance;
            if (!dir)
                return;

            LiveTimelineControl newCtl = dir._liveTimelineControl;
            StageController newStage = dir._stageController;
            if (newCtl == null || newStage == null)
                return;

            MonitorUvMovieProvider newProvider = GetComponent<MonitorUvMovieProvider>();
            if (newProvider == null)
                newProvider = dir.GetComponent<MonitorUvMovieProvider>();

            if (newProvider == null)
                newProvider = FindObjectOfType<MonitorUvMovieProvider>();

            if (newProvider == null && autoInitializeProvider)
            {
                newProvider = dir.gameObject.GetComponent<MonitorUvMovieProvider>();
                if (newProvider == null)
                    newProvider = dir.gameObject.AddComponent<MonitorUvMovieProvider>();
            }

            bool changed = _ctl != newCtl || _stage != newStage || _provider != newProvider;

            _ctl = newCtl;
            _stage = newStage;
            _provider = newProvider;

            if (!changed)
                return;

            ClearCaches();
            _hasMonitorTimelineData = HasMonitorTimelineData(_ctl);
            _providerContextReady = false;
            _lastPreparedMusicId = -1;
            _lastPreparedStageInstanceId = int.MinValue;

            if (verboseLog)
                Debug.Log($"[StageMonitorDriver] bound, hasMonitorTimelineData={_hasMonitorTimelineData}");
        }

        private void Unbind()
        {
            _ctl = null;
            _stage = null;
            _provider = null;
            _hasMonitorTimelineData = false;
            _providerContextReady = false;
            _lastPreparedMusicId = -1;
            _lastPreparedStageInstanceId = int.MinValue;
        }

        private void ClearCaches()
        {
            _bindings.Clear();
            _bindingCache.Clear();
            _missingBindingLogged.Clear();
            _missingClipLogged.Clear();
            _rebuildAttemptedForMissingBinding.Clear();
            _resolveBuffer.Clear();
            _hasBuiltCache = false;
            _initialDiagnosticDumped = false;
            _nextDiagnosticTime = 0f;
        }

        public void SetBrightnessMultiplier(float value)
        {
            brightnessMultiplier = Mathf.Clamp01(value);
            _lastBrightnessMultiplier = brightnessMultiplier;
            InvalidateAppliedStates();
            if (verboseLog)
                Debug.Log($"[MonitorDiag] brightness set to {brightnessMultiplier:F3}, bindings={_bindings.Count}");
        }

        public void SetMonitorLightIntensity(float value)
        {
            monitorLightIntensity = Mathf.Clamp01(value);
        }

        private void InvalidateAppliedStates()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i] != null)
                    _bindings[i].hasAppliedState = false;
            }
        }

        public void DumpDiagnostics()
        {
            Debug.Log(
                $"[MonitorDiag] dump brightness={brightnessMultiplier:F3}, bindings={_bindings.Count}, " +
                $"timeline={_hasMonitorTimelineData}, providerReady={_providerContextReady}, " +
                $"clips={_provider?.clips?.Count ?? 0}, slots={_provider?.ContextSlotCount ?? 0}");

            int count = Mathf.Min(_bindings.Count, 64);
            for (int i = 0; i < count; i++)
            {
                MonitorMaterialBinding binding = _bindings[i];
                if (binding == null || binding.renderer == null || binding.material == null)
                    continue;

                Material material = binding.material;
                _propertyBlock.Clear();
                binding.renderer.GetPropertyBlock(_propertyBlock, binding.materialIndex);

                Texture mainTexture = GetEffectiveTexture(material, _propertyBlock, mainTexProperty);
                Texture filterTexture = GetEffectiveTexture(material, _propertyBlock, filterTexProperty);
                Texture fadeTexture = GetEffectiveTexture(material, _propertyBlock, fadeTexProperty);
                Color materialFade = TryHasProperty(material, colorFadeProperty)
                    ? material.GetColor(colorFadeProperty) : Color.clear;
                Color blockFade = _propertyBlock.HasProperty(Shader.PropertyToID(colorFadeProperty))
                    ? _propertyBlock.GetColor(colorFadeProperty) : Color.clear;
                float materialAlpha = TryHasProperty(material, alphaProperty)
                    ? material.GetFloat(alphaProperty) : float.NaN;
                float blockAlpha = _propertyBlock.HasProperty(Shader.PropertyToID(alphaProperty))
                    ? _propertyBlock.GetFloat(alphaProperty) : float.NaN;

                Debug.Log(
                    $"[MonitorDiag] #{i} renderer='{GetHierarchyPath(binding.renderer.transform)}' " +
                    $"slot={binding.materialIndex} enabled={binding.renderer.enabled} active={binding.renderer.gameObject.activeInHierarchy} " +
                    $"material='{material.name}' shader='{material.shader?.name}' queue={material.renderQueue} " +
                    $"mpbEmpty={_propertyBlock.isEmpty} main='{DescribeTexture(mainTexture)}' " +
                    $"mainST={DescribeTextureTransform(material, _propertyBlock, mainTexProperty)} " +
                    $"filter='{DescribeTexture(filterTexture)}' filterST={DescribeTextureTransform(material, _propertyBlock, filterTexProperty)} " +
                    $"fade='{DescribeTexture(fadeTexture)}' materialAlpha={materialAlpha:F3} blockAlpha={blockAlpha:F3} " +
                     $"materialColorFade={materialFade} blockColorFade={blockFade}");

                if (material.shader != null && !string.Equals(material.shader.name, monitorShaderName, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log(
                        $"[MonitorDiag] #{i} overlayProps " +
                        $"srcBlend={GetMaterialFloat(material, srcBlendProperty)} dstBlend={GetMaterialFloat(material, dstBlendProperty)} " +
                        $"srcBlendMode={GetMaterialFloat(material, srcBlendModeProperty)} dstBlendMode={GetMaterialFloat(material, dstBlendModeProperty)} " +
                        $"mulColor0={GetMaterialColor(material, "_MulColor0")} colorPower={GetMaterialFloat(material, "_ColorPower")} " +
                        $"baseColor={GetMaterialColor(material, baseColorProperty)}");
                }
            }

            if (_bindings.Count > count)
                Debug.Log($"[MonitorDiag] dump truncated: {_bindings.Count - count} additional bindings");
        }

        private static Texture GetEffectiveTexture(Material material, MaterialPropertyBlock block, string propertyName)
        {
            int propertyId = Shader.PropertyToID(propertyName);
            if (block.HasProperty(propertyId))
                return block.GetTexture(propertyId);
            return HasTextureProperty(material, propertyName) ? material.GetTexture(propertyName) : null;
        }

        private static float GetMaterialFloat(Material material, string propertyName)
        {
            return TryHasProperty(material, propertyName) ? material.GetFloat(propertyName) : float.NaN;
        }

        private static Color GetMaterialColor(Material material, string propertyName)
        {
            return TryHasProperty(material, propertyName) ? material.GetColor(propertyName) : Color.clear;
        }

        private static string DescribeTexture(Texture texture)
        {
            return texture == null ? "<null>" : $"{texture.name} {texture.width}x{texture.height} {texture.graphicsFormat}";
        }

        private static string DescribeTextureTransform(Material material, MaterialPropertyBlock block, string textureProperty)
        {
            int propertyId = Shader.PropertyToID(textureProperty + "_ST");
            Vector4 value;
            if (block.HasProperty(propertyId))
                value = block.GetVector(propertyId);
            else
            {
                Vector2 scale = material.GetTextureScale(textureProperty);
                Vector2 offset = material.GetTextureOffset(textureProperty);
                value = new Vector4(scale.x, scale.y, offset.x, offset.y);
            }
            return value.ToString("F5");
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target != null ? target.name : "<null>";
            for (Transform parent = target != null ? target.parent : null; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }

        private bool EnsureProviderReady()
        {
            if (_provider == null || !autoInitializeProvider)
                return _provider != null;

            int musicId = Director.instance?.live?.MusicId ?? 0;
            if (musicId <= 0)
                return false;

            bool musicChanged = _lastPreparedMusicId != musicId || _provider.LoadedMusicId != musicId;
            bool stageChanged = _stage != null && _lastPreparedStageInstanceId != _stage.GetInstanceID();
            bool alreadyLoaded = _provider.LoadedMusicId == musicId && _provider.HasTriedLoad;
            if (!alreadyLoaded || musicChanged)
            {
                bool forceReload = _provider.LoadedMusicId > 0 && _provider.LoadedMusicId != musicId;
                bool loaded = _provider.InitializeForMusicId(musicId, forceReload);
                _providerContextReady = false;

                if (verboseLog)
                    Debug.Log($"[StageMonitorDriver] provider initialize musicId={musicId}, loaded={loaded}, clips={_provider.clips?.Count ?? 0}");
            }

            _lastPreparedMusicId = musicId;

            bool synced = false;
            if (!_providerContextReady || stageChanged || _provider.ContextSlotCount == 0)
            {
                synced = _provider.RebuildContextSlotsFromLiveSettings(_ctl, musicId, force: stageChanged || musicChanged);
                _providerContextReady = synced || _provider.ContextSlotCount > 0;
            }

            if (_stage != null)
                _lastPreparedStageInstanceId = _stage.GetInstanceID();

            if (verboseLog)
            {
                Debug.Log(
                    $"[StageMonitorDriver] provider ready: " +
                    $"musicId={musicId}, clips={_provider.clips?.Count ?? 0}, " +
                    $"contextSlots={_provider.ContextSlotCount}, synced={synced}, " +
                    $"stage='{_stage?.name}'");
            }

            return _provider.ContextSlotCount > 0;
        }

        private static bool HasMonitorTimelineData(LiveTimelineControl timelineControl)
        {
            if (timelineControl == null || timelineControl.data == null || timelineControl.data.worksheetList == null)
                return false;

            List<LiveTimelineWorkSheet> worksheets = timelineControl.data.worksheetList;
            for (int i = 0; i < worksheets.Count; i++)
            {
                LiveTimelineWorkSheet workSheet = worksheets[i];
                if (workSheet != null && workSheet.monitorControlList != null && workSheet.monitorControlList.Count > 0)
                    return true;
            }

            return false;
        }

        public void RebuildCache()
        {
            _bindings.Clear();
            _bindingCache.Clear();
            _missingBindingLogged.Clear();
            _hasBuiltCache = true;

            if (_stage == null)
                return;

            Renderer[] renderers = _stage.GetComponentsInChildren<Renderer>(includeInactiveRenderers);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] materials;
                try
                {
                    materials = renderer.materials;
                }
                catch (Exception ex)
                {
                    if (verboseLog)
                        Debug.LogWarning($"[StageMonitorDriver] failed to read materials from {renderer.name}: {ex.Message}");
                    continue;
                }

                if (materials == null || materials.Length == 0)
                    continue;

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (!IsMonitorMaterial(material))
                        continue;

                    MonitorMaterialBinding binding = new MonitorMaterialBinding
                    {
                        renderer = renderer,
                        material = material,
                        materialIndex = j,
                        rendererKey = NormalizeName(renderer.name),
                        materialKey = NormalizeName(material.name),
                        rendererCompact = CompactName(renderer.name),
                        materialCompact = CompactName(material.name),
                    };

                    binding.groupKey = BuildGroupKey(binding);

                    if (!string.IsNullOrEmpty(filterTexProperty) && material.HasProperty(filterTexProperty))
                    {
                        binding.baseFilterScale = material.GetTextureScale(filterTexProperty);
                        binding.baseFilterOffset = material.GetTextureOffset(filterTexProperty);
                    }

                    if (TryHasProperty(material, alphaProperty))
                        binding.baseAlpha = material.GetFloat(alphaProperty);
                    if (TryHasProperty(material, colorFadeProperty))
                        binding.baseColorFade = material.GetColor(colorFadeProperty);
                    if (TryHasProperty(material, baseColorProperty))
                        binding.baseColor = material.GetColor(baseColorProperty);
                    if (TryHasProperty(material, "_ColorPower"))
                        binding.baseColorPower = material.GetFloat("_ColorPower");

                    if (TryHasProperty(material, srcBlendModeProperty))
                    {
                        binding.hasSrcBlendMode = true;
                        binding.baseSrcBlendMode = material.GetFloat(srcBlendModeProperty);
                    }
                    else if (TryHasProperty(material, srcBlendProperty))
                    {
                        binding.hasSrcBlendMode = true;
                        binding.baseSrcBlendMode = material.GetFloat(srcBlendProperty);
                    }

                    if (TryHasProperty(material, dstBlendModeProperty))
                    {
                        binding.hasDstBlendMode = true;
                        binding.baseDstBlendMode = material.GetFloat(dstBlendModeProperty);
                    }
                    else if (TryHasProperty(material, dstBlendProperty))
                    {
                        binding.hasDstBlendMode = true;
                        binding.baseDstBlendMode = material.GetFloat(dstBlendProperty);
                    }

                    _bindings.Add(binding);
                }
            }

            _bindings.Sort((a, b) =>
            {
                int cmp = string.Compare(a.groupKey, b.groupKey, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                    return cmp;

                cmp = string.Compare(a.materialKey, b.materialKey, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0)
                    return cmp;

                return string.Compare(a.rendererKey, b.rendererKey, StringComparison.OrdinalIgnoreCase);
            });

            if (verboseLog)
                Debug.Log($"[StageMonitorDriver] cache rebuilt: bindings={_bindings.Count}");
        }

        private void ApplyMonitorLightOverlay()
        {
            float intensity = Mathf.Clamp01(brightnessMultiplier) * Mathf.Clamp01(monitorLightIntensity);
            for (int i = 0; i < _bindings.Count; i++)
            {
                MonitorMaterialBinding binding = _bindings[i];
                if (binding == null || binding.renderer == null || binding.material == null ||
                    !string.Equals(binding.rendererKey, "monitorlight000", StringComparison.OrdinalIgnoreCase))
                    continue;

                binding.renderer.enabled = !suppressMonitorLightOverlay;
                if (suppressMonitorLightOverlay)
                    continue;

                _propertyBlock.Clear();
                binding.renderer.GetPropertyBlock(_propertyBlock, binding.materialIndex);
                TrySetFloat(binding.material, _propertyBlock, "_ColorPower", binding.baseColorPower * intensity);
                binding.renderer.SetPropertyBlock(_propertyBlock, binding.materialIndex);
            }
        }

        private void ApplyMonitorTimeline()
        {
            if (_provider == null)
                return;

            bool hasPool = _provider.clips != null && _provider.clips.Count > 0;
            bool hasSlots = _provider.ContextSlotCount > 0;
            if (!hasPool && !hasSlots)
                return;

            LiveTimelineData data = _ctl.data;
            if (data == null || data.worksheetList == null)
                return;

            float currentLiveTime = _ctl.currentLiveTime;
            float currentFrame = currentLiveTime * LiveTimelineControl.kTargetFpsF;

            List<LiveTimelineWorkSheet> worksheets = data.worksheetList;
            for (int wsIndex = 0; wsIndex < worksheets.Count; wsIndex++)
            {
                LiveTimelineWorkSheet workSheet = worksheets[wsIndex];
                if (workSheet == null || workSheet.monitorControlList == null)
                    continue;

                List<LiveTimelineMonitorControlData> monitorList = workSheet.monitorControlList;
                for (int i = 0; i < monitorList.Count; i++)
                {
                    LiveTimelineMonitorControlData monitorData = monitorList[i];
                    if (monitorData == null || monitorData.keys == null)
                        continue;

                    LiveTimelineKeyMonitorControlDataList keys = monitorData.keys;
                    if (keys.Count <= 0)
                        continue;

                    if (keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable))
                        continue;

                    if (!keys.EnablePlayModeTimeline(_ctl.PlayMode))
                        continue;

                    LiveTimelineControl.FindTimelineKey(
                        out LiveTimelineKey curKeyBase,
                        out LiveTimelineKey nextKeyBase,
                        keys,
                        currentFrame);

                    LiveTimelineKeyMonitorControlData curKey = curKeyBase as LiveTimelineKeyMonitorControlData;
                    if (curKey == null)
                        continue;

                    LiveTimelineKeyMonitorControlData nextKey = nextKeyBase as LiveTimelineKeyMonitorControlData;

                    string bindingName = !string.IsNullOrWhiteSpace(monitorData.SafeName)
                        ? monitorData.SafeName
                        : monitorData.name;

                    if (verboseLog)
                    {
                        Debug.Log(
                            $"[StageMonitorDriver] timeline hit: monitor='{bindingName}', " +
                            $"frame={currentFrame:F2}, curKeyFrame={curKey.frame}, " +
                            $"dispID={curKey.dispID}, dispID2={curKey.DispID2}, " +
                            $"label='{curKey.outputTextureLabel}', speed={curKey.speed}, " +
                            $"playStartOffsetFrame={curKey.playStartOffsetFrame}, lightImageNo={curKey.LightImageNo}");
                    }

                    if (!TryBuildShaderState(monitorData, curKey, nextKey, currentFrame, out MonitorShaderState state))
                    {
                        if (verboseLog)
                        {
                            Debug.LogWarning(
                                $"[StageMonitorDriver] TryBuildShaderState failed: monitor='{bindingName}', " +
                                $"dispID={curKey.dispID}, contextSlots={_provider?.ContextSlotCount ?? 0}, " +
                                $"clips={_provider?.clips?.Count ?? 0}");
                        }
                        continue;
                    }

                    List<MonitorMaterialBinding> targets = ResolveBindings(bindingName);

                    if (verboseLog)
                    {
                        Debug.Log($"[StageMonitorDriver] resolved bindings: monitor='{bindingName}', targets={targets.Count}");
                    }

                    if (targets.Count == 0)
                    {
                        string timelineName = string.IsNullOrWhiteSpace(bindingName) ? "<unnamed>" : bindingName;

                        if (rebuildCacheWhenTargetMissing &&
                            _bindings.Count > 0 &&
                            _rebuildAttemptedForMissingBinding.Add(timelineName))
                        {
                            RebuildCache();
                            targets = ResolveBindings(bindingName);
                        }

                        if (targets.Count == 0)
                        {
                            if (_missingBindingLogged.Add(timelineName) && verboseLog)
                                Debug.LogWarning($"[StageMonitorDriver] no monitor material matched timeline '{timelineName}'");
                            continue;
                        }
                    }

                    for (int j = 0; j < targets.Count; j++)
                        ApplyShaderState(targets[j], state);

                    string mode = state.useMonitorCamera
                        ? (_cameraDriver != null && _cameraDriver.Texture != null ? "CAMERA" : "CAMERA/UNRESOLVED")
                        : (state.hasMainTexture ? "REC" : "CAMERA/UNRESOLVED");
                    string textureName = state.hasMainTexture && state.main.texture != null
                        ? state.main.texture.name : "<none>";
                    _statusText =
                        $"Monitor: {mode}  dispID={curKey.dispID}  tex={textureName}  " +
                        $"targets={targets.Count}  brightness={brightnessMultiplier:F2}";

                    if (autoDiagnosticLog && Time.unscaledTime >= _nextDiagnosticTime)
                    {
                        _nextDiagnosticTime = Time.unscaledTime + Mathf.Max(0.25f, diagnosticIntervalSeconds);
                        Debug.Log(
                            $"[MonitorDiag] auto mode={mode}, monitor='{bindingName}', dispID={curKey.dispID}, " +
                            $"dispID2={curKey.DispID2}, main='{textureName}', mainST={state.main.scale}/{state.main.offset}, " +
                            $"fade={(state.hasFadeTexture ? state.fade.texture?.name : "<none>")}, " +
                            $"alpha={state.alpha:F3}, colorFade={state.colorFade}, brightness={brightnessMultiplier:F3}, " +
                            $"targets={targets.Count}");

                        if (!_initialDiagnosticDumped)
                        {
                            _initialDiagnosticDumped = true;
                            DumpDiagnostics();
                        }
                    }
                }
            }
        }

        private bool TryBuildShaderState(
            LiveTimelineMonitorControlData monitorData,
            LiveTimelineKeyMonitorControlData curKey,
            LiveTimelineKeyMonitorControlData nextKey,
            float currentFrame,
            out MonitorShaderState state)
        {
            state = default;

            bool canInterpolate = nextKey != null && nextKey.interpolateType != LiveCameraInterpolateType.None;
            float ratio = canInterpolate
                ? LiveTimelineControl.CalculateInterpolationValue(curKey, nextKey, currentFrame)
                : 0f;

            Vector2 size = canInterpolate
                ? Vector2.Lerp(curKey.size, nextKey.size, ratio)
                : curKey.size;

            float blendFactor = canInterpolate
                ? Mathf.Lerp(curKey.blendFactor, nextKey.blendFactor, ratio)
                : curKey.blendFactor;

            Color colorFade = canInterpolate
                ? Color.Lerp(curKey.colorFade, nextKey.colorFade, ratio)
                : curKey.colorFade;

            Color baseColor = canInterpolate
                ? Color.Lerp(curKey.BaseColor, nextKey.BaseColor, ratio)
                : curKey.BaseColor;

            float crossFadeRate = canInterpolate
                ? Mathf.Lerp(curKey.CrossFadeRate, nextKey.CrossFadeRate, ratio)
                : curKey.CrossFadeRate;

            float filterTexScale = canInterpolate
                ? Mathf.Lerp(curKey.FilterTexScale, nextKey.FilterTexScale, ratio)
                : curKey.FilterTexScale;

            float localTime = (currentFrame - curKey.frame) * LiveTimelineControl.kFrameToSec;

            bool isReversePlay = curKey.IsReversePlayFlag();
            if (curKey.speed < 0f)
                isReversePlay = !isReversePlay;

            float playbackSpeed = Mathf.Abs(curKey.speed);
            if (playbackSpeed <= 0f)
                playbackSpeed = 1f;

            MonitorUvMovieContextSlot primarySlot = ResolvePrimarySlot(monitorData, curKey);
            MonitorUvMovieContextSlot fadeSlot = ResolveFadeSlot(monitorData, curKey);
            state.useMonitorCamera = curKey.IsMonitorCameraFlag() && _cameraDriver != null && _cameraDriver.IsActive;

            if (primarySlot != null && primarySlot.clip != null &&
                TryBuildTextureState(primarySlot, primarySlot.clip, localTime, playbackSpeed, isReversePlay, curKey.playStartOffsetFrame, curKey.LightImageNo, out MonitorTextureState mainTexture))
            {
                state.main = mainTexture;
                state.hasMainTexture = true;
            }

            if (fadeSlot != null && fadeSlot.clip != null &&
                TryBuildTextureState(fadeSlot, fadeSlot.clip, localTime, playbackSpeed, isReversePlay, curKey.playStartOffsetFrame, curKey.LightImageNo2, out MonitorTextureState fadeTexture))
            {
                state.fade = fadeTexture;
                state.hasFadeTexture = true;
            }

            if (state.useMonitorCamera && _cameraDriver != null && _cameraDriver.Texture != null)
            {
                state.main.texture = _cameraDriver.Texture;
                state.main.scale = Vector2.one;
                state.main.offset = Vector2.zero;
                state.hasMainTexture = true;
                state.hasFadeTexture = false;
            }

            if (assignMaskTextureToFilterTex && state.hasMainTexture)
                state.filterTexture = state.main.maskTexture;

            // _Alpha only cross-fades _MainTex into _FadeTex. The shader's default
            // fade texture is white, so keep it at zero when no second clip exists.
            state.alpha = state.hasFadeTexture ? Mathf.Clamp01(crossFadeRate) : 0f;
            state.colorFade = colorFade;
            state.useBaseColor = !IsColorEffectivelyClear(baseColor);
            state.baseColor = state.useBaseColor ? baseColor : Color.white;
            state.width = size.x;
            state.height = size.y;
            state.crossFadeRate = crossFadeRate;
            state.filterTexScale = Mathf.Max(0.0001f, filterTexScale <= 0f ? 1f : filterTexScale);
            state.srcBlendMode = curKey.SrcBlendMode;
            state.dstBlendMode = curKey.DstBlendMode;
            state.useBlendMode = curKey.IsEnabledBlendMode;
            state.renderQueue = curKey.RenderQueueNo;
            state.hasRenderQueue = curKey.IsRenderQueue != 0;

            return state.hasMainTexture;
        }

        private MonitorUvMovieContextSlot ResolvePrimarySlot(
            LiveTimelineMonitorControlData monitorData,
            LiveTimelineKeyMonitorControlData key)
        {
            if (_provider == null || key == null)
                return null;

            int originalDispId = key.dispID;
            int effectiveDispId = ResolveEffectivePrimaryDispId(key);
            if (TryGetPlayableContextSlot(effectiveDispId, out MonitorUvMovieContextSlot slot))
                return slot;

            if (effectiveDispId != originalDispId && TryGetPlayableContextSlot(originalDispId, out slot))
                return slot;

            if ((effectiveDispId > 0 || originalDispId > 0) && TryGetFallbackContextSlot(out slot))
                return slot;

            string timelineName = monitorData != null
                ? (!string.IsNullOrWhiteSpace(monitorData.SafeName) ? monitorData.SafeName : monitorData.name)
                : "<unnamed>";
            string slotName = _provider.GetSlotDebugName(effectiveDispId);
            string missKey = $"{timelineName}|primary|slotId={effectiveDispId}";
            if (_missingClipLogged.Add(missKey) && verboseLog)
            {
                Debug.LogWarning($"[StageMonitorDriver] primary official slot not found for '{timelineName}' (slotId={effectiveDispId}, slotName='{slotName}')");
            }

            return null;
        }

        private int ResolveEffectivePrimaryDispId(LiveTimelineKeyMonitorControlData key)
        {
            if (key == null)
                return -1;

            int dispId = key.dispID;
            if (key.ChangeUVSettingArray == null || key.ChangeUVSettingArray.Length == 0)
                return dispId;

            for (int i = 0; i < key.ChangeUVSettingArray.Length; i++)
            {
                LiveTimelineMonitorChangeUVSetting change = key.ChangeUVSettingArray[i];
                if (change == null || !change.IsEnabled || change.DispID <= 0)
                    continue;

                if (DoesChangeConditionMatchCurrentCharacters(change.ConditionArray))
                    return change.DispID;
            }

            return dispId;
        }

        private MonitorUvMovieContextSlot ResolveFadeSlot(
            LiveTimelineMonitorControlData monitorData,
            LiveTimelineKeyMonitorControlData key)
        {
            if (_provider == null || key == null || key.DispID2 <= 0)
                return null;

            if (TryGetPlayableContextSlot(key.DispID2, out MonitorUvMovieContextSlot slot))
                return slot;

            if (TryGetFallbackContextSlot(out slot))
                return slot;

            string timelineName = monitorData != null
                ? (!string.IsNullOrWhiteSpace(monitorData.SafeName) ? monitorData.SafeName : monitorData.name)
                : "<unnamed>";
            string slotName = _provider.GetSlotDebugName(key.DispID2);
            string missKey = $"{timelineName}|fade|slotId={key.DispID2}";
            if (_missingClipLogged.Add(missKey) && verboseLog)
            {
                Debug.LogWarning($"[StageMonitorDriver] fade slot not found for '{timelineName}' (slotId={key.DispID2}, slotName='{slotName}')");
            }

            return null;
        }

        private bool TryGetPlayableContextSlot(int slotId, out MonitorUvMovieContextSlot slot)
        {
            slot = null;
            if (_provider == null || slotId <= 0)
                return false;

            if (!_provider.TryGetContextSlot(slotId, out slot) || slot == null)
                return false;

            return slot.isEnabledLoad && slot.clip != null;
        }

        private bool TryGetFallbackContextSlot(out MonitorUvMovieContextSlot slot)
        {
            return TryGetPlayableContextSlot(1, out slot);
        }

        private bool DoesChangeConditionMatchCurrentCharacters(LiveTimelineMonitorDressCondition[] conditions)
        {
            if (conditions == null || conditions.Length == 0)
                return true;

            for (int i = 0; i < conditions.Length; i++)
            {
                LiveTimelineMonitorDressCondition condition = conditions[i];
                if (condition == null || !condition.IsEnabled)
                    continue;

                if (!DoesSingleConditionMatchCurrentCharacters(condition))
                    return false;
            }

            return true;
        }

        private bool DoesSingleConditionMatchCurrentCharacters(LiveTimelineMonitorDressCondition condition)
        {
            if (condition == null || !condition.IsEnabled)
                return true;

            Director director = Director.instance;
            if (director == null || director.CharaContainerScript == null || director.CharaContainerScript.Count == 0)
                return false;

            for (int i = 0; i < director.CharaContainerScript.Count; i++)
            {
                UmaContainerCharacter container = director.CharaContainerScript[i];
                if (container == null)
                    continue;

                int charaId = GetContainerCharaId(container);
                int dressId = GetContainerDressId(container);
                bool charaMatched = condition.CharaId <= 0 || condition.CharaId == charaId;
                bool dressMatched = condition.DressId <= 0 || condition.DressId == dressId;
                if (charaMatched && dressMatched)
                    return true;
            }

            return false;
        }

        private static int GetContainerCharaId(UmaContainerCharacter container)
        {
            if (container == null)
                return 0;

            if (container.CharaEntry != null && container.CharaEntry.Id > 0)
                return container.CharaEntry.Id;

            if (container.CharaData != null)
            {
                try
                {
                    object idValue = container.CharaData["id"];
                    if (idValue != null && int.TryParse(idValue.ToString(), out int charaId))
                        return charaId;
                }
                catch
                {
                }
            }

            return 0;
        }

        private static int GetContainerDressId(UmaContainerCharacter container)
        {
            if (container == null)
                return 0;

            if (TryParseDressIdPrefix(container.VarCostumeIdLong, out int dressId))
                return dressId;
            if (TryParseDressIdPrefix(container.VarCostumeIdShort, out dressId))
                return dressId;

            return 0;
        }

        private static bool TryParseDressIdPrefix(string costumeId, out int dressId)
        {
            dressId = 0;
            if (string.IsNullOrWhiteSpace(costumeId))
                return false;

            string[] parts = costumeId.Split('_');
            if (parts.Length == 0)
                return false;

            return int.TryParse(parts[0], out dressId);
        }

        private bool TryBuildTextureState(MonitorUvMovieContextSlot slot,MonitorUvMovieClipData clip,float localTime,float playbackSpeed,bool isReversePlay,
            int startOffsetFrame,int lightImageNo,out MonitorTextureState state)
        {
            state = default;
            if (clip == null)
                return false;

            if (slot != null && !slot.useStandardMode && clip.lightTexture != null)
            {
                state.texture = clip.lightTexture;
                state.maskTexture = clip.lightMaskTexture;
                state.imageIndex = Mathf.Max(0, lightImageNo);
                state.offset = clip.texturePixelOffset;
                state.scale = Vector2.one - clip.texturePixelScale;
                return true;
            }

            if (clip.metadata == null)
                return false;

            MonitorUvMovieFrameInfo frameInfo = clip.metadata.FrameInfo ?? new MonitorUvMovieFrameInfo();
            int totalFrameCount = Mathf.Max(clip.FrameCount, 0);
            if (totalFrameCount <= 0)
                return false;

            float fps = Mathf.Max(clip.Fps, 1f);
            float moviePlaySec = totalFrameCount / fps;
            float startOffsetSec = Mathf.Max(0f, frameInfo.StartOffsetSec);
            float startLoopSec = Mathf.Max(0f, frameInfo.StartLoopSec);
            float endLoopSec = frameInfo.EndLoopSec > 0f ? frameInfo.EndLoopSec : moviePlaySec;
            endLoopSec = Mathf.Clamp(endLoopSec, 0f, moviePlaySec);
            if (endLoopSec <= startLoopSec)
                endLoopSec = moviePlaySec;

            float playStartSec = startOffsetFrame > 0 ? startOffsetFrame / fps : 0f;

            float setTime = Mathf.Max(0f, localTime) * Mathf.Max(0f, playbackSpeed);
            if (playStartSec > 0f)
                setTime += playStartSec;
            setTime = Mathf.Max(0f, setTime - startOffsetSec);

            float sampleTime = ResolveSampleTime(setTime, moviePlaySec, frameInfo.IsLoop, startLoopSec, endLoopSec, frameInfo.LoopCount, isReversePlay);

            int imageIndex;
            int atlasIndex;
            Vector2 frameOffset;
            Vector2 frameScale;
            BuildFrameUv(clip, sampleTime, fps, totalFrameCount, out imageIndex, out atlasIndex, out frameOffset, out frameScale);

            if (!clip.TryGetFrameTexture(imageIndex, out Texture2D texture) || texture == null)
                return false;

            clip.TryGetMaskTexture(imageIndex, out Texture2D maskTexture);

            state.texture = texture;
            state.maskTexture = maskTexture;
            state.imageIndex = imageIndex;
            state.offset = frameOffset + clip.texturePixelOffset;
            state.scale = frameScale - clip.texturePixelScale;
            return true;
        }

        private static float ResolveSampleTime(float setTime,float moviePlaySec,bool isLoop,float startLoopSec,float endLoopSec,
            int loopCount,bool isReversePlay)
        {
            float loopStart = startLoopSec;
            float loopEnd = endLoopSec;

            if (isReversePlay)
            {
                loopStart = Mathf.Max(0f, moviePlaySec - endLoopSec);
                loopEnd = Mathf.Max(loopStart, moviePlaySec - startLoopSec);
            }

            float time = setTime;
            if (isLoop)
            {
                float loopLength = Mathf.Max(loopEnd - loopStart, 1f / 60f);
                if (time > loopEnd)
                {
                    float after = time - loopEnd;

                    // ٷ loop λһ helper ûȫʵ
                    // ӽʵֵĽƣ loop յ loop ظ
                    if (loopCount > 0)
                    {
                        float totalLoopSec = loopLength * loopCount;
                        if (after > totalLoopSec)
                            after -= totalLoopSec;
                    }

                    time = loopStart + Mathf.Repeat(after, loopLength);
                }
            }
            else if (time > moviePlaySec)
            {
                time = moviePlaySec;
            }

            return isReversePlay ? Mathf.Max(0f, moviePlaySec - time) : time;
        }

        private static void BuildFrameUv(
            MonitorUvMovieClipData clip,
            float sampleTime,
            float fps,
            int totalFrameCount,
            out int imageIndex,
            out int atlasIndex,
            out Vector2 frameOffset,
            out Vector2 frameScale)
        {
            int frameIndex = Mathf.Clamp((int)(sampleTime * fps), 0, totalFrameCount - 1);

            int framesPerImage = 1;
            int framesPerWidth = 1;
            if (clip.metadata != null)
            {
                framesPerImage = Mathf.Max(clip.metadata.EffectiveFramePerImage, 1);
                framesPerWidth = Mathf.Max(clip.metadata.EffectiveFramePerWidth, 1);
            }

            imageIndex = Mathf.Clamp(frameIndex / framesPerImage, 0, Mathf.Max(clip.frameTextures.Count - 1, 0));
            atlasIndex = Mathf.Clamp(frameIndex - imageIndex * framesPerImage, 0, Mathf.Max(framesPerImage - 1, 0));

            frameScale = Vector2.zero;
            if (clip.metadata != null &&
                clip.metadata.FrameInfo != null &&
                clip.metadata.FrameInfo.Size.x > 0f &&
                clip.metadata.FrameInfo.Size.y > 0f)
            {
                frameScale = clip.metadata.FrameInfo.Size;
            }

            if (frameScale.x <= 0f || frameScale.y <= 0f)
            {
                int rows = Mathf.Max(1, Mathf.CeilToInt((float)framesPerImage / framesPerWidth));
                frameScale = new Vector2(1f / framesPerWidth, 1f / rows);
            }

            int column = atlasIndex % framesPerWidth;
            int row = atlasIndex / framesPerWidth;
            frameOffset = new Vector2(column * frameScale.x, row * frameScale.y);
        }

        private void ApplyShaderState(MonitorMaterialBinding binding, MonitorShaderState state)
        {
            Material material = binding.material;
            Renderer renderer = binding.renderer;
            if (material == null || renderer == null)
                return;

            _propertyBlock.Clear();
            renderer.GetPropertyBlock(_propertyBlock, binding.materialIndex);

            if (state.hasMainTexture && HasTextureProperty(material, mainTexProperty))
            {
                _propertyBlock.SetTexture(mainTexProperty, state.main.texture);
                SetTextureTransform(_propertyBlock, mainTexProperty, state.main.scale, state.main.offset);
            }

            if (HasTextureProperty(material, fadeTexProperty))
            {
                if (state.hasFadeTexture)
                {
                    _propertyBlock.SetTexture(fadeTexProperty, state.fade.texture);
                    SetTextureTransform(_propertyBlock, fadeTexProperty, state.fade.scale, state.fade.offset);
                }
                else if (clearFadeTextureWhenUnused &&
                         (!binding.hasAppliedState || binding.appliedState.hasFadeTexture))
                {
                    material.SetTexture(fadeTexProperty, null);
                    material.SetTextureScale(fadeTexProperty, Vector2.one);
                    material.SetTextureOffset(fadeTexProperty, Vector2.zero);
                }
            }

            if (HasTextureProperty(material, filterTexProperty))
            {
                Vector2 filterScale = binding.baseFilterScale * state.filterTexScale;
                if (assignMaskTextureToFilterTex && state.filterTexture != null)
                    _propertyBlock.SetTexture(filterTexProperty, state.filterTexture);
                SetTextureTransform(_propertyBlock, filterTexProperty, filterScale, binding.baseFilterOffset);
            }

            float appliedAlpha = state.alpha;
            Color appliedColorFade = ApplyBrightnessToColorFade(state.colorFade);
            Color appliedBaseColor = state.useBaseColor ? state.baseColor : binding.baseColor;

            TrySetFloat(material, _propertyBlock, alphaProperty, appliedAlpha);
            TrySetColor(material, _propertyBlock, colorFadeProperty, appliedColorFade);
            TrySetColor(material, _propertyBlock, baseColorProperty, appliedBaseColor);
            TrySetFloat(material, _propertyBlock, monitorWidthProperty, state.width);
            TrySetFloat(material, _propertyBlock, monitorHeightProperty, state.height);
            TrySetFloat(material, _propertyBlock, crossFadeRateProperty, state.crossFadeRate);

            if (applyBlendModeProperties)
            {
                if (state.useBlendMode)
                {
                    if (!binding.hasAppliedState ||
                        !binding.appliedState.useBlendMode ||
                        binding.appliedState.srcBlendMode != state.srcBlendMode)
                    {
                        if (!TrySetFloat(material, srcBlendModeProperty, state.srcBlendMode))
                            TrySetFloat(material, srcBlendProperty, state.srcBlendMode);
                    }

                    if (!binding.hasAppliedState ||
                        !binding.appliedState.useBlendMode ||
                        binding.appliedState.dstBlendMode != state.dstBlendMode)
                    {
                        if (!TrySetFloat(material, dstBlendModeProperty, state.dstBlendMode))
                            TrySetFloat(material, dstBlendProperty, state.dstBlendMode);
                    }
                }
                else
                {
                    if (binding.hasSrcBlendMode &&
                        (!binding.hasAppliedState ||
                         binding.appliedState.useBlendMode ||
                         binding.appliedState.srcBlendMode != Mathf.RoundToInt(binding.baseSrcBlendMode)))
                    {
                        if (!TrySetFloat(material, srcBlendModeProperty, binding.baseSrcBlendMode))
                            TrySetFloat(material, srcBlendProperty, binding.baseSrcBlendMode);
                    }

                    if (binding.hasDstBlendMode &&
                        (!binding.hasAppliedState ||
                         binding.appliedState.useBlendMode ||
                         binding.appliedState.dstBlendMode != Mathf.RoundToInt(binding.baseDstBlendMode)))
                    {
                        if (!TrySetFloat(material, dstBlendModeProperty, binding.baseDstBlendMode))
                            TrySetFloat(material, dstBlendProperty, binding.baseDstBlendMode);
                    }
                }
            }

            if (applyRenderQueue &&
                state.hasRenderQueue &&
                (!binding.hasAppliedState ||
                 !binding.appliedState.hasRenderQueue ||
                 binding.appliedState.renderQueue != state.renderQueue))
            {
                material.renderQueue = state.renderQueue;
            }

            renderer.SetPropertyBlock(_propertyBlock, binding.materialIndex);

            MonitorShaderState storedState = state;
            storedState.alpha = appliedAlpha;
            storedState.colorFade = appliedColorFade;
            storedState.baseColor = appliedBaseColor;
            binding.appliedState = storedState;
            binding.hasAppliedState = true;
        }

        private List<MonitorMaterialBinding> ResolveBindings(string timelineName)
        {
            string normalized = NormalizeName(timelineName);
            if (string.IsNullOrEmpty(normalized))
                return EmptyBindingList;

            if (_bindingCache.TryGetValue(normalized, out List<MonitorMaterialBinding> cached))
                return cached;

            _resolveBuffer.Clear();
            string compact = CompactName(normalized);

            AddMatchesExact(normalized, compact, _resolveBuffer);
            if (_resolveBuffer.Count == 0)
                AddMatchesContains(normalized, compact, _resolveBuffer);

            if (_resolveBuffer.Count == 0 && TryExtractMonitorIndex(normalized, out int numericIndex))
                AddMatchesByNumericIndex(numericIndex, _resolveBuffer);

            if (_resolveBuffer.Count == 0 && TryExtractMonitorLetterIndex(normalized, out int letterIndex))
                AddMatchesByOrdinal(letterIndex, _resolveBuffer);

            List<MonitorMaterialBinding> resolved = new List<MonitorMaterialBinding>(_resolveBuffer.Count);
            for (int i = 0; i < _resolveBuffer.Count; i++)
            {
                MonitorMaterialBinding binding = _resolveBuffer[i];
                if (binding == null || resolved.Contains(binding))
                    continue;

                resolved.Add(binding);
            }

            _bindingCache[normalized] = resolved;
            return resolved;
        }

        private void AddMatchesExact(string normalized, string compact, List<MonitorMaterialBinding> result)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                MonitorMaterialBinding binding = _bindings[i];
                if (binding == null)
                    continue;

                if (binding.materialKey == normalized ||
                    binding.rendererKey == normalized ||
                    binding.materialCompact == compact ||
                    binding.rendererCompact == compact ||
                    binding.groupKey == normalized ||
                    binding.groupKey == compact)
                {
                    result.Add(binding);
                }
            }
        }

        private void AddMatchesContains(string normalized, string compact, List<MonitorMaterialBinding> result)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                MonitorMaterialBinding binding = _bindings[i];
                if (binding == null)
                    continue;

                if (binding.materialKey.Contains(normalized) ||
                    binding.rendererKey.Contains(normalized) ||
                    (!string.IsNullOrEmpty(compact) && binding.materialCompact.Contains(compact)) ||
                    (!string.IsNullOrEmpty(compact) && binding.rendererCompact.Contains(compact)))
                {
                    result.Add(binding);
                }
            }
        }

        private void AddMatchesByNumericIndex(int monitorIndex, List<MonitorMaterialBinding> result)
        {
            string groupKey = $"monitor{monitorIndex:D3}";
            string compactKey = CompactName(groupKey);
            string relaxedKey = $"monitor{monitorIndex}";

            for (int i = 0; i < _bindings.Count; i++)
            {
                MonitorMaterialBinding binding = _bindings[i];
                if (binding == null)
                    continue;

                if (binding.groupKey == groupKey ||
                    binding.materialKey.Contains(groupKey) ||
                    binding.rendererKey.Contains(groupKey) ||
                    binding.materialCompact.Contains(compactKey) ||
                    binding.rendererCompact.Contains(compactKey) ||
                    binding.materialCompact.Contains(relaxedKey) ||
                    binding.rendererCompact.Contains(relaxedKey))
                {
                    result.Add(binding);
                }
            }
        }

        private void AddMatchesByOrdinal(int ordinal, List<MonitorMaterialBinding> result)
        {
            if (ordinal < 0 || _bindings.Count == 0)
                return;

            List<string> groups = new List<string>(_bindings.Count);
            for (int i = 0; i < _bindings.Count; i++)
            {
                string groupKey = _bindings[i]?.groupKey;
                if (string.IsNullOrEmpty(groupKey) || groups.Contains(groupKey))
                    continue;

                groups.Add(groupKey);
            }

            groups.Sort(StringComparer.OrdinalIgnoreCase);
            if (ordinal >= groups.Count)
                return;

            string targetGroup = groups[ordinal];
            for (int i = 0; i < _bindings.Count; i++)
            {
                MonitorMaterialBinding binding = _bindings[i];
                if (binding != null && binding.groupKey == targetGroup)
                    result.Add(binding);
            }
        }

        private bool IsMonitorMaterial(Material material)
        {
            if (material == null)
                return false;

            string shaderName = material.shader != null ? material.shader.name : string.Empty;
            if (!string.IsNullOrEmpty(shaderName) &&
                shaderName.IndexOf(monitorShaderName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string materialName = material.name ?? string.Empty;
            if (materialName.IndexOf("monitor", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            int score = 0;
            if (HasTextureProperty(material, mainTexProperty))
                score++;
            if (HasTextureProperty(material, filterTexProperty))
                score++;
            if (HasTextureProperty(material, fadeTexProperty))
                score++;
            if (TryHasProperty(material, alphaProperty))
                score++;
            if (TryHasProperty(material, colorFadeProperty))
                score++;

            return score >= 3;
        }

        private static bool HasTextureProperty(Material material, string propertyName)
        {
            return material != null &&
                   !string.IsNullOrEmpty(propertyName) &&
                   material.HasProperty(propertyName);
        }

        private static bool TryHasProperty(Material material, string propertyName)
        {
            return material != null &&
                   !string.IsNullOrEmpty(propertyName) &&
                   material.HasProperty(propertyName);
        }

        private static bool TrySetFloat(Material material, string propertyName, float value)
        {
            if (!TryHasProperty(material, propertyName))
                return false;

            material.SetFloat(propertyName, value);
            return true;
        }

        private static bool TrySetColor(Material material, string propertyName, Color value)
        {
            if (!TryHasProperty(material, propertyName))
                return false;

            material.SetColor(propertyName, value);
            return true;
        }

        private static void TrySetFloat(Material material, MaterialPropertyBlock block, string propertyName, float value)
        {
            if (TryHasProperty(material, propertyName))
                block.SetFloat(propertyName, value);
        }

        private static void TrySetColor(Material material, MaterialPropertyBlock block, string propertyName, Color value)
        {
            if (TryHasProperty(material, propertyName))
                block.SetColor(propertyName, value);
        }

        private static void SetTextureTransform(MaterialPropertyBlock block, string textureProperty, Vector2 scale, Vector2 offset)
        {
            block.SetVector(textureProperty + "_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
        }

        private Color ApplyBrightnessToColorFade(Color colorFade)
        {
            float multiplier = Mathf.Clamp01(brightnessMultiplier);
            if (Mathf.Approximately(multiplier, 1f))
                return colorFade;

            // Monitor.shader outputs lerp(ColorFade.rgb, screen.rgb, ColorFade.a) * 2.
            // Re-encode both terms so this scales the full result even when alpha is one.
            float sourceAlpha = Mathf.Clamp01(colorFade.a);
            float scaledAlpha = sourceAlpha * multiplier;
            float remainingWeight = 1f - scaledAlpha;
            float colorScale = remainingWeight > 0.0001f
                ? multiplier * (1f - sourceAlpha) / remainingWeight
                : 0f;

            colorFade.r *= colorScale;
            colorFade.g *= colorScale;
            colorFade.b *= colorScale;
            colorFade.a = scaledAlpha;
            return colorFade;
        }

        private static bool IsColorEffectivelyClear(Color value)
        {
            return value.a <= 0.0001f &&
                   value.r <= 0.0001f &&
                   value.g <= 0.0001f &&
                   value.b <= 0.0001f;
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Approximately(a.x, b.x) && Approximately(a.y, b.y);
        }

        private static bool Approximately(Color a, Color b)
        {
            return Approximately(a.r, b.r) &&
                   Approximately(a.g, b.g) &&
                   Approximately(a.b, b.b) &&
                   Approximately(a.a, b.a);
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = value.Replace("(Instance)", string.Empty);
            value = value.Replace("(Clone)", string.Empty);
            return value.Trim().ToLowerInvariant();
        }

        private static string CompactName(string value)
        {
            string normalized = NormalizeName(value);
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;

            char[] buffer = new char[normalized.Length];
            int count = 0;
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (char.IsLetterOrDigit(c))
                    buffer[count++] = c;
            }

            return count > 0 ? new string(buffer, 0, count) : string.Empty;
        }

        private static string BuildGroupKey(MonitorMaterialBinding binding)
        {
            if (binding == null)
                return string.Empty;

            if (TryExtractMonitorIndex(binding.materialKey, out int materialIndex))
                return $"monitor{materialIndex:D3}";

            if (TryExtractMonitorIndex(binding.rendererKey, out int rendererIndex))
                return $"monitor{rendererIndex:D3}";

            if (!string.IsNullOrEmpty(binding.materialCompact) && binding.materialCompact.Contains("monitor"))
                return binding.materialCompact;

            if (!string.IsNullOrEmpty(binding.rendererCompact) && binding.rendererCompact.Contains("monitor"))
                return binding.rendererCompact;

            return !string.IsNullOrEmpty(binding.materialCompact) ? binding.materialCompact : binding.rendererCompact;
        }

        private static bool TryExtractMonitorIndex(string value, out int index)
        {
            index = -1;
            string compact = CompactName(value);
            if (string.IsNullOrEmpty(compact))
                return false;

            int monitorIndex = compact.IndexOf("monitor", StringComparison.OrdinalIgnoreCase);
            if (monitorIndex < 0)
                return false;

            monitorIndex += "monitor".Length;
            int start = monitorIndex;
            while (monitorIndex < compact.Length && char.IsDigit(compact[monitorIndex]))
                monitorIndex++;

            if (monitorIndex <= start)
                return false;

            return int.TryParse(compact.Substring(start, monitorIndex - start), out index);
        }

        private static bool TryExtractMonitorLetterIndex(string value, out int index)
        {
            index = -1;
            string compact = CompactName(value);
            if (string.IsNullOrEmpty(compact) || !compact.StartsWith("monitor", StringComparison.OrdinalIgnoreCase))
                return false;

            if (compact.Length != "monitor".Length + 1)
                return false;

            char c = compact[compact.Length - 1];
            if (c < 'a' || c > 'z')
                return false;

            index = c - 'a';
            return true;
        }
    }
}
