using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

//(Grabación del live · diagnóstico) Perfil del bucle de grabación offline.
//
//QUÉ HACE
//-------
//Mide, frame a frame, en qué se va el tiempo del bucle de grabación: actualización de la escena y
//envío de las cámaras, espera hasta el fin de frame, composición de la letra, lectura de píxeles
//(que sincroniza la GPU y suele ser el sospechoso), copia del búfer y espera a FFmpeg.
//
//Además, antes de grabar (sólo con el perfil activo) se hacen dos SONDAS para separar los costes:
//  · 'p' render sin leer píxeles (la GPU se vacía cada 8 frames): coste del bucle, sin captura.
//  · 'q' el frame en que toca vaciar (render + composición + lectura de píxeles).
//  · 'b' render + lectura de píxeles EN CADA frame, sin enviar nada a FFmpeg.
//  · 'c' la grabación de verdad (render + lectura + copia + FFmpeg).
//Comparando 'p'/'b'/'c' se ve cuánto cuesta la lectura de píxeles y cuánto el codificador.
//
//ACTIVACIÓN (si está apagado no cuesta nada ni ensucia la salida)
//---------------------------------------------------------------
//  · variable de entorno `UMA_LIVE_RECORD_PROFILE=1`, o
//  · fichero `record-profile.enable` en la carpeta de grabaciones (o junto al ejecutable/proyecto), o
//  · `LiveRecordProfile.Enabled = true` desde código.
//
//La salida son dos ficheros junto al vídeo: `<nombre>.profile.csv` (una fila por frame) y
//`<nombre>.profile.txt` (resumen). Ver LiveRecordDirector.cs.
public static class LiveRecordProfile
{
    public const string MarkerFileName = "record-profile.enable";
    private const string EnvVar = "UMA_LIVE_RECORD_PROFILE";
    private const int MaxSamples = 500000;

    //Muestra de UN frame del bucle.
    public struct Sample
    {
        public char phase;      //'p'/'q'/'b' sonda, 'c' captura real
        public int frame;       //índice dentro del tramo
        public double totalMs;  //del inicio de este frame al inicio del siguiente
        public double submitMs; //Update/LateUpdate de la escena + envío de las cámaras
        public double waitMs;   //del final de las cámaras al fin de frame
        public double resolveMs;//reducción del supermuestreo en GPU (8K -> tamaño de salida)
        public double blitMs;   //composición de la letra
        public double readMs;   //ReadPixels (incluye esperar a que la GPU termine)
        public double copyMs;   //copia a un búfer del pool
        public double writeMs;  //espera a FFmpeg (contrapresión del codificador)
        public double gapMs;    //del final de la captura al inicio del frame siguiente
        public int pending;     //frames encolados hacia FFmpeg al escribirse este frame
    }

    public static bool Enabled { get; private set; }
    public static string EnabledBy { get; private set; } = "";

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly List<Sample> Samples = new List<Sample>(8192);
    private static readonly List<string> Notes = new List<string>();
    private static string _folder;
    private static string _name;
    private static bool _dumped;

    public static double NowMs => Clock.Elapsed.TotalMilliseconds;
    public static int Count => Samples.Count;

    //Prepara la salida del perfil. 'folder'/'name' son los del vídeo (los .profile.* van al lado).
    public static void Configure(string folder, string name)
    {
        _folder = folder;
        _name = name;
        Samples.Clear();
        Notes.Clear();
        _dumped = false;
    }

