using System;
using System.Collections.Generic;
using System.Globalization;
using SFB;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

//(Grabación del live) Menú de ajustes de grabación.
//
//El panel vive en Version2.unity como objeto INACTIVO (LiveRecordPanelUI), para poder retocarlo en el
//Editor sin tocar código. Open() lo activa y lo conecta con la lógica por nombre (Bind()); si no se
//encontrara o no se pudiera conectar, se construye por código como respaldo.
//Aparece al pulsar RECORD en el panel de selección del show y no arranca nada hasta que se acepta:
//entonces entrega un LiveRecordSettings y se cierra.
public class LiveRecordPanel : MonoBehaviour
{
    public static LiveRecordPanel Instance;

    //Nombre del objeto con el panel en la escena (se guarda inactivo y se activa al abrirlo).
    public const string SceneObjectName = "LiveRecordPanelUI";

    //Serializado: marca que este panel vive en la escena (se oculta en vez de destruirse).
    [SerializeField] private bool _baked;
    private static bool _opening;
    private readonly List<Dropdown> _ownDropdowns = new List<Dropdown>();

    //Estado de las listas de los desplegables del panel (ver EnforceSingleDropdown).
    private readonly Dictionary<Dropdown, bool> _listWasOpen = new Dictionary<Dropdown, bool>();
    private readonly HashSet<Dropdown> _hideRequested = new HashSet<Dropdown>();
    private Dropdown _lastOpened;

    private LiveRecordSettings _settings = new LiveRecordSettings();
    private Action<LiveRecordSettings> _onAccept;
    private string _songLabel;

    //Controles del formulario.
    private InputField _fps, _start, _end, _folder, _name;
    private Dropdown _fpsPresets;
    private bool _syncingFps;
    private Dropdown _resolution;
    private Slider _superSample;
    private Text _superSampleValue;
    private Segmented _antiAliasing;
    private Segmented _quality;
    private ToggleSwitch _postProcess, _lyrics;
    private Text _status;
    private Text _hint;
    private Text _maxNotice;

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

    //Presets de FPS. "Custom" (valor 0) deja el número que se haya escrito a mano.
    private static readonly string[] FpsPresetLabels = { "30", "60", "90", "120", "240", "Custom" };
    private static readonly int[] FpsPresetValues = { 30, 60, 90, 120, 240, 0 };

    //Niveles de calidad (0 = Low, 1 = Normal, 2 = High, 3 = Max).
    //  Low    · H.264 4:2:0 8 bits, rápida y ligera
    //  Normal · H.264 4:2:0 8 bits, buena calidad
    //  High   · HEVC 4:4:4 8 bits (croma completa, sin máster)
    //  Max    · HEVC 4:4:4 10 bits + máster FFV1 sin pérdida
    private static readonly string[] QualityLabels = { "Low", "Normal", "High", "Max" };
    private static readonly int[] QualityValues = { 0, 1, 2, 3 };

    //Aviso que se muestra sólo con la calidad Máx (HEVC 4:4:4 de 10 bits).
    private const string MaxQualityNotice =
        "4:4:4 + 10-bit: some players may not open it — VLC (or similar) recommended.";

    //Orden de canvas del panel: por encima de todo lo que usa la app (UI del visor 0-5, overlays del
    //live 30000-32000, listas de desplegables 30000) y por debajo del límite de 32767.
    public const int PanelSortingOrder = 32760;
    //Las listas de los desplegables del propio panel se suben aquí para quedar sobre él.
    private const int PanelDropdownSortingOrder = 32766;

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

