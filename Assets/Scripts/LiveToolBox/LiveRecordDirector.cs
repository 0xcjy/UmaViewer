using Gallop.Live;
using Gallop.RenderPipeline;
using System;
using System.IO;
using System.Text;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
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
//
//DIAGNÓSTICO DEL BUCLE (opcional, ver LiveRecordProfile.cs)
//----------------------------------------------------------
//Con `LiveRecordProfile` activo, antes de grabar se miden dos sondas (render sin captura, y render con
//lectura de píxeles) y durante la grabación se desglosa cada frame (escena/envío, espera de fin de
//frame, letra, lectura de píxeles, copia y espera a FFmpeg). Sirve para decidir si el techo es la GPU,
//la CPU, el lector de píxeles o el codificador. Con el perfil apagado no cuesta nada.
public class LiveRecordDirector : MonoBehaviour
{
    public static LiveRecordDirector Instance;

    //Fotogramas de calentamiento antes del primer frame grabado: la tela y el pelo necesitan unos
    //frames de simulación para asentarse y el primer frame no debe salir "en pelota".
    private const int WarmupMinFrames = 30;
    private const int WarmupMaxFrames = 150;

    private enum Phase { Preparing, Mixing, Warmup, ProbeRender, ProbeRead, Recording, Draining, Finalizing, Done }

    private LiveRecordSettings _settings;
    private Director _director;
    private Phase _phase = Phase.Preparing;

    private RenderTexture _rt;
    private RenderTexture _readbackTarget;   //RT reducido al tamaño de salida (o _rt si no hay supermuestreo)
    private Material _downscale;
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

    //Reencode de entrega desde el máster y trabajos finales (en hilos aparte, con progreso).
    private string _deliveryArgs;
    private volatile float _jobProgress = -1f;   //0..1 cuando se conoce; -1 = todavía sin dato
    private double _jobStart;
    private volatile bool _jobOk;
    private volatile string _jobError;
    private bool _videoOk;

    //Diagnóstico (opcional, ver LiveRecordProfile.cs). Si `_profile` es false no hace nada.
    private bool _profile;
    private int _probeFrames;
    private int _probeDrainEvery = 8;
    private bool _probeDrainFrame;
    private bool _sampling;          //este frame se está midiendo
    private char _curPhase;          //etiqueta de la muestra ('p','q','b','c')
    private int _sampledFrame;       //índice de frame de la muestra
    private double _frameStartMs;    //inicio del frame actual (en AdvanceTimeline)
    private double _submitEndMs;     //final del render de las cámaras del show
    private double _captureEndMs;    //final de la captura del frame actual
    private double _markMs;          //marca de trabajo dentro del frame
    private double _curSubmitMs, _curWaitMs, _curBlitMs, _curReadMs, _curCopyMs, _curWriteMs;
    private int _curPending;
    private bool _throttleWarned;
    private int _probeNotes;

    //Lectura de píxeles asíncrona (ver 'RequestAsyncReadback') y escala de render neutralizada mientras
    //se graba (ver 'BeginWhenReady').
    private bool _asyncReadback;
    private bool _asyncFailed;
    private bool _readbackPending;
    private AsyncGPUReadbackRequest _pendingReadback;
    private UniversalRenderPipelineAsset _savedPipeline;
    private float _savedRenderScale = 1f;

    //Tiempos de la lectura asíncrona (su callback cae en otro frame, así que se acumulan aparte).
    private double _asyncCopyMs, _asyncWriteMs;
    private int _asyncFrames;

    //Búferes nativos para la lectura asíncrona sin copias: la GPU escribe en uno y el hilo escritor lo
    //manda a FFmpeg tal cual. Varios búferes para que siempre haya uno libre.
    private NativeArray<byte>[] _asyncBuffers;
    private bool[] _asyncBusy;
    private int _asyncCursor;
    private int _asyncBufferBytes;
    private int _pendingIndex = -1;

    //Tamaño del frame que va a la CPU (tamaño de salida) y tiempo de la reducción en GPU.
    private int _captureW, _captureH;
    private double _curResolveMs;
    private int _fallbacks;

