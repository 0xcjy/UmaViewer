using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

//(Grabación del live) Menú de ajustes de grabación.
//
//Se monta por código (no vive en ninguna escena) para no tocar el YAML de Version2 ni depender de
//prefabs. Aparece al pulsar RECORD en el panel de selección del show y no arranca nada hasta que se
//acepta: entonces entrega un LiveRecordSettings y se cierra.
public class LiveRecordPanel : MonoBehaviour
{
    public static LiveRecordPanel Instance;

    private LiveRecordSettings _settings = new LiveRecordSettings();
    private Action<LiveRecordSettings> _onAccept;
    private string _songLabel;

    //Controles del formulario.
    private InputField _fps, _start, _end, _folder, _name;
    private Dropdown _resolution;
    private Slider _superSample;
    private Text _superSampleValue;
    private Segmented _antiAliasing;
    private ToggleSwitch _postProcess, _lyrics;
    private Text _status;
    private Text _hint;

    //Resoluciones de salida más habituales. La resolución es UNA sola elección: ancho y alto van
    //juntos (no tiene sentido 1920x480).
    private static readonly string[] ResolutionLabels =
    {
        "640 x 360      (360p)",
        "1280 x 720    (720p)",
        "1920 x 1080  (1080p)",
        "2560 x 1440  (2K)",
        "3840 x 2160  (4K)"
    };
    private static readonly Vector2Int[] ResolutionValues =
    {
        new Vector2Int(640, 360), new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440), new Vector2Int(3840, 2160)
    };

    private static readonly string[] MsaaLabels = { "Off", "2x", "4x", "8x" };
    private static readonly int[] MsaaValues = { 1, 2, 4, 8 };

    //Colores del control segmentado (antialiasing) y de los interruptores.
    private static readonly Color SegmentOn = new Color(0.24f, 0.56f, 0.87f, 1f);
    private static readonly Color SegmentOff = new Color(0.20f, 0.22f, 0.26f, 1f);
    private static readonly Color ToggleOn = new Color(0.30f, 0.72f, 0.24f, 1f);
    private static readonly Color ToggleOff = new Color(0.85f, 0.18f, 0.14f, 1f);

    private Font _font;
    private RectTransform _panel;

    //Ancho útil del texto de ayuda: la ruta se recorta para que no se salga del panel.
    private const int HintMaxChars = 58;
    private const float SuperSampleMin = 50f;
    private const float SuperSampleMax = 200f;

    //Lado de la columna de controles (los inputs ocupan todo el ancho).
    private const float ControlX = 110f;

    private sealed class Segmented
    {
        public int[] Values;
        public int Index;
        public Image[] Backgrounds;

        public void Select(int index)
        {
            Index = Mathf.Clamp(index, 0, Values.Length - 1);
            for (int i = 0; i < Backgrounds.Length; i++)
                if (Backgrounds[i] != null) Backgrounds[i].color = i == Index ? SegmentOn : SegmentOff;
        }

        public int Current => Values[Index];

        public void SetValue(int value)
        {
            for (int i = 0; i < Values.Length; i++)
                if (Values[i] == value) { Select(i); return; }
        }
    }

    //Interruptor de dos caras (ON/OFF): un botón con fondo de color y una bolita que se desplaza.
    private sealed class ToggleSwitch
    {
        public bool IsOn;
        public Image Background;
        public RectTransform Knob;
        public Text Label;
        public float HalfTravel;

        public void Apply()
        {
            if (Background != null) Background.color = IsOn ? ToggleOn : ToggleOff;
            if (Knob != null) Knob.anchoredPosition = new Vector2(IsOn ? HalfTravel : -HalfTravel, 0f);
            if (Label != null)
            {
                Label.text = IsOn ? "ON" : "OFF";
                Label.rectTransform.anchoredPosition = new Vector2(IsOn ? -HalfTravel * 0.55f : HalfTravel * 0.55f, 0f);
            }
        }

        public void Toggle()
        {
            IsOn = !IsOn;
            Apply();
        }
    }

    //Abre el menú. 'songLabel' es sólo texto para el título (id + nombre del show).
    public static LiveRecordPanel Open(string songLabel, LiveRecordSettings defaults,
                                       Action<LiveRecordSettings> onAccept)
    {
        Close();

        var go = new GameObject("LiveRecordPanel",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var panel = go.AddComponent<LiveRecordPanel>();
        panel._settings = defaults != null ? defaults.Clone() : new LiveRecordSettings();
        panel._onAccept = onAccept;
        panel._songLabel = songLabel;
        panel.Build();
        Instance = panel;
        return panel;
    }

    public static void Close()
    {
        if (Instance != null) Destroy(Instance.gameObject);
        Instance = null;
    }

    private void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void Build()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        //OJO: el orden de canvas se guarda en 16 bits (con signo); por encima de 32767 se desborda y
        //el panel acabaría DETRÁS de todo. Con 1000 sobra para quedar por encima de la UI del viewer.
        canvas.sortingOrder = 1000;

        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        //Velo de fondo: bloquea el ratón a lo que hay detrás.
        var dim = NewRect("Dim", transform);
        Stretch(dim);
        var dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.6f);
        dimImage.raycastTarget = true;

        _panel = NewRect("Panel", transform);
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.sizeDelta = new Vector2(860f, 660f);
        var panelImage = _panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.09f, 0.10f, 0.12f, 0.98f);
        panelImage.raycastTarget = true;

        var title = AddText(_panel, "Title", new Vector2(0f, 292f), new Vector2(820f, 40f), 30,
            new Color(1f, 0.87f, 0.35f, 1f), TextAnchor.MiddleCenter);
        title.text = "RECORD LIVE  ·  " + _songLabel;

        float y = 235f;
        const float step = 40f;

        _fps = AddRowInput("FPS", ref y, step, _settings.fps.ToString(CultureInfo.InvariantCulture));

        AddLabel("Resolution", y);
        _resolution = AddDropdown(_panel, new Vector2(ControlX, y), new Vector2(420f, 32f),
            ResolutionLabels, FindResolutionIndex(_settings), value => RefreshHint());
        y -= step;

        AddLabel("Supersampling", y);
        _superSample = AddSlider(_panel, new Vector2(60f, y), new Vector2(280f, 26f),
            SuperSampleMin, SuperSampleMax, _settings.superSamplePercent);
        _superSampleValue = AddText(_panel, "SSValue", new Vector2(320f, y), new Vector2(100f, 26f), 20,
            Color.white, TextAnchor.MiddleLeft);
        _superSample.onValueChanged.AddListener(value =>
        {
            _superSampleValue.text = ((int)value) + " %";
            RefreshHint();
        });
        _superSampleValue.text = ((int)_superSample.value) + " %";
        y -= step;

        AddLabel("Anti-aliasing", y);
        _antiAliasing = AddSegmented(_panel, new Vector2(ControlX, y), new Vector2(420f, 32f),
            MsaaLabels, MsaaValues, _settings.msaa, () => RefreshHint());
        y -= step;

        AddLabel("Post effects", y);
        _postProcess = AddToggle(_panel, new Vector2(ControlX, y), _settings.postProcess);
        y -= step;

        AddLabel("Lyrics in video", y);
        _lyrics = AddToggle(_panel, new Vector2(ControlX, y), _settings.lyrics);
        y -= step;

        _start = AddRowInput("Start (s)", ref y, step, _settings.startTime.ToString("0.##", CultureInfo.InvariantCulture));
        _end = AddRowInput("End (s) · 0 = full show", ref y, step, _settings.endTime.ToString("0.##", CultureInfo.InvariantCulture));
        _folder = AddRowInput("Output folder", ref y, step,
            string.IsNullOrWhiteSpace(_settings.outputFolder) ? LiveRecordSettings.DefaultFolder : _settings.outputFolder);
        _name = AddRowInput("File name", ref y, step, string.IsNullOrWhiteSpace(_settings.fileName) ? "Live" : _settings.fileName);

        _hint = AddText(_panel, "Hint", new Vector2(0f, -232f), new Vector2(820f, 46f), 18,
            new Color(0.75f, 0.8f, 0.85f, 1f), TextAnchor.MiddleCenter);
        _status = AddText(_panel, "Status", new Vector2(0f, -266f), new Vector2(820f, 30f), 21,
            new Color(1f, 0.55f, 0.45f, 1f), TextAnchor.MiddleCenter);
        _status.text = "";

        AddButton(_panel, "Record", new Vector2(-130f, -300f), new Vector2(230f, 50f),
            new Color(0.16f, 0.42f, 0.22f, 1f), Accept);
        AddButton(_panel, "Cancel", new Vector2(130f, -300f), new Vector2(230f, 50f),
            new Color(0.35f, 0.16f, 0.16f, 1f), Close);

        RefreshHint();
    }

    //======================================================================
    // Construcción de controles
    //======================================================================

    private InputField AddRowInput(string label, ref float y, float step, string value)
    {
        AddLabel(label, y);
        var field = AddInput(_panel, "Input", new Vector2(ControlX, y), new Vector2(420f, 32f), value);
        y -= step;
        field.onValueChanged.AddListener(_ => RefreshHint());
        return field;
    }

    private void AddLabel(string label, float y)
    {
        var text = AddText(_panel, "Label", new Vector2(-190f, y), new Vector2(380f, 32f), 22,
            new Color(0.85f, 0.88f, 0.92f, 1f), TextAnchor.MiddleLeft);
        text.text = label;
    }

    private void AddButton(Transform parent, string label, Vector2 position, Vector2 size, Color color,
                           Action onClick)
    {
        var rect = NewRect(label, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick());

        var text = AddText(rect, "Text", Vector2.zero, size, 22, Color.white, TextAnchor.MiddleCenter);
        text.text = label;
        text.raycastTarget = false;
    }

    private InputField AddInput(Transform parent, string name, Vector2 position, Vector2 size, string value)
    {
        var rect = NewRect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.17f, 0.20f, 1f);

        var textRect = NewRect("Text", rect);
        Stretch(textRect, 8f, 4f);
        var text = textRect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.fontSize = 20;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        //En un InputField de una sola línea el texto va recortado dentro del campo (y se desplaza al
        //editar); con 'Overflow' se saldría por encima del resto del menú.
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        var input = rect.gameObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.textComponent = text;
        input.lineType = InputField.LineType.SingleLine;
        input.text = value;
        return input;
    }

    //Barra de 50 a 200 % (números enteros) para el supermuestreo.
    private Slider AddSlider(Transform parent, Vector2 position, Vector2 size, float min, float max, float value)
    {
        var root = NewRect("Slider", parent);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = size;
        root.anchoredPosition = position;

        var background = NewRect("Background", root);
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.pivot = new Vector2(0.5f, 0.5f);
        background.anchoredPosition = Vector2.zero;
        background.sizeDelta = new Vector2(0f, 8f);
        background.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.17f, 0.20f, 1f);

        var fillArea = NewRect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.pivot = new Vector2(0.5f, 0.5f);
        fillArea.anchoredPosition = Vector2.zero;
        fillArea.sizeDelta = new Vector2(-22f, 8f);

        var fill = NewRect("Fill", fillArea);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0.5f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.sizeDelta = new Vector2(10f, 0f);
        fill.gameObject.AddComponent<Image>().color = new Color(0.24f, 0.56f, 0.87f, 1f);

        var handleArea = NewRect("Handle Slide Area", root);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.pivot = new Vector2(0.5f, 0.5f);
        handleArea.anchoredPosition = Vector2.zero;
        handleArea.sizeDelta = new Vector2(-22f, 0f);

        var handle = NewRect("Handle", handleArea);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(22f, 0f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = CircleSprite;
        handleImage.color = Color.white;

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = true;
        slider.value = Mathf.Clamp(value, min, max);
        return slider;
    }

    //Fila de opciones (antialiasing): todas visibles y la elegida resaltada.
    private Segmented AddSegmented(Transform parent, Vector2 position, Vector2 size, string[] labels,
                                   int[] values, int value, Action onChange)
    {
        var segmented = new Segmented { Values = values, Backgrounds = new Image[labels.Length] };

        float gap = 6f;
        float width = (size.x - gap * (labels.Length - 1)) / labels.Length;
        float left = position.x - size.x * 0.5f;

        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var rect = NewRect("Option " + labels[i], parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, size.y);
            rect.anchoredPosition = new Vector2(left + width * 0.5f + i * (width + gap), position.y);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = SegmentOff;
            segmented.Backgrounds[i] = image;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                segmented.Select(index);
                if (onChange != null) onChange();
            });

            var text = AddText(rect, "Text", Vector2.zero, new Vector2(width, size.y), 20, Color.white,
                TextAnchor.MiddleCenter);
            text.text = labels[i];
            text.raycastTarget = false;
        }

        segmented.Select(FindIndex(values, value));
        return segmented;
    }

    //Interruptor ON/OFF de una sola pieza (un botón de dos caras).
    private ToggleSwitch AddToggle(Transform parent, Vector2 position, bool isOn)
    {
        var rect = NewRect("Toggle", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(104f, 34f);
        rect.anchoredPosition = position;

        var background = rect.gameObject.AddComponent<Image>();
        background.sprite = PillSprite;
        background.type = Image.Type.Sliced;
        background.color = isOn ? ToggleOn : ToggleOff;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        var toggle = new ToggleSwitch
        {
            IsOn = isOn,
            Background = background,
            HalfTravel = 104f * 0.5f - 34f * 0.5f   //hasta pegado al borde
        };

        var knob = NewRect("Knob", rect);
        knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
        knob.pivot = new Vector2(0.5f, 0.5f);
        knob.sizeDelta = new Vector2(28f, 28f);
        var knobImage = knob.gameObject.AddComponent<Image>();
        knobImage.sprite = CircleSprite;
        knobImage.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        knobImage.raycastTarget = false;
        toggle.Knob = knob;

        var label = AddText(rect, "Label", Vector2.zero, new Vector2(52f, 34f), 20, Color.white,
            TextAnchor.MiddleCenter);
        label.raycastTarget = false;
        toggle.Label = label;

        button.onClick.AddListener(toggle.Toggle);
        toggle.Apply();
        return toggle;
    }

    //Desplegable estándar de uGUI montado por código: la plantilla necesita un ScrollRect con su
    //Content y, dentro, un item con Toggle (el propio Dropdown busca el primer Toggle del template).
    private Dropdown AddDropdown(Transform parent, Vector2 position, Vector2 size, string[] labels,
                                 int index, UnityAction<int> onChanged)
    {
        var rect = NewRect("Dropdown", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.17f, 0.20f, 1f);

        var dropdown = rect.gameObject.AddComponent<Dropdown>();
        dropdown.targetGraphic = image;

        var caption = AddText(rect, "Caption", Vector2.zero, size, 20, Color.white, TextAnchor.MiddleLeft);
        var captionRect = caption.rectTransform;
        captionRect.anchorMin = Vector2.zero;
        captionRect.anchorMax = Vector2.one;
        captionRect.offsetMin = new Vector2(8f, 0f);
        captionRect.offsetMax = new Vector2(-24f, 0f);
        dropdown.captionText = caption;

        var arrow = AddText(rect, "Arrow", Vector2.zero, new Vector2(20f, 20f), 18,
            new Color(0.85f, 0.85f, 0.85f, 1f), TextAnchor.MiddleCenter);
        arrow.text = "v";
        arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        arrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        arrow.rectTransform.anchoredPosition = new Vector2(-12f, 0f);

        //--- Plantilla de la lista ---
        var template = NewRect("Template", rect);
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = new Vector2(0f, -2f);
        template.sizeDelta = new Vector2(0f, 170f);
        template.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.11f, 0.14f, 0.99f);
        var scroll = template.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 24f;

        var viewport = NewRect("Viewport", template);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0f, 1f);
        viewport.anchoredPosition = Vector2.zero;
        viewport.sizeDelta = Vector2.zero;
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 30f);
        scroll.viewport = viewport;
        scroll.content = content;

        var item = NewRect("Item", content);
        item.anchorMin = new Vector2(0f, 0.5f);
        item.anchorMax = new Vector2(1f, 0.5f);
        item.pivot = new Vector2(0.5f, 0.5f);
        item.sizeDelta = new Vector2(0f, 30f);
        item.anchoredPosition = Vector2.zero;

        var itemBackground = NewRect("Item Background", item);
        Stretch(itemBackground);
        var backgroundImage = itemBackground.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.18f, 0.20f, 0.24f, 0.6f);

        var itemCheck = NewRect("Item Checkmark", item);
        itemCheck.anchorMin = itemCheck.anchorMax = new Vector2(0f, 0.5f);
        itemCheck.pivot = new Vector2(0.5f, 0.5f);
        itemCheck.sizeDelta = new Vector2(14f, 14f);
        itemCheck.anchoredPosition = new Vector2(12f, 0f);
        var checkImage = itemCheck.gameObject.AddComponent<Image>();
        checkImage.sprite = CircleSprite;
        checkImage.color = new Color(0.35f, 0.85f, 0.45f, 1f);

        var itemLabel = AddText(item, "Item Label", Vector2.zero, new Vector2(0f, 30f), 20, Color.white,
            TextAnchor.MiddleLeft);
        var itemLabelRect = itemLabel.rectTransform;
        itemLabelRect.anchorMin = Vector2.zero;
        itemLabelRect.anchorMax = Vector2.one;
        itemLabelRect.offsetMin = new Vector2(28f, 0f);
        itemLabelRect.offsetMax = new Vector2(-8f, 0f);
        itemLabel.supportRichText = false;

        var toggle = item.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkImage;
        toggle.isOn = true;

        dropdown.template = template;
        dropdown.itemText = itemLabel;
        dropdown.itemImage = checkImage;
        dropdown.options = new List<Dropdown.OptionData>();
        foreach (string label in labels) dropdown.options.Add(new Dropdown.OptionData(label));
        dropdown.value = Mathf.Clamp(index, 0, Mathf.Max(0, labels.Length - 1));
        dropdown.RefreshShownValue();
        if (onChanged != null) dropdown.onValueChanged.AddListener(onChanged);

        template.gameObject.SetActive(false);
        return dropdown;
    }

    private Text AddText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize,
                         Color color, TextAnchor anchor)
    {
        var rect = NewRect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var text = rect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect, float paddingX = 0f, float paddingY = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(paddingX, paddingY);
        rect.offsetMax = new Vector2(-paddingX, -paddingY);
    }

    //======================================================================
    // Sprites generados (píldora del interruptor y círculo)
    //======================================================================

    private static Sprite _pillSprite;
    private static Sprite _circleSprite;

    private static Sprite PillSprite
    {
        get
        {
            if (_pillSprite == null) _pillSprite = MakeRoundedSprite(48, 48, 14f, 14f);
            return _pillSprite;
        }
    }

    private static Sprite CircleSprite
    {
        get
        {
            if (_circleSprite == null) _circleSprite = MakeRoundedSprite(48, 48, 24f, 0f);
            return _circleSprite;
        }
    }

    //Rectángulo redondeado (con borde para 9-slice) o círculo, generado por código para no depender
    //de sprites del proyecto.
    private static Sprite MakeRoundedSprite(int size, int height, float radius, float border)
    {
        var texture = new Texture2D(size, height, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float coverage = RoundedCoverage(x + 0.5f, height - y - 0.5f, size, height, radius);
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        var vectorBorder = border > 0f
            ? new Vector4(border, border, border, border)
            : Vector4.zero;
        return Sprite.Create(texture, new Rect(0f, 0f, size, height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, vectorBorder);
    }

    //Cobertura (0..1) de un rectángulo redondeado, con ~1 px de suavizado en el borde.
    private static float RoundedCoverage(float x, float y, float width, float height, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(width, height) * 0.5f);
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        float qx = Mathf.Abs(x - halfW) - (halfW - radius);
        float qy = Mathf.Abs(y - halfH) - (halfH - radius);
        float distance;
        if (qx > 0f && qy > 0f) distance = Mathf.Sqrt(qx * qx + qy * qy) - radius;
        else distance = Mathf.Max(qx, qy) - radius;

        return Mathf.Clamp01(0.5f - distance);
    }

    //======================================================================
    // Lectura de valores y aceptación
    //======================================================================

    private Vector2Int CurrentResolution =>
        ResolutionValues[Mathf.Clamp(_resolution != null ? _resolution.value : 2, 0, ResolutionValues.Length - 1)];

    private static int FindResolutionIndex(LiveRecordSettings settings)
    {
        for (int i = 0; i < ResolutionValues.Length; i++)
            if (ResolutionValues[i].x == settings.width && ResolutionValues[i].y == settings.height) return i;
        return 2;   //1080p por defecto
    }

    private static int FindIndex(int[] values, int value)
    {
        for (int i = 0; i < values.Length; i++)
            if (values[i] == value) return i;
        return 0;
    }

    private void RefreshHint()
    {
        if (_hint == null) return;
        int fps = ParseInt(_fps, 60);
        int ss = _superSample != null ? Mathf.RoundToInt(_superSample.value) : 100;
        Vector2Int resolution = CurrentResolution;
        string name = string.IsNullOrWhiteSpace(_name.text) ? "Live" : _name.text.Trim();
        string folder = string.IsNullOrWhiteSpace(_folder.text) ? LiveRecordSettings.DefaultFolder : _folder.text.Trim();

        //La resolución de render es la de salida por el factor del supermuestreo.
        int renderX = Mathf.Max(16, Mathf.RoundToInt(resolution.x * (ss / 100f)));
        int renderY = Mathf.Max(16, Mathf.RoundToInt(resolution.y * (ss / 100f)));

        _hint.text = $"Rendering {renderX}x{renderY}  →  {resolution.x}x{resolution.y} @ {fps} fps\n" +
                     ShortenPath(System.IO.Path.Combine(folder, name + ".mp4"), HintMaxChars);
    }

    //Recorta una ruta por el medio para que quepa en el ancho del panel sin salirse.
    private static string ShortenPath(string path, int maxChars)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxChars) return path;
        int tail = Mathf.Max(8, maxChars - 6);
        return path.Substring(0, 3) + "…" + path.Substring(path.Length - tail);
    }

    //Recorta un texto cualquiera (avisos de error) para que no se salga del panel.
    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars) return text;
        return text.Substring(0, maxChars - 1) + "…";
    }

    private void Accept()
    {
        try
        {
            Vector2Int resolution = CurrentResolution;
            var settings = new LiveRecordSettings
            {
                fps = ParseInt(_fps, 60),
                width = resolution.x,
                height = resolution.y,
                superSamplePercent = _superSample != null ? Mathf.RoundToInt(_superSample.value) : 100,
                msaa = _antiAliasing != null ? _antiAliasing.Current : 1,
                postProcess = _postProcess != null && _postProcess.IsOn,
                lyrics = _lyrics != null && _lyrics.IsOn,
                startTime = ParseFloat(_start, 0f),
                endTime = ParseFloat(_end, 0f),
                outputFolder = _folder.text.Trim(),
                fileName = _name.text.Trim()
            };
            settings.Validate();

            var accept = _onAccept;
            _onAccept = null;
            Close();
            accept?.Invoke(settings);
        }
        catch (Exception e)
        {
            _status.text = Truncate(e.Message, 90);
        }
    }

    private static int ParseInt(InputField field, int fallback)
    {
        if (field == null) return fallback;
        return int.TryParse(field.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value : fallback;
    }

    private static float ParseFloat(InputField field, float fallback)
    {
        if (field == null) return fallback;
        return float.TryParse(field.text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value : fallback;
    }
}
