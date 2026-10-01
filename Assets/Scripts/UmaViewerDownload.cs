using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
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
    private const string unityPlayerUserAgent = "UnityPlayer/2019.4.21f1 (UnityWebRequest/1.0, libcurl/7.52.0-DEV)";
    private static readonly object httpClientLock = new object();
    private static HttpClient httpClient;
    private static string httpClientConfiguration;

    private sealed class DownloadResponse
    {
        public byte[] Data;
        public string Error;

        public bool IsSuccess => Data != null && string.IsNullOrEmpty(Error);
    }

    public static IEnumerator DownloadText(string url, System.Action<string> callback)
    {
        byte[] data = null;
        string error = null;
        yield return DownloadBytes(url, (responseData, responseError) =>
        {
            data = responseData;
            error = responseError;
        }, 3);

        if (data == null)
        {
            Debug.Log(error);
            callback("");
            yield break;
        }

        callback(Encoding.UTF8.GetString(data));
    }

    public static void DownloadAssetSync(UmaDatabaseEntry entry, Action<string , UIMessageType> callback = null)
    {
        string baseurl = entry.IsAssetBundle ? GetAssetRequestUrl(entry.Url) : GetGenericRequestUrl(entry.Url);
        DownloadResponse response = DownloadBytesAsync(baseurl).GetAwaiter().GetResult();
        if (!response.IsSuccess)
        {
            Debug.LogError(response.Error);
            callback?.Invoke($"Failed to download resources : {response.Error}", UIMessageType.Error);
        }
        else
        {
            Debug.Log("saving " + entry.Url);
            SaveDownloadedAsset(entry, response.Data);
        }
    }
  
    public static IEnumerator DownloadAssets(List<UmaDatabaseEntry> entries, Action<int, int, string> callback = null)
    {
        entries = entries.Where(e => !File.Exists(e.Path)).ToList();
        if (entries.Count == 0) yield break;
        callback?.Invoke(0, entries.Count, "DownLoading");
        var percent_num = (float)entries.Count / 100;
        for(int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            yield return downloadWaitUntil;
            if(i % percent_num == 0)
            {
                callback?.Invoke(i, entries.Count, "DownLoading");
            }
            CurrentCoroutinesCount++;
            downloadCoroutines.Add(Instance.StartCoroutine(DownloadTask(entry)));
        }

        yield return downloadWaitUntilComplete;
    }

    public static IEnumerator DownloadTask(UmaDatabaseEntry entry)
    {
        string baseurl = (string.IsNullOrEmpty(Path.GetExtension(entry.Name)) ? GetAssetRequestUrl(entry.Url) : GetGenericRequestUrl(entry.Url));
        byte[] data = null;
        string error = null;
        yield return DownloadBytes(baseurl, (responseData, responseError) =>
        {
            data = responseData;
            error = responseError;
        });

        if (data == null)
        {
            if (Instance)
            {
                Instance.ShowMessage($"Failed to download resources : {error}", UIMessageType.Error);
            }
        }
        else
        {
            SaveDownloadedAsset(entry, data);
        }
        CurrentCoroutinesCount--;
    }

    public static async void DownloadAssets(IEnumerable<UmaDatabaseEntry> entrys)
    {
        downloadTasks.Clear();
        foreach (var entry in entrys)
        {
            await semaphore.WaitAsync();
            downloadTasks.Add(DownloadTask(entry, semaphore));
        }
        await Task.WhenAll(downloadTasks);
    }

    public static async Task DownloadTask(UmaDatabaseEntry entry, SemaphoreSlim semaphore)
    {
        try
        {
            if (!File.Exists(entry.Path))
            {
                string baseurl = (string.IsNullOrEmpty(Path.GetExtension(entry.Name)) ? GetAssetRequestUrl(entry.Url) : GetGenericRequestUrl(entry.Url));
                DownloadResponse response = await DownloadBytesAsync(baseurl);
                if (!response.IsSuccess)
                {
                    Debug.LogError(response.Error);
                }
                else
                {
                    Debug.Log("saving " + entry.Url);
                    SaveDownloadedAsset(entry, response.Data);
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    public static IEnumerator DownloadBytes(string url, Action<byte[], string> callback, int timeoutSeconds = 0)
    {
        Task<DownloadResponse> task = DownloadBytesAsync(url, timeoutSeconds);
        yield return new WaitUntil(() => task.IsCompleted);

        DownloadResponse response = task.Status == TaskStatus.RanToCompletion
            ? task.Result
            : new DownloadResponse { Error = task.Exception?.GetBaseException().Message ?? "Download was cancelled." };
        callback?.Invoke(response.Data, response.Error);
    }

    private static async Task<DownloadResponse> DownloadBytesAsync(string url, int timeoutSeconds = 0)
    {
        string lastError = null;
        for (int attempt = 0; attempt <= downloadRetryCount; attempt++)
        {
            try
            {
                HttpClient client = GetHttpClient();
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                using var timeoutCancellation = timeoutSeconds > 0
                    ? new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds))
                    : null;
                CancellationToken cancellationToken = timeoutCancellation?.Token ?? CancellationToken.None;
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase})";
                }
                else
                {
                    return new DownloadResponse { Data = await ReadResponseBytesAsync(response).ConfigureAwait(false) };
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }

            if (attempt < downloadRetryCount)
            {
                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
        }

        return new DownloadResponse { Error = $"{lastError} (after {downloadRetryCount} retries)" };
    }

    private static async Task<byte[]> ReadResponseBytesAsync(HttpResponseMessage response)
    {
        using Stream responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var memoryStream = response.Content.Headers.ContentLength is long contentLength && contentLength >= 0 && contentLength <= int.MaxValue
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

    private static HttpClient GetHttpClient()
    {
        bool useProxy = Config.Instance != null && Config.Instance.UseNetworkProxy;
        Uri proxyUri = null;
        string configuration = "direct";
        if (useProxy)
        {
            if (!Config.Instance.TryGetNetworkProxyUri(out proxyUri, out string error))
            {
                throw new InvalidOperationException(error);
            }

            configuration = proxyUri.AbsoluteUri;
        }

        lock (httpClientLock)
        {
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
            newClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", unityPlayerUserAgent);

            HttpClient oldClient = httpClient;
            httpClient = newClient;
            httpClientConfiguration = configuration;
            oldClient?.Dispose();
            return httpClient;
        }
    }

    private static void SaveDownloadedAsset(UmaDatabaseEntry entry, byte[] data)
    {
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
        if (File.Exists(entry.Path))
        {
            File.Delete(entry.Path);
        }
        File.Move(tempPath, entry.Path);
        MarkNoBackup(entry.Path);
    }

    private static void MarkNoBackup(string path)
    {
#if UNITY_IOS || UNITY_IPHONE
        UnityEngine.iOS.Device.SetNoBackupFlag(path);
#endif
    }

    public static string GetManifestRequestUrl(string hash)
    {
        return $"{MANIFEST_ROOT_URL}/{hash.Substring(0, 2)}/{hash}";
    }

    public static string GetGenericRequestUrl(string hash)
    {
        return $"{GENERIC_BASE_URL}/{hash.Substring(0, 2)}/{hash}";
    }
    
    public static string GetAssetRequestUrl(string hash)
    {
        return $"{ASSET_BASE_URL}/{hash.Substring(0, 2)}/{hash}";
    }
}