    //Decide si el perfil está activo. Se llama una vez al empezar la grabación.
    public static bool Resolve()
    {
        if (Enabled) return true;

        string env = Environment.GetEnvironmentVariable(EnvVar);
        if (!string.IsNullOrWhiteSpace(env) && env.Trim() != "0")
        {
            Enabled = true;
            EnabledBy = EnvVar + "=" + env.Trim();
            return true;
        }

        foreach (string dir in new[] { LiveRecordSettings.DefaultFolder, Path.Combine(Application.dataPath, "..") })
        {
            try
            {
                string marker = Path.Combine(dir, MarkerFileName);
                if (File.Exists(marker))
                {
                    Enabled = true;
                    EnabledBy = marker;
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    public static void Reset()
    {
        Enabled = false;
        EnabledBy = "";
        Samples.Clear();
        Notes.Clear();
        _dumped = false;
    }

    public static void Note(string line)
    {
        Notes.Add(line);
        UnityEngine.Debug.Log("[LiveRecord/Profile] " + line);
    }

    public static void Push(Sample sample)
    {
        if (Samples.Count < MaxSamples) Samples.Add(sample);
    }

    //Cuánta memoria de vídeo piden los adjuntos de UN frame (color + profundidad con MSAA) contando la
    //escala de render de URP. Si el total no cabe en la tarjeta, el bucle pasa a depender de la memoria
    //y aparecen errores de URP del tipo "color surface null": eso NO es un límite de hilos, es la
    //configuración de render.
    public static string FootprintLine(int renderWidth, int renderHeight, int msaa, float renderScale)
    {
        float scale = Mathf.Max(0.01f, renderScale);
        int w = Mathf.RoundToInt(renderWidth * scale);
        int h = Mathf.RoundToInt(renderHeight * scale);
        int samples = Mathf.Max(1, msaa);
        long bytesPerTarget = (long)w * h * 4L * samples;
        long total = bytesPerTarget * 2L;   //color + profundidad
        int vramMb = SystemInfo.graphicsMemorySize;
        bool tooBig = vramMb > 0 && total > vramMb * 1024L * 1024L / 2L;
        return string.Format(
            "huella por frame: RT {0}x{1} × escala de render {2:0.##} = {3}x{4} con MSAA x{5} → color + profundidad ≈ {6:0.0} GB (VRAM {7} MB){8}",
            renderWidth, renderHeight, scale, w, h, samples, total / 1073741824.0, vramMb,
            tooBig ? "   ← NO CABE: la VRAM se queda corta" : "");
    }

    //Estado del motor y de los ajustes que pueden limitar el bucle por su cuenta. Se registra al
    //empezar para poder descartar (o confirmar) causas ajenas al grabador.
    public static string EnvironmentLine()
    {
        try
        {
            return string.Format(
                "engine: gpu={0} vram={1}MB cpu={2} editor={3} batch={4} focused={5} screen={6}x{7} " +
                "targetFps={8} vSync={9} captureDelta={10:0.#####} maxQueuedFrames={11} cfgTargetFps={12} cfgUiFps={13}",
                SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize, SystemInfo.processorCount,
                Application.isEditor, Application.isBatchMode, Application.isFocused,
                Screen.width, Screen.height, Application.targetFrameRate, QualitySettings.vSyncCount,
                Time.captureDeltaTime, QualitySettings.maxQueuedFrames,
                Config.Instance != null ? Config.Instance.GetTargetFrameRate() : -1,
                Config.Instance != null ? Config.Instance.GetUIFrameRate() : -1);
        }
        catch (Exception e)
        {
            return "engine: (no se pudo leer) " + e.Message;
        }
    }

    //Resumen en una línea de los últimos frames de una fase (para el cuadro de progreso).
    public static string LiveLine(char phase, int tail)
    {
        if (Samples.Count == 0) return "";
        int from = Math.Max(0, Samples.Count - tail);
        double total = 0, read = 0, write = 0, copy = 0, resolve = 0;
        int count = 0;
        for (int i = from; i < Samples.Count; i++)
        {
            if (Samples[i].phase != phase) continue;
            total += Samples[i].totalMs;
            read += Samples[i].readMs;
            write += Samples[i].writeMs;
            copy += Samples[i].copyMs;
            resolve += Samples[i].resolveMs;
            count++;
        }
        if (count == 0) return "";
        return string.Format("Profile  frame {0:0} ms  (resolve {1:0} · readback {2:0} · copy {3:0} · ffmpeg {4:0})",
            total / count, resolve / count, read / count, copy / count, write / count);
    }

    //Resumen completo: una tabla por fase con media/mediana/p95/mín/máx y el desglose por etapas.
    public static string Summary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("LiveRecord profile  (" + (Enabled ? "activo: " + EnabledBy : "no activo") + ")");
        foreach (string note in Notes) sb.AppendLine(note);
        if (Samples.Count == 0) return sb.AppendLine("(sin muestras)").ToString();

        sb.AppendLine();
        sb.AppendLine("fase  frames    totalMs(avg/p50/p95/min/max)        submit  wait resolv  blit  read  copy  write   gap   pend");
        foreach (char phase in new[] { 'p', 'q', 'b', 'c' })
        {
            var group = new List<Sample>();
            foreach (var s in Samples) if (s.phase == phase) group.Add(s);
            if (group.Count == 0) continue;

            sb.AppendLine(string.Format("{0,-4}  {1,6}   {2,6:0.0}/{3,6:0.0}/{4,6:0.0}/{5,6:0.0}/{6,6:0.0}   " +
                                        "{7,6:0.0} {8,5:0.0} {9,5:0.0} {10,5:0.0} {11,5:0.0} {12,6:0.0} {13,5:0.0} {14,5:0.0} {15,5:0.0}",
                Describe(phase), group.Count,
                Avg(group, s => s.totalMs), Percentile(group, s => s.totalMs, 0.50),
                Percentile(group, s => s.totalMs, 0.95), Min(group, s => s.totalMs), Max(group, s => s.totalMs),
                Avg(group, s => s.submitMs), Avg(group, s => s.waitMs), Avg(group, s => s.resolveMs),
                Avg(group, s => s.blitMs), Avg(group, s => s.readMs), Avg(group, s => s.copyMs),
                Avg(group, s => s.writeMs), Avg(group, s => s.gapMs), Avg(group, s => s.pending)));
        }

        //Comparativa directa de las sondas con la grabación real: es lo que decide el siguiente paso.
        var p = Group('p');
        var b = Group('b');
        var c = Group('c');
        if (p.Count > 0 && b.Count > 0)
            sb.AppendLine(string.Format("\nlectura de píxeles por frame ≈ {0:0.0} ms ('b' - 'p')",
                Avg(b, s => s.totalMs) - Avg(p, s => s.totalMs)));
        if (p.Count > 0 && c.Count > 0)
            sb.AppendLine(string.Format("coste total de la captura ≈ {0:0.0} ms ('c' - 'p')", Avg(c, s => s.totalMs) - Avg(p, s => s.totalMs)));
        if (b.Count > 0 && c.Count > 0)
            sb.AppendLine(string.Format("codificador/copia ≈ {0:0.0} ms ('c' - 'b')", Avg(c, s => s.totalMs) - Avg(b, s => s.totalMs)));
        if (c.Count > 0)
            sb.AppendLine(string.Format("grabación: {0:0.0} ms/frame → {1:0.0} fps de vídeo muestreado",
                Avg(c, s => s.totalMs), 1000.0 / Math.Max(0.001, Avg(c, s => s.totalMs))));
        return sb.ToString();
    }

    //Escribe el CSV (una fila por frame) y el resumen junto al vídeo. Se puede llamar varias veces.
    public static void Dump()
    {
        if (!Enabled || _dumped || string.IsNullOrEmpty(_folder) || string.IsNullOrEmpty(_name)) return;
        _dumped = true;
        try
        {
            Directory.CreateDirectory(_folder);

            var csv = new StringBuilder();
            csv.AppendLine("phase,frame,totalMs,submitMs,waitMs,resolveMs,blitMs,readMs,copyMs,writeMs,gapMs,pending");
            foreach (var s in Samples)
                csv.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0},{1},{2:0.###},{3:0.###},{4:0.###},{5:0.###},{6:0.###},{7:0.###},{8:0.###},{9:0.###},{10:0.###},{11}",
                    s.phase, s.frame, s.totalMs, s.submitMs, s.waitMs, s.resolveMs, s.blitMs, s.readMs, s.copyMs,
                    s.writeMs, s.gapMs, s.pending));
            File.WriteAllText(Path.Combine(_folder, _name + ".profile.csv"), csv.ToString());

            string summary = Summary();
            File.WriteAllText(Path.Combine(_folder, _name + ".profile.txt"), summary);
            UnityEngine.Debug.Log("[LiveRecord/Profile] Resumen escrito en " + Path.Combine(_folder, _name + ".profile.txt") + "\n" + summary);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[LiveRecord/Profile] No se pudo escribir el perfil: " + e.Message);
        }
    }

    //======================================================================
    // Agregación
    //======================================================================

    private static string Describe(char phase)
    {
        switch (phase)
        {
            case 'p': return "p-render";
            case 'q': return "q-drain";
            case 'b': return "b-read";
            case 'c': return "c-capture";
            default: return phase.ToString();
        }
    }

    private static List<Sample> Group(char phase)
    {
        var group = new List<Sample>();
        foreach (var s in Samples) if (s.phase == phase) group.Add(s);
        return group;
    }

    private static double Avg(List<Sample> group, Func<Sample, double> pick)
    {
        if (group.Count == 0) return 0.0;
        double sum = 0;
        foreach (var s in group) sum += pick(s);
        return sum / group.Count;
    }

    private static double Min(List<Sample> group, Func<Sample, double> pick)
    {
        if (group.Count == 0) return 0.0;
        double best = double.MaxValue;
        foreach (var s in group) best = Math.Min(best, pick(s));
        return best;
    }

    private static double Max(List<Sample> group, Func<Sample, double> pick)
    {
        if (group.Count == 0) return 0.0;
        double best = double.MinValue;
        foreach (var s in group) best = Math.Max(best, pick(s));
        return best;
    }

    private static double Percentile(List<Sample> group, Func<Sample, double> pick, double fraction)
    {
        if (group.Count == 0) return 0.0;
        var values = new List<double>(group.Count);
        foreach (var s in group) values.Add(pick(s));
        values.Sort();
        int index = (int)Math.Round((values.Count - 1) * fraction);
        return values[Math.Max(0, Math.Min(values.Count - 1, index))];
    }
}