        //Cierra cualquier desplegable abierto (su lista y su "bloqueador" se pintan en canvases propios
        //por encima de casi todo) y quita el foco del EventSystem, para que las teclas no sigan
        //manejando un control de detrás.
        var openDropdowns = Resources.FindObjectsOfTypeAll<Dropdown>();
        for (int i = 0; i < openDropdowns.Length; i++)
            if (openDropdowns[i] != null) openDropdowns[i].Hide();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        //1) Panel guardado en la escena (inactivo): se activa y se conecta con la lógica.
        var baked = FindScenePanel();
        if (baked != null)
        {
            baked._settings = defaults != null ? defaults.Clone() : new LiveRecordSettings();
            baked._onAccept = onAccept;
            baked._songLabel = songLabel;

            _opening = true;
            try { baked.gameObject.SetActive(true); }
            finally { _opening = false; }

            if (baked.Bind())
            {
                baked._baked = true;
                Instance = baked;
                return baked;
            }

            //Si el panel de la escena no se pudiera conectar, se oculta y se construye uno nuevo: nunca
            //se queda sin menú.
            baked.gameObject.SetActive(false);
            Debug.LogWarning("[LiveRecordPanel] The scene panel could not be bound; building it at runtime.");
        }

        //2) Respaldo: construcción por código.
        var go = new GameObject("LiveRecordPanel",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var panel = go.AddComponent<LiveRecordPanel>();
        panel._settings = defaults != null ? defaults.Clone() : new LiveRecordSettings();
        panel._onAccept = onAccept;
        panel._songLabel = songLabel;

        //Si construir el panel fallara, no debe quedarse un objeto a medias en la escena (su "Dim"
        //taparía la pantalla y el siguiente panel saldría oscurecido).
        try { panel.Build(); }
        catch
        {
            Destroy(go);
            throw;
        }

        Instance = panel;
        return panel;
    }

    //El panel vive en la escena (Version2.unity) como objeto inactivo; aquí se busca para reutilizarlo.
    private static LiveRecordPanel FindScenePanel()
    {
        var all = Resources.FindObjectsOfTypeAll<LiveRecordPanel>();
        for (int i = 0; i < all.Length; i++)
        {
            var panel = all[i];
            if (panel == null) continue;
            if (!panel.gameObject.scene.IsValid()) continue;
            if (panel.gameObject.name != SceneObjectName) continue;
            return panel;
        }
        return null;
    }

