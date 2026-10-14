using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mono.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Traduce NOMBRES japoneses a ingleses usando las tablas de la instalacion GLOBAL del juego.
///
/// Por que funciona: las bases master de JP y Global comparten los mismos identificadores
/// (`text_data`.`index` por categoria). El JP tiene mas datos, pero en japones; el Global
/// tiene menos filas, pero en ingles. Asi que para cada nombre JP se busca el mismo
/// (categoria, index) en el global y, si existe, se usa su texto en ingles.
///
/// Fuentes (en este orden):
///   1) `Assets/Resources/GlobalNames.json`, empaquetado en el repo. Permite traducir SIN
///      tener instalado el global.
///   2) `{persistentDataPath}/GlobalNames.json`, la lista que el usuario genera desde el
///      boton "UpdateDatabase" en una build (en el Editor ese boton reescribe la (1)).
///   3) Si ademas existe la carpeta del global (Config.GlobalNamesPath o autodetectada),
///      se lee su `master.mdb` y se superpone (datos mas nuevos).
///
/// Solo NOMBRES: no toca assets, carga ni logica. Si no hay ninguna fuente, todo cae de
/// vuelta al texto japones original.
///
/// Categorias de text_data usadas:
///   6  = nombres de personaje
///   14 = nombres de traje
///   16 = nombres de cancion
///   59 = nombres de mob
/// </summary>
public static class GlobalNames
{
    public const int CategoryChara = 6;
    public const int CategoryDress = 14;
    public const int CategorySong = 16;
    public const int CategoryMob = 59;

    private static readonly int[] Categories = { CategoryChara, CategoryDress, CategorySong, CategoryMob };

    // Umbral minimo para dar por buena una base seleccionada a mano. Evita aceptar una
    // carpeta equivocada que casualmente tenga un master.mdb casi vacio.
    private const int MinTotalEntries = 50;

    private static readonly Dictionary<int, Dictionary<int, string>> Tables =
        new Dictionary<int, Dictionary<int, string>>();

    /// <summary>True si hay alguna tabla cargada (empaquetada, generada o global).</summary>
    public static bool Loaded { get; private set; }

    /// <summary>Carpeta (Persistent) del global que se uso para refrescar; null si no se uso.</summary>
    public static string SourcePath { get; private set; }

    private static bool _attempted;

    /// <summary>
    /// Carga (una sola vez) los nombres ingleses: lista empaquetada, lista generada por el
    /// usuario y, si existe, el master global para refrescar/superponer.
    /// </summary>
    public static void EnsureLoaded(string configuredPath)
    {
        if (_attempted) return;
        _attempted = true;

        LoadBundledResource();
        LoadPersistentOverride();

        string path = ResolvePath(configuredPath);
        if (!string.IsNullOrEmpty(path))
            TryLoadFromGlobalMaster(path);

        Loaded = Tables.Count > 0;
    }

    // ------------------------------------------------------------------ (update) generacion

    /// <summary>
    /// Construye la lista de nombres EN a partir de la carpeta global indicada y, SOLO si
    /// sale bien, reemplaza la lista anterior. Devuelve false y `error` con el motivo si la
    /// carpeta no es valida o no parece la version en ingles.
    /// </summary>
    public static bool BuildFromGlobalFolder(string folder, out string error)
    {
        error = null;

        string dbPath = FindMasterDb(folder);
        if (dbPath == null)
        {
            error = "Could not find 'master/master.mdb' in the selected folder.";
            return false;
        }

        var built = new Dictionary<int, Dictionary<int, string>>();
        if (!TryReadTables(dbPath, built, out error))
            return false;

        int total = 0;
        foreach (var kv in built) total += kv.Value.Count;

        if (total < MinTotalEntries)
        {
            error = $"The selected database looks incomplete ({total} names found).";
            return false;
        }
        if (!LooksEnglish(built))
        {
            error = "The selected database does not look like the English (Global) one.";
            return false;
        }

        // A partir de aqui la lista es valida: se escribe en un temporal y, solo cuando esta
        // completo, se borra la anterior y se pone la nueva.
        string outPath = GetOutputPath();
        try
        {
            string directory = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string tmpPath = outPath + ".tmp";
            File.WriteAllText(tmpPath, SerializeTables(built), new UTF8Encoding(false));
            if (File.Exists(outPath)) File.Delete(outPath);
            File.Move(tmpPath, outPath);
        }
        catch (Exception ex)
        {
            error = "Could not save the list: " + ex.Message;
            return false;
        }

        // Refresca la tabla en memoria para la sesion actual.
        Tables.Clear();
        foreach (var kv in built) Tables[kv.Key] = kv.Value;
        Loaded = true;
        SourcePath = folder;

        Debug.Log($"[GlobalNames] Lista actualizada desde {dbPath} -> {outPath} ({total} nombres).");
        return true;
    }

