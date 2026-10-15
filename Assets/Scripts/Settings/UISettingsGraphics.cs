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
}