    public static void Close()
    {
        var panel = Instance;
        Instance = null;
        if (panel == null) return;

        //El panel de la escena se OCULTA (no se destruye); el de respaldo se destruye.
        if (panel._baked) panel.gameObject.SetActive(false);
        else Destroy(panel.gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    //======================================================================
    // Conexión del panel de la escena con la lógica
    //======================================================================

    //Busca sus controles por nombre, reconstruye los ayudantes (Segmented/ToggleSwitch) y cablea los
    //eventos. No crea nada: el objeto ya existe en la escena.
    private bool Bind()
    {
        if (_settings == null) _settings = new LiveRecordSettings();

        var win = transform.Find("Panel") as RectTransform;
        if (win == null) return false;
        _panel = win;

        var canvas = GetComponent<Canvas>();
        if (canvas != null) canvas.sortingOrder = PanelSortingOrder;

        var titleText = FindText(win, "Title");
        if (titleText != null) titleText.text = "RECORD LIVE  ·  " + _songLabel;

        //FPS: combo (campo + flecha + lista de presets).
        _fps = FindInput(win, "FPS/Field");
        _fpsPresets = FindDropdown(win, "FPS/Dropdown");
        var fpsArrow = win.Find("FPS/Arrow") != null ? win.Find("FPS/Arrow").GetComponent<Button>() : null;
        if (_fps != null)
        {
            _fps.onValueChanged.RemoveAllListeners();
            _fps.text = _settings.fps.ToString(CultureInfo.InvariantCulture);
            _fps.onValueChanged.AddListener(_ => { SyncFpsPreset(); RefreshHint(); });
        }
        if (fpsArrow != null) fpsArrow.onClick.RemoveAllListeners();
        if (fpsArrow != null && _fpsPresets != null) fpsArrow.onClick.AddListener(() => _fpsPresets.Show());
        if (_fpsPresets != null)
        {
            _fpsPresets.onValueChanged.RemoveAllListeners();
            _fpsPresets.onValueChanged.AddListener(OnFpsPresetChanged);
        }
        SyncFpsPreset();

        //Resto de controles.
        _resolution = FindDropdown(win, "Dropdown");
        if (_resolution != null)
        {
            _resolution.onValueChanged.RemoveAllListeners();
            _resolution.SetValueWithoutNotify(FindResolutionIndex(_settings));
            _resolution.RefreshShownValue();
            _resolution.onValueChanged.AddListener(_ => RefreshHint());
        }

        _superSample = win.Find("Slider") != null ? win.Find("Slider").GetComponent<Slider>() : null;
        _superSampleValue = FindText(win, "SSValue");
        if (_superSample != null)
        {
            _superSample.onValueChanged.RemoveAllListeners();
            _superSample.value = Mathf.Clamp(_settings.superSamplePercent, SuperSampleMin, SuperSampleMax);
            if (_superSampleValue != null) _superSampleValue.text = ((int)_superSample.value) + " %";
            _superSample.onValueChanged.AddListener(value =>
            {
                if (_superSampleValue != null) _superSampleValue.text = ((int)value) + " %";
                RefreshHint();
            });
        }

        _antiAliasing = BindSegmented("Msaa_", MsaaValues, () => RefreshHint());
        _quality = BindSegmented("Quality_", QualityValues, () => RefreshHint());
        _postProcess = BindToggle("PostToggle", _settings.postProcess);
        _lyrics = BindToggle("LyricsToggle", _settings.lyrics);

        _start = BindInput(win, "StartInput", _settings.startTime.ToString("0.##", CultureInfo.InvariantCulture));
        _end = BindInput(win, "EndInput", _settings.endTime.ToString("0.##", CultureInfo.InvariantCulture));
        _folder = BindInput(win, "OutputFolder", string.IsNullOrWhiteSpace(_settings.outputFolder)
            ? LiveRecordSettings.DefaultFolder : _settings.outputFolder);
        _name = BindInput(win, "NameInput", string.IsNullOrWhiteSpace(_settings.fileName) ? "Live" : _settings.fileName);

        _hint = FindText(win, "Hint");
        _status = FindText(win, "Status");
        if (_status != null) _status.text = "";
        _maxNotice = FindText(win, "MaxNotice");
        if (_maxNotice != null) _maxNotice.text = MaxQualityNotice;

        var browse = win.Find("BrowseButton") != null ? win.Find("BrowseButton").GetComponent<Button>() : null;
        if (browse != null) { browse.onClick.RemoveAllListeners(); browse.onClick.AddListener(BrowseOutputFolder); }
        var record = win.Find("Record") != null ? win.Find("Record").GetComponent<Button>() : null;
        if (record != null) { record.onClick.RemoveAllListeners(); record.onClick.AddListener(Accept); }
        var cancel = win.Find("Cancel") != null ? win.Find("Cancel").GetComponent<Button>() : null;
        if (cancel != null) { cancel.onClick.RemoveAllListeners(); cancel.onClick.AddListener(Close); }

        //Valores iniciales de los segmentados e interruptores.
        SelectSegmented(_antiAliasing, MsaaValues, _settings.msaa);
        SelectSegmented(_quality, QualityValues, _settings.qualityLevel);

        //Desplegables cuyas listas hay que subir por encima del panel.
        _ownDropdowns.Clear();
        if (_fpsPresets != null) _ownDropdowns.Add(_fpsPresets);
        if (_resolution != null) _ownDropdowns.Add(_resolution);

        //Estado limpio de listas y, si se cerró el panel con una lista abierta, se destruye esa lista:
        //uGUI guarda una referencia a ella y, mientras exista, Show() no vuelve a abrirla (la flecha
        //dejaría de responder).
        _listWasOpen.Clear();
        _hideRequested.Clear();
        _lastOpened = null;
        for (int i = 0; i < _ownDropdowns.Count; i++)
        {
            var dropdown = _ownDropdowns[i];
            if (dropdown != null && dropdown.transform.Find("Dropdown List") != null) dropdown.Hide();
        }

        AttachClickAway(transform.Find("Dim"));
        AttachClickAway(win);

        RefreshHint();
        return _fps != null && _resolution != null && _quality != null;
    }

    private static Text FindText(Transform root, string path)
    {
        var t = root.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    private static InputField FindInput(Transform root, string path)
    {
        var t = root.Find(path);
        return t != null ? t.GetComponent<InputField>() : null;
    }

    private static Dropdown FindDropdown(Transform root, string path)
    {
        var t = root.Find(path);
        return t != null ? t.GetComponent<Dropdown>() : null;
    }

    private InputField BindInput(Transform root, string name, string value)
    {
        var field = FindInput(root, name);
        if (field == null) return null;
        field.onValueChanged.RemoveAllListeners();
        field.text = value;
        field.onValueChanged.AddListener(_ => RefreshHint());
        return field;
    }

    private static void SelectSegmented(Segmented segmented, int[] values, int value)
    {
        if (segmented == null) return;
        for (int i = 0; i < values.Length; i++)
            if (values[i] == value) { segmented.Select(i); return; }
    }

    //Reconstruye un control segmentado a partir de sus botones ("Option <prefijo><etiqueta>").
    private Segmented BindSegmented(string prefix, int[] values, Action onChange)
    {
        var segmented = new Segmented { Values = values, Backgrounds = new Image[values.Length] };
        var found = new List<Transform>();
        foreach (Transform child in _panel)
            if (child.name.StartsWith("Option " + prefix, StringComparison.Ordinal)) found.Add(child);

        found.Sort((a, b) => ((RectTransform)a).anchoredPosition.x.CompareTo(((RectTransform)b).anchoredPosition.x));
        for (int i = 0; i < found.Count && i < values.Length; i++)
        {
            int index = i;
            segmented.Backgrounds[i] = found[i].GetComponent<Image>();
            var button = found[i].GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => { segmented.Select(index); if (onChange != null) onChange(); });
            }
        }
        return segmented;
    }

    //Reconstruye un interruptor ON/OFF a partir de su objeto ("<nombre>/Knob", "<nombre>/Label").
    private ToggleSwitch BindToggle(string name, bool isOn)
    {
        var rect = _panel.Find(name) as RectTransform;
        if (rect == null) return null;

        var toggle = new ToggleSwitch
        {
            IsOn = isOn,
            Background = rect.GetComponent<Image>(),
            Knob = rect.Find("Knob") as RectTransform,
            Label = rect.Find("Label") != null ? rect.Find("Label").GetComponent<Text>() : null,
            HalfTravel = 104f * 0.5f - 34f * 0.5f
        };

        var button = rect.GetComponent<Button>();
        if (button != null) { button.onClick.RemoveAllListeners(); button.onClick.AddListener(toggle.Toggle); }
        toggle.Apply();
        return toggle;
    }

    //Las listas de los desplegables del panel se crean en un canvas propio a orden 30000, por DEBAJO del
    //panel (32760): aquí se suben para que se vean.
    private void LateUpdate()
    {
        for (int i = 0; i < _ownDropdowns.Count; i++)
        {
            var dropdown = _ownDropdowns[i];
            var list = dropdown != null ? dropdown.transform.Find("Dropdown List") : null;
            if (list == null) continue;
            var canvas = list.GetComponent<Canvas>();
            if (canvas != null && canvas.sortingOrder != PanelDropdownSortingOrder)
                canvas.sortingOrder = PanelDropdownSortingOrder;
        }

        EnforceSingleDropdown();
    }

    //Solo puede haber UNA lista abierta: al abrir otra, se cierra la anterior. (El "blocker" que uGUI
    //crea para cerrar al pulsar fuera se pinta en un canvas de orden inferior a este panel, así que sus
    //clics no llegan a la ventana; de ahí que hubiera que devolver ese comportamiento a mano, ver
    //AttachClickAway.)
    private void EnforceSingleDropdown()
    {
        if (_ownDropdowns.Count == 0) return;

        for (int i = 0; i < _ownDropdowns.Count; i++)
        {
            var dropdown = _ownDropdowns[i];
            if (dropdown == null) continue;

            bool open = IsListOpen(dropdown);
            bool wasOpen;
            _listWasOpen.TryGetValue(dropdown, out wasOpen);

            if (!open)
            {
                _listWasOpen[dropdown] = false;
                _hideRequested.Remove(dropdown);
                continue;
            }

            //Acaba de abrirse (y no es una que esté cerrándose): pasa a ser la lista visible.
            if (!wasOpen && !_hideRequested.Contains(dropdown)) _lastOpened = dropdown;
            _listWasOpen[dropdown] = true;

            if (_lastOpened != null && _lastOpened != dropdown && !_hideRequested.Contains(dropdown))
            {
                _hideRequested.Add(dropdown);
                dropdown.Hide();
            }
        }
    }

    private static bool IsListOpen(Dropdown dropdown)
    {
        var list = dropdown.transform.Find("Dropdown List");
        return list != null && list.gameObject.activeInHierarchy;
    }

    //Cierra todas las listas del panel (se usa al pulsar fuera de ellas).
    private void HideOwnDropdowns()
    {
        for (int i = 0; i < _ownDropdowns.Count; i++)
        {
            var dropdown = _ownDropdowns[i];
            if (dropdown == null || !IsListOpen(dropdown)) continue;
            if (_hideRequested.Contains(dropdown)) continue;
            _hideRequested.Add(dropdown);
            dropdown.Hide();
        }
    }

    //Devuelve a mano el "cerrar al pulsar fuera" que uGUI hace con su blocker: el blocker de una lista
    //abierta se pinta por debajo de este panel, así que un clic en la ventana no lo cerraría. Se pone en
    //el fondo oscuro y en el fondo de la ventana (los hijos con gráfico propio reciben antes el clic).
    private void AttachClickAway(Transform target)
    {
        if (target == null) return;

        var trigger = target.GetComponent<EventTrigger>();
        if (trigger == null) trigger = target.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entry.callback.AddListener(_ => HideOwnDropdowns());
        trigger.triggers.Add(entry);
    }

    private void Awake()
    {
        _font = UiFont.Get();

        //Si el panel de la escena arranca ACTIVO (p. ej. la escena se guardó con el menú abierto), se
        //oculta: sólo debe mostrarse a través de Open(), que es quien lo conecta con la lógica.
        if (_baked && !_opening) gameObject.SetActive(false);
    }

    private void Build()
    {
        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        //El panel va POR ENCIMA DE TODO lo que usa la app: la UI del visor (0-5), los overlays del live
        //(30000/31000/32000) y las listas de los desplegables de uGUI (30000). Así ninguna ventana de
        //detrás puede pintarse ni recibir clics por encima. Sus propios desplegables se suben aún más
        //(ver LateUpdate). El límite del campo es 32767.
        canvas.sortingOrder = PanelSortingOrder;

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
        _panel.sizeDelta = new Vector2(860f, 760f);
        var panelImage = _panel.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.09f, 0.10f, 0.12f, 0.98f);
        panelImage.raycastTarget = true;

        var title = AddText(_panel, "Title", new Vector2(0f, 292f), new Vector2(820f, 40f), 30,
            new Color(1f, 0.87f, 0.35f, 1f), TextAnchor.MiddleCenter);
        title.text = "RECORD LIVE  ·  " + _songLabel;

        float y = 235f;
        const float step = 40f;

        //FPS: un único elemento que sirve para escribir un valor a mano y para elegir uno de los
        //habituales con la flecha (ver AddFpsCombo).
        AddLabel("FPS", y);
        _fps = AddFpsCombo(y);
        y -= step;

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
            MsaaLabels, MsaaValues, _settings.msaa, "Msaa_", () => RefreshHint());
        y -= step;

