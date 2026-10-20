using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

//(Grabación del live) Utilidades de FFmpeg.
//
//El vídeo se compone fuera del motor: los frames se renderizan a un RenderTexture, se leen como
//bytes crudos (RGB24) y se envían por la entrada estándar de FFmpeg, que los codifica a H.264 en
//paralelo. Al final se muxa el WAV de la mezcla. Así no hace falta ningún codificador dentro de
//Unity y funciona igual en el Editor y en una build.
public static class LiveFfmpeg
{
    //Busca el ejecutable de FFmpeg. Primero el que fije Config, luego las ubicaciones típicas
    //(junto al proyecto/ejecutable y StreamingAssets) y por último el PATH del sistema.
    public static string Locate()
    {
        var candidates = new System.Collections.Generic.List<string>();

        if (Config.Instance != null && !string.IsNullOrWhiteSpace(Config.Instance.FfmpegPath))
            candidates.Add(Config.Instance.FfmpegPath.Trim());

        candidates.Add(Path.Combine(Application.streamingAssetsPath, "ffmpeg", ExeName));
        candidates.Add(Path.Combine(Application.streamingAssetsPath, ExeName));
        candidates.Add(Path.Combine(Application.dataPath, "..", ExeName));
        candidates.Add(Path.Combine(Application.dataPath, "..", "ffmpeg", ExeName));
        candidates.Add(Path.Combine(Application.dataPath, "..", "..", "ffmpeg", "bin", ExeName));
        candidates.Add(Path.Combine(Application.persistentDataPath, ExeName));

        string env = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(env)) candidates.Insert(0, env.Trim());

        foreach (string candidate in candidates)
        {
            try
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch { }
        }

