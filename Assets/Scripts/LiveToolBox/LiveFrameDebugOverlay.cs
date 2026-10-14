using System;
using Gallop.Live;
using Gallop.Live.Cutt;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Overlay de depuración con el FRAME ACTUAL del live.
//
// QUÉ ES
// ------
// Un recuadro en la esquina superior derecha, apagado por defecto, que muestra el frame del show
// (el índice de la línea de tiempo a 60 fps, que es la unidad con la que están escritos los
// keyframes de las canciones), el tiempo en segundos, la RESOLUCIÓN DE RENDER y el FPS real del
// render con su límite (`Application.targetFrameRate`) y el `vSyncCount`. El frame del show NO
// depende del límite de frames (el show va en tiempo real); el FPS real es lo que sí cambia al
// tocar la fila "FPS".
//
// LA RESOLUCIÓN DE RENDER NO ES LA DE PANTALLA
// --------------------------------------------
// Esa línea es el `renderScale` de URP ya aplicado: `resolución del target de la cámara *
// renderScale`. URP dimensiona con eso sus attachments internos (scaledWidth/scaledHeight en
// UniversalRenderPipelineCore) y luego lo reescala a la salida, así que el tamaño de la ventana NO
// cambia; por eso el ajuste "Render Scale (%)" del panel de ajustes se ve aquí y no en la ventana.
// Se lee del pipeline activo (`GraphicsSettings.currentRenderPipeline`), que es el clon en runtime,
// y la base es `Director.MainRenderCamera` (la cámara que renderiza el live), con `Camera.main` de
// reserva.
//
// POR QUÉ EL FRAME ES `tiempo * 60`
// ---------------------------------
// La timeline del live está escrita a 60 fps: `LiveTimelineControl.kTargetFpsF` (y
// `LiveTimelineKey.FrameSecond = frame / 60`). La búsqueda de keyframe del motor hace
// `frame = tiempo * 60` (LiveTimelineKeyDataListTemplate.BinarySearchKey), así que este overlay usa
// exactamente la misma conversión para no mentir.
//
// CÓMO SE ENCIENDE Y POR QUÉ VIVE AQUÍ
// ------------------------------------
// Ctrl+Shift+F9 (ver el bloque de Update). El overlay vive en la escena `LiveScene` porque es donde
// está el `Director`: la escena del viewer se DESCARGA al cargar el live (UmaSceneController), así
// que un script de la escena principal no llegaría a ejecutarse durante el show.
//
// SE MONTA POR CÓDIGO, NO EN EL YAML
// ----------------------------------
// Igual que el resto de paneles de la escena del live: tocar `LiveScene.unity` a mano es donde se
// corrompen las escenas. Aquí se crea un Canvas propio para no depender de la jerarquía ni del
// Canvas de la UI del live.
public class LiveFrameDebugOverlay : MonoBehaviour
{
    private const string OverlayObjectName = "LiveFrameDebugOverlay";
    private const string VisiblePrefKey = "LiveFrameDebugOverlay.Visible";

    // Mismo recurso builtin que usa el resto de la UI del live: en Unity 2022.3+ "Arial.ttf" ya no
    // es una fuente builtin válida y GetBuiltinResource lanza.
    private const string BuiltinFontName = "LegacyRuntime.ttf";

    public static LiveFrameDebugOverlay Instance;

    private GameObject _panel;
    private Text _text;
    private bool _visible;

    // FPS real medido a reloj de pared (no `Time.deltaTime`, que durante una grabación vale
    // 1/cadencia por `Time.captureDeltaTime`). Se suaviza con una media exponencial (~0.25 s).
    private float _fps;
    private float _lastRealtime;

    // Tiempos por frame del motor (CPU main-thread / render-thread / GPU, en ms). Requiere el
    // ajuste "Frame Timing Stats" activado en Project Settings; si no, GetLatestTimings devuelve 0.
    private readonly FrameTiming[] _timings = new FrameTiming[1];

