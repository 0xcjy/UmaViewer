
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using static UmaViewerUI;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

public class UmaViewerDownload : MonoBehaviour
{
    public static string MANIFEST_ROOT_URL = "https://prd-storage-app-umamusume.akamaized.net/dl/resources/Manifest";
    public static string GENERIC_BASE_URL = "https://prd-storage-game-umamusume.akamaized.net/dl/resources/Generic";
#if UNITY_IOS || UNITY_IPHONE || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    public static string ASSET_BASE_URL = "https://prd-storage-game-umamusume.akamaized.net/dl/resources/iOS/assetbundles/";
#elif UNITY_ANDROID
    public static string ASSET_BASE_URL = "https://prd-storage-game-umamusume.akamaized.net/dl/resources/Android/assetbundles/";
#else
    public static string ASSET_BASE_URL = "https://prd-storage-game-umamusume.akamaized.net/dl/resources/Windows/assetbundles/";
#endif

#if UNITY_ANDROID || UNITY_IOS || UNITY_IPHONE
    private const int maxConcurrentDownloads = 6;
#else
    private const int maxConcurrentDownloads = 30;
#endif

    private static SemaphoreSlim semaphore = new SemaphoreSlim(maxConcurrentDownloads);
    private static List<Coroutine> downloadCoroutines = new List<Coroutine>();
    private static int CurrentCoroutinesCount = 0;
    private static WaitUntil downloadWaitUntil = new WaitUntil(() => CurrentCoroutinesCount < maxConcurrentDownloads);
    private static WaitUntil downloadWaitUntilComplete = new WaitUntil(() => CurrentCoroutinesCount == 0);
    private static List<Task> downloadTasks = new List<Task>();
    private const int downloadRetryCount = 3;
    // A main-thread (synchronous) download must not hang the editor for long.
    private const int syncDownloadTimeoutSeconds = 10;
    // How long to wait for a TCP handshake when probing a configured proxy.
    private const int proxyProbeTimeoutMilliseconds = 400;
    private const string unityPlayerUserAgent = "UnityPlayer/2019.4.21f1 (UnityWebRequest/1.0, libcurl/7.52.0-DEV)";
    private static readonly object httpClientLock = new object();
    private static HttpClient httpClient;
    private static string httpClientConfiguration;

    // A proxy probe already known to fail must not repeat on every request: it costs up to
    // proxyProbeTimeoutMilliseconds on the calling thread and logs a warning with a stack trace.
    // The fallback to a direct connection is retried once the cooldown expires.
    private static string proxyFallbackConfiguration;
    private static DateTime proxyFallbackRetryAtUtc = DateTime.MinValue;
    private const double proxyFallbackCooldownSeconds = 60.0;

    // Files requested by callers that cannot wait for the network (the Live load runs on the
    // main thread). Deduplicated while in flight; the caller picks the file up on the next load.
    private static readonly HashSet<string> pendingBackgroundDownloads = new HashSet<string>();
    private static readonly object pendingBackgroundDownloadsLock = new object();

    private sealed class DownloadResponse
    {
        public byte[] Data;
        public string Error;
        public bool IsSuccess => Data != null && string.IsNullOrEmpty(Error);
    }

    public static IEnumerator DownloadText(string url, Action<string> callback)
    {
        StandaloneLogger.Begin("DownloadText");
        StandaloneLogger.Info("URL: " + url);

        byte[] data = null;
        string error = null;

        yield return DownloadBytes(url, (responseData, responseError) =>
        {
            data = responseData;
            error = responseError;
        }, 3);

        if (data == null)
        {
            StandaloneLogger.Fail("DownloadText failed: " + url, null);
            StandaloneLogger.Error(error ?? "No response data");
            UnityEngine.Debug.Log(error);
            callback("");
            yield break;
        }

        StandaloneLogger.Info("DownloadText success, bytes: " + data.Length);
        callback(Encoding.UTF8.GetString(data));
        StandaloneLogger.End("DownloadText");
    }