        AddLabel("Post effects", y);
        _postProcess = AddToggle(_panel, "PostToggle", new Vector2(ControlX, y), _settings.postProcess);
        y -= step;

        AddLabel("Lyrics in video", y);
        _lyrics = AddToggle(_panel, "LyricsToggle", new Vector2(ControlX, y), _settings.lyrics);
        y -= step;

        _start = AddRowInput("Start (s)", "StartInput", ref y, step, _settings.startTime.ToString("0.##", CultureInfo.InvariantCulture));
        _end = AddRowInput("End (s) · 0 = full show", "EndInput", ref y, step, _settings.endTime.ToString("0.##", CultureInfo.InvariantCulture));

        //Carpeta de salida: campo (alineado con el resto) + botón que abre el explorador del sistema.
        AddLabel("Output folder", y);
        _folder = AddInput(_panel, "OutputFolder", new Vector2(30f, y), new Vector2(260f, 32f),
            string.IsNullOrWhiteSpace(_settings.outputFolder) ? LiveRecordSettings.DefaultFolder : _settings.outputFolder);
        _folder.onValueChanged.AddListener(_ => RefreshHint());
        AddButton(_panel, "BrowseButton", "Browse…", new Vector2(250f, y), new Vector2(140f, 32f),
            new Color(0.20f, 0.30f, 0.45f, 1f), BrowseOutputFolder);
        y -= step;

