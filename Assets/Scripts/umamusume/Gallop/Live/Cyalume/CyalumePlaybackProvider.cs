
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Gallop.Live;

namespace Gallop.Live.Cyalume
{
    [Serializable]
    public class CyalumePlaybackData
    {
        public int DataNo;
        public int PatternId;
        public float StartTime;
        public float PlaySpeed = 1f;
        public int ChoreographyType;
    }

    [Serializable]
    public class CyalumePatternDefinition
    {
        public int PatternId;
        public string MoveType;
        public string ColorPattern;
        public string[] Colors = Array.Empty<string>();
        public string[] Widths = Array.Empty<string>();
        public string Signature;
    }

    [Serializable]
    public class CyalumePartFrame
    {
        public float StartTime;
        public int[] States = new int[7];
        public float[] Volumes = new float[7];
        public float[] Pans = new float[7];
        public float VolumeRate = 1f;

        public int GetState(int zoneIndex)
        {
            if (States == null || zoneIndex < 0 || zoneIndex >= States.Length)
                return 0;
            return States[zoneIndex];
        }

        public bool IsActive(int zoneIndex)
        {
            return GetState(zoneIndex) != 0;
        }
    }

    [DisallowMultipleComponent]
    public class CyalumePlaybackProvider : MonoBehaviour
    {
        [Header("Loaded playback data used by the controller.")]
        public List<CyalumePlaybackData> data = new List<CyalumePlaybackData>();

        [Header("Debug: unique choreography pattern definitions built from the CSV.")]
        public List<CyalumePatternDefinition> patternDefinitions = new List<CyalumePatternDefinition>();

        [Header("Loaded part data.")]
        public List<CyalumePartFrame> partFrames = new List<CyalumePartFrame>();

        [Header("Auto-load mXXXX_cyalume.csv for the current live.")]
        public bool autoLoadForCurrentLive = true;
        public bool preferResourcesCsv = true;
        public bool tryLoadBundleEntryFromIndex = true;
        public bool verboseLog = true;
        public float bpmReference = 140f;
        public bool keepPreviousPatternOnPause = true;

        [SerializeField] private int _loadedMusicId;
        [SerializeField] private string _loadedCsvSource;
        [SerializeField] private string _loadedPartCsvSource;

        public int LoadedMusicId => _loadedMusicId;
        public string LoadedCsvSource => _loadedCsvSource;
        public string LoadedPartCsvSource => _loadedPartCsvSource;

        private void Start()
        {
            if (!autoLoadForCurrentLive || _loadedMusicId > 0 || (data != null && data.Count > 0))
                return;

            if (Director.instance != null && Director.instance.live != null)
            {
                int resolvedMusicId = Director.instance.live.MusicId;
                if (resolvedMusicId > 0)
                    InitializeForMusicId(resolvedMusicId);
            }
        }

        public bool InitializeForMusicId(int musicId, bool forceReload = false)
        {
            if (musicId <= 0)
            {
                if (verboseLog)
                    Debug.LogWarning("[CyalumePlaybackProvider] InitializeForMusicId failed: invalid musicId.");
                return false;
            }

            if (!forceReload && _loadedMusicId == musicId && data.Count > 0)
                return true;

            data.Clear();
            patternDefinitions.Clear();
            partFrames.Clear();

            _loadedMusicId = musicId;
            _loadedCsvSource = null;
            _loadedPartCsvSource = null;

            if (!TryLoadCsvText(musicId, out string csvText, out string sourceName) || string.IsNullOrEmpty(csvText))
            {
                if (verboseLog)
                    Debug.LogWarning($"[CyalumePlaybackProvider] Failed to resolve cyalume CSV for musicId={musicId}.");
                return false;
            }

            _loadedCsvSource = sourceName;
            BuildPlaybackDataFromCsv(csvText);

            if (TryLoadPartCsvText(musicId, out string partCsvText, out string partSource) && !string.IsNullOrEmpty(partCsvText))
            {
                _loadedPartCsvSource = partSource;
                BuildPartFramesFromCsv(partCsvText);
                if (verboseLog)
                    Debug.Log($"[CyalumePlaybackProvider] Loaded part CSV: musicId={musicId}, source={partSource}, rows={partFrames.Count}");
            }

            if (verboseLog)
            {
                Debug.Log($"[CyalumePlaybackProvider] Loaded cyalume CSV: musicId={musicId}, source={sourceName}, rows={data.Count}, uniquePatterns={patternDefinitions.Count}");
            }

            return data.Count > 0;
        }

