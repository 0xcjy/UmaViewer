using Gallop.Live;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

//(Grabación de pantalla del live) Grabador de CLIPS en tiempo real.
//
// QUÉ HACE
// -------
// Ctrl+Shift+Enter empieza a grabar lo que se ve en pantalla y otra pulsación lo para (o a los
// 30 s, lo que llegue antes). No toca el motor: el show sigue reproduciéndose con normalidad y se
// puede pausar, cambiar de cámara o ponerlo a cámara lenta; el clip refleja exactamente lo que
// había en pantalla en cada momento.
//
// POR QUÉ NO ES EL "RECORD" (LiveRecordDirector)
// ----------------------------------------------
// El Record del panel es OFFLINE: avanza la timeline frame a frame a la cadencia elegida y no deja
// ver el show mientras graba. Este grabador es una captura de pantalla en vivo: no usa
// `Director.IsOfflineRender`, ni `Time.captureDeltaTime`, ni cambia FPS, vSync ni la escala de
// render. Sólo lee el framebuffer que Unity acaba de presentar.
//
// CADENCIA Y DURACIÓN
// -------------------
// El vídeo sale a 60 fps constantes, pero se captura lo que hay: si el juego va a 40 fps se
// repiten frames y si va a 144 se descartan. La duración del clip es SIEMPRE el reloj de pared
// (`Time.realtimeSinceStartupAsDouble`, sin escalar), así que pausar el show congela la imagen
// pero el clip sigue avanzando, igual que una grabación de pantalla normal.
//
// DE DÓNDE SALE LA IMAGEN
// ----------------------
// `ScreenCapture.CaptureScreenshotIntoRenderTexture` en `WaitForEndOfFrame` (el momento que
// documenta Unity) + `AsyncGPUReadback`, para no bloquear el hilo principal esperando a la GPU.
// Incluye lo que se ve: escena, postefectos, letra y la UI que esté visible (la barra del live se
// auto-oculta). Si el Game view es más ancho que `MaxWidth`, se reduce en la GPU antes de leer
// (leer 4K a 60 fps son ~2 GB/s de GPU a CPU y lastraría el propio directo).
//
// AUDIO
// -----
// Con `IncludeAudio`, se añade una sonda temporal al GameObject del `AudioListener` (ver
// LiveClipAudioTap.cs) que copia la mezcla final. Al parar se vuelca a un WAV y se muxa con el
// vídeo. Si no hay AudioListener o el mux falla, el clip se guarda sin sonido.
//
// SALIDA
// ------
// `Recordings/LiveClip_<fecha_hora>.mp4`, junto al proyecto/ejecutable (la misma carpeta que usa
// el Record). Se escribe primero un `.partial.mp4` y se renombra al cerrar bien.
public class LiveClipRecorder : MonoBehaviour
{
    public static LiveClipRecorder Instance;

    private const string ObjectName = "LiveClipRecorder";

    //==================================================================
    // Ajustes del clip (tocar aquí)
    //==================================================================
    // Duración máxima, en segundos de reloj de pared.
    private const float MaxSeconds = 30f;
    // Cadencia del vídeo. Se captura como mucho esta cantidad de veces por segundo.
    private const int OutputFps = 60;
    // Ancho máximo del vídeo; si el Game view es mayor, se reduce en la GPU antes de leer.
    private const int MaxWidth = 1920;
    // Grabar también el audio que suena (mezcla final del AudioListener).
    private const bool IncludeAudio = true;
    // Calidad del H.264 (más bajo = más calidad y más peso).
    private const int NvencCq = 20;
    private const int X264Crf = 20;

    private enum State { Idle, Recording, Stopping }

    private State _state = State.Idle;

    //Sonda de NVENC en segundo plano (ver StartEncoderProbe). -1 = sin dato todavía.
    private static volatile int _nvenc = -1;
    private static int _probeStarted;

    //Cierre en segundo plano: esperar a FFmpeg, escribir el WAV y muxar no puede bloquear el hilo
    //principal, porque cualquier parada de más de Time.maximumDeltaTime desincroniza el reloj
    //visual del live (avanza con Time.deltaTime) respecto al audio.
    private Task _finalizeTask;

