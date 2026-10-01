using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
//绠＄悊 AssetBundle 鐨勫姞杞姐€佸紩鐢ㄨ鏁般€佷緷璧栧叧绯诲拰閲婃斁
public class UmaAssetManager : MonoBehaviour
{
    private sealed class BundleHandle
    {
        public string Name;
        public UmaDatabaseEntry Entry;
        public AssetBundle Bundle;
        public UmaAssetBundleStream Stream;
        public int RefCount;
        public bool IsLoaded;
        public bool NeverUnload;
    }

    private readonly struct LoadItem
    {
        public readonly UmaDatabaseEntry Entry;
        public readonly bool NeverUnload;

        public LoadItem(UmaDatabaseEntry entry, bool neverUnload)
        {
            Entry = entry;
            NeverUnload = neverUnload;
        }
    }

    public static UmaAssetManager instance;

    private readonly Dictionary<string, AssetBundle> LoadedBundles =
        new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, AssetBundle> NeverUnload =
        new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, BundleHandle> Handles =
        new Dictionary<string, BundleHandle>(StringComparer.OrdinalIgnoreCase);

    private const int MaxLoadsPerFrame = 4;

    public static Shader HairShader,
        FaceShader,
        EyeShader,
        CheekShader,
        EyebrowShader,
        AlphaShader,
        BodyAlphaShader,
        BodyBehindAlphaShader;

    public static event Action<UmaDatabaseEntry> OnLoadedBundleUpdate;
    public static event Action<UmaDatabaseEntry> OnLoadedBundleRemove;
    public static event Action OnLoadedBundleClear;
    public static event Action<int, int, string> OnLoadProgressChange;

    public static Coroutine LoadCoroutine;

    /// <summary>Set true before loading a Live to emit phased bundle counts to the Unity log.</summary>
    public static bool EnableLiveDiagnostics = false;

    private static bool _diagActive;
    private static int _diagHandleReuse;
    private static int _diagNewOpened;
    private static int _diagFileMissing;
    private static int _diagLoadFailed;

