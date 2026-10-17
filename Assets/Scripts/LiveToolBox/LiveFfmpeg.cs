using System;
using System.Collections.Concurrent;
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

    //Une un mux de audio a un vídeo ya terminado, copiando el flujo de vídeo (sin recomprimir).
    public static bool MuxAudio(string ffmpeg, string videoNoAudio, string audioWav, string finalVideo,
                               out string error)
    {
        error = null;
        var args = new StringBuilder();
        args.Append("-hide_banner -loglevel error -y ");
        args.Append($"-i \"{videoNoAudio}\" ");
        args.Append($"-i \"{audioWav}\" ");
        args.Append("-map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 320k -shortest -movflags +faststart ");
        args.Append($"\"{finalVideo}\"");
        return Run(ffmpeg, args.ToString(), out error);
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

    private struct Frame { public byte[] Buffer; public int Length; }

    public int FrameBytes => _frameBytes;
    public bool HasFailed { get; private set; }
    public string Error { get; private set; }

    //'filterChain' es opcional (por ejemplo el reescalado del supermuestreo). Si es null/vacío no se
    //añade ningún filtro de vídeo.
    public LiveFfmpegVideoPipe(string ffmpeg, string videoOutputPath,
                              int renderWidth, int renderHeight, int fps,
                              string filterChain, int poolSize = 4)
    {
        _frameBytes = renderWidth * renderHeight * 3;
        //Colchón entre render y codificación: cuanto más grande, menos se bloquea el hilo principal
        //esperando a FFmpeg; se acota por memoria (≈256 MB como mucho).
        int capacity = Mathf.Clamp(268435456 / Mathf.Max(_frameBytes, 1), 4, 24);
        poolSize = Mathf.Clamp(poolSize, 2, capacity);
        _queue = new BlockingCollection<Frame>(capacity);

        var args = new StringBuilder();
        args.Append("-hide_banner -loglevel error -y ");
        //Entrada de vídeo crudo por stdin.
        args.Append($"-f rawvideo -pixel_format rgb24 -video_size {renderWidth}x{renderHeight} ");
        args.Append($"-framerate {fps} -i pipe:0 ");
        if (!string.IsNullOrEmpty(filterChain))
            args.Append($"-vf \"{filterChain}\" ");
        //H.264 con un preset rápido y calidad alta constante; yuv420p para que lo abran todos los
        //reproductores/redes sociales.
        args.Append("-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p ");
        args.Append("-movflags +faststart ");
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

        for (int i = 0; i < poolSize; i++) _pool.Enqueue(new byte[_frameBytes]);

        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "LiveFfmpegWriter" };
        _writer.Start();
    }

    //Devuelve un buffer libre del pool (o uno nuevo si el pool está vacío).
    public byte[] Rent()
    {
        return _pool.TryDequeue(out var buffer) ? buffer : new byte[_frameBytes];
    }

    //Encola un frame lleno. Bloquea si hay demasiados frames pendientes (regulación de memoria).
    public void Write(byte[] buffer, int length)
    {
        if (HasFailed || _queue.IsAddingCompleted) { _pool.Enqueue(buffer); return; }
        try { _queue.Add(new Frame { Buffer = buffer, Length = length }); }
        catch (InvalidOperationException) { _pool.Enqueue(buffer); }
    }

    private void WriteLoop()
    {
        try
        {
            foreach (var frame in _queue.GetConsumingEnumerable())
            {
                if (HasFailed) { _pool.Enqueue(frame.Buffer); continue; }
                try { _stdin.Write(frame.Buffer, 0, frame.Length); }
                catch (Exception e) { Fail(e.Message); }
                _pool.Enqueue(frame.Buffer);
            }
        }
        catch (Exception e) { Fail(e.Message); }
    }

    private void Fail(string message)
    {
        if (HasFailed) return;
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
        try { if (!_queue.IsAddingCompleted) _queue.CompleteAdding(); } catch { }
        try { if (_process != null && !_process.HasExited) _process.Kill(); } catch { }
        try { _process?.Dispose(); } catch { }
    }
}
