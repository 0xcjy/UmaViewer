#if UNITY_EDITOR || UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Gallop.RenderPipeline;

namespace Gallop.Live
{
    /// <summary>
    /// Opt-in, read-only Live lifecycle diagnostics. It records comparable counts and
    /// render descriptors without changing timeline, physics, DOF, or post-effect values.
    /// Enable through UmaAssetManager.EnableLiveDiagnostics, UMA_LIVE_DIAGNOSTICS=1,
    /// or the local tmp/enable-live-diagnostics marker.
    /// </summary>
    public static class LiveRuntimeDiagnostics
    {
        [Serializable]
        private sealed class Record
        {
            public string type;
            public string utc;
            public string session;
            public string phase;
            public string bundle;
            public string operation;
            public string requestedType;
            public string assetName;
            public string assetPath;
            public string[] assetNames;
            public int assetsLoaded = -1;
            public int assetsMatching = -1;
            public string outcome;
            public string camera;
            public string mode;
            public string renderPassEvent;
            public string renderStep;
            public string inputRT;
            public string outputRT;
            public int inputNameId = -1;
            public int outputNameId = -1;
            public string graphicsFormat;
            public string depthStencilFormat;
            public int songId;
            public string stageId;
            public int liveFrame = -1;
            public int unityFrame = -1;
            public int roots = -1;
            public int directorRoots = -1;
            public int normalRoots = -1;
            public int mobRoots = -1;
            public int normalCharacters = -1;
            public int mobCharacters = -1;
            public int skippedCharacters = -1;
            public int downloadEntriesUnique = -1;
            public int loadItemsWithDuplicates = -1;
            public int rootIndex = -1;
            public int requestCount = -1;
            public int repeatedRequests = -1;
        public int progressCurrent = -1;
        public int progressTarget = -1;
        public string progressMessage;
            public int handleReuse = -1;
            public int newOpened = -1;
            public int fileMissing = -1;
            public int loadFailed = -1;
            public int uniqueLoadedHandles = -1;
            public int loadedAssetBundles = -1;
            public int loadedNonBundles = -1;
            public int assetIndexCandidates = -1;
            public int assetIndexErrors = -1;
            public int refCount = -1;
            public int loadedScenes = -1;
            public int sceneRoots = -1;
            public int characters = -1;
            public int stageObjects = -1;
            public int stageMapObjects = -1;
            public int totalTransforms = -1;
            public int renderers = -1;
            public int lights = -1;
            public int particleSystems = -1;
            public int cameras = -1;
            public int multiCameraCount = -1;
            public int multiCameraActive = -1;
            public string multiCameraDetails;
            public int timelineSheets = -1;
            public int particlePrefabNames = -1;
            public int spotlightPrefabNames = -1;
            public bool hasSunShaftsSettings;
            public float sunShaftsIntensity;
            public float sunShaftsPower;
            public float sunShaftsBlurRadius;
            public int sunShaftsBlurIterations = -1;
            public int indirectShaftTextureNames = -1;
            public int lensFlareGroups = -1;
            public bool monitorTextureWidthRateEnabled;
            public int monitorPosTracks = -1;
            public int monitorLookAtTracks = -1;
            public int postFilm1Keys = -1;
            public int postFilm2Keys = -1;
            public int postFilm3Keys = -1;
            public int bloomKeys = -1;
            public int dofKeys = -1;
            public int radialKeys = -1;
            public int colorCorrectionGroups = -1;
            public int videos = -1;
            public int rendererPassCount = -1;
            public int width = -1;
            public int height = -1;
            public int msaaSamples = -1;
            public int depthBufferBits = -1;
            public float seconds = -1f;
            public float fov = -1f;
            public float nearClip = -1f;
            public float farClip = -1f;
            public float cameraX = float.NaN;
            public float cameraY = float.NaN;
            public float cameraZ = float.NaN;
            public bool requireStage;
            public bool allowHDR;
            public bool bindMS;
            public bool useMipMap;
            public bool sRGB;
            public bool dof;
            public bool bloom;
            public bool diffusion;
            public bool radial;
            public bool colorCorrection;
            public bool shaderReady;
            public bool urpPostProcessing;
            public string bloomBlendMode;
            public float bloomThreshold;
            public float bloomIntensity;
            public float bloomBlurSize;
            public float bloomDofWeight;
            public float diffusionBright;
            public float diffusionThreshold;
            public float diffusionBlurSize;
            public float diffusionSaturation;
            public float diffusionContrast;
            public string postFilmModes;
            public string postFilmValidity;
            public string postFilmPowers;
            public string postFilmBlends;
            public string postFilmBlinkLookupStatus;
            public string postFilmColor0Rgb;
            // Stage surface audit (only populated for stage_surface / stage_surface_pair).
            public string rendererPath;
            public string otherRendererPath;
            public string meshName;
            public int meshVertices = -1;
            public string materialName;
            public string shaderName;
            public int renderQueue = -1;
            public int zTest = -1;
            public int zWrite = -1;
            public int layer = -1;
            public bool activeInHierarchy;
            public bool rendererEnabled;
            public bool forceRenderingOff;
            public string worldBounds;
            public int sortingOrder;
            public int srcBlend = -1;
            public int dstBlend = -1;
            public float reflectionRate = float.NaN;
            public string reflectionTexture;
            public string materialColor;
            public string horizontalTriangleHeights;
        }

