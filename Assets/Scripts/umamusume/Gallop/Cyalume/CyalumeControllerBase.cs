using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallop.Live;
using Gallop.Live.Cyalume;
using UnityEngine;

namespace Gallop.Cyalume
{

    /// 鎺у埗鍣ㄤ晶鐨勪繚瀹堣繕鍘熷疄鐜般€?    ///
    /// 姝ょ増鏈殑閲嶈瑙勫垯锛?    /// 浠呭湪缃戞牸鍙鏃惰闂?CPU 绔綉鏍兼暟鎹€?    /// 涓嶉€氳繃鍏嬮殕缃戞牸鏉モ€滃己鍒垛€濊幏寰楀彲鍐欐€с€?    /// 涓嶇敓鎴愭帹娴嬬殑椤剁偣棰滆壊鎴?UV 鏁版嵁娴併€?    /// 褰撴棤娉曠‘瀹氬畼鏂圭殑鍑嗙‘琛屼负鏃讹紝閫夋嫨璺宠繃锛岃€屼笉鏄繘琛岀寽娴嬨€?
    public abstract class CyalumeControllerBase : MonoBehaviour
    {
        [Header("Recovered official-ish settings")]
        public int _animationFrameCount = 32;
        public bool _useUVAudienceSpread = true;

        [Header("Runtime shader override")]
        [SerializeField] protected bool _useCustomCyalumeShader = true;
        [SerializeField] protected Shader _customCyalumeShader;
        [SerializeField] protected string _customCyalumeShaderName = "Custom/CyalumeSimple_OfficialLike";
        [SerializeField] protected string _cyalumeNameKeyword = "cyalume";
        [SerializeField] protected string _cyalumeShaderKeyword = "Cyalume";
        [SerializeField] protected string _officialCyalumeShaderName = "Gallop_3D_Live_Cyalume_CyalumeDefault";
        [SerializeField] protected string _officialCyalumeShaderPrefix = "Gallop_3D_Live_Cyalume_";
        [SerializeField] protected bool _writeCyalumeDebugDump = true;
        [SerializeField] protected string _cyalumeDebugDumpFileName = "cyalume_shader_snapshot.txt";
        [SerializeField] protected string _mobShadowShaderKeyword = "MobShadow";

        [SerializeField] protected bool _verboseLog;
        [SerializeField] protected bool _isInitialized;
        [SerializeField] protected bool _isEnabledCyalume = true;
        [SerializeField] protected bool _isEnabledAudience = true;

        private const int GroupMatrixCount = 11;
        private static readonly int CyalumeGroupMatrixPropertyId = Shader.PropertyToID("_CyalumeGroupMatrix");

        protected readonly List<Renderer> _allRendererList = new List<Renderer>();
        protected readonly List<Renderer> _targetRendererList = new List<Renderer>();
        protected readonly Matrix4x4[] _groupMatrixArray = CreateIdentityGroupMatrixArray();
        protected bool _isUpdateGroupMatrix = true;

        protected MeshFilter[] _meshFilters = Array.Empty<MeshFilter>();
        protected Material[] _materialArray = Array.Empty<Material>();
        protected Color32[][][] _colorsTable = Array.Empty<Color32[][]>();

        protected readonly Dictionary<int, Texture2D> _textureSet = new Dictionary<int, Texture2D>();
        protected MaterialPropertyBlock _mpb;
        protected int _mainTexStPropertyId = -1;

        protected CyalumePlaybackProvider _playbackProvider;
        protected int _musicIdOverride;
        protected int _lastAppliedPatternId = -1;
        protected float _lastAppliedScrollOffset = float.NaN;
        protected bool _loggedMissingCustomCyalumeShader;

        protected bool _hasManualPlaybackState;
        protected int _manualPatternId;
        protected float _manualPatternStartTime;
        protected float _manualPlaySpeed = 1f;
        protected int _manualChoreographyType;

        public IReadOnlyList<Renderer> AllRenderers => _allRendererList;
        public IReadOnlyList<Renderer> TargetRenderers => _targetRendererList;
        public bool IsReady => _isInitialized;

