using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Chinese live lyrics from the hachimi zh-CN pack.
///
/// Each song is a JSON map of "milliseconds" -> "japanese line\nchinese line"; the second line
/// is used when present. Lookup order: memory cache -> translations/lyrics next to the player
/// -> downloaded cache -> network (a blocking GET, only reached for songs never seen before).
/// </summary>
public static class LocalizeLyrics
{
    public const string BaseUrl = "https://fuwadl.yingqwq.cn/zh-cn/root";

    private static readonly Dictionary<int, List<UmaLyricsData>> Cache =
        new Dictionary<int, List<UmaLyricsData>>();

    private static string LocalDirectory
    {
        get
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;

            return string.IsNullOrEmpty(root) ? null : Path.Combine(root, "translations", "lyrics");
        }
    }

    private static string DownloadedDirectory =>
        Path.Combine(Application.persistentDataPath, "lyrics");

    public static bool TryGet(int songId, out List<UmaLyricsData> lyrics)
    {
        lyrics = null;

        if (songId <= 0) return false;

        lock (Cache)
        {
            if (Cache.TryGetValue(songId, out lyrics) && lyrics != null && lyrics.Count > 0)
            {
                return true;
            }
        }

        string fileName = "m" + songId + "_lyrics.json";

        // Bundled lyrics first (packed under Resources; Android has no translations/ folder).
        TextAsset bundled = Resources.Load<TextAsset>("Localize/lyrics/m" + songId + "_lyrics");
        string json = bundled != null ? bundled.text : null;

        string local = LocalDirectory;

        if (string.IsNullOrEmpty(json))
        {
            json = string.IsNullOrEmpty(local) ? null : ReadFile(Path.Combine(local, fileName));
        }

        if (string.IsNullOrEmpty(json))
        {
            json = ReadFile(Path.Combine(DownloadedDirectory, fileName));
        }

        if (string.IsNullOrEmpty(json))
        {
            // Never seen before: fetch it now (blocking, ~1s) and remember it on disk.
            json = UmaViewerDownload.DownloadTextSync(BaseUrl + "/assets/lyrics/" + fileName);

            if (!string.IsNullOrEmpty(json))
            {
                TryWriteFile(Path.Combine(DownloadedDirectory, fileName), json);
            }
        }

        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            lyrics = Parse(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LocalizeLyrics] could not parse " + fileName + ": " + ex.Message);
            return false;
        }

        if (lyrics == null || lyrics.Count == 0) return false;

        lock (Cache)
        {
            Cache[songId] = lyrics;
        }

        return true;
    }

    private static List<UmaLyricsData> Parse(string json)
    {
        JObject root = JObject.Parse(json);

        List<UmaLyricsData> result = new List<UmaLyricsData>();

        foreach (JProperty property in root.Properties())
        {
            if (!long.TryParse(property.Name, out long millis)) continue;

            string value = property.Value?.ToString() ?? string.Empty;

            int split = value.IndexOf('\n');

            if (split >= 0)
            {
                string translated = value.Substring(split + 1).Trim();

                if (!string.IsNullOrEmpty(translated))
                {
                    value = translated;
                }
            }

            result.Add(new UmaLyricsData { time = millis / 1000f, text = value.Trim() });
        }

        result.Sort((a, b) => a.time.CompareTo(b.time));

        return result;
    }

    private static string ReadFile(string path)
    {
        try
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LocalizeLyrics] read failed: " + ex.Message);
            return null;
        }
    }

    private static void TryWriteFile(string path, string content)
    {
        try
        {
            string directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(path, content);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LocalizeLyrics] cache write failed: " + ex.Message);
        }
    }
}