        _name = AddRowInput("File name", "NameInput", ref y, step, string.IsNullOrWhiteSpace(_settings.fileName) ? "Live" : _settings.fileName);

        AddLabel("Video quality", y);
        _quality = AddSegmented(_panel, new Vector2(ControlX, y), new Vector2(420f, 32f),
            QualityLabels, QualityValues, _settings.qualityLevel, "Quality_", () => RefreshHint());
        y -= step;

        //Resumen (dos líneas: el texto puede partirse) y avisos.
        _hint = AddText(_panel, "Hint", new Vector2(0f, -232f), new Vector2(800f, 46f), 18,
            new Color(0.75f, 0.8f, 0.85f, 1f), TextAnchor.MiddleCenter);
        _hint.horizontalOverflow = HorizontalWrapMode.Wrap;

        //Aviso que sólo aparece con la calidad Máx (4:4:4 de 10 bits).
        _maxNotice = AddText(_panel, "MaxNotice", new Vector2(0f, -270f), new Vector2(800f, 24f), 17,
            new Color(1f, 0.72f, 0.35f, 1f), TextAnchor.MiddleCenter);
        _maxNotice.text = MaxQualityNotice;

        _status = AddText(_panel, "Status", new Vector2(0f, -298f), new Vector2(800f, 26f), 20,
            new Color(1f, 0.55f, 0.45f, 1f), TextAnchor.MiddleCenter);
        _status.text = "";

