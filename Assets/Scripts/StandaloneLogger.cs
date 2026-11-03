using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// File + console logger used to trace the standalone (no game client) workflow,
/// in particular the resource download / database bootstrap chain.
/// </summary>
public static class StandaloneLogger
{
    private const string DefaultStage = "App";

    private static readonly object Sync = new object();
    private static readonly Dictionary<string, Stopwatch> Running = new Dictionary<string, Stopwatch>();

    private static string logPath;
    private static bool initialized;

    public static string LogPath
    {
        get
        {
            Initialize();
            return logPath;
        }
    }

    public static void Initialize()
    {
        lock (Sync)
        {
            if (initialized) return;

            try
            {
                string root = Directory.GetParent(Application.dataPath)?.FullName
                    ?? Application.persistentDataPath;

                string directory = Path.Combine(root, "Logs");
                Directory.CreateDirectory(directory);

                logPath = Path.Combine(directory, "standalone.log");
                initialized = true;

                WriteInternal("INFO", "Logger",
                    "Logger initialized. Application=" + Application.version);

                WriteInternal("INFO", "Logger",
                    "Platform=" + Application.platform);

                WriteInternal("INFO", "Logger",
                    "LogPath=" + logPath);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(
                    "[StandaloneLogger] Initialization failed: " + ex);
            }
        }
    }

    // ---- plain messages (single argument) --------------------------------

    public static void Info(string message)
    {
        Write("INFO", DefaultStage, message);
    }

    public static void Warning(string message)
    {
        Write("WARN", DefaultStage, message);
    }

    public static void Error(string message)
    {
        Write("ERROR", DefaultStage, message);
    }

    // ---- staged messages -------------------------------------------------

    public static void Info(string stage, string message)
    {
        Write("INFO", stage, message);
    }

    public static void Warning(string stage, string message)
    {
        Write("WARN", stage, message);
    }

    public static void Error(string stage, string message)
    {
        Write("ERROR", stage, message);
    }

    public static void Exception(string stage, Exception ex)
    {
        Write("EXCEPTION", stage,
            ex == null ? "Unknown exception" : ex.ToString());
    }

    // ---- scope helpers ---------------------------------------------------

    public static Stopwatch Begin(string stage)
    {
        return Begin(stage, stage);
    }

    public static Stopwatch Begin(string stage, string message)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        lock (Sync)
        {
            Running[stage] = stopwatch;
        }

        Info(stage, "START: " + message);
        return stopwatch;
    }

    /// <summary>Ends the scope identified by <paramref name="stage"/> using the stopwatch started by Begin.</summary>
    public static void End(string stage)
    {
        Stopwatch stopwatch = null;

        lock (Sync)
        {
            if (Running.TryGetValue(stage, out stopwatch))
            {
                Running.Remove(stage);
            }
        }

        End(stage, stopwatch, stage);
    }

    public static void End(string stage, Stopwatch stopwatch, string message)
    {
        if (stopwatch != null)
        {
            stopwatch.Stop();
            Info(stage, "END: " + message +
                " | Elapsed=" + stopwatch.ElapsedMilliseconds + "ms");
        }
        else
        {
            Info(stage, "END: " + message);
        }
    }

    public static void Fail(string stage, Exception ex)
    {
        Stopwatch stopwatch = null;

        lock (Sync)
        {
            if (Running.TryGetValue(stage, out stopwatch))
            {
                Running.Remove(stage);
            }
        }

        Fail(stage, stopwatch, ex);
    }

    public static void Fail(string stage, Stopwatch stopwatch, Exception ex)
    {
        if (stopwatch != null)
        {
            stopwatch.Stop();
        }

        Error(stage,
            "FAILED | Elapsed=" +
            (stopwatch == null ? -1 : stopwatch.ElapsedMilliseconds) +
            "ms");

        if (ex != null)
        {
            Exception(stage, ex);
        }
    }

    // ---- configuration / path diagnostics --------------------------------

    /// <summary>Dumps the runtime-relevant configuration values (secrets excluded).</summary>
    public static void RecordConfig()
    {
        try
        {
            Config config = Config.Instance;

            if (config == null)
            {
                Write("INFO", "Config", "Config instance is not available yet");
                return;
            }

            Write("INFO", "Config",
                "Version=" + config.Version +
                " | WorkMode=" + config.WorkMode +
                " | Region=" + config.Region +
                " | Language=" + config.Language +
                " | DownloadMissingResources=" + config.DownloadMissingResources +
                " | ProxyMode=" + config.NetworkProxyMode +
                " | Proxy=" + config.NetworkProxyHost + ":" + config.NetworkProxyPort);

            Write("INFO", "Config", "MainPath=" + config.MainPath);
            Write("INFO", "Config", "ConfigPath=" + Config.configPath);
        }
        catch (Exception ex)
        {
            Exception("Config", ex);
        }
    }

    public static void RecordPath(string label, string path)
    {
        string state;

        if (string.IsNullOrEmpty(path))
        {
            state = "empty";
        }
        else
        {
            try
            {
                FileInfo info = new FileInfo(path);

                if (!info.Exists)
                {
                    state = "missing";
                }
                else
                {
                    state = "exists size=" + info.Length +
                        " modified=" + info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            catch (Exception ex)
            {
                state = "error: " + ex.GetType().Name;
            }
        }

        Write("INFO", "Path", label + " => " + (path ?? "<null>") + " [" + state + "]");
    }

    public static void RecordDirectory(string label, string path)
    {
        string state;

        if (string.IsNullOrEmpty(path))
        {
            state = "empty";
        }
        else
        {
            try
            {
                state = Directory.Exists(path) ? "exists" : "missing";
            }
            catch (Exception ex)
            {
                state = "error: " + ex.GetType().Name;
            }
        }

        Write("INFO", "Dir", label + " => " + (path ?? "<null>") + " [" + state + "]");
    }

    public static void RecordDownload(
        string url, int attempt, int statusCode, long bytes, long elapsedMs)
    {
        double seconds = elapsedMs / 1000.0;
        double kilobytesPerSecond =
            seconds > 0 ? (bytes / 1024.0) / seconds : 0;

        Write("INFO", "Download",
            "attempt=" + attempt +
            " | status=" + statusCode +
            " | bytes=" + bytes +
            " | elapsed=" + elapsedMs + "ms" +
            " | speed=" + kilobytesPerSecond.ToString("0.0") + "KB/s" +
            " | url=" + (url ?? string.Empty));
    }

    // ---- core ------------------------------------------------------------

    private static void Write(string level, string stage, string message)
    {
        Initialize();

        lock (Sync)
        {
            WriteInternal(level, stage, message);
        }
    }

    private static void WriteInternal(
        string level, string stage, string message)
    {
        string line =
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
            " [" + level + "]" +
            " [" + stage + "] " +
            (message ?? string.Empty);

        UnityEngine.Debug.Log("[Standalone] " + line);

        if (string.IsNullOrEmpty(logPath)) return;

        try
        {
            File.AppendAllText(
                logPath,
                line + Environment.NewLine,
                new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError(
                "[StandaloneLogger] Write failed: " + ex.Message);
        }
    }
}
