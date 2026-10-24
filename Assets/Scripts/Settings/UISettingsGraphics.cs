using UnityEngine;

public static class UISettingsGraphics
{
    //(Los controles del panel de ajustes del live viven en la escena `LiveScene` (YAML) y los cablea
    //LiveGraphicsSettingsUI. Aqui solo queda lo que aplica el cambio en `Config` y en el runtime.)
    public static void SaveGraphics()
    {
        Config.Instance.UpdateConfig(false);
        RuntimeGraphicsSettings.ApplyLiveRenderScale();
        if (UmaViewerUI.Instance != null && UmaViewerUI.Instance.CameraSettings != null)
            UmaViewerUI.Instance.CameraSettings.AAModeDropdown.SetValueWithoutNotify(Config.Instance.AntiAliasing);
    }

    //(Live: postefectos de la presentacion) Un unico interruptor apaga toda la cadena de imagen
    //del show (DOF/bloom-diffusion, radial blur, correccion de color, exposure/tone curve, sun
    //shafts y light shafts) sin tocar la composicion multicamara ni los proyectores, que es
    //justo lo que la camara libre no recibe. Es el mismo interruptor que usa el grabador.
    public static void ApplyLivePostEffects()
    {
        var feature = Gallop.RenderPipeline.PostImageEffectFeature.Instance;
        if (feature == null) return;
        feature.EnableRendering = Config.Instance == null || Config.Instance.PostEffects;
    }
}
