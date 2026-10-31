using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallop.Live;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Overlay de depuración con los SLOTS DE VOZ del live.
//
// QUÉ ES
// ------
// Un recuadro en la esquina superior IZQUIERDA, apagado por defecto (Ctrl+F8), con una FILA por
// slot de voz (los mismos que monta el live desde el bloque `VocalSelect`). Cada fila lleva:
//   · rótulo: "Slot N: <PJ> (<columna del _part>)"  y, si la tabla pide una onda que ese PJ no
//     tiene en su AWB, un aviso "[wave X/Y: no audio]";
//   · espectro de audio de esa pista (barras, frecuencia en escala log), sacado con
//     `AudioSource.GetSpectrumData` de la fuente que ese slot está sonando.
//
// CLIC PARA MUTEAR
// ----------------
// Cada fila es clicable: un clic mutea esa pista (todas sus fuentes, vía `AudioSource.mute`, que
// sobrevive a AlterUpdate) y otro clic la vuelve a habilitar. La fila muteada se marca "[MUTE]", se
// le tiñe el fondo de rojo suave y sus barras pasan a rojo.
//
// TOPE DE LA MISMA VOZ
// --------------------
// Si la MISMA cantante ocupa TODAS las pistas (incluye Solo concert, que reparte el PJ del slot 1 por
// todas), el motor limita cuántas suenan a la vez: las que sobran quedan mudas y aquí se marcan
// "[max]" (en ámbar). En Mix voices el límite es 3; en Solo concert sale de
// `Config.MaxSimultaneousVoiceOnSoloConcert` (2 por defecto). Ver `UmaViewerAudio.ComputeSilencedSlots`.
//
// ESTILO DEL RÓTULO (como se pidió)
// ---------------------------------
//   · la pista del slot está ACTIVA (la tabla `_part` la marca > 0) → NEGRITA y BLANCO;
//   · la pista NO está activa → GRIS, sin negrita y en CURSIVA.
// El espectro va en VERDE si la fila está activa y en gris apagado si no.
//
// PARA QUÉ SIRVE
// --------------
// Para ver a mano la mezcla: si dos slots comparten PJ y los dos suenan (dos espectros verdes), el
// volumen se dobla; y si un slot está marcado activo pero su espectro está PLANO, ese slot no suena
// (o su onda no existe en el AWB, que es el caso que avisa el "[wave X/Y: no audio]").
//
// DE DÓNDE SALE CADA DATO
// -----------------------
//   · Los slots son `Director.liveVocal` (un `CuteAudioSource` por pista; `tag` = columna del `_part`).
//   · El PJ de cada slot es `Director.liveVocalCharaIds` (mismos índices que `liveVocal`).
//   · "Activa" se decide con la MISMA tabla que usa el motor: `Director.partInfo` (PartEntry) en la
//     fila de tiempo actual, como `UmaViewerAudio.AlterUpdate`. Con 0 o 1 pistas (`_soloMode`) el
//     motor se salta la tabla y suena la canción entera, así que el slot se marca activo siempre.
//   · El espectro se lee de la fuente AUDIBLE del slot (`activeSource`, o la primera en solo), no de
//     toda la lista: así se ve lo que de verdad sale por ese slot.
//
// CÓMO SE ENCIENDE Y POR QUÉ VIVE AQUÍ
// ------------------------------------
// Ctrl+F8. Vive en la escena `LiveScene` (donde está el `Director`) y se monta por código, no en el
// YAML, igual que el overlay del frame (LiveFrameDebugOverlay).
public class LiveVocalDebugOverlay : MonoBehaviour
{
    private const string OverlayObjectName = "LiveVocalDebugOverlay";
    private const string VisiblePrefKey = "LiveVocalDebugOverlay.Visible";

    // Mismo recurso builtin que usa el resto de overlays: en Unity 2022.3+ "Arial.ttf" ya no es una
    // fuente builtin válida y GetBuiltinResource lanza.
    private const string BuiltinFontName = "LegacyRuntime.ttf";

