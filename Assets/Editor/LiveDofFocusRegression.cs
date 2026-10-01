using System;
using System.Reflection;
using Gallop;
using Gallop.Live;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEditor;
using UnityEngine;

// Runs the production Director -> image-effect adapter -> render-parameter path.
public static class LiveDofFocusRegression
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("[LiveDofFocusRegression] " + message);
    }
    private static void Set(object obj, string field, object value)
    {
        obj.GetType().GetField(field, PrivateInstance).SetValue(obj, value);
    }
    private static void SetProperty(object obj, string name, object value)
    {
        Set(obj, "<" + name + ">k__BackingField", value);
    }

    private static void RequirePreparedFocus(float expectedDepth)
    {
        // Exercise the real shader-parameter preparation, not a duplicated formula.
        var pass = new DofDiffusionBloomOverlayPass(
            UnityEngine.Rendering.Universal.RenderPassEvent.BeforeRenderingPostProcessing);
        using (var command = new UnityEngine.Rendering.CommandBuffer())
        {
            var state = PostImageEffectFeature.RuntimeParameter.DofDiffuionBloomOverlay;
            object[] args = { default(UnityEngine.Rendering.ScriptableRenderContext), command,
                state, RenderTextureHandle.Make(Shader.PropertyToID("_DofTestSource"), 1920, 1080) };
            typeof(DofDiffusionBloomOverlayPass).GetMethod("PrepareDofParam", PrivateInstance)
                .Invoke(pass, args);
            Require(Mathf.Abs(DofDiffusionBloomOverlayPass.LastDofFocalDepth - expectedDepth) < 0.001f,
                "Prepared focal depth must use the selected focus, not the default 1m value.");
            float sharpEnd = DofDiffusionBloomOverlayPass.LastDofFocalEnd *
                DofDiffusionBloomOverlayPass.LastDofDepthRange;
            Require(Mathf.Abs(sharpEnd - expectedDepth - state.DofFocalSize * 0.5f) < 0.001f,
                "Prepared sharp interval must be centered on the selected focal depth.");
        }
    }

    [MenuItem("UmaViewer/Rendering/Validate DOF focus routing")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || Director.instance != null)
            throw new InvalidOperationException("Run outside Live/Play Mode; validation must not replace active Live state.");
        var previousRuntime = PostImageEffectFeature.RuntimeParameter;
        bool previousDof = GallopImageEffect.UserDofEnabled;
        var root = new GameObject("DofFocusRegression");
        root.SetActive(false);
        try
        {
            var director = root.AddComponent<Director>();
            var timeline = root.AddComponent<LiveTimelineControl>();
            director._liveTimelineControl = timeline;
            var cameraObject = new GameObject("DofTestCamera");
            cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.8f;
            camera.farClipPlane = 1000f;
            camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Set(director, "_cameraObjects", new[] { camera });
            Set(director, "_activeCameraIndex", 0);
            typeof(Director).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, director);
            var effect = cameraObject.AddComponent<GallopImageEffect>();
            effect.DofDiffusionBloomOverlayParam.IsEnableBloom = true;
            effect.DofDiffusionBloomOverlayParam.IsEnableDiffusion = true;
            effect.DofDiffusionBloomOverlayParam.BloomIntensity = 0.63f;
            SetProperty(timeline, "DofTrackCamera", camera);
            SetProperty(timeline, "DofLookAtCamera", camera);
            SetProperty(timeline, "HasDofCameraLookAt", true);
            Vector3 focus = new Vector3(0, 1, 25);
            SetProperty(timeline, "DofCameraLookAt", focus);
            var key = new LiveTimelineKeyPostEffectDOFData
            {
                frame = 0, attribute = (LiveTimelineKeyAttribute)0x50000,
                dofFocalPoint = 1f, forcalSize = 30f, blurSpread = 1.75f,
                dofSmoothness = 0.5f, dofQuality = 1, charactor = 1
            };
            var update = typeof(Director).GetMethod("OnUpdatePostEffect_Dof", PrivateInstance);
            Action apply = () => update.Invoke(director, new object[] { key });
            PostImageEffectFeature.ResetRuntimeParameter();
            GallopImageEffect.SetUserDofEnabled(true);
            apply();
            var state = PostImageEffectFeature.RuntimeParameter.DofDiffuionBloomOverlay;
            Require(state.IsEnableDof, "Valid camera look-at must enable DOF.");
            Require(state.DofFocalType == DepthBlurAndBloom.DofFocalType.Position,
                "Camera look-at was overwritten by the 1m fallback setter (expected Position, got " + state.DofFocalType + ").");
            Require(state.DofFocalPosition == focus, "Camera target must reach renderer unchanged.");
            Require(PostImageEffectFeature.RuntimeParameter.IsUseDepthTexture, "DOF must request depth.");
            RequirePreparedFocus(25f);

            // A subsequent metric-distance key must deliberately switch modes.
            key.attribute = (LiveTimelineKeyAttribute)0x70000;
            key.dofFocalPoint = 8f;
            apply();
            state = PostImageEffectFeature.RuntimeParameter.DofDiffuionBloomOverlay;
            Require(state.DofFocalType == DepthBlurAndBloom.DofFocalType.Point &&
                Mathf.Approximately(state.DofFocalPoint, 8f), "Metric-distance focus must be retained.");

            RequirePreparedFocus(8f - camera.nearClipPlane);

            // Switch back on a later cut, with a different world-space target.
            key.attribute = (LiveTimelineKeyAttribute)0x50000;
            key.dofFocalPoint = 1f;
            focus = new Vector3(1, 2, 4);
            SetProperty(timeline, "DofCameraLookAt", focus);
            apply();
            state = PostImageEffectFeature.RuntimeParameter.DofDiffuionBloomOverlay;
            Require(state.DofFocalType == DepthBlurAndBloom.DofFocalType.Position &&
                state.DofFocalPosition == focus, "Cut back to positional focus must not retain metric mode.");

            RequirePreparedFocus(4f);

            // Invalid target bypasses only DOF, rather than falling back to 1m.
            SetProperty(timeline, "HasDofCameraLookAt", false);
            apply();
            state = PostImageEffectFeature.RuntimeParameter.DofDiffuionBloomOverlay;
            Require(!state.IsEnableDof && state.IsEnableBloom && state.IsEnableDiffusion,
                "Missing focus must preserve Bloom/Diffusion and disable DOF.");
            SetProperty(timeline, "HasDofCameraLookAt", true);
            apply();
            GallopImageEffect.SetUserDofEnabled(false);
            state = PostImageEffectFeature.RuntimeParameter.DofDiffuionBloomOverlay;
            Require(!state.IsEnableDof && state.UseDofDiffusionBloomType ==
                Gallop.ImageEffect.DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DiffusionBloom,
                "User gate must bypass only DOF immediately.");
            Require(Mathf.Approximately(state.BloomIntensity, 0.63f), "DOF gate must not alter Bloom intensity.");
            GallopImageEffect.SetUserDofEnabled(true);
            state = PostImageEffectFeature.RuntimeParameter.DofDiffuionBloomOverlay;
            Require(state.IsEnableDof && state.DofFocalType == DepthBlurAndBloom.DofFocalType.Position,
                "Re-enabling while paused must restore positional focus.");
            Debug.Log("[LiveDofFocusRegression] PASS: startup look-at, metric focus, camera cut, missing target, depth request, prepared focal depths/sharp intervals, DOF gate, paused restore.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            typeof(Director).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            typeof(PostImageEffectFeature).GetField("_runtimeParameter", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, previousRuntime);
            GallopImageEffect.SetUserDofEnabled(previousDof);
        }
    }
}
