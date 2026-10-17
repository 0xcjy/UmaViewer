using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using UmaMusumeAudio;
using UnityEngine;

//(Grabación del live) Mezcla OFFLINE del audio del show a un WAV.
//
//Grabación offline
//-----------------
//Durante la grabación el vídeo no va en tiempo real (se avanza frame a frame, tan rápido como dé la
//máquina), así que no se puede "escuchar y grabar" la salida: la mezcla audible iría a otra
//velocidad. Por eso el audio se calcula aparte, directamente desde los datos:
//
//  · La tabla `_part` de la canción dice, para cada momento, QUÉ onda (variante de voz) de cada
//    pista suena y con qué volumen/paneo. Es la misma decisión que toma `UmaViewerAudio.AlterUpdate`
//    en vivo, con las mismas tablas por defecto.
//  · Cada .awb de voz/música se decodifica con el mismo lector HCA que usa el viewer, bloque a
//    bloque (no carga la canción entera en memoria).
//
//El resultado es un WAV de la duración del tramo grabado, que FFmpeg muxa con el vídeo.
public static class LiveAudioMixdown
{
    private const int OutputChannels = 2;

    //Datos de mezcla de UNA pista de voz en UN tramo de la tabla.
    private struct VocalSegment
    {
        public List<int> Waves;   //índices de onda (0-based) que suenan
        public float Volume;
        public float Pan;
    }

    //Una pista del show (música o una voz) con sus decodificadores.
    private sealed class Track
    {
        public UmaViewerAudio.CuteAudioSource Source;
        public List<UmaWaveStream> Streams = new List<UmaWaveStream>();
        public ISampleProvider Sample;
        public int ActiveWave = -1;
        public long FramesRead;
        public int Rate;
        public int Channels = 2;

        public void Close()
        {
            Sample = null;
            foreach (var stream in Streams)
            {
                try { stream.Dispose(); } catch { }
            }
            Streams.Clear();
        }
    }

    //Devuelve null si todo fue bien, o el mensaje de error.
    public static string Render(Gallop.Live.Director director, LiveRecordSettings settings, string outputWav)
    {
        try
        {
            return RenderInternal(director, settings, outputWav);
        }
        catch (Exception e)
        {
            return e.ToString();
        }
    }