    // Punto de entrada. Lo llama LiveViewerUI.Awake. Idempotente: una vez creado el overlay, las
    // siguientes llamadas (una por live, si se encadenan) no hacen nada. Si algo falla, avisa una
    // vez por consola y el resto del show sigue igual: es una ayuda, no un requisito.
    public static void Ensure()
    {
        if (Instance != null)
            return;

        try
        {
            GameObject go = new GameObject(OverlayObjectName);
            LiveFrameDebugOverlay overlay = go.AddComponent<LiveFrameDebugOverlay>();

            // El GameObject nace sin padre, así que cae en la escena ACTIVA. Durante la carga
            // aditiva del live la activa todavía es la escena anterior (Version2), que se descarga
            // justo después: sin este Move, el overlay se destruiría nada más crearse. Se mueve a
            // LiveScene para que viva exactamente lo que vive el show.
            Scene liveScene = SceneManager.GetSceneByName("LiveScene");
            if (liveScene.IsValid() && liveScene.isLoaded)
                SceneManager.MoveGameObjectToScene(go, liveScene);

            overlay.Build();
            Instance = overlay;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LiveFrameDebugOverlay] No se pudo crear el overlay del frame: " + ex.Message);
        }
    }

    private void Build()
    {
        // Canvas propio, encima de todo (el HUD del live y los paneles de ajustes no deben taparlo).
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // --- Fondo (esquina superior derecha) ---
        _panel = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);

        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-16f, -16f);
        panelRect.sizeDelta = new Vector2(480f, 232f);

        Image bg = _panel.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);
        bg.raycastTarget = false;

        // --- Texto ---
        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(_panel.transform, false);

        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(12f, 8f);
        textRect.offsetMax = new Vector2(-12f, -8f);

        _text = textGo.GetComponent<Text>();
        _text.font = Resources.GetBuiltinResource<Font>(BuiltinFontName);
        _text.fontSize = 22;
        _text.alignment = TextAnchor.UpperRight;
        _text.color = new Color(0.95f, 1f, 0.85f, 1f);
        _text.raycastTarget = false;
        _text.horizontalOverflow = HorizontalWrapMode.Overflow;
        _text.verticalOverflow = VerticalWrapMode.Overflow;

        // La visibilidad se recuerda entre lives/ejecuciones: es una herramienta de desarrollo y no
        // apetece volver a pulsar el atajo cada vez que se relanza el viewer.
        _visible = PlayerPrefs.GetInt(VisiblePrefKey, 0) == 1;
        ApplyVisibility();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9) && ControlHeld() && ShiftHeld())
        {
            _visible = !_visible;
            PlayerPrefs.SetInt(VisiblePrefKey, _visible ? 1 : 0);
            PlayerPrefs.Save();
            ApplyVisibility();
        }

        if (_visible)
            RefreshText();
    }

    // Se apaga el FONDO, no este componente: así el Update sigue corriendo y el atajo vuelve a
    // encenderlo aunque el panel esté oculto.
    private void ApplyVisibility()
    {
        if (_panel != null)
            _panel.SetActive(_visible);
    }

    private void RefreshText()
    {
        if (_text == null)
            return;

        // FPS real, medido a reloj de pared y suavizado. Se calcula siempre (haya o no Director).
        float now = Time.realtimeSinceStartup;
        if (_lastRealtime > 0f)
        {
            float dt = now - _lastRealtime;
            if (dt > 0.00001f)
            {
                float instant = 1f / dt;
                _fps = _fps <= 0f ? instant : Mathf.Lerp(_fps, instant, 1f - Mathf.Exp(-dt / 0.25f));
            }
        }
        _lastRealtime = now;

        // `limit` es el valor que de verdad manda (Application.targetFrameRate, que solo se aplica
        // si vSyncCount == 0). Si el FPS real no sube al cambiarlo, el problema es la máquina/el
        // Editor, no el ajuste.
        string fpsLine = string.Format("FPS    {0:0.0}   limit {1}   vsync {2}",
            _fps, Application.targetFrameRate, QualitySettings.vSyncCount);
        string timingLine = FrameTimingsLine();

        Director director = Director.instance;
        string resLine = RenderResolutionLine(director);

        if (director == null)
        {
            _text.text = "LIVE FRAME\n(sin Director)\nRes    " + resLine + "\n" + fpsLine + "\n" + timingLine;
            return;
        }

        float time = director._liveCurrentTime;
        // Misma conversión que el motor (LiveTimelineKeyDataListTemplate.BinarySearchKey): el frame
        // "actual" es el mayor keyframe con frame <= tiempo * 60.
        int frame = Mathf.FloorToInt(Mathf.Max(0f, time) * LiveTimelineControl.kTargetFpsF);
        int totalFrames = director.totalTime > 0.001f
            ? Mathf.FloorToInt(director.totalTime * LiveTimelineControl.kTargetFpsF)
            : 0;

        string frames = totalFrames > 0
            ? string.Format("{0} / {1}", frame, totalFrames)
            : frame.ToString();

        _text.text = string.Format(
            "LIVE FRAME\nFrame  {0}\nTime   {1:0.000} s\nRes    {2}\n{3}\n{4}",
            frames, time, resLine, fpsLine, timingLine);
    }

    // Resolución a la que se renderiza DE VERDAD: el target de la cámara del live multiplicado por
    // el `renderScale` de URP (misma cuenta que URP: `Mathf.Max(1, (int)(pixel * renderScale))`). La
    // salida no cambia; esto es el tamaño interno. Se lee del pipeline activo, que es el clon en
    // runtime que toca el ajuste "Render Scale (%)". Si el pipeline no es URP, no hay escalado.
    private static string RenderResolutionLine(Director director)
    {
        Camera cam = director != null ? director.MainRenderCamera : null;
        if (cam == null) cam = Camera.main;

        int baseW = cam != null ? cam.pixelWidth : Screen.width;
        int baseH = cam != null ? cam.pixelHeight : Screen.height;

        UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null)
            return string.Format("{0}x{1}  x1.00 (sin URP)", baseW, baseH);

        float scale = urp.renderScale;
        int w = Mathf.Max(1, (int)(baseW * scale));
        int h = Mathf.Max(1, (int)(baseH * scale));
        return string.Format("{0}x{1}  x{2:0.00}", w, h, scale);
    }

    // Desglose por frame del motor, en ms. `espera` es lo que el hilo principal pasa esperando al
    // present (si es grande, el cuello es la GPU/el present; si es 0 y CPU es grande, es CPU).
    // Si "Frame Timing Stats" no está activado en Project Settings, GetLatestTimings devuelve 0.
    private string FrameTimingsLine()
    {
        uint count = FrameTimingManager.GetLatestTimings(1, _timings);
        if (count == 0)
            return "CPU/GPU ms: N/A (Frame Timing Stats?)";

        FrameTiming ft = _timings[0];
        return string.Format("CPU {0:0.0}ms (espera {1:0.0})  RT {2:0.0}ms  GPU {3:0.0}ms",
            ft.cpuMainThreadFrameTime, ft.cpuMainThreadPresentWaitTime,
            ft.cpuRenderThreadFrameTime, ft.gpuFrameTime);
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
