using System;
using System.Linq;
using Gallop.RenderPipeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Idempotent renderer registration using Unity serialization, not YAML edits.</summary>
public static class LiveRenderSetup
{
    [MenuItem("UmaViewer/Live/Install recovered post effects")]
    public static void Install()
    {
        const string path = "Assets/Resources/RenderPipeline/UMAUniversalRenderPipelineAsset_Renderer.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if (renderer == null) throw new InvalidOperationException("Missing Live renderer: " + path);
        var feature = renderer.rendererFeatures.OfType<PostImageEffectFeature>().FirstOrDefault();
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<PostImageEffectFeature>();
            feature.name = "Gallop Live Post Effects";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
        }
        feature.SetActive(true);
        renderer.SetDirty();
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(renderer);
        InstallMirrorRenderer(renderer);
        AssetDatabase.SaveAssets();
        Debug.Log("[LiveRenderSetup] Installed recovered post effects: " + path);
    }

    private static void InstallMirrorRenderer(UniversalRendererData source)
    {
        const string path = "Assets/Resources/RenderPipeline/UMASimpleMirrorRenderer.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
            "Assets/Resources/RenderPipeline/UMAUniversalRenderPipelineAsset.asset");
        if (pipeline == null) throw new InvalidOperationException("Missing Live pipeline");
        var simple = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if (simple == null)
        {
            simple = UnityEngine.Object.Instantiate(source);
            simple.name = "UMASimpleMirrorRenderer";
            simple.rendererFeatures.Clear();
            simple.opaqueLayerMask = 0;
            simple.transparentLayerMask = 0;
            AssetDatabase.CreateAsset(simple, path);
            foreach (bool opaque in new[] { true, false })
            {
                var draw = ScriptableObject.CreateInstance<UnityEngine.Experimental.Rendering.Universal.RenderObjects>();
                draw.name = opaque ? "SimpleMirrorOpaque" : "SimpleMirrorTransparent";
                draw.settings.Event = opaque ? RenderPassEvent.BeforeRenderingOpaques : RenderPassEvent.BeforeRenderingTransparents;
                draw.settings.filterSettings.LayerMask = -1;
                draw.settings.filterSettings.RenderQueueType = opaque
                    ? UnityEngine.Experimental.Rendering.Universal.RenderQueueType.Opaque
                    : UnityEngine.Experimental.Rendering.Universal.RenderQueueType.Transparent;
                draw.settings.filterSettings.PassNames = new[] { "SimpleMirrorCaster" };
                AssetDatabase.AddObjectToAsset(draw, simple);
                simple.rendererFeatures.Add(draw);
                EditorUtility.SetDirty(draw);
            }
            simple.SetDirty();
            EditorUtility.SetDirty(simple);
        }
        var serialized = new SerializedObject(pipeline);
        var renderers = serialized.FindProperty("m_RendererDataList");
        if (renderers.arraySize > 1 && renderers.GetArrayElementAtIndex(1).objectReferenceValue != null &&
            renderers.GetArrayElementAtIndex(1).objectReferenceValue != simple)
            throw new InvalidOperationException("Renderer slot 1 is already owned by another renderer");
        if (renderers.arraySize < 2) renderers.arraySize = 2;
        renderers.GetArrayElementAtIndex(1).objectReferenceValue = simple;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
    }

    // Batch-mode contract checks. These validate state routing, not rendered pixels.
    public static void Validate()
    {
        Install();
        Install();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
            "Assets/Resources/RenderPipeline/UMAUniversalRenderPipelineAsset_Renderer.asset");
        Require(renderer.rendererFeatures.OfType<PostImageEffectFeature>().Count() == 1,
            "Renderer registration must be idempotent");
        var source = new Gallop.ImageEffect.DofDiffusionBloomOverlayParam();
        var state = DofDiffusionBloomOverlayPass.Parameter.Default();
        state.Setup(source);
        state.DecideDrawType(false);
        Require(!state.IsValidity, "Default must be a lossless bypass");
        source.IsEnableBloom = true;
        source.BloomIntensity = 0.63f;
        state.Setup(source);
        state.DecideDrawType(false);
        Require(state.UseDofDiffusionBloomType == Gallop.ImageEffect.DofDiffusionBloomOverlayParam.DofDiffusionBloomType.Bloom,
            "Bloom timeline must select Bloom mode");
        Require(Mathf.Approximately(state.BloomIntensity, 0.63f), "Timeline intensity must not be rescaled");
        source.IsEnableDiffusion = true;
        state.Setup(source);
        state.DecideDrawType(false);
        Require(state.UseDofDiffusionBloomType == Gallop.ImageEffect.DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DiffusionBloom,
            "Diffusion must select the diffusion composition");
        source.IsEnableDof = true;
        source.DofFocalType = Gallop.DepthBlurAndBloom.DofFocalType.Position;
        source.DofFocalPosition = new Vector3(1, 2, 3);
        state.Setup(source);
        state.DecideDrawType(true);
        Require(new PostImageEffectFeature.Parameter { DofDiffuionBloomOverlay = state }.IsUseDepthTexture,
            "URP DOF mode must request depth even without overlays");
        Require(state.UseDofDiffusionBloomType == Gallop.ImageEffect.DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DiffusionDofBloom,
            "DOF must compose with existing Bloom/Diffusion");
        Require(state.DofFocalPosition == source.DofFocalPosition, "World-space focus must reach renderer");
        source.IsEnableBloom = false;
        source.IsEnableDiffusion = false;
        state.Setup(source);
        state.DecideDrawType(true);
        Require(new PostImageEffectFeature.Parameter { DofDiffuionBloomOverlay = state }.IsUseDepthTexture,
            "Standalone DOF must request depth without Bloom or overlays");
        source.IsEnableDof = false;
        source.IsEnableBloom = false;
        source.IsEnableDiffusion = false;
        state.Setup(source);
        state.DecideDrawType(false);
        Require(!state.IsValidity, "Disabled key must clear the previous mode");
        var previous = PostImageEffectFeature.RuntimeParameter;
        previous.DofDiffuionBloomOverlay = state;
        PostImageEffectFeature.ResetRuntimeParameter();
        Require(!ReferenceEquals(previous, PostImageEffectFeature.RuntimeParameter), "New Live must reset shared state");
        Require(!PostImageEffectFeature.RuntimeParameter.DofDiffuionBloomOverlay.IsValidity, "New Live must not inherit effects");
        Debug.Log("[LiveRenderValidation] PASS: registration, default bypass, Bloom, Diffusion, disable, intensity, reset");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