        protected virtual void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _mainTexStPropertyId = Shader.PropertyToID("_MainTex_ST");
            _isUpdateGroupMatrix = true;
        }

        private static Matrix4x4[] CreateIdentityGroupMatrixArray()
        {
            var matrices = new Matrix4x4[GroupMatrixCount];
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = Matrix4x4.identity;

            return matrices;
        }

        public void SetGroupTRS(int index, ref Vector3 position, ref Quaternion rotation, ref Vector3 scale)
        {
            // 瀹樻柟涓?Clamp锛涜秺鐣屾椂鐢辨暟缁勮闂洿鎺ユ姏鍑哄紓甯搞€?            _groupMatrixArray[index].SetTRS(position, rotation, scale);
            _isUpdateGroupMatrix = true;
        }

        public void FlushGroupMatrix()
        {
            if (!_isUpdateGroupMatrix || !_isEnabledCyalume)
                return;

            _isUpdateGroupMatrix = false;
            Shader.SetGlobalMatrixArray(CyalumeGroupMatrixPropertyId, _groupMatrixArray);
        }

        protected void MarkGroupMatrixDirty()
        {
            _isUpdateGroupMatrix = true;
        }

        public void SetPlaybackProvider(CyalumePlaybackProvider provider)
        {
            _playbackProvider = provider;
        }

        public void SetVerboseLog(bool enabled)
        {
            _verboseLog = enabled;
        }

        public void SetMusicIdOverride(int musicId)
        {
            _musicIdOverride = Mathf.Max(0, musicId);
        }

        public void SetManualPlaybackState(int patternId, float patternStartTime, float playSpeed, int choreographyType)
        {
            _hasManualPlaybackState = true;
            _manualPatternId = Mathf.Max(0, patternId);
            _manualPatternStartTime = patternStartTime;
            _manualPlaySpeed = playSpeed > 0f ? playSpeed : 1f;
            _manualChoreographyType = choreographyType;
        }

        public void ClearManualPlaybackState()
        {
            _hasManualPlaybackState = false;
        }

        public void SetVisibleCyalume(bool enabled)
        {
            _isEnabledCyalume = enabled;
            if (enabled)
                _isUpdateGroupMatrix = true;

            RefreshRendererEnabledState();
        }

        protected void RefreshRendererEnabledState()
        {
            for (int i = 0; i < _allRendererList.Count; i++)
            {
                var renderer = _allRendererList[i];
                if (!renderer)
                    continue;

                bool shouldEnable = _isEnabledCyalume && _targetRendererList.Contains(renderer);
                renderer.enabled = shouldEnable;
            }
        }

        protected void ResetRuntimeState()
        {
            _textureSet.Clear();
            _meshFilters = Array.Empty<MeshFilter>();
            _materialArray = Array.Empty<Material>();
            _colorsTable = Array.Empty<Color32[][]>();
            _lastAppliedPatternId = -1;
            _lastAppliedScrollOffset = float.NaN;
            _isInitialized = false;
        }

        protected int ResolveMusicId()
        {
            if (_musicIdOverride > 0)
                return _musicIdOverride;

            if (Director.instance != null && Director.instance.live != null)
                return Director.instance.live.MusicId;

            return 0;
        }

        protected CyalumePlaybackProvider ResolvePlaybackProvider()
        {
            if (_playbackProvider != null)
                return _playbackProvider;

            _playbackProvider = GetComponent<CyalumePlaybackProvider>();
            if (_playbackProvider == null)
                _playbackProvider = FindObjectOfType<CyalumePlaybackProvider>(true);
            if (_playbackProvider == null)
                _playbackProvider = gameObject.AddComponent<CyalumePlaybackProvider>();

            return _playbackProvider;
        }

        protected Shader ResolveCustomCyalumeShader()
        {
            if (!_useCustomCyalumeShader)
                return null;

            if (_customCyalumeShader == null && !string.IsNullOrEmpty(_customCyalumeShaderName))
                _customCyalumeShader = Shader.Find(_customCyalumeShaderName);

            if (_customCyalumeShader == null && !_loggedMissingCustomCyalumeShader)
            {
                Debug.LogWarning($"[CyalumeControllerBase] Custom cyalume shader not found: {_customCyalumeShaderName}");
                _loggedMissingCustomCyalumeShader = true;
            }

            return _customCyalumeShader;
        }