    public static void DownloadAssetSync(UmaDatabaseEntry entry, Action<string, UIMessageType> callback = null)
    {
        string baseurl = entry.IsAssetBundle
            ? GetAssetRequestUrl(entry.Url)
            : GetGenericRequestUrl(entry.Url);

        StandaloneLogger.Begin("DownloadAssetSync");
        StandaloneLogger.Info("Name: " + entry.Name);
        StandaloneLogger.Info("Hash: " + entry.Url);
        StandaloneLogger.Info("IsAssetBundle: " + entry.IsAssetBundle);
        StandaloneLogger.Info("URL: " + baseurl);
        StandaloneLogger.Info("Target: " + entry.Path);

        try
        {
            DownloadResponse response =
                DownloadBytesAsync(baseurl, syncMainThread: true).GetAwaiter().GetResult();

            if (!response.IsSuccess)
            {
                StandaloneLogger.Fail("DownloadAssetSync failed: " + entry.Name, null);
                StandaloneLogger.Error(response.Error);

                if (UmaMissingAssets.IsPermanentFailure(response.Error))
                {
                    UmaMissingAssets.MarkMissing(entry.Url, response.Error);
                }

                UnityEngine.Debug.LogError(response.Error);
                callback?.Invoke($"Failed to download resources : {response.Error}", UIMessageType.Error);
            }
            else
            {
                StandaloneLogger.Info("Download success, bytes: " + response.Data.Length);
                UmaMissingAssets.Clear(entry.Url);
                SaveDownloadedAsset(entry, response.Data);
                StandaloneLogger.End("DownloadAssetSync");
            }
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("DownloadAssetSync: " + entry.Name, ex);
            callback?.Invoke($"Failed to download resources : {ex.Message}", UIMessageType.Error);
        }
    }
    // Non-blocking counterpart of DownloadAssetSync, for callers on the main thread that can live
    // without the file this frame. `UmaDatabaseEntry.FilePath` downloads on demand, so touching it
    // from the Live load froze the editor for up to syncDownloadTimeoutSeconds per missing file.
    public static void RequestDownload(UmaDatabaseEntry entry)
    {
        if (entry == null)
            return;

        string path = entry.Path;
        lock (pendingBackgroundDownloadsLock)
        {
            if (File.Exists(path) || !pendingBackgroundDownloads.Add(path))
                return;
        }

        DownloadInBackground(entry, path);
    }

    private static async void DownloadInBackground(UmaDatabaseEntry entry, string path)
    {
        try
        {
            string baseurl = entry.IsAssetBundle ? GetAssetRequestUrl(entry.Url) : GetGenericRequestUrl(entry.Url);
            DownloadResponse response = await DownloadBytesAsync(baseurl).ConfigureAwait(false);
            if (!response.IsSuccess)
            {
                if (UmaMissingAssets.IsPermanentFailure(response.Error))
                {
                    UmaMissingAssets.MarkMissing(entry.Url, response.Error);
                }

                UnityEngine.Debug.LogWarning($"[UmaViewerDownload] Background download failed for {entry.Name}: {response.Error}");
                return;
            }

            UmaMissingAssets.Clear(entry.Url);
            SaveDownloadedAsset(entry, response.Data);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[UmaViewerDownload] Background download failed for {entry.Name}: {ex.Message}");
        }
        finally
        {
            lock (pendingBackgroundDownloadsLock)
                pendingBackgroundDownloads.Remove(path);
        }
    }

