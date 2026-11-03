
using Mono.Data.Sqlite;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using static BSVReader;
using static ManifestCategory;

public class ManifestDB
{
    public SqliteConnection MetaDB;
    public static string DBPath;
    public Action<string, UIMessageType> callback;
    private bool isError;
    private bool bulkWriteConfigured;

    // A single multi-thousand-row transaction spills the SQLite page cache and then
    // fsyncs every page write, which on Windows (antivirus scanning the db) stalled the
    // Default Manifest insert for minutes. Commit in batches instead.
    private const int ManifestInsertBatchSize = 2000;

    public ManifestDB()
    {
        StandaloneLogger.Begin("ManifestDB Constructor");

        DBPath = $"{Config.Instance.MainPath}/meta_umaviewer";
        ManifestDB.DBPath = DBPath;

        StandaloneLogger.RecordPath("MetaDB", DBPath);
        StandaloneLogger.RecordDirectory(
            "MetaDB directory",
            Path.GetDirectoryName(DBPath));

        if (!File.Exists(DBPath))
        {
            StandaloneLogger.Info("MetaDB does not exist. Creating directory.");
            Directory.CreateDirectory(Path.GetDirectoryName(DBPath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(DBPath));

        try
        {
            StandaloneLogger.Info("Opening SQLite MetaDB");

            MetaDB = new SqliteConnection($@"Data Source={DBPath}");
            MetaDB.Open();

            StandaloneLogger.Info("SQLite state: " + MetaDB.State);

            SqliteCommand cmd = new SqliteCommand("SELECT name FROM sqlite_master");
            cmd.Connection = MetaDB;

            var reader = cmd.ExecuteReader();
            var hasrows = reader.HasRows;
            reader.Close();

            StandaloneLogger.Info("SQLite has existing schema: " + hasrows);

            if (!hasrows)
            {
                StandaloneLogger.Info("Initializing MetaDB schema");

                SqliteTransaction tran = MetaDB.BeginTransaction();

                cmd.CommandText = "CREATE TABLE `a` ( `i` INTEGER PRIMARY KEY,`n` TEXT NOT NULL,`d` TEXT,`g` INTEGER(4) NOT NULL,`l` INTEGER(8) NOT NULL,`c` INTEGER(8) NOT NULL,`h` TEXT NOT NULL,`m` TEXT NOT NULL,`k` INTEGER(1) NOT NULL,`s` INTEGER(1) NOT NULL,`p` INTEGER(4) NOT NULL DEFAULT 0)";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "CREATE INDEX `a0` ON `a`(`n`, `s`)";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "CREATE INDEX `a2` ON `a`(`m`, `s`)";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "CREATE INDEX `a3` ON `a`(`g`, 's')";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "CREATE INDEX `a4` ON `a`(`p`)";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "CREATE INDEX `a1` ON `a`(`s`)";
                cmd.ExecuteNonQuery();

                tran.Commit();

                StandaloneLogger.Info("MetaDB schema initialized");
            }
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("ManifestDB constructor / SQLite initialization", ex);
            throw;
        }

        StandaloneLogger.End("ManifestDB Constructor");
    }

    public IEnumerator UpdateResourceVersion(Action<string, UIMessageType> callback)
    {
        StandaloneLogger.Begin("UpdateResourceVersion");

        isError = false;
        this.callback = callback;

        callback?.Invoke("Checking Resource Version", UIMessageType.Default);

        yield return null;

        StandaloneLogger.Info("Starting UpdateMetaDB");

        yield return UpdateMetaDB();

        StandaloneLogger.Info("UpdateMetaDB completed. isError=" + isError);

        if (!isError)
        {
            // Same pass: the freshly built meta has no e column, so fetch the key list and
            // write it into this database now instead of leaving it for a second run.
            callback?.Invoke("Syncing Keys", UIMessageType.Default);

            yield return UmaKeySync.Ensure(DBPath);

            StandaloneLogger.Info("Starting UpdateMasterDB");
            yield return UpdateMasterDB();
            StandaloneLogger.Info("UpdateMasterDB completed. isError=" + isError);
        }
        else
        {
            StandaloneLogger.Warning("Skipping UpdateMasterDB because MetaDB update failed");
        }

        callback?.Invoke(
            isError ? "Update aborted." : "Done. Please restart the application.\n",
            isError ? UIMessageType.Error : UIMessageType.Success);

        StandaloneLogger.End("UpdateResourceVersion");
    }

    // Root -> Platform Manifest -> Asset Manifest -> Default Manifest
    public IEnumerator UpdateMetaDB()
    {
        StandaloneLogger.Begin("UpdateMetaDB");

        string rootHash = null;

        callback?.Invoke("Getting resource version", UIMessageType.Default);

        yield return null;

        string rootUrl = "https://umapi.buenavista.fans:8443/api/MetaC/root";

        StandaloneLogger.Info("Requesting Root API: " + rootUrl);

        yield return UmaViewerDownload.DownloadText(rootUrl, txt =>
        {
            StandaloneLogger.Info(
                "Root API response length: " +
                (txt == null ? -1 : txt.Length));

            if (string.IsNullOrEmpty(txt))
            {
                StandaloneLogger.Error("Root API returned empty response");
                return;
            }

            try
            {
                StandaloneLogger.Info("Parsing Root API JSON");

                var item = JObject.Parse(txt);

                string name = (string)item["n"];
                string hash = (string)item["h"];

                StandaloneLogger.Info("Root name: " + name);
                StandaloneLogger.Info("Root hash: " + hash);

                if (name == "//root")
                {
                    rootHash = hash;
                }
                else
                {
                    StandaloneLogger.Error("Unexpected Root API name: " + name);
                }
            }
            catch (Exception ex)
            {
                StandaloneLogger.Exception("Root API JSON parsing", ex);
            }
        });

        if (rootHash != null)
        {
            // The endpoint works: remember the hash so a later run can start without it.
            SaveRootCache(rootHash);
        }
        else
        {
            StandaloneLogger.Warning(
                "Root API is unreachable; falling back to the local root cache");

            if (TryLoadRootCache(out string cachedHash, out string cachedAge))
            {
                rootHash = cachedHash;

                StandaloneLogger.Warning(
                    "Using cached root hash " + cachedHash +
                    " (" + cachedAge + ")");

                callback?.Invoke(
                    "Root API unreachable: using cached manifest root",
                    UIMessageType.Warning);
            }
        }

        if (rootHash == null)
        {
            StandaloneLogger.Fail("Root API / RootHash validation", null);

            callback?.Invoke("Get RootHash Error", UIMessageType.Error);
            Debug.LogError("Get RootHash Error");

            isError = true;

            yield break;
        }

        StandaloneLogger.Info("RootHash accepted: " + rootHash);

        ManifestEntry rootEntry = new ManifestEntry(
            Encoding.UTF8.GetBytes("root"),
            null,
            0,
            0,
            0,
            0,
            "",
            3);

        rootEntry.hname = rootHash;

        StandaloneLogger.Info("Starting manifest chain from RootHash");

        yield return UpdateMetaDB(rootEntry);

        StandaloneLogger.End("UpdateMetaDB");
    }

    public IEnumerator UpdateMetaDB(ulong size, ulong checksum)
    {
        StandaloneLogger.Begin("UpdateMetaDB legacy root");

        ManifestEntry rootEntry = new ManifestEntry(
            Encoding.UTF8.GetBytes("root"),
            null,
            0,
            0,
            size,
            checksum,
            "",
            3);

        rootEntry.hname = rootEntry.CalHameString();

        StandaloneLogger.Info("Calculated RootHash: " + rootEntry.hname);

        yield return UpdateMetaDB(rootEntry);

        StandaloneLogger.End("UpdateMetaDB legacy root");
    }

    public IEnumerator UpdateMetaDB(ManifestEntry rootEntry)
    {
        StandaloneLogger.Begin("UpdateMetaDB Manifest Chain");

        StandaloneLogger.Info("Root entry hash: " + rootEntry.hname);

        if (MetaDB == null || MetaDB.State != System.Data.ConnectionState.Open)
        {
            StandaloneLogger.Fail("MetaDB connection is not open", null);

            Debug.LogError("Open Meta Database Error");
            isError = true;

            yield break;
        }

        StandaloneLogger.Info("MetaDB connection is open");

        SqliteCommand command = new SqliteCommand(MetaDB);

        // Insert root
        int index = 1;

        StandaloneLogger.Info("Inserting Root Manifest entry");

        UpdateManifestEntry(index, "manifest3", rootEntry, ref command);

        // Insert platform
        callback?.Invoke("Reading Platform Manifest", UIMessageType.Default);

        yield return null;

        ManifestEntry[] platformEntrys = null;

        StandaloneLogger.Info("Reading Platform Manifest: " + rootEntry.hname);

        yield return GetManifest(
            rootEntry.hname,
            Kind.PlatformManifest,
            entrys => { platformEntrys = entrys; });

        if (platformEntrys == null)
        {
            StandaloneLogger.Fail("Read PlatformManifest Error", null);

            Debug.LogError("Read PlatformManifest Error");

            isError = true;

            MetaDB.Close();

            yield break;
        }

        StandaloneLogger.Info("Platform Manifest entries: " + platformEntrys.Length);

        if (platformEntrys.Length == 0)
        {
            StandaloneLogger.Fail("Platform Manifest is empty", null);
            isError = true;
            MetaDB.Close();
            yield break;
        }

        ManifestEntry platformEntry = GetLocalPlatformEntry(platformEntrys);

        StandaloneLogger.Info(
            "Selected platform: " +
            Encoding.UTF8.GetString(platformEntry.name));

        StandaloneLogger.Info("Platform Manifest hash: " + platformEntry.hname);

        index++;

        UpdateManifestEntry(index, "manifest2", platformEntry, ref command);

        // Insert AssetManifest
        callback?.Invoke("Reading Assets Manifest", UIMessageType.Default);

        yield return null;

        ManifestEntry[] assetEntrys = null;

        StandaloneLogger.Info("Reading Asset Manifest: " + platformEntry.hname);

        yield return GetManifest(
            platformEntry.hname,
            Kind.AssetManifest,
            entrys => { assetEntrys = entrys; });

        if (assetEntrys == null)
        {
            StandaloneLogger.Fail("Read AssetManifest Error", null);

            Debug.LogError("Read AssetManifest Error");

            isError = true;

            MetaDB.Close();

            yield break;
        }

        StandaloneLogger.Info("Asset Manifest entries: " + assetEntrys.Length);

        ConfigureMetaDbForBulkWrite();

        SqliteTransaction assetTran = MetaDB.BeginTransaction();

        StandaloneLogger.Begin("Insert Asset Manifest entries");

        try
        {
            for (int i = 0; i < assetEntrys.Length; i++)
            {
                index++;

                UpdateManifestEntry(index, "manifest", assetEntrys[i], ref command);

                int done = i + 1;

                if (done == assetEntrys.Length || done % ManifestInsertBatchSize == 0)
                {
                    assetTran.Commit();
                    assetTran.Dispose();

                    assetTran = MetaDB.BeginTransaction();

                    StandaloneLogger.Info(
                        "Asset Manifest DB insert progress: " +
                        done + "/" + assetEntrys.Length);
                }
            }
        }
        finally
        {
            assetTran.Dispose();
        }

        StandaloneLogger.End("Insert Asset Manifest entries");

        // Insert Default Manifest entries
        int assetManifestIndex = 0;

        foreach (var assetEntry in assetEntrys)
        {
            assetManifestIndex++;

            StandaloneLogger.Begin(
                "Default Manifest " +
                assetManifestIndex + "/" + assetEntrys.Length);

            StandaloneLogger.Info("Manifest name: " + assetEntry.tname);
            StandaloneLogger.Info("Manifest hash: " + assetEntry.hname);

            ManifestEntry[] assets = null;

            yield return GetManifest(
                assetEntry.hname,
                Kind.Default,
                entrys => { assets = entrys; });

            if (assets == null)
            {
                StandaloneLogger.Fail(
                    "Read Default Manifest Error: " + assetEntry.tname,
                    null);

                Debug.LogError("Read AssetManifest Error");

                isError = true;

                continue;
            }

            StandaloneLogger.Info("Default Manifest entries: " + assets.Length);

            callback?.Invoke(
                $"Reading AssetBundles Manifest : {assetEntry.tname}",
                UIMessageType.Default);

            yield return null;

            SqliteTransaction tran = MetaDB.BeginTransaction();

            try
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    index++;

                    UpdateManifestEntry(index, assetEntry.tname, assets[i], ref command);

                    int done = i + 1;

                    if (done == assets.Length || done % ManifestInsertBatchSize == 0)
                    {
                        tran.Commit();
                        tran.Dispose();

                        tran = MetaDB.BeginTransaction();

                        StandaloneLogger.Info(
                            "Default Manifest DB insert progress: " +
                            done + "/" + assets.Length);
                    }
                }
            }
            finally
            {
                tran.Dispose();
            }

            StandaloneLogger.Info(
                "Default Manifest inserted: " +
                assetEntry.tname +
                ", count=" + assets.Length);

            StandaloneLogger.End(
                "Default Manifest " +
                assetManifestIndex + "/" + assetEntrys.Length);
        }

        StandaloneLogger.Info("Total MetaDB rows processed: " + index);

        StandaloneLogger.End("UpdateMetaDB Manifest Chain");
    }

