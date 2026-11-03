using System;
using System.Collections;
using System.IO;
using Mono.Data.Sqlite;
using UnityEngine;

/// <summary>
/// Fills the per-entry decryption keys into a rebuilt meta database.
///
/// `Update Database` recreates meta_umaviewer from the CDN manifests, which carry names, sizes
/// and hashes but no key — the keys only exist in the game's own meta. The API keeps an
/// exported "hash TAB key" list, so after a rebuild we pull it and write column `e`, which is
/// what UmaAssetBundleStream needs to decrypt the bundles.
/// </summary>
public static class UmaKeySync
{
    /// <summary>Windows 版 meta 的 e 列导出（bundle hash 按 Windows 平台编译）。</summary>
    public const string Url = "https://umapi.buenavista.fans:8443/keys.txt";

    /// <summary>
    /// Android 版 meta 的 e 列导出。同一个资源在两端的 bundle hash 不同，所以密钥表必须
    /// 按平台分开取；拿错表的话 key 会全部对不上（文件长度、头部都正常，解密结果却是垃圾）。
    /// </summary>
    public const string UrlAndroid = "https://umapi.buenavista.fans:8443/keys-android.txt";

    /// <summary>当前平台应使用的密钥表地址。</summary>
    public static string ResolveUrl()
    {
        return Application.platform == RuntimePlatform.Android ? UrlAndroid : Url;
    }

    /// <summary>
    /// True when the database either has no `e` column at all or has one that is entirely
    /// empty — i.e. a rebuilt meta that still cannot decrypt anything.
    /// </summary>
    public static bool NeedsSync(string dbPath)
    {
        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            return false;

        try
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + dbPath))
            {
                connection.Open();

                bool hasKeyColumn;

                using (SqliteCommand probe = new SqliteCommand(
                    "SELECT name FROM pragma_table_info('a') WHERE name = 'e'", connection))
                using (SqliteDataReader reader = probe.ExecuteReader())
                {
                    hasKeyColumn = reader.HasRows;
                }

                if (!hasKeyColumn)
                    return true;

                using (SqliteCommand count = new SqliteCommand(
                    "SELECT COUNT(*) FROM a WHERE e IS NOT NULL AND e <> 0", connection))
                {
                    object value = count.ExecuteScalar();
                    return value == null || Convert.ToInt64(value) == 0L;
                }
            }
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("KeySync: probing " + dbPath + " failed", ex);
            return false;
        }
    }

    public static IEnumerator Ensure(string dbPath)
    {
        StandaloneLogger.Begin("KeySync");

        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
        {
            StandaloneLogger.Fail("KeySync: database not found", null);
            yield break;
        }

        StandaloneLogger.Info("KeySync: downloading keys from " + ResolveUrl());

        string payload = null;

        yield return UmaViewerDownload.DownloadText(ResolveUrl(), text => payload = text);

        if (string.IsNullOrEmpty(payload))
        {
            StandaloneLogger.Fail("KeySync: key list unavailable, resources stay encrypted", null);
            yield break;
        }

        int applied = 0;

        try
        {
            applied = Apply(dbPath, payload);
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("KeySync: applying keys failed", ex);
        }

        StandaloneLogger.Info("KeySync: " + applied + " key(s) written into " + dbPath);

        StandaloneLogger.End("KeySync");
    }

    /// <summary>Parses "hash TAB key" lines and updates column e. Returns the number of rows keyed.</summary>
    private static int Apply(string dbPath, string payload)
    {
        using SqliteConnection connection = new SqliteConnection("Data Source=" + dbPath);
        connection.Open();

        bool hasKeyColumn = false;

        using (SqliteCommand probe = new SqliteCommand(
            "SELECT name FROM pragma_table_info('a') WHERE name = 'e'", connection))
        using (SqliteDataReader reader = probe.ExecuteReader())
        {
            hasKeyColumn = reader.HasRows;
        }

        if (!hasKeyColumn)
        {
            StandaloneLogger.Info("KeySync: adding column e to the rebuilt database");
            new SqliteCommand("ALTER TABLE a ADD e INTEGER", connection).ExecuteNonQuery();
        }

        new SqliteCommand("DROP TABLE IF EXISTS _keys", connection).ExecuteNonQuery();
        new SqliteCommand("CREATE TABLE _keys (h TEXT PRIMARY KEY, e INTEGER)", connection).ExecuteNonQuery();

        int inserted = 0;

        using (SqliteTransaction transaction = connection.BeginTransaction())
        using (SqliteCommand insert = new SqliteCommand(
            "INSERT OR REPLACE INTO _keys (h, e) VALUES (@h, @e)", connection, transaction))
        {
            SqliteParameter hashParameter = insert.Parameters.Add("@h", System.Data.DbType.String);
            SqliteParameter keyParameter = insert.Parameters.Add("@e", System.Data.DbType.Int64);

            foreach (string rawLine in payload.Split('\n'))
            {
                string line = rawLine.Trim();

                if (line.Length == 0) continue;

                int tab = line.IndexOf('\t');

                if (tab <= 0) continue;

                string hash = line.Substring(0, tab).Trim();
                string keyText = line.Substring(tab + 1).Trim();

                if (hash.Length == 0 || !long.TryParse(keyText, out long key)) continue;

                hashParameter.Value = hash;
                keyParameter.Value = key;

                insert.ExecuteNonQuery();
                inserted++;
            }

            transaction.Commit();
        }

        using (SqliteCommand update = new SqliteCommand(
            "UPDATE a SET e = (SELECT e FROM _keys WHERE _keys.h = a.h) " +
            "WHERE h IN (SELECT h FROM _keys)", connection))
        {
            update.ExecuteNonQuery();
        }

        new SqliteCommand("DROP TABLE IF EXISTS _keys", connection).ExecuteNonQuery();
        connection.Close();

        return inserted;
    }
}
