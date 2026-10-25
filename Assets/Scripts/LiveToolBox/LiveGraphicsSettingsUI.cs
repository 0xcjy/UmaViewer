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

    // (VIDEO SPEED) Velocidades del live (Time.timeScale). 1 = normal, <1 = camara lenta.
    private static readonly float[] VideoSpeedValues = { 0.1f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f };
    private static readonly string[] VideoSpeedLabels = { "0.10x", "0.25x", "0.50x", "0.75x", "1.00x", "1.50x", "2.00x" };

    private const int MinRenderScale = 50;
    private const int MaxRenderScale = 200;

    public Dropdown FrameRateDropdown;
    public Dropdown RenderScaleDropdown;
    public Dropdown MsaaDropdown;
    public Toggle PostEffectsToggle;

    private TypeInDropdown _frameRate;
    private TypeInDropdown _renderScale;
    private Dropdown _videoSpeedDropdown;
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

        FillChoice(MsaaDropdown, MsaaOptions, Config.Instance.AntiAliasing, delegate (int value)
        {
            Config.Instance.AntiAliasing = value;
            UISettingsGraphics.SaveGraphics();
        });

        if (PostEffectsToggle != null)
        {
            PostEffectsToggle.SetIsOnWithoutNotify(Config.Instance.PostEffects);
            PostEffectsToggle.onValueChanged.AddListener(delegate (bool value)
            {
                Config.Instance.PostEffects = value;
                Config.Instance.UpdateConfig(false);
                UISettingsGraphics.ApplyLivePostEffects();
                SetPostEffectRowsInteractable(value);
            });
        }

        WirePostEffectRows();

        //Aplica el estado guardado nada mas entrar al live (el interruptor puede venir apagado).
        UISettingsGraphics.ApplyLivePostEffects();
        if (PostEffectsToggle != null) SetPostEffectRowsInteractable(Config.Instance.PostEffects);

        CreateVideoSpeedRow();
    }

    // =========================================================================================
    // (VIDEO SPEED) Fila nueva "Video speed" clonada de la fila de "MAX fps".
    //
    // El panel es un ScrollRect (Viewport -> Content) con VerticalLayoutGroup + ContentSizeFitter,
    // asi que basta con anadir el clon como hermano de la fila original: el layout lo coloca y el
    // contenido crece solo (si no cabe, se desplaza con el scroll). No hace falta tocar la escena.
    // =========================================================================================
    private void CreateVideoSpeedRow()
    {
        if (FrameRateDropdown == null) return;

        Transform sourceRow = FrameRateDropdown.transform.parent;
        if (sourceRow == null || sourceRow.parent == null) return;

        GameObject rowClone = Instantiate(sourceRow.gameObject, sourceRow.parent, false);
        rowClone.name = "VideoSpeedRow";
        rowClone.transform.SetSiblingIndex(sourceRow.GetSiblingIndex() + 1);

        Dropdown dropdown = rowClone.GetComponentInChildren<Dropdown>(true);
        if (dropdown == null)
        {
            Destroy(rowClone);
            return;
        }

        RenameRowLabel(rowClone, dropdown, "Video speed");

        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(VideoSpeedLabels));
        dropdown.SetValueWithoutNotify(IndexOfVideoSpeed(Config.Instance.GetVideoSpeed()));
        dropdown.onValueChanged.AddListener(delegate (int value)
        {
            int index = Mathf.Clamp(value, 0, VideoSpeedValues.Length - 1);
            Config.Instance.VideoSpeed = VideoSpeedValues[index];
            Config.Instance.UpdateConfig(false);

            var director = Gallop.Live.Director.instance;
            if (director != null) director.ApplyVideoSpeed();
        });

        _videoSpeedDropdown = dropdown;
    }

    // =========================================================================================
    // (POST EFFECTS) Elemento unico con listado emergente (estilo Dropdown) y multiseleccion.
    //
    // Los objetos viven en la escena `LiveScene`:
    //  - `PostEffectListButton`  fila "Post effects list" (boton; caption = "N/16")
    //  - `PostEffectListPopup`   panel flotante fuera del Viewport (para que el RectMask2D no lo
    //                            recorte) con una fila label + tick por efecto
    //  - `PostEffectListBlocker` capa transparente a pantalla completa que cierra al clicar fuera
    // Aqui solo se cablean, se posiciona el panel bajo el boton y se sincroniza con `Config`.
    // Un efecto se dibuja solo si el interruptor global Y el suyo estan activos.
    // =========================================================================================
    [Header("Post effects list (scene objects)")]
    public Button PostEffectListButton;
    public Text PostEffectListCaption;
    public GameObject PostEffectListPopup;
    public GameObject PostEffectListBlocker;
    public Toggle[] PostEffectToggles;

    private void WirePostEffectRows()
    {
        if (PostEffectListButton != null)
            PostEffectListButton.onClick.AddListener(TogglePostEffectList);

        if (PostEffectListBlocker != null)
        {
            var button = PostEffectListBlocker.GetComponent<Button>();
            if (button == null) button = PostEffectListBlocker.AddComponent<Button>();
            button.onClick.AddListener(ClosePostEffectList);
        }

        ClosePostEffectList();

        if (PostEffectToggles == null)
        {
            UpdatePostEffectCaption();
            return;
        }

        var ids = Gallop.RenderPipeline.PostEffectSelection.All;
        int count = Mathf.Min(PostEffectToggles.Length, ids.Length);
        for (int i = 0; i < count; i++)
        {
            Toggle toggle = PostEffectToggles[i];
            if (toggle == null) continue;

            Gallop.RenderPipeline.PostEffectId captured = ids[i];
            toggle.SetIsOnWithoutNotify(Gallop.RenderPipeline.PostEffectSelection.IsEnabled(captured));
            toggle.onValueChanged.AddListener(delegate (bool value)
            {
                Gallop.RenderPipeline.PostEffectSelection.SetEnabled(captured, value);
                UpdatePostEffectCaption();
            });
        }

        UpdatePostEffectCaption();
    }

    private void TogglePostEffectList()
    {
        if (PostEffectListPopup == null) return;
        if (PostEffectListPopup.activeSelf) ClosePostEffectList();
        else OpenPostEffectList();
    }

    private void OpenPostEffectList()
    {
        if (PostEffectListPopup == null) return;

        // The popup keeps the anchored placement authored in the scene (anchor top-left of the
        // panel, pivot top-left); it is not repositioned at runtime so the panel layout stays as
        // designed.
        if (PostEffectListBlocker != null) PostEffectListBlocker.SetActive(true);
        PostEffectListPopup.SetActive(true);
        PostEffectListPopup.transform.SetAsLastSibling();
    }

    private void ClosePostEffectList()
    {
        if (PostEffectListPopup != null) PostEffectListPopup.SetActive(false);
        if (PostEffectListBlocker != null) PostEffectListBlocker.SetActive(false);
    }

    private void UpdatePostEffectCaption()
    {
        if (PostEffectListCaption == null) return;
        int total = PostEffectToggles != null ? PostEffectToggles.Length : 0;
        int on = 0;
        if (PostEffectToggles != null)
        {
            for (int i = 0; i < PostEffectToggles.Length; i++)
                if (PostEffectToggles[i] != null && PostEffectToggles[i].isOn) on++;
        }
        PostEffectListCaption.text = on + "/" + total;
    }

    // Con el interruptor global apagado ningun efecto se dibuja: se dejan visibles pero no
    // editables, para que quede claro que el apagado manda.
    private void SetPostEffectRowsInteractable(bool interactable)
    {
        if (PostEffectListButton != null) PostEffectListButton.interactable = true;
        if (PostEffectToggles == null) return;
        for (int i = 0; i < PostEffectToggles.Length; i++)
        {
            if (PostEffectToggles[i] != null) PostEffectToggles[i].interactable = interactable;
        }
    }

    // La etiqueta de la fila es el texto que NO esta dentro del desplegable (el resto son el
    // caption y los items del propio Dropdown).
    private static void RenameRowLabel(GameObject row, Dropdown dropdown, string text)
    {
        var texts = row.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null || texts[i].transform.IsChildOf(dropdown.transform)) continue;
            texts[i].text = text;
            return;
        }

        var tmpTexts = row.GetComponentsInChildren<TMPro.TMP_Text>(true);
        for (int i = 0; i < tmpTexts.Length; i++)
        {
            if (tmpTexts[i] == null || tmpTexts[i].transform.IsChildOf(dropdown.transform)) continue;
            tmpTexts[i].text = text;
            return;
        }
    }

    private static int IndexOfVideoSpeed(float speed)
    {
        int best = 4;   // 1.00x
        float bestDelta = float.MaxValue;
        for (int i = 0; i < VideoSpeedValues.Length; i++)
        {
            float delta = Mathf.Abs(VideoSpeedValues[i] - speed);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }
        return best;
    }

    private void Update()
    {
        // El listado de post efectos se cierra con Escape (o clic fuera, via el blocker).
        if (PostEffectListPopup != null && PostEffectListPopup.activeSelf &&
            Input.GetKeyDown(KeyCode.Escape))
            ClosePostEffectList();

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
