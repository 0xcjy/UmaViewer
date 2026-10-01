#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using Gallop.RenderPipeline;
using Overlay = Gallop.ImageEffect.ScreenOverlay.Overlay;

public static class LiveCaptureContractRegression
{
    static void Check(bool ok, string message)
    { if (!ok) throw new Exception("LiveCaptureContractRegression: " + message); }
    static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, message);
    }
    public static void Run()
    {
        Check(Application.isBatchMode, "isolated batch only");
        var plan = LiveCapturePlan.Default(); plan.Validate();
        LiveCapturePlan.ParseCharacterIdentity("1068_0001_00", out int genericId, out string genericCostume);
        Check(genericId == 1068 && genericCostume == "0001_00", "full generic costume preserved");
        LiveCapturePlan.ParseCharacterIdentity("1068_00", out int normalId, out string normalCostume);
        Check(normalId == 1068 && normalCostume == "00", "short costume preserved");
        foreach (string bad in new[] { "", "1068", "1068_", "1068__00", "1068_00_", "1068_../00", "0_00" })
            Reject(() => LiveCapturePlan.ParseCharacterIdentity(bad, out _, out _), "reject malformed identity");
        var genericPlan = LiveCapturePlan.Default();
        genericPlan.characters[0] = "1068_0001_00";
        genericPlan.Validate();
        var genericRoundTrip = JsonUtility.FromJson<LiveCapturePlan>(JsonUtility.ToJson(genericPlan));
        genericRoundTrip.Validate();
        Check(genericRoundTrip.MatchesIdentity(genericPlan.songId, genericPlan.stageId, genericPlan.characters), "generic identity survives reload");
        Check(!genericRoundTrip.MatchesIdentity(genericPlan.songId, genericPlan.stageId, LiveCapturePlan.Default().characters), "generic costume not truncated during comparison");
        Check(plan.characters.Length == 18 && plan.MatchesIdentity(1001, "10102", plan.characters), "default aligned identity");
        Check(!plan.MatchesIdentity(1176, "10102", plan.characters) && !plan.MatchesIdentity(1001, "other", plan.characters), "reject wrong song/stage");
        var cast = (string[])plan.characters.Clone(); cast[0] = "1068_02";
        Check(!plan.MatchesIdentity(1001, "10102", cast), "reject wrong costume");
        Reject(() => plan.SelectFrames(new[] { 1 }), "reject reference-missing frame");
        Reject(() => plan.SelectFrames(new[] { 1740, 1740 }), "reject duplicate and restore plan");
        Check(plan.frames.Length == 5, "bad subset must not mutate plan");
        plan.SelectFrames(new[] { 3000, 1740 });
        Check(plan.frames[0] == 3000, "backseek capture order retained");
        var restored = JsonUtility.FromJson<LiveCapturePlan>(JsonUtility.ToJson(plan));
        restored.Validate(); Check(restored.frames[1] == 1740 && restored.characters[2] == "1030_00", "domain-reload plan roundtrip");
        foreach (string name in new[] { "UMA_CAPTURE_TEST_REFERENCE1001", "UMA_CAPTURE_TEST_REFERENCE1176" })
        {
            string path = Environment.GetEnvironmentVariable(name);
            Check(Path.GetFileName(path) == "manifest.json" && File.Exists(path), "explicit real reference manifest");
            var reference = JsonUtility.FromJson<LiveCapturePlan>(File.ReadAllText(path));
            reference.Validate();
            Check(reference.songId == (name.EndsWith("1176") ? 1176 : 1001), "actual song manifest identity");
            reference.SelectFrames(new[] { reference.frames[1], reference.frames[reference.frames.Length - 1] });
        }
        var gate = new LiveCaptureStabilityGate();
        for (int i = 0; i < 59; ++i)
        {
            Check(!gate.Observe(50, 3000, 60, i, true), "needs sixty distinct renders");
            Check(!gate.Observe(50, 3000, 60, i, true), "duplicate callback doesn't advance");
        }
        Check(gate.Observe(50, 3000, 60, 59, true), "sixtieth correct render ready");
        gate.Reset();
        for (int i = 0; i < 80; ++i) Check(!gate.Observe(49, 3000, 60, i, true), "old frame cannot be mislabeled");
        Check(!gate.Observe(float.NaN, 3000, 60, 80, true), "invalid clock");
        for (int i = 81; i < 140; ++i) Check(!gate.Observe(50, 3000, 60, i, true), "settle after mismatch");
        Check(!gate.Observe(50, 3000, 60, 140, false), "shader/feature unavailable clears settle");
        for (int i = 141; i < 200; ++i) Check(!gate.Observe(50, 3000, 60, i, true), "settle after readiness loss");
        Check(gate.Observe(50, 3000, 60, 200, true), "stable readiness restored");
        // Simulate the exact scoped noFilm parameter mutation without a Live camera.
        // The URP 14 callback order is begin -> AddRenderPasses -> Execute -> end;
        // this contract tests struct write-back and restoration, not GPU pixels.
        var runtime = PostImageEffectFeature.RuntimeParameter;
        var old = runtime.DofDiffuionBloomOverlay;
        try
        {
            var film = old;
            film.Overlay1.PostFilmMode = Overlay.PostFilmMode.Add;
            film.Overlay1.PostFilmPower = .31f;
            film.Overlay2.PostFilmMode = Overlay.PostFilmMode.Mul;
            film.Overlay2.PostFilmPower = .42f;
            film.Overlay3.PostFilmMode = Overlay.PostFilmMode.ScreenBlend;
            film.Overlay3.PostFilmPower = .53f;
            runtime.DofDiffuionBloomOverlay = film;
            LiveRenderCapture.SuppressFilm();
            var suppressed = runtime.DofDiffuionBloomOverlay;
            Check(!suppressed.IsEnableOverlay && suppressed.Overlay1.PostFilmPower == .31f &&
                suppressed.Overlay2.PostFilmPower == .42f && suppressed.Overlay3.PostFilmPower == .53f,
                "noFilm must suppress all three overlay validity flags without changing strengths");
            Reject(() => LiveRenderCapture.SuppressFilm(), "nested film suppression must fail");
            suppressed.BloomIntensity = .67f; // another callback can change unrelated timeline state
            runtime.DofDiffuionBloomOverlay = suppressed;
            LiveRenderCapture.RestoreFilm();
            var restoredFilm = runtime.DofDiffuionBloomOverlay;
            Check(restoredFilm.Overlay1.PostFilmMode == Overlay.PostFilmMode.Add &&
                restoredFilm.Overlay2.PostFilmMode == Overlay.PostFilmMode.Mul &&
                restoredFilm.Overlay3.PostFilmMode == Overlay.PostFilmMode.ScreenBlend &&
                restoredFilm.BloomIntensity == .67f,
                "restoration must preserve other timeline fields and all three modes");
            LiveRenderCapture.RestoreFilm(); // harmless after an early cancellation
        }
        finally { LiveRenderCapture.RestoreFilm(); runtime.DofDiffuionBloomOverlay = old; }
        Debug.Log("LiveCaptureContractRegression PASS: two actual reference manifests, identity/subset/reload, stale-frame/duplicate/readiness gates. No full Live was loaded.");
    }
}
#endif