        public bool TryGetCurrent(float liveTime, out CyalumePlaybackData current)
        {
            current = null;
            if (data == null || data.Count == 0)
                return false;

            for (int i = 0; i < data.Count; i++)
            {
                var item = data[i];
                if (item == null)
                    continue;

                if (item.StartTime <= liveTime)
                    current = item;
                else
                    break;
            }

            return current != null;
        }

        public bool TryGetCurrentPartFrame(float liveTime, out CyalumePartFrame current)
        {
            current = null;
            if (partFrames == null || partFrames.Count == 0)
                return false;

            for (int i = 0; i < partFrames.Count; i++)
            {
                var item = partFrames[i];
                if (item == null)
                    continue;

                if (item.StartTime <= liveTime)
                    current = item;
                else
                    break;
            }

            return current != null;
        }

        public bool TryGetCurrentState(float liveTime, out int patternId, out float patternStartTime, out float playSpeed, out int choreographyType)
        {
            if (TryGetCurrent(liveTime, out var current) && current != null)
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

        public bool TryGetPatternDefinition(int patternId, out CyalumePatternDefinition definition)
        {
            definition = null;
            if (patternDefinitions == null || patternDefinitions.Count == 0)
                return false;

            for (int i = 0; i < patternDefinitions.Count; i++)
            {
                var item = patternDefinitions[i];
                if (item != null && item.PatternId == patternId)
                {
                    definition = item;
                    return true;
                }
            }

            return false;
        }

        public bool IsRandomPattern(int patternId)
        {
            return TryGetPatternDefinition(patternId, out var definition) &&
                   !string.IsNullOrEmpty(definition.ColorPattern) &&
                   definition.ColorPattern.StartsWith("Random", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildPrimaryLiveKey(int musicId)
        {
            return $"m{musicId}";
        }

        public static string[] BuildLiveKeyCandidates(int musicId)
        {
            var keys = new List<string>();
            string raw = $"m{musicId}";
            keys.Add(raw);

            string padded4 = $"m{musicId:D4}";
            if (!keys.Any(x => string.Equals(x, padded4, StringComparison.OrdinalIgnoreCase)))
            {
                keys.Add(padded4);
            }

            return keys.ToArray();
        }

        private bool TryLoadCsvText(int musicId, out string csvText, out string sourceName)
        {
            csvText = null;
            sourceName = null;

            string[] liveKeys = BuildLiveKeyCandidates(musicId);
            string[] resourcePaths = BuildResourcePathCandidates(liveKeys);

            if (preferResourcesCsv)
            {
                for (int i = 0; i < resourcePaths.Length; i++)
                {
                    string resourcePath = resourcePaths[i];
                    if (string.IsNullOrEmpty(resourcePath))
                        continue;

                    TextAsset asset = null;
                    try
                    {
                        asset = Resources.Load<TextAsset>(resourcePath);
                    }
                    catch
                    {
                        asset = null;
                    }

                    if (asset != null && !string.IsNullOrEmpty(asset.text))
                    {
                        csvText = asset.text;
                        sourceName = $"Resources/{resourcePath}";
                        return true;
                    }
                }
            }

            if (TryFindCsvInLoadedBundles(liveKeys, out csvText, out sourceName))
                return true;

            if (tryLoadBundleEntryFromIndex)
            {
                TryLoadCsvBundlesFromIndex(liveKeys);
                if (TryFindCsvInLoadedBundles(liveKeys, out csvText, out sourceName))
                    return true;
            }

            return false;
        }

        private bool TryLoadPartCsvText(int musicId, out string csvText, out string sourceName)
        {
            csvText = null;
            sourceName = null;

            string[] liveKeys = BuildLiveKeyCandidates(musicId);

            for (int i = 0; i < liveKeys.Length; i++)
            {
                string liveKey = liveKeys[i];
                string[] candidates =
                {
                    $"live/musicscores/{liveKey}/{liveKey}_part",
                    $"live/musicscores/{liveKey}/{liveKey}_part.csv",
                    $"{liveKey}_part"
                };

                for (int j = 0; j < candidates.Length; j++)
                {
                    TextAsset asset = null;
                    try
                    {
                        asset = Resources.Load<TextAsset>(candidates[j]);
                    }
                    catch
                    {
                        asset = null;
                    }

                    if (asset != null && !string.IsNullOrEmpty(asset.text))
                    {
                        csvText = asset.text;
                        sourceName = $"Resources/{candidates[j]}";
                        return true;
                    }
                }
            }

            if (TryFindAuxCsvInLoadedBundles(liveKeys, "part", out csvText, out sourceName))
                return true;

            if (tryLoadBundleEntryFromIndex)
            {
                TryLoadAuxCsvBundlesFromIndex(liveKeys, "part");
                if (TryFindAuxCsvInLoadedBundles(liveKeys, "part", out csvText, out sourceName))
                    return true;
            }

            return false;
        }

        private static string[] BuildResourcePathCandidates(string[] liveKeys)
        {
            var candidates = new List<string>();
            for (int i = 0; i < liveKeys.Length; i++)
            {
                string liveKey = liveKeys[i];
                candidates.Add($"live/musicscores/{liveKey}/{liveKey}_cyalume");
                candidates.Add($"live/musicscores/{liveKey}/{liveKey}_cyalume.csv");
                candidates.Add($"{liveKey}_cyalume");
            }
            return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private void TryLoadCsvBundlesFromIndex(string[] liveKeys)
        {
            var main = FindObjectOfType<UmaViewerMain>();
            if (main == null || main.AbList == null)
                return;

            var entries = main.AbList.Values
                .Where(e => e != null && !string.IsNullOrEmpty(e.Name))
                .Where(e => liveKeys.Any(liveKey =>
                    e.Name.IndexOf($"live/musicscores/{liveKey}/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    e.Name.IndexOf($"{liveKey}_cyalume", StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            foreach (var entry in entries)
            {
                try
                {
                    UmaAssetManager.LoadAssetBundle(entry, neverUnload: true, isRecursive: true);
                }
                catch (Exception ex)
                {
                    if (verboseLog)
                    {
                        Debug.LogWarning($"[CyalumePlaybackProvider] Failed to load CSV bundle entry {entry?.Name}: {ex.Message}");
                    }
                }
            }
        }

        private void TryLoadAuxCsvBundlesFromIndex(string[] liveKeys, string suffix)
        {
            var main = FindObjectOfType<UmaViewerMain>();
            if (main == null || main.AbList == null)
                return;

            var entries = main.AbList.Values
                .Where(e => e != null && !string.IsNullOrEmpty(e.Name))
                .Where(e => liveKeys.Any(liveKey =>
                    e.Name.IndexOf($"live/musicscores/{liveKey}/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    e.Name.IndexOf($"{liveKey}_{suffix}", StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            foreach (var entry in entries)
            {
                try
                {
                    UmaAssetManager.LoadAssetBundle(entry, neverUnload: true, isRecursive: true);
                }
                catch (Exception ex)
                {
                    if (verboseLog)
                    {
                        Debug.LogWarning($"[CyalumePlaybackProvider] Failed to load {suffix} CSV bundle entry {entry?.Name}: {ex.Message}");
                    }
                }
            }
        }

        private bool TryFindCsvInLoadedBundles(string[] liveKeys, out string csvText, out string sourceName)
        {
            csvText = null;
            sourceName = null;

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

                if (names == null || names.Length == 0)
                    continue;

                for (int i = 0; i < names.Length; i++)
                {
                    string path = names[i];
                    if (!IsMatchingCyalumeCsvPath(path, liveKeys))
                        continue;

                    TextAsset textAsset = null;
                    try
                    {
                        textAsset = assetBundle.LoadAsset<TextAsset>(path);
                    }
                    catch
                    {
                        textAsset = null;
                    }

                    if (textAsset == null || string.IsNullOrEmpty(textAsset.text))
                        continue;

                    csvText = textAsset.text;
                    sourceName = path;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindAuxCsvInLoadedBundles(string[] liveKeys, string suffix, out string csvText, out string sourceName)
        {
            csvText = null;
            sourceName = null;

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

                if (names == null || names.Length == 0)
                    continue;

                for (int i = 0; i < names.Length; i++)
                {
                    string path = names[i];
                    if (!IsMatchingAuxCsvPath(path, liveKeys, suffix))
                        continue;

                    TextAsset textAsset = null;
                    try
                    {
                        textAsset = assetBundle.LoadAsset<TextAsset>(path);
                    }
                    catch
                    {
                        textAsset = null;
                    }

                    if (textAsset == null || string.IsNullOrEmpty(textAsset.text))
                        continue;

                    csvText = textAsset.text;
                    sourceName = path;
                    return true;
                }
            }

            return false;
        }

        private static bool IsMatchingCyalumeCsvPath(string path, string[] liveKeys)
        {
            if (string.IsNullOrEmpty(path) || liveKeys == null || liveKeys.Length == 0)
                return false;

            string normalized = path.Replace('\\', '/');
            for (int i = 0; i < liveKeys.Length; i++)
            {
                string liveKey = liveKeys[i];
                if (normalized.IndexOf($"live/musicscores/{liveKey}/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(normalized);
                if (string.Equals(fileName, $"{liveKey}_cyalume", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsMatchingAuxCsvPath(string path, string[] liveKeys, string suffix)
        {
            if (string.IsNullOrEmpty(path) || liveKeys == null || liveKeys.Length == 0)
                return false;

            string normalized = path.Replace('\\', '/');
            for (int i = 0; i < liveKeys.Length; i++)
            {
                string liveKey = liveKeys[i];
                if (normalized.IndexOf($"live/musicscores/{liveKey}/", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(normalized);
                if (string.Equals(fileName, $"{liveKey}_{suffix}", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void BuildPlaybackDataFromCsv(string csvText)
        {
            data.Clear();
            patternDefinitions.Clear();

            if (string.IsNullOrEmpty(csvText))
                return;

            var rows = ParseCsvRows(csvText);
            if (rows.Count <= 1)
                return;

            var headerMap = BuildHeaderMap(rows[0]);
            if (!headerMap.ContainsKey("time"))
            {
                if (verboseLog)
                    Debug.LogWarning("[CyalumePlaybackProvider] CSV missing time column.");
                return;
            }

            var patternMap = new Dictionary<string, int>(StringComparer.Ordinal);
            int lastVisualPatternId = 0;
            bool hasLastVisualPattern = false;

            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row == null || row.Count == 0)
                    continue;

                if (!TryParseTimeSeconds(GetCell(row, headerMap, "time"), out float startTime))
                    continue;

                string moveType = NormalizeCell(GetCell(row, headerMap, "move_type"));
                string colorPattern = NormalizeCell(GetCell(row, headerMap, "color_pattern"));
                string[] colors = GetSeriesCells(row, headerMap, "color", 5);
                string[] widths = GetSeriesCells(row, headerMap, "width", 5);

                int choreographyType = ComputeChoreographyType(moveType);
                bool isPauseLike = keepPreviousPatternOnPause && choreographyType >= 8;

                int patternId;
                if (isPauseLike)
                {
                    patternId = hasLastVisualPattern ? lastVisualPatternId : 0;
                }
                else
                {
                    string signature = BuildPatternSignature(moveType, colorPattern, colors, widths);
                    if (!patternMap.TryGetValue(signature, out patternId))
                    {
                        patternId = patternMap.Count;
                        patternMap[signature] = patternId;

                        patternDefinitions.Add(new CyalumePatternDefinition
                        {
                            PatternId = patternId,
                            MoveType = moveType,
                            ColorPattern = colorPattern,
                            Colors = colors.ToArray(),
                            Widths = widths.ToArray(),
                            Signature = signature,
                        });
                    }

                    lastVisualPatternId = patternId;
                    hasLastVisualPattern = true;
                }

                float playSpeed = ComputePlaySpeed(GetCell(row, headerMap, "bpm"));

                data.Add(new CyalumePlaybackData
                {
                    DataNo = data.Count,
                    PatternId = patternId,
                    StartTime = startTime,
                    PlaySpeed = playSpeed,
                    ChoreographyType = choreographyType,
                });
            }

            data.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            for (int i = 0; i < data.Count; i++)
            {
                data[i].DataNo = i;
            }
        }

        private void BuildPartFramesFromCsv(string csvText)
        {
            partFrames.Clear();
            if (string.IsNullOrEmpty(csvText))
                return;

            var rows = ParseCsvRows(csvText);
            if (rows.Count <= 1)
                return;

            var headerMap = BuildHeaderMap(rows[0]);
            if (!headerMap.ContainsKey("time"))
            {
                if (verboseLog)
                    Debug.LogWarning("[CyalumePlaybackProvider] Part CSV missing time column.");
                return;
            }

            string[] zoneNames = { "left3", "left2", "left", "center", "right", "right2", "right3" };

            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row == null || row.Count == 0)
                    continue;

                if (!TryParseTimeSeconds(GetCell(row, headerMap, "time"), out float startTime))
                    continue;

                var frame = new CyalumePartFrame();
                frame.StartTime = startTime;

                for (int i = 0; i < zoneNames.Length; i++)
                {
                    frame.States[i] = ParseIntOrDefault(GetCell(row, headerMap, zoneNames[i]), 0);
                    frame.Volumes[i] = ParseFloatOrSentinel(GetCell(row, headerMap, zoneNames[i] + "_vol"));
                    frame.Pans[i] = ParseFloatOrSentinel(GetCell(row, headerMap, zoneNames[i] + "_pan"));
                }

                frame.VolumeRate = ParseFloatOrDefault(GetCell(row, headerMap, "volume_rate"), 1f);
                partFrames.Add(frame);
            }

            partFrames.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        }

        private static Dictionary<string, int> BuildHeaderMap(List<string> header)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (header == null)
                return map;

            for (int i = 0; i < header.Count; i++)
            {
                string key = NormalizeHeader(header[i]);
                if (string.IsNullOrEmpty(key) || map.ContainsKey(key))
                    continue;
                map[key] = i;
            }

            return map;
        }

        private static string NormalizeHeader(string value)
        {
            return (value ?? string.Empty).Trim().Trim('\ufeff').ToLowerInvariant();
        }

        private static string GetCell(List<string> row, Dictionary<string, int> headerMap, string key)
        {
            if (row == null || headerMap == null || !headerMap.TryGetValue(key, out int index))
                return null;

            return index >= 0 && index < row.Count ? row[index] : null;
        }

        private static string[] GetSeriesCells(List<string> row, Dictionary<string, int> headerMap, string prefix, int count)
        {
            var values = new string[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = NormalizeCell(GetCell(row, headerMap, $"{prefix}{i + 1}"));
            }
            return values;
        }

        private static string NormalizeCell(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string BuildPatternSignature(string moveType, string colorPattern, string[] colors, string[] widths)
        {
            var parts = new List<string>(2 + (colors?.Length ?? 0) + (widths?.Length ?? 0))
            {
                NormalizeSignatureValue(moveType),
                NormalizeSignatureValue(colorPattern)
            };

            if (colors != null)
            {
                for (int i = 0; i < colors.Length; i++)
                    parts.Add(NormalizeSignatureValue(colors[i]));
            }

            if (widths != null)
            {
                for (int i = 0; i < widths.Length; i++)
                    parts.Add(NormalizeSignatureValue(widths[i]));
            }

            return string.Join("|", parts);
        }

        private static string NormalizeSignatureValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim().ToLowerInvariant();
        }

        private bool TryParseTimeSeconds(string rawValue, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            rawValue = rawValue.Trim();
            if (int.TryParse(rawValue, out int milliseconds))
            {
                seconds = milliseconds / 1000f;
                return true;
            }

            if (float.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
            {
                seconds = value > 1000f ? (value / 1000f) : value;
                return true;
            }

            return false;
        }

        private float ComputePlaySpeed(string bpmValue)
        {
            float fallback = 1f;
            if (string.IsNullOrWhiteSpace(bpmValue))
                return fallback;

            if (!float.TryParse(bpmValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float bpm))
                return fallback;

            if (bpm <= 0f)
                return fallback;

            float reference = bpmReference > 0f ? bpmReference : 140f;
            return Mathf.Max(0.01f, bpm / reference);
        }

        private static int ComputeChoreographyType(string moveType)
        {
            if (string.IsNullOrWhiteSpace(moveType))
                return 0;

            return string.Equals(moveType.Trim(), "Pause", StringComparison.OrdinalIgnoreCase) ? 8 : 0;
        }

        private static int ParseIntOrDefault(string raw, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            return int.TryParse(raw.Trim(), out int value) ? value : fallback;
        }

        private static float ParseFloatOrDefault(string raw, float fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            return float.TryParse(raw.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value)
                ? value
                : fallback;
        }

        private static float ParseFloatOrSentinel(string raw)
        {
            float value = ParseFloatOrDefault(raw, 999f);
            return Mathf.Approximately(value, 999f) ? float.NaN : value;
        }

        private static List<List<string>> ParseCsvRows(string text)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(text))
                return rows;

            var row = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        bool escapedQuote = i + 1 < text.Length && text[i + 1] == '"';
                        if (escapedQuote)
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        row.Add(cell.ToString());
                        cell.Length = 0;
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(cell.ToString());
                        cell.Length = 0;
                        rows.Add(row);
                        row = new List<string>();
                        break;
                    default:
                        cell.Append(c);
                        break;
                }
            }

            row.Add(cell.ToString());
            if (row.Count > 1 || !string.IsNullOrEmpty(row[0]))
                rows.Add(row);

            return rows;
        }
    }
}