    public static IEnumerator DownloadAssets(List<UmaDatabaseEntry> entries, Action<int, int, string> callback = null)
    {
        StandaloneLogger.Begin("DownloadAssets coroutine");
        StandaloneLogger.Info("Input entries: " + (entries == null ? -1 : entries.Count));

        entries = entries.Where(e => !File.Exists(e.Path) && !UmaMissingAssets.IsMissing(e.Url)).ToList();

        StandaloneLogger.Info("Missing entries: " + entries.Count);

        if (entries.Count == 0)
        {
            StandaloneLogger.End("DownloadAssets coroutine: nothing to download");
            yield break;
        }

        callback?.Invoke(0, entries.Count, "DownLoading");
        var percent_num = (float)entries.Count / 100;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            yield return downloadWaitUntil;

            if (i % percent_num == 0)
            {
                callback?.Invoke(i, entries.Count, "DownLoading");
            }

            CurrentCoroutinesCount++;
            downloadCoroutines.Add(Instance.StartCoroutine(DownloadTask(entry)));
        }

        yield return downloadWaitUntilComplete;
        StandaloneLogger.End("DownloadAssets coroutine");
    }

    public static IEnumerator DownloadTask(UmaDatabaseEntry entry)
    {
        if (UmaMissingAssets.IsMissing(entry.Url))
        {
            yield break;
        }

        string baseurl = string.IsNullOrEmpty(Path.GetExtension(entry.Name))
            ? GetAssetRequestUrl(entry.Url)
            : GetGenericRequestUrl(entry.Url);

        StandaloneLogger.Begin("DownloadTask");
        StandaloneLogger.Info("Name: " + entry.Name);
        StandaloneLogger.Info("URL: " + baseurl);
        StandaloneLogger.Info("Target: " + entry.Path);

        byte[] data = null;
        string error = null;

        yield return DownloadBytes(baseurl, (responseData, responseError) =>
        {
            data = responseData;
            error = responseError;
        });

        if (data == null)
        {
            StandaloneLogger.Error("DownloadTask failed: " + entry.Name + " : " + error);

            if (UmaMissingAssets.IsPermanentFailure(error))
            {
                UmaMissingAssets.MarkMissing(entry.Url, error);
            }

            if (Instance)
            {
                Instance.ShowMessage($"Failed to download resources : {error}", UIMessageType.Error);
            }
        }
        else
        {
            try
            {
                SaveDownloadedAsset(entry, data);
                StandaloneLogger.Info("Saved bytes: " + data.Length);
            }
            catch (Exception ex)
            {
                StandaloneLogger.Exception("DownloadTask SaveDownloadedAsset: " + entry.Name, ex);
            }
        }

        CurrentCoroutinesCount--;
        StandaloneLogger.End("DownloadTask");
    }

    public static async void DownloadAssets(IEnumerable<UmaDatabaseEntry> entrys)
    {
        StandaloneLogger.Begin("DownloadAssets async");

        downloadTasks.Clear();

        foreach (var entry in entrys)
        {
            await semaphore.WaitAsync();
            downloadTasks.Add(DownloadTask(entry, semaphore));
        }

        await Task.WhenAll(downloadTasks);

        StandaloneLogger.End("DownloadAssets async");
    }

    public static async Task DownloadTask(UmaDatabaseEntry entry, SemaphoreSlim semaphore)
    {
        try
        {
            if (!File.Exists(entry.Path))
            {
                string baseurl = string.IsNullOrEmpty(Path.GetExtension(entry.Name))
                    ? GetAssetRequestUrl(entry.Url)
                    : GetGenericRequestUrl(entry.Url);

                StandaloneLogger.Info("Async Download: " + entry.Name);
                StandaloneLogger.Info("URL: " + baseurl);

                DownloadResponse response = await DownloadBytesAsync(baseurl);

                if (!response.IsSuccess)
                {
                    StandaloneLogger.Error("Async Download failed: " + response.Error);
                    UnityEngine.Debug.LogError(response.Error);
                }
                else
                {
                    SaveDownloadedAsset(entry, response.Data);
                    StandaloneLogger.Info("Async Download saved: " + entry.Path);
                }
            }
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("DownloadTask async: " + entry.Name, ex);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static IEnumerator DownloadBytes(string url, Action<byte[], string> callback, int timeoutSeconds = 0)
    {
        StandaloneLogger.Info("DownloadBytes requested: " + url);
        StandaloneLogger.Info("Timeout seconds: " + timeoutSeconds);

        Task<DownloadResponse> task = DownloadBytesAsync(url, timeoutSeconds);
        yield return new WaitUntil(() => task.IsCompleted);

        DownloadResponse response = task.Status == TaskStatus.RanToCompletion
            ? task.Result
            : new DownloadResponse
            {
                Error = task.Exception?.GetBaseException().Message ?? "Download was cancelled."
            };

        if (response.IsSuccess)
        {
            StandaloneLogger.Info("DownloadBytes completed: " + response.Data.Length + " bytes");
        }
        else
        {
            StandaloneLogger.Error("DownloadBytes failed: " + response.Error);
        }

        callback?.Invoke(response.Data, response.Error);
    }

    private static async Task<DownloadResponse> DownloadBytesAsync(string url, int timeoutSeconds = 0, bool syncMainThread = false)
    {
        // A synchronous (main-thread) request must fail fast: no retries and no waiting
        // between attempts, otherwise a dead endpoint freezes the whole editor.
        int attempts = syncMainThread ? 1 : downloadRetryCount + 1;
        int effectiveTimeout = timeoutSeconds > 0 ? timeoutSeconds : (syncMainThread ? syncDownloadTimeoutSeconds : 0);

        string lastError = null;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                StandaloneLogger.Info(
                    "HTTP GET attempt " + (attempt + 1)
                    + "/" + (downloadRetryCount + 1)
                    + ": " + url);

                HttpClient client = IsApiEndpoint(url)
                    ? GetApiHttpClient()
                    : GetHttpClient();

                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                using var timeoutCancellation = effectiveTimeout > 0
                    ? new CancellationTokenSource(TimeSpan.FromSeconds(effectiveTimeout))
                    : null;

                CancellationToken cancellationToken =
                    timeoutCancellation?.Token ?? CancellationToken.None;

                using HttpResponseMessage response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";

                    StandaloneLogger.Error(
                        "HTTP failed: " + lastError
                        + " URL: " + url);

                    if (UmaMissingAssets.IsPermanentFailure(lastError))
                    {
                        // 404/403/410: retrying cannot help, fail this request immediately.
                        StandaloneLogger.Warning(
                            "Permanent server rejection, retries skipped: " + url);

                        return new DownloadResponse { Error = lastError };
                    }
                }
                else
                {
                    byte[] data = await ReadResponseBytesAsync(response).ConfigureAwait(false);

                    stopwatch.Stop();

                    StandaloneLogger.RecordDownload(
                        url,
                        attempt + 1,
                        (int)response.StatusCode,
                        data.Length,
                        stopwatch.ElapsedMilliseconds);

                    return new DownloadResponse { Data = data };
                }
            }
            catch (Exception ex)
            {
                lastError = ex.ToString();

                stopwatch.Stop();

                StandaloneLogger.Exception(
                    "HTTP attempt " + (attempt + 1) + ": " + url,
                    ex);

                // Direct connection failed (timeout / reset, typically SNI filtering):
                // switch to the configured proxy and retry without using up the budget.
                if (!autoProxyActive && TryEnableAutoProxy())
                {
                    attempt--;
                }
            }

            if (attempt < attempts - 1)
            {
                StandaloneLogger.Warning("Retrying after 3 seconds");
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
        }

        StandaloneLogger.Fail("HTTP download exhausted retries: " + url, null);

        return new DownloadResponse
        {
            Error = attempts > 1 ? $"{lastError} (after {attempts - 1} retries)" : lastError
        };
    }

    private static async Task<byte[]> ReadResponseBytesAsync(HttpResponseMessage response)
    {
        using Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

        using var memoryStream =
            response.Content.Headers.ContentLength is long contentLength
            && contentLength >= 0
            && contentLength <= int.MaxValue
                ? new MemoryStream((int)contentLength)
                : new MemoryStream();

        var buffer = new byte[64 * 1024];
        int bytesRead;

        while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
        {
            await memoryStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
        }

        return memoryStream.ToArray();
    }

    // Dedicated client for the metadata API: from mainland China that host is SNI-filtered
    // over TLS 1.2 (the handshake gets a forged FIN) while TLS 1.3 goes through, so the API
    // client pins 1.3 and leaves the CDN traffic on the platform default.
    private static HttpClient apiHttpClient;
    private static readonly object apiHttpClientLock = new object();

    private const string ApiHost = "umapi.buenavista.fans";
    private const int Tls13Value = 12288;

    private static bool IsApiEndpoint(string url)
    {
        return !string.IsNullOrEmpty(url)
            && url.IndexOf(ApiHost, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Set once when a direct request times out or fails: the next attempt goes through the
    /// configured local proxy. Reset per session only.
    /// </summary>
    private static bool autoProxyActive;

    /// <summary>
    /// Direct connections usually work; when they do not (SNI filtering, blocked edge), retry
    /// through the configured proxy instead of failing the whole download.
    /// </summary>
    private static bool TryEnableAutoProxy()
    {
        if (autoProxyActive) return false;

        if (Config.Instance == null) return false;

        // Only Auto performs the direct -> proxy fallback; Off stays direct, On never gets here.
        if (Config.Instance.NetworkProxyMode != ProxyMode.Auto) return false;

        if (!Config.Instance.TryGetNetworkProxyUri(out Uri proxyUri, out string error))
        {
            StandaloneLogger.Warning(
                "Auto-proxy: direct request failed and the proxy setting is unusable (" + error + ")");

            return false;
        }

        if (!IsProxyReachable(proxyUri))
        {
            StandaloneLogger.Warning(
                "Auto-proxy: direct request failed and " + proxyUri.AbsoluteUri + " is not reachable");

            return false;
        }

        lock (httpClientLock)
        {
            autoProxyActive = true;
            httpClient = null;
            httpClientConfiguration = null;
        }

        lock (apiHttpClientLock)
        {
            apiHttpClient = null;
        }

        StandaloneLogger.Warning(
            "Auto-proxy: direct connection failed, switching to " + proxyUri.AbsoluteUri);

        return true;
    }

    /// <summary>Drops the cached clients so the next request picks up a changed proxy setting.</summary>
    public static void ResetProxyClients()
    {
        lock (httpClientLock)
        {
            httpClient = null;
            httpClientConfiguration = null;
        }

        lock (apiHttpClientLock)
        {
            apiHttpClient = null;
        }

        autoProxyActive = false;

        StandaloneLogger.Info("HTTP clients reset after a proxy change");
    }

    /// <summary>Blocking GET returning the body as UTF-8 text (for callers that cannot await).</summary>
    public static string DownloadTextSync(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        try
        {
            DownloadResponse response = DownloadBytesAsync(url, syncMainThread: true)
                .GetAwaiter().GetResult();

            if (response.IsSuccess)
            {
                return Encoding.UTF8.GetString(response.Data);
            }

            StandaloneLogger.Warning("Blocking download failed: " + url + " | " + response.Error);
        }
        catch (Exception ex)
        {
            StandaloneLogger.Exception("Blocking download exception: " + url, ex);
        }

        return null;
    }

    private static HttpClient GetApiHttpClient()
    {
        lock (apiHttpClientLock)
        {
            if (apiHttpClient != null) return apiHttpClient;

            ProxyMode proxyMode = Config.Instance != null
                ? Config.Instance.NetworkProxyMode
                : ProxyMode.Off;

            bool useProxy = proxyMode == ProxyMode.On
                || (proxyMode == ProxyMode.Auto && autoProxyActive);
            Uri proxyUri = null;

            if (useProxy && !Config.Instance.TryGetNetworkProxyUri(out proxyUri, out string error))
            {
                UnityEngine.Debug.LogWarning(
                    $"[UmaViewerDownload] API proxy is invalid ({error}); using a direct connection.");
                useProxy = false;
            }

            HttpClientHandler handler = new HttpClientHandler { UseProxy = useProxy };

            if (useProxy)
            {
                handler.Proxy = new WebProxy(proxyUri);
            }

            // Reflection: the SslProtocols member and even the handler property are missing
            // from some Mono/Unity API profiles, and a compile-time reference would break
            // the build there.
            //
            // Also push the value into ServicePointManager: Mono's HttpClient talks through
            // MonoWebRequestHandler, which consults ServicePointManager before creating the
            // SslStream. That is the only client-side lever that can actually restrict the
            // protocol on this runtime — the handler property alone is often ignored.
            try
            {
                System.Net.ServicePointManager.SecurityProtocol =
                    (System.Net.SecurityProtocolType)Tls13Value;

                StandaloneLogger.Info("API client: ServicePointManager.SecurityProtocol = TLS 1.3");
            }
            catch (Exception spmEx)
            {
                StandaloneLogger.Warning(
                    "API client: could not set ServicePointManager.SecurityProtocol (" +
                    spmEx.GetType().Name + ")");
            }

            try
            {
                var tlsProperty = handler.GetType().GetProperty("SslProtocols");

                if (tlsProperty != null && tlsProperty.CanWrite)
                {
                    tlsProperty.SetValue(
                        handler,
                        Enum.ToObject(tlsProperty.PropertyType, Tls13Value));

                    StandaloneLogger.Info("API client: TLS pinned to 1.3 for " + ApiHost);
                }
                else
                {
                    StandaloneLogger.Warning(
                        "API client: this runtime cannot pin a TLS version; using the platform default");
                }
            }
            catch (Exception ex)
            {
                StandaloneLogger.Warning(
                    "API client: pinning TLS 1.3 failed (" + ex.GetType().Name + ": " +
                    ex.Message + "); using the platform default");
            }

            apiHttpClient = new HttpClient(handler, true)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            StandaloneLogger.Info(
                "API client created (proxy=" + (useProxy ? proxyUri.AbsoluteUri : "direct") + ")");

            return apiHttpClient;
        }
    }

    private static HttpClient GetHttpClient()
    {
        lock (httpClientLock)
        {
            ProxyMode proxyMode = Config.Instance != null
                ? Config.Instance.NetworkProxyMode
                : ProxyMode.Off;

            bool useProxy = proxyMode == ProxyMode.On
                || (proxyMode == ProxyMode.Auto && autoProxyActive);
            Uri proxyUri = null;
            string configuration = "direct";
            if (useProxy)
            {
                if (!Config.Instance.TryGetNetworkProxyUri(out proxyUri, out string error))
                {
                    UnityEngine.Debug.LogWarning($"[UmaViewerDownload] Network proxy is enabled but invalid ({error}); using a direct connection.");
                    useProxy = false;
                }
                else
                {
                    configuration = proxyUri.AbsoluteUri;

                    // A probe that already failed is remembered: without this the client is cached
                    // as "direct" but requested as the proxy URI, so the probe ran again (and logged
                    // another warning) on every single request.
                    if (string.Equals(proxyFallbackConfiguration, configuration, StringComparison.Ordinal) &&
                        DateTime.UtcNow < proxyFallbackRetryAtUtc)
                    {
                        useProxy = false;
                        configuration = "direct";
                    }
                }
            }

            StandaloneLogger.Info("HTTP proxy enabled: " + useProxy);
            StandaloneLogger.Info("HTTP configuration: " + configuration);

            if (httpClient != null && httpClientConfiguration == configuration)
            {
                return httpClient;
            }

            // A configured proxy that does not answer would make every single download
            // fail (e.g. "Connection refused" on a local proxy that is not running).
            // Probe it once when building the client and fall back to a direct request.
            if (useProxy && !IsProxyReachable(proxyUri))
            {
                UnityEngine.Debug.LogWarning($"[UmaViewerDownload] Network proxy {proxyUri} is not reachable; using a direct connection.");
                proxyFallbackConfiguration = configuration;
                proxyFallbackRetryAtUtc = DateTime.UtcNow.AddSeconds(proxyFallbackCooldownSeconds);
                useProxy = false;
                configuration = "direct";
            }
            else if (useProxy)
            {
                // Reachable again: drop the remembered failure so the proxy keeps being used.
                proxyFallbackConfiguration = null;
            }

            if (httpClient != null && httpClientConfiguration == configuration)
            {
                return httpClient;
            }

            var handler = new HttpClientHandler
            {
                UseProxy = useProxy
            };

            if (useProxy)
            {
                handler.Proxy = new WebProxy(proxyUri);
            }

            var newClient = new HttpClient(handler, true)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            newClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                unityPlayerUserAgent);

            HttpClient oldClient = httpClient;

            httpClient = newClient;
            httpClientConfiguration = configuration;

            oldClient?.Dispose();

            StandaloneLogger.Info("HttpClient created: " + configuration);

            return httpClient;
        }
    }

    private static bool IsProxyReachable(Uri proxyUri)
    {
        if (proxyUri == null)
            return false;

        try
        {
            using var probe = new TcpClient();
            IAsyncResult result = probe.BeginConnect(proxyUri.Host, proxyUri.Port, null, null);
            bool connected = result.AsyncWaitHandle.WaitOne(proxyProbeTimeoutMilliseconds);
            if (!connected)
                return false;

            probe.EndConnect(result);
            return probe.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveDownloadedAsset(UmaDatabaseEntry entry, byte[] data)
    {
        StandaloneLogger.Begin("SaveDownloadedAsset");
        StandaloneLogger.Info("Name: " + entry.Name);
        StandaloneLogger.Info("Target: " + entry.Path);
        StandaloneLogger.Info("Bytes: " + data.Length);

        var directory = Path.GetDirectoryName(entry.Path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            MarkNoBackup(directory);
        }

        var tempPath = entry.Path + ".tmp";

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        File.WriteAllBytes(tempPath, data);

        StandaloneLogger.RecordPath("Temporary file", tempPath);

        if (File.Exists(entry.Path))
        {
            File.Delete(entry.Path);
        }

        File.Move(tempPath, entry.Path);
        MarkNoBackup(entry.Path);

        StandaloneLogger.RecordPath("Saved asset", entry.Path);
        StandaloneLogger.End("SaveDownloadedAsset");
    }

    private static void MarkNoBackup(string path)
    {
#if UNITY_IOS || UNITY_IPHONE
        UnityEngine.iOS.Device.SetNoBackupFlag(path);
#endif
    }

    public static string GetManifestRequestUrl(string hash)
    {
        string url = BuildResourceUrl(MANIFEST_ROOT_URL, hash);
        StandaloneLogger.Info("Manifest URL: " + url);
        return url;
    }

    public static string GetGenericRequestUrl(string hash)
    {
        string url = BuildResourceUrl(GENERIC_BASE_URL, hash);
        StandaloneLogger.Info("Generic URL: " + url);
        return url;
    }

    public static string GetAssetRequestUrl(string hash)
    {
        string url = BuildResourceUrl(ASSET_BASE_URL, hash);
        StandaloneLogger.Info("AssetBundle URL: " + url);
        return url;
    }

    /// <summary>
    /// Joins a base URL and a content hash into {base}/{hash[0:2]}/{hash}.
    /// ASSET_BASE_URL already ends with a slash, and the previous
    /// "{base}/{xx}/{hash}" form produced "assetbundles//VH/..." which the CDN
    /// answered with 404 for every asset bundle.
    /// </summary>
    private static string BuildResourceUrl(string baseUrl, string hash)
    {
        if (string.IsNullOrEmpty(hash) || hash.Length < 2)
        {
            StandaloneLogger.Error(
                "Cannot build a resource URL: hash is null or shorter than 2 chars");

            return null;
        }

        string root = (baseUrl ?? string.Empty).TrimEnd('/');

        return root + "/" + hash.Substring(0, 2) + "/" + hash;
    }
}
