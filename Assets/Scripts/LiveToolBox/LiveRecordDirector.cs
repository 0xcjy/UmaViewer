using Gallop.Live;
using Gallop.RenderPipeline;
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

//(Grabación del live) Grabador OFFLINE del show.
//
//QUÉ HACE
//-------
//Al pulsar GRABAR en el panel del viewer se carga el show como siempre, pero en vez de reproducirlo
//en tiempo real se avanza frame a frame a la cadencia elegida y cada frame se renderiza a un
//RenderTexture del tamaño pedido. Los frames se envían por tubería a FFmpeg, que los codifica a
//MP4; el audio se calcula aparte (LiveAudioMixdown) y al final se muxa.
//
//POR QUÉ ASÍ
//-----------
//  · No es una captura de ventana: la cadencia y la resolución quedan garantizadas aunque la máquina
//    no dé ese FPS en vivo (se puede grabar a 120 fps algo que solo corría a 60).
//  · El tiempo se controla de forma explícita (Director.OfflineRenderTime) y `Time.captureDeltaTime`
//    fija el paso real de Unity, así que partículas, tela y pelo avanzan igual de deterministas.
//  · Mientras se graba no se ve el show, sólo un cuadro de progreso (frames, %, tiempo y estimación).
//
//La captura reutiliza la misma mecánica que la herramienta de diagnóstico del Editor
//(Assets/Editor/LiveRenderCapture.cs): la cámara de presentación apunta a un RenderTexture y se leen
//los píxeles justo al terminar su render, para no perder los postefectos del live.
public class LiveRecordDirector : MonoBehaviour
{
    public static LiveRecordDirector Instance;

    //Fotogramas de calentamiento antes del primer frame grabado: la tela y el pelo necesitan unos
    //frames de simulación para asentarse y el primer frame no debe salir "en pelota".
    private const int WarmupMinFrames = 30;
    private const int WarmupMaxFrames = 150;

    private enum Phase { Preparing, Mixing, Warmup, Recording, Draining, Finalizing, Done }

    private LiveRecordSettings _settings;
    private Director _director;
    private Phase _phase = Phase.Preparing;

    private RenderTexture _rt;
    private Texture2D _readback;
    private Camera _frameCamera;
    private RenderTexture _savedCameraTarget;

    private LiveFfmpegVideoPipe _pipe;
    private string _ffmpeg;
    private string _tempVideoPath;
    private string _videoError;

    private int _totalFrames;
    private int _frameScheduled;   //frames de timeline ya fijados en el Director
    private int _frameWritten;     //frames ya enviados a FFmpeg
    private int _showRenderedFrame = -1;   //último frame de Unity en que el show se renderizó de verdad
    private int _warmupFrames;
    private float _endTime;
    private float _startTime;

    //Mezcla de audio en segundo plano.
    private Thread _audioThread;
    private volatile bool _audioDone;
    private volatile string _audioError;
    private string _audioPath;

    private double _startRealtime;
    private bool _finalizing;

    //Cuadro de progreso (se monta por código, como el resto de overlays del live).
    private GameObject _progressRoot;
    private Text _progressText;
    private Text _progressTitle;
    private GameObject _hudRoot;
    private bool _hudWasActive;
    private GameObject _lyricsRoot;
    private Camera _lyricsCamera;
    private RenderTexture _lyricsRT;
    private Material _lyricsBlit;
    private Camera _coverCamera;
    private GameObject _coverCanvas;
    private Text _lyricsText;
    private Text _lyricsSource;

    //Estado a restaurar al terminar.
    private bool _savedEnableRendering;
    private bool _savedHasFeature;
    private float _savedCaptureDelta;
    private int _savedTargetFrameRate;
    private int _savedVSync;
    private float _savedListenerVolume;
    private bool _savedRunInBackground;

    private const int LyricsLayer = 5;   //"UI": la letra se renderiza a un RT propio, así que no la
                                         //afectan los postefectos del show.