        //PATH del sistema.
        string path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (string dir in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    string full = Path.Combine(dir.Trim(), ExeName);
                    if (File.Exists(full)) return Path.GetFullPath(full);
                }
                catch { }
            }
        }

        return null;
    }

    private static string ExeName => Application.platform == RuntimePlatform.WindowsPlayer ||
                                     Application.platform == RuntimePlatform.WindowsEditor
        ? "ffmpeg.exe" : "ffmpeg";

    //Argumentos del codificador de vídeo según el nivel:
    //  0 Low    · H.264 4:2:0 8 bits, cq 24 (rápida, archivos pequeños)
    //  1 Normal · H.264 4:2:0 8 bits, cq 19
    //  2 High   · HEVC 4:4:4 8 bits (croma completa, sin máster)
    //  3 Max    · HEVC 4:4:4 10 bits (croma completa y 10 bits, sin banding)
    //Con NVENC la codificación va al chip dedicado de la GPU: no cuesta tiempo y libera la CPU.
    public static string VideoEncoderArgs(int quality, bool nvencH264, bool nvencHevc,
                                          bool nvencHevc444, bool nvencHevc44410)
    {
        if (quality >= 3)
        {
            if (nvencHevc44410)
                return "-c:v hevc_nvenc -preset p7 -tune hq -rc vbr -cq 18 -b:v 0 -spatial-aq 1 -pix_fmt yuv444p10le -tag:v hvc1 ";
            if (nvencHevc444)
                return "-c:v hevc_nvenc -preset p7 -tune hq -rc vbr -cq 17 -b:v 0 -spatial-aq 1 -pix_fmt yuv444p -tag:v hvc1 ";
            if (nvencHevc)
                return "-c:v hevc_nvenc -preset p7 -tune hq -rc vbr -cq 17 -b:v 0 -spatial-aq 1 -pix_fmt yuv420p10le -tag:v hvc1 ";
            return "-c:v libx264 -preset slow -crf 12 -pix_fmt yuv444p ";
        }

        if (quality >= 2)
        {
            if (nvencHevc444)
                return "-c:v hevc_nvenc -preset p7 -tune hq -rc vbr -cq 18 -b:v 0 -spatial-aq 1 -pix_fmt yuv444p -tag:v hvc1 ";
            if (nvencHevc)
                return "-c:v hevc_nvenc -preset p7 -tune hq -rc vbr -cq 19 -b:v 0 -spatial-aq 1 -pix_fmt yuv420p -tag:v hvc1 ";
            return "-c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p ";
        }

        if (nvencH264)
            return quality >= 1
                ? "-c:v h264_nvenc -preset p7 -tune hq -rc vbr -cq 16 -b:v 0 -spatial-aq 1 -aq-strength 9 -pix_fmt yuv420p "
                : "-c:v h264_nvenc -preset p4 -tune hq -rc vbr -cq 24 -b:v 0 -pix_fmt yuv420p ";

        return quality >= 1
            ? "-c:v libx264 -preset slow -crf 15 -pix_fmt yuv420p "
            : "-c:v libx264 -preset veryfast -crf 23 -pix_fmt yuv420p ";
    }

    //Argumentos del máster sin pérdida: FFV1 en RGB (exactamente lo que renderiza el motor, sin matriz
    //de color), con checksum por slice. Va en contenedor Matroska.
    public const string MasterEncoderArgs = "-c:v ffv1 -level 3 -g 1 -slices 16 -slicecrc 1 -pix_fmt bgr0 ";

    //Nombre legible del encoder que se va a usar (para el panel y el registro).
    public static string EncoderLabel(int quality, bool nvencH264, bool nvencHevc,
                                      bool nvencHevc444, bool nvencHevc44410)
    {
        if (quality >= 3)
            return nvencHevc44410 ? "HEVC 4:4:4 10-bit"
                 : (nvencHevc444 ? "HEVC 4:4:4 8-bit" : (nvencHevc ? "HEVC 10-bit" : "x264 4:4:4 (CPU)"));
        if (quality >= 2)
            return nvencHevc444 ? "HEVC 4:4:4 8-bit" : (nvencHevc ? "HEVC 4:2:0" : "x264 (CPU)");
        return nvencH264 ? "H.264 NVENC" : "x264 (CPU)";
    }

    //¿Existe y funciona este encoder (con este formato de píxel)? Se prueba una sola vez por sesión con
    //un frame sintético. El formato importa: h264_nvenc puede existir y no soportar 4:4:4.
    private static readonly Dictionary<string, bool> _encoders = new Dictionary<string, bool>();
    public static bool SupportsEncoder(string ffmpeg, string encoder, string pixelFormat = "yuv420p")
    {
        if (string.IsNullOrEmpty(ffmpeg)) return false;
        string key = encoder + "|" + pixelFormat;
        if (_encoders.TryGetValue(key, out bool known)) return known;

        bool ok = false;
        try
        {
            string args = "-hide_banner -loglevel error -f lavfi -i color=c=black:s=256x256:d=0.1 " +
                          "-c:v " + encoder + " -pix_fmt " + pixelFormat + " -f null -";
            ok = Run(ffmpeg, args, out _);
        }
        catch { ok = false; }

        _encoders[key] = ok;
        Debug.Log("[LiveRecord] Encoder " + encoder + " (" + pixelFormat + "): " + (ok ? "disponible" : "no disponible"));
        return ok;
    }

    //Ejecuta FFmpeg leyendo su salida de progreso (`-progress pipe:1`). 'onProgress' recibe los
    //segundos de salida ya procesados (0..duración), así se puede mostrar porcentaje y estimación.
    //Se llama desde un hilo aparte: el callback NO debe tocar la API de Unity.
    public static bool RunWithProgress(string ffmpeg, string args, Action<double> onProgress, out string error)
    {
        error = null;
        try
        {
            var info = new ProcessStartInfo(ffmpeg, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var proc = Process.Start(info))
            {
                var stderr = new StringBuilder();
                proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) lock (stderr) stderr.AppendLine(e.Data); };
                proc.BeginErrorReadLine();

                string line;
                while ((line = proc.StandardOutput.ReadLine()) != null)
                {
                    //ffmpeg emite bloques "clave=valor"; 'out_time' es hh:mm:ss.ffffff
                    if (line.StartsWith("out_time="))
                    {
                        string value = line.Substring("out_time=".Length);
                        if (TimeSpan.TryParse(value, out TimeSpan span) && onProgress != null)
                            onProgress(span.TotalSeconds);
                    }
                }

                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    lock (stderr)
                        error = stderr.Length > 0 ? stderr.ToString().Trim() : ("ffmpeg salió con código " + proc.ExitCode);
                    return false;
                }
                return true;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    //Une un mux de audio a un vídeo ya terminado, copiando el flujo de vídeo (sin recomprimir).
    //'losslessAudio' = copiar el PCM del WAV tal cual (para el máster .mkv); si no, AAC para MP4.
    public static bool MuxAudio(string ffmpeg, string videoNoAudio, string audioWav, string finalVideo,
                               out string error, bool losslessAudio = false, Action<double> onProgress = null)
    {
        error = null;
        var args = new StringBuilder();
        args.Append("-hide_banner -loglevel error -y -nostats -progress pipe:1 ");
        args.Append($"-i \"{videoNoAudio}\" ");
        args.Append($"-i \"{audioWav}\" ");
        args.Append("-map 0:v:0 -map 1:a:0 -c:v copy ");
        args.Append(losslessAudio ? "-c:a copy " : "-c:a aac -b:a 320k ");
        args.Append("-shortest ");
        if (!losslessAudio) args.Append("-movflags +faststart ");
        args.Append($"\"{finalVideo}\"");
        return RunWithProgress(ffmpeg, args.ToString(), onProgress, out error);
    }

    //Reencoda el máster sin pérdida a un MP4 de entrega. 'outputFps' > 0 remuestrea la cadencia (por
    //ejemplo, máster a 120 y entrega a 60: la mitad de frames y de tiempo de encode). Va informando del
    //progreso y se puede repetir con otros ajustes sin volver a grabar.
    public static bool ReencodeFromMaster(string ffmpeg, string masterPath, string outputMp4,
                                          string encoderArgs, out string error, Action<double> onProgress = null,
                                          int outputFps = 0)
    {
        error = null;
        if (!File.Exists(masterPath)) { error = "master not found: " + masterPath; return false; }
        var args = new StringBuilder();
        args.Append("-hide_banner -loglevel error -y -nostats -progress pipe:1 ");
        args.Append($"-i \"{masterPath}\" ");
        //Conversión explícita RGB -> YUV BT.709 rango limitado + etiquetas para que ningún reproductor
        //(ni YouTube) tenga que adivinar el espacio de color.
        var vf = new StringBuilder("scale=out_color_matrix=bt709:out_range=tv");
        if (outputFps > 0) vf.Append($",fps={outputFps}");
        vf.Append(",setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv");
        args.Append($"-vf \"{vf}\" ");
        args.Append(string.IsNullOrEmpty(encoderArgs) ? VideoEncoderArgs(1, false, false, false, false) : encoderArgs);
        args.Append("-c:a aac -b:a 320k -movflags +faststart ");
        args.Append($"\"{outputMp4}\"");
        return RunWithProgress(ffmpeg, args.ToString(), onProgress, out error);
    }

    private static bool Run(string ffmpeg, string args, out string error)
    {
        error = null;
        try
        {
            var info = new ProcessStartInfo(ffmpeg, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            using (var proc = Process.Start(info))
            {
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    error = string.IsNullOrWhiteSpace(err) ? ("ffmpeg salió con código " + proc.ExitCode) : err.Trim();
                    return false;
                }
                return true;
            }
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }
}

