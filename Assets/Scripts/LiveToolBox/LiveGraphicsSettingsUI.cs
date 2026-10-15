using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Controles de los ajustes del live. Los objetos viven en la escena `LiveScene` (YAML): este
// componente solo los rellena, sincroniza sus valores con `Config` y cablea los eventos. Los
// desplegables de FPS y de render scale ademas se pueden escribir (ver TypeInDropdown).
public class LiveGraphicsSettingsUI : MonoBehaviour
{
    // Presets del panel del viewer, mas 0 = Unlimited para el live.
    private static readonly int[] FrameRatePresets = { 30, 60, 90, 120, 180, 240, 0 };
    private static readonly int[] RenderScalePresets = { 50, 75, 100, 150, 200 };
    private static readonly string[] MsaaOptions = { "Off", "2x", "4x", "8x" };
    private static readonly string[] AnisoOptions = { "Off", "Per Texture", "Force On" };
    private static readonly string[] TextureQualityOptions = { "Full", "Half", "Quarter", "Eighth" };

    private const int MinRenderScale = 50;
    private const int MaxRenderScale = 200;

    public Dropdown FrameRateDropdown;
    public Dropdown RenderScaleDropdown;
    public Dropdown MsaaDropdown;
    public Dropdown AnisoDropdown;
    public Dropdown TextureQualityDropdown;
    public InputField LodBias;
    public InputField ShadowDistance;

    private TypeInDropdown _frameRate;
    private TypeInDropdown _renderScale;
    private bool _applied;

    // Idempotente: lo llama LiveViewerUI cuando Config ya existe.
    public void Apply()
    {
        if (_applied) return;
        _applied = true;

        if (FrameRateDropdown != null)
        {
            _frameRate = new LegacyTypeInDropdown(FrameRateDropdown, FrameRatePresets, 0, 0, "", FrameRateLabel, ApplyFrameRate);
            _frameRate.SetValue(Config.Instance.TargetFrameRate);
        }

        if (RenderScaleDropdown != null)
        {
            _renderScale = new LegacyTypeInDropdown(RenderScaleDropdown, RenderScalePresets, MinRenderScale, MaxRenderScale, "%", ApplyRenderScale);
            _renderScale.SetValue(Config.Instance.RenderScalePercent);
        }

        if (LodBias != null)
            LodBias.SetTextWithoutNotify(Config.Instance.LodBias.ToString(CultureInfo.InvariantCulture));
        if (ShadowDistance != null)
            ShadowDistance.SetTextWithoutNotify(Config.Instance.ShadowDistance.ToString(CultureInfo.InvariantCulture));

        FillChoice(MsaaDropdown, MsaaOptions, Config.Instance.AntiAliasing, delegate (int value)
        {
            Config.Instance.AntiAliasing = value;
            UISettingsGraphics.SaveGraphics();
        });

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

    private void Update()
    {
        // Un clic dentro de uno de los desplegables escribibles empieza a escuchar el teclado; un
        // clic fuera adopta lo que hubiera escrito.
        if (Input.GetMouseButtonDown(0))
        {
            HandleTypingClick(_frameRate);
            HandleTypingClick(_renderScale);
        }

        if (_frameRate != null) _frameRate.Update();
        if (_renderScale != null) _renderScale.Update();
    }

    // Vuelve a pintar el desplegable de FPS con el valor actual de Config.
    public void RefreshFrameRate()
    {
        if (_frameRate != null) _frameRate.SetValue(Config.Instance.TargetFrameRate);
    }

    private static void HandleTypingClick(TypeInDropdown field)
    {
        if (field == null) return;
        if (ContainsPointer(field.Rect)) field.BeginTyping();
        else if (field.Typing) field.Commit();
    }

    private static bool ContainsPointer(RectTransform rect)
    {
        if (rect == null) return false;
        var canvas = rect.GetComponentInParent<Canvas>();
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
    }

    private static string FrameRateLabel(int fps)
    {
        return fps == 0 ? "Unlimited" : fps.ToString(CultureInfo.InvariantCulture);
    }

    private void ApplyFrameRate(int fps)
    {
        if (Config.Instance.TargetFrameRate != fps)
        {
            Config.Instance.TargetFrameRate = fps;
            Config.Instance.UpdateConfig(false);
        }
        UmaViewerMain.ApplyFrameRateLimitLive();
        if (_frameRate != null) _frameRate.SetValue(fps);
    }

    private void ApplyRenderScale(int percent)
    {
        Config.Instance.RenderScalePercent = Mathf.Clamp(percent, MinRenderScale, MaxRenderScale);
        UISettingsGraphics.SaveGraphics();
        if (_renderScale != null) _renderScale.SetValue(Config.Instance.RenderScalePercent);
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