    //Punto de entrada. Lo llama LiveViewerUI.Awake en la escena del live si hay una grabación
    //pendiente (LiveRecordSettings.Pending).
    public static bool TryStart()
    {
        if (Instance != null) return false;
        if (LiveRecordSettings.Pending == null) return false;

        var go = new GameObject("LiveRecordDirector");
        var recorder = go.AddComponent<LiveRecordDirector>();

        //El objeto nace sin padre, así que cae en la escena ACTIVA; durante la carga aditiva del live
        //esa todavía puede ser Version2, que se descarga justo después. Se mueve a LiveScene para que
        //viva exactamente lo que vive el show (igual que hace LiveFrameDebugOverlay).
        var liveScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName("LiveScene");
        if (liveScene.IsValid() && liveScene.isLoaded)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, liveScene);

        recorder._settings = LiveRecordSettings.Pending;
        LiveRecordSettings.Pending = null;
        Instance = recorder;
        return true;
    }

    private void Awake()
    {
        Instance = this;
        //Cuanto antes, mejor: la cortina tapa la pantalla y el HUD del live se apaga, para que
        //mientras carga el show no se vea nada raro (ni la UI, ni el escenario montándose). El cuadro
        //de progreso se monta ya aquí, con el aviso de preparación.
        EnsureScreenCover();
        HideLiveHud();
        BuildProgressOverlay();
    }

    private void OnDestroy()
    {
        RenderPipelineManager.endCameraRendering -= OnCameraRendered;
        if (Instance == this) Instance = null;
    }

    //(Director) El motor llama aquí en cada Update mientras `IsOfflineRender` está activo, ANTES de
    //renderizar. Aquí se decide en qué instante de la canción se renderiza este frame.
    public void AdvanceTimeline(Director director)
    {
        if (_settings == null || director == null) return;
        if (_phase == Phase.Done) return;

        switch (_phase)
        {
            case Phase.Preparing:
                BeginWhenReady(director);
                break;

            case Phase.Mixing:
                director.OfflineRenderTime = _startTime;
                if (_audioDone) StartWarmup(director);
                break;

            case Phase.Warmup:
                director.OfflineRenderTime = _startTime;
                if (--_warmupFrames <= 0) StartRecording(director);
                break;

            case Phase.Recording:
                //El frame N se renderiza en el instante startTime + N/fps.
                director.OfflineRenderTime = _startTime + _frameScheduled * _settings.Step;
                _frameScheduled++;
                if (_frameScheduled >= _totalFrames) _phase = Phase.Draining;
                break;

            case Phase.Draining:
                director.OfflineRenderTime = _endTime - _settings.Step;
                Finish();
                break;

            case Phase.Finalizing:
                //Se está cerrando el vídeo y muxando el audio: el show se queda congelado en el último
                //frame (la pantalla sigue tapada y el sonido en silencio) hasta salir al visor.
                director.OfflineRenderTime = _endTime - _settings.Step;
                break;
        }

        SyncFrameCamera(director);
    }

    private void BeginWhenReady(Director director)
    {
        if (!director._isLiveSetup) return;
        if (!Gallop.ShaderManager.IsShaderBundleReady) return;

        try
        {
            _settings = _settings.Clone();
            _settings.Validate();
        }
        catch (Exception e)
        {
            Abort("Invalid recording settings: " + e.Message);
            return;
        }

        _director = director;
        _startTime = Mathf.Clamp(_settings.startTime, 0f, Mathf.Max(0f, director.totalTime - 0.01f));
        _endTime = _settings.endTime > 0f ? Mathf.Min(_settings.endTime, director.totalTime) : director.totalTime;
        if (_endTime - _startTime < _settings.Step) _endTime = Mathf.Min(director.totalTime, _startTime + _settings.Step);
        _totalFrames = Mathf.Max(1, Mathf.RoundToInt((_endTime - _startTime) * _settings.fps));

        _ffmpeg = LiveFfmpeg.Locate();
        if (string.IsNullOrEmpty(_ffmpeg))
        {
            Abort("FFmpeg not found. Install it, add it to the PATH or set its full path in Config (FfmpegPath).");
            return;
        }

        string folder = _settings.ResolvedFolder;
        string name = _settings.ResolvedName;
        try { Directory.CreateDirectory(folder); }
        catch (Exception e) { Abort("Cannot create the output folder: " + e.Message); return; }

        _audioPath = Path.Combine(folder, name + ".wav");
        //Nombre provisional: si la grabación se interrumpe (p. ej. se sale del Play Mode) queda este
        //fichero a medias en lugar de un MP4 incompleto con el nombre bueno.
        _tempVideoPath = Path.Combine(folder, name + ".partial.mp4");

        //RenderTexture de captura (con supermuestreo y MSAA) y textura de lectura.
        int rw = _settings.RenderWidth, rh = _settings.RenderHeight;
        try
        {
            _rt = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            _rt.antiAliasing = _settings.msaa;
            _rt.Create();
            _readback = new Texture2D(rw, rh, TextureFormat.RGB24, false);
        }
        catch (Exception e) { Abort("Could not create the capture RenderTexture: " + e.Message); return; }

        //Ajustes globales de grabación: paso fijo, sin límite de FPS y sin sync del vertido.
        _savedCaptureDelta = Time.captureDeltaTime;
        _savedTargetFrameRate = Application.targetFrameRate;
        _savedVSync = QualitySettings.vSyncCount;
        _savedListenerVolume = AudioListener.volume;
        _savedRunInBackground = Application.runInBackground;
        Time.captureDeltaTime = _settings.Step;
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        AudioListener.volume = 0f;    //el audio audible iría a otra velocidad; se mezcla aparte
        //Sin esto, con la ventana del Editor en segundo plano Unity frena el bucle y la grabación
        //tarda mucho más de lo necesario.
        Application.runInBackground = true;

        //El motor pausa los efectos (láseres, flashes, partículas) mientras no está "sincronizado":
        //en modo offline no hay reloj de audio que lo ponga a true, así que se marca a mano y se deja
        //el slider sin tocar, para que los efectos se reproduzcan con normalidad.
        director._syncTime = true;
        if (director.sliderControl != null)
        {
            director.sliderControl.is_Touched = false;
            director.sliderControl.is_Outed = false;
        }

        //Postefectos on/off.
        var feature = PostImageEffectFeature.Instance;
        _savedHasFeature = feature != null;
        if (feature != null)
        {
            _savedEnableRendering = feature.EnableRendering;
            feature.EnableRendering = _settings.postProcess;
        }

        //La letra (si toca) se dibuja con una cámara propia DENTRO del RenderTexture de captura. El HUD
        //del live ya se ocultó y el cuadro de progreso ya está montado (se hizo al arrancar).
        if (_settings.lyrics) BuildLyricsOverlay();

        //FFmpeg.
        //OJO: la memoria de textura de Unity va de ABAJO hacia arriba y el `rawvideo` de FFmpeg la
        //interpreta de ARRIBA hacia abajo, así que el frame entra volteado verticalmente y hay que
        //deshacerlo con `vflip` (si no, todo el vídeo sale boca abajo). El reescalado del
        //supermuestreo se encadena al volteo.
        string filter = "vflip";
        if (_settings.superSamplePercent != 100)
            filter += $",scale={_settings.width}:{_settings.height}:flags=lanczos";
        try
        {
            _pipe = new LiveFfmpegVideoPipe(_ffmpeg, _tempVideoPath,
                rw, rh, _settings.fps, filter);
        }
        catch (Exception e) { Abort("Could not start FFmpeg: " + e.Message); return; }

        //Una captura por frame de Unity, y sólo si el show se ha renderizado de verdad ese frame.
        RenderPipelineManager.endCameraRendering += OnCameraRendered;
        StartCoroutine(CaptureLoop());

        //La mezcla de audio se calcula en un hilo aparte mientras se calienta el show.
        _audioThread = new Thread(RenderAudio) { IsBackground = true, Name = "LiveAudioMixdown" };
        _audioThread.Start();

        _startRealtime = Time.realtimeSinceStartupAsDouble;
        SetPhaseText("Preparing recording…");
        _phase = Phase.Mixing;
    }

    private void RenderAudio()
    {
        try
        {
            var snapshot = _settings.Clone();
            string error = LiveAudioMixdown.Render(_director, snapshot, _audioPath);
            _audioError = error;
        }
        catch (Exception e)
        {
            _audioError = e.ToString();
        }
        finally
        {
            _audioDone = true;
        }
    }

    private void StartWarmup(Director director)
    {
        if (!string.IsNullOrEmpty(_audioError))
            Debug.LogWarning("[LiveRecord] Audio mixdown failed; the video will be recorded without sound: " + _audioError);

        _warmupFrames = Mathf.Clamp(_settings.fps, WarmupMinFrames, WarmupMaxFrames);
        _startRealtime = Time.realtimeSinceStartupAsDouble;
        _phase = Phase.Warmup;
    }

    private void StartRecording(Director director)
    {
        _frameScheduled = 0;
        _frameWritten = 0;
        _startRealtime = Time.realtimeSinceStartupAsDouble;
        _phase = Phase.Recording;
        Debug.Log($"[LiveRecord] Recording {_totalFrames} frames at {_settings.fps} fps " +
                  $"({_settings.RenderWidth}x{_settings.RenderHeight} -> {_settings.width}x{_settings.height}) to {_tempVideoPath}");
    }

    //Asigna el RenderTexture a la cámara que presenta el show en este frame (la que cambia según el
    //CameraSwitcher de la timeline).
    private void SyncFrameCamera(Director director)
    {
        if (_rt == null) return;
        var cam = director.MainRenderCamera;

        if (cam != _frameCamera)
        {
            if (_frameCamera != null) _frameCamera.targetTexture = _savedCameraTarget;
            _frameCamera = cam;
            if (_frameCamera != null)
            {
                _savedCameraTarget = _frameCamera.targetTexture;
                _frameCamera.targetTexture = _rt;
            }
        }

        //La cámara de la letra SIEMPRE debe renderizar después de la que presenta el show (se captura
        //al acabar ella), así que su 'depth' se recalcula cada frame.
        if (_lyricsCamera != null && _frameCamera != null)
        {
            float wanted = _frameCamera.depth + 50f;
            if (!Mathf.Approximately(_lyricsCamera.depth, wanted)) _lyricsCamera.depth = wanted;
        }
    }

    //La captura NO puede colgar de 'endFrameRendering': en el Editor ese callback se dispara una vez
    //por cada render (Game view, Scene view...), así que se enviaban hasta DOS frames por cada paso de
    //la timeline y el vídeo salía al doble de duración ("a cámara lenta").
    //
    //Ahora: la cámara que presenta marca en qué frame de Unity ha renderizado de verdad, y una
    //corrutina captura UNA sola vez por frame al final del mismo. Así cada paso del show = un frame.
    private void OnCameraRendered(ScriptableRenderContext context, Camera cam)
    {
        if (cam == null || _rt == null) return;

        var director = Director.instance;
        if (cam == _frameCamera || (director != null && director.IsPresentationCamera(cam)))
            _showRenderedFrame = Time.frameCount;
    }

    private System.Collections.IEnumerator CaptureLoop()
    {
        while (_phase != Phase.Done && _phase != Phase.Finalizing)
        {
            yield return new WaitForEndOfFrame();

            if (_phase != Phase.Recording) continue;
            if (_showRenderedFrame != Time.frameCount) continue;   //el show no se renderizó ese frame
            if (_frameWritten >= _totalFrames) continue;

            CaptureFrame();
        }
    }

    private void CaptureFrame()
    {
        Texture2D image = _readback;
        var previous = RenderTexture.active;
        try
        {
            //Compone la letra (si toca) encima del frame del show. Se hace aquí, con el frame ya
            //renderizado, para no depender de cómo vuelca URP las cámaras a un RenderTexture.
            if (_lyricsRT != null && _lyricsBlit != null)
                Graphics.Blit(_lyricsRT, _rt, _lyricsBlit);

            RenderTexture.active = _rt;
            image.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            var raw = image.GetRawTextureData<byte>();
            var buffer = _pipe.Rent();
            if (buffer.Length != raw.Length) buffer = new byte[raw.Length];
            raw.CopyTo(buffer);
            _pipe.Write(buffer, buffer.Length);
            _frameWritten++;
        }
        catch (Exception e)
        {
            RenderTexture.active = previous;
            _videoError = e.Message;
            Debug.LogError("[LiveRecord] Failed to capture the frame: " + e);
        }
    }

    private void Update()
    {
        if (_phase == Phase.Done) return;

        if (_phase == Phase.Preparing)
        {
            //En cuanto existe el Director se le pasa el mando del tiempo: así no avanza solo ni sale
            //del live al llegar al final (el grabador decide cuándo termina).
            var director = Director.instance;
            if (director != null && !director.IsOfflineRender)
            {
                director.IsOfflineRender = true;
                director.OfflineRenderTime = Mathf.Max(0f, _settings != null ? _settings.startTime : 0f);
            }
            return;
        }

        //Esc cancela la grabación (el botón Cancelar hace lo mismo).
        if (Input.GetKeyDown(KeyCode.Escape)) Cancel();

        UpdateProgressText();
    }

    //======================================================================
    // Cuadro de progreso y ocultado del HUD del live
    //======================================================================

    private void HideLiveHud()
    {
        if (_hudRoot != null) return;   //idempotente
        try
        {
            var ui = LiveViewerUI.Instance;
            Transform root = null;
            if (ui != null && ui.ProgressBar != null)
            {
                //El canvas RAÍZ del HUD: la barra de progreso cuelga de él y con él se van el slider,
                //los tiempos, los botones y los ajustes.
                var canvas = ui.ProgressBar.GetComponentInParent<Canvas>();
                root = canvas != null ? canvas.rootCanvas.transform : ui.ProgressBar.transform.root;
            }
            else
            {
                var found = GameObject.Find("Canvas");
                if (found != null) root = found.transform;
            }

            if (root != null)
            {
                _hudRoot = root.gameObject;
                _hudWasActive = _hudRoot.activeSelf;
                _hudRoot.SetActive(false);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LiveRecord] Could not hide the live HUD: " + e.Message);
        }
    }

    //Cámara "cortina": deja la pantalla en un color liso para que no se vea nada de lo que hay
    //detrás mientras se graba (restos del visor, la pantalla de carga a medio fundir, el escenario
    //montándose). Además, al haber una cámara que dibuja EN LA PANTALLA, la Game view deja de mostrar
    //"Display 1 No cameras rendering": la cámara del show renderiza a un RenderTexture, no a pantalla.
    private void EnsureScreenCover()
    {
        if (_coverCamera != null) return;
        try
        {
            var go = new GameObject("LiveRecordScreenCover");
            go.transform.SetParent(transform, false);
            _coverCamera = go.AddComponent<Camera>();
            _coverCamera.clearFlags = CameraClearFlags.SolidColor;
            _coverCamera.backgroundColor = new Color(0.055f, 0.062f, 0.078f, 1f);
            _coverCamera.cullingMask = 0;
            _coverCamera.orthographic = true;
            _coverCamera.orthographicSize = 1f;
            _coverCamera.nearClipPlane = 0.01f;
            _coverCamera.farClipPlane = 2f;
            _coverCamera.allowHDR = false;
            _coverCamera.allowMSAA = false;
            _coverCamera.useOcclusionCulling = false;
            //Se dibuja la ÚLTIMA: así limpia a color liso lo que hayan pintado otras cámaras en la
            //pantalla durante la carga (antes de que el show pase al RenderTexture).
            _coverCamera.depth = 1000f;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LiveRecord] Could not create the screen cover camera: " + e.Message);
        }

        BuildLoadingCover();
    }

    //Fondo de la "cortina": la MISMA imagen que usa la pantalla de carga del visor
    //(Assets/Resources/text2D/still_020001041_01_00.png, la que aparece en LoadingCanvas). Se dibuja
    //en un canvas propio por debajo del cuadro de progreso.
    private void BuildLoadingCover()
    {
        try
        {
            var sprite = Resources.Load<Sprite>("text2D/still_020001041_01_00");
            if (sprite == null)
            {
                Debug.LogWarning("[LiveRecord] Loading screen background not found; keeping the plain cover.");
                return;
            }

            _coverCanvas = new GameObject("LiveRecordCover",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = _coverCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;   //por debajo del cuadro de progreso (32000)

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_coverCanvas.transform, false);
            var rect = bg.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = bg.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LiveRecord] Could not create the loading background: " + e.Message);
        }
    }

    private void BuildProgressOverlay()
    {
        _progressRoot = new GameObject("LiveRecordProgress",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = _progressRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;   //por encima de cualquier overlay de depuración

        var scaler = _progressRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_progressRoot.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        //Esquina superior derecha: el cuadro no debe tapar el centro del show.
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(720f, 300f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);
        bg.raycastTarget = false;

        _progressTitle = CreateText(panel.transform, "Title", new Vector2(0f, 108f), new Vector2(680f, 48f), 34,
            new Color(1f, 0.85f, 0.3f, 1f), TextAnchor.MiddleCenter);
        _progressTitle.text = "RECORDING LIVE";

        _progressText = CreateText(panel.transform, "Body", new Vector2(0f, -4f), new Vector2(680f, 170f), 26,
            new Color(0.95f, 1f, 0.95f, 1f), TextAnchor.UpperLeft);

        var cancel = new GameObject("Cancel", typeof(RectTransform), typeof(Image), typeof(Button));
        cancel.transform.SetParent(panel.transform, false);
        var crect = cancel.GetComponent<RectTransform>();
        crect.anchorMin = crect.anchorMax = new Vector2(0.5f, 0.5f);
        crect.pivot = new Vector2(0.5f, 0.5f);
        crect.sizeDelta = new Vector2(220f, 46f);
        crect.anchoredPosition = new Vector2(0f, -130f);
        cancel.GetComponent<Image>().color = new Color(0.45f, 0.12f, 0.12f, 1f);
        var label = CreateText(cancel.transform, "Label", Vector2.zero, new Vector2(220f, 46f), 24,
            Color.white, TextAnchor.MiddleCenter);
        label.text = "Cancel";
        cancel.GetComponent<Button>().onClick.AddListener(Cancel);

        SetPhaseText("Preparing recording…");
    }

    private Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize,
                            Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private void SetPhaseText(string message)
    {
        if (_progressText != null) _progressText.text = message;
    }

    private void UpdateProgressText()
    {
        if (_progressText == null) return;

        if (_phase == Phase.Mixing)
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - _startRealtime;
            _progressText.text = "Mixing show audio…\n\n" +
                                 $"Elapsed: {FormatDuration(elapsed)}";
            return;
        }

        double total = Mathf.Max(_totalFrames, 1);
        int written = Mathf.Min(_frameWritten, _totalFrames);
        double percent = Math.Min(1.0, Math.Max(0.0, written / total)) * 100.0;
        double elapsedSec = Time.realtimeSinceStartupAsDouble - _startRealtime;
        double speed = elapsedSec > 0.01 ? written / elapsedSec / _settings.fps : 0.0;
        double remaining = speed > 0.001 ? (total - written) / (speed * _settings.fps) : 0.0;
        float currentTime = _startTime + written * _settings.Step;

        var sb = new StringBuilder();
        sb.AppendLine($"Frames    {written} / {_totalFrames}");
        sb.AppendLine($"Progress  {percent:0.0} %");
        sb.AppendLine($"Time      {FormatDuration(currentTime - _startTime)} / {FormatDuration(_endTime - _startTime)}");
        sb.AppendLine($"Remaining {FormatDuration(remaining)}   ({speed:0.00}x realtime)");
        sb.AppendLine($"Output    {_settings.width}x{_settings.height} @ {_settings.fps} fps");
        if (_phase == Phase.Draining) sb.AppendLine("Finalizing video…");
        _progressText.text = sb.ToString();
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) seconds = 0;
        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
            : $"{span.Minutes:00}:{span.Seconds:00}";
    }

    //======================================================================
    // Letra quemada en el vídeo
    //======================================================================

    //La letra se dibuja con una cámara propia que sólo ve la capa UI, así entra en el vídeo sin
    //atravesar los postefectos del show (bloom/DOF no deben emborronarla).
    //
    //La cámara NO dibuja sobre el RenderTexture de captura: dibuja sobre un RenderTexture propio
    //con fondo transparente, y al capturar cada frame se compone encima con un blit alfa. Motivo: en
    //URP, una cámara que renderiza a una RenderTexture recibe un attachment de color intermedio y lo
    //vuelca al final sobre el target, reemplazando lo que hubiera (se perdería la composición
    //multicámara del show). Componiendo nosotros el volcado, el frame del show se conserva.
    private void BuildLyricsOverlay()
    {
        var source = LiveViewerUI.Instance != null ? LiveViewerUI.Instance.LyricsText : null;
        if (source == null) return;

        _lyricsSource = source;

        var shader = Resources.Load<Shader>("Shaders/LyricsOverlayBlit");
        if (shader != null) _lyricsBlit = new Material(shader) { hideFlags = HideFlags.DontSave };

        int rw = _rt.width, rh = _rt.height;
        _lyricsRT = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
        {
            name = "LiveRecordLyricsRT"
        };
        _lyricsRT.Create();

        var camGo = new GameObject("LiveRecordLyricsCamera");
        _lyricsCamera = camGo.AddComponent<Camera>();
        //Fondo transparente: la letra se compone luego encima del frame del show.
        _lyricsCamera.clearFlags = CameraClearFlags.SolidColor;
        _lyricsCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        _lyricsCamera.cullingMask = 1 << LyricsLayer;
        _lyricsCamera.orthographic = true;
        _lyricsCamera.orthographicSize = 5f;
        _lyricsCamera.nearClipPlane = 0.01f;
        _lyricsCamera.farClipPlane = 100f;
        _lyricsCamera.targetTexture = _lyricsRT;
        _lyricsCamera.depth = 100f;
        _lyricsCamera.allowHDR = false;
        _lyricsCamera.allowMSAA = false;

        _lyricsRoot = new GameObject("LiveRecordLyrics", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _lyricsRoot.layer = LyricsLayer;
        var canvas = _lyricsRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _lyricsCamera;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 0;

        //Misma escala de UI que el HUD del live, si se puede leer.
        var scaler = _lyricsRoot.GetComponent<CanvasScaler>();
        var sourceScaler = source.GetComponentInParent<CanvasScaler>();
        if (sourceScaler != null)
        {
            scaler.uiScaleMode = sourceScaler.uiScaleMode;
            scaler.referenceResolution = sourceScaler.referenceResolution;
            scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            scaler.screenMatchMode = sourceScaler.screenMatchMode;
            scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
        }

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.layer = LyricsLayer;
        textGo.transform.SetParent(_lyricsRoot.transform, false);
        var rect = textGo.GetComponent<RectTransform>();
        var sourceRect = source.rectTransform;
        rect.anchorMin = sourceRect.anchorMin;
        rect.anchorMax = sourceRect.anchorMax;
        rect.pivot = sourceRect.pivot;
        rect.anchoredPosition = sourceRect.anchoredPosition;
        rect.sizeDelta = sourceRect.sizeDelta;

        _lyricsText = textGo.GetComponent<Text>();
        _lyricsText.font = source.font;
        _lyricsText.fontSize = source.fontSize;
        _lyricsText.fontStyle = source.fontStyle;
        _lyricsText.color = source.color;
        _lyricsText.alignment = source.alignment;
        _lyricsText.raycastTarget = false;
        _lyricsText.horizontalOverflow = source.horizontalOverflow;
        _lyricsText.verticalOverflow = source.verticalOverflow;
        _lyricsText.text = "";

        UpdateLyricsCanvas();
    }

    private void UpdateLyricsCanvas()
    {
        if (_lyricsText == null) return;
        _lyricsText.text = _lyricsSource != null ? _lyricsSource.text : "";
    }

    private void LateUpdate()
    {
        if (_lyricsText != null) UpdateLyricsCanvas();
    }

    //======================================================================
    // Fin, cancelación y restauración
    //======================================================================

    public void Cancel()
    {
        if (_phase == Phase.Done || _phase == Phase.Draining || _phase == Phase.Finalizing) return;
        _phase = Phase.Draining;
    }

    //Fin de la grabación. Se hace en CORRUTINA por dos razones:
    //  · poder pintar el aviso de "Finalizing video" antes de los bloqueos;
    //  · no devolver la pantalla ni el sonido al show hasta que FFmpeg haya terminado. Si se
    //    restaurase antes, mientras se cierra el vídeo y se muxa el audio se vería el live y se oiría
    //    la música (el motor la tiene sonando en tiempo real, estaba sólo silenciada).
    private void Finish()
    {
        if (_finalizing) return;
        _finalizing = true;
        _phase = Phase.Finalizing;

        RenderPipelineManager.endCameraRendering -= OnCameraRendered;

        StartCoroutine(FinalizeRoutine());
    }

    private System.Collections.IEnumerator FinalizeRoutine()
    {
        SetPhaseText("Finalizing video…");
        yield return null;   //deja que el aviso se dibuje antes de los bloqueos

        //Espera a que termine la mezcla de audio si aún estaba en marcha.
        try { _audioThread?.Join(TimeSpan.FromMinutes(5)); } catch { }

        bool videoOk = _pipe != null && _pipe.Complete();
        if (!videoOk)
            Debug.LogError("[LiveRecord] FFmpeg failed to encode the video: " + (_pipe != null ? _pipe.Error : _videoError));

        string finalPath = _settings.VideoPath;
        bool hasAudio = !string.IsNullOrEmpty(_audioPath) && File.Exists(_audioPath);
        bool muxed = false;
        if (videoOk && File.Exists(_tempVideoPath))
        {
            if (hasAudio)
            {
                string error;
                muxed = LiveFfmpeg.MuxAudio(_ffmpeg, _tempVideoPath, _audioPath, finalPath, out error);
                if (!muxed) Debug.LogError("[LiveRecord] Could not mux the audio: " + error);
            }
            else
            {
                try { File.Move(_tempVideoPath, finalPath); muxed = true; } catch { }
            }
        }

        if (muxed)
        {
            try { if (File.Exists(_tempVideoPath)) File.Delete(_tempVideoPath); } catch { }
            Debug.Log($"[LiveRecord] Recording finished: {finalPath}" + (hasAudio ? $" (+ {_audioPath})" : " (no audio)"));
        }

        //Ya no se graba: se calla el show, se devuelven pantalla/sonido y se sale al visor.
        _phase = Phase.Done;
        StopLiveAudio();
        Restore();
        Destroy(gameObject);

        var director = Director.instance;
        if (director != null) director.RequestExit();
    }

    //Corta la música y las voces del show (estaban sonando en tiempo real, sólo silenciadas por el
    //AudioListener). Así no se oye nada entre el fin de la grabación y la vuelta al visor.
    private void StopLiveAudio()
    {
        try
        {
            var director = Director.instance;
            if (director == null) return;
            if (director.liveMusic != null) UmaViewerAudio.Stop(director.liveMusic);
            if (director.liveVocal != null)
                foreach (var vocal in director.liveVocal)
                    if (vocal != null) UmaViewerAudio.Stop(vocal);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LiveRecord] Could not stop the live audio: " + e.Message);
        }
    }

    private void Abort(string message)
    {
        Debug.LogError("[LiveRecord] " + message);
        if (UmaViewerUI.Instance != null)
            UmaViewerUI.Instance.ShowMessage("Recording: " + message, UIMessageType.Error);
        _phase = Phase.Done;
        StopLiveAudio();
        Restore();
        Destroy(gameObject);

        //Devuelve el control del tiempo al motor y sale del live, para no quedarse dentro "congelado".
        var director = Director.instance;
        if (director != null)
        {
            director.IsOfflineRender = false;
            director.RequestExit();
        }
    }

    private void Restore()
    {
        Time.captureDeltaTime = _savedCaptureDelta;
        Application.targetFrameRate = _savedTargetFrameRate;
        QualitySettings.vSyncCount = _savedVSync;
        AudioListener.volume = _savedListenerVolume;
        Application.runInBackground = _savedRunInBackground;

        if (_savedHasFeature)
        {
            var feature = PostImageEffectFeature.Instance;
            if (feature != null) feature.EnableRendering = _savedEnableRendering;
        }

        if (_frameCamera != null) _frameCamera.targetTexture = _savedCameraTarget;

        if (_hudRoot != null) _hudRoot.SetActive(_hudWasActive);

        if (_lyricsCamera != null) Destroy(_lyricsCamera.gameObject);
        if (_lyricsRoot != null) Destroy(_lyricsRoot);
        if (_lyricsRT != null) { _lyricsRT.Release(); Destroy(_lyricsRT); }
        if (_lyricsBlit != null) Destroy(_lyricsBlit);
        if (_coverCanvas != null) Destroy(_coverCanvas);
        if (_coverCamera != null) Destroy(_coverCamera.gameObject);

        if (_progressRoot != null) Destroy(_progressRoot);

        if (_readback != null) Destroy(_readback);
        if (_rt != null) { _rt.Release(); Destroy(_rt); }

        _pipe?.Dispose();
    }
}