    //Aviso REC: círculo rojo parpadeante en la esquina superior derecha mientras se graba. Va en
    //una UI aparte, así que también aparece en el vídeo (igual que un grabador de pantalla).
    private const float BlinkPeriod = 1f;          //ciclo completo del parpadeo
    private const int BadgeSortingOrder = 33000;   //por encima de los overlays de depuración

    private GameObject _badgeRoot;
    private Image _badgeDot;
    private Sprite _badgeCircle;
    private Texture2D _badgeTexture;
    private double _badgeStartTime;

    //Codificación.
    private string _ffmpeg;
    private LiveFfmpegVideoPipe _pipe;

    //Captura.
    private RenderTexture _screenRT;   //tamaño del Game view
    private RenderTexture _outRT;      //tamaño de salida (sólo si hay reducción)
    private RenderTexture _readRT;     //la que se lee: _outRT o _screenRT
    private int _outW, _outH;
    private int _maxFrames;

    //Lecturas asíncronas EN PARALELO. Con una sola en vuelo el bucle esperaba ~2-3 frames a que
    //llegara cada lectura y solo capturaba ~15 fps reales (clip a tirones) aunque el live fuera a
    //40. Varios búferes dejan que la GPU entregue mientras se capturan los siguientes frames.
    private NativeArray<byte>[] _readBuffers;
    private bool[] _readBusy;
    private AsyncGPUReadbackRequest[] _requests;
    private double[] _captureTimes;    //instante de captura de cada búfer
    private int _readCursor;
    private double _lastCaptureRealtime;

    //Último frame capturado (para repetirlo si el juego va por debajo de 60 fps) y un segundo
    //búfer para no pisarlo al llegar la siguiente lectura.
    private byte[] _lastFrame;
    private byte[] _frameScratch;
    private bool _lastFrameValid;

    //Tiempo y progreso.
    private double _startRealtime;
    private int _framesWritten;

    //Audio y rutas.
    private LiveClipAudioTap _audioTap;
    private string _finalPath;
    private string _tempVideoPath;
    private string _tempAudioPath;
    private string _videoError;

    //==================================================================
    // Arranque
    //==================================================================