    private const string ActiveColor = "#FFFFFF";
    private const string InactiveColor = "#8C8C8C";
    private const string WarnColor = "#FF9C6B";
    private const string MuteColor = "#FF6B6B";
    private const string CapColor = "#FFD166";

    private const int MaxSlots = 7;
    private const float PanelWidth = 700f;
    private const float PanelPad = 12f;
    private const float TitleHeight = 30f;
    private const float RowHeight = 52f;
    private const float LabelHeight = 22f;

    // Espectro por pista (FFT pequeña: sólo es un vistazo).
    private const int FftSize = 256;
    private const int SpectrumWidth = 200;
    private const int SpectrumHeight = 26;
    private const float MinDb = -70f;
    private const float MaxDb = -5f;

    private static readonly Color32 BarOn = new Color32(96, 220, 120, 255);
    private static readonly Color32 BarOff = new Color32(88, 88, 88, 255);
    private static readonly Color32 BarMuted = new Color32(200, 84, 84, 255);
    private static readonly Color32 BarBackground = new Color32(0, 0, 0, 150);

    public static LiveVocalDebugOverlay Instance;

    private class SlotRow
    {
        public RectTransform root;
        public Image hit;
        public Text label;
        public RawImage spectrum;
        public Texture2D texture;
        public Color32[] pixels;
    }

    private GameObject _panel;
    private Text _title;
    private readonly List<SlotRow> _rows = new List<SlotRow>();
    private readonly float[] _fft = new float[FftSize];
    private readonly int[] _binOfColumn = new int[SpectrumWidth];
    private readonly StringBuilder _sb = new StringBuilder(256);
    private readonly bool[] _muted = new bool[MaxSlots];
    private bool _visible;

    // Punto de entrada. Lo llama LiveViewerUI.Awake. Idempotente.
    public static void Ensure()
    {
        if (Instance != null)
            return;

        try
        {
            GameObject go = new GameObject(OverlayObjectName);
            LiveVocalDebugOverlay overlay = go.AddComponent<LiveVocalDebugOverlay>();

            // Durante la carga aditiva del live la escena activa todavía es la anterior (Version2),
            // que se descarga justo después: sin este Move el overlay se destruiría nada más nacer.
            Scene liveScene = SceneManager.GetSceneByName("LiveScene");
            if (liveScene.IsValid() && liveScene.isLoaded)
                SceneManager.MoveGameObjectToScene(go, liveScene);

            overlay.Build();
            Instance = overlay;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[LiveVocalDebugOverlay] No se pudo crear el overlay de voces: " + ex.Message);
        }
    }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Necesario para que las filas reciban clics (el EventSystem de la escena ya existe).
        gameObject.AddComponent<GraphicRaycaster>();

        // --- Fondo (esquina superior izquierda) ---
        _panel = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(16f, -16f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelPad * 2f + TitleHeight + MaxSlots * RowHeight);

