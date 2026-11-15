#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

/// <summary>
/// Android 上 Default 模式的取数通道。
///
/// 游戏把 meta / master.mdb / dat 全部放在 /data/data/jp.co.cygames.umamusume/files/，那个目录
/// 属于另一个应用，Viewer 既没有权限直接读，也没法靠 SAF 拿到（应用私有目录不进 MediaStore）。
/// 所以这里用 su 做两件事：先探测 root，再把需要的文件复制到 Viewer 自己的持久化目录，
/// 之后所有代码照旧按普通路径访问，不需要在别处再关心权限。
/// </summary>
public static class AndroidRootSource
{
    public const string GamePackage = "jp.co.cygames.umamusume";
    public const string GameFiles = "/data/data/" + GamePackage + "/files";

    /// <summary>Viewer 侧存放复制结果的目录名（位于 persistentDataPath 下）。</summary>
    public const string MirrorDirName = "game_root";

    private static int _checked;      // 0 = 未检测, 1 = 有 root, 2 = 无 root
    private static string _lastError = string.Empty;

    public static string LastError => _lastError;

    /// <summary>跑一条 su 命令，返回标准输出；没有 root 或执行失败时返回 null。</summary>
    public static string RunAsRoot(string arguments, int timeoutMs = 120000)
    {
        try
        {
            using (Process process = new Process())
            {
                process.StartInfo.FileName = "su";
                process.StartInfo.Arguments = "-c " + Quote(arguments);
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); } catch { }
                    _lastError = "su 超时";
                    return null;
                }

                if (process.ExitCode != 0)
                {
                    _lastError = string.IsNullOrEmpty(stderr) ? ("su exit " + process.ExitCode) : stderr.Trim();
                    return null;
                }

                return stdout;
            }
        }
        catch (Exception ex)
        {
            _lastError = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
    }

    /// <summary>探测 root（有一次成功即缓存为有）。</summary>
    public static bool HasRoot()
    {
        if (Volatile.Read(ref _checked) == 1) return true;
        if (Volatile.Read(ref _checked) == 2) return false;

        string id = RunAsRoot("id", 15000);

        bool ok = id != null && id.Contains("uid=0");

        Volatile.Write(ref _checked, ok ? 1 : 2);

        return ok;
    }

    /// <summary>游戏数据是否可访问（目录存在且能列出内容）。</summary>
    public static bool GameDataAccessible()
    {
        string ls = RunAsRoot("ls " + Quote(GameFiles), 20000);
        return ls != null && ls.Trim().Length > 0;
    }

    /// <summary>
    /// 只复制 meta 与 master/（约 210 MB —— SQLite 必须是本地可 seek 的真实文件）。
    /// dat/ 不进镜像：那里面几千个 bundle、合计好几 GB，全部搬运没有意义，
    /// 单个文件由 <see cref="EnsureBundle"/> 按需取。
    /// </summary>
    public static string Mirror(string persistentRoot)
    {
        string mirror = Path.Combine(persistentRoot, MirrorDirName);

        try
        {
            Directory.CreateDirectory(mirror);
        }
        catch (Exception ex)
        {
            _lastError = "创建镜像目录失败: " + ex.Message;
            return null;
        }

        string[] items = { "meta", "master" };

        foreach (string item in items)
        {
            string src = GameFiles + "/" + item;

            // cp -a 保留时间戳；目标已存在就直接覆盖
            string cmd = "cp -a " + Quote(src) + " " + Quote(mirror) + " 2>&1";

            if (RunAsRoot(cmd, 900000) == null)
            {
                _lastError = "复制 " + item + " 失败: " + _lastError;
                return null;
            }
        }

        // meta 必须真的落盘，否则后面 SQLite 打开失败时看不出原因
        if (!File.Exists(Path.Combine(mirror, "meta")))
        {
            _lastError = "复制后找不到 meta";
            return null;
        }

        return mirror;
    }

    /// <summary>
    /// 按需把单个 bundle 从游戏目录取到缓存，返回本地路径；失败返回 null。
    /// 游戏侧路径是 dat/{hash[0:2]}/{hash}。
    /// </summary>
    public static string EnsureBundle(string persistentRoot, string hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length < 3)
            return null;

        string bucket = hash.Substring(0, 2);
        string local = Path.Combine(persistentRoot, BundleCacheDirName, bucket, hash);

        if (File.Exists(local) && new FileInfo(local).Length > 0)
            return local;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(local));
        }
        catch (Exception ex)
        {
            _lastError = "创建 bundle 缓存目录失败: " + ex.Message;
            return null;
        }

        string src = GameFiles + "/dat/" + bucket + "/" + hash;
        string cmd = "cat " + Quote(src) + " > " + Quote(local) + " 2>&1";

        if (RunAsRoot(cmd, 120000) == null)
        {
            try { File.Delete(local); } catch { }
            return null;
        }

        return File.Exists(local) && new FileInfo(local).Length > 0 ? local : null;
    }

    /// <summary>
    /// 当前是否处于 Android 的 root 模式（Default 模式且已镜像成功）。
    /// 判断依据是 MainPath 指向镜像目录 —— 镜像成功才会被设成那个路径。
    /// </summary>
    public static bool IsRootMode
    {
        get
        {
            Config cfg = Config.Instance;

            return cfg != null
                && cfg.WorkMode == WorkMode.Default
                && cfg.RootAvailable
                && !string.IsNullOrEmpty(cfg.MainPath)
                && cfg.MainPath.Replace("\\", "/").EndsWith("/" + MirrorDirName);
        }
    }

    private static string Quote(string s)
    {
        return "'" + s.Replace("'", "'\\''") + "'";
    }
}
#else
/// <summary>非 Android 平台的占位实现，保证 Config 里可以无条件引用。</summary>
public static class AndroidRootSource
{
    public const string GamePackage = "jp.co.cygames.umamusume";
    public const string GameFiles = "";
    public const string MirrorDirName = "game_root";
    public const string BundleCacheDirName = "game_bundles";

    public static string LastError => "not an Android build";

    public static string RunAsRoot(string arguments, int timeoutMs = 120000) => null;

    public static bool IsRootMode => false;

    public static bool HasRoot() => false;

    public static bool GameDataAccessible() => false;

    public static string Mirror(string persistentRoot) => null;

    public static string EnsureBundle(string persistentRoot, string hash) => null;
}
#endif