    public IEnumerator UpdateMasterDB()
    {
        StandaloneLogger.Begin("UpdateMasterDB");

        if (MetaDB == null || MetaDB.State != System.Data.ConnectionState.Open)
        {
            StandaloneLogger.Fail("MetaDB connection is not open for master update", null);

            Debug.LogError("Open Meta Database Error");

            isError = true;

            yield break;
        }

        var masterPath = $"{Path.GetDirectoryName(DBPath)}/master/master_umaviewer.mdb";
        var masterDir = Path.GetDirectoryName(masterPath);

        StandaloneLogger.RecordPath("Master DB", masterPath);
        StandaloneLogger.RecordDirectory("Master DB directory", masterDir);

        callback?.Invoke("Checking master.mdb", UIMessageType.Default);

        yield return null;

        SqliteCommand command = new SqliteCommand(
            "SELECT h FROM a WHERE n LIKE 'master.mdb.lz4'",
            MetaDB);

        var reader = command.ExecuteReader();

        if (reader.Read())
        {
            yield return null;

            var hash = reader.GetString(0);

            reader.Close();

            StandaloneLogger.Info("master.mdb.lz4 hash: " + hash);

            var path = GetManifestPath(hash);

            StandaloneLogger.RecordPath("Compressed master DB", path);

            if (!File.Exists(path))
            {
                callback?.Invoke("Downloading master.mdb", UIMessageType.Default);

                yield return null;

                byte[] data = null;
                string error = null;

                string url = UmaViewerDownload.GetGenericRequestUrl(hash);

                StandaloneLogger.Info("Downloading master DB: " + url);

                yield return UmaViewerDownload.DownloadBytes(
                    url,
                    (responseData, responseError) =>
                    {
                        data = responseData;
                        error = responseError;
                    });

                if (data != null)
                {
                    StandaloneLogger.Info("master.mdb.lz4 downloaded: " + data.Length + " bytes");

                    File.WriteAllBytes(path, data);

                    StandaloneLogger.RecordPath("Downloaded master DB", path);
                }
                else
                {
                    StandaloneLogger.Fail("Download Master.mdb Failed", null);
                    StandaloneLogger.Error(error);

                    Debug.LogError("Download Master.mdb Failed :" + error);

                    callback?.Invoke(
                        "Download Master.mdb Failed :" + error,
                        UIMessageType.Error);

                    isError = true;

                    yield break;
                }
            }
            else
            {
                StandaloneLogger.Info("Using cached master.mdb.lz4");
            }

            callback?.Invoke("Decompress master.mdb", UIMessageType.Default);

            yield return null;

            StandaloneLogger.Begin("Decompress master.mdb.lz4");

            try
            {
                Directory.CreateDirectory(masterDir);

                StandaloneLogger.Info("LZ4 source: " + path);
                StandaloneLogger.Info("LZ4 target: " + masterPath);

                var bytes = LZ4Util.DecompressFromFile(path);

                StandaloneLogger.Info(
                    "LZ4 decompressed bytes: " +
                    (bytes == null ? -1 : bytes.Length));

                File.WriteAllBytes(masterPath, bytes);

                StandaloneLogger.RecordPath("Decompressed master DB", masterPath);

                StandaloneLogger.End("Decompress master.mdb.lz4");
            }
            catch (Exception e)
            {
                StandaloneLogger.Exception("Decompress master.mdb", e);

                Debug.LogError($"Decompress master.mdb Error: {e.Message}");

                callback?.Invoke("Decompress master.mdb Error", UIMessageType.Error);

                isError = true;

                yield break;
            }
        }
        else
        {
            reader.Close();

            StandaloneLogger.Fail(
                "master.mdb.lz4 not found in MetaDB",
                null);

            isError = true;
        }

        StandaloneLogger.End("UpdateMasterDB");
    }

