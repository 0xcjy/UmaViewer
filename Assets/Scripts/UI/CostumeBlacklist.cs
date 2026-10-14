using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Blacklist de trajes "personaje -> traje sin cuerpo/modelado". Un traje sin cuerpo hace que
/// la carga reviente ("No body, can't load!"), asi que no debe aparecer en el listado ni poder
/// elegirse.
///
/// Hay DOS fuentes que se combinan:
///   1) `Assets/Resources/blacklist_trajes.json` (empaquetado) y
///      `{persistentDataPath}/blacklist_trajes.json` (generado por el menu del Editor).
///   2) El escaneo en runtime de `Main.AbList` que hace el propio listado de trajes (asi la
///      blacklist se mantiene sola si cambian los assets, sin depender del archivo).
///
/// Todo se calcula igual que la carga normal (`LoadNormalUma`), de modo que un traje se marca
/// sin cuerpo exactamente cuando cargarlo daria el error.
/// </summary>
public static class CostumeBlacklist
{
    private const string ResourceName = "blacklist_trajes";

    private static readonly HashSet<string> Entries =
        new HashSet<string>(StringComparer.Ordinal);

    private static bool _attempted;

    /// <summary>True si hay alguna entrada cargada de archivo.</summary>
    public static bool Loaded { get; private set; }

    private static string Key(int charaId, string costumeId)
    {
        return charaId + ":" + costumeId;
    }