    //Cuadro de progreso (se monta por código, como el resto de overlays del live).
    private GameObject _progressRoot;
    private GameObject _eventSystem;
    private bool _ownEventSystem;
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
        EnsureEventSystem();
        BuildProgressOverlay();
    }

    //El cuadro de progreso necesita un EventSystem para recibir clics. Durante el live la escena que
    //lo tenía (Version2) ya se ha descargado, así que si no hay ninguno activo se crea uno propio.
    //Sin esto, el botón Cancelar no responde (y era justo el bug que se veía).
    private void EnsureEventSystem()
    {
        if (_eventSystem != null) return;
        var current = EventSystem.current;
        if (current != null)
        {
            _eventSystem = current.gameObject;
            _ownEventSystem = false;
            return;
        }

        var go = new GameObject("LiveRecordEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        _eventSystem = go;
        _ownEventSystem = true;
        Debug.LogWarning("[LiveRecord] No active EventSystem in the live scene; created one for the progress overlay.");
    }

    private void OnDestroy()
    {
        RenderPipelineManager.endCameraRendering -= OnCameraRendered;
        //Diagnóstico: si se sale del Play Mode a mitad de grabación, deja igualmente lo medido hasta ahí.
        LiveRecordProfile.Dump();
        if (Instance == this) Instance = null;
    }

    //(Director) El motor llama aquí en cada Update mientras `IsOfflineRender` está activo, ANTES de
    //renderizar. Aquí se decide en qué instante de la canción se renderiza este frame.
    public void AdvanceTimeline(Director director)
    {
        if (_settings == null || director == null) return;
        if (_phase == Phase.Done) return;

        TickFrame();

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
                if (--_warmupFrames <= 0) StartProbe(director);
                break;

            //Sondas de diagnóstico: se avanza la línea de tiempo igual que al grabar, pero la captura
            //(y en 'ProbeRender' también la lectura de píxeles) se salta. Ver LiveRecordProfile.cs.
            case Phase.ProbeRender:
            case Phase.ProbeRead:
                director.OfflineRenderTime = _startTime + _frameScheduled * _settings.Step;
                _frameScheduled++;
                if (_frameScheduled >= _probeFrames) AdvanceProbe(director);
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
        BeginSampledFrame();
    }

    //======================================================================
    // Diagnóstico del bucle (LiveRecordProfile)
    //======================================================================

    //Cierra la muestra del frame anterior y abre la del actual. Se llama en cada Update, al principio
    //de 'AdvanceTimeline', así que el tiempo "total" de un frame es del inicio de un Update al del
    //siguiente: lo que se tarda de verdad en producir ese frame.
    private void TickFrame()
    {
        if (!_profile) return;

        double now = LiveRecordProfile.NowMs;
        if (_sampling)
        {
            double captureEnd = _captureEndMs > 0 ? _captureEndMs : now;
            LiveRecordProfile.Push(new LiveRecordProfile.Sample
            {
                phase = _curPhase,
                frame = _sampledFrame,
                totalMs = now - _frameStartMs,
                submitMs = _submitEndMs > 0 ? _submitEndMs - _frameStartMs : 0,
                waitMs = _curWaitMs,
                resolveMs = _curResolveMs,
                blitMs = _curBlitMs,
                readMs = _curReadMs,
                copyMs = _curCopyMs,
                writeMs = _curWriteMs,
                gapMs = now - captureEnd,
                pending = _curPending
            });

            //Deja rastro en la consola durante las sondas: si el bucle va a segundos por frame, no debe
            //parecer que se ha colgado.
            if (_curPhase == 'p' || _curPhase == 'q' || _curPhase == 'b')
            {
                _probeNotes++;
                if (_probeNotes % 5 == 0)
                    LiveRecordProfile.Note(string.Format("sonda '{0}' {1}/{2}: {3:0} ms   |   {4}",
                        _curPhase, _sampledFrame, _probeFrames, now - _frameStartMs, LiveRecordProfile.LiveLine(_curPhase, 1000)));
            }
        }

        _frameStartMs = now;
        _markMs = now;
        _submitEndMs = 0;
        _captureEndMs = 0;
        _curSubmitMs = _curWaitMs = _curBlitMs = _curReadMs = _curCopyMs = _curWriteMs = 0;
        _curResolveMs = 0;
        _curPending = 0;
        _sampling = false;   //lo decide BeginSampledFrame() al final del Update

        //Vigila que nadie devuelva el límite de FPS ni el vSync a mitad de grabación: eso estrangula el
        //bucle y se confundiría con un cuello de botella real.
        if (!_throttleWarned && (Application.targetFrameRate != -1 || QualitySettings.vSyncCount != 0))
        {
            _throttleWarned = true;
            LiveRecordProfile.Note(string.Format("¡ojo! límite de fotogramas restaurado durante la grabación: targetFrameRate={0} vSync={1}",
                Application.targetFrameRate, QualitySettings.vSyncCount));
        }
    }

    //Marca el frame actual (y con qué etiqueta) para medirlo. Se llama al final de 'AdvanceTimeline',
    //cuando ya se sabe en qué fase se va a renderizar.
    private void BeginSampledFrame()
    {
        if (!_profile) return;

        _sampling = _phase == Phase.Recording || _phase == Phase.ProbeRender || _phase == Phase.ProbeRead;
        if (!_sampling) { _curPhase = '\0'; return; }

        _sampledFrame = _frameScheduled;
        if (_phase == Phase.ProbeRender)
        {
            _probeDrainFrame = _frameScheduled % _probeDrainEvery == 0;
            _curPhase = _probeDrainFrame ? 'q' : 'p';
        }
        else
        {
            _curPhase = _phase == Phase.ProbeRead ? 'b' : 'c';
        }
    }

    private void StartProbe(Director director)
    {
        if (!_profile)
        {
            StartRecording(director);
            return;
        }

        //Sondas cortas a propósito: si el bucle va a segundos por frame (configuraciones que agotan la
        //VRAM), una sonda larga parece un cuelgue. 12-24 frames bastan para ver el orden de magnitud.
        _probeFrames = Mathf.Clamp(_settings.fps / 8, 12, 24);
        _frameScheduled = 0;
        _probeDrainFrame = false;
        _phase = Phase.ProbeRender;
        LiveRecordProfile.Note("sondas: " + _probeFrames + " frames de render sin captura y " + _probeFrames + " con lectura de píxeles");
        SetPhaseText("Measuring the render loop (diagnostic)…");
    }

    private void AdvanceProbe(Director director)
    {
        _frameScheduled = 0;
        if (_phase == Phase.ProbeRender)
        {
            _phase = Phase.ProbeRead;
            return;
        }

        LiveRecordProfile.Note("sonda 'p' (render sin captura): " + LiveRecordProfile.LiveLine('p', 1000));
        LiveRecordProfile.Note("sonda 'b' (render + lectura de píxeles): " + LiveRecordProfile.LiveLine('b', 1000));
        StartRecording(director);
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
        //fichero a medias en lugar de un MP4 incompleto con el nombre bueno. Con máster, el contenedor
        //es Matroska (FFV1 no va en MP4).
        _tempVideoPath = Path.Combine(folder, name + (_settings.masterMode ? ".partial.mkv" : ".partial.mp4"));

        //RenderTexture de captura (con supermuestreo y MSAA) y, si hay supermuestreo, un segundo RT del
        //tamaño de salida donde se reduce EN LA GPU antes de leer (ver LiveDownscale.shader): la CPU
        //sólo recibe el frame final, 4x menos datos a 200 % de supermuestreo.
        int rw = _settings.RenderWidth, rh = _settings.RenderHeight;
        _captureW = _settings.width;
        _captureH = _settings.height;
        try
        {
            _rt = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            _rt.antiAliasing = _settings.msaa;
            _rt.Create();

            if (rw != _captureW || rh != _captureH)
            {
                var shader = Resources.Load<Shader>("Shaders/LiveDownscale");
                if (shader != null)
                {
                    _downscale = new Material(shader) { hideFlags = HideFlags.DontSave };
                    _downscale.SetVector("_DstSize", new Vector4(_captureW, _captureH, 0f, 0f));
                    _readbackTarget = new RenderTexture(_captureW, _captureH, 0, RenderTextureFormat.ARGB32,
                                                        RenderTextureReadWrite.Default);
                    _readbackTarget.Create();
                }
                else
                {
                    //Sin shader no hay reducción: se lee el frame grande y FFmpeg lo reescala (como antes).
                    Debug.LogWarning("[LiveRecord] LiveDownscale shader not found; falling back to a CPU-side scale in FFmpeg.");
                    _captureW = rw;
                    _captureH = rh;
                }
            }
        }
        catch (Exception e) { Abort("Could not create the capture RenderTexture: " + e.Message); return; }

        //La escala de render de URP se neutraliza mientras se graba. Con una cámara que renderiza a un
        //RenderTexture, URP crea la profundidad a RT × escala mientras el color está al tamaño del RT:
        //el frame queda inválido ("color surface does not match depth surface") y además cuesta 4x más.
        //El supermuestreo del grabador ya hace ese trabajo (y FFmpeg reduce con lanczos).
        //El pipeline activo es una copia en memoria (RuntimeGraphicsSettings), no el asset del proyecto.
        _savedPipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (_savedPipeline != null)
        {
            _savedRenderScale = _savedPipeline.renderScale;
            _savedPipeline.renderScale = 1f;
        }

        //Lectura de píxeles asíncrona: la GPU entrega el frame cuando termina, sin bloquear el hilo
        //principal (que puede empezar el siguiente). Si el sistema no la soporta, se sigue leyendo en
        //síncrono. En las sondas de diagnóstico se mide siempre la síncrona, que es la que se compara.
        _asyncReadback = SystemInfo.supportsAsyncGPUReadback;
        if (_asyncReadback)
        {
            //Cuatro búferes persistentes del tamaño de SALIDA. La GPU escribe en uno; el callback copia
            //a un búfer de RAM (2-3 ms) y lo libera al instante, así que nunca escasean.
            _asyncBufferBytes = _captureW * _captureH * 4;
            _asyncBuffers = new NativeArray<byte>[4];
            _asyncBusy = new bool[4];
            for (int i = 0; i < _asyncBuffers.Length; i++)
                _asyncBuffers[i] = new NativeArray<byte>(_asyncBufferBytes, Allocator.Persistent,
                                                         NativeArrayOptions.UninitializedMemory);
        }

        //Diagnóstico del bucle (opcional): perfil por frame y sondas antes de grabar.
        //Ver LiveRecordProfile.cs; con el perfil apagado no cuesta nada.
        LiveRecordProfile.Configure(folder, name);
        _profile = LiveRecordProfile.Resolve();
        if (_profile)
        {
            LiveRecordProfile.Note(LiveRecordProfile.EnvironmentLine());
            LiveRecordProfile.Note(string.Format(
                "ajustes: fps={0} salida={1}x{2} supermuestreo={3}% rt={4}x{5} msaa={6} post={7} letra={8} frames={9} paso={10:0.#####}",
                _settings.fps, _settings.width, _settings.height, _settings.superSamplePercent, rw, rh,
                _settings.msaa, _settings.postProcess, _settings.lyrics, _totalFrames, _settings.Step));
            LiveRecordProfile.Note("perfil activado por: " + LiveRecordProfile.EnabledBy);

            //Con la escala de render ya neutralizada: cuánta memoria piden los adjuntos del frame y con
            //qué método se leen los píxeles.
            LiveRecordProfile.Note(string.Format(
                "urp: renderScale={0:0.##} msaa={1}; lectura de píxeles={2}",
                _savedPipeline != null ? _savedPipeline.renderScale : -1f,
                _savedPipeline != null ? _savedPipeline.msaaSampleCount : -1,
                _asyncReadback ? "asíncrona" : "síncrona"));
            LiveRecordProfile.Note(LiveRecordProfile.FootprintLine(rw, rh, _settings.msaa, 1f));
        }

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
        //deshacerlo con `vflip` (si no, todo el vídeo sale boca abajo). El supermuestreo ya NO se
        //reescala aquí: se reduce en la GPU antes de leer el frame.
        //El formato es `rgba` porque es el que se lee del RenderTexture: es nativo (no obliga a
        //convertir en CPU como `rgb24`) y es el más rápido de transferir.
        bool master = _settings.masterMode;
        string filter = "vflip";
        //Si por lo que sea no hay reducción en GPU (shader ausente), FFmpeg vuelve a reescalar como antes.
        if (_captureW != _settings.width || _captureH != _settings.height)
            filter += $",scale={_settings.width}:{_settings.height}:flags=lanczos";

        //Etiquetas de color explícitas (BT.709, rango limitado). Sin esto, los ficheros 10 bits salen
        //con matrix/primaries/transfer "unknown" y algunos reproductores (y YouTube) reinterpretan el
        //color: se ven lavados. El máster FFV1 no las lleva porque va en RGB (bgr0/pc), que es lo suyo.
        if (!master)
            filter += ",scale=out_color_matrix=bt709:out_range=tv" +
                      ",setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv";

        string encoderArgs = null;
        try
        {
            //El encoder se resuelve una vez: NVENC/HEVC si el equipo los tiene, x264 si no.
            bool nvencH264 = LiveFfmpeg.SupportsEncoder(_ffmpeg, "h264_nvenc");
            bool nvencHevc = LiveFfmpeg.SupportsEncoder(_ffmpeg, "hevc_nvenc");
            //4:4:4 se comprueba con el formato exacto: puede haber HEVC sin soporte de croma completa.
            bool nvencHevc444 = (_settings.qualityLevel >= 2 || master) &&
                                LiveFfmpeg.SupportsEncoder(_ffmpeg, "hevc_nvenc", "yuv444p");
            bool nvencHevc44410 = (_settings.qualityLevel >= 3 || master) &&
                                  LiveFfmpeg.SupportsEncoder(_ffmpeg, "hevc_nvenc", "yuv444p10le");

            //Codificador de la captura: máster sin pérdida (FFV1, nivel Max) o entrega directa.
            encoderArgs = master
                ? LiveFfmpeg.MasterEncoderArgs
                : LiveFfmpeg.VideoEncoderArgs(_settings.qualityLevel, nvencH264, nvencHevc,
                                              nvencHevc444, nvencHevc44410);

            //Entrega al reencodar desde el máster: el mismo nivel elegido (Max = 4:4:4 10 bits).
            _deliveryArgs = LiveFfmpeg.VideoEncoderArgs(_settings.qualityLevel, nvencH264, nvencHevc,
                                                        nvencHevc444, nvencHevc44410);

            _pipe = new LiveFfmpegVideoPipe(_ffmpeg, _tempVideoPath,
                _captureW, _captureH, _settings.fps, filter, 4, "rgba", encoderArgs, master);

            if (_profile)
            {
                LiveRecordProfile.Note("captura: " + (master ? "FFV1 máster sin pérdida (.mkv)"
                    : LiveFfmpeg.EncoderLabel(_settings.qualityLevel, nvencH264, nvencHevc,
                                              nvencHevc444, nvencHevc44410) +
                      " (nivel " + _settings.qualityLevel + ")"));
                LiveRecordProfile.Note("entrega: " + LiveFfmpeg.EncoderLabel(_settings.qualityLevel,
                    nvencH264, nvencHevc, nvencHevc444, nvencHevc44410));
                LiveRecordProfile.Note(string.Format("frame a la CPU: {0}x{1} ({2:0.0} MB)",
                    _captureW, _captureH, _asyncBufferBytes / 1048576.0));
            }
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

        //Diagnóstico: hasta aquí ha llegado el trabajo de escena + envío de cámaras de este frame.
        if (_profile && _sampling && (cam == _frameCamera || cam == _lyricsCamera))
            _submitEndMs = LiveRecordProfile.NowMs;
    }

    private System.Collections.IEnumerator CaptureLoop()
    {
        while (_phase != Phase.Done && _phase != Phase.Finalizing)
        {
            yield return new WaitForEndOfFrame();

            bool recording = _phase == Phase.Recording;
            bool probeRead = _phase == Phase.ProbeRead;
            bool probeRender = _phase == Phase.ProbeRender;
            if (!recording && !probeRead && !probeRender) continue;

            //El show no se renderizó en este frame de Unity: no hay nada que capturar.
            if (_showRenderedFrame != Time.frameCount) continue;

            //Sondas: 'ProbeRead' lee los píxeles en cada frame; 'ProbeRender' sólo cada
            //'_probeDrainEvery' frames (para vaciar la GPU de vez en cuando y que el bucle no
            //se mida "adelantando" trabajo). Ni una ni otra envían nada a FFmpeg.
            bool read = recording || probeRead || (probeRender && _probeDrainFrame);
            if (recording && _frameWritten >= _totalFrames) continue;

            if (_profile)
            {
                //La espera es del final del render de las cámaras al arranque de la captura; y la marca
                //se reinicia aquí para que el desglose del frame empiece en este punto.
                _curWaitMs = _submitEndMs > 0 ? LiveRecordProfile.NowMs - _submitEndMs : 0;
                _markMs = LiveRecordProfile.NowMs;
            }
            if (read) CaptureFrame(recording);
            if (_profile) _captureEndMs = LiveRecordProfile.NowMs;
        }
    }

    //'toPipe' = enviar el frame a FFmpeg; si es false (sondas) sólo se lee, para separar el coste de
    //la lectura de píxeles del de la codificación.
    private void CaptureFrame(bool toPipe)
    {
        try
        {
            //1) Reducción del supermuestreo EN LA GPU: el frame grande (8K) se lleva al tamaño de salida
            //(4K). Sin esto, a la CPU viajarían 4x más datos y FFmpeg tendría que reescalar.
            RenderTexture target = _readbackTarget != null ? _readbackTarget : _rt;
            if (_readbackTarget != null)
            {
                Graphics.Blit(_rt, _readbackTarget, _downscale);
                if (_profile) MarkStage(ref _curResolveMs);
            }

            //2) Compone la letra (si toca) encima del frame, ya al tamaño de salida. Se hace aquí, con el
            //frame ya renderizado, para no depender de cómo vuelca URP las cámaras a un RenderTexture.
            if (_lyricsRT != null && _lyricsBlit != null)
            {
                Graphics.Blit(_lyricsRT, target, _lyricsBlit);
                if (_profile) MarkStage(ref _curBlitMs);
            }

            if (!toPipe)
            {
                //Sondas: lectura síncrona (es justo lo que se quiere medir) y nada para FFmpeg.
                ReadBackSync(target);
                if (_profile) MarkStage(ref _curReadMs);
                return;
            }

            if (_asyncReadback)
            {
                //La GPU entrega el frame por el callback, sin bloquear este hilo.
                RequestAsyncReadback(target);
                return;
            }

            ReadBackSync(target);
            if (_profile) MarkStage(ref _curReadMs);
            SendReadbackTexture(_readback.GetRawTextureData<byte>(), true);
        }
        catch (Exception e)
        {
            _videoError = e.Message;
            Debug.LogError("[LiveRecord] Failed to capture the frame: " + e);
        }
    }

    //Lectura síncrona del RenderTexture a la textura de CPU. Bloquea hasta que la GPU termina el frame
    //(es el método de referencia y el que usan las sondas).
    private void ReadBackSync(RenderTexture source)
    {
        EnsureReadbackTexture();
        var previous = RenderTexture.active;
        try
        {
            RenderTexture.active = source;
            _readback.ReadPixels(new Rect(0, 0, _captureW, _captureH), 0, 0);
            _readback.Apply();
        }
        finally
        {
            RenderTexture.active = previous;
        }
    }

    //Copia los píxeles a un búfer del pool y los encola hacia FFmpeg. Sólo se usa en la lectura síncrona
    //(sondas y reserva): la asíncrona entrega el búfer nativo sin copiar.
    private void SendReadbackTexture(NativeArray<byte> pixels, bool fromStage)
    {
        var buffer = _pipe.Rent();
        if (buffer.Length != pixels.Length) buffer = new byte[pixels.Length];

        double t0 = _profile ? LiveRecordProfile.NowMs : 0;
        pixels.CopyTo(buffer);
        double t1 = _profile ? LiveRecordProfile.NowMs : 0;

        _pipe.Write(buffer, buffer.Length);
        double t2 = _profile ? LiveRecordProfile.NowMs : 0;

        if (_profile && fromStage)
        {
            _curCopyMs = t1 - t0;
            _curWriteMs = t2 - t1;
            _curPending = _pipe.Pending;
            _markMs = t2;
        }
        _frameWritten++;
    }

    //Textura de lectura de CPU, al tamaño de salida. Se crea la primera vez y en RGBA32, que es nativo
    //y no obliga a convertir canales como RGB24.
    private void EnsureReadbackTexture()
    {
        if (_readback != null) return;
        _readback = new Texture2D(_captureW, _captureH, TextureFormat.RGBA32, false);
    }

    //Pide la lectura asíncrona del frame, directamente a un búfer nativo. Si la anterior aún no ha
    //llegado se espera: saltarse un frame rompería la secuencia del vídeo.
    private void RequestAsyncReadback(RenderTexture source)
    {
        if (_readbackPending)
        {
            try { _pendingReadback.WaitForCompletion(); } catch { _readbackPending = false; }
        }

        int index = PickFreeAsyncBuffer();
        if (index < 0)
        {
            //No debería ocurrir: el búfer se libera en cuanto se copia (2-3 ms), no al escribir. Si
            //pasara, se espera a lo pendiente y se reintenta; el camino síncrono queda como último
            //recurso y se cuenta, para verlo en el perfil si alguna vez se usa.
            _fallbacks++;
            try { _pendingReadback.WaitForCompletion(); } catch { }
            _readbackPending = false;
            index = PickFreeAsyncBuffer();

            if (index < 0)
            {
                ReadBackSync(source);
                if (_profile) MarkStage(ref _curReadMs);
                SendReadbackTexture(_readback.GetRawTextureData<byte>(), true);
                return;
            }
        }

        _asyncBusy[index] = true;
        _pendingIndex = index;
        _readbackPending = true;
        _pendingReadback = AsyncGPUReadback.RequestIntoNativeArray(ref _asyncBuffers[index], source, 0,
                                                                  TextureFormat.RGBA32, OnAsyncReadback);
        if (_profile) MarkStage(ref _curReadMs);
    }

    //Busca un búfer nativo que el hilo escritor haya terminado de usar.
    private int PickFreeAsyncBuffer()
    {
        if (_asyncBuffers == null) return -1;
        for (int k = 1; k <= _asyncBuffers.Length; k++)
        {
            int i = (_asyncCursor + k) % _asyncBuffers.Length;
            if (!_asyncBusy[i]) { _asyncCursor = i; return i; }
        }
        return -1;
    }

    //Callback de la lectura asíncrona (se ejecuta en el hilo principal). Aquí se copia el frame a RAM y
    //se libera el búfer de GPU ENSEGUIDA: así el siguiente frame nunca se queda sin sitio aunque el
    //hilo escritor vaya con retraso (era lo que hacía caer medio bucle al camino síncrono).
    private void OnAsyncReadback(AsyncGPUReadbackRequest request)
    {
        _readbackPending = false;
        int index = _pendingIndex;
        _pendingIndex = -1;
        if (index < 0) return;

        if (request.hasError)
        {
            _asyncBusy[index] = false;
            if (!_asyncFailed)
            {
                _asyncFailed = true;
                _asyncReadback = false;
                Debug.LogWarning("[LiveRecord] The asynchronous pixel readback failed; switching to the synchronous one.");
            }
            return;
        }

        try
        {
            int length = _asyncBufferBytes;
            var buffer = _pipe.Rent();
            if (buffer.Length != length) buffer = new byte[length];

            double t0 = _profile ? LiveRecordProfile.NowMs : 0;
            _asyncBuffers[index].CopyTo(buffer);
            _asyncBusy[index] = false;   //el búfer de GPU ya está libre
            double t1 = _profile ? LiveRecordProfile.NowMs : 0;

            _pipe.Write(buffer, length);
            double t2 = _profile ? LiveRecordProfile.NowMs : 0;

            if (_profile)
            {
                _asyncCopyMs += t1 - t0;
                _asyncWriteMs += t2 - t1;
                _asyncFrames++;
                _curPending = _pipe.Pending;
            }
            _frameWritten++;
        }
        catch (Exception e)
        {
            _asyncBusy[index] = false;
            _videoError = e.Message;
            Debug.LogError("[LiveRecord] Failed to send the asynchronous frame: " + e);
        }
    }

    //Libera los búferes nativos. Se llama al restaurar, DESPUÉS de cerrar la tubería (su hilo escritor
    //podría estar usándolos).
    private void DisposeAsyncBuffers()
    {
        if (_asyncBuffers == null) return;
        for (int i = 0; i < _asyncBuffers.Length; i++)
            if (_asyncBuffers[i].IsCreated) _asyncBuffers[i].Dispose();
        _asyncBuffers = null;
        _asyncBusy = null;
        _pendingIndex = -1;
    }

    //Espera a que la última lectura asíncrona llegue antes de cerrar la tubería (si no se perdería el
    //último frame).
    private void FlushReadback()
    {
        if (!_readbackPending) return;
        try { _pendingReadback.WaitForCompletion(); } catch { }
        _readbackPending = false;
    }

    private void MarkStage(ref double slot)
    {
        double now = LiveRecordProfile.NowMs;
        slot = now - _markMs;
        _markMs = now;
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
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

        //Sondas de diagnóstico (ver LiveRecordProfile.cs): se miden antes de empezar a grabar.
        if (_phase == Phase.ProbeRender || _phase == Phase.ProbeRead)
        {
            var probe = new StringBuilder();
            probe.AppendLine(_phase == Phase.ProbeRender
                ? "Measuring the render loop (diagnostic)…"
                : "Measuring the pixel readback (diagnostic)…");
            probe.AppendLine($"Frame {_frameScheduled} / {_probeFrames}");
            AppendProfileLine(probe, 'p');
            AppendProfileLine(probe, 'b');
            _progressText.text = probe.ToString();
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
        AppendProfileLine(sb, 'c');
        if (_phase == Phase.Draining) sb.AppendLine("Finalizing video…");
        _progressText.text = sb.ToString();
    }

    //Añade al cuadro de progreso la media de la fase indicada (sólo con el diagnóstico activo).
    private static void AppendProfileLine(StringBuilder sb, char phase)
    {
        if (!LiveRecordProfile.Enabled) return;
        string line = LiveRecordProfile.LiveLine(phase, 1000);
        if (!string.IsNullOrEmpty(line)) sb.AppendLine(line);
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

        int rw = _captureW, rh = _captureH;
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

        //------------------------------------------------------------------------
        //ESCALA: la letra se mide en PÍXELES DEL VÍDEO, no en píxeles de la pantalla.
        //
        //CAUSA DEL BUG: el HUD del live se apaga (SetActive(false)) antes de montar esta capa, y
        //`GetComponentInParent` NO mira objetos inactivos: devolvía null. La copia del CanvasScaler
        //nunca se hacía y el canvas de la letra se quedaba con los valores por defecto
        //(ConstantPixelSize, scaleFactor 1) → el texto se dibujaba a 15 px dentro de un frame de
        //2160 (0,7 % del alto). De ahí que la letra saliera diminuta y que los ajustes de escala
        //anteriores no cambiasen nada: la rama que los aplicaba no llegaba a ejecutarse.
        //(Y el propio Canvas sí se dimensiona con el RenderTexture: lo que fallaba era el factor.)
        //
        //Ahora la capa busca el scaler subiendo la jerarquía a mano (funciona con el HUD apagado) y
        //calcula el factor con la fórmula del CanvasScaler, pero evaluada con el tamaño del VÍDEO en
        //vez del de la ventana:
        //     px por unidad = blend(altoRT/refAlto, anchoRT/refAncho, match)
        //
        //Resultado: la letra ocupa siempre la misma fracción del frame (la del diseño del HUD) a
        //cualquier resolución, sin depender de cómo esté de grande el editor.
        //El canvas se deja a 1 unidad = 1 píxel y se escala el TEXTO (fontSize, caja, posición y
        //sombra), no el canvas: así los glifos se rasterizan al tamaño final y salen nítidos.
        //------------------------------------------------------------------------
        var sourceScaler = FindCanvasScaler(source.transform);
        float pxPerUnit = VideoUiScale(sourceScaler, _captureW, _captureH);

        var scaler = _lyricsRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;   //1 unidad de canvas = 1 píxel del RenderTexture

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.layer = LyricsLayer;
        textGo.transform.SetParent(_lyricsRoot.transform, false);
        var rect = textGo.GetComponent<RectTransform>();
        var sourceRect = source.rectTransform;
        rect.anchorMin = sourceRect.anchorMin;
        rect.anchorMax = sourceRect.anchorMax;
        rect.pivot = sourceRect.pivot;
        rect.anchoredPosition = sourceRect.anchoredPosition * pxPerUnit;
        rect.sizeDelta = new Vector2(sourceRect.sizeDelta.x * pxPerUnit,
                                     sourceRect.sizeDelta.y * pxPerUnit);

        _lyricsText = textGo.GetComponent<Text>();
        _lyricsText.font = source.font;
        //El tamaño va en píxeles del vídeo (ver el bloque de escala de arriba).
        _lyricsText.fontSize = Mathf.Max(1, Mathf.RoundToInt(source.fontSize * pxPerUnit));
        _lyricsText.fontStyle = source.fontStyle;
        _lyricsText.color = source.color;
        _lyricsText.alignment = source.alignment;
        _lyricsText.raycastTarget = false;
        _lyricsText.horizontalOverflow = source.horizontalOverflow;
        _lyricsText.verticalOverflow = source.verticalOverflow;
        _lyricsText.lineSpacing = source.lineSpacing;
        _lyricsText.supportRichText = source.supportRichText;
        _lyricsText.resizeTextForBestFit = source.resizeTextForBestFit;
        if (source.resizeTextForBestFit)
        {
            _lyricsText.resizeTextMinSize = Mathf.Max(1, Mathf.RoundToInt(source.resizeTextMinSize * pxPerUnit));
            _lyricsText.resizeTextMaxSize = Mathf.Max(1, Mathf.RoundToInt(source.resizeTextMaxSize * pxPerUnit));
        }
        _lyricsText.text = "";

        //La letra del HUD lleva sombra/contorno (1 unidad de separación). Se copia también, escalado
        //como el resto, o en el vídeo sale más plana que en pantalla.
        foreach (var sourceEffect in source.GetComponents<Shadow>())
        {
            var effect = textGo.AddComponent(sourceEffect.GetType()) as Shadow;
            if (effect == null) continue;
            effect.effectColor = sourceEffect.effectColor;
            effect.effectDistance = sourceEffect.effectDistance * pxPerUnit;
            effect.useGraphicAlpha = sourceEffect.useGraphicAlpha;
        }

        //Diagnóstico (sólo con el perfil activo): con estos números se comprueba de un vistazo que la
        //letra va al tamaño del vídeo y no al de la ventana.
        if (_profile)
            LiveRecordProfile.Note(string.Format(
                "letra: píxeles de vídeo por unidad={0:0.###} (vídeo {1}x{2}, referencia {3}x{4}) " +
                "→ fontSize={5} px, caja {6}x{7} px",
                pxPerUnit, _captureW, _captureH,
                sourceScaler != null ? sourceScaler.referenceResolution.x : -1f,
                sourceScaler != null ? sourceScaler.referenceResolution.y : -1f,
                _lyricsText.fontSize, rect.sizeDelta.x, rect.sizeDelta.y));

        UpdateLyricsCanvas();
    }

    private void UpdateLyricsCanvas()
    {
        if (_lyricsText == null) return;
        _lyricsText.text = _lyricsSource != null ? _lyricsSource.text : "";
    }

    //Sube la jerarquía a mano en vez de usar GetComponentInParent: el HUD del live está apagado
    //(SetActive(false)) cuando se monta la capa de la letra y GetComponentInParent ignora los
    //objetos inactivos, así que devolvía null y la escala se quedaba en los valores por defecto.
    private static CanvasScaler FindCanvasScaler(Transform t)
    {
        while (t != null)
        {
            var scaler = t.GetComponent<CanvasScaler>();
            if (scaler != null) return scaler;
            t = t.parent;
        }
        return null;
    }

    //Factor de escala que el HUD del live tendría si se dibujara a la resolución del vídeo. Es la
    //fórmula del CanvasScaler (ScaleWithScreenSize) evaluada con el tamaño del RenderTexture en vez
    //del de la ventana, para que la letra ocupe siempre la misma fracción del frame.
    private static float VideoUiScale(CanvasScaler scaler, int width, int height)
    {
        float w = Mathf.Max(1, width);
        float h = Mathf.Max(1, height);

        if (scaler == null) return Mathf.Max(0.01f, h / 600f);   //sin referencia conocida

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            return Mathf.Max(0.01f, scaler.scaleFactor);

        Vector2 reference = scaler.referenceResolution;
        if (reference.x <= 0f || reference.y <= 0f) return 1f;

        switch (scaler.screenMatchMode)
        {
            case CanvasScaler.ScreenMatchMode.Expand:
                return Mathf.Max(0.01f, Mathf.Min(w / reference.x, h / reference.y));
            case CanvasScaler.ScreenMatchMode.Shrink:
                return Mathf.Max(0.01f, Mathf.Max(w / reference.x, h / reference.y));
            default:
            {
                const float logBase = 2f;
                float logWidth = Mathf.Log(w / reference.x, logBase);
                float logHeight = Mathf.Log(h / reference.y, logBase);
                return Mathf.Max(0.01f,
                    Mathf.Pow(logBase, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight)));
            }
        }
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

        //Diagnóstico: qué ha hecho el codificador mientras se grababa (espera por contrapresión).
        if (_profile && _pipe != null)
        {
            LiveRecordProfile.Note(string.Format(
                "ffmpeg: {0} frames entregados; la escritura ha esperado {1:0.0} s en total, {2} de {3} veces con contrapresión (>2 ms); pendientes {4}",
                _pipe.FramesWritten, _pipe.WriteMs / 1000.0, _pipe.BackpressureCalls, _pipe.WriteCalls, _pipe.Pending));

            if (_asyncFrames > 0)
                LiveRecordProfile.Note(string.Format(
                    "lectura asíncrona: {0} frames; copia {1:0.0} ms + envío {2:0.0} ms por frame (hilo principal); lecturas síncronas por falta de búfer: {3}",
                    _asyncFrames, _asyncCopyMs / _asyncFrames, _asyncWriteMs / _asyncFrames, _fallbacks));
        }

        StartCoroutine(FinalizeRoutine());
    }

    private System.Collections.IEnumerator FinalizeRoutine()
    {
        SetPhaseText("Finalizing video…");
        yield return null;   //deja que el aviso se dibuje antes de los bloqueos

        //Diagnóstico: el perfil se escribe ANTES de cerrar el vídeo, para que quede en disco aunque
        //la codificación final se alargue o falle.
        LiveRecordProfile.Dump();

        //Espera a que termine la mezcla de audio si aún estaba en marcha.
        try { _audioThread?.Join(TimeSpan.FromMinutes(5)); } catch { }

        //Espera a la última lectura asíncrona: si no, se perdería el último frame de la grabación.
        FlushReadback();

        //------------------------------------------------------------------------
        // 1) Cerrar el vídeo capturado (FFmpeg termina de escribir lo que quede en cola).
        //    En un hilo aparte: con el máster sin pérdida esto puede tardar y antes congelaba el Editor.
        //------------------------------------------------------------------------
        SetPhaseText("Closing the recording…");
        _jobProgress = -1f;
        _jobStart = Time.realtimeSinceStartupAsDouble;
        _videoOk = false;

        var closeThread = new Thread(() =>
        {
            try { _videoOk = _pipe != null && _pipe.Complete(); }
            catch (Exception e) { _jobError = e.Message; }
        })
        { IsBackground = true, Name = "LiveRecordClose" };
        closeThread.Start();

        while (closeThread.IsAlive)
        {
            SetJobText("Closing the recording (FFmpeg finishing)…",
                       "Queued frames: " + (_pipe != null ? _pipe.Pending : 0));
            yield return null;
        }

        bool videoOk = _videoOk;
        if (!videoOk)
            Debug.LogError("[LiveRecord] FFmpeg failed to encode the video: " + (_pipe != null ? _pipe.Error : _videoError));

        //Con máster, el vídeo capturado es el .mkv sin pérdida; si no, el MP4 de entrega.
        bool master = _settings.masterMode;
        string finalPath = master ? _settings.MasterPath : _settings.VideoPath;
        bool hasAudio = !string.IsNullOrEmpty(_audioPath) && File.Exists(_audioPath);
        bool muxed = false;

        //------------------------------------------------------------------------
        // 2) Mux del audio (con el máster se COPIA el PCM: son varios GB de lectura/escritura).
        //    También en un hilo, con progreso por tiempo de salida.
        //------------------------------------------------------------------------
        if (videoOk && File.Exists(_tempVideoPath))
        {
            if (hasAudio)
            {
                double totalSeconds = Math.Max(1.0, _endTime - _startTime);
                _jobProgress = -1f;
                _jobStart = Time.realtimeSinceStartupAsDouble;
                _jobOk = false;
                _jobError = null;

                var muxThread = new Thread(() =>
                {
                    try
                    {
                        _jobOk = LiveFfmpeg.MuxAudio(_ffmpeg, _tempVideoPath, _audioPath, finalPath, out _jobError,
                            master, progress => _jobProgress = (float)Math.Min(1.0, progress / totalSeconds));
                    }
                    catch (Exception e) { _jobError = e.Message; }
                })
                { IsBackground = true, Name = "LiveRecordMux" };
                muxThread.Start();

                while (muxThread.IsAlive)
                {
                    SetJobText(master ? "Muxing the lossless master (audio in PCM)…" : "Muxing the audio…", null);
                    yield return null;
                }

                muxed = _jobOk;
                if (!muxed) Debug.LogError("[LiveRecord] Could not mux the audio: " + _jobError);
            }
            else
            {
                try { File.Move(_tempVideoPath, finalPath); muxed = true; } catch { }
            }
        }

        if (muxed)
        {
            try { if (File.Exists(_tempVideoPath)) File.Delete(_tempVideoPath); } catch { }
        }

        //------------------------------------------------------------------------
        // 3) Reencode de entrega desde el máster (High o superior). Es lo más largo después del
        //    render: ~30 ms por frame, o sea varios minutos para un show entero.
        //------------------------------------------------------------------------
        if (muxed && master)
        {
            double totalSeconds = Math.Max(1.0, _endTime - _startTime);
            _jobProgress = -1f;
            _jobStart = Time.realtimeSinceStartupAsDouble;
            _jobOk = false;
            _jobError = null;

            string masterPath = finalPath;
            string deliveryPath = _settings.VideoPath;
            string args = _deliveryArgs;

            var encodeThread = new Thread(() =>
            {
                try
                {
                    //La entrega mantiene la misma cadencia que la captura (sin remuestreo).
                    _jobOk = LiveFfmpeg.ReencodeFromMaster(_ffmpeg, masterPath, deliveryPath, args, out _jobError,
                        progress => _jobProgress = (float)Math.Min(1.0, progress / totalSeconds));
                }
                catch (Exception e) { _jobError = e.Message; }
            })
            { IsBackground = true, Name = "LiveRecordDelivery" };
            encodeThread.Start();

            while (encodeThread.IsAlive)
            {
                SetJobText("Encoding delivery (MP4) from the lossless master…",
                           _totalFrames > 0 ? "Frames in total: " + _totalFrames : null);
                yield return null;
            }

            if (!string.IsNullOrEmpty(_jobError))
                Debug.LogError("[LiveRecord] Delivery encode failed: " + _jobError);
        }

        //Ya no se graba: se calla el show, se devuelven pantalla/sonido y se sale al visor.
        _phase = Phase.Done;
        StopLiveAudio();
        Restore();
        Destroy(gameObject);

        var director = Director.instance;
        if (director != null) director.RequestExit();
    }

    //Cuadro de progreso de los trabajos finales (cierre, mux, reencode). 'progress' se rellena desde el
    //hilo de trabajo con los segundos de salida ya procesados, así que aquí solo se pinta.
    private void SetJobText(string title, string detail)
    {
        if (_progressText == null) return;

        double elapsed = Time.realtimeSinceStartupAsDouble - _jobStart;
        float progress = _jobProgress;
        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine();

        if (progress >= 0f)
        {
            float clamped = Mathf.Clamp01(progress);
            sb.AppendLine($"Progress  {clamped * 100f:0.0} %");
            sb.AppendLine($"Elapsed   {FormatDuration(elapsed)}");

            if (clamped > 0.01f)
            {
                double total = elapsed / clamped;
                sb.AppendLine($"Remaining {FormatDuration(Math.Max(0.0, total - elapsed))}   (total ≈ {FormatDuration(total)})");
            }
        }
        else
        {
            sb.AppendLine($"Elapsed   {FormatDuration(elapsed)}");
        }

        if (!string.IsNullOrEmpty(detail)) sb.AppendLine(detail);
        _progressText.text = sb.ToString();
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
        LiveRecordProfile.Dump();   //diagnóstico: deja el perfil de lo medido hasta aquí
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
        //Nada de lecturas en vuelo a partir de aquí (el callback escribiría en una tubería cerrada).
        FlushReadback();
        _asyncReadback = false;

        //Devuelve la escala de render que tenía el live antes de grabar.
        if (_savedPipeline != null) _savedPipeline.renderScale = _savedRenderScale;

        Time.captureDeltaTime = _savedCaptureDelta;
        Application.targetFrameRate = _savedTargetFrameRate;
        QualitySettings.vSyncCount = _savedVSync;
        AudioListener.volume = _savedListenerVolume;
        Application.runInBackground = _savedRunInBackground;

        if (_savedHasFeature)
        {
            var feature = PostImageEffectFeature.Instance;
            //Si el panel del live cambió el interruptor durante la grabación, manda el panel;
            //si no, se devuelve tal y como estaba antes de grabar.
            bool restore = Config.Instance != null ? Config.Instance.PostEffects : _savedEnableRendering;
            if (feature != null) feature.EnableRendering = restore;
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
        //Solo se destruye el EventSystem si lo creó el grabador (nunca uno de la escena).
        if (_ownEventSystem && _eventSystem != null) Destroy(_eventSystem);

        if (_readback != null) Destroy(_readback);
        if (_readbackTarget != null) { _readbackTarget.Release(); Destroy(_readbackTarget); }
        if (_downscale != null) Destroy(_downscale);
        if (_rt != null) { _rt.Release(); Destroy(_rt); }

        _pipe?.Dispose();

        //Los búferes nativos se liberan después de cerrar la tubería: su hilo escritor podría estar
        //usándolos (Dispose espera a que termine).
        DisposeAsyncBuffers();
    }
}