    private bool insertParametersReady;

    // Reuse one prepared statement for all rows: rebuilding 11 SqliteParameter objects
    // 58k times dominated the insert loop.
    private void PrepareInsertParameters(SqliteCommand command)
    {
        if (insertParametersReady) return;

        command.CommandText =
            "INSERT OR REPLACE INTO a (i,n,d,g,l,c,h,m,k,s,p) " +
            "VALUES (@id,@name,@deps,@group,@length,@check,@hash,@m,@k,@s,@p)";

        command.Parameters.Clear();

        command.Parameters.Add(new SqliteParameter("@id", 0L));
        command.Parameters.Add(new SqliteParameter("@name", ""));
        command.Parameters.Add(new SqliteParameter("@deps", null));
        command.Parameters.Add(new SqliteParameter("@group", 0));
        command.Parameters.Add(new SqliteParameter("@length", 0L));
        command.Parameters.Add(new SqliteParameter("@check", 0L));
        command.Parameters.Add(new SqliteParameter("@hash", ""));
        command.Parameters.Add(new SqliteParameter("@m", ""));
        command.Parameters.Add(new SqliteParameter("@k", 0));
        command.Parameters.Add(new SqliteParameter("@s", 1));
        command.Parameters.Add(new SqliteParameter("@p", 0));

        insertParametersReady = true;
    }

