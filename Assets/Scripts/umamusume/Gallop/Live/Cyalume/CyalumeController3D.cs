using System;
using System.Collections;
using System.Collections.Generic;
using Gallop.Cyalume;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live.Cyalume
{
    [DisallowMultipleComponent]
    public class CyalumeController3D : CyalumeControllerBase
    {
        [SerializeField] private bool _initializedObjects;
        [SerializeField] private bool _autoSetupOnStart;
        [SerializeField] private float _forceReplaceWarmupSeconds = 1.5f;

        private readonly List<Renderer> _defaultRenderers = new List<Renderer>();
        private readonly List<Renderer> _randomRenderers = new List<Renderer>();

        private GameObject _defaultInstance;
        private GameObject _randomInstance;
        private bool _usingRandomTarget;
        private Coroutine _setupCoroutine;
        private Coroutine _forceReplaceCoroutine;

        [Header("Mob")]
        [SerializeField] private bool _enableMobController = true;
        [SerializeField] private string _mobPrefabName = "mob";
        [SerializeField] private GameObject _mobRootOverride;
        [SerializeField] private Color _mobColor = Color.white;
        [SerializeField] private Color _mobAmbientColor = Color.white;
        [SerializeField] private bool _mobVerboseLog;

        private GameObject _mobInstance;
        private MobShadowController _mobShadowController;
        private LiveTimelineControl _mobTimelineControl;
        private readonly Vector3[] _mobPositions = new Vector3[11];
        private readonly Quaternion[] _mobRotations = new Quaternion[11];
        private readonly Vector3[] _mobScales = new Vector3[11];
        private readonly bool[] _mobDirtySlots = new bool[11];
        private bool _mobHasDirty;

        public bool UsingRandomTarget => _usingRandomTarget;

        protected override void Awake()
        {
            base.Awake();
            if (_autoSetupOnStart)
                InitializeCyalumeObjectsOnly();
        }

        private void Start()
        {
            if (_autoSetupOnStart)
                StartOfficialLikeSetup();
        }

        private void OnEnable()
        {
            TryBindGroupTimeline();
            MarkGroupMatrixDirty();
        }

        private void OnDisable()
        {
            UnbindGroupTimeline();
        }

        public void StartOfficialLikeSetup(bool forceRebuild = false)
        {
            if (_setupCoroutine != null)
            {
                StopCoroutine(_setupCoroutine);
                _setupCoroutine = null;
            }

            _setupCoroutine = StartCoroutine(SetupOfficialLike(forceRebuild));
        }

        public IEnumerator SetupOfficialLike(bool forceRebuild = false)
        {
            ResetRuntimeState();
            InitializeCyalumeObjectsOnly(forceRebuild);

            int musicId = ResolveMusicId();
            if (musicId <= 0)
            {
                Debug.LogWarning("[CyalumeController3D] musicId is unavailable.");
                yield break;
            }

            var provider = ResolvePlaybackProvider();
            provider?.InitializeForMusicId(musicId);

            string liveKey = CyalumePlaybackProvider.BuildPrimaryLiveKey(musicId);
            LoadTextureBundlesFromIndex(liveKey);

            ApplyTargetSelection(false, true);
            EnsureCustomShaderOnCurrentTargets();
            CollectTargetMeshAndMaterials();
            CreateVertexColorCacheOfficialConservative();
            yield return null;

            _textureSet.Clear();
            foreach (var pair in ResolveTexSetFromLoadedBundles(liveKey))
                _textureSet[pair.Key] = pair.Value;
            yield return null;

            InitializeAudienceUvSpreadOfficialConservative();
            RefreshRendererEnabledState();
            _isInitialized = _targetRendererList.Count > 0 && _materialArray.Length > 0;
            ForceReplaceOfficialMaterialsInMemory();
            ForceReplaceCustomShaderInHierarchy(transform, true);
            ForceReplaceOfficialShaderAcrossLoadedScene();
            LogTargetShaderSummary("SetupOfficialLike");
            WriteCyalumeSceneSnapshot("SetupOfficialLike");

            if (_verboseLog)
            {
                Debug.Log($"[CyalumeController3D] Setup complete. liveKey={liveKey}, targets={_targetRendererList.Count}, meshes={_meshFilters.Length}, materials={_materialArray.Length}, patterns={_textureSet.Count}");
            }

            StartForceReplaceWarmup();
            _setupCoroutine = null;
        }

        private void Update()
        {
            TryBindGroupTimeline();

            if (!_isEnabledCyalume || !_initializedObjects || !_isInitialized)
                return;

            if (!TryGetCurrentPlayback(out int patternId, out _, out _, out _, out _))
                return;

            bool preferRandom = _playbackProvider != null && _playbackProvider.IsRandomPattern(patternId);
            bool targetChanged = ApplyTargetSelection(preferRandom);
            bool shaderChanged = EnsureCustomShaderOnCurrentTargets() > 0;
            bool memoryChanged = ForceReplaceOfficialMaterialsInMemory() > 0;
            if (targetChanged)
            {
                ForceReplaceOfficialMaterialsInMemory();
                ForceReplaceCustomShaderInHierarchy(transform, true);
                ForceReplaceOfficialShaderAcrossLoadedScene();
                CollectTargetMeshAndMaterials();
                CreateVertexColorCacheOfficialConservative();
                InitializeAudienceUvSpreadOfficialConservative();
            }
            else if (shaderChanged || memoryChanged)
            {
                CollectTargetMeshAndMaterials();
                ForceReplaceOfficialShaderAcrossLoadedScene();
                LogTargetShaderSummary(memoryChanged ? "ForceReplaceOfficialMaterialsInMemory" : "EnsureCustomShaderOnCurrentTargets");
            }

            UpdateCyalumeOfficialConservative(false);
        }


        private void LateUpdate()
        {
            TryBindGroupTimeline();
            FlushGroupMatrix();
            FlushMobShadowIfNeeded();
        }

        private void StartForceReplaceWarmup()
        {
            if (_forceReplaceCoroutine != null)
            {
                StopCoroutine(_forceReplaceCoroutine);
                _forceReplaceCoroutine = null;
            }

            if (_forceReplaceWarmupSeconds <= 0f)
                return;

            _forceReplaceCoroutine = StartCoroutine(ForceReplaceWarmupCoroutine());
        }

        private IEnumerator ForceReplaceWarmupCoroutine()
        {
            float endTime = Time.unscaledTime + _forceReplaceWarmupSeconds;
            while (Time.unscaledTime < endTime)
            {
                int replacedCount = 0;
                replacedCount += ForceReplaceOfficialMaterialsInMemory();
                replacedCount += ForceReplaceCustomShaderInHierarchy(transform, true);
                replacedCount += ForceReplaceOfficialShaderAcrossLoadedScene();
                if (replacedCount > 0)
                {
                    CollectTargetMeshAndMaterials();
                    LogTargetShaderSummary("ForceReplaceWarmup");
                    WriteCyalumeSceneSnapshot("ForceReplaceWarmup");
                }

                yield return null;
            }

            _forceReplaceCoroutine = null;
        }

        public override void InitializeCyalumeObjectsOnly(bool forceRebuild = false)
        {
            if (_initializedObjects && !forceRebuild)
                return;

            _initializedObjects = false;
            _usingRandomTarget = false;
            _defaultInstance = null;
            _randomInstance = null;

            _allRendererList.Clear();
            _targetRendererList.Clear();
            _defaultRenderers.Clear();
            _randomRenderers.Clear();

            var assetHolder = GetComponent<AssetHolder>();
            if (assetHolder == null || assetHolder._assetTable == null)
            {
                Debug.LogWarning("[CyalumeController3D] AssetHolder or asset table is missing.");
                return;
            }

            DestroyExistingCyalumeChildren();
            TryInstantiateNamedPrefab(assetHolder, "default", out _defaultInstance, _defaultRenderers);
            TryInstantiateNamedPrefab(assetHolder, "random", out _randomInstance, _randomRenderers);
            InitializeMobController(assetHolder);

            _allRendererList.AddRange(_defaultRenderers);
            _allRendererList.AddRange(_randomRenderers);
            RefreshRendererEnabledState();

            _initializedObjects = true;
            ApplyTargetSelection(false, true);
        }

        public override bool ApplyTargetSelection(bool preferRandom, bool forceRefresh = false)
        {
            if (!_initializedObjects)
            {
                InitializeCyalumeObjectsOnly();
                if (!_initializedObjects)
                    return false;
            }

            bool useRandom = preferRandom || _defaultRenderers.Count == 0;
            if (!useRandom && _defaultRenderers.Count == 0 && _randomRenderers.Count > 0)
                useRandom = true;

            bool changed = forceRefresh || _usingRandomTarget != useRandom || _targetRendererList.Count == 0;
            _usingRandomTarget = useRandom;

            _targetRendererList.Clear();
            if (_usingRandomTarget)
                _targetRendererList.AddRange(_randomRenderers.Count > 0 ? _randomRenderers : _defaultRenderers);
            else
                _targetRendererList.AddRange(_defaultRenderers.Count > 0 ? _defaultRenderers : _randomRenderers);

            RefreshRendererEnabledState();

            if (changed && _verboseLog)
            {
                Debug.Log($"[CyalumeController3D] Target selection -> {(_usingRandomTarget ? "random" : "default")}, count={_targetRendererList.Count}");
            }

            return changed;
        }

        public bool ContainsTargetRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            for (int i = 0; i < _targetRendererList.Count; i++)
            {
                if (_targetRendererList[i] == renderer)
                    return true;
            }

            return false;
        }

        public bool ContainsAnyRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            for (int i = 0; i < _allRendererList.Count; i++)
            {
                if (_allRendererList[i] == renderer)
                    return true;
            }

            return false;
        }



        private void TryBindGroupTimeline()
        {
            var director = Gallop.Live.Director.instance;
            if (director == null)
                return;

            var timeline = director._liveTimelineControl;
            if (timeline == null || ReferenceEquals(_mobTimelineControl, timeline))
                return;

            UnbindGroupTimeline();
            _mobTimelineControl = timeline;
            _mobTimelineControl.OnUpdateCyalumeControl += OnCyalumeControl;

            if (_enableMobController)
                _mobTimelineControl.OnUpdateMobControl += OnMobControl;
        }

        private void UnbindGroupTimeline()
        {
            if (_mobTimelineControl == null)
                return;

            _mobTimelineControl.OnUpdateCyalumeControl -= OnCyalumeControl;
            _mobTimelineControl.OnUpdateMobControl -= OnMobControl;
            _mobTimelineControl = null;
        }

        private void InitializeMobController(AssetHolder assetHolder)
        {
            _mobInstance = null;
            _mobShadowController = null;
            _mobHasDirty = false;
            Array.Clear(_mobDirtySlots, 0, _mobDirtySlots.Length);

            if (!_enableMobController)
                return;

            GameObject mobRoot = _mobRootOverride;
            if (!mobRoot && assetHolder != null && !string.IsNullOrEmpty(_mobPrefabName))
            {
                TryInstantiateNamedPrefab(assetHolder, _mobPrefabName, out _mobInstance, null);
                mobRoot = _mobInstance;
            }

            if (!mobRoot)
            {
                if (_mobVerboseLog)
                    Debug.LogWarning($"[CyalumeController3D] Mob root not found. prefabName='{_mobPrefabName}'");
                return;
            }

            _mobShadowController = mobRoot.GetComponent<MobShadowController>();
            if (_mobShadowController == null)
                _mobShadowController = mobRoot.AddComponent<MobShadowController>();

            _mobShadowController.Initialize(mobRoot);
            _mobShadowController.SetMobColor(_mobColor);
            _mobShadowController.SetAmbientColor(_mobAmbientColor);

            if (_mobVerboseLog)
                Debug.Log($"[CyalumeController3D] Mob controller initialized on '{mobRoot.name}'.");
        }

        private void OnCyalumeControl(ref MobCyalumeUpdateInfo info)
        {
            int index = ResolveGroupIndex(info);
            if ((uint)index >= 11u)
                return;

            var position = info.position;
            var rotation = info.rotation;
            var scale = info.scale;
            SetGroupTRS(index, ref position, ref rotation, ref scale);
        }

        private void OnMobControl(ref MobCyalumeUpdateInfo info)
        {
            int index = ResolveGroupIndex(info);
            if ((uint)index >= 11u)
                return;

            _mobPositions[index] = info.position;
            _mobRotations[index] = info.rotation;
            _mobScales[index] = info.scale;
            _mobDirtySlots[index] = true;
            _mobHasDirty = true;
        }

        private static int ResolveGroupIndex(MobCyalumeUpdateInfo info)
        {
            if ((uint)info.unk0 < 11u)
                return info.unk0;

            if (info.data != null && info.data.keys != null && (uint)info.data.keys.unk48 < 11u)
                return info.data.keys.unk48;

            return -1;
        }

        private void FlushMobShadowIfNeeded()
        {
            if (!_mobHasDirty)
                return;

            if (_mobShadowController == null)
            {
                var assetHolder = GetComponent<AssetHolder>();
                InitializeMobController(assetHolder);
                if (_mobShadowController == null)
                    return;
            }

            for (int i = 0; i < _mobDirtySlots.Length; i++)
            {
                if (!_mobDirtySlots[i])
                    continue;

                var position = _mobPositions[i];
                var rotation = _mobRotations[i];
                var scale = _mobScales[i];
                _mobShadowController.SetGroupTRS(i, ref position, ref rotation, ref scale);
                _mobDirtySlots[i] = false;
            }

            _mobShadowController.FlushGroupMatrix();
            _mobHasDirty = false;
        }

        private void DestroyExistingCyalumeChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null)
                    continue;

                string childName = child.name ?? string.Empty;
                bool isKnownCyalumeChild =
                    childName.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                    childName.Equals("random", StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(_mobPrefabName) && childName.Equals(_mobPrefabName, StringComparison.OrdinalIgnoreCase)) ||
                    childName.IndexOf("cyalume", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isKnownCyalumeChild)
                    Destroy(child.gameObject);
            }
        }

        private bool TryInstantiateNamedPrefab(AssetHolder assetHolder, string assetName, out GameObject instance, List<Renderer> destination)
        {
            instance = null;
            destination?.Clear();

            if (!TryResolvePrefab(assetHolder, assetName, out var prefab) || !prefab)
            {
                if (_verboseLog)
                    Debug.LogWarning($"[CyalumeController3D] Missing cyalume prefab: {assetName}");
                return false;
            }

            instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            SetLayerRecursively(instance, gameObject.layer);

            if (destination != null)
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                ApplyCustomShaderOverrideToRenderers(renderers);
                destination.AddRange(renderers);
            }

            if (_verboseLog)
            {
                Debug.Log($"[CyalumeController3D] Spawned cyalume prefab '{assetName}' renderers={destination?.Count ?? 0}");
            }

            return true;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (!root)
                return;

            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child)
                    SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static bool TryResolvePrefab(AssetHolder assetHolder, string assetName, out GameObject prefab)
        {
            prefab = null;
            if (assetHolder == null || assetHolder._assetTable == null || assetHolder._assetTable.list == null)
                return false;

            foreach (var pair in assetHolder._assetTable.list)
            {
                var candidate = pair.Value as GameObject;
                if (!candidate)
                    continue;

                if (candidate.name.Equals(assetName, StringComparison.OrdinalIgnoreCase))
                {
                    prefab = candidate;
                    return true;
                }

                string keyText = pair.Key != null ? pair.Key.ToString() : string.Empty;
                if (!string.IsNullOrEmpty(keyText) && keyText.Equals(assetName, StringComparison.OrdinalIgnoreCase))
                {
                    prefab = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
