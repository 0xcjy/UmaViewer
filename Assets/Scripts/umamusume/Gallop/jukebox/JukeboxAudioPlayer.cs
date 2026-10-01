using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CriWareFormats;
using NAudio.Wave;
using UmaMusumeAudio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gallop
{
    /// <summary>
    /// Resolves the official jukebox cuesheet/cue pair to the matching ACB/AWB
    /// resources and exposes them through UmaViewer's regular AudioSource UI.
    /// </summary>
    internal sealed class JukeboxAudioPlayer : IDisposable
    {
        private GameObject _root;
        private readonly List<AudioSource> _sources = new List<AudioSource>();
        private readonly List<AudioClip> _clips = new List<AudioClip>();
        private readonly List<AwbReader> _awbReaders = new List<AwbReader>();
        private bool _prepared;
        private UmaDatabaseEntry _currentAwb;
        private int _currentWaveIndex = -1;

        public bool IsPrepared => _prepared;
        public bool IsPlaying => _sources.Any(source => source != null && source.isPlaying);

        public bool Prepare(
            MasterJukeboxMusicData.JukeboxMusicData music,
            bool loop,
            out string error)
        {
            return Prepare(music, false, loop, out error);
        }

        public bool Prepare(
            MasterJukeboxMusicData.JukeboxMusicData music,
            bool useGameSize,
            bool loop,
            out string error)
        {
            Clear();
            error = string.Empty;

            if (music == null)
            {
                error = "Jukebox music data is null.";
                return false;
            }

            if (!music.TryGetAudio(useGameSize, out string cuesheetName, out string cueName))
            {
                error = $"Music {music.MusicId} has no playable cue pair.";
                return false;
            }

            UmaDatabaseEntry awb = FindSoundEntry(cuesheetName, ".awb");
            if (awb == null)
            {
                error = $"AWB not found for cuesheet: {cuesheetName}";
                return false;
            }

            string awbPath;
            try
            {
                awbPath = awb.FilePath;
            }
            catch (Exception ex)
            {
                error = $"Could not obtain AWB {cuesheetName}: {ex.Message}";
                return false;
            }

            if (string.IsNullOrEmpty(awbPath) || !File.Exists(awbPath))
            {
                error = $"AWB file does not exist: {awbPath}";
                return false;
            }

            UmaDatabaseEntry acb = FindSoundEntry(cuesheetName, ".acb");
            List<int> waveIds;
            List<int> allWaveIds;

            try
            {
                allWaveIds = ReadWaveIds(awbPath);
                waveIds = ResolveCueWaveIds(acb, allWaveIds, cueName);
            }
            catch (Exception ex)
            {
                error = $"Could not resolve cue '{cueName}': {ex.Message}";
                return false;
            }

            if (waveIds.Count == 0)
            {
                if (allWaveIds.Count == 1)
                {
                    waveIds.Add(allWaveIds[0]);
                }
                else
                {
                    error =
                        $"Cue '{cueName}' was not found in '{cuesheetName}'. " +
                        $"The AWB contains {allWaveIds.Count} waves, so selecting a random wave was refused.";
                    return false;
                }
            }

            try
            {
                CreateAudioSources(awb, awbPath, allWaveIds, waveIds, loop);
            }
            catch (Exception ex)
            {
                Clear();
                error = $"Could not decode jukebox music {music.MusicId}: {ex.Message}";
                return false;
            }

            _prepared = _sources.Count > 0;
            if (!_prepared)
            {
                error = $"No AudioClip was created for music {music.MusicId}.";
                Clear();
                return false;
            }

            return true;
        }

        public void Play()
        {
            if (!_prepared)
                return;

            foreach (AudioSource source in _sources)
            {
                if (source != null && source.clip != null)
                    source.Play();
            }
        }

        public void Stop()
        {
            foreach (AudioSource source in _sources)
            {
                if (source != null)
                    source.Stop();
            }
        }

        public void Clear()
        {
            Stop();

            UmaViewerBuilder builder = UmaViewerBuilder.Instance;
            if (builder != null)
            {
                builder.CurrentAudioSources.RemoveAll(source =>
                    source == null || (_root != null && source.gameObject == _root));

                if (_currentAwb != null)
                {
                    builder.CurrentLiveSoundAWB.RemoveAll(entry => entry == _currentAwb);
                    builder.CurrentLiveSoundAWBIndex = -1;
                }
            }

            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            foreach (AudioClip clip in _clips)
            {
                if (clip != null)
                    Object.Destroy(clip);
            }
            _clips.Clear();
            _sources.Clear();

            foreach (AwbReader reader in _awbReaders)
            {
                try
                {
                    reader?.Dispose();
                }
                catch
                {
                }
            }
            _awbReaders.Clear();

            _currentAwb = null;
            _currentWaveIndex = -1;
            _prepared = false;
        }

        public void Dispose()
        {
            Clear();
        }

        private static UmaDatabaseEntry FindSoundEntry(string cuesheetName, string extension)
        {
            UmaViewerMain main = UmaViewerMain.Instance;
            if (main == null || main.AbSounds == null || string.IsNullOrWhiteSpace(cuesheetName))
                return null;

            string normalizedSheet = NormalizeBaseName(cuesheetName);

            UmaDatabaseEntry exact = main.AbSounds.FirstOrDefault(entry =>
                entry != null &&
                string.Equals(Path.GetExtension(entry.Name), extension, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeBaseName(entry.Name), normalizedSheet, StringComparison.OrdinalIgnoreCase));

            if (exact != null)
                return exact;

            return main.AbSounds.FirstOrDefault(entry =>
                entry != null &&
                string.Equals(Path.GetExtension(entry.Name), extension, StringComparison.OrdinalIgnoreCase) &&
                entry.Name.IndexOf(cuesheetName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string NormalizeBaseName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return Path.GetFileNameWithoutExtension(value.Replace('\\', '/'));
        }

        private static List<int> ReadWaveIds(string awbPath)
        {
            using (FileStream stream = File.OpenRead(awbPath))
            using (var reader = new AwbReader(stream))
            {
                return reader.Waves.Select(wave => wave.WaveId).ToList();
            }
        }

        private static List<int> ResolveCueWaveIds(
            UmaDatabaseEntry acb,
            List<int> waveIds,
            string cueName)
        {
            var result = new List<int>();
            if (acb == null || waveIds == null || waveIds.Count == 0 || string.IsNullOrWhiteSpace(cueName))
                return result;

            string acbPath = acb.FilePath;
            if (string.IsNullOrEmpty(acbPath) || !File.Exists(acbPath))
                return result;

            using (FileStream stream = File.OpenRead(acbPath))
            {
                var loader = new AcbNameLoader(stream);
                foreach (int waveId in waveIds)
                {
                    string names;
                    try
                    {
                        // Jukebox music is normally in the streaming AWB.
                        names = loader.LoadWaveName(waveId, -1, false);
                    }
                    catch
                    {
                        continue;
                    }

                    if (ContainsCueName(names, cueName))
                        result.Add(waveId);
                }
            }

            return result;
        }

        private static bool ContainsCueName(string resolvedNames, string cueName)
        {
            if (string.IsNullOrWhiteSpace(resolvedNames) || string.IsNullOrWhiteSpace(cueName))
                return false;

            string[] names = resolvedNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in names)
            {
                string candidate = raw.Trim();
                if (candidate.EndsWith(" [pre]", StringComparison.OrdinalIgnoreCase))
                    candidate = candidate.Substring(0, candidate.Length - " [pre]".Length).TrimEnd();

                if (string.Equals(candidate, cueName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void CreateAudioSources(
            UmaDatabaseEntry awb,
            string awbPath,
            List<int> allWaveIds,
            List<int> targetWaveIds,
            bool loop)
        {
            StopOtherViewerAudio();

            _root = new GameObject("HomeJukeboxAudio");
            GameObject audioController = GameObject.Find("AudioManager/AudioControllerBgm");
            if (audioController != null)
                _root.transform.SetParent(audioController.transform, false);

            // The reader and its FileStream must remain alive while Unity invokes
            // the streaming PCM callbacks, so the reader is owned by this player.
            FileStream awbFile = File.OpenRead(awbPath);
            var awbReader = new AwbReader(awbFile);
            _awbReaders.Add(awbReader);

            foreach (int waveId in targetWaveIds.Distinct())
            {
                Wave wave = awbReader.Waves.FirstOrDefault(item => item.WaveId == waveId);
                if (wave.Length <= 0)
                    continue;

                var stream = new UmaWaveStream(awbReader, waveId);
                ISampleProvider sampleProvider = stream.ToSampleProvider();

                int channels = stream.WaveFormat.Channels;
                int bytesPerSample = stream.WaveFormat.BitsPerSample / 8;
                int sampleRate = stream.WaveFormat.SampleRate;
                int sampleLength = checked((int)(stream.Length / channels / bytesPerSample));

                AudioClip clip = AudioClip.Create(
                    $"{Path.GetFileNameWithoutExtension(awb.Name)}_{waveId}",
                    sampleLength,
                    channels,
                    sampleRate,
                    true,
                    data => sampleProvider.Read(data, 0, data.Length),
                    position => stream.Position = (long)position * channels * bytesPerSample);

                AudioSource source = _root.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = loop;
                source.playOnAwake = false;

                _clips.Add(clip);
                _sources.Add(source);
            }

            _currentAwb = awb;
            _currentWaveIndex = targetWaveIds.Count == 1
                ? allWaveIds.IndexOf(targetWaveIds[0])
                : -1;

            UmaViewerBuilder builder = UmaViewerBuilder.Instance;
            if (builder != null)
            {
                builder.CurrentAudioSources.Clear();
                builder.CurrentAudioSources.AddRange(_sources);
                builder.CurrentLiveSoundAWB.Clear();
                builder.CurrentLiveSoundAWB.Add(awb);
                builder.CurrentLiveSoundAWBIndex = _currentWaveIndex;
            }
        }

        private static void StopOtherViewerAudio()
        {
            UmaViewerBuilder builder = UmaViewerBuilder.Instance;
            if (builder == null)
                return;

            var roots = new HashSet<GameObject>();
            foreach (AudioSource source in builder.CurrentAudioSources)
            {
                if (source == null)
                    continue;

                source.Stop();
                roots.Add(source.gameObject);
            }

            builder.CurrentAudioSources.Clear();
            foreach (GameObject root in roots)
            {
                if (root != null)
                    Object.Destroy(root);
            }

            if (UmaViewerUI.Instance != null && UmaViewerUI.Instance.AudioSettings != null)
                UmaViewerUI.Instance.AudioSettings.ResetPlayer();
        }
    }
}