    private static void ResetDiagCounters()
    {
        _diagHandleReuse = 0;
        _diagNewOpened   = 0;
        _diagFileMissing = 0;
        _diagLoadFailed  = 0;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            DestroyImmediate(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        ReleaseAllInternal(false, true, false);
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        ReleaseAllInternal(false, true, false);
        instance = null;
    }

    public static void PreLoadAndRun(List<UmaDatabaseEntry> entries, Action onDone)
    {
        if (instance == null)
        {
            Debug.LogError("[UmaAssetManager] instance is null.");
            onDone?.Invoke();
            return;
        }

        if (LoadCoroutine != null)
            return;

        LoadCoroutine = instance.StartCoroutine(instance.PreLoadAsset(entries, onDone));
    }

    private IEnumerator PreLoadAsset(List<UmaDatabaseEntry> entries, Action onDone)
    {
        _diagActive = Gallop.Live.LiveRuntimeDiagnostics.IsRequested();
        if (_diagActive)
        {
            ResetDiagCounters();
            Debug.Log($"[LiveDiag] PreLoadAsset begin. raw_input={entries?.Count ?? 0}");
        }

        List<UmaDatabaseEntry> roots = DeduplicateEntries(entries);

        if (_diagActive)
            Debug.Log($"[LiveDiag] roots_deduped={roots.Count}");

        // ExpandUniqueEntries 銇儉銈︺兂銉兗銉夊璞°伄瑷堢畻銇伄銇夸娇銇嗐€?
        List<UmaDatabaseEntry> downloadEntries = ExpandUniqueEntries(roots);

        if (_diagActive)
            Debug.Log($"[LiveDiag] download_entries_unique={downloadEntries.Count}");

        if (Config.Instance.WorkMode == WorkMode.Standalone)
        {
            yield return UmaViewerDownload.DownloadAssets(
                downloadEntries,
                UmaSceneController.instance.LoadingProgressChange);
        }

        // 姣忎釜鏍硅姹備繚鐣欒嚜宸辩殑渚濊禆寮曠敤锛涘叡浜緷璧栦細鍍忓畼鏂逛竴鏍峰鍔犲娆?refCount銆?
        List<LoadItem> loadItems = new List<LoadItem>();
        // A request is counted again if another root expands to the same bundle.
        // Keep this set diagnostics-only: it must not deduplicate the actual load plan.
        HashSet<string> seenRequests = _diagActive
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;

        for (int i = 0; i < roots.Count; i++)
        {
            UmaDatabaseEntry root = roots[i];
            List<UmaDatabaseEntry> requests = SearchAB(UmaViewerMain.Instance, root);
            int repeated = 0;
            int added = 0;
            for (int j = 0; j < requests.Count; j++)
            {
                UmaDatabaseEntry request = requests[j];
                if (request == null)
                    continue;
                loadItems.Add(new LoadItem(request, false));
                added++;
                if (_diagActive && !seenRequests.Add(request.Name ?? string.Empty))
                    repeated++;
            }
            if (_diagActive)
                Gallop.Live.LiveRuntimeDiagnostics.RecordPreloadRoot(
                    i, root.Name, added, repeated);
        }

        if (_diagActive)
        {
            Debug.Log($"[LiveDiag] load_items_with_duplicates={loadItems.Count}");
            Gallop.Live.LiveRuntimeDiagnostics.RecordPreloadCounts(
                entries?.Count ?? 0, roots.Count, downloadEntries.Count, loadItems.Count);
        }

        int completed = 0;

        for (int i = 0; i < loadItems.Count; i++)
        {
            LoadItem item = loadItems[i];
            AcquireOne(item.Entry, item.NeverUnload);

            completed++;
            OnLoadProgressChange?.Invoke(completed, loadItems.Count, "Loading");
            if (_diagActive && (completed == 1 || completed == loadItems.Count || completed % 64 == 0))
                Gallop.Live.LiveRuntimeDiagnostics.RecordProgress(completed, loadItems.Count, "Loading");
            // 鍒嗗抚鍔犺浇锛岄伩鍏嶅崟甯ц€楁椂杩囬暱
            if (completed % MaxLoadsPerFrame == 0)
                yield return null;
        }

        if (_diagActive)
        {
            Debug.Log(
                $"[LiveDiag] PreLoadAsset done. " +
                $"handle_reuse={_diagHandleReuse} " +
                $"new_opened={_diagNewOpened} " +
                $"file_missing={_diagFileMissing} " +
                $"load_failed={_diagLoadFailed} " +
                $"unique_loaded_handles={Handles.Count(kv => kv.Value.IsLoaded)}");
            Gallop.Live.LiveRuntimeDiagnostics.RecordPreloadComplete(
                _diagHandleReuse, _diagNewOpened, _diagFileMissing, _diagLoadFailed,
                Handles.Count(kv => kv.Value.IsLoaded));
        }

        OnLoadProgressChange?.Invoke(-1, loadItems.Count, null);
        if (_diagActive)
            Gallop.Live.LiveRuntimeDiagnostics.RecordProgress(-1, loadItems.Count, "complete");
        LoadCoroutine = null;
        onDone?.Invoke();
    }

    public static AssetBundle LoadAssetBundle(
        UmaDatabaseEntry entry,
        bool neverUnload = false,
        bool isRecursive = true)
    {
        if (instance == null || entry == null)
            return null;

        if (_diagActive)
            Gallop.Live.LiveRuntimeDiagnostics.RecordBundleRequest(entry.Name, "runtime", isRecursive);

        if (isRecursive)
        {
            List<UmaDatabaseEntry> requests = SearchAB(UmaViewerMain.Instance, entry);

            for (int i = 0; i < requests.Count; i++)
            {
                UmaDatabaseEntry request = requests[i];
                if (request != null)
                    AcquireOne(request, neverUnload);
            }
        }
        else
        {
            AcquireOne(entry, neverUnload);
        }

        return Get(entry);
    }

    public static void UnloadAssetBundle(UmaDatabaseEntry entry, bool unloadAllObjects)
    {
        if (instance == null || entry == null)
            return;

        List<UmaDatabaseEntry> requests = SearchAB(UmaViewerMain.Instance, entry);

        // 瀹樻柟 TryUnload 浠庝緷璧栬姹傛暟缁勫熬閮ㄥ紑濮嬪弽鍚?Dereference銆?
        for (int i = requests.Count - 1; i >= 0; i--)
        {
            UmaDatabaseEntry request = requests[i];
            if (request != null)
                Dereference(request.Name, unloadAllObjects, false);
        }
    }

    private static bool AcquireOne(UmaDatabaseEntry entry, bool neverUnload)
    {
        if (instance == null || entry == null || string.IsNullOrEmpty(entry.Name))
            return false;

        string key = NormalizeName(entry.Name);
        BundleHandle handle = GetOrCreateHandle(key, entry);

        if (neverUnload)
            handle.NeverUnload = true;

        // 鍏煎椤圭洰涓粛鐒剁洿鎺ヨ皟鐢?AssetBundle.Unload鐨勬棫浠ｇ爜
        // Unity閿€姣?AssetBundle鍚庯紝鎵樼寮曠敤浠嶇劧瀛樺湪锛屼絾bundle == null浼氳繑鍥?true 杩欐椂蹇呴』娓呮帀鏃andle鍜?Stream,鍐嶄粠鏂囦欢閲嶆柊鍔犺浇
        if (entry.IsAssetBundle && handle.IsLoaded && handle.Bundle == null)
            ResetExternallyUnloadedHandle(handle);

        if (handle.IsLoaded)
        {
            if (_diagActive)
            {
                _diagHandleReuse++;
                Gallop.Live.LiveRuntimeDiagnostics.RecordBundleAcquire(entry.Name, "handle_reuse", "bundle");
            }
            handle.RefCount++;
            SyncLegacyDictionaries(handle);
            return true;
        }

        string filePath = entry.FilePath;

        if (!File.Exists(filePath))
        {
            if (_diagActive)
            {
                _diagFileMissing++;
                Gallop.Live.LiveRuntimeDiagnostics.RecordBundleAcquire(entry.Name, "file_missing", "bundle");
            }
            Debug.LogError($"{entry.Name} - {filePath} does not exist");
            UmaViewerUI.Instance?.ShowMessage(
                $"{entry.Name} - {filePath} does not exist",
                UIMessageType.Error);
            return false;
        }

        if (!entry.IsAssetBundle)
        {
            if (_diagActive)
            {
                _diagNewOpened++;
                Gallop.Live.LiveRuntimeDiagnostics.RecordBundleAcquire(entry.Name, "non_bundle", "bundle");
            }
            handle.Bundle = null;
            handle.Stream = null;
            handle.IsLoaded = true;
            handle.RefCount++;
            SyncLegacyDictionaries(handle);
            OnLoadedBundleUpdate?.Invoke(entry);
            return true;
        }

        AssetBundle bundle = null;
        UmaAssetBundleStream stream = null;

        try
        {
            if (!entry.IsEncrypted)
            {
                bundle = AssetBundle.LoadFromFile(filePath);
            }
            else
            {
                // 瀹樻柟 LoadOne锛氬悓涓€涓?Handle 宸插姞杞芥椂鍙?++refCount锛屼笉閲嶅鎵撳紑鏂囦欢銆?
                stream = new UmaAssetBundleStream(filePath, entry.FKey);
                bundle = AssetBundle.LoadFromStream(stream);
            }

            if (bundle == null)
            {
                if (_diagActive)
                {
                    _diagLoadFailed++;
                    Gallop.Live.LiveRuntimeDiagnostics.RecordBundleAcquire(entry.Name, "load_failed", "bundle");
                }
                stream?.Dispose();
                Debug.LogError(filePath + " exists and doesn't work");
                UmaViewerUI.Instance?.ShowMessage(
                    filePath + " exists and doesn't work",
                    UIMessageType.Error);
                return false;
            }

            if (_diagActive)
            {
                _diagNewOpened++;
                Gallop.Live.LiveRuntimeDiagnostics.RecordBundleAcquire(entry.Name, "opened", "bundle");
            }
            handle.Bundle = bundle;
            handle.Stream = stream;
            handle.IsLoaded = true;
            handle.RefCount++;

            RegisterLoadedBundle(handle);
            return true;
        }
        catch (Exception exception)
        {
            if (_diagActive)
            {
                _diagLoadFailed++;
                Gallop.Live.LiveRuntimeDiagnostics.RecordBundleAcquire(entry.Name, "exception", "bundle");
            }
            if (bundle != null)
            {
                try
                {
                    bundle.Unload(true);
                }
                catch
                {
                    // 淇濈暀鍘熷寮傚父
                }
            }

            stream?.Dispose();
            handle.Bundle = null;
            handle.Stream = null;
            handle.IsLoaded = false;
            handle.RefCount = 0;
            Debug.LogException(exception);
            return false;
        }
    }

    private static BundleHandle GetOrCreateHandle(string key, UmaDatabaseEntry entry)
    {
        if (!instance.Handles.TryGetValue(key, out BundleHandle handle))
        {
            handle = new BundleHandle
            {
                Name = key,
                Entry = entry
            };
            instance.Handles.Add(key, handle);
        }
        else if (handle.Entry == null)
        {
            handle.Entry = entry;
        }

        return handle;
    }

    private static void ResetExternallyUnloadedHandle(BundleHandle handle)
    {
        if (handle == null)
            return;

        // AssetBundle宸茬粡琚閮ㄤ唬鐮乁nload,鍏堝叧闂垜浠粛鎸佹湁鐨勫姞瀵哠tream
        if (handle.Stream != null)
        {
            try
            {
                handle.Stream.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        handle.Bundle = null;
        handle.Stream = null;
        handle.IsLoaded = false;
        handle.RefCount = 0;

        if (instance != null)
        {
            instance.LoadedBundles.Remove(handle.Name);
            instance.NeverUnload.Remove(handle.Name);
        }
    }

    private static void RegisterLoadedBundle(BundleHandle handle)
    {
        AssetBundle bundle = handle.Bundle;

        if (bundle != null && bundle.name == "shader.a")
        {
            handle.NeverUnload = true;

            EyeShader = bundle.LoadAsset<Shader>(
                "assets/_gallop/resources/shader/3d/character/charactertooneyet.shader");
            FaceShader = bundle.LoadAsset<Shader>(
                "assets/_gallop/resources/shader/3d/character/charactertoonfacetser.shader");
            HairShader = bundle.LoadAsset<Shader>(
                "assets/_gallop/resources/shader/3d/character/charactertoonhairtser.shader");
            AlphaShader = bundle.LoadAsset<Shader>(
                "assets/_gallop/resources/shader/3d/character/characteralphanolinetoonhairtser.shader");
            CheekShader = bundle.LoadAsset<Shader>(
                "assets/_gallop/resources/shader/3d/character/charactermultiplycheek.shader");
            EyebrowShader = bundle.LoadAsset<Shader>(
                "assets/_gallop/resources/shader/3d/character/charactertoonmayu.shader");
            BodyAlphaShader = bundle.LoadAsset<Shader>(
                "assets/_gallop/resources/shader/3d/character/characteralphanolinetoontser.shader");
            BodyBehindAlphaShader = bundle.LoadAsset<Shader>(
                "assets/_gallop/resources/shader/3d/character/characteralphanolinetoonbehindtser.shader");
        }

        SyncLegacyDictionaries(handle);

        if (!handle.NeverUnload && handle.Entry != null)
            OnLoadedBundleUpdate?.Invoke(handle.Entry);
    }

    private static void SyncLegacyDictionaries(BundleHandle handle)
    {
        instance.LoadedBundles[handle.Name] = handle.Bundle;

        if (handle.NeverUnload)
            instance.NeverUnload[handle.Name] = handle.Bundle;
        else
            instance.NeverUnload.Remove(handle.Name);
    }

    private static void Dereference(string name, bool unloadAllObjects, bool force)
    {
        if (instance == null || string.IsNullOrEmpty(name))
            return;

        string key = NormalizeName(name);

        if (!instance.Handles.TryGetValue(key, out BundleHandle handle))
            return;

        if (!handle.IsLoaded)
            return;

        if (handle.RefCount > 0)
            handle.RefCount--;

        if (force || (handle.RefCount <= 0 && !handle.NeverUnload))
            UnloadHandle(handle, unloadAllObjects);
    }

    private static void UnloadHandle(BundleHandle handle, bool unloadAllObjects)
    {
        AssetBundle bundle = handle.Bundle;
        UmaAssetBundleStream stream = handle.Stream;

        // Unity瑕佹眰鍏圲nload AssetBundle鍐嶉噴鏀句紶缁橪oadFromStream鐨凷tream
        if (bundle != null)
        {
            try
            {
                bundle.Unload(unloadAllObjects);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        if (stream != null)
        {
            try
            {
                stream.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        handle.Bundle = null;
        handle.Stream = null;
        handle.IsLoaded = false;
        handle.RefCount = 0;

        instance.LoadedBundles.Remove(handle.Name);
        instance.NeverUnload.Remove(handle.Name);

        if (handle.Entry != null)
            OnLoadedBundleRemove?.Invoke(handle.Entry);
    }


    //Editor 鑴氭湰缂栬瘧鍓嶈皟鐢║nload(false)淇濈暀宸插疄渚嬪寲璧勬簮
    //浣嗗叧闂?AssetBundle 瀹瑰櫒鍜屽簳灞傛枃浠?Stream
    public static void ReleaseAllForEditorCompilation()
    {
        if (instance == null)
            return;

        if (LoadCoroutine != null)
        {
            try
            {
                instance.StopCoroutine(LoadCoroutine);
            }
            catch
            {
                // Coroutine 鍙兘宸茬粡缁撴潫銆?
            }
            LoadCoroutine = null;
        }

        instance.ReleaseAllInternal(false, true, true);

        // 娓呯悊鐢辨棫瀹炵幇閬楃暀銆佺瓑寰呯粓缁撳櫒鍏抽棴鐨?FileStream銆?
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private void ReleaseAllInternal(
        bool unloadAllObjects,
        bool includeNeverUnload,
        bool notifyClear)
    {
        List<BundleHandle> snapshot = Handles.Values.ToList();

        for (int i = 0; i < snapshot.Count; i++)
        {
            BundleHandle handle = snapshot[i];
            if (!includeNeverUnload && handle.NeverUnload)
                continue;

            UnloadHandle(handle, unloadAllObjects);
        }

        if (includeNeverUnload)
        {
            Handles.Clear();
            LoadedBundles.Clear();
            NeverUnload.Clear();
        }
        else
        {
            List<string> removeKeys = Handles
                .Where(pair => !pair.Value.NeverUnload)
                .Select(pair => pair.Key)
                .ToList();

            for (int i = 0; i < removeKeys.Count; i++)
                Handles.Remove(removeKeys[i]);
        }

        if (notifyClear)
            OnLoadedBundleClear?.Invoke();
    }
    
    /// 杩斿洖椤哄簭淇濇寔鏍硅祫婧愬湪鍓嶃€佷緷璧栧湪鍚庯紱閲婃斁鏃跺弽鍚戦亶鍘嗐€?
    public static List<UmaDatabaseEntry> SearchAB(UmaViewerMain main, UmaDatabaseEntry entry)
    {
        if (entry == null)
            return new List<UmaDatabaseEntry>();

        UmaDatabaseEntry dependencyEntry = ResolveLaserCandidateIfMissing(main, entry);

        if (dependencyEntry == null || string.IsNullOrEmpty(dependencyEntry.Prerequisites))
            return new List<UmaDatabaseEntry> { entry };

        if (entry.CachedPrerequisites != null)
        {
            return new List<UmaDatabaseEntry> { entry }
                .Concat(entry.CachedPrerequisites)
                .ToList();
        }

        List<UmaDatabaseEntry> prerequisites = new List<UmaDatabaseEntry>();
        string[] dependencyNames = dependencyEntry.Prerequisites.Split(';');

        for (int i = 0; i < dependencyNames.Length; i++)
        {
            string dependencyName = dependencyNames[i]?.Trim();
            if (string.IsNullOrEmpty(dependencyName))
                continue;

            if (main == null || main.AbList == null ||
                !main.AbList.TryGetValue(dependencyName, out UmaDatabaseEntry dependency) ||
                dependency == null)
            {
                Debug.LogWarning(
                    $"[SearchAB] Missing prerequisite '{dependencyName}' for '{dependencyEntry.Name}'");
                continue;
            }

            prerequisites.AddRange(SearchAB(main, dependency));
        }

        entry.CachedPrerequisites = prerequisites;

        return new List<UmaDatabaseEntry> { entry }
            .Concat(prerequisites)
            .ToList();
    }

    private static UmaDatabaseEntry ResolveLaserCandidateIfMissing(
        UmaViewerMain main,
        UmaDatabaseEntry entry)
    {
        if (entry == null)
            return null;

        if (entry.Name != "3d/effect/live/pfb_eff_live_laser_01")
            return entry;

        if (File.Exists(entry.Path))
            return entry;

        if (main == null || main.AbList == null)
            return entry;

        string[] candidates =
        {
            "3d/effect/live/pfb_eff_live_laser_02",
            "3d/effect/live/pfb_eff_live_laser_03",
            "3d/effect/live/pfb_eff_live_laser_04"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidateName = candidates[i];

            if (main.AbList.TryGetValue(candidateName, out UmaDatabaseEntry candidate) &&
                candidate != null && File.Exists(candidate.Path))
            {
                Debug.LogWarning(
                    $"[LaserFallback] '{entry.Name}' missing -> use '{candidateName}'");
                return candidate;
            }
        }

        return entry;
    }

    private static List<UmaDatabaseEntry> DeduplicateEntries(IEnumerable<UmaDatabaseEntry> entries)
    {
        if (entries == null)
            return new List<UmaDatabaseEntry>();

        return entries
            .Where(entry => entry != null && !string.IsNullOrEmpty(entry.Name))
            .GroupBy(entry => NormalizeName(entry.Name), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static List<UmaDatabaseEntry> ExpandUniqueEntries(IEnumerable<UmaDatabaseEntry> roots)
    {
        List<UmaDatabaseEntry> expanded = new List<UmaDatabaseEntry>();

        if (roots != null)
        {
            foreach (UmaDatabaseEntry root in roots)
                expanded.AddRange(SearchAB(UmaViewerMain.Instance, root));
        }

        return DeduplicateEntries(expanded);
    }

    private static string NormalizeName(string name)
    {
        return name?.Trim().ToLowerInvariant();
    }

    public static AssetBundle Get(string name)
    {
        if (instance == null || string.IsNullOrEmpty(name))
            return null;

        string key = NormalizeName(name);

        if (!instance.LoadedBundles.TryGetValue(key, out AssetBundle bundle))
            return null;

        if (bundle != null)
            return bundle;

        // 瀛楀吀閲屾槸宸茬粡琚閮?AssetBundle.Unload 閿€姣佺殑 Unity Object銆?
        if (instance.Handles.TryGetValue(key, out BundleHandle staleHandle))
            ResetExternallyUnloadedHandle(staleHandle);
        else
            instance.LoadedBundles.Remove(key);

        return null;
    }

    public static AssetBundle Get(UmaDatabaseEntry entry)
    {
        return entry != null ? Get(entry.Name) : null;
    }

    // 淇濈暀缁欑幇鏈夎剼鏈娇鐢?
    public static void AddOrUpdate(string name, AssetBundle bundle, bool neverUnload = false)
    {
        if (instance == null || string.IsNullOrEmpty(name))
            return;

        string key = NormalizeName(name);
        BundleHandle handle = GetOrCreateHandle(key, null);

        handle.Bundle = bundle;
        handle.IsLoaded = bundle != null;
        handle.NeverUnload |= neverUnload;

        SyncLegacyDictionaries(handle);
    }

    /// <summary>Opt-in catalog inventory at a few Live lifecycle boundaries only.</summary>
    public static void CaptureLiveBundleInventory(string phase)
    {
        if (instance == null || !Gallop.Live.LiveRuntimeDiagnostics.Enabled)
            return;

        int handles = 0, bundles = 0, nonBundles = 0, candidates = 0, errors = 0;
        // Copy the handles first so a diagnostic failure cannot mutate ownership or refcounts.
        foreach (BundleHandle handle in instance.Handles.Values.ToArray())
        {
            if (!handle.IsLoaded) continue;
            handles++;
            if (handle.Bundle == null)
            {
                if (handle.Entry != null && handle.Entry.IsAssetBundle)
                {
                    errors++;
                    Gallop.Live.LiveRuntimeDiagnostics.RecordBundleIndex(
                        phase, handle.Name, -1, handle.RefCount, "bundle_unavailable");
                }
                else
                {
                    nonBundles++;
                }
                continue;
            }
            bundles++;
            try
            {
                int count = handle.Bundle.GetAllAssetNames().Length;
                candidates += count;
                Gallop.Live.LiveRuntimeDiagnostics.RecordBundleIndex(
                    phase, handle.Name, count, handle.RefCount, "indexed");
            }
            catch (Exception)
            {
                errors++;
                Gallop.Live.LiveRuntimeDiagnostics.RecordBundleIndex(
                    phase, handle.Name, -1, handle.RefCount, "index_error");
            }
        }
        Gallop.Live.LiveRuntimeDiagnostics.RecordBundleInventory(
            phase, handles, bundles, nonBundles, candidates, errors);
    }

    public static bool Exist(string name)
    {
        if (instance == null || string.IsNullOrEmpty(name))
            return false;

        string key = NormalizeName(name);
        return instance.Handles.TryGetValue(key, out BundleHandle handle) &&
               handle.IsLoaded && handle.Bundle != null;
    }

    public static bool Exist(UmaDatabaseEntry entry)
    {
        return entry != null && Exist(entry.Name);
    }

    public static bool Exist(AssetBundle bundle)
    {
        return instance != null && bundle != null &&
               instance.LoadedBundles.ContainsValue(bundle);
    }

    public static void UnloadAllBundle(bool unloadAllObjects = false)
    {
        if (instance == null)
            return;

        instance.ReleaseAllInternal(unloadAllObjects, false, false);

        if (unloadAllObjects)
        {
            UmaViewerBuilder builder = UmaViewerBuilder.Instance;
            if (builder != null)
            {
                if (builder.CurrentUMAContainer != null)
                    builder.UnloadUma();

                if (builder.CurrentOtherContainer != null)
                    Destroy(builder.CurrentOtherContainer.gameObject);
            }
        }

        OnLoadedBundleClear?.Invoke();
    }

    public static Texture2D FindTextureInLoadedBundlesByShortName(string shortName)
    {
        if (instance == null || string.IsNullOrEmpty(shortName))
            return null;

        foreach (KeyValuePair<string, AssetBundle> pair in instance.LoadedBundles)
        {
            AssetBundle bundle = pair.Value;
            if (bundle == null)
                continue;

            string[] assetNames;
            try
            {
                assetNames = bundle.GetAllAssetNames();
            }
            catch
            {
                continue;
            }

            string hit = assetNames.FirstOrDefault(assetName =>
                assetName.EndsWith("/" + shortName, StringComparison.OrdinalIgnoreCase) ||
                assetName.EndsWith(shortName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(hit))
                return bundle.LoadAsset<Texture2D>(hit);
        }

        return null;
    }

    public static IEnumerable<AssetBundle> EnumerateLoadedBundles()
    {
        if (instance == null)
            yield break;

        foreach (AssetBundle bundle in instance.LoadedBundles.Values)
        {
            if (bundle != null)
                yield return bundle;
        }
    }

    public static int GetOpenEncryptedStreamCount()
    {
        if (instance == null)
            return 0;

        return instance.Handles.Values.Count(handle => handle.Stream != null);
    }

    public static int GetLoadedHandleCount()
    {
        if (instance == null)
            return 0;

        return instance.Handles.Values.Count(handle => handle.IsLoaded);
    }
}