        private static readonly object Sync = new object();
        private static StreamWriter writer;
        private static string sessionId;
        private static string outputPath;
        private static bool enabled;
        private static int lastFrame = -1;
        private static bool stageSurfaceRecorded;
        private static int lastCameraPassFrame = -1;
        private static readonly HashSet<string> cameraPassesThisFrame = new HashSet<string>(StringComparer.Ordinal);

        public static bool Enabled => enabled;
        public static string OutputPath => outputPath;

        public static bool IsRequested()
        {
            if (UmaAssetManager.EnableLiveDiagnostics ||
                string.Equals(Environment.GetEnvironmentVariable("UMA_LIVE_DIAGNOSTICS"), "1", StringComparison.Ordinal))
                return true;

            // The already-open Editor cannot inherit a newly set process environment variable.
            string marker = Path.Combine(Application.dataPath, "..", "tmp", "enable-live-diagnostics");
            return File.Exists(marker);
        }

        public static void BeginPreload(LiveEntry live, IList<LiveCharacterLoadData> characters, bool requireStage)
        {
            if (!IsRequested())
                return;

            End();
            enabled = true;
            sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            lastFrame = -1;
            stageSurfaceRecorded = false;
            lastCameraPassFrame = -1;
            cameraPassesThisFrame.Clear();

            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "tmp"));
                Directory.CreateDirectory(root);
                outputPath = Path.Combine(root, "live-diagnostics-" + sessionId + ".jsonl");
                writer = new StreamWriter(new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new System.Text.UTF8Encoding(false))
                {
                    AutoFlush = true
                };
            }
            catch (Exception exception)
            {
                enabled = false;
                Debug.LogWarning("[LiveDiag] Could not create diagnostics file: " + exception.Message);
                return;
            }

            var record = New("session_begin", "preload");
            record.songId = live != null ? live.MusicId : 0;
            record.stageId = live != null ? live.BackGroundId : string.Empty;
            record.requireStage = requireStage;
            record.characters = characters != null ? characters.Count : 0;
            Write(record);
            Debug.Log("[LiveDiag] JSONL output=" + outputPath);
        }

        public static void RecordResourceRoots(int director, int normal, int mob,
            int normalCount, int mobCount, int skipped, int total)
        {
            if (!enabled) return;
            var record = New("resource_roots", "preload");
            record.directorRoots = director;
            record.normalRoots = normal;
            record.mobRoots = mob;
            record.normalCharacters = normalCount;
            record.mobCharacters = mobCount;
            record.skippedCharacters = skipped;
            record.roots = total;
            Write(record);
        }

        // SearchAB is expanded once per root. Each record's requestCount sums to
        // the progress denominator, including shared dependencies and cache hits.
        public static void RecordPreloadRoot(int index, string name, int requests, int repeated)
        {
            if (!enabled) return;
            var record = New("preload_root", "preload");
            record.rootIndex = index;
            record.bundle = name;
            record.requestCount = requests;
            record.repeatedRequests = repeated;
            Write(record);
        }

        public static void RecordPreloadCounts(int rawInput, int roots, int downloadUnique, int loadItems)
        {
            if (!enabled) return;
            var record = New("preload_counts", "preload");
            record.roots = roots;
            record.downloadEntriesUnique = downloadUnique;
            record.loadItemsWithDuplicates = loadItems;
            record.characters = rawInput;
            Write(record);
        }

        public static void RecordProgress(int current, int target, string message)
        {
            if (!enabled) return;
            var record = New("load_progress", "preload");
            record.progressCurrent = current;
            record.progressTarget = target;
            record.progressMessage = message ?? string.Empty;
            Write(record);
        }

        public static void RecordBundleRequest(string bundle, string phase, bool recursive)
        {
            if (!enabled) return;
            var record = New("bundle_request", phase);
            record.bundle = SafeName(bundle);
            record.outcome = recursive ? "recursive" : "direct";
            Write(record);
        }

        public static void RecordBundleAcquire(string bundle, string outcome, string phase)
        {
            if (!enabled) return;
            var record = New("bundle_acquire", phase);
            record.bundle = SafeName(bundle);
            record.outcome = outcome;
            Write(record);
        }

        // Counts materialized assets, not bundle entries or GetAllAssetNames candidates.
        public static void RecordAssetLoad(string bundle, string operation, Type requestedType,
            UnityEngine.Object[] assets, UnityEngine.Object selected = null, string assetPath = null)
        {
            if (!enabled) return;
            var record = New("asset_load", "runtime");
            record.bundle = SafeName(bundle);
            record.operation = operation;
            record.assetPath = assetPath;
            record.requestedType = requestedType != null ? requestedType.Name : string.Empty;
            record.assetsLoaded = assets != null ? assets.Count(asset => asset != null) : 0;
            if (assets != null)
            {
                var matching = assets.Where(asset => asset != null &&
                    (requestedType == null || asset.GetType() == requestedType)).ToArray();
                record.assetsMatching = matching.Length;
                record.assetNames = operation == "GetAll" || operation == "LoadAllAssets"
                    ? matching.Select(asset => asset.name).ToArray() : null;
            }
            record.assetName = selected != null ? selected.name : string.Empty;
            record.outcome = selected != null ||
                ((operation == "GetAll" || operation == "LoadAllAssets") && record.assetsMatching > 0)
                ? "found" : "empty";
            Write(record);
        }

        public static void RecordInstantiation(string source, GameObject created)
        {
            if (!enabled) return;
            var record = New("instance_create", "runtime");
            record.bundle = SafeName(source);
            record.assetName = created != null ? created.name : string.Empty;
            record.outcome = created != null ? "created" : "null";
            if (created != null)
            {
                record.totalTransforms = created.GetComponentsInChildren<Transform>(true).Length;
                record.renderers = created.GetComponentsInChildren<Renderer>(true).Length;
                record.lights = created.GetComponentsInChildren<Light>(true).Length;
                record.particleSystems = created.GetComponentsInChildren<ParticleSystem>(true).Length;
            }
            Write(record);
        }
        public static void RecordPreloadComplete(int handleReuse, int newOpened, int fileMissing, int loadFailed, int uniqueLoadedHandles)
        {
            if (!enabled) return;
            var record = New("preload_complete", "preload");
            record.handleReuse = handleReuse;
            record.newOpened = newOpened;
            record.fileMissing = fileMissing;
            record.loadFailed = loadFailed;
            record.uniqueLoadedHandles = uniqueLoadedHandles;
            Write(record);
        }

        public static void RecordPhase(string phase, Director director = null)
        {
            if (!enabled) return;
            var record = New("phase", phase);
            FillTimeline(record, director);
            Write(record);

            // Phase-only snapshots: never scan bundle indexes or scene trees per frame.
            if (phase == "preload_callback" || phase == "director_initialized" ||
                phase == "characters_loaded" || phase == "timeline_music_initialized")
            {
                UmaAssetManager.CaptureLiveBundleInventory(phase);
                RecordSceneInventory(phase);
            }
        }

        // GetAllAssetNames counts catalog candidates, NOT LoadAsset calls or resident assets.
        public static void RecordBundleIndex(string phase, string bundle, int candidates, int refCount, string outcome)
        {
            if (!enabled) return;
            var record = New("bundle_index", phase);
            record.bundle = SafeName(bundle);
            record.assetIndexCandidates = candidates;
            record.refCount = refCount;
            record.outcome = outcome;
            Write(record);
        }

        public static void RecordBundleInventory(string phase, int loadedHandles, int bundles,
            int nonBundles, int candidates, int errors)
        {
            if (!enabled) return;
            var record = New("bundle_inventory", phase);
            record.uniqueLoadedHandles = loadedHandles;
            record.loadedAssetBundles = bundles;
            record.loadedNonBundles = nonBundles;
            record.assetIndexCandidates = candidates;
            record.assetIndexErrors = errors;
            Write(record);
        }

        private static void RecordSceneInventory(string phase)
        {
            if (!enabled) return;
            var record = New("scene_inventory", phase);
            record.loadedScenes = 0;
            record.sceneRoots = 0;
            record.totalTransforms = 0;
            record.renderers = 0;
            record.lights = 0;
            record.particleSystems = 0;
            record.cameras = 0;
            record.videos = 0;
            try
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    record.loadedScenes++;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        record.sceneRoots++;
                        record.totalTransforms += root.GetComponentsInChildren<Transform>(true).Length;
                        record.renderers += root.GetComponentsInChildren<Renderer>(true).Length;
                        record.lights += root.GetComponentsInChildren<Light>(true).Length;
                        record.particleSystems += root.GetComponentsInChildren<ParticleSystem>(true).Length;
                        record.cameras += root.GetComponentsInChildren<Camera>(true).Length;
                        record.videos += root.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true).Length;
                    }
                }
                record.outcome = "loaded_scenes_only";
            }
            catch (Exception)
            {
                record.outcome = "partial_error";
            }
            Write(record);
        }

        public static void BeginDirector(Director director)
        {
            if (!enabled || director == null) return;
            RecordPhase("director_initialize", director);
        }

        public static void RecordFrame(Director director, bool force = false)
        {
            if (!enabled || director == null) return;

            int frame = Mathf.RoundToInt(director._liveCurrentTime * 60f);
            if (!force && frame == lastFrame)
                return;
            if (!force && lastFrame >= 0 && frame / 30 == lastFrame / 30 && !IsTargetFrame(frame))
                return;
            lastFrame = frame;

            Camera camera = director.MainRenderCamera;
            var record = New("live_frame", "playback");
            FillTimeline(record, director);
            record.liveFrame = frame;
            record.characters = director.CharaContainerScript != null ? director.CharaContainerScript.Count : 0;
            record.stageObjects = director._stageController != null && director._stageController._stageObjects != null
                ? director._stageController._stageObjects.Count : 0;
            record.stageMapObjects = director._stageController != null && director._stageController.StageObjectMap != null
                ? director._stageController.StageObjectMap.Count : 0;

            if (director.gameObject != null)
            {
                record.totalTransforms = director.GetComponentsInChildren<Transform>(true).Length;
                record.renderers = director.GetComponentsInChildren<Renderer>(true).Length;
                record.lights = director.GetComponentsInChildren<Light>(true).Length;
                record.particleSystems = director.GetComponentsInChildren<ParticleSystem>(true).Length;
                record.cameras = director.GetComponentsInChildren<Camera>(true).Length;
                record.videos = director.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true).Length;
            }

            FillCamera(record, camera, null);
            FillMultiCamera(record, director);
            FillTimelineFeatureInventory(record, director);
            FillPost(record);
            Write(record);

            // One read-only snapshot near the reported Live1093 20 s fault. Never
            // instantiate materials or modify enabled/queue/depth state here.
            if (!stageSurfaceRecorded && frame >= 1190 && frame <= 1220 && director._stageController != null)
            {
                stageSurfaceRecorded = true;
                RecordStageSurfaces(director._stageController, frame);
            }
        }

        private struct StageSurface
        {
            public Renderer renderer;
            public Bounds bounds;
            public string path;
            public Mesh mesh;
        }

        private static string StagePath(Transform t, Transform root)
        {
            var names = new List<string>(8);
            while (t != null && t != root)
            {
                names.Add(t.name);
                t = t.parent;
            }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string BoundsText(Bounds b)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0:R},{1:R},{2:R}|{3:R},{4:R},{5:R}",
                b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z);
        }

        // Read-only world-space triangle heights distinguish a flat mirror overlay
        // from a ground mesh whose AABB spans several elevations. Bounded to the
        // named floor renderers in the one-shot stage snapshot.
        private static string HorizontalTriangleHeights(Mesh mesh, Matrix4x4 localToWorld)
        {
            if (mesh == null || !mesh.isReadable) return "unreadable";
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var heights = new Dictionary<int, int>();
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                if (ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length) continue;
                var a = localToWorld.MultiplyPoint3x4(vertices[ia]);
                var b = localToWorld.MultiplyPoint3x4(vertices[ib]);
                var c = localToWorld.MultiplyPoint3x4(vertices[ic]);
                if (Mathf.Max(a.y, b.y, c.y) - Mathf.Min(a.y, b.y, c.y) > 0.005f) continue;
                float area2 = Mathf.Abs((b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x));
                if (area2 < 0.001f) continue;
                int millimeters = Mathf.RoundToInt((a.y + b.y + c.y) * (1000f / 3f));
                heights[millimeters] = heights.TryGetValue(millimeters, out var count) ? count + 1 : 1;
            }
            var keys = new List<int>(heights.Keys);
            keys.Sort();
            var parts = new List<string>(keys.Count);
            foreach (int y in keys) parts.Add(y.ToString(CultureInfo.InvariantCulture) + "mm:" + heights[y].ToString(CultureInfo.InvariantCulture));
            return string.Join("|", parts.ToArray());
        }

        private static void RecordStageSurfaces(StageController stage, int frame)
        {
            // Capture even inactive renderers: alpha masks and timeline panels may
            // be disabled at creation but become active later. Pair candidates only
            // compare surfaces that actually render at this instant.
            var renderers = stage.GetComponentsInChildren<Renderer>(true);
            var surfaces = new List<StageSurface>(renderers.Length);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var filter = r.GetComponent<MeshFilter>();
                var skinned = r as SkinnedMeshRenderer;
                Mesh mesh = filter != null ? filter.sharedMesh : skinned != null ? skinned.sharedMesh : null;
                var path = StagePath(r.transform, stage.transform);
                var b = r.bounds;
                surfaces.Add(new StageSurface { renderer = r, bounds = b, path = path, mesh = mesh });

                var materials = r.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var mat = materials[i];
                    var item = New("stage_surface", "playback");
                    item.liveFrame = frame;
                    item.rendererPath = path;
                    item.assetName = r.name;
                    item.meshName = mesh != null ? mesh.name : string.Empty;
                    item.meshVertices = mesh != null ? mesh.vertexCount : -1;
                    item.materialName = mat != null ? mat.name : string.Empty;
                    item.shaderName = mat != null && mat.shader != null ? mat.shader.name : string.Empty;
                    item.renderQueue = mat != null ? mat.renderQueue : -1;
                    item.zTest = mat != null && mat.HasProperty("_ZTest") ? mat.GetInt("_ZTest") : -1;
                    item.zWrite = mat != null && mat.HasProperty("_ZWrite") ? mat.GetInt("_ZWrite") : -1;
                    item.layer = r.gameObject.layer;
                    item.activeInHierarchy = r.gameObject.activeInHierarchy;
                    item.rendererEnabled = r.enabled;
                    item.forceRenderingOff = r.forceRenderingOff;
                    item.worldBounds = BoundsText(b);
                    item.sortingOrder = r.sortingOrder;
                    if (mat != null)
                    {
                        item.srcBlend = mat.HasProperty("_SrcBlend") ? mat.GetInt("_SrcBlend") : -1;
                        item.dstBlend = mat.HasProperty("_DstBlend") ? mat.GetInt("_DstBlend") : -1;
                        if (mat.HasProperty("_ReflectionRate")) item.reflectionRate = mat.GetFloat("_ReflectionRate");
                        if (mat.HasProperty("_ReflectionTex"))
                        {
                            var tex = mat.GetTexture("_ReflectionTex");
                            item.reflectionTexture = tex != null ? tex.name : "<null>";
                        }
                        if (mat.HasProperty("_Color")) item.materialColor = mat.GetColor("_Color").ToString("F3");
                    }
                    if (mesh != null && (path.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.IndexOf("mirror000", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        path.EndsWith("/shadow", StringComparison.OrdinalIgnoreCase)))
                        item.horizontalTriangleHeights = HorizontalTriangleHeights(mesh, r.localToWorldMatrix);
                    item.rootIndex = i;
                    Write(item);
                }
            }

            // This is a geometric *candidate*, not proof of identical triangles:
            // horizontal, thin, simultaneously rendered surfaces at nearly the
            // same height with a nontrivial XZ footprint intersection.
            const float heightEpsilon = 0.01f;
            const float minOverlap = 0.01f;
            int emitted = 0;
            for (int i = 0; i < surfaces.Count && emitted < 120; i++)
            {
                var a = surfaces[i];
                if (!IsVisiblePlanarSurface(a)) continue;
                for (int j = i + 1; j < surfaces.Count && emitted < 120; j++)
                {
                    var b = surfaces[j];
                    if (!IsVisiblePlanarSurface(b)) continue;
                    if (Mathf.Abs(a.bounds.center.y - b.bounds.center.y) > heightEpsilon) continue;
                    float x = Mathf.Min(a.bounds.max.x, b.bounds.max.x) - Mathf.Max(a.bounds.min.x, b.bounds.min.x);
                    float z = Mathf.Min(a.bounds.max.z, b.bounds.max.z) - Mathf.Max(a.bounds.min.z, b.bounds.min.z);
                    if (x * z < minOverlap || x <= 0f || z <= 0f) continue;
                    var pair = New("stage_surface_pair", "playback");
                    pair.liveFrame = frame;
                    pair.rendererPath = a.path;
                    pair.otherRendererPath = b.path;
                    pair.outcome = a.mesh != null && a.mesh == b.mesh &&
                        a.renderer.localToWorldMatrix == b.renderer.localToWorldMatrix
                        ? "same_mesh_and_transform" : "coplanar_bounds_candidate";
                    Write(pair);
                    emitted++;
                }
            }
        }

        private static bool IsVisiblePlanarSurface(StageSurface s)
        {
            var r = s.renderer;
            return r != null && r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff &&
                s.bounds.size.y <= 0.15f && s.bounds.size.x > 0.05f && s.bounds.size.z > 0.05f;
        }

        private static void FillTimelineFeatureInventory(Record record, Director director)
        {
            var data = director != null && director._liveTimelineControl != null ? director._liveTimelineControl.data : null;
            if (data == null) return;
            record.timelineSheets = data.worksheetList != null ? data.worksheetList.Count : 0;
            record.particlePrefabNames = data.particlePrefabNames != null ? data.particlePrefabNames.Length : 0;
            record.spotlightPrefabNames = data.spotLightPrefabNames != null ? data.spotLightPrefabNames.Length : 0;
            var sun = data.sunShaftsSettings;
            record.hasSunShaftsSettings = sun != null;
            if (sun != null)
            {
                record.sunShaftsIntensity = sun.intensity;
                record.sunShaftsPower = sun.sunPower;
                record.sunShaftsBlurRadius = sun.blurRadius;
                record.sunShaftsBlurIterations = sun.blurIterations;
            }
            record.indirectShaftTextureNames = data.indirectLightShaftsSettings != null &&
                data.indirectLightShaftsSettings.shaftTextureNames != null
                ? data.indirectLightShaftsSettings.shaftTextureNames.Length : 0;
            record.lensFlareGroups = data.lensFlareSetting != null && data.lensFlareSetting.flareDataGroup != null ? data.lensFlareSetting.flareDataGroup.Length : 0;
            record.monitorTextureWidthRateEnabled = data.MonitorCameraSettings != null && data.MonitorCameraSettings.IsEnabledTextureWidthRate;
            if (data.worksheetList == null || data.worksheetList.Count == 0) return;
            var sheet = data.worksheetList[0];
            record.monitorPosTracks = sheet.monitorCameraPosKeys != null ? sheet.monitorCameraPosKeys.Count : 0;
            record.monitorLookAtTracks = sheet.monitorCameraLookAtKeys != null ? sheet.monitorCameraLookAtKeys.Count : 0;
            record.postFilm1Keys = sheet.postFilmKeys != null ? sheet.postFilmKeys.Count : 0;
            record.postFilm2Keys = sheet.postFilm2Keys != null ? sheet.postFilm2Keys.Count : 0;
            record.postFilm3Keys = sheet.postFilm3Keys != null ? sheet.postFilm3Keys.Count : 0;
            record.bloomKeys = sheet.postEffectBloomDiffusionKeys != null ? sheet.postEffectBloomDiffusionKeys.Count : 0;
            record.dofKeys = sheet.postEffectDOFKeys != null ? sheet.postEffectDOFKeys.Count : 0;
            record.radialKeys = sheet.radialBlurKeys != null ? sheet.radialBlurKeys.Count : 0;
            record.colorCorrectionGroups = sheet.colorCorrectionDataLists != null ? sheet.colorCorrectionDataLists.Count : 0;
        }
        private static void FillMultiCamera(Record record, Director director)
        {
            var control = director != null ? director._liveTimelineControl : null;
            if (control == null) return;
            record.multiCameraCount = control.MultiCameraCount;
            record.multiCameraActive = control.IsMultiCameraEnabled ? 1 : 0;
            if (control.MultiCameraCount <= 0) return;
            var details = new List<string>(control.MultiCameraCount);
            for (int i = 0; i < control.MultiCameraCount; i++)
            {
                Camera camera = control.GetMultiCamera(i);
                if (camera == null) { details.Add(i + ":null"); continue; }
                string target = camera.targetTexture != null ? camera.targetTexture.name : "screen";
                details.Add(string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}:{3}", i, camera.enabled ? "on" : "off", camera.depth, target));
            }
            record.multiCameraDetails = string.Join("|", details);
        }
        public static void RecordRenderPass(Camera camera, RenderTextureDescriptor descriptor,
            PostImageEffectFeature.Parameter parameter, int passCount, bool shaderReady, bool urpPostProcessing)
        {
            if (!enabled || camera == null)
                return;

            int frame = Time.frameCount;
            if (lastCameraPassFrame != frame)
            {
                lastCameraPassFrame = frame;
                cameraPassesThisFrame.Clear();
            }

            string cameraKey = camera.GetInstanceID().ToString(CultureInfo.InvariantCulture);
            if (!cameraPassesThisFrame.Add(cameraKey))
                return;

            var record = New("render_pass", "render");
            record.camera = camera.name;
            record.unityFrame = frame;
            record.rendererPassCount = passCount;
            record.width = descriptor.width;
            record.height = descriptor.height;
            record.msaaSamples = descriptor.msaaSamples;
            record.depthBufferBits = descriptor.depthBufferBits;
            record.graphicsFormat = descriptor.graphicsFormat.ToString();
            record.depthStencilFormat = descriptor.depthStencilFormat.ToString();
            record.bindMS = descriptor.bindMS;
            record.useMipMap = descriptor.useMipMap;
            record.sRGB = descriptor.sRGB;
            record.allowHDR = camera.allowHDR;
            record.shaderReady = shaderReady;
            record.urpPostProcessing = urpPostProcessing;
            record.dof = parameter != null && parameter.DofDiffuionBloomOverlay.IsValidity;
            record.bloom = parameter != null && parameter.DofDiffuionBloomOverlay.IsEnableBloom;
            record.diffusion = parameter != null && parameter.DofDiffuionBloomOverlay.IsEnableDiffusion;
            record.radial = parameter != null && parameter.RadialBlur.IsValid;
            record.colorCorrection = parameter != null && parameter.ColorCorrection.IsValid;
            record.mode = parameter != null ? parameter.DofDiffuionBloomOverlay.UseDofDiffusionBloomType.ToString() : "None";
            record.renderPassEvent = parameter != null ? parameter.DofDiffuionBloomOverlay.RenderPassEvent.ToString() : string.Empty;
            FillCamera(record, camera, descriptor);
            Write(record);
        }

        // Optional execution/RT handoff trace; unlike render_pass, this is reached
        // from Execute after command-buffer submission. It is not a GPU pixel capture.
        // Sample only selected Live frames to keep synchronous JSONL I/O bounded.
        public static void RecordRenderExecution(Camera camera, string step, RenderPassEvent passEvent,
            RenderTextureHandle input, RenderTextureHandle output)
        {
            if (!enabled || camera == null) return;

            Director director = Director.instance;
            if (director == null || director.live == null) return;
            int liveFrame = Mathf.RoundToInt(director._liveCurrentTime * 60f);
            // Playback may advance several Live frames per Editor frame. Keep the
            // measured frame in the record; never label a nearby sample as exact.
            if (!IsNearTargetFrame(liveFrame, 6)) return;
            if (!string.Equals(Environment.GetEnvironmentVariable("UMA_LIVE_PASS_TRACE"), "1", StringComparison.Ordinal) &&
                !File.Exists(Path.Combine(Application.dataPath, "..", "tmp", "enable-live-pass-trace"))) return;

            var record = New("render_execute", "render");
            FillTimeline(record, director);
            record.camera = camera.name;
            record.renderStep = step;
            record.renderPassEvent = passEvent.ToString();
            record.inputNameId = input.NameId;
            record.outputNameId = output.NameId;
            record.inputRT = input.RtId.ToString();
            record.outputRT = output.RtId.ToString();
            Write(record);
        }

        public static void End()
        {
            if (writer != null)
            {
                try
                {
                    var record = New("session_end", "shutdown");
                    Write(record);
                    writer.Dispose();
                }
                catch
                {
                    // Diagnostics must never affect Live shutdown.
                }
                writer = null;
            }
            enabled = false;
            sessionId = null;
            outputPath = null;
            lastFrame = -1;
            lastCameraPassFrame = -1;
            cameraPassesThisFrame.Clear();
        }

        private static Record New(string type, string phase)
        {
            return new Record
            {
                type = type,
                phase = phase,
                session = sessionId,
                utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityFrame = Time.frameCount
            };
        }

        private static void FillTimeline(Record record, Director director)
        {
            if (director == null) return;
            record.seconds = director._liveCurrentTime;
            record.liveFrame = Mathf.RoundToInt(director._liveCurrentTime * 60f);
            if (director.live != null)
            {
                record.songId = director.live.MusicId;
                record.stageId = director.live.BackGroundId;
            }
        }

        private static void FillCamera(Record record, Camera camera, RenderTextureDescriptor? descriptor)
        {
            if (camera == null) return;
            record.camera = camera.name;
            record.fov = camera.fieldOfView;
            record.nearClip = camera.nearClipPlane;
            record.farClip = camera.farClipPlane;
            record.cameraX = camera.transform.position.x;
            record.cameraY = camera.transform.position.y;
            record.cameraZ = camera.transform.position.z;
            record.allowHDR = camera.allowHDR;
            if (descriptor.HasValue)
            {
                RenderTextureDescriptor value = descriptor.Value;
                record.width = value.width;
                record.height = value.height;
                record.msaaSamples = value.msaaSamples;
                record.depthBufferBits = value.depthBufferBits;
                record.graphicsFormat = value.graphicsFormat.ToString();
                record.depthStencilFormat = value.depthStencilFormat.ToString();
            }
            else if (camera.targetTexture != null)
            {
                RenderTextureDescriptor value = camera.targetTexture.descriptor;
                record.width = value.width;
                record.height = value.height;
                record.msaaSamples = value.msaaSamples;
                record.depthBufferBits = value.depthBufferBits;
                record.graphicsFormat = value.graphicsFormat.ToString();
                record.depthStencilFormat = value.depthStencilFormat.ToString();
            }
        }

        private static void FillPost(Record record)
        {
            PostImageEffectFeature.Parameter parameter = PostImageEffectFeature.RuntimeParameter;
            record.dof = parameter != null && parameter.DofDiffuionBloomOverlay.IsValidity;
            record.bloom = parameter != null && parameter.DofDiffuionBloomOverlay.IsEnableBloom;
            record.diffusion = parameter != null && parameter.DofDiffuionBloomOverlay.IsEnableDiffusion;
            record.radial = parameter != null && parameter.RadialBlur.IsValid;
            record.colorCorrection = parameter != null && parameter.ColorCorrection.IsValid;
            record.mode = parameter != null ? parameter.DofDiffuionBloomOverlay.UseDofDiffusionBloomType.ToString() : "None";
            record.shaderReady = ShaderManager.IsShaderBundleReady;
            if (parameter != null)
            {
                var p = parameter.DofDiffuionBloomOverlay;
                record.bloomBlendMode = p.BloomBlendMode.ToString();
                record.bloomThreshold = p.BloomThreshold; record.bloomIntensity = p.BloomIntensity; record.bloomBlurSize = p.BloomBlurSize; record.bloomDofWeight = p.BloomDofWeight;
                record.diffusionBright = p.DiffusionBright; record.diffusionThreshold = p.DiffusionThreshold; record.diffusionBlurSize = p.DiffusionBlurSize; record.diffusionSaturation = p.DiffusionSaturation; record.diffusionContrast = p.DiffusionContrast;
                record.postFilmModes = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", p.Overlay1.PostFilmMode, p.Overlay2.PostFilmMode, p.Overlay3.PostFilmMode);
                record.postFilmValidity = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", p.Overlay1.IsValidity, p.Overlay2.IsValidity, p.Overlay3.IsValidity);
                record.postFilmPowers = string.Format(CultureInfo.InvariantCulture, "{0:F4}|{1:F4}|{2:F4}", p.Overlay1.PostFilmPower, p.Overlay2.PostFilmPower, p.Overlay3.PostFilmPower);
                record.postFilmBlends = string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}", p.Overlay1.ColorBlend, p.Overlay2.ColorBlend, p.Overlay3.ColorBlend);
                var director = Director.instance;
                record.postFilmBlinkLookupStatus = director != null && director._liveTimelineControl != null
                    ? director._liveTimelineControl.PostFilmBlinkLookupStatus : "unavailable";
                record.postFilmColor0Rgb = string.Format(CultureInfo.InvariantCulture, "{0:F3},{1:F3},{2:F3}|{3:F3},{4:F3},{5:F3}|{6:F3},{7:F3},{8:F3}",
                    p.Overlay1.PostFilmColor0.r, p.Overlay1.PostFilmColor0.g, p.Overlay1.PostFilmColor0.b,
                    p.Overlay2.PostFilmColor0.r, p.Overlay2.PostFilmColor0.g, p.Overlay2.PostFilmColor0.b,
                    p.Overlay3.PostFilmColor0.r, p.Overlay3.PostFilmColor0.g, p.Overlay3.PostFilmColor0.b);
            }
        }

        private static bool IsNearTargetFrame(int frame, int tolerance)
        {
            string selected = Environment.GetEnvironmentVariable("UMA_LIVE_DIAG_FRAMES");
            if (string.IsNullOrWhiteSpace(selected))
                return Mathf.Abs(frame - 308) <= tolerance ||
                       Mathf.Abs(frame - 868) <= tolerance ||
                       Mathf.Abs(frame - 3848) <= tolerance;
            return selected.Split(',').Any(value => int.TryParse(value.Trim(), out int target) &&
                                                   Mathf.Abs(frame - target) <= tolerance);
        }

        private static bool IsTargetFrame(int frame)
        {
            string selected = Environment.GetEnvironmentVariable("UMA_LIVE_DIAG_FRAMES");
            if (string.IsNullOrWhiteSpace(selected))
                return frame == 308 || frame == 868 || frame == 3848;
            return selected.Split(',').Any(value => int.TryParse(value.Trim(), out int parsed) && parsed == frame);
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.Replace('\\', '/');
        }

        private static void Write(Record record)
        {
            if (!enabled || writer == null || record == null) return;
            lock (Sync)
            {
                try { writer.WriteLine(JsonUtility.ToJson(record)); }
                catch { /* diagnostics are best effort */ }
            }
        }
    }
}
#endif