        protected void ApplyCustomShaderOverrideToRenderers(IList<Renderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
                return;

            var replacementShader = ResolveCustomCyalumeShader();
            if (replacementShader == null)
                return;

            int replacedCount = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                replacedCount += ApplyCustomShaderOverrideToRenderer(renderer, replacementShader);
            }

            if (_verboseLog)
            {
                Debug.Log($"[CyalumeControllerBase] Shader override pass finished. targetRenderers={renderers.Count}, replacedMaterials={replacedCount}, replacementShader={replacementShader.name}");
            }
        }

        protected int ApplyCustomShaderOverrideToRenderer(Renderer renderer, Shader replacementShader)
        {
            if (renderer == null || replacementShader == null)
                return 0;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return 0;

            bool changed = false;
            var replacedMaterials = new Material[materials.Length];
            int replacedCount = 0;

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null)
                {
                    replacedMaterials[i] = null;
                    continue;
                }

                if (!IsOfficialCyalumeMaterial(material))
                {
                    replacedMaterials[i] = material;
                    continue;
                }

                if (material.shader == replacementShader)
                {
                    replacedMaterials[i] = material;
                    continue;
                }

                string previousShaderName = material.shader != null ? material.shader.name : "<null>";
                replacedMaterials[i] = CreateReplacementCyalumeMaterial(material, replacementShader);
                changed = true;
                replacedCount++;

                if (_verboseLog)
                {
                    Debug.Log($"[CyalumeControllerBase] Replaced material on {renderer.name}/{material.name}: {previousShaderName} -> {replacementShader.name}");
                }
            }

            if (changed)
                renderer.sharedMaterials = replacedMaterials;

            return replacedCount;
        }

        protected Material CreateReplacementCyalumeMaterial(Material sourceMaterial, Shader replacementShader)
        {
            var replacementMaterial = new Material(replacementShader)
            {
                name = sourceMaterial != null ? $"{sourceMaterial.name}_CustomCyalume" : "Cyalume_Custom"
            };

            string sourceTextureProperty = ResolveTextureProperty(sourceMaterial);
            Texture sourceTexture = null;
            Vector2 sourceTextureScale = Vector2.one;
            Vector2 sourceTextureOffset = Vector2.zero;
            if (!string.IsNullOrEmpty(sourceTextureProperty) && sourceMaterial != null && sourceMaterial.HasProperty(sourceTextureProperty))
            {
                sourceTexture = sourceMaterial.GetTexture(sourceTextureProperty);
                sourceTextureScale = sourceMaterial.GetTextureScale(sourceTextureProperty);
                sourceTextureOffset = sourceMaterial.GetTextureOffset(sourceTextureProperty);
            }

            Color tint = Color.white;
            if (sourceMaterial != null)
            {
                if (sourceMaterial.HasProperty("_Tint"))
                    tint = sourceMaterial.GetColor("_Tint");
                else if (sourceMaterial.HasProperty("_Color"))
                    tint = sourceMaterial.GetColor("_Color");
            }

            float intensity = sourceMaterial != null && sourceMaterial.HasProperty("_Intensity")
                ? sourceMaterial.GetFloat("_Intensity")
                : 1f;

            if (replacementMaterial.HasProperty("_MainTex"))
            {
                replacementMaterial.SetTexture("_MainTex", sourceTexture);
                replacementMaterial.SetTextureScale("_MainTex", sourceTextureScale);
                replacementMaterial.SetTextureOffset("_MainTex", sourceTextureOffset);
            }

            if (replacementMaterial.HasProperty("_Tint"))
                replacementMaterial.SetColor("_Tint", tint);

            if (replacementMaterial.HasProperty("_Intensity"))
                replacementMaterial.SetFloat("_Intensity", intensity);

            return replacementMaterial;
        }

        protected int EnsureCustomShaderOnCurrentTargets()
        {
            if (_targetRendererList == null || _targetRendererList.Count == 0)
                return 0;

            var replacementShader = ResolveCustomCyalumeShader();
            if (replacementShader == null)
                return 0;

            int replacedCount = 0;
            for (int i = 0; i < _targetRendererList.Count; i++)
            {
                var renderer = _targetRendererList[i];
                if (renderer == null)
                    continue;

                replacedCount += ApplyCustomShaderOverrideToRenderer(renderer, replacementShader);
            }

            if (replacedCount > 0 && _verboseLog)
            {
                Debug.Log($"[CyalumeControllerBase] EnsureCustomShaderOnCurrentTargets reapplied {replacedCount} material slot(s).");
            }

            return replacedCount;
        }

        protected int ForceReplaceCustomShaderInHierarchy(Transform root, bool includeInactive = true)
        {
            if (root == null)
                return 0;

            var replacementShader = ResolveCustomCyalumeShader();
            if (replacementShader == null)
                return 0;

            int replacedCount = 0;
            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!IsLikelyCyalumeRenderer(renderer))
                    continue;

                replacedCount += ApplyCustomShaderOverrideToRenderer(renderer, replacementShader);
            }

            if (replacedCount > 0 && _verboseLog)
            {
                Debug.Log($"[CyalumeControllerBase] ForceReplaceCustomShaderInHierarchy replaced {replacedCount} material slot(s) under '{root.name}'.");
            }

            return replacedCount;
        }

        protected int ForceReplaceOfficialShaderAcrossLoadedScene()
        {
            var replacementShader = ResolveCustomCyalumeShader();
            if (replacementShader == null)
                return 0;

            int replacedCount = 0;
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!IsOfficialCyalumeRenderer(renderer))
                    continue;

                replacedCount += ApplyCustomShaderOverrideToRenderer(renderer, replacementShader);
            }

            if (replacedCount > 0 && _verboseLog)
            {
                Debug.Log($"[CyalumeControllerBase] ForceReplaceOfficialShaderAcrossLoadedScene replaced {replacedCount} material slot(s).");
            }

            return replacedCount;
        }

        protected int ForceReplaceOfficialMaterialsInMemory()
        {
            var replacementShader = ResolveCustomCyalumeShader();
            if (replacementShader == null)
                return 0;

            int replacedCount = 0;
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (!IsOfficialCyalumeMaterial(material))
                    continue;

                if (ApplyCustomShaderOverrideInPlace(material, replacementShader))
                    replacedCount++;
            }

            if (replacedCount > 0 && _verboseLog)
            {
                Debug.Log($"[CyalumeControllerBase] ForceReplaceOfficialMaterialsInMemory replaced {replacedCount} material asset(s).");
            }

            return replacedCount;
        }

        protected bool IsLikelyCyalumeRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            if (_targetRendererList.Contains(renderer) || _allRendererList.Contains(renderer))
                return true;

            if (IsOfficialCyalumeRenderer(renderer))
                return true;

            string rendererName = renderer.name ?? string.Empty;
            if (!string.IsNullOrEmpty(_cyalumeNameKeyword) &&
                rendererName.IndexOf(_cyalumeNameKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return false;

            for (int i = 0; i < materials.Length; i++)
            {
                var material = materials[i];
                if (material == null)
                    continue;

                string materialName = material.name ?? string.Empty;
                if (!string.IsNullOrEmpty(_cyalumeNameKeyword) &&
                    materialName.IndexOf(_cyalumeNameKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                string shaderName = material.shader != null ? material.shader.name : string.Empty;
                if (!string.IsNullOrEmpty(_cyalumeShaderKeyword) &&
                    shaderName.IndexOf(_cyalumeShaderKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        protected bool IsOfficialCyalumeRenderer(Renderer renderer)
        {
            if (renderer == null)
                return false;

            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                return false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (IsOfficialCyalumeMaterial(materials[i]))
                    return true;
            }

            return false;
        }

        protected virtual bool IsMobShadowMaterial(Material material)
        {
            if (material == null)
                return false;

            string keyword = _mobShadowShaderKeyword ?? string.Empty;
            if (string.IsNullOrEmpty(keyword))
                keyword = "MobShadow";

            string shaderName = material.shader != null ? (material.shader.name ?? string.Empty) : string.Empty;
            if (shaderName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string materialName = material.name ?? string.Empty;
            if (materialName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        protected bool IsOfficialCyalumeMaterial(Material material)
        {
            if (material == null || material.shader == null)
                return false;

            if (IsMobShadowMaterial(material))
                return false;

            string shaderName = material.shader.name ?? string.Empty;
            if (!string.IsNullOrEmpty(_officialCyalumeShaderName) &&
                string.Equals(shaderName, _officialCyalumeShaderName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(_officialCyalumeShaderPrefix) &&
                shaderName.StartsWith(_officialCyalumeShaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        protected bool ApplyCustomShaderOverrideInPlace(Material material, Shader replacementShader)
        {
            if (material == null || replacementShader == null || material.shader == replacementShader)
                return false;

            string sourceTextureProperty = ResolveTextureProperty(material);
            Texture sourceTexture = null;
            Vector2 sourceTextureScale = Vector2.one;
            Vector2 sourceTextureOffset = Vector2.zero;
            if (!string.IsNullOrEmpty(sourceTextureProperty) && material.HasProperty(sourceTextureProperty))
            {
                sourceTexture = material.GetTexture(sourceTextureProperty);
                sourceTextureScale = material.GetTextureScale(sourceTextureProperty);
                sourceTextureOffset = material.GetTextureOffset(sourceTextureProperty);
            }

            Color tint = Color.white;
            if (material.HasProperty("_Tint"))
                tint = material.GetColor("_Tint");
            else if (material.HasProperty("_Color"))
                tint = material.GetColor("_Color");

            float intensity = material.HasProperty("_Intensity") ? material.GetFloat("_Intensity") : 1f;
            string previousShaderName = material.shader != null ? material.shader.name : "<null>";

            material.shader = replacementShader;

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", sourceTexture);
                material.SetTextureScale("_MainTex", sourceTextureScale);
                material.SetTextureOffset("_MainTex", sourceTextureOffset);
            }

            if (material.HasProperty("_Tint"))
                material.SetColor("_Tint", tint);

            if (material.HasProperty("_Intensity"))
                material.SetFloat("_Intensity", intensity);

            if (_verboseLog)
            {
                Debug.Log($"[CyalumeControllerBase] Replaced material asset in memory {material.name}: {previousShaderName} -> {replacementShader.name}");
            }

            return true;
        }

        protected void LogTargetShaderSummary(string context)
        {
            if (!_verboseLog)
                return;

            var shaderCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            sb.AppendLine($"[CyalumeControllerBase] {context} target shader summary");

            for (int i = 0; i < _targetRendererList.Count; i++)
            {
                var renderer = _targetRendererList[i];
                if (renderer == null)
                    continue;

                var materials = renderer.materials;
                var shaderNames = new List<string>();
                if (materials != null)
                {
                    for (int j = 0; j < materials.Length; j++)
                    {
                        var material = materials[j];
                        string shaderName = material != null && material.shader != null ? material.shader.name : "<null>";
                        shaderNames.Add(shaderName);

                        if (!shaderCounts.TryGetValue(shaderName, out int count))
                            count = 0;
                        shaderCounts[shaderName] = count + 1;
                    }
                }

                sb.AppendLine($"  renderer={renderer.name}, shaders=[{string.Join(", ", shaderNames)}]");
            }

            if (shaderCounts.Count > 0)
            {
                sb.AppendLine("  uniqueShaders=" + string.Join(", ", shaderCounts.Select(pair => $"{pair.Key} x{pair.Value}")));
            }

            Debug.Log(sb.ToString());
        }

        protected void WriteCyalumeSceneSnapshot(string context)
        {
            if (!_writeCyalumeDebugDump)
                return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"context={context}");
                sb.AppendLine($"time={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"unityTime={Time.time}");

                var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
                int hitCount = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (!IsLikelyCyalumeRenderer(renderer))
                        continue;

                    var materials = renderer.sharedMaterials;
                    var entries = new List<string>();
                    if (materials != null)
                    {
                        for (int j = 0; j < materials.Length; j++)
                        {
                            var material = materials[j];
                            string materialName = material != null ? material.name : "<null>";
                            string shaderName = material != null && material.shader != null ? material.shader.name : "<null>";
                            entries.Add($"{materialName} => {shaderName}");
                        }
                    }

                    sb.AppendLine($"renderer={GetTransformPath(renderer.transform)}");
                    sb.AppendLine($"materials=[{string.Join(" | ", entries)}]");
                    hitCount++;
                }

                sb.AppendLine($"hitCount={hitCount}");

                string path = System.IO.Path.Combine(Application.persistentDataPath, _cyalumeDebugDumpFileName);
                System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CyalumeControllerBase] Failed to write cyalume scene snapshot: {ex.Message}");
            }
        }

        protected static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<null>";

            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        protected void LoadTextureBundlesFromIndex(string liveKey)
        {
            if (string.IsNullOrEmpty(liveKey))
                return;

            var main = FindObjectOfType<UmaViewerMain>();
            if (main == null || main.AbList == null)
                return;

            string needle = $"tex_live_cyalume_{liveKey}_";
            foreach (var entry in main.AbList.Values.Where(e =>
                         e != null &&
                         !string.IsNullOrEmpty(e.Name) &&
                         e.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                UmaAssetManager.LoadAssetBundle(entry, neverUnload: true, isRecursive: true);
            }
        }

        protected Dictionary<int, Texture2D> ResolveTexSetFromLoadedBundles(string liveKey)
        {
            var result = new Dictionary<int, Texture2D>();
            if (string.IsNullOrEmpty(liveKey))
                return result;

            foreach (var assetBundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (!assetBundle)
                    continue;

                string[] names;
                try
                {
                    names = assetBundle.GetAllAssetNames();
                }
                catch
                {
                    continue;
                }

                foreach (var path in names)
                {
                    string file = System.IO.Path.GetFileNameWithoutExtension(path);
                    var match = System.Text.RegularExpressions.Regex.Match(
                        file,
                        @"^tex_live_cyalume_(m\d+)_(\d{3})$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (!match.Success)
                        continue;

                    if (!string.Equals(match.Groups[1].Value, liveKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!int.TryParse(match.Groups[2].Value, out int patternId))
                        continue;

                    if (result.ContainsKey(patternId))
                        continue;

                    Texture2D tex = null;
                    try
                    {
                        tex = assetBundle.LoadAsset<Texture2D>(path);
                    }
                    catch
                    {
                        tex = null;
                    }

                    if (tex != null)
                        result[patternId] = tex;
                }
            }

            return result;
        }

        protected void CollectTargetMeshAndMaterials()
        {
            var meshFilters = new List<MeshFilter>();
            var materials = new List<Material>();

            for (int i = 0; i < _targetRendererList.Count; i++)
            {
                var renderer = _targetRendererList[i];
                if (!renderer)
                    continue;

                var mf = renderer.GetComponent<MeshFilter>();
                if (mf != null)
                    meshFilters.Add(mf);

                var slots = renderer.materials;
                if (slots == null)
                    continue;

                for (int j = 0; j < slots.Length; j++)
                {
                    var material = slots[j];
                    if (material != null)
                        materials.Add(material);
                }
            }

            _meshFilters = meshFilters.ToArray();
            _materialArray = materials.ToArray();
        }

        protected bool TryGetCurrentPlayback(out int patternId, out float patternStartTime, out float playSpeed, out int choreographyType, out float liveTime)
        {
            liveTime = GetCurrentLiveTime();

            if (_hasManualPlaybackState)
            {
                patternId = _manualPatternId;
                patternStartTime = _manualPatternStartTime;
                playSpeed = _manualPlaySpeed > 0f ? _manualPlaySpeed : 1f;
                choreographyType = _manualChoreographyType;
                return true;
            }

            var provider = ResolvePlaybackProvider();
            if (provider != null && provider.TryGetCurrent(liveTime, out var current) && current != null)
            {
                patternId = Mathf.Max(0, current.PatternId);
                patternStartTime = current.StartTime;
                playSpeed = current.PlaySpeed > 0f ? current.PlaySpeed : 1f;
                choreographyType = current.ChoreographyType;
                return true;
            }

            patternId = 0;
            patternStartTime = 0f;
            playSpeed = 1f;
            choreographyType = 0;
            return false;
        }

        protected float GetCurrentLiveTime()
        {
            if (Director.instance != null && Director.instance._liveTimelineControl != null)
                return Director.instance._liveTimelineControl.currentLiveTime;

            return Time.time;
        }

        protected float ComputeRecoveredYOffset(float liveTime, float patternStartTime, float playSpeed, int choreographyType)
        {
            int frameCount = Mathf.Max(1, _animationFrameCount);
            int frameNo;

            if (choreographyType >= 8)
            {
                frameNo = 0;
            }
            else
            {
                float delta = Mathf.Max(0f, liveTime - patternStartTime);
                int raw = (int)((((delta * 40f) / 40.0f) * playSpeed) * frameCount) % frameCount;
                frameNo = raw >= 0 ? raw : 0;
            }

            return 1.0f - ((float)frameNo / frameCount);
        }

        protected Texture2D ResolveTextureForPatternId(int patternId)
        {
            if (_textureSet.TryGetValue(patternId, out var exact) && exact != null)
                return exact;

            if (_textureSet.TryGetValue(0, out var fallbackZero) && fallbackZero != null)
                return fallbackZero;

            return _textureSet.Values.FirstOrDefault(x => x != null);
        }

        protected void ApplyPatternTextureToCurrentTargets(int patternId)
        {
            if (patternId == _lastAppliedPatternId)
                return;

            var texture = ResolveTextureForPatternId(patternId);
            if (texture == null)
                return;

            for (int i = 0; i < _targetRendererList.Count; i++)
            {
                var renderer = _targetRendererList[i];
                if (!renderer)
                    continue;

                var materials = renderer.materials;
                if (materials == null)
                    continue;

                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (material == null)
                        continue;

                    string prop = ResolveTextureProperty(material);
                    if (string.IsNullOrEmpty(prop))
                        continue;

                    material.SetTexture(prop, texture);
#if UNITY_2021_2_OR_NEWER
                    renderer.GetPropertyBlock(_mpb, slot);
                    _mpb.SetTexture(prop, texture);
                    renderer.SetPropertyBlock(_mpb, slot);
#else
                    renderer.GetPropertyBlock(_mpb);
                    _mpb.SetTexture(prop, texture);
                    renderer.SetPropertyBlock(_mpb);
#endif
                }
            }

            _lastAppliedPatternId = patternId;
        }

        protected void ApplyScrollOffsetToCurrentTargets(float yOffset)
        {
            if (!float.IsNaN(_lastAppliedScrollOffset) && Mathf.Abs(_lastAppliedScrollOffset - yOffset) < 0.0001f)
                return;

            for (int i = 0; i < _targetRendererList.Count; i++)
            {
                var renderer = _targetRendererList[i];
                if (!renderer)
                    continue;

                var materials = renderer.materials;
                if (materials == null)
                    continue;

                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (material == null)
                        continue;

                    string prop = ResolveTextureProperty(material);
                    if (string.IsNullOrEmpty(prop) || !material.HasProperty(prop))
                        continue;

                    material.SetTextureOffset(prop, new Vector2(0f, yOffset));

#if UNITY_2021_2_OR_NEWER
                    if (_mainTexStPropertyId >= 0)
                    {
                        var scale = material.GetTextureScale(prop);
                        renderer.GetPropertyBlock(_mpb, slot);
                        _mpb.SetVector(_mainTexStPropertyId, new Vector4(scale.x, scale.y, 0f, yOffset));
                        renderer.SetPropertyBlock(_mpb, slot);
                    }
#endif
                }
            }

            _lastAppliedScrollOffset = yOffset;
        }

        protected string ResolveTextureProperty(Material material)
        {
            if (material == null)
                return null;

            if (material.HasProperty("_MainTex"))
                return "_MainTex";
            if (material.HasProperty("_BaseMap"))
                return "_BaseMap";

            var props = material.GetTexturePropertyNames();
            return props != null && props.Length > 0 ? props[0] : null;
        }

        /// <summary>
        /// Official-style conservative behavior:
        /// - no guessed UV rewriting
        /// - unreadable meshes are skipped
        /// </summary>
        protected virtual void InitializeAudienceUvSpreadOfficialConservative()
        {
            if (!_useUVAudienceSpread || _meshFilters == null || _meshFilters.Length == 0)
                return;

            for (int i = 0; i < _meshFilters.Length; i++)
            {
                var filter = _meshFilters[i];
                if (filter == null || filter.sharedMesh == null)
                    continue;

                var mesh = filter.sharedMesh;
                if (!mesh.isReadable)
                    continue;

                // No guessed UV rewrite here. Exact official filter/name condition is not fully restored yet.
                // Keep the conservative official rule: readable meshes may be handled; unreadable meshes are skipped.
            }
        }

        /// <summary>
        /// Official-style conservative behavior:
        /// - only cache readable meshes
        /// - do not fabricate fallback colors
        /// - if exact official pattern-color mapping is unknown, leave the table empty
        /// </summary>
        protected virtual void CreateVertexColorCacheOfficialConservative()
        {
            if (_meshFilters == null || _meshFilters.Length == 0)
            {
                _colorsTable = Array.Empty<Color32[][]>();
                return;
            }

            _colorsTable = new Color32[_meshFilters.Length][][];

            for (int i = 0; i < _meshFilters.Length; i++)
            {
                var filter = _meshFilters[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    _colorsTable[i] = Array.Empty<Color32[]>();
                    continue;
                }

                var mesh = filter.sharedMesh;
                if (!mesh.isReadable)
                {
                    _colorsTable[i] = Array.Empty<Color32[]>();
                    continue;
                }

                try
                {
                    var colors = mesh.colors32;
                    if (colors == null || colors.Length == 0)
                    {
                        _colorsTable[i] = Array.Empty<Color32[]>();
                        continue;
                    }

                    // Keep only the readable source colors as a single conservative slot.
                    var copy = new Color32[colors.Length];
                    Array.Copy(colors, copy, colors.Length);
                    _colorsTable[i] = new[] { copy };
                }
                catch
                {
                    _colorsTable[i] = Array.Empty<Color32[]>();
                }
            }
        }

        /// <summary>
        /// Official-style conservative behavior:
        /// - readable meshes may receive cached colors
        /// - unreadable meshes are skipped
        /// - no front/back guessed repair path here
        /// - texture and scroll are still driven from CSV playback
        /// </summary>
        protected virtual void UpdateCyalumeOfficialConservative(bool isForceUpdate)
        {
            if (!_isInitialized)
                return;

            if (!TryGetCurrentPlayback(out int patternId, out float patternStartTime, out float playSpeed, out int choreographyType, out float liveTime))
                return;

            bool patternChanged = patternId != _lastAppliedPatternId;
            if (isForceUpdate || patternChanged)
            {
                for (int i = 0; i < _meshFilters.Length; i++)
                {
                    var filter = _meshFilters[i];
                    if (filter == null || filter.sharedMesh == null)
                        continue;

                    var mesh = filter.sharedMesh;
                    if (!mesh.isReadable)
                        continue;

                    if (_colorsTable == null || i >= _colorsTable.Length)
                        continue;

                    var perMesh = _colorsTable[i];
                    if (perMesh == null || perMesh.Length == 0)
                        continue;

                    var colors = perMesh[0];
                    if (colors == null || colors.Length != mesh.vertexCount)
                        continue;

                    try
                    {
                        mesh.colors32 = colors;
                    }
                    catch (Exception ex)
                    {
                        if (_verboseLog)
                            Debug.LogWarning($"[CyalumeControllerBase] Failed to write colors to {mesh.name}: {ex.Message}");
                    }
                }
            }

            ApplyPatternTextureToCurrentTargets(patternId);

            float yOffset = ComputeRecoveredYOffset(liveTime, patternStartTime, playSpeed, choreographyType);
            yOffset -= Mathf.Floor(yOffset);
            ApplyScrollOffsetToCurrentTargets(yOffset);
        }

        public abstract void InitializeCyalumeObjectsOnly(bool forceRebuild = false);
        public abstract bool ApplyTargetSelection(bool preferRandom, bool forceRefresh = false);
    }
}
