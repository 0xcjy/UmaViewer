using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Controles de los ajustes graficos del live. Los objetos viven en la escena `LiveScene` (YAML):
// este componente solo los rellena, sincroniza sus valores con `Config` y cablea los eventos.
public class LiveGraphicsSettingsUI : MonoBehaviour
{
    private static readonly string[] MsaaOptions = { "Off", "2x", "4x", "8x" };
    private static readonly string[] AnisoOptions = { "Off", "Per Texture", "Force On" };
    private static readonly string[] TextureQualityOptions = { "Full", "Half", "Quarter", "Eighth" };

    public Dropdown FrameRateDropdown;
    public InputField CustomFps;
    public Dropdown MsaaDropdown;
    public InputField RenderScale;
    public Dropdown AnisoDropdown;
    public Dropdown TextureQualityDropdown;
    public InputField LodBias;
    public InputField ShadowDistance;

    private bool _applied;

    // Idempotente: lo llama LiveViewerUI cuando Config ya existe.
    public void Apply()
    {
        if (_applied) return;
        _applied = true;

        RefreshFrameRate();
        if (CustomFps != null)
            CustomFps.SetTextWithoutNotify(Config.Instance.TargetFrameRate.ToString(CultureInfo.InvariantCulture));
        if (RenderScale != null)
            RenderScale.SetTextWithoutNotify(Config.Instance.RenderScalePercent.ToString(CultureInfo.InvariantCulture));
        if (LodBias != null)
            LodBias.SetTextWithoutNotify(Config.Instance.LodBias.ToString(CultureInfo.InvariantCulture));
        if (ShadowDistance != null)
            ShadowDistance.SetTextWithoutNotify(Config.Instance.ShadowDistance.ToString(CultureInfo.InvariantCulture));

        if (FrameRateDropdown != null)
        {
            FrameRateDropdown.onValueChanged.AddListener(delegate (int index)
            {
                if (CustomFps != null)
                    CustomFps.SetTextWithoutNotify(
                        UISettingsGraphics.FrameRateAt(index, Config.Instance.TargetFrameRate).ToString(CultureInfo.InvariantCulture));
            });
        }

        if (CustomFps != null)
        {
            CustomFps.onEndEdit.AddListener(delegate (string raw)
            {
                int rate;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out rate) && rate >= 0)
                {
                    Config.Instance.TargetFrameRate = rate;
                    Config.Instance.UpdateConfig(false);
                    UmaViewerMain.ApplyFrameRateLimitLive();
                    RefreshFrameRate();
                }
                CustomFps.SetTextWithoutNotify(Config.Instance.TargetFrameRate.ToString(CultureInfo.InvariantCulture));
            });
        }

        FillChoice(MsaaDropdown, MsaaOptions, Config.Instance.AntiAliasing, delegate (int value)
        {
            Config.Instance.AntiAliasing = value;
            UISettingsGraphics.SaveGraphics();
        });

        if (RenderScale != null)
        {
            RenderScale.onEndEdit.AddListener(delegate (string raw)
            {
                int value;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 50 && value <= 200)
                    Config.Instance.RenderScalePercent = value;
                UISettingsGraphics.SaveGraphics();
                RenderScale.SetTextWithoutNotify(Config.Instance.RenderScalePercent.ToString(CultureInfo.InvariantCulture));
            });
        }

        FillChoice(AnisoDropdown, AnisoOptions, Config.Instance.AnisotropicFiltering, delegate (int value)
        {
            Config.Instance.AnisotropicFiltering = value;
            UISettingsGraphics.SaveGraphics();
        });

        FillChoice(TextureQualityDropdown, TextureQualityOptions, Config.Instance.TextureMipmapLimit, delegate (int value)
        {
            Config.Instance.TextureMipmapLimit = value;
            UISettingsGraphics.SaveGraphics();
        });

        if (LodBias != null)
        {
            LodBias.onEndEdit.AddListener(delegate (string raw)
            {
                float value;
                if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0.3f && value <= 2f)
                    Config.Instance.LodBias = value;
                UISettingsGraphics.SaveGraphics();
                LodBias.SetTextWithoutNotify(Config.Instance.LodBias.ToString(CultureInfo.InvariantCulture));
            });
        }

        if (ShadowDistance != null)
        {
            ShadowDistance.onEndEdit.AddListener(delegate (string raw)
            {
                float value;
                if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= 150)
                    Config.Instance.ShadowDistance = value;
                UISettingsGraphics.SaveGraphics();
                ShadowDistance.SetTextWithoutNotify(Config.Instance.ShadowDistance.ToString(CultureInfo.InvariantCulture));
            });
        }
    }

    public void RefreshFrameRate()
    {
        if (FrameRateDropdown == null) return;
        FrameRateDropdown.ClearOptions();
        FrameRateDropdown.AddOptions(UISettingsGraphics.FrameRateLabels(Config.Instance.TargetFrameRate));
        FrameRateDropdown.SetValueWithoutNotify(UISettingsGraphics.FrameRateIndexOf(Config.Instance.TargetFrameRate));
        FrameRateDropdown.RefreshShownValue();
    }

    private static void FillChoice(Dropdown dropdown, string[] options, int value, UnityEngine.Events.UnityAction<int> change)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, options.Length - 1));
        dropdown.onValueChanged.AddListener(change);
    }
}