    /// <summary>Carga (una sola vez) la lista empaquetada y la del usuario.</summary>
    public static void EnsureLoaded()
    {
        if (_attempted) return;
        _attempted = true;

        try
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset != null) ParseInto(asset.text);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CostumeBlacklist] No se pudo leer la lista empaquetada: {ex.Message}");
        }

        try
        {
            string path = Path.Combine(Application.persistentDataPath, "blacklist_trajes.json");
            if (File.Exists(path)) ParseInto(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CostumeBlacklist] No se pudo leer la lista del usuario: {ex.Message}");
        }

        Loaded = Entries.Count > 0;
    }

    /// <summary>True si (personaje, traje) esta en la blacklist de archivo.</summary>
    public static bool IsBlacklisted(int charaId, string costumeId)
    {
        if (string.IsNullOrEmpty(costumeId)) return false;
        EnsureLoaded();
        return Entries.Contains(Key(charaId, costumeId));
    }

    private static void ParseInto(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        JObject root = JObject.Parse(json);
        JObject blacklist = root["blacklist"] as JObject;
        if (blacklist == null) return;

        foreach (var charaPair in blacklist)
        {
            int charaId;
            if (!int.TryParse(charaPair.Key, out charaId)) continue;
            var obj = charaPair.Value as JObject;
            if (obj == null) continue;
            var costumes = obj["costumes"] as JArray;
            if (costumes == null) continue;

            foreach (var item in costumes)
            {
                string id = item["id"] == null ? null : item["id"].ToString();
                if (!string.IsNullOrEmpty(id)) Entries.Add(Key(charaId, id));
            }
        }
    }

    // ------------------------------------------------------------------ generacion (menu Editor + boton)

    /// <summary>Numero de personajes que se procesan por frame en BuildAndSaveRoutine.</summary>
    private const int CharaPerFrame = 12;

    private static bool DataReady(UmaViewerMain main)
    {
        return main != null && main.AbList != null && main.AbChara != null &&
               main.Costumes != null && main.Characters != null;
    }

    /// <summary>
    /// Trajes sin cuerpo de UN personaje normal (escaneo de `Main.AbList`, igual que
    /// `LoadNormalUma`). Devuelve una lista vacia si el personaje tiene todos sus cuerpos.
    /// </summary>
    private static JArray BuildCharaCostumes(UmaViewerMain main, CharaEntry chara, string bodyPath)
    {
        var costumes = new JArray();
        var bodyData = UmaDatabaseController.ReadCharaData(chara);
        if (bodyData == null) return costumes;
        string height = bodyData["height"].ToString();
        string shape = bodyData["shape"].ToString();
        string bust = bodyData["bust"].ToString();

        //--- Trajes PROPIOS (id corto).
        string nameVar = $"pfb_bdy{chara.Id}";
        foreach (var entry in main.AbChara.Where(a =>
            a.Name.StartsWith(bodyPath) && !a.Name.Contains("clothes") && a.Name.Contains(nameVar)))
        {
            string[] split = entry.Name.Split('_');
            string costumeId = split[split.Length - 1];
            int sub;
            if (!int.TryParse(costumeId, out sub)) sub = int.MinValue;
            var dress = main.Costumes.FirstOrDefault(a => a.CharaId == chara.Id && a.BodyTypeSub == sub);

            string key = bodyPath + $"bdy{chara.Id}_{costumeId}/pfb_bdy{chara.Id}_{costumeId}";
            if (!main.AbList.ContainsKey(key))
                AddCostume(costumes, costumeId, !UmaViewerUI.EsTrajeDeTest(costumeId, dress) ? "unique" : "extra", costumeId, dress);
        }

        //--- Trajes COMUNES (id XXXX_XX_XX), deduplicados.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in main.AbChara.Where(a =>
            a.Name.StartsWith(bodyPath) && !a.Name.Contains("/clothes/") && a.Name.Contains("pfb_bdy0")))
        {
            string id = Path.GetFileName(entry.Name);
            string[] split = id.Split('_');
            if (split.Length < 4) continue;
            id = split[1].Substring(3) + "_" + split[2] + "_" + split[3];
            if (!seen.Add(id)) continue;

            string[] data = id.Split('_');
            int bodytype, bodysub;
            if (!int.TryParse(data[0], out bodytype) || !int.TryParse(data[1], out bodysub))
            {
                bodytype = int.MinValue;
                bodysub = int.MinValue;
            }
            var dress = main.Costumes.FirstOrDefault(a => a.BodyType == bodytype && a.BodyTypeSub == bodysub);

            string bodyShort = id.Remove(id.LastIndexOf('_'));
            string key = bodyPath + $"bdy{bodyShort}/pfb_bdy{id}_{height}_{shape}_{bust}";
            if (!main.AbList.ContainsKey(key))
                AddCostume(costumes, id, UmaViewerUI.EsTrajeDeTest(id, dress) ? "extra" : "common", id, dress);
        }

        return costumes;
    }

    private static JObject BuildRoot(JObject blacklist)
    {
        return new JObject
        {
            ["generated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["note"] = "Trajes por personaje SIN cuerpo/modelado (no deben ofrecerse). Derivado de la comprobacion de la carga normal.",
            ["blacklist"] = blacklist
        };
    }

    /// <summary>
    /// Version SINCRONA (menu del Editor). Devuelve null si los datos aun no estan listos.
    /// </summary>
    public static string BuildJson()
    {
        var main = UmaViewerMain.Instance;
        if (!DataReady(main)) return null;

        string bodyPath = UmaDatabaseController.BodyPath;
        var blacklist = new JObject();
        foreach (var chara in main.Characters)
        {
            if (chara == null || chara.IsMob) continue;
            var costumes = BuildCharaCostumes(main, chara, bodyPath);
            if (costumes.Count > 0)
            {
                blacklist[chara.Id.ToString()] = new JObject
                {
                    ["name"] = chara.GetName(),
                    ["costumes"] = costumes
                };
            }
        }
        return BuildRoot(blacklist).ToString(Formatting.Indented);
    }

    /// <summary>
    /// Genera la blacklist repartiendo el trabajo entre frames (para que la UI pueda mostrar un
    /// aviso mientras se construye) y la guarda. Llama a `onDone(success, count, error)` al
    /// terminar. Pensada para lanzarse con `StartCoroutine` desde un MonoBehaviour.
    /// </summary>
    public static IEnumerator BuildAndSaveRoutine(Action<bool, int, string> onDone)
    {
        var main = UmaViewerMain.Instance;
        if (!DataReady(main))
        {
            onDone?.Invoke(false, 0, "The database is not loaded yet.");
            yield break;
        }

        string bodyPath = UmaDatabaseController.BodyPath;
        var blacklist = new JObject();
        int sinceYield = 0;
        foreach (var chara in main.Characters)
        {
            if (chara == null || chara.IsMob) continue;

            var costumes = BuildCharaCostumes(main, chara, bodyPath);
            if (costumes.Count > 0)
            {
                blacklist[chara.Id.ToString()] = new JObject
                {
                    ["name"] = chara.GetName(),
                    ["costumes"] = costumes
                };
            }

            if (++sinceYield >= CharaPerFrame)
            {
                sinceYield = 0;
                yield return null;   // deja respirar a la UI (el popup sigue visible)
            }
        }

        bool ok = SaveJson(BuildRoot(blacklist).ToString(Formatting.Indented), out int count, out string error);
        onDone?.Invoke(ok, count, error);
    }

    /// <summary>
    /// Version SINCRONA: construye y guarda. En el Editor escribe en
    /// `Assets/Resources/blacklist_trajes.json`; en un build, en
    /// `{persistentDataPath}/blacklist_trajes.json`.
    /// </summary>
    public static bool BuildAndSave(out int count, out string error)
    {
        count = 0;
        string json = BuildJson();
        if (json == null)
        {
            error = "The database is not loaded yet.";
            return false;
        }
        return SaveJson(json, out count, out error);
    }

    private static bool SaveJson(string json, out int count, out string error)
    {
        count = 0;
        error = null;
        try
        {
            string outPath = GetOutputPath();
            string directory = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outPath, json, new UTF8Encoding(false));

            // Refresca la tabla en memoria para la sesion actual.
            Entries.Clear();
            ParseInto(json);
            Loaded = Entries.Count > 0;
            count = Entries.Count;

            Debug.Log($"[CostumeBlacklist] Generado {outPath} ({count} entradas).");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string GetOutputPath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "Resources", "blacklist_trajes.json");
#else
        return Path.Combine(Application.persistentDataPath, "blacklist_trajes.json");
#endif
    }

    private static void AddCostume(JArray arr, string id, string kind, string costumeId, CostumeEntry dress)
    {
        arr.Add(new JObject
        {
            ["id"] = id,
            ["kind"] = kind,
            ["name"] = UmaViewerUI.GetCostumeName(costumeId, (dress == null || string.IsNullOrEmpty(dress.DressName)) ? costumeId : dress.DressName)
        });
    }
}
