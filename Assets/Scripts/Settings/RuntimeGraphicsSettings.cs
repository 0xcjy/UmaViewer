using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Quality changes affect an in-memory copy, never the authored pipeline asset.
public static class RuntimeGraphicsSettings
{
    private static UniversalRenderPipelineAsset source;
    private static UniversalRenderPipelineAsset runtime;
    private static readonly int[] Samples = { 1, 2, 4, 8 };

    // Version2 (viewer/UI): usa el ajuste "UIrenderScale" del panel (por defecto 100 % = nativa).
    public static void Apply()
    {
        ApplyScale(Config.Instance != null ? Config.Instance.UIRenderScalePercent / 100f : 1f);
    }

    // Live: aplica el ajuste "Render Scale (%)" del panel del live.
    public static void ApplyLiveRenderScale()
    {
        ApplyScale(Config.Instance != null ? Config.Instance.RenderScalePercent / 100f : 1f);
    }

    // `renderScale` = resolución interna de render (1 = nativa).
    private static void ApplyScale(float renderScale)
    {
        var config = Config.Instance;
        if (config == null) return;
        int samples = Samples[Mathf.Clamp(config.AntiAliasing, 0, 3)];
        QualitySettings.antiAliasing = samples == 1 ? 0 : samples;
        QualitySettings.anisotropicFiltering = (AnisotropicFiltering)Mathf.Clamp(config.AnisotropicFiltering, 0, 2);
        QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(config.TextureMipmapLimit, 0, 3);
        QualitySettings.lodBias = Mathf.Clamp(config.LodBias, 0.3f, 2f);

        var active = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (active == null) return;
        if (active != runtime)
        {
            if (active != source || runtime == null)
            {
                if (runtime != null) Object.Destroy(runtime);
                source = active;
                runtime = Object.Instantiate(active);
                runtime.name = active.name + " (Runtime Graphics)";
                runtime.hideFlags = HideFlags.DontSave;
            }
            // A quality-level override takes precedence over the default pipeline.
            if (QualitySettings.renderPipeline != null) QualitySettings.renderPipeline = runtime;
            else GraphicsSettings.renderPipelineAsset = runtime;
        }
        runtime.msaaSampleCount = samples;
        runtime.renderScale = Mathf.Clamp(renderScale, 0.5f, 2f);
        runtime.shadowDistance = Mathf.Clamp(config.ShadowDistance, 0f, 150f);
    }
}