    /// <summary>En el Editor actualiza la lista del repo (Resources); en build, una copia del usuario.</summary>
    private static string GetOutputPath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "Resources", "GlobalNames.json");
#else
        return Path.Combine(Application.persistentDataPath, "GlobalNames.json");
#endif
    }

    /// <summary>True si la carpeta tiene un master.mdb/master_umaviewer.mdb usable.</summary>
    public static bool IsValidFolder(string folder)
    {
        return FindMasterDb(folder) != null;
    }

    private static string FindMasterDb(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return null;

        // Se acepta tanto la carpeta .../Persistent como su carpeta padre ..._Data.
        string[] baseDirs = { folder, Path.Combine(folder, "Persistent") };
        foreach (string baseDir in baseDirs)
        {
            if (!Directory.Exists(baseDir)) continue;
            string path = Path.Combine(baseDir, "master", "master.mdb");
            if (File.Exists(path)) return path;
            path = Path.Combine(baseDir, "master", "master_umaviewer.mdb");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static bool TryReadTables(string dbPath, Dictionary<int, Dictionary<int, string>> target, out string error)
    {
        error = null;
        try
        {
            using (var conn = new SqliteConnection($@"Data Source={dbPath};"))
            {
                conn.Open();
                foreach (int category in Categories)
                {
                    var table = new Dictionary<int, string>();
                    using (var cmd = conn.CreateCommand())
                    {
                        // category es un int constante: seguro para interpolar.
                        cmd.CommandText = $"SELECT \"index\", text FROM text_data WHERE id = {category}";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;
                                int index = Convert.ToInt32(reader.GetValue(0));
                                string text = reader.GetString(1);
                                if (!string.IsNullOrEmpty(text)) table[index] = text;
                            }
                        }
                    }
                    target[category] = table;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "Could not read the database: " + ex.Message;
            return false;
        }
    }

    // Guarda contra elegir la base equivocada: los nombres EN son casi todos ASCII, los JP no.
    private static bool LooksEnglish(Dictionary<int, Dictionary<int, string>> tables)
    {
        long ascii = 0, nonAscii = 0;
        foreach (var category in Categories)
        {
            if (!tables.TryGetValue(category, out var table)) continue;
            foreach (var pair in table)
            {
                string text = pair.Value;
                if (string.IsNullOrEmpty(text)) continue;
                for (int i = 0; i < text.Length; i++)
                {
                    if (text[i] < 128) ascii++;
                    else nonAscii++;
                }
            }
        }
        if (ascii + nonAscii == 0) return false;
        return ascii >= (ascii + nonAscii) * 8 / 10;   // >= 80% ASCII
    }

    // ------------------------------------------------------------------ carga de fuentes

    private static void LoadBundledResource()
    {
        try
        {
            var asset = Resources.Load<TextAsset>("GlobalNames");
            if (asset == null)
            {
                Debug.LogWarning("[GlobalNames] No esta el recurso Resources/GlobalNames.json.");
                return;
            }
            MergeTables(ParseJson(asset.text));
            Debug.Log($"[GlobalNames] Lista empaquetada cargada " +
                      $"(chara={Count(CategoryChara)}, dress={Count(CategoryDress)}, " +
                      $"song={Count(CategorySong)}, mob={Count(CategoryMob)}).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GlobalNames] No se pudo leer la lista empaquetada: {ex.Message}");
        }
    }

    private static void LoadPersistentOverride()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "GlobalNames.json");
            if (!File.Exists(path)) return;
            MergeTables(ParseJson(File.ReadAllText(path)));
            Debug.Log($"[GlobalNames] Lista del usuario cargada desde {path} " +
                      $"(chara={Count(CategoryChara)}, dress={Count(CategoryDress)}, " +
                      $"song={Count(CategorySong)}, mob={Count(CategoryMob)}).");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GlobalNames] No se pudo leer la lista del usuario: {ex.Message}");
        }
    }

    private static void TryLoadFromGlobalMaster(string path)
    {
        string dbPath = FindMasterDb(path);
        if (dbPath == null) return;

        var built = new Dictionary<int, Dictionary<int, string>>();
        if (!TryReadTables(dbPath, built, out string error))
        {
            Debug.LogWarning($"[GlobalNames] No se pudo leer el master global ({path}): {error}");
            return;
        }

        MergeTables(built);
        SourcePath = path;
        Debug.Log($"[GlobalNames] Refrescado desde el global {dbPath} " +
                  $"(chara={Count(CategoryChara)}, dress={Count(CategoryDress)}, " +
                  $"song={Count(CategorySong)}, mob={Count(CategoryMob)}).");
    }

    private static Dictionary<int, Dictionary<int, string>> ParseJson(string json)
    {
        var result = new Dictionary<int, Dictionary<int, string>>();
        var root = JObject.Parse(json);
        foreach (var categoryPair in root)
        {
            int category;
            if (!int.TryParse(categoryPair.Key, out category)) continue;
            var obj = categoryPair.Value as JObject;
            if (obj == null) continue;

            var table = new Dictionary<int, string>();
            foreach (var namePair in obj)
            {
                int index;
                if (!int.TryParse(namePair.Key, out index)) continue;
                string text = namePair.Value == null ? null : namePair.Value.ToString();
                if (!string.IsNullOrEmpty(text)) table[index] = text;
            }
            result[category] = table;
        }
        return result;
    }

    private static string SerializeTables(Dictionary<int, Dictionary<int, string>> tables)
    {
        return JsonConvert.SerializeObject(tables, Formatting.None);
    }

    private static void MergeTables(Dictionary<int, Dictionary<int, string>> source)
    {
        foreach (var categoryPair in source)
        {
            var table = GetOrCreateTable(categoryPair.Key);
            foreach (var namePair in categoryPair.Value)
                table[namePair.Key] = namePair.Value;
        }
    }

    private static Dictionary<int, string> GetOrCreateTable(int category)
    {
        if (!Tables.TryGetValue(category, out var table))
        {
            table = new Dictionary<int, string>();
            Tables[category] = table;
        }
        return table;
    }

    private static int Count(int category)
    {
        return Tables.TryGetValue(category, out var table) ? table.Count : 0;
    }

    public static bool TryGet(int category, int index, out string name)
    {
        name = null;
        return Tables.TryGetValue(category, out var table) && table.TryGetValue(index, out name);
    }

    /// <summary>Nombre ingles para (categoria, index), o `fallback` si no esta en el global.</summary>
    public static string Get(int category, int index, string fallback)
    {
        return TryGet(category, index, out var name) ? name : fallback;
    }

    private static string ResolvePath(string configuredPath)
    {
        if (!string.IsNullOrEmpty(configuredPath) && Directory.Exists(configuredPath))
            return configuredPath;

        //Candidatos habituales de la instalacion GLOBAL por Steam. Si el juego esta en otra
        //biblioteca, se puede generar la lista desde el boton o fijar la ruta en Config.json.
        string[] candidates =
        {
            @"C:\Program Files (x86)\Steam\steamapps\common\UmamusumePrettyDerby\UmamusumePrettyDerby_Data\Persistent",
            @"C:\Program Files\Steam\steamapps\common\UmamusumePrettyDerby\UmamusumePrettyDerby_Data\Persistent",
        };

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "master", "master.mdb")))
                return candidate;
        }

        return null;
    }
}
