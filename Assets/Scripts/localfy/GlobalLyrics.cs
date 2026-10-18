using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Letras (lyrics) en ingles de las canciones live, leidas del cliente GLOBAL.
///
/// Por que: las lyrics salen de los bundles `live/musicscores/m{song}/m{song}_lyrics` del
/// cliente activo. El cliente JP las trae en japones; el Global, en ingles. La traduccion de
/// nombres NO cubre las lyrics (no estan en `text_data`), asi que aqui se guarda la letra EN
/// por cancion y se usa SOLO cuando el idioma es English.
///
/// Fuentes (en este orden):
///   1) `Assets/Resources/GlobalLyrics.json`, empaquetado en el repo.
///   2) `{persistentDataPath}/GlobalLyrics.json`, generado por el boton "UpdateDatabase".
///
/// Si no hay lista, `LoadLiveLyrics` conserva las lyrics del cliente activo (las japonesas).
/// </summary>
public static class GlobalLyrics
{
    // songId -> lista de lineas (time en segundos, texto)
    private static Dictionary<int, List<UmaLyricsData>> _songs;
    private static bool _attempted;

    /// <summary>Carga (una sola vez) la lista empaquetada y la del usuario.</summary>
    public static void EnsureLoaded()
    {
        if (_attempted) return;
        _attempted = true;
        _songs = new Dictionary<int, List<UmaLyricsData>>();

        try
        {
            var asset = Resources.Load<TextAsset>("GlobalLyrics");
            if (asset != null) Merge(Parse(asset.text));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GlobalLyrics] No se pudo leer la lista empaquetada: {ex.Message}");
        }

        try
        {
            string path = Path.Combine(Application.persistentDataPath, "GlobalLyrics.json");
            if (File.Exists(path)) Merge(Parse(File.ReadAllText(path)));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GlobalLyrics] No se pudo leer la lista del usuario: {ex.Message}");
        }
    }

    /// <summary>True si hay lyrics EN para esa cancion.</summary>
    public static bool TryGet(int songId, out List<UmaLyricsData> lyrics)
    {
        EnsureLoaded();
        lyrics = null;
        return _songs != null && _songs.TryGetValue(songId, out lyrics) && lyrics != null && lyrics.Count > 0;
    }

    private static void Merge(Dictionary<int, List<UmaLyricsData>> source)
    {
        if (source == null) return;
        foreach (var kv in source) _songs[kv.Key] = kv.Value;
    }

    // ------------------------------------------------------------------ generacion (Editor)

    /// <summary>
    /// Construye la lista de lyrics EN a partir de la carpeta del cliente GLOBAL. Solo funciona
    /// en el Editor (leer y desencriptar los bundles del global no se hace en un build).
    /// Devuelve false y `error` con el motivo si no se pudo.
    /// </summary>
    public static bool BuildFromGlobalFolder(string folder, out string error)
    {
        error = null;
#if UNITY_EDITOR
        string persistent = ResolvePersistent(folder);
        if (persistent == null)
        {
            error = "Could not find the GLOBAL 'Persistent' folder.";
            return false;
        }

        Dictionary<string, UmaDatabaseEntry> meta =
            UmaDatabaseController.ReadMetaFromFolder(persistent, Config.Instance.GlobalDBKey);
        if (meta == null)
        {
            error = "Could not read the GLOBAL 'meta' database.";
            return false;
        }

        var songs = new Dictionary<int, List<UmaLyricsData>>();
        int scanned = 0;
        foreach (var kv in meta)
        {
            string name = kv.Key;
            if (string.IsNullOrEmpty(name)) continue;
            if (!name.StartsWith("live/musicscores/") || !name.EndsWith("_lyrics")) continue;

            int songId;
            if (!TryParseSongId(name, out songId)) continue;
            scanned++;

            List<UmaLyricsData> lyrics = TryReadLyricsBundle(persistent, kv.Value);
            if (lyrics != null && lyrics.Count > 0) songs[songId] = lyrics;
        }

        if (songs.Count == 0)
        {
            error = scanned == 0
                ? "The GLOBAL database did not contain any live lyrics."
                : "No se pudo leer ninguna lyric del GLOBAL.";
            return false;
        }

        string outPath = GetOutputPath();
        try
        {
            string directory = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string tmpPath = outPath + ".tmp";
            File.WriteAllText(tmpPath, Serialize(songs), new UTF8Encoding(false));
            if (File.Exists(outPath)) File.Delete(outPath);
            File.Move(tmpPath, outPath);
        }
        catch (Exception ex)
        {
            error = "Could not save the lyrics list: " + ex.Message;
            return false;
        }

        // Refresca la tabla en memoria para la sesion actual.
        _songs = songs;
        _attempted = true;

        return true;
#else
        error = "Building the lyrics list is only supported in the Editor.";
        return false;
#endif
    }