    private void UpdateManifestEntry(
        int index,
        string type,
        ManifestEntry entry,
        ref SqliteCommand command)
    {
        PrepareInsertParameters(command);

        command.Parameters["@id"].Value = (long)index;
        command.Parameters["@name"].Value =
            (type.Contains("manifest") ? "//" : "") + entry.tname;
        command.Parameters["@deps"].Value = (object)entry.tdeps ?? DBNull.Value;
        command.Parameters["@group"].Value = (long)entry.group;
        command.Parameters["@length"].Value = (long)entry.size;
        command.Parameters["@check"].Value = (long)entry.checksum;
        command.Parameters["@hash"].Value = (object)entry.hname ?? DBNull.Value;
        command.Parameters["@m"].Value = type;
        command.Parameters["@k"].Value = (byte)entry.kind;
        command.Parameters["@s"].Value = 1;
        command.Parameters["@p"].Value = (long)entry.priority;

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Prepares the meta DB for the bulk manifest insert: WAL keeps the pages in memory
    /// instead of fsyncing each one, and a bigger page cache avoids spilling to disk
    /// mid-transaction (the previous single 58k-row transaction stalled there).
    /// </summary>
    private void ConfigureMetaDbForBulkWrite()
    {
        if (bulkWriteConfigured) return;

        bulkWriteConfigured = true;

        try
        {
            using SqliteCommand pragma = new SqliteCommand(MetaDB);

            pragma.CommandText = "PRAGMA journal_mode=WAL";
            StandaloneLogger.Info(
                "MetaDB journal_mode = " + (pragma.ExecuteScalar() ?? "<null>"));

            pragma.CommandText = "PRAGMA synchronous=NORMAL";
            pragma.ExecuteNonQuery();

            pragma.CommandText = "PRAGMA cache_size=-32000";
            pragma.ExecuteNonQuery();

            StandaloneLogger.Info(
                "MetaDB bulk-write settings applied (synchronous=NORMAL, cache_size=32MB, " +
                "batch=" + ManifestInsertBatchSize + " rows)");
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("Applying MetaDB bulk-write settings", ex);
        }
    }

    public static ManifestEntry[] GetManifest(string path, bool isCompressed, Kind kind)
    {
        StandaloneLogger.Begin("Parse Manifest");
        StandaloneLogger.Info("Manifest path: " + path);
        StandaloneLogger.Info("Is compressed: " + isCompressed);
        StandaloneLogger.Info("Manifest kind: " + kind);

        IBSVReader reader = Init(path, isCompressed);

        if (reader == null)
        {
            StandaloneLogger.Fail("BSVReader.Init returned null", null);
            return null;
        }

        StandaloneLogger.Info("BSVReader initialized");

        ulong count = reader.GetRowCount();

        StandaloneLogger.Info("Manifest row count: " + count);

        ManifestEntry[] manifests = new ManifestEntry[count];

        for (ulong i = 0; i < count; i++)
        {
            manifests[i].kind = (int)kind;

            if (kind == Kind.Default)
            {
                if (!reader.ReadLine(
                    ManifestEntry.GetBsvParser(ManifestEntry.Format.Full, reader),
                    ref manifests[i]))
                {
                    StandaloneLogger.Fail(
                        "Manifest ReadLine failed at row " + i,
                        null);

                    Debug.LogError("ReadLineError");

                    return null;
                }
            }
            else
            {
                if (!reader.ReadLine(
                    ManifestEntry.GetBsvParser(ManifestEntry.Format.Simplified, reader),
                    ref manifests[i]))
                {
                    StandaloneLogger.Fail(
                        "Manifest ReadLine failed at row " + i,
                        null);

                    Debug.LogError("ReadLineError");

                    return null;
                }
            }

            if (i == 0 || (i + 1) % 5000 == 0)
            {
                StandaloneLogger.Info(
                    "Manifest parsing progress: " +
                    (i + 1) + "/" + count);
            }
        }

        StandaloneLogger.Info("Manifest parsing complete: " + count);

        StandaloneLogger.End("Parse Manifest");

        return manifests;
    }

    public IEnumerator GetManifest(
        string hash,
        Kind kind,
        Action<ManifestEntry[]> callback)
    {
        StandaloneLogger.Begin("GetManifest");
        StandaloneLogger.Info("Hash: " + hash);
        StandaloneLogger.Info("Kind: " + kind);

        var path = GetManifestPath(hash);

        StandaloneLogger.RecordPath("Manifest cache", path);

        if (!File.Exists(path))
        {
            StandaloneLogger.Info("Manifest cache miss: " + hash);

            this.callback?.Invoke(
                $"Downloading Manifest :{hash}",
                UIMessageType.Default);

            yield return null;

            yield return DownloadManifest(hash);
        }
        else
        {
            StandaloneLogger.Info("Manifest cache hit: " + hash);
        }

        if (File.Exists(path))
        {
            yield return null;

            StandaloneLogger.Info("Parsing manifest file: " + path);

            ManifestEntry[] result = GetManifest(path, true, kind);

            StandaloneLogger.Info(
                "Parsed manifest result: " +
                (result == null ? "null" : result.Length.ToString()));

            callback.Invoke(result);
        }
        else
        {
            StandaloneLogger.Fail(
                "Manifest file unavailable after download: " + path,
                null);

            callback.Invoke(null);
        }

        StandaloneLogger.End("GetManifest");
    }

    public IEnumerator DownloadManifest(string hash)
    {
        StandaloneLogger.Begin("DownloadManifest");

        StandaloneLogger.Info("Hash: " + hash);

        var url = UmaViewerDownload.GetManifestRequestUrl(hash);

        StandaloneLogger.Info("URL: " + url);

        byte[] data = null;
        string error = null;

        yield return UmaViewerDownload.DownloadBytes(
            url,
            (responseData, responseError) =>
            {
                data = responseData;
                error = responseError;
            },
            15);

        if (data != null)
        {
            var targetPath = GetManifestPath(hash);

            StandaloneLogger.Info("Downloaded manifest bytes: " + data.Length);
            StandaloneLogger.Info("Saving manifest to: " + targetPath);

            File.WriteAllBytes(targetPath, data);

            StandaloneLogger.RecordPath("Saved manifest", targetPath);

            StandaloneLogger.End("DownloadManifest");
        }
        else
        {
            StandaloneLogger.Fail("Download Manifest Failed: " + url, null);
            StandaloneLogger.Error(error);

            Debug.LogError($"Download Manifest Failed :{url} {error}");

            callback?.Invoke(
                $"Download Manifest Failed :{error}",
                UIMessageType.Error);

            isError = true;

            yield break;
        }
    }

    public string GetManifestPath(string hash, bool createPath = true)
    {
        string path = $"{Path.GetDirectoryName(DBPath)}/dat/{hash.Substring(0, 2)}";

        if (createPath)
        {
            Directory.CreateDirectory(path);
        }

        return path + $"/{hash}";
    }

    // ---- root API cache -------------------------------------------------

    /// <summary>
    /// Location of the cached root manifest hash: {MainPath}/root_cache.json
    /// Written the first time the root API answers, used when it cannot be reached.
    /// </summary>
    private string GetRootCachePath()
    {
        string directory = Path.GetDirectoryName(DBPath);

        if (string.IsNullOrEmpty(directory))
        {
            directory = Config.Instance.MainPath;
        }

        return Path.Combine(directory, "root_cache.json");
    }

    private static string ReadRootCacheHash(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            JObject cached = JObject.Parse(File.ReadAllText(path));

            string hash = (string)cached["h"];

            return string.IsNullOrEmpty(hash) ? null : hash;
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("Reading root cache", ex);
            return null;
        }
    }