        AddButton(_panel, "Record", "Record", new Vector2(-130f, -338f), new Vector2(230f, 52f),
            new Color(0.16f, 0.42f, 0.22f, 1f), Accept);
        AddButton(_panel, "Cancel", "Cancel", new Vector2(130f, -338f), new Vector2(230f, 52f),
            new Color(0.35f, 0.16f, 0.16f, 1f), Close);

        //Desplegables cuyas listas hay que subir por encima del panel (ver LateUpdate).
        _ownDropdowns.Clear();
        if (_fpsPresets != null) _ownDropdowns.Add(_fpsPresets);
        if (_resolution != null) _ownDropdowns.Add(_resolution);

        //Cerrar al pulsar fuera (ver AttachClickAway).
        _listWasOpen.Clear();
        _hideRequested.Clear();
        _lastOpened = null;
        AttachClickAway(transform.Find("Dim"));
        AttachClickAway(_panel);

        RefreshHint();
    }

    //======================================================================
    // Construcción de controles
    //======================================================================

    private InputField AddRowInput(string label, string name, ref float y, float step, string value)
    {
        AddLabel(label, y);
        var field = AddInput(_panel, name, new Vector2(ControlX, y), new Vector2(420f, 32f), value);
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

    private void AddButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color,
                           Action onClick)
    {
        var rect = NewRect(name, parent);
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

        var input = rect.gameObject.AddComponent<LiveRecordTextField>();
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

    //Fila de opciones (antialiasing, calidad): todas visibles y la elegida resaltada.
    //'prefix' va en el nombre de cada botón ("Option Msaa_Off"), para poder re-vincularlos al abrir el
    //panel horneado en la escena.
    private Segmented AddSegmented(Transform parent, Vector2 position, Vector2 size, string[] labels,
                                   int[] values, int value, string prefix, Action onChange)
    {
        var segmented = new Segmented { Values = values, Backgrounds = new Image[labels.Length] };

        float gap = 6f;
        float width = (size.x - gap * (labels.Length - 1)) / labels.Length;
        float left = position.x - size.x * 0.5f;

        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var rect = NewRect("Option " + prefix + labels[i], parent);
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
    private ToggleSwitch AddToggle(Transform parent, string name, Vector2 position, bool isOn)
    {
        var rect = NewRect(name, parent);
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

    //Combo de FPS: un solo elemento con el mismo aspecto que los demás campos. Se puede escribir un
    //valor a mano y, con la flecha de la derecha, elegir uno de los habituales.
    //
    //Se reutiliza el Dropdown de uGUI solo por su lista: su parte visible se anula (sin gráfico que
    //reciba clics) y la caja la dibuja el combo. La flecha llama a 'Show()', y la lista aparece justo
    //debajo porque el desplegable ocupa exactamente el mismo rectángulo que la caja.
    private InputField AddFpsCombo(float y)
    {
        //Contenedor (sin gráfico propio: el fondo lo pone el objeto del propio campo).
        var box = NewRect("FPS", _panel);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.sizeDelta = new Vector2(420f, 32f);
        box.anchoredPosition = new Vector2(ControlX, y);

        //Objeto del campo: fondo + InputField (un solo Graphic por GameObject, como en AddInput).
        var fieldRect = NewRect("Field", box);
        fieldRect.anchorMin = Vector2.zero;
        fieldRect.anchorMax = Vector2.one;
        fieldRect.offsetMin = Vector2.zero;
        fieldRect.offsetMax = new Vector2(-30f, 0f);

        var background = fieldRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.16f, 0.17f, 0.20f);

        var textRect = NewRect("Text", fieldRect);
        Stretch(textRect, 8f, 4f);
        var text = textRect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.fontSize = 20;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        var input = fieldRect.gameObject.AddComponent<LiveRecordTextField>();
        input.targetGraphic = background;
        input.textComponent = text;
        input.lineType = InputField.LineType.SingleLine;
        input.text = _settings.fps.ToString(CultureInfo.InvariantCulture);
        input.onValueChanged.AddListener(_ => { SyncFpsPreset(); RefreshHint(); });

        //Flecha: abre la lista.
        var arrow = NewRect("Arrow", box);
        arrow.anchorMin = new Vector2(1f, 0f);
        arrow.anchorMax = new Vector2(1f, 1f);
        arrow.pivot = new Vector2(1f, 0.5f);
        arrow.sizeDelta = new Vector2(30f, 0f);
        arrow.anchoredPosition = Vector2.zero;

        var arrowImage = arrow.gameObject.AddComponent<Image>();
        arrowImage.color = new Color(0.20f, 0.21f, 0.25f, 1f);

        var glyph = AddText(arrow, "Glyph", Vector2.zero, new Vector2(30f, 32f), 18,
            new Color(0.85f, 0.85f, 0.85f, 1f), TextAnchor.MiddleCenter);
        glyph.text = "v";

        //Lista de presets (el desplegable de uGUI, sin su parte visible: no debe interceptar clics).
        _fpsPresets = AddDropdown(box, Vector2.zero, new Vector2(420f, 32f),
            FpsPresetLabels, FpsPresetLabels.Length - 1, OnFpsPresetChanged);
        var dropdownImage = _fpsPresets.GetComponent<Image>();
        if (dropdownImage != null)
        {
            //El desplegable solo aporta la lista: su fondo debe ser INVISIBLE, porque se dibuja encima
            //del campo (mismo rectángulo) y taparía el texto que se escribe y la flecha.
            dropdownImage.color = new Color(0f, 0f, 0f, 0f);
            dropdownImage.raycastTarget = false;
        }
        if (_fpsPresets.captionText != null) _fpsPresets.captionText.gameObject.SetActive(false);
        var dropdownArrow = _fpsPresets.transform.Find("Arrow");
        if (dropdownArrow != null) dropdownArrow.gameObject.SetActive(false);

        var button = arrow.gameObject.AddComponent<Button>();
        button.targetGraphic = arrowImage;
        button.onClick.AddListener(() => { if (_fpsPresets != null) _fpsPresets.Show(); });

        SyncFpsPreset();
        return input;
    }

    //Desplegable de FPS: escribe el preset en el campo. "Custom" no toca lo escrito.
    private void OnFpsPresetChanged(int index)
    {
        if (_syncingFps) return;
        int value = FpsPresetValues[Mathf.Clamp(index, 0, FpsPresetValues.Length - 1)];
        if (value <= 0) return;
        if (_fps != null) _fps.text = value.ToString(CultureInfo.InvariantCulture);
        RefreshHint();
    }

    //Pone el desplegable en el preset que coincida con el campo (o "Custom" si es otro valor).
    private void SyncFpsPreset()
    {
        if (_fpsPresets == null) return;
        int fps = ParseInt(_fps, 60);
        int index = FpsPresetValues.Length - 1;
        for (int i = 0; i < FpsPresetValues.Length; i++)
            if (FpsPresetValues[i] > 0 && FpsPresetValues[i] == fps) { index = i; break; }

        _syncingFps = true;
        _fpsPresets.value = index;
        _fpsPresets.RefreshShownValue();
        _syncingFps = false;
    }

    //Abre el explorador de carpetas del sistema para elegir la carpeta de salida.
    private void BrowseOutputFolder()
    {
        try
        {
            string current = _folder != null && !string.IsNullOrWhiteSpace(_folder.text)
                ? _folder.text.Trim()
                : LiveRecordSettings.DefaultFolder;

            string[] picked = StandaloneFileBrowser.OpenFolderPanel("Select output folder", current, false);
            if (picked != null && picked.Length > 0 && !string.IsNullOrWhiteSpace(picked[0]))
            {
                _folder.text = picked[0];
                RefreshHint();
            }
        }
        catch (Exception e)
        {
            if (_status != null) _status.text = Truncate("Folder picker: " + e.Message, 90);
        }
    }

    private void RefreshHint()
    {
        if (_hint == null) return;
        int fps = ParseInt(_fps, 60);
        int ss = _superSample != null ? Mathf.RoundToInt(_superSample.value) : 100;
        Vector2Int resolution = CurrentResolution;

        //La resolución de render es la de salida por el factor del supermuestreo.
        int renderX = Mathf.Max(16, Mathf.RoundToInt(resolution.x * (ss / 100f)));
        int renderY = Mathf.Max(16, Mathf.RoundToInt(resolution.y * (ss / 100f)));

        //Descripción del nivel elegido.
        int quality = _quality != null ? _quality.Current : 1;
        string level = quality >= 3
            ? "Max · HEVC 4:4:4 10-bit + FFV1 master (~28 GB/min)"
            : (quality >= 2
                ? "High · HEVC 4:4:4 8-bit"
                : (quality >= 1 ? "Normal · H.264 4:2:0" : "Low · H.264 4:2:0 (fast)"));

        _hint.text = $"Rendering {renderX}x{renderY}  →  {resolution.x}x{resolution.y} @ {fps} fps\n" +
                     level;

        //Con la calidad Máx se avisa de que el 4:4:4 de 10 bits no lo abren todos los reproductores.
        if (_maxNotice != null) _maxNotice.gameObject.SetActive(quality >= 3);

        //Aviso medido: el MSAA encarece muchísimo la lectura de píxeles (el "resolve" de la superficie
        //con MSAA se paga al leer el frame). Con supermuestreo ya hay antialiasing por muestreo:
        //medido a 4K con supermuestreo 200 %, MSAA x2 → 32 ms de lectura; MSAA x8 → ~410 ms.
        int msaa = _antiAliasing != null ? _antiAliasing.Current : 1;
        if (msaa > 1 && ss > 100)
            _status.text = $"MSAA x{msaa} + supersampling: capture is ~13x slower. MSAA Off is recommended.";
        else if (_status.text != null && _status.text.StartsWith("MSAA x", StringComparison.Ordinal))
            _status.text = "";
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
                qualityLevel = _quality != null ? _quality.Current : 1,
                //El nivel Max incluye el máster FFV1 sin pérdida; los demás, no.
                masterMode = _quality != null && _quality.Current >= 3,
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