#if UNITY_EDITOR
    private static string GetOutputPath()
    {
        return Path.Combine(Application.dataPath, "Resources", "GlobalLyrics.json");
    }

    // Acepta tanto la carpeta .../Persistent como su carpeta padre ..._Data.
    private static string ResolvePersistent(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return null;
        if (File.Exists(Path.Combine(folder, "meta"))) return folder;
        string nested = Path.Combine(folder, "Persistent");
        if (File.Exists(Path.Combine(nested, "meta"))) return nested;
        return null;
    }

    // "live/musicscores/m1001/m1001_lyrics" -> 1001
    private static bool TryParseSongId(string name, out int songId)
    {
        songId = 0;
        int slash = name.LastIndexOf('/');
        if (slash < 0) return false;
        string file = name.Substring(slash + 1);   // m1001_lyrics
        int underscore = file.IndexOf('_');
        if (underscore <= 1) return false;
        string digits = file.Substring(1, underscore - 1);  // sin la 'm'
        return int.TryParse(digits, out songId);
    }

    private static List<UmaLyricsData> TryReadLyricsBundle(string persistent, UmaDatabaseEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.Url)) return null;

        string url = entry.Url.Replace("\\", "/");
        if (url.Length < 2) return null;
        string filePath = Path.Combine(persistent, "dat", url.Substring(0, 2), url).Replace("\\", "/");
        if (!File.Exists(filePath)) return null;

        AssetBundle bundle = null;
        UmaAssetBundleStream stream = null;
        try
        {
            if (entry.IsEncrypted)
            {
                stream = new UmaAssetBundleStream(filePath, entry.FKey);
                bundle = AssetBundle.LoadFromStream(stream);
            }
            else
            {
                bundle = AssetBundle.LoadFromFile(filePath);
            }
            if (bundle == null) return null;

            string assetName = Path.GetFileNameWithoutExtension(entry.Name);
            TextAsset text = bundle.LoadAsset<TextAsset>(assetName);
            if (text == null || string.IsNullOrEmpty(text.text)) return null;

            return ParseLyricsText(text.text);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GlobalLyrics] No se pudo leer '{entry.Name}': {ex.Message}");
            return null;
        }
        finally
        {
            if (bundle != null) bundle.Unload(true);
            stream?.Dispose();
        }
    }

    // Mismo formato que el cliente: lineas "ms,texto" (la 1a linea es cabecera).
    private static List<UmaLyricsData> ParseLyricsText(string content)
    {
        var lyrics = new List<UmaLyricsData>();
        string[] lines = content.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            string[] words = lines[i].Split(',');
            if (words.Length == 0) continue;
            try
            {
                lyrics.Add(new UmaLyricsData
                {
                    time = float.Parse(words[0]) / 1000f,
                    text = (words.Length > 1) ? words[1] : ""
                });
            }
            catch { }
        }
        return lyrics;
    }

    private static string Serialize(Dictionary<int, List<UmaLyricsData>> songs)
    {
        var root = new JObject();
        foreach (var kv in songs)
        {
            var arr = new JArray();
            foreach (var line in kv.Value)
            {
                arr.Add(new JObject
                {
                    ["t"] = Mathf.RoundToInt(line.time * 1000f),
                    ["x"] = line.text
                });
            }
            root[kv.Key.ToString()] = arr;
        }
        return root.ToString(Formatting.None);
    }
#endif

    private static Dictionary<int, List<UmaLyricsData>> Parse(string json)
    {
        var result = new Dictionary<int, List<UmaLyricsData>>();
        if (string.IsNullOrEmpty(json)) return result;

        JObject root = JObject.Parse(json);
        foreach (var songPair in root)
        {
            int songId;
            if (!int.TryParse(songPair.Key, out songId)) continue;
            var arr = songPair.Value as JArray;
            if (arr == null) continue;

            var lyrics = new List<UmaLyricsData>();
            foreach (var item in arr)
            {
                var obj = item as JObject;
                if (obj == null) continue;
                lyrics.Add(new UmaLyricsData
                {
                    time = (obj["t"] == null ? 0f : obj["t"].ToObject<float>()) / 1000f,
                    text = obj["x"] == null ? "" : obj["x"].ToString()
                });
            }
            if (lyrics.Count > 0) result[songId] = lyrics;
        }
        return result;
    }
}