    private static string RenderInternal(Gallop.Live.Director director, LiveRecordSettings settings, string outputWav)
    {
        var partInfo = director.partInfo;
        var vocals = director.liveVocal ?? new List<UmaViewerAudio.CuteAudioSource>();
        var music = director.liveMusic;

        //Pistas que tienen datos decodificables.
        var tracks = new List<Track>();
        var trackIsMusic = new List<bool>();
        var vocalsByTrack = new List<UmaViewerAudio.CuteAudioSource>();

        if (music != null && music.sourceList != null && music.sourceList.Count > 0)
        {
            var t = Open(music);
            if (t != null) { tracks.Add(t); trackIsMusic.Add(true); vocalsByTrack.Add(null); }
        }
        foreach (var vocal in vocals)
        {
            if (vocal == null || vocal.sourceList == null || vocal.sourceList.Count == 0) continue;
            var t = Open(vocal);
            if (t != null) { tracks.Add(t); trackIsMusic.Add(false); vocalsByTrack.Add(vocal); }
        }

        if (tracks.Count == 0) return "No decodable audio tracks for the mixdown.";

        int rate = tracks[0].Rate;
        foreach (var t in tracks)
        {
            if (t.Rate != rate)
            {
                Close(tracks);
                return $"Audio tracks have different sample rates ({t.Rate} vs {rate}); the offline mixdown needs them equal.";
            }
        }

        float start = Mathf.Max(0f, settings.startTime);
        float end = settings.endTime > 0f ? settings.endTime : director.totalTime;
        if (end <= start) { Close(tracks); return "The audio range is empty."; }

        int totalFrames = Mathf.Max(1, Mathf.RoundToInt((end - start) * rate));
        long startFrame = (long)Math.Round(start * rate);

        //Segmentos de la tabla `_part`: dentro de uno, el reparto de ondas/volúmenes no cambia.
        var times = SegmentTimes(partInfo, end);

        Directory.CreateDirectory(Path.GetDirectoryName(outputWav));
        using (var file = new FileStream(outputWav, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        using (var writer = new BinaryWriter(file))
        {
            WriteWavHeader(writer, totalFrames, rate);
            var mix = new float[65536 * OutputChannels];
            var scratch = new float[65536 * OutputChannels];

            int frame = 0;
            int segmentIndex = 0;
            while (frame < totalFrames)
            {
                float t0 = start + (float)frame / rate;
                //Índice de fila de la tabla válido para t0 (mayor `time` <= t0).
                while (segmentIndex + 1 < times.Count && times[segmentIndex + 1] <= t0) segmentIndex++;

                int segmentFrames = totalFrames - frame;
                if (segmentIndex + 1 < times.Count)
                    segmentFrames = Mathf.Min(segmentFrames,
                        Mathf.Max(1, Mathf.RoundToInt((times[segmentIndex + 1] - t0) * rate)));

                while (segmentFrames > 0)
                {
                    int block = Mathf.Min(segmentFrames, mix.Length / OutputChannels);
                    Array.Clear(mix, 0, block * OutputChannels);

                    for (int i = 0; i < tracks.Count; i++)
                    {
                        var t = tracks[i];
                        if (t.Channels != OutputChannels && t.Channels != 1) continue;

                        List<VocalSegment> segments = trackIsMusic[i]
                            ? MusicSegments(t)
                            : VocalSegmentsFor(partInfo, vocals, vocalsByTrack[i], segmentIndex, director._capSameVoice, director.liveVocal.Count < 2);

                        foreach (var seg in segments)
                        {
                            foreach (int wave in seg.Waves)
                            {
                                if (wave < 0 || wave >= t.Streams.Count) continue;
                                long srcStart = startFrame + frame;
                                int got = ReadFrames(t, wave, srcStart, scratch, block);
                                float gL, gR;
                                PanGains(seg.Pan, out gL, out gR);
                                gL *= seg.Volume;
                                gR *= seg.Volume;

                                for (int n = 0; n < block; n++)
                                {
                                    float sl = 0f, sr = 0f;
                                    if (n < got)
                                    {
                                        if (t.Channels == 1) { sl = sr = scratch[n]; }
                                        else { sl = scratch[n * 2]; sr = scratch[n * 2 + 1]; }
                                    }
                                    mix[n * 2] += sl * gL;
                                    mix[n * 2 + 1] += sr * gR;
                                }
                            }
                        }
                    }

                    for (int n = 0; n < block * OutputChannels; n++)
                    {
                        float v = mix[n];
                        if (v > 1f) v = 1f; else if (v < -1f) v = -1f;
                        writer.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
                    }

                    frame += block;
                    segmentFrames -= block;
                }
            }

            //Parchea los tamaños del WAV con los frames realmente escritos.
            long dataBytes = (long)totalFrames * OutputChannels * sizeof(short);
            file.Seek(4, SeekOrigin.Begin); writer.Write((uint)(36 + dataBytes));
            file.Seek(40, SeekOrigin.Begin); writer.Write((uint)dataBytes);
        }

        Close(tracks);
        return null;
    }

    private static void Close(List<Track> tracks)
    {
        foreach (var t in tracks) t.Close();
    }

    private static Track Open(UmaViewerAudio.CuteAudioSource source)
    {
        if (source.awb == null || source.awb.FilePath == null || !File.Exists(source.awb.FilePath)) return null;
        var streams = UmaViewerBuilder.Instance.LoadAudioStreams(source.awb);
        if (streams == null || streams.Count == 0) return null;
        var track = new Track { Source = source, Streams = streams };
        track.Rate = streams[0].WaveFormat.SampleRate;
        track.Channels = streams[0].WaveFormat.Channels;
        return track;
    }

    //Lee 'frames' frames de la onda indicada a partir del frame 'startFrame'. Devuelve los frames
    //realmente leídos (los que falten se tratan como silencio). Reutiliza el decodificador mientras
    //la onda no cambie y el avance sea continuo.
    private static int ReadFrames(Track track, int wave, long startFrame, float[] buffer, int frames)
    {
        var stream = track.Streams[wave];
        if (track.ActiveWave != wave || track.Sample == null)
        {
            track.Sample = stream.ToSampleProvider();
            track.ActiveWave = wave;
            track.FramesRead = -1;   //fuerza el seek
        }

        if (track.FramesRead != startFrame)
        {
            stream.Position = startFrame * track.Channels * sizeof(short);
            track.FramesRead = startFrame;
        }

        int wanted = frames * track.Channels;
        int read = 0;
        try { read = track.Sample.Read(buffer, 0, wanted); }
        catch { read = 0; }
        if (read < 0) read = 0;
        track.FramesRead += read / track.Channels;
        return read / track.Channels;
    }

    //Paneo "constant gain": el centro no cambia de nivel y los extremos reparten.
    private static void PanGains(float pan, out float left, out float right)
    {
        if (pan < 0f) { left = 1f; right = 1f + pan; }
        else { left = 1f - pan; right = 1f; }
    }

    //Límites de tiempo (segundos) de los segmentos de la tabla `_part`, recortados al tramo.
    private static List<float> SegmentTimes(PartEntry partInfo, float end)
    {
        var result = new List<float> { 0f };
        if (partInfo != null && partInfo.PartSettings.TryGetValue("time", out var times) && times != null)
        {
            foreach (float ms in times)
            {
                float t = ms / 1000f;
                if (t <= 0f || t >= end) continue;
                if (t > result[result.Count - 1]) result.Add(t);
            }
        }
        result.Add(end);
        return result;
    }

    //La música no pasa por la tabla: suenan todas sus ondas a volumen 1 y sin paneo.
    private static List<VocalSegment> MusicSegments(Track track)
    {
        var waves = new List<int>();
        for (int i = 0; i < track.Streams.Count; i++) waves.Add(i);
        return new List<VocalSegment> { new VocalSegment { Waves = waves, Volume = 1f, Pan = 0f } };
    }

    //Reparto de ondas/volumen/paneo de UNA pista de voz en la fila 'row' de la tabla.
    //
    //Es la misma cuenta que `UmaViewerAudio.AlterUpdate`: se replican las tablas por defecto y la
    //regla de "no más de 3 pistas con la misma voz a la vez". En modo solista (una sola pista) la
    //tabla no se consulta: suenan todas las ondas completas.
    private static List<VocalSegment> VocalSegmentsFor(PartEntry data,
                                                       List<UmaViewerAudio.CuteAudioSource> vocals,
                                                       UmaViewerAudio.CuteAudioSource target,
                                                       int row, bool capSameVoice, bool soloMode)
    {
        var result = new List<VocalSegment>();

        if (soloMode)
        {
            var waves = new List<int>();
            for (int i = 0; i < target.sourceList.Count; i++) waves.Add(i);
            result.Add(new VocalSegment { Waves = waves, Volume = 1f, Pan = 0f });
            return result;
        }

        if (data == null || !data.PartSettings.ContainsKey("time")) return result;

        string part = target.tag;
        if (!UmaViewerAudio.PartSlot.TryGetValue(part, out var slot)) return result;

        //Tope de pistas con la misma voz: misma regla que la mezcla en vivo.
        var silenced = new bool[7];
        UmaViewerAudio.ComputeSilencedSlots(capSameVoice, vocals, data, row, silenced);

        int activeCount = 0;
        foreach (string name in UmaViewerAudio.OrderedParts)
        {
            if (UmaViewerAudio.FindVocal(vocals, name) == null) continue;
            int index = (int)UmaViewerAudio.PartValue(data, name, row, 0f);
            if (UmaViewerAudio.PartSlot.TryGetValue(name, out var sl) && silenced[sl]) index = 0;
            if (index > 0) activeCount++;
        }

        float rate = UmaViewerAudio.PartValue(data, "volume_rate", row, 901f);
        if (rate >= 900f)
            rate = activeCount < UmaViewerAudio.DefaultVolumeRates.Length
                ? UmaViewerAudio.DefaultVolumeRates[activeCount] : 1f;

        int rank = 0;
        foreach (string name in UmaViewerAudio.OrderedParts)
        {
            if (UmaViewerAudio.FindVocal(vocals, name) == null) continue;
            int index = (int)UmaViewerAudio.PartValue(data, name, row, 0f);
            if (UmaViewerAudio.PartSlot.TryGetValue(name, out var sl) && silenced[sl]) index = 0;
            bool active = index > 0;
            int volumeRow = activeCount < UmaViewerAudio.DefaultVolumeIndices.Length ? activeCount : 0;
            int column = active && volumeRow > 0 ? rank++ : 0;
            if (!active) volumeRow = 0;

            if (name != part) continue;
            if (!active || silenced[slot] || index - 1 < 0 || index - 1 >= target.sourceList.Count) return result;

            float volume = UmaViewerAudio.PartValue(data, name + "_vol", row, 901f);
            float pan = UmaViewerAudio.PartValue(data, name + "_pan", row, 901f);
            if (volume >= 900f) volume = UmaViewerAudio.DefaultVolumes[UmaViewerAudio.DefaultVolumeIndices[volumeRow][column]];
            if (pan >= 900f) pan = UmaViewerAudio.DefaultPans[volumeRow][column];

            result.Add(new VocalSegment { Waves = new List<int> { index - 1 }, Volume = volume * rate, Pan = pan });
            return result;
        }

        return result;
    }

    private static void WriteWavHeader(BinaryWriter writer, int totalFrames, int rate)
    {
        long dataBytes = (long)totalFrames * OutputChannels * sizeof(short);
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write((uint)(36 + dataBytes));
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((ushort)1);                                            //PCM
        writer.Write((ushort)OutputChannels);
        writer.Write(rate);
        writer.Write(rate * OutputChannels * sizeof(short));                //byte rate
        writer.Write((ushort)(OutputChannels * sizeof(short)));             //block align
        writer.Write((ushort)16);                                           //bits
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write((uint)dataBytes);
    }
}
