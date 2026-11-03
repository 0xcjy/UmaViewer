
using System;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using System.Data;
using System.IO;
using Debug = UnityEngine.Debug;

public class UmaDatabaseController
{
    /// <summary>Path of the meta database actually in use (native meta or rebuilt meta_umaviewer).</summary>
    public string MetaDatabasePath { get; private set; }

    /// <summary>Path of the master database actually in use.</summary>
    public string MasterDatabasePath { get; private set; }

    private static UmaDatabaseController instance;

    /// <summary>
    /// Database Loader instance
    /// </summary>
    /// <summary>
    /// Drops the cached controller so the next Instance access rebuilds it from scratch.
    /// Needed after Standalone auto-rebuild: the first construction ran while the database did
    /// not exist yet, so its state (and cached exceptions) must not be reused.
    /// </summary>
    public static void ResetInstance()
    {
        instance = null;
    }

    public static UmaDatabaseController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new UmaDatabaseController();
            }
            return instance;
        }
    }

    public static string BodyPath = "3d/chara/body/";
    public static string MiniBodyPath = "3d/chara/mini/body/";
    public static string HeadPath = "3d/chara/head/";
    public static string MotionPath = "3d/motion/";
    public static string CharaPath = "3d/chara/";
    public static string EffectPath = "3d/effect/";
    public static string CostumePath = "outgame/dress/";

    public Dictionary<string, UmaDatabaseEntry> MetaEntries;
    public List<DataRow> CharaData;
    public List<DataRow> MobCharaData;
    public List<FaceTypeData> FaceTypeData;
    public List<DataRow> LiveData;
    public List<DataRow> DressData;

    // 从 master 里面取点歌机歌曲目录
    public Gallop.MasterJukeboxMusicData JukeboxMusicData;

    // 载入通用服装 ColorSet 相关
    public List<DataRow> CharaDressColor;

    /// <summary> Meta Database Connection </summary>
    public SqliteConnection metaDb;

    /// <summary> Master Database Connection </summary>
    private SqliteConnection masterDb;

    /// <summary> Loads the file database </summary>
    public UmaDatabaseController()
    {
        StandaloneLogger.Begin("UmaDatabaseController Constructor");

        try
        {
            if (Config.Instance == null)
                throw new InvalidOperationException("Config.Instance is null");

            StandaloneLogger.RecordConfig();
            StandaloneLogger.Info("Database initialization started");
            StandaloneLogger.Info("MainPath = " + Config.Instance.MainPath);
            StandaloneLogger.Info("Region = " + Config.Instance.Region);
            StandaloneLogger.Info("WorkMode = " + Config.Instance.WorkMode);

            var dbKey = Config.Instance.DBKey;

            if (Config.Instance.Region == Region.Global)
            {
                StandaloneLogger.Info("Using GlobalDBKey");
                dbKey = Config.Instance.GlobalDBKey;
            }
            else
            {
                StandaloneLogger.Info("Using Japan DBKey");
            }


            string metaPath;
            string masterPath;

            if (Config.Instance.WorkMode == WorkMode.Standalone)
            {
                string nativeMeta = Path.Combine(Config.Instance.MainPath, "meta");
                string nativeMaster = Path.Combine(
                    Config.Instance.MainPath,
                    "master",
                    "master.mdb"
                );

                // The CDN manifest rebuild loses the real entry names (they come back as
                // UTF-8 replacement characters), so prefer the game's own databases when
                // they are present and fall back to the rebuilt ones only otherwise.
                if (File.Exists(nativeMeta) && File.Exists(nativeMaster))
                {
                    metaPath = nativeMeta;
                    masterPath = nativeMaster;

                    StandaloneLogger.Info(
                        "Native game databases found; using meta + master.mdb instead of " +
                        "the CDN-rebuilt meta_umaviewer"
                    );
                }
                else
                {
                    metaPath = Path.Combine(
                        Config.Instance.MainPath,
                        "meta_umaviewer"
                    );

                    masterPath = Path.Combine(
                        Config.Instance.MainPath,
                        "master",
                        "master_umaviewer.mdb"
                    );

                    StandaloneLogger.Info(
                        "No native meta/master.mdb; falling back to the CDN-built database files"
                    );
                }
            }
            else
            {
                metaPath = Path.Combine(
                    Config.Instance.MainPath,
                    "meta"
                );

                masterPath = Path.Combine(
                    Config.Instance.MainPath,
                    "master",
                    "master.mdb"
                );
            }

            MetaDatabasePath = metaPath;
            MasterDatabasePath = masterPath;

            StandaloneLogger.RecordPath("Meta database", metaPath);
            StandaloneLogger.RecordPath("Master database", masterPath);

            if (!File.Exists(metaPath))
                throw new FileNotFoundException(
                    "Meta database not found",
                    metaPath
                );

            if (!File.Exists(masterPath))
                throw new FileNotFoundException(
                    "Master database not found",
                    masterPath
                );

            metaDb = new SqliteConnection(
                "Data Source=" + metaPath + ";"
            );

            masterDb = new SqliteConnection(
                "Data Source=" + masterPath + ";"
            );

            StandaloneLogger.Info(
                "SQLite connection objects created"
            );

            try
            {
                StandaloneLogger.Begin("Open MetaDB / ReadMeta");

                metaDb.Open();

                StandaloneLogger.Info(
                    "MetaDB opened successfully"
                );

                MetaEntries = ReadMeta(metaDb);

                StandaloneLogger.Info(
                    "ReadMeta completed. Entry count = " +
                    (MetaEntries == null ? -1 : MetaEntries.Count)
                );

                StandaloneLogger.End("Open MetaDB / ReadMeta");
            }
            catch (Exception metaReadException)
            {
                StandaloneLogger.Exception(
                    "Normal MetaDB open/read failed; trying encrypted DB fallback",
                    metaReadException
                );

                try
                {
                    var dbPath = Path.Combine(
                        Config.Instance.MainPath,
                        "meta"
                    );

                    StandaloneLogger.RecordPath(
                        "Encrypted MetaDB fallback",
                        dbPath
                    );

                    var key = GenFinalKey((byte[])dbKey.Clone());

                    StandaloneLogger.Info(
                        "Generated MetaDB decryption key; key bytes are not logged"
                    );

                    MetaEntries = ReadMetaFromEncryptedDb(
                        dbPath,
                        key,
                        3
                    );

                    StandaloneLogger.Info(
                        "Encrypted MetaDB read completed. Entry count = " +
                        (MetaEntries == null ? -1 : MetaEntries.Count)
                    );
                }
                catch (Exception encryptedReadException)
                {
                    StandaloneLogger.Exception(
                        "Encrypted MetaDB fallback failed",
                        encryptedReadException
                    );

                    throw;
                }
            }


            StandaloneLogger.Begin("Open MasterDB");

            masterDb.Open();

            StandaloneLogger.Info(
                "MasterDB opened successfully"
            );

            StandaloneLogger.End("Open MasterDB");

            StandaloneLogger.Begin("Load Master tables");

            CharaData = ReadCharaMaster(masterDb);

            StandaloneLogger.Info(
                "CharaData rows = " +
                (CharaData == null ? -1 : CharaData.Count)
            );

            MobCharaData = ReadMobCharaMaster(masterDb);

            StandaloneLogger.Info(
                "MobCharaData rows = " +
                (MobCharaData == null ? -1 : MobCharaData.Count)
            );

            FaceTypeData = ReadFaceTypeData(masterDb);

            StandaloneLogger.Info(
                "FaceTypeData rows = " +
                (FaceTypeData == null ? -1 : FaceTypeData.Count)
            );

            LiveData = ReadAllLiveData(masterDb);

            StandaloneLogger.Info(
                "LiveData rows = " +
                (LiveData == null ? -1 : LiveData.Count)
            );

            DressData = ReadAllDressData(masterDb);

            StandaloneLogger.Info(
                "DressData rows = " +
                (DressData == null ? -1 : DressData.Count)
            );

            JukeboxMusicData =
                new Gallop.MasterJukeboxMusicData(masterDb);

            StandaloneLogger.Info(
                "JukeboxMusicData initialized"
            );

            CharaDressColor = ReadAllCharaDressColor(masterDb);

            StandaloneLogger.Info(
                "CharaDressColor rows = " +
                (CharaDressColor == null ? -1 : CharaDressColor.Count)
            );

            StandaloneLogger.End("Load Master tables");

#if UNITY_STANDALONE_WIN

            if (!Config.Instance.RegionDetectionPassed)
            {
                StandaloneLogger.Info(
                    "Running game region detection"
                );

                SetDefaultGameRegion(masterDb);

                StandaloneLogger.Info(
                    "Game region detection completed"
                );
            }

#endif
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception(
                "Database initialization failed",
                ex
            );

            Debug.LogError(
                "Database initialization failed: " + ex
            );

            CloseAllConnection();

#if UNITY_ANDROID || UNITY_IOS || UNITY_IPHONE

            var msg =
                $"Mobile database initialization failed at: {Config.Instance.MainPath}\n" +
                $"{ex.GetType().Name}: {ex.Message}";

            WriteMobileDatabaseErrorLog(ex);

#else
            // Standalone rebuilds the database by itself (see UmaViewerMain), so reaching this
            // point means the rebuild failed. Stop telling users to switch to `Default` - that
            // advice is meaningless and it used to appear on Android too.
            var msg = $"Database unavailable at: {Config.Instance.MainPath}";

            if (Config.Instance.WorkMode == WorkMode.Standalone)
            {
                msg +=
                    "\nAutomatic rebuild did not produce a usable database. " +
                    "See the log for the step that failed.";
            }
            else
            {
                msg +=
                    "\nPlease make sure the game client is installed and follow the instructions on GitHub.";
            }


#endif

            StandaloneLogger.Error(
                "Database initialization aborted. User message: " + msg
            );

            UmaViewerUI.Instance?.ShowMessage(
                msg,
                UIMessageType.Error
            );
        }
        finally
        {
            StandaloneLogger.End(
                "UmaDatabaseController Constructor"
            );
        }
    }

    private static void WriteMobileDatabaseErrorLog(Exception ex)
    {
#if UNITY_ANDROID || UNITY_IOS || UNITY_IPHONE

        try
        {
            var logPath = Path.Combine(
                Config.Instance.MainPath,
                "MobileDatabaseInitError.txt"
            );

            File.WriteAllText(logPath, ex.ToString());
        }
        catch
        {
        }

#endif
    }

    private static void EnsureMobileDatabaseFiles()
    {
        Directory.CreateDirectory(Config.Instance.MainPath);

        CopyBuiltinDatabaseToFile(
            "Database/meta",
            Path.Combine(Config.Instance.MainPath, "meta")
        );

        CopyBuiltinDatabaseToFile(
            "Database/master.mdb",
            Path.Combine(
                Config.Instance.MainPath,
                "master",
                "master.mdb"
            )
        );
    }

    private static void CopyBuiltinDatabaseToFile(
        string resourcePath,
        string targetPath
    )
    {
        var asset =
            UnityEngine.Resources.Load<UnityEngine.TextAsset>(resourcePath);

        if (asset == null)
        {
            throw new FileNotFoundException(
                $"Built-in database resource not found: Resources/{resourcePath}.bytes"
            );
        }

        try
        {
            var bytes = asset.bytes;
            var target = new FileInfo(targetPath);

            if (target.Exists && target.Length == bytes.LongLength)
            {
                MarkNoBackup(targetPath);
                return;
            }

            var directory = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                MarkNoBackup(directory);
            }

            var tempPath = targetPath + ".tmp";

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            File.WriteAllBytes(tempPath, bytes);

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tempPath, targetPath);

            MarkNoBackup(targetPath);
        }
        finally
        {
            UnityEngine.Resources.UnloadAsset(asset);
        }
    }

    private static void MarkNoBackup(string path)
    {
#if UNITY_IOS || UNITY_IPHONE
        UnityEngine.iOS.Device.SetNoBackupFlag(path);
#endif
    }

    static List<DataRow> ReadAllCharaDressColor(SqliteConnection conn)
    {
        StandaloneLogger.Begin("ReadAllCharaDressColor");

        SqliteCommand sqlite_cmd = conn.CreateCommand();

        sqlite_cmd.CommandText =
            "SELECT name FROM sqlite_master WHERE type='table' AND name='chara_dress_color_set'";

        SqliteDataReader sqlite_datareader =
            sqlite_cmd.ExecuteReader();

        bool HasTable = sqlite_datareader.HasRows;

        sqlite_datareader.Close();
        sqlite_cmd.Dispose();

        if (!HasTable)
        {
            StandaloneLogger.Warning(
                "Table chara_dress_color_set does not exist"
            );

            StandaloneLogger.End("ReadAllCharaDressColor");

            return new List<DataRow>();
        }

        var result = ReadMaster(
            conn,
            "SELECT * FROM chara_dress_color_set"
        );

        StandaloneLogger.Info(
            "CharaDressColor rows = " + result.Count
        );

        StandaloneLogger.End("ReadAllCharaDressColor");

        return result;
    }

    /// <summary>
    /// Reads the per-entry decryption key from column `e`.
    /// The game's own meta stores this as TEXT (SQLite gives every column dynamic typing),
    /// while GetInt64 throws on TEXT — that exception was swallowed per row, so reading the
    /// native meta ended with 0 entries and the viewer hung on "Loading Live Data".
    /// </summary>
    private static long ReadSignedKey(SqliteDataReader reader, int ordinal)
    {
        object raw = reader.GetValue(ordinal);

        if (raw == null || raw == DBNull.Value) return 0L;

        switch (raw)
        {
            case long l: return l;
            case int i: return i;
            case short s: return s;
            case byte b: return b;
            case ulong ul when ul <= long.MaxValue: return (long)ul;
            case double d: return (long)d;
        }

        string text = Convert.ToString(raw);

        return long.TryParse(text, out long parsed) ? parsed : 0L;
    }

    static Dictionary<string, UmaDatabaseEntry> ReadMeta(
        SqliteConnection conn
    )
    {
        StandaloneLogger.Begin("ReadMeta");

        bool HasEncryption = false;

        SqliteCommand sqlite_cmd = conn.CreateCommand();

        sqlite_cmd.CommandText =
            "SELECT name FROM pragma_table_info('a') WHERE name = 'e'";

        SqliteDataReader sqlite_datareader =
            sqlite_cmd.ExecuteReader();

        HasEncryption = sqlite_datareader.HasRows;

        sqlite_datareader.Close();
        sqlite_cmd.Dispose();

        StandaloneLogger.Info(
            "MetaDB encryption column detected = " + HasEncryption
        );

        sqlite_cmd = conn.CreateCommand();

        if (HasEncryption)
        {
            sqlite_cmd.CommandText =
                "SELECT m,n,h,d,e FROM a WHERE d IS NOT NULL";
        }
        else
        {
            sqlite_cmd.CommandText =
                "SELECT m,n,h,d FROM a WHERE d IS NOT NULL";
        }

        sqlite_datareader = sqlite_cmd.ExecuteReader();

        Dictionary<string, UmaDatabaseEntry> meta =
            new Dictionary<string, UmaDatabaseEntry>();

        int rowCount = 0;
        int skippedCount = 0;

        while (sqlite_datareader.Read())
        {
            rowCount++;

            UmaDatabaseEntry entry = null;

            try
            {
                var typestr = sqlite_datareader.GetString(0);

                if (Enum.TryParse<UmaFileType>(
                    typestr,
                    false,
                    out var type
                ))
                {
                    var name = sqlite_datareader.GetString(1);
                    var url = sqlite_datareader.GetString(2);
                    var prerequisites = sqlite_datareader.GetString(3);
                    var key = 0L;

                    if (HasEncryption)
                    {
                        key = ReadSignedKey(sqlite_datareader, 4);
                    }

                    if (string.IsNullOrEmpty(name))
                    {
                        throw new Exception(
                            $"Invalid entry name {name} or URL {url}"
                        );
                    }

                    entry = new UmaDatabaseEntry
                    {
                        Type = type,
                        Name = name,
                        Url = url,
                        Prerequisites = prerequisites,
                        Key = key
                    };
                }
                else
                {
                    skippedCount++;

                    Debug.LogWarning(
                        $"Unrecognized EntryType Enum Value :{typestr}"
                    );
                }
            }
            catch (Exception e)
            {
                skippedCount++;

                Debug.LogError(
                    $"Error caught while reading Entry : \n {e}"
                );
            }

            if (entry != null && !meta.ContainsKey(entry.Name))
            {
                meta.Add(entry.Name, entry);
            }
        }

        sqlite_datareader.Close();
        sqlite_cmd.Dispose();

        StandaloneLogger.Info(
            "ReadMeta database rows = " + rowCount
        );

        StandaloneLogger.Info(
            "ReadMeta skipped rows = " + skippedCount
        );

        StandaloneLogger.Info(
            "ReadMeta parsed entries = " + meta.Count
        );

        StandaloneLogger.End("ReadMeta");

        return meta;
    }

    public static Dictionary<string, UmaDatabaseEntry>
        ReadMetaFromEncryptedDb(
            string dbPath,
            byte[] keyBytes,
            int cipherIndex = -1
        )
    {
        StandaloneLogger.Begin("ReadMetaFromEncryptedDb");

        StandaloneLogger.RecordPath(
            "Encrypted MetaDB",
            dbPath
        );

        StandaloneLogger.Info(
            "Cipher index = " + cipherIndex +
            "; key length = " +
            (keyBytes == null ? -1 : keyBytes.Length)
        );

        var meta =
            new Dictionary<string, UmaDatabaseEntry>(StringComparer.Ordinal);

        IntPtr db = IntPtr.Zero;

        try
        {
            db = Sqlite3MC.Open(dbPath);

            StandaloneLogger.Info(
                "Encrypted SQLite handle opened"
            );

            if (cipherIndex >= 0)
            {
                try
                {
                    int cfgRc =
                        Sqlite3MC.MC_Config(db, "cipher", cipherIndex);

                    StandaloneLogger.Info(
                        "MC_Config cipher return code = " + cfgRc
                    );
                }
                catch (Exception e)
                {
                    StandaloneLogger.Exception(
                        "MC_Config failed",
                        e
                    );

                    Debug.LogError(
                        $"MC_Config thrown: {e}"
                    );
                }
            }

            int rcKey =
                Sqlite3MC.Key_SetBytes(db, keyBytes);

            if (rcKey != Sqlite3MC.SQLITE_OK)
            {
                string em = Sqlite3MC.GetErrMsg(db);

                throw new InvalidOperationException(
                    $"sqlite3_key returned rc={rcKey}, errmsg={em}"
                );
            }

            StandaloneLogger.Info(
                "sqlite3_key completed successfully"
            );

            if (!Sqlite3MC.ValidateReadable(
                db,
                out string validateErr
            ))
            {
                StandaloneLogger.Error(
                    "Encrypted MetaDB validation failed: " + validateErr
                );

                Debug.LogError(
                    $"DB validation after key failed: {validateErr}"
                );

                throw new InvalidOperationException(
                    "DB validation after key failed: " + validateErr
                );
            }

            StandaloneLogger.Info(
                "Encrypted MetaDB validation succeeded"
            );

            string sql = "SELECT m,n,h,c,d,e FROM a";

            Sqlite3MC.ForEachRow(sql, db, (stmt) =>
            {
                try
                {
                    string m = Sqlite3MC.ColumnText(stmt, 0);
                    string n = Sqlite3MC.ColumnText(stmt, 1);
                    string h = Sqlite3MC.ColumnText(stmt, 2);
                    string c = Sqlite3MC.ColumnText(stmt, 3);
                    string d = Sqlite3MC.ColumnText(stmt, 4);
                    long e = Sqlite3MC.ColumnInt64(stmt, 5);

                    if (string.IsNullOrEmpty(m))
                    {
                        Debug.LogWarning(
                            "Skipping row: empty type string (m)."
                        );

                        return;
                    }

                    if (!Enum.TryParse<UmaFileType>(
                        m,
                        false,
                        out UmaFileType type
                    ))
                    {
                        Debug.LogWarning(
                            $"Unrecognized EntryType Enum Value :{m}"
                        );

                        return;
                    }

                    if (string.IsNullOrEmpty(n))
                    {
                        Debug.LogError(
                            $"Invalid entry name '{n}' or URL '{h}'. Skipping row."
                        );

                        return;
                    }

                    var entry = new UmaDatabaseEntry
                    {
                        Type = type,
                        Name = n,
                        Url = h,
                        Checksum = c,
                        Prerequisites = d,
                        Key = e
                    };

                    if (!meta.ContainsKey(entry.Name))
                    {
                        meta.Add(entry.Name, entry);
                    }
                }
                catch (Exception exRow)
                {
                    StandaloneLogger.Exception(
                        "Error while reading encrypted MetaDB row",
                        exRow
                    );

                    Debug.LogError(
                        "Error caught while reading row: " + exRow
                    );
                }
            });

            StandaloneLogger.Info(
                "Encrypted MetaDB entries parsed = " + meta.Count
            );
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception(
                "ReadMetaFromEncryptedDb failed",
                ex
            );

            Debug.LogError(
                "ReadMetaFromEncryptedDb failed: " + ex
            );

            throw;
        }
        finally
        {
            if (db != IntPtr.Zero)
            {
                try
                {
                    Sqlite3MC.Close(db);

                    StandaloneLogger.Info(
                        "Encrypted SQLite handle closed"
                    );
                }
                catch (Exception e)
                {
                    StandaloneLogger.Exception(
                        "Closing encrypted DB failed",
                        e
                    );

                    Debug.LogError(
                        "Closing DB failed: " + e
                    );
                }
            }
        }

        StandaloneLogger.End("ReadMetaFromEncryptedDb");

        return meta;
    }

    /// <summary>
    /// Lee la tabla `a` (meta) de OTRO cliente (p.ej. el Global) sin tocar las conexiones
    /// activas: intenta primero sqlite normal y, si falla, la via encriptada (sqlite3mc).
    /// Devuelve null si la carpeta no tiene meta o no se pudo leer.
    /// </summary>
    public static Dictionary<string, UmaDatabaseEntry> ReadMetaFromFolder(string folder, byte[] dbKey)
    {
        if (string.IsNullOrEmpty(folder)) return null;

        string metaPath = Path.Combine(folder, "meta");
        if (!File.Exists(metaPath)) return null;

        try
        {
            using (var conn = new SqliteConnection($@"Data Source={metaPath};"))
            {
                conn.Open();
                return ReadMeta(conn);
            }
        }
        catch (Exception)
        {
            // El meta del cliente Global va encriptado (sqlite3mc): se intenta abajo.
        }

        try
        {
            byte[] effectiveKey = (dbKey == null || dbKey.Length == 0)
                ? Config.Instance.GlobalDBKey
                : dbKey;
            var key = Instance.GenFinalKey((byte[])effectiveKey.Clone());
            return ReadMetaFromEncryptedDb(metaPath, key, 3);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[UmaDatabaseController] No se pudo leer el meta de " + folder + ": " + ex.Message);
            return null;
        }
    }

    static void SetDefaultGameRegion(SqliteConnection conn)
    {
        StandaloneLogger.Begin("SetDefaultGameRegion");

        var str = ReadMaster(
            conn,
            "SELECT text FROM text_data LIMIT 1"
        );

        if (str == null || str.Count == 0 ||
            str[0] == null || str[0][0] == DBNull.Value)
        {
            throw new InvalidOperationException(
                "Cannot detect game region: text_data returned no usable row"
            );
        }

        bool isAscii =
            str[0][0].ToString()[0] < 128;

        Region detectedRegion =
            isAscii ? Region.Global : Region.Jp;

        StandaloneLogger.Info(
            "Detected region = " + detectedRegion
        );

        StandaloneLogger.Info(
            "Configured region = " + Config.Instance.Region
        );

        if (Config.Instance.Region != detectedRegion)
        {
            if (detectedRegion == Region.Global)
            {
                Popup.Create(
                    $"Different game region was detected. Current region is {Config.Instance.Region}.\nDo you want to change it? (It can be changed again in `Other` settings)",
                    -1,
                    200,
                    "Set to Global (Steam)",
                    () =>
                    {
                        Config.Instance.RegionDetectionPassed = true;
                        Config.Instance.Region = detectedRegion;
                        Config.Instance.UpdateConfig(false);
                        UmaViewerUI.Instance.OtherSettings.ApplySettings();
                        UmaViewerUI.Instance.StartCoroutine(
                            UmaViewerUI.Instance.ApplyGraphicsSettings()
                        );
                    },
                    "Keep Japan (DMM)",
                    () =>
                    {
                        Config.Instance.RegionDetectionPassed = true;
                        Config.Instance.UpdateConfig(false);
                    }
                );
            }
            else
            {
                Popup.Create(
                    $"Different game region was detected. Current region is {Config.Instance.Region}.\nDo you want to change it? (It can be changed again in `Other` settings)",
                    -1,
                    200,
                    "Set to Japan (DMM)",
                    () =>
                    {
                        Config.Instance.RegionDetectionPassed = true;
                        Config.Instance.Region = detectedRegion;
                        Config.Instance.UpdateConfig(false);
                        UmaViewerUI.Instance.OtherSettings.ApplySettings();
                        UmaViewerUI.Instance.StartCoroutine(
                            UmaViewerUI.Instance.ApplyGraphicsSettings()
                        );
                    },
                    "Keep Global (Steam)",
                    () =>
                    {
                        Config.Instance.RegionDetectionPassed = true;
                        Config.Instance.UpdateConfig(false);
                    }
                );
            }
        }

        StandaloneLogger.End("SetDefaultGameRegion");
    }

    static List<DataRow> ReadCharaMaster(SqliteConnection conn)
    {
        return ReadMaster(
            conn,
            "SELECT * FROM chara_data C,(SELECT D.'index' charaid,D.'text' charaname FROM text_data D WHERE id like 6) T WHERE C.id like T.charaid"
        );
    }

    static List<DataRow> ReadMobCharaMaster(SqliteConnection conn)
    {
        return ReadMaster(
            conn,
            "SELECT * FROM mob_data M,(SELECT D.'index' charaid,D.'text' charaname FROM text_data D WHERE id like 59) T WHERE M.mob_id like T.charaid"
        );
    }

    static List<DataRow> ReadMaster(
        SqliteConnection conn,
        string sql
    )
    {
        List<DataRow> dr = new List<DataRow>();

        using (var sqlite_cmd = conn.CreateCommand())
        {
            sqlite_cmd.CommandText = sql;

            using var sqlite_datareader =
                sqlite_cmd.ExecuteReader();

            var result = new DataTable();

            for (int i = 0; i < sqlite_datareader.FieldCount; i++)
            {
                result.Columns.Add(
                    sqlite_datareader.GetName(i),
                    sqlite_datareader.GetFieldType(i)
                );
            }

            while (sqlite_datareader.Read())
            {
                DataRow row = result.NewRow();

                for (int i = 0; i < sqlite_datareader.FieldCount; i++)
                {
                    row[i] = sqlite_datareader[i];
                }

                result.Rows.Add(row);
                dr.Add(row);
            }
        }

        return dr;
    }

    static List<FaceTypeData> ReadFaceTypeData(SqliteConnection conn)
    {
        List<FaceTypeData> data = new List<FaceTypeData>();

        using (SqliteCommand sqlite_cmd = conn.CreateCommand())
        {
            sqlite_cmd.CommandText =
                "SELECT * FROM face_type_data";

            using (SqliteDataReader sqlite_datareader =
                sqlite_cmd.ExecuteReader())
            {
                while (sqlite_datareader.Read())
                {
                    FaceTypeData entry = new FaceTypeData()
                    {
                        label = sqlite_datareader.GetString(0),
                        eyebrow_l = sqlite_datareader.GetString(1),
                        eyebrow_r = sqlite_datareader.GetString(2),
                        eye_l = sqlite_datareader.GetString(3),
                        eye_r = sqlite_datareader.GetString(4),
                        mouth = sqlite_datareader.GetString(5),
                        mouth_shape_type = sqlite_datareader.GetInt32(6),
                        inverce_face_type = sqlite_datareader.GetString(7),
                        set_face_group = sqlite_datareader.GetInt32(8),
                    };

                    data.Add(entry);
                }
            }
        }

        return data;
    }

    static List<DataRow> ReadAllLiveData(SqliteConnection conn)
    {
        return ReadMaster(
            conn,
            $"SELECT * FROM live_data L,(SELECT D.'index' songid,D.'text' songname FROM text_data D WHERE id like 16) T WHERE L.music_id like T.songid"
        );
    }

    static List<DataRow> ReadAllDressData(SqliteConnection conn)
    {
        return ReadMaster(
            conn,
            $"SELECT * FROM dress_data C,(SELECT D.'index' dressid,D.'text' dressname FROM text_data D WHERE id like 14) T WHERE C.id like T.dressid"
        );
    }

    public static DataRow ReadCharaData(CharaEntry chara)
    {
        if (chara.IsMob)
        {
            return instance.MobCharaData.Find(
                e => Convert.ToInt32(e["mob_id"]) == chara.Id
            );
        }

        return instance.CharaData.Find(
            e => Convert.ToInt32(e["id"]) == chara.Id
        );
    }

    public static DataRow ReadMobDressColor(string mobid)
    {
        var results = ReadMaster(
            instance.masterDb,
            $"SELECT * FROM mob_dress_color_set WHERE id LIKE {mobid}"
        );

        return results.Count > 0 ? results[0] : null;
    }

    public static DataRow ReadMobHairColor(string colorid)
    {
        var results = ReadMaster(
            instance.masterDb,
            $"SELECT * FROM mob_hair_color_set WHERE id LIKE {colorid}"
        );

        foreach (var data in results)
        {
            return data;
        }

        return null;
    }

    public void CloseAllConnection()
    {
        StandaloneLogger.Begin("CloseAllConnection");

        JukeboxMusicData?.Unload();
        JukeboxMusicData = null;

        masterDb?.Close();
        metaDb?.Close();

        masterDb?.Dispose();
        metaDb?.Dispose();

        SqliteConnection.ClearAllPools();

        GC.Collect();
        GC.WaitForPendingFinalizers();

        StandaloneLogger.Info(
            "Database connections closed and disposed"
        );

        StandaloneLogger.End("CloseAllConnection");
    }

    public byte[] GenFinalKey(byte[] key)
    {
        StandaloneLogger.Begin("GenFinalKey");

        var baseKey = Config.Instance.DBBaseKey;

        StandaloneLogger.Info(
            "Input key length = " +
            (key == null ? -1 : key.Length)
        );

        StandaloneLogger.Info(
            "Base key length = " +
            (baseKey == null ? -1 : baseKey.Length)
        );

        if (key == null)
            throw new ArgumentNullException(nameof(key));

        if (baseKey == null || baseKey.Length < 13)
            throw new Exception("Invalid Base Key length");

        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)(key[i] ^ baseKey[i % 13]);
        }

        StandaloneLogger.Info(
            "Final DB key generated; key material omitted from log"
        );

        StandaloneLogger.End("GenFinalKey");

        return key;
    }
}
