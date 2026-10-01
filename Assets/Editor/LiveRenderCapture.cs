#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Gallop;
using Gallop.Live;
using Gallop.RenderPipeline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Opt-in graphics smoke test. Does not alter saved scenes or user configuration.</summary>
[InitializeOnLoad]
public static class LiveRenderCapture
{
    const string Armed = "UmaViewer.RenderCapture.Armed";
    static double deadline;
    static bool requested;
    static int captureIndex;
    const string PlanState = Armed + ".Plan", ExitState = Armed + ".Exit", FilmIsolationState = Armed + ".FilmIsolation";
    static LiveCapturePlan plan;
    static readonly LiveCaptureStabilityGate gate = new LiveCaptureStabilityGate();
    static bool exitOnFinish;
    static bool filmIsolation;
    static bool filmSuppressed;
    static Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode savedMode1, savedMode2, savedMode3;
    static bool suppressedThisRender;
    static int CaptureSlots => filmIsolation ? 3 : 2;
    static int CaptureFrame => plan.frames[captureIndex / CaptureSlots];
    static int CaptureVariant => captureIndex % CaptureSlots;
    static string VariantName => CaptureVariant == 0 ? "off" : filmIsolation && CaptureVariant == 1 ? "noFilm" : "on";
    static float previousProgress;
    static bool previousSync, previousTouched, previousOuted;
    static Director capturedDirector;
    static Camera camera;
    static RenderTexture target, previousTarget;
    static PostImageEffectFeature feature;
    static bool originalActive;
    static bool originalRendering;
    static string output;
    static bool finishing;
    static CapturePreview preview;

    static LiveRenderCapture()
    {
        EditorApplication.update += Tick;
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
        RenderPipelineManager.endCameraRendering += AfterCamera;
        AssemblyReloadEvents.beforeAssemblyReload += () => {
            if (SessionState.GetBool(Armed, false) && capturedDirector != null)
                Finish("Cancelled before script reload", 0);
            if (finishing) RestoreCaptureState();
        };
    }

