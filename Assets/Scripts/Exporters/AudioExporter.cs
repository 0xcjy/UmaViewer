using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using NAudio.Lame;
using UmaMusumeAudio;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using UnityEngine;
/// <summary>
/// 使用多线程,防止导出音频时使用主线程导致UI卡死
/// </summary>
public class AudioExporter
{
    public static IEnumerator ExportAudio(List<UmaWaveStream> audioStreams, string path, int index = -1)
    {
        if (audioStreams == null || audioStreams.Count == 0)
        {
            UmaViewerUI.Instance.ShowMessage("No audio available to export", UIMessageType.Warning);
            yield break;
        }

        if (index < -1 || index >= audioStreams.Count)
        {
            UmaViewerUI.Instance.ShowMessage("The selected audio track is no longer available", UIMessageType.Error);
            yield break;
        }

        // 在 Unity 主线程中复制选中的内容
        // 在解码、混音、编码以及写入 MP3 文件的整个过程中,工作线程（worker）独占并管理这个列表
        var selectedStreams = index == -1
            ? new List<UmaWaveStream>(audioStreams)
            : new List<UmaWaveStream> { audioStreams[index] };

        Task exportTask = Task.Run(() => ExportAudioBlocking(selectedStreams, path));

        // 协程依然运行在 Unity 主线程中,不过现在它只负责等待后台工作线程完成任务
        // 真正耗时的导出工作已经交给工作线程执行
        while (!exportTask.IsCompleted)
        {
            yield return null;
        }

        if (exportTask.IsFaulted)
        {
            Exception exception = exportTask.Exception?.GetBaseException();
            if (exception != null)
            {
                Debug.LogException(exception);
            }

            UmaViewerUI.Instance.ShowMessage(
                $"Audio export failed: {exception?.Message ?? "Unknown error"}",
                UIMessageType.Error);
            yield break;
        }

        if (exportTask.IsCanceled)
        {
            UmaViewerUI.Instance.ShowMessage("Audio export was canceled", UIMessageType.Warning);
            yield break;
        }

        UmaViewerUI.Instance.ShowMessage("Export complete", UIMessageType.Default);
    }

    private static void ExportAudioBlocking(List<UmaWaveStream> audioStreams, string path)
    {
        var sampleProviders = new List<ISampleProvider>(audioStreams.Count);

        foreach (var stream in audioStreams)
        {
            sampleProviders.Add(GetSampleFixedLength(stream));
        }

        var waveFormat = audioStreams[0].WaveFormat;

        var mixer = new MixingSampleProvider(sampleProviders);
        IWaveProvider sourceProvider = new SampleToWaveProvider16(mixer);

        using (var writer = new LameMP3FileWriter(path, new NAudio.Wave.WZT.WaveFormat(waveFormat.SampleRate, waveFormat.Channels), 320))
        {
            byte[] buffer = new byte[waveFormat.AverageBytesPerSecond];
            int bytesRead;

            while ((bytesRead = sourceProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                writer.Write(buffer, 0, bytesRead);
            }
        }
    }

    private static ISampleProvider GetSampleFixedLength(UmaWaveStream sample)
    {
        var blah = sample.ToSampleProvider().Take(sample.TotalTime);
        return blah;
    }
}
