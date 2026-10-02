using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;

public class LiveViewerUI : MonoBehaviour
{
    public static LiveViewerUI Instance;

    public UnityEngine.UI.Slider ProgressBar;

    public RectTransform BottonUITransform;

    public Dropdown FrameRateDropDown;

    public GameObject RecordingUI;

    public Text RecordingText;

    public Text LyricsText;

    public List<UmaLyricsData> CurrentLyrics = new List<UmaLyricsData>();

    private UnityEngine.UI.Text progressSeconds;
    private int displayedCurrentTenth = -1;
    private int displayedTotalTenth = -1;

    float targetHeight = 0;
    float height;
    private void Awake()
    {
        if (Config.Instance == null)
        {
            new Config();
        }
        ApplyFrameRateOptions();
        UISettingsGraphics.Build(FrameRateDropDown);
        RuntimeGraphicsSettings.Apply();
        UmaViewerMain.ApplyFrameRateLimit();

        height = BottonUITransform.rect.height;
        targetHeight = 0;
        Invoke(nameof(HideSlider), 1.5f);
        Instance = this;
        CreatePlaybackDiagnostics();
        //TrueProgressBar = (UnityEngine.UIElements.Slider)ProgressBar;
    }

    // The scene reserves 200 canvas units on each side of the progress slider.
    private void CreatePlaybackDiagnostics()
    {
        if (ProgressBar == null || BottonUITransform == null) return;
        progressSeconds = CreateDiagnosticText("ProgressSeconds", BottonUITransform);
        var rect = progressSeconds.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-8, 0);
        rect.sizeDelta = new Vector2(184, 24);
        progressSeconds.alignment = TextAnchor.MiddleRight;
        progressSeconds.text = "0.0s / 0.0s";

    }

    private UnityEngine.UI.Text CreateDiagnosticText(string name, RectTransform parent)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);
        var text = textObject.GetComponent<UnityEngine.UI.Text>();
        text.font = FrameRateDropDown != null && FrameRateDropDown.captionText != null
            ? FrameRateDropDown.captionText.font : null;
        if (text.font == null && LyricsText != null) text.font = LyricsText.font;
        if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }


    private void LateUpdate()
    {
        if (progressSeconds == null) return;
        var director = Gallop.Live.Director.instance;
        float total = director != null ? director.totalTime : 0f;
        if (float.IsNaN(total) || float.IsInfinity(total) || total < 0) total = 0;
        bool seeking = director != null && director.sliderControl != null &&
            (director.sliderControl.is_Touched || director.sliderControl.is_Outed);
        float current = director == null ? 0f : seeking
            ? ProgressBar.normalizedValue * total : director._liveCurrentTime;
        if (float.IsNaN(current) || float.IsInfinity(current)) current = 0;
        int currentTenth = Mathf.FloorToInt(Mathf.Clamp(current, 0, total) * 10f);
        int totalTenth = Mathf.FloorToInt(total * 10f);
        if (currentTenth == displayedCurrentTenth && totalTenth == displayedTotalTenth) return;
        displayedCurrentTenth = currentTenth;
        displayedTotalTenth = totalTenth;
        progressSeconds.text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:F1}s / {1:F1}s", currentTenth / 10f, totalTenth / 10f);
    }

    public void OnMouse(bool isEnter)
    {
        if (isEnter)
        {
            targetHeight = 0;
            CancelInvoke();
        }
        else
        {
            Invoke(nameof(HideSlider), 2.5f);
        }
    }

    private void FixedUpdate()
    {
        BottonUITransform.anchoredPosition = Vector2.Lerp(BottonUITransform.anchoredPosition, new Vector2(0, targetHeight), Time.fixedDeltaTime * 5);
    }

    private void HideSlider()
    {
        targetHeight = -height;
    }

    public void SetFrameRate(int fps)
    {
        Config.Instance.TargetFrameRate = UISettingsGraphics.FrameRateAt(fps);
        Config.Instance.UpdateConfig(false);
        UmaViewerMain.ApplyFrameRateLimit();
    }

    private void ApplyFrameRateOptions()
    {
        if (FrameRateDropDown == null) return;

        FrameRateDropDown.ClearOptions();
        FrameRateDropDown.AddOptions(UISettingsGraphics.FrameRateLabels());
        FrameRateDropDown.SetValueWithoutNotify(UISettingsGraphics.FrameRateIndex());
        FrameRateDropDown.RefreshShownValue();
    }

    public void UpdateLyrics(float time)
    {
        var text = UmaUtility.GetCurrentLyrics(time, CurrentLyrics);
        LyricsText.text = text;
    }
}
