using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Remembers content hashes the CDN answered 404/403/410 for.
/// The manifest occasionally lists entries whose payload is no longer (or not yet) on the
/// CDN; without this the viewer re-requested them on every load, which is what showed up
/// as "continuously requesting resources that do not exist".
/// </summary>
public static class UmaMissingAssets
{
    private static readonly object Sync = new object();
    private static readonly HashSet<string> Missing =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static string cachePath;
    private static bool loaded;

    private static string CachePath
    {
        get
        {
            if (!string.IsNullOrEmpty(cachePath)) return cachePath;

            string root = Config.Instance != null && !string.IsNullOrEmpty(Config.Instance.MainPath)
                ? Config.Instance.MainPath
                : Application.persistentDataPath;

            cachePath = Path.Combine(root, "missing_assets.txt");

            return cachePath;
        }
    }

    private static void EnsureLoaded()
    {
        lock (Sync)
        {
            if (loaded) return;

            loaded = true;

            try
            {
                if (!File.Exists(CachePath)) return;

                int count = 0;

                foreach (string line in File.ReadAllLines(CachePath))
                {
                    string hash = (line ?? string.Empty).Trim();

                    if (hash.Length == 0 || hash.StartsWith("#", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (Missing.Add(hash)) count++;
                }

                StandaloneLogger.Info(
                    "Missing-asset cache loaded: " + count + " entries from " + CachePath);
            }
            catch (Exception ex)
            {
                StandaloneLogger.Exception("Loading missing-asset cache", ex);
            }
        }
    }

    public static bool IsMissing(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;

        EnsureLoaded();

        lock (Sync)
        {
            return Missing.Contains(hash);
        }
    }

    public static int Count
    {
        get
        {
            EnsureLoaded();

            lock (Sync)
            {
                return Missing.Count;
            }
        }
    }

    /// <summary>Records a hash and appends it to {MainPath}/missing_assets.txt.</summary>
    public static void MarkMissing(string hash, string reason)
    {
        if (string.IsNullOrEmpty(hash)) return;

        EnsureLoaded();

        bool added;

        lock (Sync)
        {
            added = Missing.Add(hash);
        }

        if (!added) return;

        StandaloneLogger.Warning(
            "Asset marked as unavailable, further requests skipped: " + hash +
            " (" + reason + ")");

        try
        {
            File.AppendAllText(
                CachePath,
                hash + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("Saving missing-asset cache", ex);
        }
    }

    /// <summary>Forgets a hash again (used when a later download succeeds).</summary>
    public static void Clear(string hash)
    {
        if (string.IsNullOrEmpty(hash)) return;

        EnsureLoaded();

        bool removed;

        lock (Sync)
        {
            removed = Missing.Remove(hash);
        }

        if (!removed) return;

        StandaloneLogger.Info("Asset available again, removed from missing cache: " + hash);

        try
        {
            List<string> lines = new List<string>();

            lock (Sync)
            {
                lines.AddRange(Missing);
            }

            File.WriteAllLines(CachePath, lines, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("Rewriting missing-asset cache", ex);
        }
    }

    /// <summary>
    /// True when the download error is a deterministic server rejection, i.e. retrying
    /// cannot succeed.
    /// </summary>
    public static bool IsPermanentFailure(string error)
    {
        if (string.IsNullOrEmpty(error)) return false;

        // 403 is deliberately NOT permanent: the CDN answers 403 for a few bundles per
        // session (rate limiting / cold cache) and the same URL returns 206 on a retry,
        // which is how the shader bundle got blacklisted and left the model magenta.
        return error.IndexOf("HTTP 404", StringComparison.OrdinalIgnoreCase) >= 0
            || error.IndexOf("HTTP 410", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