    static void Configure(bool exit)
    {
        string reference = Environment.GetEnvironmentVariable("UMA_LIVE_CAPTURE_REFERENCE");
        if (string.IsNullOrEmpty(reference)) plan = LiveCapturePlan.Default();
        else
        {
            // Only a capture manifest, never arbitrary application configuration.
            if (!string.Equals(Path.GetFileName(reference), "manifest.json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("UMA_LIVE_CAPTURE_REFERENCE must name a reference manifest.json.");
            plan = JsonUtility.FromJson<LiveCapturePlan>(File.ReadAllText(reference));
        }
        plan.Validate();
        string frames = Environment.GetEnvironmentVariable("UMA_LIVE_CAPTURE_FRAMES");
        if (!string.IsNullOrWhiteSpace(frames)) plan.SelectFrames(frames.Split(',').Select(int.Parse).ToArray());
        SessionState.SetString(PlanState, JsonUtility.ToJson(plan));
        SessionState.SetBool(ExitState, exit);
        exitOnFinish = exit;
        filmIsolation = false;
        SessionState.SetBool(FilmIsolationState, false);
        captureIndex = 0; gate.Reset(); requested = false; deadline = 0;
    }

    public static void Start()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Automatic scene-loading capture is restricted to an isolated batch Editor. Use Capture current Live in an interactive Editor.");
        if (SessionState.GetBool(Armed, false) || finishing) throw new InvalidOperationException("A capture is already running or restoring state.");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start capture from edit mode.");
        Configure(true);
        LiveRenderSetup.Install();
        SessionState.SetBool(Armed, true);
        EditorSceneManager.OpenScene("Assets/Scenes/Version2.unity");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("UmaViewer/Rendering/Capture current Live diagnostic frames (308,868,3848)")]
    public static void CaptureCurrentDiagnosticFrames()
    {
        BeginCurrentLiveCapture(true);
    }

    [MenuItem("UmaViewer/Rendering/Capture current Live BlinkFilm diagnostic frames (900,1240,3900)")]
    public static void CaptureCurrentBlinkFilmFrames()
    {
        // Live1176/stage10148 authored PostFilm sync intervals; report still requires
        // identity/parameter checks if a different song is currently loaded.
        BeginCurrentLiveCapture(true, new[] { 900, 1240, 3900 });
    }

    [MenuItem("UmaViewer/Rendering/Capture current Live off/noFilm/on (900,1240,3900)")]
    public static void CaptureCurrentFilmIsolation()
    {
        BeginCurrentLiveCapture(true, new[] { 900, 1240, 3900 });
        filmIsolation = true;
        SessionState.SetBool(FilmIsolationState, true);
    }

    [MenuItem("UmaViewer/Rendering/Capture current Live post-effects off-on")]
    public static void CaptureCurrentLive()
    {
        BeginCurrentLiveCapture(false);
    }

    private static void BeginCurrentLiveCapture(bool diagnostic, int[] diagnosticFrames = null)
    {
        if (SessionState.GetBool(Armed, false) || finishing) throw new InvalidOperationException("A capture is already running or restoring state.");
        var director = Director.instance;
        if (!EditorApplication.isPlaying || director == null || !director._isLiveSetup)
            throw new InvalidOperationException("Load the reference Live and characters first.");
        if (director.IsRecordVMD) throw new InvalidOperationException("Capture cannot seek while VMD recording is active.");
        Configure(false);
        if (diagnostic)
        {
            // A same-session off/on experiment, NOT a matched game-reference capture.
            // Use actual cast identity instead of silently imposing the song1001 preset.
            plan = new LiveCapturePlan {
                songId = director.live.MusicId,
                stageId = director.live.BackGroundId,
                characters = director.CharaContainerScript.Select(x => x.CharaEntry.Id + "_" + x.VarCostumeIdLong).ToArray(),
                frames = diagnosticFrames ?? new[] { 308, 868, 3848 }
            };
            plan.Validate();
            if (plan.frames.Any(x => x / (float)plan.fps >= director.totalTime))
                throw new InvalidOperationException("Diagnostic frame exceeds the loaded Live duration.");
            SessionState.SetString(PlanState, JsonUtility.ToJson(plan));
        }
        requested = true;
        deadline = EditorApplication.timeSinceStartup + 180;
        captureIndex = 0; gate.Reset();
        capturedDirector = director;
        previousProgress = director.UI.ProgressBar.value;
        previousSync = director._syncTime;
        previousTouched = director.sliderControl.is_Touched;
        previousOuted = director.sliderControl.is_Outed;
        SessionState.SetBool(Armed, true);
        preview = ScriptableObject.CreateInstance<CapturePreview>();
        preview.titleContent = new GUIContent("Live capture preview");
        preview.minSize = new Vector2(560, 380);
        preview.ShowUtility();
        Debug.Log("[LiveRenderCapture] Capturing off-screen: Live time is held temporarily. Use the preview to cancel; playback is restored on completion.");
    }

    [MenuItem("UmaViewer/Rendering/Cancel Live capture")]
    public static void CancelCapture()
    {
        if (SessionState.GetBool(Armed, false)) Finish("Cancelled by user", 0);
    }

    private sealed class CapturePreview : EditorWindow
    {
        void OnInspectorUpdate() { Repaint(); }
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Capture uses an off-screen camera target. The Game view may say 'No cameras rendering'. Live time is held while each frame settles; this preview shows the capture target.", MessageType.Info);
            if (plan != null && captureIndex < plan.frames.Length * CaptureSlots)
                GUILayout.Label("Frame " + CaptureFrame + " / effects " + VariantName + " / saved " + captureIndex + " of " + plan.frames.Length * CaptureSlots);
            if (GUILayout.Button("Cancel capture and restore playback")) CancelCapture();
            var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (target != null) GUI.DrawTexture(rect, target, ScaleMode.ScaleToFit, false);
        }
        void OnDisable() { if (!finishing) CancelCapture(); }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Armed, false)) return;
        // Entering play mode reloads statics. Restore intent rather than hanging
        // the isolated Editor after a successful batch capture.
        if (plan == null)
        {
            plan = JsonUtility.FromJson<LiveCapturePlan>(SessionState.GetString(PlanState, ""));
            exitOnFinish = SessionState.GetBool(ExitState, false);
            filmIsolation = SessionState.GetBool(FilmIsolationState, false);
            if (plan == null) { Finish("Capture plan missing after reload", 1); return; }
        }
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + System.Math.Max(600, plan.frames.Length * 20);
        if (EditorApplication.timeSinceStartup > deadline) { Finish("Timed out loading/capturing Live", 1); return; }
        if (!EditorApplication.isPlaying) return;
        try
        {
            var director = Director.instance;
            if (director == null || !director._isLiveSetup)
            {
                if (requested) return;
                var main = UmaViewerMain.Instance;
                var ui = UmaViewerUI.Instance;
                if (main == null || ui == null || !ShaderManager.IsShaderBundleReady) return;
                var live = main.Lives.FirstOrDefault(x => x.MusicId == plan.songId);
                if (live == null || main.Characters.Count < live.MemberCount) return;
                var selections = new List<LiveCharacterSelect>();
                if (live.MemberCount != plan.characters.Length || live.BackGroundId != plan.stageId)
                    throw new InvalidOperationException("Reference song/stage/member count differs from loaded Live metadata.");
                for (int i = 0; i < live.MemberCount; ++i)
                {
                    var item = new GameObject("CaptureSelection_" + i).AddComponent<LiveCharacterSelect>();
                    LiveCapturePlan.ParseCharacterIdentity(plan.characters[i], out int id, out string costume);
                    item.CharaEntry = main.Characters.First(x => !x.IsMob && x.Id == id);
                    item.CostumeId = costume;
                    selections.Add(item);
                }
                ui.LiveMode = 1;
                ui.isRequireStage = true;
                ui.isRecordVMD = false;
                requested = true;
                Debug.Log("[LiveRenderCapture] loading live" + plan.songId + " characters=" + string.Join(",", selections.Select(x => x.CharaEntry.Id + "_" + x.CostumeId)));
                UmaViewerBuilder.Instance.LoadLive(live, selections);
                return;
            }
            if (capturedDirector == null)
            {
                if (director.IsRecordVMD) throw new InvalidOperationException("Capture cannot seek while VMD recording is active.");
                capturedDirector = director;
                previousProgress = director.UI.ProgressBar.value;
                previousSync = director._syncTime;
                previousTouched = director.sliderControl.is_Touched;
                previousOuted = director.sliderControl.is_Outed;
            }
            if (!plan.MatchesIdentity(director.live.MusicId, director.live.BackGroundId,
                director.CharaContainerScript.Select(x => x.CharaEntry.Id + "_" + x.VarCostumeIdLong).ToArray()))
                throw new InvalidOperationException("Loaded Live cast/costumes/song/stage do not match capture reference.");
            if (plan.frames.Any(x => x / (float)plan.fps >= director.totalTime))
                throw new InvalidOperationException("Reference capture frame lies outside this Live timeline.");
            director._syncTime = true;
            director.sliderControl.is_Outed = false;
            director.sliderControl.is_Touched = true;
            director.UI.ProgressBar.SetValueWithoutNotify(CaptureFrame / ((float)plan.fps * director.totalTime));
            if (target == null)
            {
                output = Path.GetFullPath("captures/render-smoke/" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
                Directory.CreateDirectory(output);
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Resources/RenderPipeline/UMAUniversalRenderPipelineAsset_Renderer.asset");
                feature = renderer.rendererFeatures.OfType<PostImageEffectFeature>().Single();
                originalActive = feature.isActive;
                originalRendering = feature.EnableRendering;
                target = new RenderTexture(plan.width, plan.height, 24, RenderTextureFormat.ARGB32);
                target.Create();
                feature.SetActive(true);
                feature.EnableRendering = false;
                File.WriteAllText(Path.Combine(output, "capture-plan.json"), JsonUtility.ToJson(plan, true));
                Debug.Log("[LiveRenderCapture] output=" + output);
            }
            if (camera != director.MainRenderCamera)
            {
                if (camera != null) camera.targetTexture = previousTarget;
                camera = director.MainRenderCamera;
                if (camera == null) return;
                previousTarget = camera.targetTexture;
                camera.targetTexture = target;
                gate.Reset();
            }
        }
        catch (Exception e) { Finish(e.ToString(), 1); }
    }

    static void BeforeCamera(ScriptableRenderContext context, Camera renderedCamera)
    {
        if (!SessionState.GetBool(Armed, false) || !filmIsolation || CaptureVariant != 1 ||
            target == null || renderedCamera != camera || feature == null || !feature.isActive || filmSuppressed) return;
        // Scoped to one camera's URP render. The timeline owns these values; never
        // mutate them outside begin/endCameraRendering or persist them to scene assets.
        SuppressFilm();
        suppressedThisRender = true;
    }

    // Kept separate from the SRP callback so the exact struct write/restore
    // contract can be checked in an isolated Editor without loading a Live.
    internal static void SuppressFilm()
    {
        if (filmSuppressed) throw new InvalidOperationException("Film capture suppression is already active.");
        var runtime = PostImageEffectFeature.RuntimeParameter;
        var p = runtime.DofDiffuionBloomOverlay;
        savedMode1 = p.Overlay1.PostFilmMode;
        savedMode2 = p.Overlay2.PostFilmMode;
        savedMode3 = p.Overlay3.PostFilmMode;
        p.Overlay1.PostFilmMode = Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode.None;
        p.Overlay2.PostFilmMode = Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode.None;
        p.Overlay3.PostFilmMode = Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode.None;
        runtime.DofDiffuionBloomOverlay = p;
        filmSuppressed = true;
    }

    internal static void RestoreFilm()
    {
        if (!filmSuppressed) return;
        var runtime = PostImageEffectFeature.RuntimeParameter;
        var p = runtime.DofDiffuionBloomOverlay;
        // Restore only the fields we changed. Other timeline-owned parameters
        // must survive even if another camera callback refreshed them.
        p.Overlay1.PostFilmMode = savedMode1;
        p.Overlay2.PostFilmMode = savedMode2;
        p.Overlay3.PostFilmMode = savedMode3;
        runtime.DofDiffuionBloomOverlay = p;
        filmSuppressed = false;
    }

    static void AfterCamera(ScriptableRenderContext context, Camera renderedCamera)
    {
        if (!SessionState.GetBool(Armed, false) || target == null || renderedCamera != camera) return;
        bool wasSuppressed = filmSuppressed && suppressedThisRender;
        RestoreFilm();
        suppressedThisRender = false;
        var director = Director.instance;
        if (director == null || director != capturedDirector || camera != director.MainRenderCamera) { gate.Reset(); return; }
        if (!gate.Observe(director._liveCurrentTime, CaptureFrame, plan.fps,
            Time.frameCount, ShaderManager.IsShaderBundleReady && feature.isActive && feature.EnableRendering == (CaptureVariant != 0) &&
                (CaptureVariant != 1 || !filmIsolation || wasSuppressed))) return;
        Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            context.Submit();
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            string name = "frame" + CaptureFrame.ToString("D6") + "_" + VariantName;
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            var p = PostImageEffectFeature.RuntimeParameter.DofDiffuionBloomOverlay;
            File.WriteAllText(Path.Combine(output, name + ".txt"),
                $"requestedFrame={CaptureFrame}\nfps={plan.fps}\ntime={Director.instance._liveCurrentTime:R}\ncamera={camera.name}\nmode={p.UseDofDiffusionBloomType}\nfeature={feature.isActive}\nimageEffects={feature.EnableRendering}\nfilmIsolation={filmIsolation && CaptureVariant == 1}\nshaderReady={ShaderManager.IsShaderBundleReady}\nbloomEnabled={p.IsEnableBloom}\ndiffusionEnabled={p.IsEnableDiffusion}\nbloomWeight={p.BloomDofWeight:R}\nintensity={p.BloomIntensity:R}\nthreshold={p.BloomThreshold:R}\nbloomRadius={p.BloomBlurSize:R}\nbloomBlend={p.BloomBlendMode}\ndiffusionBright={p.DiffusionBright:R}\ndiffusionThreshold={p.DiffusionThreshold:R}\ndiffusionSaturation={p.DiffusionSaturation:R}\ndiffusionContrast={p.DiffusionContrast:R}\nfilm1={p.Overlay1.PostFilmMode}:{p.Overlay1.PostFilmPower:R}\nfilm2={p.Overlay2.PostFilmMode}:{p.Overlay2.PostFilmPower:R}\nfilm3={p.Overlay3.PostFilmMode}:{p.Overlay3.PostFilmPower:R}\nfilmBlinkLookup={Director.instance._liveTimelineControl.PostFilmBlinkLookupStatus}\nfilmColor0={FilmColor0(p.Overlay1.PostFilmColor0)}|{FilmColor0(p.Overlay2.PostFilmColor0)}|{FilmColor0(p.Overlay3.PostFilmColor0)}\nformat={target.graphicsFormat}\ndof={p.IsEnableDof}\nfocusType={p.DofFocalType}\nfocusPosition={p.DofFocalPosition}\nfocalPoint={p.DofFocalPoint:R}\nlookAtValid={Director.instance._liveTimelineControl.HasDofCameraLookAt}\nlookAt={Director.instance._liveTimelineControl.DofCameraLookAt}\ncharacters={string.Join(",", Director.instance.CharaContainerScript.Select(x => x.CharaEntry.Id + "_" + x.VarCostumeIdLong))}\n");
            Debug.Log("[LiveRenderCapture] saved " + name);
            captureIndex++;
            gate.Reset();
            if (captureIndex == plan.frames.Length * CaptureSlots) { Finish("Completed: " + output, 0); return; }
            feature.EnableRendering = CaptureVariant != 0;
        }
        catch (Exception e) { Finish(e.ToString(), 1); }
        finally
        {
            RenderTexture.active = previous;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }

    private static string FilmColor0(Color c) => string.Format(CultureInfo.InvariantCulture,
        "{0:R},{1:R},{2:R},{3:R}", c.r, c.g, c.b, c.a);

    static void Finish(string message, int code)
    {
        if (finishing) return;
        finishCode = code;
        RestoreFilm();
        suppressedThisRender = false;
        SessionState.SetBool(Armed, false);
        if (feature != null)
        {
            feature.EnableRendering = originalRendering;
            feature.SetActive(originalActive);
        }
        if (camera != null) camera.targetTexture = previousTarget;
        Debug.Log("[LiveRenderCapture] " + message);
        finishing = true;
        // Leave the render callback before releasing render targets.
        EditorApplication.delayCall += RestoreCaptureState;
    }

    static void RestoreCaptureState()
    {
        if (!finishing) return;
        EditorApplication.delayCall -= RestoreCaptureState;
        if (capturedDirector != null)
        {
            capturedDirector.UI.ProgressBar.SetValueWithoutNotify(previousProgress);
            capturedDirector._syncTime = previousSync;
            capturedDirector.sliderControl.is_Touched = previousTouched;
            capturedDirector.sliderControl.is_Outed = previousOuted;
        }
        if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        if (preview != null) preview.Close();
        preview = null;
        target = null;
        camera = null;
        feature = null;
        capturedDirector = null;
        deadline = 0;
        plan = null;
        SessionState.EraseString(PlanState);
        SessionState.EraseBool(ExitState);
        SessionState.EraseBool(FilmIsolationState);
        filmIsolation = false;
        suppressedThisRender = false;
        finishing = false;
        if (exitOnFinish) EditorApplication.Exit(finishCode);
    }
    static int finishCode;
}
#endif