//(Grabación del live) Tubería de frames crudos hacia FFmpeg.
//
//El hilo principal de Unity sólo encola buffers (y se bloquea si FFmpeg no da abasto, que es la
//forma correcta de regular la memoria); un hilo aparte los escribe en la entrada estándar y
//devuelve los buffers al pool. Así la codificación se solapa con el render.
public sealed class LiveFfmpegVideoPipe : IDisposable
{
    private readonly Process _process;
    private readonly Stream _stdin;
    private readonly BlockingCollection<Frame> _queue;
    private readonly Thread _writer;
    private readonly ConcurrentQueue<byte[]> _pool = new ConcurrentQueue<byte[]>();
    private readonly int _frameBytes;
    private readonly StringBuilder _stderr = new StringBuilder();

    private struct Frame
    {
        public byte[] Buffer;
        public int Length;
    }

    //Diagnóstico (ver LiveRecordProfile.cs): cola pendiente, frames ya entregados al codificador y
    //tiempo de espera por contrapresión del propio FFmpeg.
    private volatile int _framesWritten;
    private long _writeTicks;
    private int _writeCalls;
    private int _backpressureCalls;
    private volatile bool _closing;

    public int FrameBytes => _frameBytes;
    public bool HasFailed { get; private set; }
    public string Error { get; private set; }