    //Punto de entrada. Lo llama LiveViewerUI.Awake. Idempotente.
    public static void Ensure()
    {
        if (Instance != null) return;

        try
        {
            GameObject go = new GameObject(ObjectName);
            LiveClipRecorder recorder = go.AddComponent<LiveClipRecorder>();

            //El objeto nace sin padre y cae en la escena ACTIVA, que durante la carga aditiva del
            //live todavía puede ser la anterior (Version2), que se descarga justo después. Se mueve
            //a LiveScene para que viva exactamente lo que vive el show.
            Scene liveScene = SceneManager.GetSceneByName("LiveScene");
            if (liveScene.IsValid() && liveScene.isLoaded)
                SceneManager.MoveGameObjectToScene(go, liveScene);

            Instance = recorder;
            StartEncoderProbe();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LiveClip] Could not create the clip recorder: " + e.Message);
        }
    }

    //La detección de NVENC lanza FFmpeg y espera a que termine (~0,3-1 s). Hacerlo al pulsar el
    //atajo congelaba el hilo principal lo suficiente para que el live perdiera timeline (su reloj
    //visual avanza con Time.deltaTime, que Unity limita a Time.maximumDeltaTime por frame) y el
    //audio se quedara adelantado. Se hace UNA vez al cargar el live, en un hilo aparte.
    private static void StartEncoderProbe()
    {
        if (_probeStarted == 1) return;
        _probeStarted = 1;

        //Las rutas se resuelven en el hilo principal (tocan API de Unity) y al hilo sólo va el string.
        string ffmpeg = LiveFfmpeg.Locate();
        if (string.IsNullOrEmpty(ffmpeg)) return;

        var thread = new Thread(delegate ()
        {
            try
            {
                var info = new ProcessStartInfo(ffmpeg,
                    "-hide_banner -loglevel error -f lavfi -i color=c=black:s=256x256:d=0.1 " +
                    "-c:v h264_nvenc -pix_fmt yuv420p -f null -")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var proc = Process.Start(info))
                {
                    proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    _nvenc = proc.ExitCode == 0 ? 1 : 0;
                }
            }
            catch
            {
                _nvenc = 0;
            }
        })
        { IsBackground = true, Name = "LiveClipEncoderProbe" };

        thread.Start();
    }

    private void Awake()
    {
        Instance = this;
        BuildBadge();
    }

    private void Update()
    {
        UpdateBadge();

        //Ctrl+Shift+Enter (Return del teclado principal o del numérico) alterna grabar/parar.
        if (!EnterDown() || !ControlHeld() || !ShiftHeld()) return;

        if (_state == State.Idle) StartClip();
        else if (_state == State.Recording) StopClip("stopped by the user");
    }

    //==================================================================
    // Aviso REC (círculo rojo parpadeante)
    //==================================================================

    //Se monta por código (no en el YAML de LiveScene) y vive como hijo de este objeto, que se
    //mueve a LiveScene: se destruye solo al salir del live.
    private void BuildBadge()
    {
        try
        {
            _badgeRoot = new GameObject("LiveClipRecBadge",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _badgeRoot.transform.SetParent(transform, false);

            Canvas canvas = _badgeRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = BadgeSortingOrder;

            CanvasScaler scaler = _badgeRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject container = new GameObject("Badge", typeof(RectTransform));
            container.transform.SetParent(_badgeRoot.transform, false);
            RectTransform rect = container.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(40f, 40f);
            rect.anchoredPosition = new Vector2(-24f, -24f);

            _badgeCircle = CreateCircleSprite(64);

            //Aro oscuro de fondo: así el punto rojo se distingue también sobre escenarios claros.
            CreateCircleImage(container.transform, "Back", 40f, new Color(0f, 0f, 0f, 0.45f));
            _badgeDot = CreateCircleImage(container.transform, "Dot", 26f, new Color(0.95f, 0.10f, 0.10f, 1f));

            _badgeRoot.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LiveClip] Could not create the REC badge: " + e.Message);
        }
    }

    private Image CreateCircleImage(Transform parent, string name, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.sprite = _badgeCircle;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    //Textura de círculo con 1 px de antialiasing, generada en runtime (no depende de assets).
    private Sprite CreateCircleSprite(int size)
    {
        _badgeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "LiveClipRecCircle",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        _badgeTexture.SetPixels32(pixels);
        _badgeTexture.Apply();

        Sprite sprite = Sprite.Create(_badgeTexture, new Rect(0f, 0f, size, size),
                                      new Vector2(0.5f, 0.5f), 100f);
        sprite.name = "LiveClipRecCircle";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    private void UpdateBadge()
    {
        if (_badgeRoot == null) return;

        bool recording = _state == State.Recording;
        if (_badgeRoot.activeSelf != recording) _badgeRoot.SetActive(recording);
        if (!recording || _badgeDot == null) return;

        //Parpadeo con reloj SIN escalar: sigue a tiempo real aunque el live esté pausado o a
        //cámara lenta. Empieza encendido (fase 0).
        float phase = Mathf.Repeat((float)(Time.unscaledTimeAsDouble - _badgeStartTime), BlinkPeriod);
        _badgeDot.enabled = phase < BlinkPeriod * 0.5f;
    }

    //==================================================================
    // Inicio
    //==================================================================

    private void StartClip()
    {
        Director director = Director.instance;
        if (director == null)
        {
            Debug.LogWarning("[LiveClip] There is no live Director; nothing to record.");
            return;
        }
        if (director.IsOfflineRender)
        {
            Debug.LogWarning("[LiveClip] The offline recorder is running; the clip recorder is not available.");
            return;
        }

        _ffmpeg = LiveFfmpeg.Locate();
        if (string.IsNullOrEmpty(_ffmpeg))
        {
            Debug.LogWarning("[LiveClip] FFmpeg not found. Install it, add it to the PATH or set its full path in Config (FfmpegPath).");
            return;
        }

        try
        {
            PrepareOutput();
            PrepareTextures();
            PrepareReadback();
            PreparePipe();
            PrepareAudio();

            _framesWritten = 0;
            _videoError = null;
            _startRealtime = Time.realtimeSinceStartupAsDouble;
            _badgeStartTime = Time.unscaledTimeAsDouble;
            _maxFrames = Mathf.Max(1, Mathf.RoundToInt(MaxSeconds * OutputFps));
            _state = State.Recording;

            StartCoroutine(CaptureLoop());

            Debug.Log(string.Format(
                "[LiveClip] Recording {0}x{1} @ {2} fps (max {3:0} s) -> {4}",
                _outW, _outH, OutputFps, MaxSeconds, _finalPath));
        }
        catch (Exception e)
        {
            Debug.LogError("[LiveClip] Could not start the recording: " + e);
            StopAndDestroyAudioTap();
            Cleanup();
            SafeDelete(_tempVideoPath);
            SafeDelete(_tempAudioPath);
            _state = State.Idle;
        }
    }

    //Carpeta y nombres. Se reserva un nombre nuevo si ya existe ese mismo segundo.
    private void PrepareOutput()
    {
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));
        Directory.CreateDirectory(folder);

        string baseName = "LiveClip_" + DateTime.Now.ToString("yyyyMMdd_HH-mm-ss");
        _finalPath = Path.Combine(folder, baseName + ".mp4");
        for (int i = 2; File.Exists(_finalPath); i++)
            _finalPath = Path.Combine(folder, baseName + "_" + i + ".mp4");

        //Provisional: si se interrumpe la grabación queda este fichero a medias en vez de un MP4
        //incompleto con el nombre bueno.
        _tempVideoPath = Path.ChangeExtension(_finalPath, ".partial.mp4");
        _tempAudioPath = Path.ChangeExtension(_finalPath, ".partial.wav");
        SafeDelete(_tempVideoPath);
        SafeDelete(_tempAudioPath);
    }

    //RenderTextures de captura. Se lee a la resolución de SALIDA (tope MaxWidth); si la pantalla
    //es mayor, primero se captura a tamaño completo y se reduce con un blit en GPU.
    private void PrepareTextures()
    {
        int screenW = Mathf.Max(16, Screen.width);
        int screenH = Mathf.Max(16, Screen.height);

        _outW = screenW;
        _outH = screenH;
        if (screenW > MaxWidth)
        {
            float scale = MaxWidth / (float)screenW;
            _outW = MaxWidth;
            _outH = Mathf.RoundToInt(screenH * scale);
        }

        //H.264 4:2:0 exige lados pares.
        _outW = Mathf.Max(16, _outW - (_outW & 1));
        _outH = Mathf.Max(16, _outH - (_outH & 1));

        _screenRT = NewRenderTexture(screenW, screenH, "LiveClipScreen");
        _screenRT.Create();

        if (_outW != screenW || _outH != screenH)
        {
            _outRT = NewRenderTexture(_outW, _outH, "LiveClipOut");
            _outRT.Create();
            _readRT = _outRT;
        }
        else
        {
            _readRT = _screenRT;
        }
    }

    //Búferes de lectura y estado del "último frame" que se repite cuando el juego va por debajo
    //de la cadencia de salida.
    private void PrepareReadback()
    {
        int bytes = _outW * _outH * 4;

        _readBuffers = new NativeArray<byte>[4];
        _readBusy = new bool[4];
        _requests = new AsyncGPUReadbackRequest[4];
        _captureTimes = new double[4];
        for (int i = 0; i < _readBuffers.Length; i++)
            _readBuffers[i] = new NativeArray<byte>(bytes, Allocator.Persistent,
                                                     NativeArrayOptions.UninitializedMemory);

        _readCursor = 0;
        _lastFrame = null;
        _frameScratch = null;
        _lastFrameValid = false;
        _lastCaptureRealtime = 0;
    }

    private static RenderTexture NewRenderTexture(int width, int height, string name)
    {
        var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
        {
            name = name,
            useMipMap = false,
            filterMode = FilterMode.Bilinear
        };
        return rt;
    }

    private void PreparePipe()
    {
        //Para un clip en tiempo real interesa un encoder ligero: NVENC si está y, si no, x264 en
        //un preset rápido. No se usan los niveles del Record porque allí el tiempo no importa.
        //El resultado lo dejó StartEncoderProbe en segundo plano (aquí NO se puede sondear: bloquear
        //el hilo principal desincroniza el live). Si aún no hay dato, se usa x264.
        bool nvenc = _nvenc == 1;
        string encoder = nvenc
            ? "-c:v h264_nvenc -preset p5 -tune hq -rc vbr -cq " + NvencCq +
              " -b:v 0 -spatial-aq 1 -pix_fmt yuv420p "
            : "-c:v libx264 -preset veryfast -crf " + X264Crf + " -pix_fmt yuv420p ";

        //La captura de pantalla ya entrega las filas en orden correcto (arriba->abajo), así que NO
        //hay que voltear: `vflip` aquí dejaría el vídeo boca abajo. Las etiquetas de color BT.709
        //evitan que los reproductores lo reinterpretren (se vería lavado).
        const string filter = "scale=out_color_matrix=bt709:out_range=tv," +
                              "setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv";

        _pipe = new LiveFfmpegVideoPipe(_ffmpeg, _tempVideoPath, _outW, _outH, OutputFps,
                                        filter, 4, "rgba", encoder);
    }

    private void PrepareAudio()
    {
        if (!IncludeAudio) return;

        AudioListener listener = FindObjectOfType<AudioListener>();
        if (listener == null)
        {
            Debug.LogWarning("[LiveClip] No AudioListener in the live; the clip will be silent.");
            return;
        }

        _audioTap = listener.gameObject.AddComponent<LiveClipAudioTap>();
        _audioTap.Begin(MaxSeconds + 1f);
    }

    //==================================================================
    // Bucle de captura
    //==================================================================

    private IEnumerator CaptureLoop()
    {
        while (_state == State.Recording)
        {
            yield return new WaitForEndOfFrame();
            if (_state != State.Recording) yield break;

            double now = Time.realtimeSinceStartupAsDouble;
            if (now - _startRealtime >= MaxSeconds)
            {
                StopClip("time limit reached");
                yield break;
            }

            //Como mucho una captura por ventana de salida (60/s). Si el juego va a más fps no hace
            //falta capturar más; si va a menos, ya se repite el último frame.
            if (now - _lastCaptureRealtime < 1.0 / OutputFps) continue;

            //Con los 4 búferes ocupados (la GPU aún no ha entregado ninguno) se salta este frame:
            //mejor perder una captura que bloquear el render esperando a la lectura.
            int index = PickFreeReadBuffer();
            if (index < 0) continue;

            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_screenRT);

                //Reducción a la resolución de salida en la GPU (si hace falta).
                if (_outRT != null) Graphics.Blit(_screenRT, _outRT);

                int captured = index;
                _captureTimes[index] = now;
                _readBusy[index] = true;
                _requests[index] = AsyncGPUReadback.RequestIntoNativeArray(
                    ref _readBuffers[index], _readRT, 0, TextureFormat.RGBA32,
                    delegate (AsyncGPUReadbackRequest request) { OnReadback(request, captured); });
                _lastCaptureRealtime = now;
            }
            catch (Exception e)
            {
                Fail("Could not capture the screen: " + e.Message);
                yield break;
            }
        }
    }

    private int PickFreeReadBuffer()
    {
        if (_readBuffers == null) return -1;
        for (int k = 1; k <= _readBuffers.Length; k++)
        {
            int i = (_readCursor + k) % _readBuffers.Length;
            if (!_readBusy[i]) { _readCursor = i; return i; }
        }
        return -1;
    }

    //Cada lectura llega en un frame posterior, ya en el hilo principal. Aquí se copia el frame y se
    //coloca en el instante en que se capturó (no en el de la llegada): si no, todo el vídeo saldría
    //retrasado respecto al audio por la latencia de la lectura.
    private void OnReadback(AsyncGPUReadbackRequest request, int index)
    {
        if (_readBuffers == null || _readBusy == null || _captureTimes == null ||
            index < 0 || index >= _readBuffers.Length || index >= _captureTimes.Length)
            return;

        if (_state != State.Recording)
        {
            _readBusy[index] = false;
            return;
        }

        double captureElapsed;
        try
        {
            if (request.hasError)
            {
                _readBusy[index] = false;
                Fail("The GPU readback of the screen failed.");
                return;
            }

            int length = _outW * _outH * 4;
            if (_readBuffers[index].Length < length)
            {
                _readBusy[index] = false;
                Fail("Unexpected readback size: " + _readBuffers[index].Length +
                     " bytes (expected " + length + ").");
                return;
            }

            if (_lastFrame == null || _lastFrame.Length != length)
            {
                _lastFrame = new byte[length];
                _frameScratch = new byte[length];
                _lastFrameValid = false;
            }

            _readBuffers[index].CopyTo(_frameScratch);
            captureElapsed = _captureTimes[index] - _startRealtime;
        }
        catch (Exception e)
        {
            Fail("Could not read the captured frame: " + e.Message);
            return;
        }
        finally
        {
            if (_readBusy != null) _readBusy[index] = false;
        }

        if (_pipe == null) return;

        //Índice de salida que corresponde al instante de captura. Los huecos anteriores se rellenan
        //con el frame ANTERIOR (el que estaba en pantalla hasta ahora); este frame se guarda como
        //"último" para los índices siguientes. Así no se adelanta nada ni se desplaza el vídeo.
        int target = (int)Math.Floor(captureElapsed * OutputFps);
        target = Math.Max(0, Math.Min(target, _maxFrames));

        byte[] previous = _lastFrameValid ? _lastFrame : _frameScratch;
        FillWith(previous, target);

        byte[] swap = _lastFrame;
        _lastFrame = _frameScratch;
        _frameScratch = swap;
        _lastFrameValid = true;
    }

    //Escribe `source` repetido hasta que haya `until` frames de salida. Cada escritura necesita su
    //propio búfer (la tubería se lo queda y lo devuelve al pool).
    private void FillWith(byte[] source, int until)
    {
        if (source == null || _pipe == null) return;

        int limit = Mathf.Min(until, _maxFrames);
        while (_framesWritten < limit)
        {
            byte[] buffer = _pipe.Rent();
            if (buffer.Length != source.Length) buffer = new byte[source.Length];
            Buffer.BlockCopy(source, 0, buffer, 0, source.Length);
            _pipe.Write(buffer, source.Length);
            _framesWritten++;
        }
    }

    private void Fail(string message)
    {
        _videoError = message;
        Debug.LogError("[LiveClip] " + message);
        StopClip("capture error");
    }

    //==================================================================
    // Cierre
    //==================================================================

    private void StopClip(string reason)
    {
        if (_state != State.Recording) return;
        _state = State.Stopping;
        Debug.Log("[LiveClip] Stopping the clip (" + reason + ")…");

        try
        {
            //Audio: se recoge ANTES de destruir la sonda (Snapshot copia y para la captura).
            float[] samples = null;
            int channels = 0, rate = 0;
            if (_audioTap != null)
            {
                samples = _audioTap.Snapshot(out channels, out rate);
                StopAndDestroyAudioTap();
            }

            //Completa la duración hasta el instante de parada con el último frame capturado (la
            //salida va a 60 fps fijos aunque el juego vaya más lento).
            if (_lastFrameValid)
            {
                double stopElapsed = Time.realtimeSinceStartupAsDouble - _startRealtime;
                int stopTarget = (int)Math.Floor(stopElapsed * OutputFps);
                stopTarget = Math.Max(0, Math.Min(stopTarget, _maxFrames));
                FillWith(_lastFrame, stopTarget);
            }

            //Las texturas ya no hacen falta: se sueltan en el hilo principal mientras el cierre
            //real (FFmpeg + WAV + mux) se hace en segundo plano.
            ReleaseTextures();

            LiveFfmpegVideoPipe pipe = _pipe;
            _pipe = null;
            string ffmpegPath = _ffmpeg;
            string tempVideo = _tempVideoPath;
            string tempAudio = _tempAudioPath;
            string finalPath = _finalPath;
            int frames = _framesWritten;
            string error = _videoError;

            _state = State.Idle;

            //OJO: esto NO puede hacerse en el hilo principal. `Complete()` espera a que FFmpeg
            //termine y el mux lanza otro proceso: entre 1 y 3 s de parón. Como el reloj visual del
            //live avanza con Time.deltaTime (limitado a Time.maximumDeltaTime), un parón así le
            //hacía perder timeline y el audio quedaba adelantado en el clip siguiente y en el live.
            _finalizeTask = Task.Run(delegate ()
            {
                FinalizeClip(pipe, ffmpegPath, tempVideo, tempAudio, finalPath, frames, error,
                             samples, channels, rate);
            });
        }
        catch (Exception e)
        {
            Debug.LogError("[LiveClip] Error while closing the clip: " + e);
            SafeDelete(_tempVideoPath);
            SafeDelete(_tempAudioPath);
            ReleaseTextures();
            _state = State.Idle;
        }
    }

    //Cierre fuera del hilo principal: volcar la cola de FFmpeg, escribir el WAV con el audio ya
    //copiado y muxar. No toca ninguna API de Unity (sólo .NET), por eso puede correr en un Task.
    private static void FinalizeClip(LiveFfmpegVideoPipe pipe, string ffmpegPath,
                                     string tempVideo, string tempAudio, string finalPath,
                                     int frames, string error,
                                     float[] samples, int channels, int rate)
    {
        try
        {
            bool videoOk = pipe != null && pipe.Complete();
            string pipeError = pipe != null ? pipe.Error : null;
            if (pipe != null) { try { pipe.Dispose(); } catch { } }

            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("[LiveClip] Recording discarded because of an error: " + error);
                SafeDelete(tempVideo);
                SafeDelete(tempAudio);
                return;
            }
            if (frames <= 0)
            {
                Debug.LogWarning("[LiveClip] No frame was captured; the clip is discarded.");
                SafeDelete(tempVideo);
                SafeDelete(tempAudio);
                return;
            }
            if (!videoOk)
            {
                Debug.LogError("[LiveClip] FFmpeg could not close the video: " +
                               (string.IsNullOrEmpty(pipeError) ? "unknown error" : pipeError));
                SafeDelete(tempVideo);
                SafeDelete(tempAudio);
                return;
            }

            //WAV del audio capturado. Se rellena con silencio hasta la duración del vídeo para que
            //`-shortest` no recorte el clip si la sonda arrancó un pelín tarde.
            string audioPath = null;
            if (samples != null && samples.Length > 0 && channels > 0 && rate > 0)
            {
                try
                {
                    WriteWav(tempAudio, samples, channels, rate, frames / (double)OutputFps);
                    audioPath = tempAudio;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[LiveClip] Could not write the audio: " + e.Message);
                }
            }

            bool muxed = false;
            if (audioPath != null)
            {
                muxed = LiveFfmpeg.MuxAudio(ffmpegPath, tempVideo, audioPath, finalPath, out string muxError);
                if (!muxed)
                    Debug.LogWarning("[LiveClip] Could not mux the audio (" + muxError +
                                     "); the clip is saved without sound.");
            }

            if (!muxed)
            {
                SafeDelete(finalPath);
                File.Move(tempVideo, finalPath);
            }

            SafeDelete(tempVideo);
            SafeDelete(tempAudio);

            long bytes = 0;
            try { if (File.Exists(finalPath)) bytes = new FileInfo(finalPath).Length; } catch { }

            Debug.Log(string.Format("[LiveClip] Clip saved: {0} ({1:0.0} s, {2:0.0} MB, {3})",
                finalPath, frames / (double)OutputFps, bytes / 1048576.0,
                muxed ? "with audio" : "no audio"));
        }
        catch (Exception e)
        {
            Debug.LogError("[LiveClip] Error while finalizing the clip: " + e);
            SafeDelete(tempVideo);
            SafeDelete(tempAudio);
        }
    }

    //Se destruye el objeto a mitad de grabación (salida del live, recarga de escena, Play Mode):
    //se cierra el vídeo para no perder el clip. Sin audio y sin mux para no retrasar la descarga.
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (_state == State.Recording)
        {
            _state = State.Stopping;
            try
            {
                StopAndDestroyAudioTap();

                if (_pipe != null && _pipe.Complete() && _framesWritten > 0)
                {
                    SafeDelete(_finalPath);
                    File.Move(_tempVideoPath, _finalPath);
                    Debug.Log("[LiveClip] The live ended while recording; the clip was saved without audio: " + _finalPath);
                }
                else
                {
                    SafeDelete(_tempVideoPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LiveClip] Could not close the clip when leaving the live: " + e.Message);
                SafeDelete(_tempVideoPath);
            }
            _state = State.Idle;
        }

        Cleanup();

        //La textura y el sprite del aviso REC se crean en runtime: se liberan a mano.
        if (_badgeCircle != null) { Destroy(_badgeCircle); _badgeCircle = null; }
        if (_badgeTexture != null) { Destroy(_badgeTexture); _badgeTexture = null; }
    }

    //Al cerrar la aplicación puede haber un cierre en segundo plano a medias: se le da un margen
    //para que el MP4 quede completo.
    private void OnApplicationQuit()
    {
        Task task = _finalizeTask;
        if (task == null) return;
        try { task.Wait(5000); } catch { }
    }

    private void StopAndDestroyAudioTap()
    {
        if (_audioTap == null) return;
        _audioTap.StopCapture();
        Destroy(_audioTap);
        _audioTap = null;
    }

    //Suelta las lecturas, los búferes nativos y las RenderTextures (hilo principal). Antes espera a
    //las lecturas en vuelo para no destruir recursos que la GPU todavía está leyendo.
    private void ReleaseTextures()
    {
        if (_readBuffers != null && _readBusy != null && _requests != null)
        {
            for (int i = 0; i < _readBuffers.Length; i++)
            {
                if (!_readBusy[i]) continue;
                try { _requests[i].WaitForCompletion(); } catch { }
                _readBusy[i] = false;
            }
        }

        if (_readBuffers != null)
        {
            for (int i = 0; i < _readBuffers.Length; i++)
                if (_readBuffers[i].IsCreated) _readBuffers[i].Dispose();
            _readBuffers = null;
        }

        _readBusy = null;
        _requests = null;
        _captureTimes = null;
        _lastFrame = null;
        _frameScratch = null;
        _lastFrameValid = false;

        if (_screenRT != null) { try { _screenRT.Release(); } catch { } Destroy(_screenRT); _screenRT = null; }
        if (_outRT != null) { try { _outRT.Release(); } catch { } Destroy(_outRT); _outRT = null; }
        _readRT = null;
    }

    //Libera tubería, texturas y búferes al destruirse el objeto.
    private void Cleanup()
    {
        ReleaseTextures();

        if (_pipe != null)
        {
            try { _pipe.Dispose(); } catch { }
            _pipe = null;
        }
    }

    //==================================================================
    // WAV y utilidades
    //==================================================================

    //WAV PCM 16 bits. Si `minSeconds` es mayor que lo capturado, se rellena con silencio.
    //Se llama desde el hilo de cierre, así que sólo usa System.Math (nada de Mathf/API de Unity).
    private static void WriteWav(string path, float[] samples, int channels, int rate, double minSeconds)
    {
        channels = Math.Max(1, Math.Min(8, channels));
        int frames = samples.Length / channels;
        int minFrames = Math.Max(0, (int)Math.Ceiling(minSeconds * rate));
        int totalFrames = Math.Max(frames, minFrames);
        int totalSamples = totalFrames * channels;

        short[] pcm = new short[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            if (i >= samples.Length) { pcm[i] = 0; continue; }
            float v = samples[i];
            if (v > 1f) v = 1f;
            else if (v < -1f) v = -1f;
            pcm[i] = (short)Math.Round(v * 32767f);
        }

        byte[] payload = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, payload, 0, payload.Length);

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(36 + payload.Length);
            writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);
            writer.Write((short)1);                    //PCM
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(rate * channels * 2);         //bytes por segundo
            writer.Write((short)(channels * 2));       //alineación de bloque
            writer.Write((short)16);                   //bits por muestra
            writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(payload.Length);
            writer.Write(payload);
        }
    }

    private static void SafeDelete(string path)
    {
        try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { }
    }

    private static bool ControlHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
    }

    private static bool ShiftHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private static bool EnterDown()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }
}