    /// <summary>
    /// Stores the live root hash. An existing cache is overwritten whenever the live
    /// hash differs from it, so the file always mirrors the last reachable state.
    /// </summary>
    private void SaveRootCache(string rootHash)
    {
        try
        {
            string path = GetRootCachePath();

            StandaloneLogger.RecordPath("Root cache", path);

            string previous = ReadRootCacheHash(path);

            if (!string.IsNullOrEmpty(previous) && previous == rootHash)
            {
                StandaloneLogger.Info(
                    "Root cache already matches the live root hash; left untouched");
                return;
            }

            JObject payload = new JObject
            {
                ["n"] = "//root",
                ["h"] = rootHash,
                ["source"] = "https://umapi.buenavista.fans:8443/api/MetaC/root",
                ["savedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };

            string tempPath = path + ".tmp";

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            File.WriteAllText(
                tempPath,
                payload.ToString(Newtonsoft.Json.Formatting.Indented),
                new UTF8Encoding(false));

            File.Copy(tempPath, path, true);
            File.Delete(tempPath);

            StandaloneLogger.RecordPath("Root cache (written)", path);

            if (string.IsNullOrEmpty(previous))
            {
                StandaloneLogger.Info("Root cache created: " + rootHash);
            }
            else
            {
                StandaloneLogger.Warning(
                    "Root hash changed " + previous + " -> " + rootHash +
                    "; root cache overwritten");
            }
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("Saving root cache", ex);
        }
    }

    private bool TryLoadRootCache(out string rootHash, out string ageDescription)
    {
        rootHash = null;
        ageDescription = null;

        string path = GetRootCachePath();

        StandaloneLogger.RecordPath("Root cache", path);

        try
        {
            if (!File.Exists(path)) return false;

            JObject cached = JObject.Parse(File.ReadAllText(path));

            string hash = (string)cached["h"];

            if (string.IsNullOrEmpty(hash))
            {
                StandaloneLogger.Warning("Root cache has no usable hash");
                return false;
            }

            string savedAt = (string)cached["savedAtUtc"];
            DateTime savedUtc;

            if (DateTime.TryParse(
                savedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out savedUtc))
            {
                int ageDays = (int)(DateTime.UtcNow - savedUtc.ToUniversalTime()).TotalDays;

                ageDescription =
                    "saved " + savedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") +
                    ", age " + ageDays + "d";

                if (ageDays >= 30)
                {
                    StandaloneLogger.Warning(
                        "Root cache is " + ageDays + " days old; the cached manifest " +
                        "chain may no longer exist on the server");
                }
            }
            else
            {
                ageDescription = "unknown save time";
            }

            rootHash = hash;
            return true;
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("Loading root cache", ex);
            return false;
        }
    }

    public static ManifestEntry GetLocalPlatformEntry(ManifestEntry[] platformEntrys)
    {
        StandaloneLogger.Begin("Select Local Platform");

        foreach (var entry in platformEntrys)
        {
            string platformName = Encoding.UTF8.GetString(entry.name);

            StandaloneLogger.Info("Available platform: " + platformName);

            if (platformName == "Windows" && Application.platform == RuntimePlatform.WindowsPlayer)
            {
                StandaloneLogger.Info("Selected platform: Windows");
                StandaloneLogger.End("Select Local Platform");
                return entry;
            }
            else if (platformName == "IOS" && Application.platform == RuntimePlatform.IPhonePlayer)
            {
                StandaloneLogger.Info("Selected platform: IOS");
                StandaloneLogger.End("Select Local Platform");
                return entry;
            }
            else if (platformName == "Android" && Application.platform == RuntimePlatform.Android)
            {
                StandaloneLogger.Info("Selected platform: Android");
                StandaloneLogger.End("Select Local Platform");
                return entry;
            }
        }

        StandaloneLogger.Warning(
            "No exact platform match. Falling back to first platform entry.");

        StandaloneLogger.End("Select Local Platform");

        return platformEntrys[0];
    }
}