    public int Pending => _queue.Count;
    public int FramesWritten => _framesWritten;
    public int WriteCalls => _writeCalls;
    public int BackpressureCalls => _backpressureCalls;
    public double WriteMs => _writeTicks * 1000.0 / Stopwatch.Frequency;

    //'filterChain' es opcional (por ejemplo el reescalado del supermuestreo). Si es null/vacío no se
    //añade ningún filtro de vídeo. 'pixelFormat' tiene que coincidir con el formato con el que se lee
    //el RenderTexture (rgba es el nativo y el más rápido; rgb24 obliga a convertir en CPU).
    public LiveFfmpegVideoPipe(string ffmpeg, string videoOutputPath,
                              int renderWidth, int renderHeight, int fps,
                              string filterChain, int poolSize = 4, string pixelFormat = "rgb24",
                              string encoderArgs = null, bool matroska = false)
    {
        pixelFormat = string.IsNullOrEmpty(pixelFormat) ? "rgb24" : pixelFormat;
        int bytesPerPixel = pixelFormat == "rgba" || pixelFormat == "bgra" || pixelFormat == "argb" ||
                            pixelFormat == "abgr" ? 4 : 3;
        _frameBytes = renderWidth * renderHeight * bytesPerPixel;
        //Colchón entre render y codificación: el hilo principal sólo se bloquea si hay más frames
        //pendientes que éstos (el codificador va sobrado, así que en la práctica nunca espera).
        //Se acota por memoria: 512 MB de cola como máximo.
        int capacity = Mathf.Clamp(536870912 / Mathf.Max(_frameBytes, 1), 4, 32);
        poolSize = Mathf.Clamp(poolSize, 2, capacity);
        _queue = new BlockingCollection<Frame>(capacity);

        var args = new StringBuilder();
        args.Append("-hide_banner -loglevel error -y ");
        //Entrada de vídeo crudo por stdin.
        args.Append($"-f rawvideo -pixel_format {pixelFormat} -video_size {renderWidth}x{renderHeight} ");
        args.Append($"-framerate {fps} -i pipe:0 ");
        if (!string.IsNullOrEmpty(filterChain))
            args.Append($"-vf \"{filterChain}\" ");
        //H.264/HEVC; yuv420p para que lo abran todos los reproductores/redes sociales.
        args.Append(string.IsNullOrEmpty(encoderArgs) ? LiveFfmpeg.VideoEncoderArgs(0, false, false, false, false) : encoderArgs);
        //`faststart` reorganiza el índice al final: solo tiene sentido en MP4.
        if (!matroska) args.Append("-movflags +faststart ");
        args.Append($"\"{videoOutputPath}\"");

        var info = new ProcessStartInfo(ffmpeg, args.ToString())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };

