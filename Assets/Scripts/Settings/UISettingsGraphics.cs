using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public static class UISettingsGraphics
{
    private static readonly int[] Rates = { 60, 30, 90, 120, 144, 165, 240, 0 };

    public static List<string> FrameRateLabels()
    {
        var labels = new List<string>();
        foreach (int rate in Rates) labels.Add(rate == 0 ? "Unlimited" : rate + " FPS");
        if (FrameRateIndex() == Rates.Length) labels.Add(Config.Instance.TargetFrameRate + " FPS");
        return labels;
    }

    public static int FrameRateIndex()
    {
        int rate = Math.Max(0, Config.Instance.TargetFrameRate);
        int index = Array.IndexOf(Rates, rate);
        return index < 0 ? Rates.Length : index;
    }

    public static int FrameRateAt(int index)
    {
        return index >= 0 && index < Rates.Length ? Rates[index] : Config.Instance.TargetFrameRate;
    }

    public static InputField AddFrameRateInput(Transform parent, Action refresh, Font font = null)
    {
        return AddNumber(parent, "Custom FPS (0 = unlimited)", Config.Instance.TargetFrameRate.ToString(), true,
            text =>
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rate) || rate < 0)
                    return Config.Instance.TargetFrameRate.ToString();
                Config.Instance.TargetFrameRate = rate;
                Config.Instance.UpdateConfig(false);
                UmaViewerMain.ApplyFrameRateLimit();
                refresh();
                return rate.ToString();
            }, font);
    }

    public static void Build(Dropdown frameRate)
    {
        if (frameRate == null) return;
        var template = (RectTransform)frameRate.transform.parent;
        var parent = (RectTransform)template.parent;
        if (parent.Find("GraphicsOptions") != null) return;
        var window = parent.parent as RectTransform;
        if (window != null) window.sizeDelta = new Vector2(340, 400);
        var layout = parent.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
        }
        var font = frameRate.captionText.font;
        FitDropdown(template, frameRate, "Frame Rate");

        // Keep the original camera/lyrics controls and put quality rows in their parent.
        // Scrolling keeps all options reachable on smaller windows.
        var scrollObject = new GameObject("GraphicsOptions", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        scrollObject.transform.SetParent(parent, false);
        scrollObject.layer = parent.gameObject.layer;
        var scrollRect = (RectTransform)scrollObject.transform;
        scrollRect.sizeDelta = new Vector2(320, 280);
        scrollObject.GetComponent<LayoutElement>().preferredHeight = 280;
        scrollObject.GetComponent<Image>().color = new Color(0, 0, 0, 0.1f);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollRect, false);
        Stretch((RectTransform)viewport.transform, 0, 1, 0, 0);
        var contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport.transform, false);
        var content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 5;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;

        var customFps = AddFrameRateInput(content, () => RefreshFrameRate(frameRate), font);
        frameRate.onValueChanged.AddListener(index => customFps.SetTextWithoutNotify(FrameRateAt(index).ToString()));
        AddChoice(content, template, "MSAA", new[] { "Off", "2x", "4x", "8x" }, Config.Instance.AntiAliasing,
            value => Config.Instance.AntiAliasing = value);
        AddNumber(content, "Render Scale (%)", Config.Instance.RenderScalePercent.ToString(), true, text =>
        {
            if (int.TryParse(text, out int value) && value >= 50 && value <= 200)
                Config.Instance.RenderScalePercent = value;
            SaveGraphics();
            return Config.Instance.RenderScalePercent.ToString();
        }, font);
        AddChoice(content, template, "Anisotropic Filter", new[] { "Off", "Per Texture", "Force On" }, Config.Instance.AnisotropicFiltering,
            value => Config.Instance.AnisotropicFiltering = value);
        AddChoice(content, template, "Texture Quality", new[] { "Full", "Half", "Quarter", "Eighth" }, Config.Instance.TextureMipmapLimit,
            value => Config.Instance.TextureMipmapLimit = value);
        AddNumber(content, "LOD Bias (0.3 - 2)", Config.Instance.LodBias.ToString(CultureInfo.InvariantCulture), false, text =>
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value >= 0.3f && value <= 2f)
                Config.Instance.LodBias = value;
            SaveGraphics();
            return Config.Instance.LodBias.ToString(CultureInfo.InvariantCulture);
        }, font);
        AddNumber(content, "Shadow Distance", Config.Instance.ShadowDistance.ToString(CultureInfo.InvariantCulture), false, text =>
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value >= 0 && value <= 150)
                Config.Instance.ShadowDistance = value;
            SaveGraphics();
            return Config.Instance.ShadowDistance.ToString(CultureInfo.InvariantCulture);
        }, font);
    }

    private static void RefreshFrameRate(Dropdown dropdown)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(FrameRateLabels());
        dropdown.SetValueWithoutNotify(FrameRateIndex());
        dropdown.RefreshShownValue();
    }

    private static void SaveGraphics()
    {
        Config.Instance.UpdateConfig(false);
        RuntimeGraphicsSettings.Apply();
        if (UmaViewerUI.Instance != null && UmaViewerUI.Instance.CameraSettings != null)
            UmaViewerUI.Instance.CameraSettings.AAModeDropdown.SetValueWithoutNotify(Config.Instance.AntiAliasing);
    }

    private static void AddChoice(Transform parent, RectTransform template, string label, string[] options, int value, Action<int> change)
    {
        var row = UnityEngine.Object.Instantiate(template.gameObject, parent);
        row.name = label;
        var dropdown = row.GetComponentInChildren<Dropdown>();
        dropdown.onValueChanged = new Dropdown.DropdownEvent();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, options.Length - 1));
        dropdown.onValueChanged.AddListener(index => { change(index); SaveGraphics(); });
        FitDropdown((RectTransform)row.transform, dropdown, label);
    }

    private static void FitDropdown(RectTransform row, Dropdown dropdown, string label)
    {
        row.sizeDelta = new Vector2(320, 30);
        Stretch((RectTransform)dropdown.transform, 0.58f, 1, 0, 0);
        foreach (var text in row.GetComponentsInChildren<Text>(true))
        {
            if (text.transform.IsChildOf(dropdown.transform)) continue;
            text.text = label;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleLeft;
            Stretch(text.rectTransform, 0, 0.56f, 0, 0);
        }
    }

    private static InputField AddNumber(Transform parent, string label, string value, bool integer, Func<string, string> submit, Font font)
    {
        var row = new GameObject(label, typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.layer = parent.gameObject.layer;
        ((RectTransform)row.transform).sizeDelta = new Vector2(320, 30);
        row.GetComponent<LayoutElement>().preferredHeight = 30;
        var caption = AddText(row.transform, "Label", label, font);
        Stretch(caption.rectTransform, 0, 0.58f, 0, 0);
        var field = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        field.transform.SetParent(row.transform, false);
        field.layer = row.layer;
        Stretch((RectTransform)field.transform, 0.6f, 1, 0, 0);
        field.GetComponent<Image>().color = Color.white;
        var text = AddText(field.transform, "Text", value, font);
        text.color = Color.black;
        Stretch(text.rectTransform, 0, 1, 6, -6);
        var input = field.GetComponent<InputField>();
        input.targetGraphic = field.GetComponent<Image>();
        input.textComponent = text;
        input.contentType = integer ? InputField.ContentType.IntegerNumber : InputField.ContentType.DecimalNumber;
        input.text = value;
        input.onEndEdit.AddListener(raw => input.SetTextWithoutNotify(submit(raw)));
        return input;
    }

    private static Text AddText(Transform parent, string name, string value, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var text = go.GetComponent<Text>();
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static void Stretch(RectTransform rect, float left, float right, float insetLeft, float insetRight)
    {
        rect.anchorMin = new Vector2(left, 0);
        rect.anchorMax = new Vector2(right, 1);
        rect.offsetMin = new Vector2(insetLeft, 0);
        rect.offsetMax = new Vector2(insetRight, 0);
    }
}