        Image bg = _panel.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);
        bg.raycastTarget = false;

        // --- Título ---
        _title = CreateText(_panel.transform, "LIVE VOICES", 18, TextAnchor.MiddleLeft, FontStyle.Bold);
        RectTransform titleRect = _title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -PanelPad);
        titleRect.sizeDelta = new Vector2(-PanelPad * 2f, TitleHeight);

        // Reparto logarítmico de columnas → bins de la FFT (1 .. FftSize/2).
        for (int x = 0; x < SpectrumWidth; x++)
        {
            float t = SpectrumWidth <= 1 ? 0f : (float)x / (SpectrumWidth - 1);
            int bin = Mathf.RoundToInt(Mathf.Pow(FftSize * 0.5f, t));
            _binOfColumn[x] = Mathf.Clamp(bin, 1, FftSize - 1);
        }

        // --- Filas (siempre las 7; se enseñan las que hagan falta) ---
        for (int k = 0; k < MaxSlots; k++)
            _rows.Add(CreateRow(k));

        _visible = PlayerPrefs.GetInt(VisiblePrefKey, 0) == 1;
        ApplyVisibility();
    }

    private SlotRow CreateRow(int index)
    {
        var row = new SlotRow();

        var rootGo = new GameObject("Slot" + (index + 1), typeof(RectTransform));
        rootGo.transform.SetParent(_panel.transform, false);
        row.root = (RectTransform)rootGo.transform;
        row.root.anchorMin = new Vector2(0f, 1f);
        row.root.anchorMax = new Vector2(1f, 1f);
        row.root.pivot = new Vector2(0.5f, 1f);
        row.root.anchoredPosition = new Vector2(0f, -(PanelPad + TitleHeight + index * RowHeight));
        row.root.sizeDelta = new Vector2(-PanelPad * 2f, RowHeight);

        // Fila CLICABLE: un Image transparente cubre todo el renglón y recibe el clic para mutear esa
        // pista (el rótulo y el espectro no son raycast targets, así que el clic llega aquí).
        row.hit = rootGo.AddComponent<Image>();
        row.hit.color = new Color(1f, 1f, 1f, 0f);
        row.hit.raycastTarget = true;
        var button = rootGo.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = row.hit;
        int slot = index;
        button.onClick.AddListener(() => ToggleMute(slot));

        row.label = CreateText(row.root, "", 20, TextAnchor.MiddleLeft, FontStyle.Normal);
        row.label.rectTransform.anchorMin = new Vector2(0f, 1f);
        row.label.rectTransform.anchorMax = new Vector2(1f, 1f);
        row.label.rectTransform.pivot = new Vector2(0.5f, 1f);
        row.label.rectTransform.anchoredPosition = Vector2.zero;
        row.label.rectTransform.sizeDelta = new Vector2(0f, LabelHeight);

        var imgGo = new GameObject("Spectrum", typeof(RectTransform), typeof(RawImage));
        imgGo.transform.SetParent(row.root, false);
        var imgRt = (RectTransform)imgGo.transform;
        imgRt.anchorMin = new Vector2(0f, 0f);
        imgRt.anchorMax = new Vector2(1f, 0f);
        imgRt.pivot = new Vector2(0.5f, 0f);
        imgRt.anchoredPosition = new Vector2(0f, 2f);
        imgRt.sizeDelta = new Vector2(0f, SpectrumHeight);

        row.texture = new Texture2D(SpectrumWidth, SpectrumHeight, TextureFormat.RGBA32, false);
        row.texture.filterMode = FilterMode.Point;
        row.texture.wrapMode = TextureWrapMode.Clamp;
        row.pixels = new Color32[SpectrumWidth * SpectrumHeight];

        row.spectrum = imgGo.GetComponent<RawImage>();
        row.spectrum.texture = row.texture;
        row.spectrum.color = Color.white;
        row.spectrum.raycastTarget = false;

        rootGo.SetActive(false);
        return row;
    }

    private static Text CreateText(Transform parent, string text, int size, TextAnchor anchor, FontStyle style)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = UiFont.Get();
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = text;
        return t;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8) && ControlHeld() && !ShiftHeld())
        {
            _visible = !_visible;
            PlayerPrefs.SetInt(VisiblePrefKey, _visible ? 1 : 0);
            PlayerPrefs.Save();
            ApplyVisibility();
        }

        if (_visible)
            Refresh();
    }

    // Se apaga el FONDO, no este componente: el Update sigue corriendo y el atajo vuelve a encenderlo.
    private void ApplyVisibility()
    {
        if (_panel != null)
            _panel.SetActive(_visible);
    }

    private void Refresh()
    {
        Director director = Director.instance;
        if (director == null || director.liveVocal == null || director.liveVocal.Count == 0)
        {
            SetRowsVisible(0);
            if (_title != null) _title.text = "LIVE VOICES   (no tracks)";
            return;
        }

        List<UmaViewerAudio.CuteAudioSource> vocals = director.liveVocal;
        PartEntry partInfo = director.partInfo;
        int rowIndex = CurrentRow(partInfo, director._liveCurrentTime);

        // Con 0 o 1 pistas el motor no mezcla: la canción entera suena, así que el slot está siempre
        // activo (mismo criterio que Director._soloMode).
        bool solo = director._soloMode;

        int used = Mathf.Min(vocals.Count, MaxSlots);

        //Tope de la misma voz (lo aplica AlterUpdate): las pistas activas que sobran quedan mudas.
        bool capOn = director._capSameVoice && used > director._maxSameVoice;
        int capAllowed = 0;

        int mutedCount = 0;
        for (int k = 0; k < used; k++) if (_muted[k]) mutedCount++;

        if (_title != null)
        {
            string mode = director.VocalMode == LiveVocalMode.SoloConcert ? "Solo concert" : "Mix voices";
            string title = string.Format("LIVE VOICES   ·   {0}   ·   {1} tracks", mode, vocals.Count);
            title += partInfo != null
                ? string.Format("   ·   table {0} singing / {1} declared", partInfo.SingerCount, partInfo.TrackCount)
                : "   ·   no table";
            if (director._capSameVoice) title += "   ·   Max " + director._maxSameVoice;
            title += mutedCount > 0
                ? "   ·   " + mutedCount + " muted"
                : "";
            _title.text = title;
        }

        SetRowsVisible(used);

        for (int k = 0; k < used; k++)
        {
            UmaViewerAudio.CuteAudioSource vocal = vocals[k];
            string part = vocal != null ? vocal.tag : null;
            int index = SlotIndex(partInfo, part, rowIndex);   // onda que pide la tabla (0 = silencio)
            bool tableActive = solo || index > 0;
            AudioSource source = AudibleSource(vocal, solo);

            //La 4ª pista activa (y siguientes) la silencia AlterUpdate cuando la misma voz ocupa todo.
            bool cappedOut = false;
            if (capOn && tableActive)
            {
                if (capAllowed >= director._maxSameVoice) cappedOut = true;
                else capAllowed++;
            }

            int waves = (vocal != null && vocal.sourceList != null) ? vocal.sourceList.Count : 0;

            _sb.Length = 0;
            _sb.Append("Slot ").Append(k + 1).Append(": ").Append(SlotName(director, k));
            //La columna del `_part` identifica la PISTA del motor: sirve para ver, p. ej., que `left`
            //y `left2` son el mismo PJ.
            if (!string.IsNullOrEmpty(part)) _sb.Append(" (").Append(part).Append(')');
            //Aviso: la tabla pide una onda que ese PJ no tiene en este AWB ⇒ el slot se queda MUDO
            //aunque la tabla lo marque como activo.
            if (!solo && index > 0 && index > waves)
                _sb.Append("  <color=").Append(WarnColor).Append(">[wave ").Append(index).Append('/')
                   .Append(waves).Append(": no audio]</color>");

            bool muted = _muted[k];
            ApplyMute(vocal, muted);
            if (cappedOut)
                _sb.Append("  <color=").Append(CapColor).Append(">[max]</color>");
            if (muted)
                _sb.Append("  <color=").Append(MuteColor).Append(">[MUTE]</color>");

            string body = _sb.ToString();
            _rows[k].label.text = tableActive
                ? "<b><color=" + ActiveColor + ">" + body + "</color></b>"
                : "<i><color=" + InactiveColor + ">" + body + "</color></i>";

            if (_rows[k].hit != null)
                _rows[k].hit.color = muted ? new Color(1f, 0.3f, 0.3f, 0.12f) : new Color(1f, 1f, 1f, 0f);

            DrawSpectrum(_rows[k], source, tableActive, muted);
        }
    }

    // Clic en una fila: mutea/desmutea esa pista. Se aplica sobre TODAS las fuentes del slot (el
    // `mute` de AudioSource sobrevive a AlterUpdate, que sólo toca `volume`).
    private void ToggleMute(int slot)
    {
        if (slot < 0 || slot >= _muted.Length) return;
        _muted[slot] = !_muted[slot];
    }

    private static void ApplyMute(UmaViewerAudio.CuteAudioSource vocal, bool muted)
    {
        if (vocal == null || vocal.sourceList == null) return;
        for (int i = 0; i < vocal.sourceList.Count; i++)
        {
            var source = vocal.sourceList[i];
            if (source != null && source.mute != muted) source.mute = muted;
        }
    }

    // La fuente que ese slot está sacando de verdad: la activa (si tiene volumen) o, en Solo concert,
    // la primera (el motor suena todas las waves a volumen 1). Si no hay ninguna, el slot está mudo.
    private static AudioSource AudibleSource(UmaViewerAudio.CuteAudioSource vocal, bool solo)
    {
        if (vocal == null || vocal.sourceList == null || vocal.sourceList.Count == 0) return null;
        if (vocal.activeSource != null && vocal.activeSource.volume > 0f) return vocal.activeSource;
        if (solo) return vocal.sourceList[0];
        return null;
    }

    private void DrawSpectrum(SlotRow row, AudioSource source, bool tableActive, bool muted)
    {
        if (source != null)
            source.GetSpectrumData(_fft, 0, FFTWindow.BlackmanHarris);
        else
            for (int i = 0; i < _fft.Length; i++) _fft[i] = 0f;

        Color32 barColor = muted ? BarMuted : (tableActive ? BarOn : BarOff);
        Color32[] px = row.pixels;

        for (int x = 0; x < SpectrumWidth; x++)
        {
            float mag = 0f;
            if (source != null)
            {
                //Máximo de los bins entre la columna anterior y ésta: al comprimir en log no se pierden
                //los picos.
                int prev = _binOfColumn[x > 0 ? x - 1 : 0];
                int bin = _binOfColumn[x];
                int from = Mathf.Min(prev, bin);
                int to = Mathf.Max(prev, bin);
                for (int b = from; b <= to && b < _fft.Length; b++)
                    if (_fft[b] > mag) mag = _fft[b];
            }

            float db = 20f * Mathf.Log10(mag + 1e-7f);
            int height = Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(MinDb, MaxDb, db) * SpectrumHeight), 0, SpectrumHeight);

            for (int y = 0; y < SpectrumHeight; y++)
                px[y * SpectrumWidth + x] = y < height ? barColor : BarBackground;
        }

        row.texture.SetPixels32(px);
        row.texture.Apply(false);
    }

    private void SetRowsVisible(int count)
    {
        for (int k = 0; k < _rows.Count; k++)
        {
            bool on = k < count;
            if (_rows[k].root != null && _rows[k].root.gameObject.activeSelf != on)
                _rows[k].root.gameObject.SetActive(on);
        }
    }

    // Fila de la tabla `_part` para el tiempo actual: mismo cálculo que UmaViewerAudio.AlterUpdate.
    private static int CurrentRow(PartEntry partInfo, float time)
    {
        if (partInfo == null) return -1;
        List<float> times;
        if (!partInfo.PartSettings.TryGetValue("time", out times) || times == null) return -1;

        int row = times.Count - 1;
        while (row >= 0 && time < times[row] / 1000.0) row--;
        return row;
    }

    // Onda (wave) que la tabla pide para esa columna en esa fila. 0 = silencio.
    private static int SlotIndex(PartEntry partInfo, string part, int row)
    {
        if (partInfo == null || string.IsNullOrEmpty(part) || row < 0) return 0;
        List<float> values;
        if (!partInfo.PartSettings.TryGetValue(part, out values) || values == null || row >= values.Count) return 0;
        return (int)values[row];
    }

    private static string SlotName(Director director, int slot)
    {
        int id = -1;
        if (director.liveVocalCharaIds != null && slot < director.liveVocalCharaIds.Count)
            id = director.liveVocalCharaIds[slot];

        var main = UmaViewerMain.Instance;
        var entry = (main != null && main.Characters != null && id >= 0)
            ? main.Characters.FirstOrDefault(c => c.Id == id)
            : null;
        return entry != null ? entry.GetName() : (id >= 0 ? id.ToString() : "?");
    }

    private static bool ControlHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }

    private static bool ShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