        _process = Process.Start(info);
        _stdin = _process.StandardInput.BaseStream;
        _process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) lock (_stderr) _stderr.AppendLine(e.Data);
        };
        _process.BeginErrorReadLine();

        //Sólo se pre-reservan dos búferes: la lectura asíncrona (la normal al grabar) no copia nada y
        //no usa el pool; el resto se crean bajo demanda.
        for (int i = 0; i < Mathf.Min(poolSize, 2); i++) _pool.Enqueue(new byte[_frameBytes]);

        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "LiveFfmpegWriter" };
        _writer.Start();
    }

    //Devuelve un buffer libre del pool (o uno nuevo si el pool está vacío).
    public byte[] Rent()
    {
        return _pool.TryDequeue(out var buffer) ? buffer : new byte[_frameBytes];
    }

    //Encola un frame que ya está en memoria (la lectura asíncrona copia a un búfer de RAM y libera el
    //de GPU al instante, así que aquí siempre entra un byte[]).
    public void Write(byte[] buffer, int length)
    {
        long start = Stopwatch.GetTimestamp();
        if (HasFailed || _queue.IsAddingCompleted) { _pool.Enqueue(buffer); CountWrite(start); return; }
        try { _queue.Add(new Frame { Buffer = buffer, Length = length }); }
        catch (InvalidOperationException) { _pool.Enqueue(buffer); }
        CountWrite(start);
    }

    private void CountWrite(long start)
    {
        long ticks = Stopwatch.GetTimestamp() - start;
        _writeTicks += ticks;
        _writeCalls++;
        //Espera de verdad por contrapresión del codificador (no un simple cambio de contexto).
        if (ticks * 1000.0 / Stopwatch.Frequency > 2.0) _backpressureCalls++;
    }

    private void WriteLoop()
    {
        try
        {
            foreach (var frame in _queue.GetConsumingEnumerable())
            {
                if (HasFailed) { _pool.Enqueue(frame.Buffer); continue; }
                try
                {
                    _stdin.Write(frame.Buffer, 0, frame.Length);
                    _framesWritten++;
                }
                catch (ThreadAbortException) { return; }   //cierre abrupto (salir del Play Mode)
                catch (Exception e) { Fail(e.Message); }
                _pool.Enqueue(frame.Buffer);
            }
        }
        catch (ThreadAbortException) { }   //idem: la tubería muere con el proceso, no es un fallo del vídeo
        catch (Exception e) { Fail(e.Message); }
    }

    private void Fail(string message)
    {
        //Si ya se está cerrando (Complete/Dispose) la tubería o el motor está apagándose, no es un fallo
        //del vídeo: no se ensucia la consola con un error que no lo es.
        if (HasFailed || _closing) return;
        HasFailed = true;
        Error = message;
        Debug.LogError("[LiveRecord] Failed to write to FFmpeg: " + message);
    }

    //Cierra la entrada y espera a que FFmpeg termine de codificar.
    public bool Complete()
    {
        if (!_queue.IsAddingCompleted) _queue.CompleteAdding();
        try { _writer.Join(TimeSpan.FromMinutes(30)); } catch { }
        try { _stdin.Flush(); _stdin.Close(); } catch { }
        _process.WaitForExit();
        if (_process.ExitCode != 0 || HasFailed)
        {
            lock (_stderr)
            {
                Error = _stdin != null && _stderr.Length > 0 ? _stderr.ToString().Trim() : Error;
            }
            return false;
        }
        return true;
    }

    public void Dispose()
    {
        //A partir de aquí los errores de escritura ya no interesan: se está cerrando la tubería.
        _closing = true;
        try { if (!_queue.IsAddingCompleted) _queue.CompleteAdding(); } catch { }
        try { if (_process != null && !_process.HasExited) _process.Kill(); } catch { }
        //Espera a que el hilo escritor suelte el frame que tuviera entre manos: los búferes nativos los
        //libera el grabador después y no pueden liberarse mientras se están escribiendo.
        try { _writer?.Join(2000); } catch { }
        try { _process?.Dispose(); } catch { }
    }
}
