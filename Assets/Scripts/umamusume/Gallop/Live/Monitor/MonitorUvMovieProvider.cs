using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    //UVMovie动画的帧率、帧数、循环区间和显示尺寸
    [Serializable]
    public class MonitorUvMovieFrameInfo
    {
        public float Fps = 30f;
        public int Count;
        public float StartOffsetSec;
        public float EndOffsetSec;
        public bool IsLoop = true;
        public float StartLoopSec;
        public float EndLoopSec;
        public int LoopCount;
        public Vector2 Size = Vector2.one;
    }

    [Serializable]
    public class MonitorUvMovieMetadata
    {
        public string Name;
        public int ImageCount;

        [JsonProperty("FramePerImage")]
        public int FramePerImage = -1;

        [JsonProperty("FramePerWidth")]
        public int FramePerWidth = -1;

        [JsonProperty("FremePerImage")]
        public int FremePerImage = -1;

        [JsonProperty("FremePerWidth")]
        public int FremePerWidth = -1;

        public MonitorUvMovieFrameInfo FrameInfo = new MonitorUvMovieFrameInfo();
        public bool ExistMaskTex;

        [JsonIgnore]
        public int EffectiveFramePerImage
        {
            get
            {
                if (FramePerImage > 0)
                    return FramePerImage;
                if (FremePerImage > 0)
                    return FremePerImage;
                return 1;
            }
        }

        [JsonIgnore]
        public int EffectiveFramePerWidth
        {
            get
            {
                if (FramePerWidth > 0)
                    return FramePerWidth;
                if (FremePerWidth > 0)
                    return FremePerWidth;
                return 1;
            }
        }
    }

    [Serializable]
    public class MonitorUvMovieClipData
    {
        public string folderName;
        public string folderPath;
        public string metadataPath;
        public MonitorUvMovieMetadata metadata;
        public List<string> framePaths = new List<string>();
        public List<string> maskTexturePaths = new List<string>();
        public string lightTexturePath;
        public string lightMaskTexturePath;
        public bool ownsLoadedTextures;

        [NonSerialized] public List<Texture2D> frameTextures = new List<Texture2D>();
        [NonSerialized] public List<Texture2D> maskTextures = new List<Texture2D>();
        [NonSerialized] public Texture2D lightTexture;
        [NonSerialized] public Texture2D lightMaskTexture;
        [NonSerialized] public Vector2 texturePixelOffset = Vector2.zero;
        [NonSerialized] public Vector2 texturePixelScale = Vector2.zero;

        public string DisplayName
        {
            get
            {
                if (metadata != null && !string.IsNullOrEmpty(metadata.Name))
                    return metadata.Name;
                return folderName;
            }
        }

        public int ImageCount
        {
            get
            {
                if (metadata != null && metadata.ImageCount > 0)
                    return metadata.ImageCount;

                return framePaths != null ? framePaths.Count : 0;
            }
        }
        /// 获取动画总帧数
        /// 优先读取明确的帧数,其次通过图片数和每图帧数计算
        public int FrameCount
        {
            get
            {
                if (metadata != null && metadata.FrameInfo != null && metadata.FrameInfo.Count > 0)
                    return metadata.FrameInfo.Count;

                if (metadata != null && metadata.ImageCount > 0 && metadata.EffectiveFramePerImage > 0)
                    return metadata.ImageCount * metadata.EffectiveFramePerImage;

                return framePaths != null ? framePaths.Count : 0;
            }
        }

        public float Fps
        {
            get
            {
                if (metadata != null && metadata.FrameInfo != null && metadata.FrameInfo.Fps > 0f)
                    return metadata.FrameInfo.Fps;
                return 30f;
            }
        }

        public bool TryGetFrameTexture(int imageIndex, out Texture2D texture)
        {
            texture = null;
            if (frameTextures == null || imageIndex < 0 || imageIndex >= frameTextures.Count)
                return false;

            texture = frameTextures[imageIndex];
            return texture != null;
        }

        public bool TryGetMaskTexture(int imageIndex, out Texture2D texture)
        {
            texture = null;
            if (maskTextures == null || imageIndex < 0 || imageIndex >= maskTextures.Count)
                return false;

            texture = maskTextures[imageIndex];
            return texture != null;
        }

        public void FinalizeDerivedData()
        {
            SortPairs(framePaths, frameTextures);
            SortPairs(maskTexturePaths, maskTextures);

            int imageCount = Mathf.Max(ImageCount, frameTextures.Count);

            if (maskTextures.Count == 1 && imageCount > 1)
            {
                Texture2D single = maskTextures[0];
                maskTextures = Enumerable.Repeat(single, imageCount).ToList();

                string singlePath = maskTexturePaths.Count > 0 ? maskTexturePaths[0] : null;
                if (!string.IsNullOrEmpty(singlePath))
                    maskTexturePaths = Enumerable.Repeat(singlePath, imageCount).ToList();
            }

            Texture2D referenceTexture = frameTextures.FirstOrDefault(t => t != null) ?? lightTexture;
            if (referenceTexture != null && referenceTexture.filterMode != FilterMode.Point)
            {
                float invWidth = referenceTexture.width > 0 ? 1f / referenceTexture.width : 0f;
                float invHeight = referenceTexture.height > 0 ? 1f / referenceTexture.height : 0f;
                texturePixelOffset = new Vector2(invWidth * 0.5f, invHeight * 0.5f);
                texturePixelScale = new Vector2(invWidth, invHeight);
            }
            else
            {
                texturePixelOffset = Vector2.zero;
                texturePixelScale = Vector2.zero;
            }
        }

        private static void SortPairs(List<string> paths, List<Texture2D> textures)
        {
            if (paths == null || textures == null || paths.Count != textures.Count || paths.Count <= 1)
                return;

            List<KeyValuePair<string, Texture2D>> pairs = new List<KeyValuePair<string, Texture2D>>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
                pairs.Add(new KeyValuePair<string, Texture2D>(paths[i], textures[i]));

            pairs = pairs
                .OrderBy(p => MonitorUvMovieProvider.GetTextureSortKey(p.Key))
                .ThenBy(p => Path.GetFileName(p.Key), StringComparer.OrdinalIgnoreCase)
                .ToList();

            paths.Clear();
            textures.Clear();

            for (int i = 0; i < pairs.Count; i++)
            {
                paths.Add(pairs[i].Key);
                textures.Add(pairs[i].Value);
            }
        }
    }


    [Serializable]
    public class MonitorUvMovieContextSlot
    {
        public int slotIndex;
        public int slotId;
        public string resourceName;
        public string effectiveResourceName;
        public bool isLoadAlways;
        public bool isReferencedByWorksheet;
        public bool isEnabledLoad;
        public bool useStandardMode;
        public int playConditionValue;
        public string resolvedClipFolder;
        public string resolvedBy;

        [NonSerialized] public MonitorUvMovieClipData clip;
    }

    [Serializable]
    public class LiveSettingsUvMovieRow
    {
        public int recordId;
        public int slotIndex;
        public int slotId;
        public string resourceName;
        public int loadConditionValue;
        public int playConditionValue;
        public bool isEnabledLoad;
    }

    public class MonitorUvMovieProvider : MonoBehaviour
    {
        [Header("Auto-load uvmovie data for the current live.")]
        public bool autoLoadForCurrentLive = true;
        public bool loadTexturesOnInitialize = true;
        public bool verboseLog = false;

        [Header("Official slot table from decrypted liveSettings data")]
        public string decryptedLiveSettingsRootOverride = string.Empty;

        [Header("Debug view of loaded clips.")]
        public List<MonitorUvMovieClipData> clips = new List<MonitorUvMovieClipData>();

        [Header("Resolved UVMovie context slots from official liveSettings slot table.")]
        public List<MonitorUvMovieContextSlot> contextSlots = new List<MonitorUvMovieContextSlot>();

        [SerializeField] private int _loadedMusicId;
        [SerializeField] private string _loadedRootPath;
        [SerializeField] private bool _hasTriedLoad;
        [SerializeField] private bool _lastLoadSucceeded;
        [SerializeField] private int _lastSyncedStageInstanceId;
        [SerializeField] private int _lastContextSlotHash;
        private bool _hasResolvedContextSlots;

        public int LoadedMusicId => _loadedMusicId;
        public string LoadedRootPath => _loadedRootPath;
        public bool HasTriedLoad => _hasTriedLoad;
        public bool LastLoadSucceeded => _lastLoadSucceeded;
        public int ContextSlotCount => contextSlots != null ? contextSlots.Count : 0;
        // A loaded table may legitimately contain no UVMovie rows. Completion
        // is independent of slot availability, so that case is not retried each frame.
        public bool HasResolvedContextSlots => _hasResolvedContextSlots;

        private void Start()
        {
            if (!autoLoadForCurrentLive || _loadedMusicId > 0 || clips.Count > 0)
                return;

            int musicId = Director.instance?.live?.MusicId ?? 0;
            if (musicId > 0)
                InitializeForMusicId(musicId);
        }

        public bool InitializeForMusicId(int musicId, bool forceReload = false)
        {
            if (musicId <= 0)
            {
                if (verboseLog)
                    Debug.LogWarning("[MonitorUvMovieProvider] InitializeForMusicId failed: invalid musicId.");
                return false;
            }

            if (!forceReload && _loadedMusicId == musicId && _hasTriedLoad)
                return _lastLoadSucceeded;

            ReleaseLoadedTextures();
            clips.Clear();
            ClearContextSlots();
            _loadedMusicId = musicId;
            _loadedRootPath = null;
            _hasTriedLoad = true;
            _lastLoadSucceeded = false;

            if (TryLoadClipsFromAssetBundles(musicId, out List<MonitorUvMovieClipData> bundleClips, out string bundleSource))
            {
                clips = bundleClips;
                _loadedRootPath = bundleSource;
                _lastLoadSucceeded = clips.Count > 0;

                if (verboseLog)
                    Debug.Log($"[MonitorUvMovieProvider] Loaded uvmovie clips from AssetBundles: musicId={musicId}, clips={clips.Count}, source={bundleSource}");

                return _lastLoadSucceeded;
            }

            string rootPath = ResolveUvMovieRootPath();
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                if (verboseLog)
                    Debug.LogWarning("[MonitorUvMovieProvider] uvmovie root path not found.");
                return false;
            }

            _loadedRootPath = rootPath;

            List<string> folderPaths = FindClipFolders(rootPath, musicId);
            if (folderPaths.Count == 0)
            {
                if (verboseLog)
                    Debug.LogWarning($"[MonitorUvMovieProvider] No uvmovie folders found for musicId={musicId} under {rootPath}");
                return false;
            }

            for (int i = 0; i < folderPaths.Count; i++)
            {
                MonitorUvMovieClipData clip = BuildClipData(folderPaths[i], loadTexturesOnInitialize);
                if (clip != null)
                    clips.Add(clip);
            }

            clips = clips
                .OrderBy(c => c.folderName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (verboseLog)
                Debug.Log($"[MonitorUvMovieProvider] Loaded uvmovie clips: musicId={musicId}, clips={clips.Count}, root={rootPath}");

            _lastLoadSucceeded = clips.Count > 0;
            return _lastLoadSucceeded;
        }

        public bool TryGetClipByDisplayId(int dispId, out MonitorUvMovieClipData clip)
        {
            return TryGetClipBySlotId(dispId, out clip);
        }

        public bool TryGetClipBySlotIndex(int slotIndex, out MonitorUvMovieClipData clip)
        {
            return TryGetClipBySlotId(slotIndex + 1, out clip);
        }

        public string GetSlotDebugName(int slotId)
        {
            if (contextSlots == null)
                return string.Empty;

            for (int i = 0; i < contextSlots.Count; i++)
            {
                MonitorUvMovieContextSlot slot = contextSlots[i];
                if (slot == null || slot.slotId != slotId)
                    continue;

                if (!string.IsNullOrWhiteSpace(slot.effectiveResourceName))
                    return slot.effectiveResourceName;
                if (!string.IsNullOrWhiteSpace(slot.resourceName))
                    return slot.resourceName;
                return slot.resolvedClipFolder ?? string.Empty;
            }

            return string.Empty;
        }

        public bool TryGetContextSlot(int slotId, out MonitorUvMovieContextSlot slot)
        {
            slot = null;
            if (contextSlots == null || slotId <= 0)
                return false;

            for (int i = 0; i < contextSlots.Count; i++)
            {
                MonitorUvMovieContextSlot item = contextSlots[i];
                if (item != null && item.slotId == slotId)
                {
                    slot = item;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetClipBySlotId(int slotId, out MonitorUvMovieClipData clip)
        {
            clip = null;

            if (TryGetContextSlot(slotId, out MonitorUvMovieContextSlot slot))
            {
                clip = slot.clip;
                return clip != null;
            }

            return false;
        }

        private void ClearContextSlots()
        {
            if (contextSlots != null)
            {
                for (int i = 0; i < contextSlots.Count; i++)
                {
                    if (contextSlots[i] != null)
                        contextSlots[i].clip = null;
                }

                contextSlots.Clear();
            }

            _lastSyncedStageInstanceId = 0;
            _lastContextSlotHash = 0;
            _hasResolvedContextSlots = false;
        }

        public bool RebuildContextSlotsFromLiveSettings(LiveTimelineControl timelineControl, int musicId, bool force = false)
        {
            _hasResolvedContextSlots = false;
            string csvText = ResolveLiveSettingsCsvText(musicId);
            if (string.IsNullOrWhiteSpace(csvText))
            {
                if (verboseLog)
                    Debug.LogWarning($"[MonitorUvMovieProvider] liveSettings data not found: musicId={musicId}");
                return false;
            }

            List<LiveSettingsUvMovieRow> sourceRows = ParseLiveSettingsUvMovieRows(csvText);
            if (sourceRows.Count == 0)
            {
                _hasResolvedContextSlots = true;
                if (verboseLog)
                    Debug.LogWarning("[MonitorUvMovieProvider] liveSettings csv contains no type=4 UVMovie rows.");
                return false;
            }

            List<LiveSettingsUvMovieRow> finalRows = BuildFinalUvMovieRows(sourceRows, musicId);
            HashSet<int> referencedSlotIds = CollectReferencedSlotIds(timelineControl);
            ApplyRegisterUvMovieRules(finalRows, referencedSlotIds);
            EnsureClipsLoadedForRows(finalRows);

            List<MonitorUvMovieContextSlot> resolvedSlots = new List<MonitorUvMovieContextSlot>(finalRows.Count);
            for (int i = 0; i < finalRows.Count; i++)
            {
                LiveSettingsUvMovieRow row = finalRows[i];
                string effectiveResourceName = row.isEnabledLoad ? (row.resourceName ?? string.Empty) : string.Empty;

                MonitorUvMovieContextSlot slot = new MonitorUvMovieContextSlot
                {
                    slotIndex = row.slotIndex,
                    slotId = row.slotId,
                    resourceName = row.resourceName ?? string.Empty,
                    effectiveResourceName = effectiveResourceName,
                    isLoadAlways = row.loadConditionValue == 0,
                    isReferencedByWorksheet = referencedSlotIds == null || referencedSlotIds.Count == 0 || referencedSlotIds.Contains(row.slotId),
                    isEnabledLoad = row.isEnabledLoad,
                    useStandardMode = ShouldUseStandardMode(row.playConditionValue),
                    playConditionValue = row.playConditionValue,
                };

                if (!string.IsNullOrWhiteSpace(effectiveResourceName))
                {
                    slot.clip = ResolveClipByResourceName(effectiveResourceName, out string matchKind);
                    if (slot.clip != null && slot.useStandardMode)
                    {
                        bool hasStandardFrames = slot.clip.metadata != null && slot.clip.FrameCount > 0;
                        if (!hasStandardFrames && slot.clip.lightTexture != null)
                            slot.useStandardMode = false;
                    }

                    slot.resolvedBy = string.IsNullOrWhiteSpace(matchKind) ? "live-settings" : matchKind;
                    slot.resolvedClipFolder = slot.clip != null ? slot.clip.folderName : string.Empty;
                }
                else
                {
                    slot.clip = null;
                    slot.resolvedBy = row.isEnabledLoad ? "unresolved" : "disabled";
                    slot.resolvedClipFolder = string.Empty;
                }

                resolvedSlots.Add(slot);
            }

            int slotHash = ComputeSlotHash(resolvedSlots);
            if (!force && _lastContextSlotHash == slotHash && contextSlots.Count == resolvedSlots.Count)
            {
                _hasResolvedContextSlots = true;
                if (verboseLog)
                    Debug.Log($"[MonitorUvMovieProvider] liveSettings slots unchanged: total={resolvedSlots.Count}, musicId={musicId}");
                return false;
            }

            contextSlots = resolvedSlots;
            _lastSyncedStageInstanceId = musicId;
            _lastContextSlotHash = slotHash;
            _hasResolvedContextSlots = true;

            if (verboseLog)
            {
                int resolvedCount = 0;
                for (int i = 0; i < contextSlots.Count; i++)
                {
                    if (contextSlots[i] != null && contextSlots[i].clip != null)
                        resolvedCount++;
                }

                Debug.Log($"[MonitorUvMovieProvider] rebuilt context slots from liveSettings: total={contextSlots.Count}, resolved={resolvedCount}, musicId={musicId}");
            }

            return contextSlots.Count > 0;
        }

        private List<LiveSettingsUvMovieRow> BuildFinalUvMovieRows(List<LiveSettingsUvMovieRow> sourceRows, int musicId)
        {
            List<LiveSettingsUvMovieRow> finalRows = new List<LiveSettingsUvMovieRow>(sourceRows != null ? sourceRows.Count : 0);
            if (sourceRows == null || sourceRows.Count == 0)
                return finalRows;

            bool highQualityMode = IsHighQualityMonitorMode();
            string lowQualityLightResourceName = BuildLowQualityLightResourceName(musicId);

            for (int i = 0; i < sourceRows.Count; i++)
            {
                LiveSettingsUvMovieRow row = sourceRows[i];
                if (row == null)
                    continue;

                string resourceName = (row.resourceName ?? string.Empty).Trim();
                int playConditionValue = row.playConditionValue;
                if (ShouldSwapToLowQualityLightResource(resourceName, playConditionValue, highQualityMode, lowQualityLightResourceName))
                {
                    resourceName = lowQualityLightResourceName;
                    playConditionValue = 1;
                }

                finalRows.Add(new LiveSettingsUvMovieRow
                {
                    recordId = row.recordId,
                    slotIndex = row.slotIndex >= 0 ? row.slotIndex : finalRows.Count,
                    slotId = row.slotId > 0 ? row.slotId : finalRows.Count + 1,
                    resourceName = resourceName,
                    loadConditionValue = row.loadConditionValue,
                    playConditionValue = playConditionValue,
                    isEnabledLoad = row.isEnabledLoad,
                });
            }

            return finalRows;
        }

        private HashSet<int> CollectReferencedSlotIds(LiveTimelineControl timelineControl)
        {
            HashSet<int> result = new HashSet<int>();
            if (timelineControl == null || timelineControl.data == null || timelineControl.data.worksheetList == null)
                return result;

            List<LiveTimelineWorkSheet> worksheets = timelineControl.data.worksheetList;
            for (int wsIndex = 0; wsIndex < worksheets.Count; wsIndex++)
            {
                LiveTimelineWorkSheet worksheet = worksheets[wsIndex];
                if (worksheet == null || worksheet.monitorControlList == null)
                    continue;

                for (int monitorIndex = 0; monitorIndex < worksheet.monitorControlList.Count; monitorIndex++)
                {
                    LiveTimelineMonitorControlData monitor = worksheet.monitorControlList[monitorIndex];
                    LiveTimelineKeyMonitorControlDataList keys = monitor != null ? monitor.keys : null;
                    if (keys == null || keys.Count == 0)
                        continue;

                    for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
                    {
                        LiveTimelineKeyMonitorControlData key = keys.At(keyIndex) as LiveTimelineKeyMonitorControlData;
                        if (key == null)
                            continue;

                        if (key.dispID > 0)
                            result.Add(key.dispID);
                        if (key.DispID2 > 0)
                            result.Add(key.DispID2);

                        LiveTimelineMonitorChangeUVSetting[] changes = key.ChangeUVSettingArray;
                        if (changes == null || changes.Length == 0)
                            continue;

                        for (int changeIndex = 0; changeIndex < changes.Length; changeIndex++)
                        {
                            LiveTimelineMonitorChangeUVSetting change = changes[changeIndex];
                            if (change != null &&
                                change.IsEnabled &&
                                change.DispID > 0 &&
                                DoesChangeConditionMatchCurrentCharacters(change.ConditionArray))
                            {
                                result.Add(change.DispID);
                            }
                        }
                    }
                }
            }

            return result;
        }

        private bool ShouldUseStandardMode(int playConditionValue)
        {
            return playConditionValue != 1;
        }

        private void EnsureClipsLoadedForRows(List<LiveSettingsUvMovieRow> rows)
        {
            if (rows == null || rows.Count == 0)
                return;

            HashSet<string> requiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < rows.Count; i++)
            {
                LiveSettingsUvMovieRow row = rows[i];
                if (row == null || !row.isEnabledLoad || string.IsNullOrWhiteSpace(row.resourceName))
                    continue;

                requiredNames.Add(row.resourceName.Trim());
            }

            if (requiredNames.Count == 0)
                return;

            List<string> missingNames = new List<string>();
            foreach (string resourceName in requiredNames)
            {
                if (ResolveClipByResourceName(resourceName, out _) == null)
                    missingNames.Add(resourceName);
            }

            if (missingNames.Count == 0)
                return;

            if (clips == null)
                clips = new List<MonitorUvMovieClipData>();

            string[] folderKeys = missingNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (folderKeys.Length == 0)
                return;

            TryLoadUvMovieBundlesFromIndex(folderKeys);
            MergeResolvedClips(ResolveUvMovieClipsFromLoadedBundles(folderKeys));

            string rootPath = ResolveUvMovieRootPath();
            if (!string.IsNullOrWhiteSpace(rootPath) && Directory.Exists(rootPath))
            {
                for (int i = 0; i < folderKeys.Length; i++)
                {
                    if (ResolveClipByResourceName(folderKeys[i], out _) != null)
                        continue;

                    string folderPath = ResolveClipFolderPath(rootPath, folderKeys[i]);
                    if (string.IsNullOrWhiteSpace(folderPath))
                        continue;

                    MonitorUvMovieClipData clip = BuildClipData(folderPath, loadTexturesOnInitialize);
                    if (clip != null)
                        MergeClip(clip);
                }
            }

            clips = clips
                .Where(c => c != null)
                .OrderBy(c => c.folderName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void MergeResolvedClips(Dictionary<string, MonitorUvMovieClipData> resolved)
        {
            if (resolved == null || resolved.Count == 0)
                return;

            foreach (MonitorUvMovieClipData clip in resolved.Values)
                MergeClip(clip);
        }

        private void MergeClip(MonitorUvMovieClipData clip)
        {
            if (clip == null)
                return;

            if (clips == null)
                clips = new List<MonitorUvMovieClipData>();

            for (int i = 0; i < clips.Count; i++)
            {
                MonitorUvMovieClipData existing = clips[i];
                if (existing == null)
                    continue;

                if (string.Equals(existing.folderName, clip.folderName, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            clips.Add(clip);
        }

        private static string ResolveClipFolderPath(string rootPath, string resourceName)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(resourceName) || !Directory.Exists(rootPath))
                return null;

            string directPath = Path.Combine(rootPath, resourceName);
            if (Directory.Exists(directPath))
                return directPath;

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(rootPath);
            }
            catch
            {
                return null;
            }

            for (int i = 0; i < directories.Length; i++)
            {
                string folderName = Path.GetFileName(directories[i]);
                if (string.Equals(folderName, resourceName, StringComparison.OrdinalIgnoreCase))
                    return directories[i];
            }

            return null;
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

        private static string BuildLowQualityLightResourceName(int musicId)
        {
            return musicId > 0 ? $"gal_UVmovie_{musicId}_Light" : string.Empty;
        }

        private static bool ShouldSwapToLowQualityLightResource(
            string resourceName,
            int playConditionValue,
            bool highQualityMode,
            string lowQualityLightResourceName)
        {
            if (highQualityMode ||
                string.IsNullOrWhiteSpace(resourceName) ||
                string.IsNullOrWhiteSpace(lowQualityLightResourceName))
            {
                return false;
            }

            if (playConditionValue == 1)
                return false;

            return !resourceName.EndsWith("_Light", StringComparison.OrdinalIgnoreCase);
        }

        private MonitorUvMovieClipData ResolveClipByResourceName(string resourceName, out string matchKind)
        {
            matchKind = null;
            if (clips == null || clips.Count == 0 || string.IsNullOrWhiteSpace(resourceName))
                return null;

            string normalized = NormalizeLookupName(resourceName);
            if (string.IsNullOrEmpty(normalized))
                return null;

            string fileName = NormalizeLookupName(Path.GetFileName(resourceName));
            string fileStem = NormalizeLookupName(Path.GetFileNameWithoutExtension(resourceName));
            MonitorUvMovieClipData bestLooseMatch = null;
            string bestLooseMatchKind = null;
            int bestLooseScore = int.MinValue;

            void EvaluateCandidate(MonitorUvMovieClipData clip, string candidate, string candidateKind)
            {
                if (clip == null || string.IsNullOrWhiteSpace(candidate))
                    return;

                if (NameEqualsExact(candidate, normalized, fileName, fileStem))
                {
                    bestLooseMatch = clip;
                    bestLooseMatchKind = candidateKind;
                    bestLooseScore = int.MaxValue;
                    return;
                }

                string candidateNorm = NormalizeLookupName(candidate);
                if (string.IsNullOrEmpty(candidateNorm))
                    return;

                int score = int.MinValue;
                if (candidateNorm.EndsWith(normalized, StringComparison.OrdinalIgnoreCase) ||
                    normalized.EndsWith(candidateNorm, StringComparison.OrdinalIgnoreCase))
                {
                    score = 600 - Mathf.Abs(candidateNorm.Length - normalized.Length);
                }
                else if (candidateNorm.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                         normalized.Contains(candidateNorm, StringComparison.OrdinalIgnoreCase))
                {
                    score = 400 - Mathf.Abs(candidateNorm.Length - normalized.Length);
                }

                if (score > bestLooseScore)
                {
                    bestLooseMatch = clip;
                    bestLooseMatchKind = $"loose-{candidateKind}";
                    bestLooseScore = score;
                }
            }

            for (int i = 0; i < clips.Count; i++)
            {
                MonitorUvMovieClipData clip = clips[i];
                if (clip == null)
                    continue;

                EvaluateCandidate(clip, clip.metadata != null ? clip.metadata.Name : null, "metadata-name");
                if (bestLooseScore == int.MaxValue)
                {
                    matchKind = bestLooseMatchKind;
                    return bestLooseMatch;
                }

                EvaluateCandidate(clip, clip.DisplayName, "display-name");
                if (bestLooseScore == int.MaxValue)
                {
                    matchKind = bestLooseMatchKind;
                    return bestLooseMatch;
                }

                EvaluateCandidate(clip, clip.folderName, "folder-name");
                if (bestLooseScore == int.MaxValue)
                {
                    matchKind = bestLooseMatchKind;
                    return bestLooseMatch;
                }
            }

            matchKind = bestLooseMatchKind;
            return bestLooseMatch;
        }

        private static string NormalizeLookupName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim().Replace('\\', '/');
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0 && slashIndex + 1 < normalized.Length)
                normalized = normalized.Substring(slashIndex + 1);

            string fileStem = Path.GetFileNameWithoutExtension(normalized);
            if (!string.IsNullOrEmpty(fileStem))
                normalized = fileStem;

            StringBuilder builder = new StringBuilder(normalized.Length);
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (char.IsLetterOrDigit(c))
                    builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        private static bool NameEqualsExact(string candidate, string normalized, string fileName, string fileStem)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            string candidateNorm = NormalizeLookupName(candidate);
            if (string.IsNullOrEmpty(candidateNorm))
                return false;

            return string.Equals(candidateNorm, normalized, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(candidateNorm, fileName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(candidateNorm, fileStem, StringComparison.OrdinalIgnoreCase);
        }

        private static int ComputeSlotHash(List<MonitorUvMovieContextSlot> slots)
        {
            unchecked
            {
                int hash = 17;
                if (slots == null)
                    return hash;

                for (int i = 0; i < slots.Count; i++)
                {
                    MonitorUvMovieContextSlot slot = slots[i];
                    hash = hash * 31 + (slot != null ? slot.slotIndex : -1);
                    hash = hash * 31 + (slot != null ? slot.slotId : -1);
                    hash = hash * 31 + (slot != null && slot.resourceName != null ? slot.resourceName.GetHashCode() : 0);
                    hash = hash * 31 + (slot != null && slot.effectiveResourceName != null ? slot.effectiveResourceName.GetHashCode() : 0);
                    hash = hash * 31 + (slot != null && slot.isLoadAlways ? 1 : 0);
                    hash = hash * 31 + (slot != null && slot.isReferencedByWorksheet ? 1 : 0);
                    hash = hash * 31 + (slot != null && slot.isEnabledLoad ? 1 : 0);
                    hash = hash * 31 + (slot != null && slot.useStandardMode ? 1 : 0);
                    hash = hash * 31 + (slot != null ? slot.playConditionValue : 0);
                    hash = hash * 31 + (slot != null && slot.resolvedClipFolder != null ? slot.resolvedClipFolder.GetHashCode() : 0);
                }

                return hash;
            }
        }

        private string ResolveLiveSettingsCsvText(int musicId)
        {
            string bundledText = LoadLiveSettingsTextFromAssetBundle(musicId);
            if (!string.IsNullOrWhiteSpace(bundledText))
                return bundledText;

            string csvPath = ResolveLiveSettingsCsvPath(musicId);
            if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
            {
                if (verboseLog)
                    Debug.LogWarning($"[MonitorUvMovieProvider] liveSettings unresolved from AssetBundle and file fallback: musicId={musicId}");
                return null;
            }

            return File.ReadAllText(csvPath);
        }

        private string LoadLiveSettingsTextFromAssetBundle(int musicId)
        {
            if (musicId <= 0)
                return null;

            UmaViewerMain main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null)
                return null;

            if (!main.AbList.TryGetValue("livesettings", out UmaDatabaseEntry asset) || asset == null)
                return null;

            try
            {
                AssetBundle bundle = UmaAssetManager.LoadAssetBundle(asset, true);
                if (!bundle)
                    return null;

                string assetName = musicId.ToString();
                if (!bundle.Contains(assetName))
                    return null;

                TextAsset textAsset = bundle.LoadAsset<TextAsset>(assetName);
                if (LiveRuntimeDiagnostics.Enabled)
                    LiveRuntimeDiagnostics.RecordAssetLoad(asset.Name, "LoadAsset", typeof(TextAsset),
                        new UnityEngine.Object[] { textAsset }, textAsset, assetName);
                return textAsset != null ? textAsset.text : null;
            }
            catch (Exception ex)
            {
                if (verboseLog)
                    Debug.LogWarning($"[MonitorUvMovieProvider] Failed to load livesettings from AssetBundle: musicId={musicId}, error={ex.Message}");
                return null;
            }
        }

        private string ResolveLiveSettingsCsvPath(int musicId)
        {
            List<string> candidates = new List<string>();

            void AddCandidate(string path)
            {
                if (!string.IsNullOrWhiteSpace(path) && !candidates.Contains(path))
                    candidates.Add(path);
            }

            if (!string.IsNullOrWhiteSpace(decryptedLiveSettingsRootOverride))
            {
                AddCandidate(Path.Combine(decryptedLiveSettingsRootOverride, $"{musicId}.csv"));
                AddCandidate(Path.Combine(decryptedLiveSettingsRootOverride, $"m{musicId}.csv"));
                AddCandidate(Path.Combine(decryptedLiveSettingsRootOverride, $"livesettings_{musicId}.csv"));
            }

            List<string> roots = new List<string>();
            if (Config.Instance != null && !string.IsNullOrWhiteSpace(Config.Instance.MainPath))
                roots.Add(Config.Instance.MainPath);

            roots.Add(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));

            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    continue;

                AddCandidate(Path.Combine(root, $"{musicId}.csv"));
                AddCandidate(Path.Combine(root, $"m{musicId}.csv"));
                AddCandidate(Path.Combine(root, "Assets", $"{musicId}.csv"));
                AddCandidate(Path.Combine(root, "Assets", $"m{musicId}.csv"));
                AddCandidate(Path.Combine(root, "assets", $"{musicId}.csv"));
                AddCandidate(Path.Combine(root, "assets", $"m{musicId}.csv"));
                AddCandidate(Path.Combine(root, "livesettings", $"{musicId}.csv"));
                AddCandidate(Path.Combine(root, "livesettings", $"m{musicId}.csv"));
                AddCandidate(Path.Combine(root, "assets", "_gallopresources", "bundle", "root", "livesettings", $"{musicId}.csv"));
                AddCandidate(Path.Combine(root, "assets", "_gallopresources", "bundle", "root", "livesettings", $"m{musicId}.csv"));
                AddCandidate(Path.Combine(root, "Assets", "_gallopresources", "bundle", "root", "livesettings", $"{musicId}.csv"));
                AddCandidate(Path.Combine(root, "Assets", "_gallopresources", "bundle", "root", "livesettings", $"m{musicId}.csv"));
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (File.Exists(candidate))
                    return candidate;
            }

            string keyword1 = $",4,gal_UVmovie_{musicId}_";
            string keyword2 = $",4,gal_uvmovie_{musicId}_";

            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    continue;

                try
                {
                    string[] csvFiles = Directory.GetFiles(root, "*.csv", SearchOption.AllDirectories);
                    for (int i = 0; i < csvFiles.Length; i++)
                    {
                        string path = csvFiles[i];
                        string fileName = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
                        if (!fileName.Contains(musicId.ToString(), StringComparison.OrdinalIgnoreCase) &&
                            !path.Contains("livesettings", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string text;
                        try { text = File.ReadAllText(path); }
                        catch { continue; }

                        if (text.IndexOf(keyword1, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            text.IndexOf(keyword2, StringComparison.OrdinalIgnoreCase) >= 0)
                            return path;
                    }
                }
                catch { }
            }

            return null;
        }

        private bool IsHighQualityMonitorMode()
        {
            try
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (type == null)
                            continue;

                        if (!type.Name.Contains("ApplicationSettingSaveLoader", StringComparison.OrdinalIgnoreCase))
                            continue;

                        object instance = GetStaticMemberValue(type, "Instance") ?? GetStaticMemberValue(type, "instance");
                        if (instance == null)
                            continue;

                        MethodInfo getter = type.GetMethod("get_GameQuality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (getter != null)
                        {
                            object value = getter.Invoke(instance, null);
                            if (value != null)
                            {
                                int quality = Convert.ToInt32(value);
                                return quality == 2 || quality == 3;
                            }
                        }

                        PropertyInfo property = type.GetProperty("GameQuality", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (property != null && property.CanRead)
                        {
                            object value = property.GetValue(instance, null);
                            if (value != null)
                            {
                                int quality = Convert.ToInt32(value);
                                return quality == 2 || quality == 3;
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return true;
        }

        private static object GetStaticMemberValue(Type type, string memberName)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null)
            {
                try { return field.GetValue(null); } catch { }
            }

            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.CanRead)
            {
                try { return property.GetValue(null, null); } catch { }
            }

            return null;
        }

        private static List<LiveSettingsUvMovieRow> ParseLiveSettingsUvMovieRows(string csvText)
        {
            List<LiveSettingsUvMovieRow> rows = new List<LiveSettingsUvMovieRow>();
            if (string.IsNullOrWhiteSpace(csvText))
                return rows;

            string[] lines = csvText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int slotIndex = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] cols = line.Split(',');
                if (cols.Length < 3)
                    continue;

                int type = ParseCsvInt(cols, 1, -1);
                if (type != 4)
                    continue;

                string resourceName = GetCsvCell(cols, 2);
                int loadConditionValue = ParseCsvInt(cols, 3, 0);
                int playConditionValue = ParseCsvInt(cols, 4, 0);

                rows.Add(new LiveSettingsUvMovieRow
                {
                    recordId = ParseCsvInt(cols, 0, 0),
                    slotIndex = slotIndex,
                    slotId = slotIndex + 1,
                    resourceName = resourceName,
                    loadConditionValue = loadConditionValue,
                    playConditionValue = playConditionValue,
                });

                slotIndex++;
            }

            return rows;
        }

        private static string GetCsvCell(string[] cols, int index)
        {
            if (cols == null || index < 0 || index >= cols.Length)
                return string.Empty;
            return (cols[index] ?? string.Empty).Trim();
        }

        private static int ParseCsvInt(string[] cols, int index, int fallback)
        {
            string cell = GetCsvCell(cols, index);
            return int.TryParse(cell, out int value) ? value : fallback;
        }

        private static void ApplyRegisterUvMovieRules(List<LiveSettingsUvMovieRow> rows, HashSet<int> referencedSlotIds)
        {
            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                LiveSettingsUvMovieRow row = rows[i];
                if (row == null)
                    continue;

                switch (row.loadConditionValue)
                {
                    case -1:
                        row.isEnabledLoad = false;
                        break;
                    case 0:
                        row.isEnabledLoad = true;
                        break;
                    case 1:
                        row.isEnabledLoad = referencedSlotIds != null && referencedSlotIds.Contains(row.slotId);
                        break;
                    default:
                        row.isEnabledLoad = false;
                        break;
                }
            }
        }

        public void ReleaseLoadedTextures()
        {
            for (int i = 0; i < clips.Count; i++)
            {
                MonitorUvMovieClipData clip = clips[i];
                if (clip == null)
                    continue;

                ReleaseTextureList(clip.frameTextures, clip.ownsLoadedTextures);
                ReleaseTextureList(clip.maskTextures, clip.ownsLoadedTextures);

                if (clip.ownsLoadedTextures && clip.lightTexture != null)
                    UnityEngine.Object.Destroy(clip.lightTexture);
                if (clip.ownsLoadedTextures && clip.lightMaskTexture != null)
                    UnityEngine.Object.Destroy(clip.lightMaskTexture);

                clip.lightTexture = null;
                clip.lightMaskTexture = null;
                clip.texturePixelOffset = Vector2.zero;
                clip.texturePixelScale = Vector2.zero;
            }
        }

        private static void ReleaseTextureList(List<Texture2D> textures, bool ownsLoadedTextures)
        {
            if (textures == null)
                return;

            for (int i = 0; i < textures.Count; i++)
            {
                Texture2D tex = textures[i];
                if (tex == null || !ownsLoadedTextures)
                    continue;

                UnityEngine.Object.Destroy(tex);
            }

            textures.Clear();
        }

        private static string ResolveUvMovieRootPath()
        {
            List<string> candidates = new List<string>();

            if (Config.Instance != null && !string.IsNullOrWhiteSpace(Config.Instance.MainPath))
            {
                candidates.Add(Path.Combine(Config.Instance.MainPath, "assets", "_gallopresources", "bundle", "resources", "live", "uvmovie"));
                candidates.Add(Path.Combine(Config.Instance.MainPath, "Assets", "_gallopresources", "bundle", "resources", "live", "uvmovie"));
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            candidates.Add(Path.Combine(projectRoot, "assets", "_gallopresources", "bundle", "resources", "live", "uvmovie"));
            candidates.Add(Path.Combine(projectRoot, "Assets", "_gallopresources", "bundle", "resources", "live", "uvmovie"));

            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static List<string> FindClipFolders(string rootPath, int musicId)
        {
            string rawId = musicId.ToString();
            string padded4 = musicId.ToString("D4");

            return Directory
                .GetDirectories(rootPath)
                .Where(path =>
                {
                    string name = Path.GetFileName(path);
                    if (string.IsNullOrEmpty(name))
                        return false;

                    return name.StartsWith($"gal_uvmovie_{rawId}_", StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith($"gal_uvmovie_{padded4}_", StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith($"gal_UVmovie_{rawId}_", StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith($"gal_UVmovie_{padded4}_", StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private MonitorUvMovieClipData BuildClipData(string folderPath, bool loadTextures)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return null;

            string metadataPath = FindMetadataPath(folderPath);
            MonitorUvMovieMetadata metadata = null;
            if (!string.IsNullOrEmpty(metadataPath))
            {
                try
                {
                    metadata = JsonConvert.DeserializeObject<MonitorUvMovieMetadata>(File.ReadAllText(metadataPath));
                }
                catch (Exception ex)
                {
                    if (verboseLog)
                        Debug.LogWarning($"[MonitorUvMovieProvider] Failed to parse metadata: {metadataPath} ({ex.Message})");
                }
            }

            string folderName = Path.GetFileName(folderPath);
            string clipName = GetClipResourceName(metadata, folderName);
            List<string> imagePaths = Directory.GetFiles(folderPath)
                .Where(IsImagePath)
                .OrderBy(GetTextureSortKey)
                .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<string> framePaths = BuildOfficialStandardPathList(folderPath, clipName, metadata, false);
            List<string> maskPaths = BuildOfficialStandardPathList(folderPath, clipName, metadata, true);
            string lightPath = FindOfficialLightPath(folderPath, clipName, false);
            string lightMaskPath = FindOfficialLightPath(folderPath, clipName, true);

            //  metadata ȱʧԴٷȫһʱ˵Ŀ¼ɨ
            if (framePaths.Count == 0)
                framePaths = imagePaths.Where(IsStandardFrameTexturePath).ToList();

            if (metadata != null && metadata.ExistMaskTex)
            {
                if (maskPaths.Count == 0)
                    maskPaths = imagePaths.Where(IsStandardMaskTexturePath).ToList();
            }
            else
            {
                maskPaths.Clear();
            }

            if (string.IsNullOrEmpty(lightPath))
                lightPath = imagePaths.FirstOrDefault(IsLightTexturePath);

            if (metadata != null && metadata.ExistMaskTex)
            {
                if (string.IsNullOrEmpty(lightMaskPath))
                    lightMaskPath = imagePaths.FirstOrDefault(IsLightMaskTexturePath);
            }
            else
            {
                lightMaskPath = null;
            }

            MonitorUvMovieClipData clip = new MonitorUvMovieClipData
            {
                folderName = folderName,
                folderPath = folderPath,
                metadataPath = metadataPath,
                metadata = metadata,
                framePaths = framePaths,
                maskTexturePaths = maskPaths,
                lightTexturePath = lightPath,
                lightMaskTexturePath = lightMaskPath,
                ownsLoadedTextures = loadTextures,
            };

            if (loadTextures)
            {
                for (int i = 0; i < framePaths.Count; i++)
                {
                    Texture2D tex = LoadTextureFromFile(framePaths[i]);
                    if (tex != null)
                        clip.frameTextures.Add(tex);
                }

                for (int i = 0; i < maskPaths.Count; i++)
                {
                    Texture2D tex = LoadTextureFromFile(maskPaths[i]);
                    if (tex != null)
                        clip.maskTextures.Add(tex);
                }

                if (!string.IsNullOrEmpty(lightPath))
                    clip.lightTexture = LoadTextureFromFile(lightPath);
                if (!string.IsNullOrEmpty(lightMaskPath))
                    clip.lightMaskTexture = LoadTextureFromFile(lightMaskPath);
            }

            clip.FinalizeDerivedData();
            return clip;
        }

        private static string GetClipResourceName(MonitorUvMovieMetadata metadata, string folderName)
        {
            if (metadata != null && !string.IsNullOrWhiteSpace(metadata.Name))
                return metadata.Name.Trim();
            return folderName ?? string.Empty;
        }

        private static List<string> BuildOfficialStandardPathList(string folderPath, string clipName, MonitorUvMovieMetadata metadata, bool mask)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(clipName) || metadata == null)
                return result;

            int imageCount = Mathf.Max(metadata.ImageCount, 0);
            if (imageCount <= 0 || imageCount > 10000)
                return result;

            int digits = imageCount <= 100 ? 2 : imageCount <= 1000 ? 3 : 4;
            for (int i = 0; i < imageCount; i++)
            {
                string stem = $"{clipName}_tex_{i.ToString().PadLeft(digits, '0')}";
                if (mask)
                    stem += "_mask";

                string found = FindImageByStem(folderPath, stem);
                if (!string.IsNullOrEmpty(found))
                    result.Add(found);
            }

            return result;
        }

        private static string FindOfficialLightPath(string folderPath, string clipName, bool mask)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(clipName))
                return null;

            string stem = mask ? $"{clipName}_tex_mask" : $"{clipName}_tex";
            return FindImageByStem(folderPath, stem);
        }

        private static string FindImageByStem(string folderPath, string expectedStem)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(expectedStem) || !Directory.Exists(folderPath))
                return null;

            string[] candidates = Directory.GetFiles(folderPath)
                .Where(IsImagePath)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int i = 0; i < candidates.Length; i++)
            {
                string stem = Path.GetFileNameWithoutExtension(candidates[i]);
                if (string.Equals(stem, expectedStem, StringComparison.OrdinalIgnoreCase))
                    return candidates[i];
            }

            return null;
        }

        private static string FindMetadataPath(string folderPath)
        {
            string[] jsonFiles = Directory
                .GetFiles(folderPath, "*.json")
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (jsonFiles.Length > 0)
                return jsonFiles[0];

            string[] txtFiles = Directory
                .GetFiles(folderPath, "*.txt")
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (txtFiles.Length > 0)
                return txtFiles[0];

            return null;
        }

        private static bool IsFrameTexturePath(string path)
        {
            return IsStandardFrameTexturePath(path);
        }

        private static bool IsStandardFrameTexturePath(string path)
        {
            if (!IsImagePath(path))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.IndexOf("mask", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            int texIndex = fileName.LastIndexOf("_tex_", StringComparison.OrdinalIgnoreCase);
            if (texIndex < 0)
                return false;

            string suffix = fileName.Substring(texIndex + 5);
            return suffix.Length > 0 && suffix.All(char.IsDigit);
        }

        private static bool IsStandardMaskTexturePath(string path)
        {
            if (!IsImagePath(path))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.IndexOf("mask", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            int texIndex = fileName.LastIndexOf("_tex_", StringComparison.OrdinalIgnoreCase);
            int maskIndex = fileName.LastIndexOf("_mask", StringComparison.OrdinalIgnoreCase);
            if (texIndex < 0 || maskIndex < 0 || maskIndex <= texIndex)
                return false;

            string suffix = fileName.Substring(texIndex + 5, maskIndex - (texIndex + 5));
            return suffix.Length > 0 && suffix.All(char.IsDigit);
        }

        private static bool IsLightTexturePath(string path)
        {
            if (!IsImagePath(path))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(path);
            if (fileName.IndexOf("mask", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return fileName.EndsWith("_tex", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLightMaskTexturePath(string path)
        {
            if (!IsImagePath(path))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(path);
            return fileName.EndsWith("_tex_mask", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsImagePath(string path)
        {
            string ext = Path.GetExtension(path);
            return string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".tga", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ext, ".bmp", StringComparison.OrdinalIgnoreCase);
        }

        internal static int GetTextureSortKey(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            string digits = new string(fileName.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int key))
                return key;
            return int.MaxValue;
        }

        private static Texture2D LoadTextureFromFile(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                {
                    name = Path.GetFileNameWithoutExtension(path),
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };

                if (!texture.LoadImage(bytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                return texture;
            }
            catch
            {
                return null;
            }
        }

        private bool TryLoadClipsFromAssetBundles(int musicId, out List<MonitorUvMovieClipData> loadedClips, out string sourceName)
        {
            loadedClips = new List<MonitorUvMovieClipData>();
            sourceName = null;

            string[] folderKeys = BuildClipFolderCandidates(musicId);
            TryLoadUvMovieBundlesFromIndex(folderKeys);

            Dictionary<string, MonitorUvMovieClipData> resolved = ResolveUvMovieClipsFromLoadedBundles(folderKeys);
            if (resolved.Count == 0)
                return false;

            loadedClips = resolved.Values
                .OrderBy(c => c.folderName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            sourceName = "AssetBundle/live/uvmovie";
            return loadedClips.Count > 0;
        }

        private static string[] BuildClipFolderCandidates(int musicId)
        {
            string rawId = musicId.ToString();
            string padded4 = musicId.ToString("D4");

            return new[]
            {
                $"gal_uvmovie_{rawId}_",
                $"gal_uvmovie_{padded4}_",
                $"gal_UVmovie_{rawId}_",
                $"gal_UVmovie_{padded4}_",
            }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        }

        private void TryLoadUvMovieBundlesFromIndex(string[] folderKeys)
        {
            UmaViewerMain main = FindObjectOfType<UmaViewerMain>();
            if (main == null || main.AbList == null || folderKeys == null || folderKeys.Length == 0)
                return;

            List<UmaDatabaseEntry> entries = main.AbList
                .Where(kv =>
                {
                    UmaDatabaseEntry entry = kv.Value;
                    if (entry == null || !entry.IsAssetBundle)
                        return false;

                    return IsMatchingUvMovieEntry(kv.Key, entry, folderKeys);
                })
                .Select(kv => kv.Value)
                .Distinct()
                .ToList();

            if (verboseLog)
                Debug.Log($"[MonitorUvMovieProvider] uvmovie bundle index hits={entries.Count} for [{string.Join(",", folderKeys)}]");

            for (int i = 0; i < entries.Count; i++)
            {
                try
                {
                    UmaAssetManager.LoadAssetBundle(entries[i], neverUnload: true, isRecursive: true);
                }
                catch (Exception ex)
                {
                    if (verboseLog)
                        Debug.LogWarning($"[MonitorUvMovieProvider] Failed to load uvmovie bundle entry {entries[i]?.Name}: {ex.Message}");
                }
            }
        }

        private Dictionary<string, MonitorUvMovieClipData> ResolveUvMovieClipsFromLoadedBundles(string[] folderKeys)
        {
            Dictionary<string, MonitorUvMovieClipData> result = new Dictionary<string, MonitorUvMovieClipData>(StringComparer.OrdinalIgnoreCase);

            foreach (AssetBundle assetBundle in AssetBundle.GetAllLoadedAssetBundles())
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

                if (names == null || names.Length == 0)
                    continue;

                for (int i = 0; i < names.Length; i++)
                {
                    string assetPath = names[i];
                    string folderName = ExtractUvMovieFolderName(assetPath, folderKeys);
                    if (string.IsNullOrEmpty(folderName))
                        continue;

                    if (!result.TryGetValue(folderName, out MonitorUvMovieClipData clip))
                    {
                        clip = new MonitorUvMovieClipData
                        {
                            folderName = folderName,
                            folderPath = $"AssetBundle::{folderName}",
                            ownsLoadedTextures = false,
                        };
                        result.Add(folderName, clip);
                    }

                    if (clip.metadata == null)
                    {
                        TextAsset textAsset = TryLoadTextAsset(assetBundle, assetPath);
                        if (textAsset != null && TryParseMetadata(textAsset.text, out MonitorUvMovieMetadata metadata))
                        {
                            clip.metadata = metadata;
                            clip.metadataPath = assetPath;
                            continue;
                        }
                    }

                    Texture2D texture = TryLoadTexture(assetBundle, assetPath);
                    if (texture == null)
                        continue;

                    if (IsLightMaskTexturePath(assetPath))
                    {
                        if (clip.lightMaskTexture == null)
                        {
                            clip.lightMaskTexturePath = assetPath;
                            clip.lightMaskTexture = texture;
                        }
                        continue;
                    }

                    if (IsLightTexturePath(assetPath))
                    {
                        if (clip.lightTexture == null)
                        {
                            clip.lightTexturePath = assetPath;
                            clip.lightTexture = texture;
                        }
                        continue;
                    }

                    if (IsStandardMaskTexturePath(assetPath))
                    {
                        if (!clip.maskTexturePaths.Contains(assetPath))
                        {
                            clip.maskTexturePaths.Add(assetPath);
                            clip.maskTextures.Add(texture);
                        }
                        continue;
                    }

                    if (IsStandardFrameTexturePath(assetPath) && !clip.framePaths.Contains(assetPath))
                    {
                        clip.framePaths.Add(assetPath);
                        clip.frameTextures.Add(texture);
                    }
                }
            }

            foreach (MonitorUvMovieClipData clip in result.Values)
                clip.FinalizeDerivedData();

            return result;
        }

        private static string ExtractUvMovieFolderName(string assetPath, string[] folderKeys)
        {
            if (string.IsNullOrEmpty(assetPath) || folderKeys == null || folderKeys.Length == 0)
                return null;

            string normalized = assetPath.Replace('\\', '/');
            const string needle = "live/uvmovie/";
            int idx = normalized.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                string remain = normalized.Substring(idx + needle.Length);
                int slash = remain.IndexOf('/');
                if (slash > 0)
                {
                    string folderName = remain.Substring(0, slash);
                    for (int i = 0; i < folderKeys.Length; i++)
                    {
                        if (folderName.StartsWith(folderKeys[i], StringComparison.OrdinalIgnoreCase))
                            return folderName;
                    }
                }
            }

            for (int i = 0; i < folderKeys.Length; i++)
            {
                int folderIndex = normalized.IndexOf(folderKeys[i], StringComparison.OrdinalIgnoreCase);
                if (folderIndex < 0)
                    continue;

                string remain = normalized.Substring(folderIndex);
                int slash = remain.IndexOf('/');
                if (slash > 0)
                    return remain.Substring(0, slash);

                int dot = remain.IndexOf('.');
                if (dot > 0)
                    return remain.Substring(0, dot);

                return remain;
            }

            return null;
        }

        private static bool IsMatchingUvMovieEntry(string key, UmaDatabaseEntry entry, string[] folderKeys)
        {
            string[] candidates = { key, entry?.Name, entry?.Url };

            bool hasUvMovieSegment = false;
            bool hasFolderKey = false;
            for (int i = 0; i < candidates.Length; i++)
            {
                string normalized = NormalizePathCandidate(candidates[i]);
                if (string.IsNullOrEmpty(normalized))
                    continue;

                if (normalized.IndexOf("live/uvmovie", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf("_uvmovie_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hasUvMovieSegment = true;
                }

                for (int j = 0; j < folderKeys.Length; j++)
                {
                    if (normalized.IndexOf(folderKeys[j], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        hasFolderKey = true;
                        break;
                    }
                }

                if (hasUvMovieSegment && hasFolderKey)
                    return true;
            }

            return false;
        }

        private static string NormalizePathCandidate(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static TextAsset TryLoadTextAsset(AssetBundle assetBundle, string assetPath)
        {
            try
            {
                TextAsset asset = assetBundle.LoadAsset<TextAsset>(assetPath);
                if (LiveRuntimeDiagnostics.Enabled)
                    LiveRuntimeDiagnostics.RecordAssetLoad(assetBundle.name, "LoadAsset", typeof(TextAsset),
                        new UnityEngine.Object[] { asset }, asset, assetPath);
                return asset;
            }
            catch
            {
                return null;
            }
        }

        private static Texture2D TryLoadTexture(AssetBundle assetBundle, string assetPath)
        {
            try
            {
                Texture2D asset = assetBundle.LoadAsset<Texture2D>(assetPath);
                if (LiveRuntimeDiagnostics.Enabled)
                    LiveRuntimeDiagnostics.RecordAssetLoad(assetBundle.name, "LoadAsset", typeof(Texture2D),
                        new UnityEngine.Object[] { asset }, asset, assetPath);
                return asset;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryParseMetadata(string jsonText, out MonitorUvMovieMetadata metadata)
        {
            metadata = null;
            if (string.IsNullOrWhiteSpace(jsonText))
                return false;

            try
            {
                metadata = JsonConvert.DeserializeObject<MonitorUvMovieMetadata>(jsonText);
                return metadata != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
