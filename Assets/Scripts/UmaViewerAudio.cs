using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UmaViewerAudio
{

    static UmaViewerMain Main => UmaViewerMain.Instance;
    public static int LastAudioPartIndex = -1;

    public struct UmaSoundInfo
    {
        public UmaDatabaseEntry awb;
    }

    public class CuteAudioSource
    {
        public string tag;
        public bool enable;
        public float volume;
        public float pan;
        public int cur_active_source = - 1;
        public AudioSource activeSource;
        public List<AudioSource> sourceList;
        //(Grabación del live) AWB del que salieron estas pistas. Lo rellena ApplySound y lo usa la
        //mezcla offline para volver a decodificar el audio sin depender del motor de Unity.
        public UmaDatabaseEntry awb;

        public void SwitchActiveSource(int index, bool forceUpdate = false)
        {
            if (cur_active_source == index && !forceUpdate) return;
            cur_active_source = index;
            activeSource = null;
            for(int i = 0; i < sourceList.Count; i++)
            {
                if (i + 1 == index)
                {
                    activeSource = sourceList[i];
                    sourceList[i].volume = volume;
                    sourceList[i].panStereo = pan;
                }
                else
                {
                    sourceList[i].volume = 0;
                }
            }
        }

        public void SetVolume(float volume) 
        {
            this.volume = volume;
            if (activeSource) activeSource.volume = volume;
        }

        public void SetPanStereo(float pan)
        {
            this.pan = pan;
            if (activeSource) activeSource.panStereo = pan;
        }
    }

    static public UmaSoundInfo getSoundPath(string name)
    {
        UmaSoundInfo info;
        info.awb = Main.AbSounds.FirstOrDefault(a => a.Name.Contains(name) && a.Name.EndsWith("awb"));
        return info;
    }

    static public CuteAudioSource ApplySound(string cueName, int part)
    {
        UmaSoundInfo soundInfo = getSoundPath(cueName);

        GameObject sourceRoot = new GameObject("CuteAudioSource");
        sourceRoot.transform.SetParent(GameObject.Find("AudioManager/AudioControllerBgm").transform);

        List<AudioSource> sourceList = new List<AudioSource>();

        List<AudioClip> sounds = UmaViewerBuilder.LoadAudio(soundInfo.awb);
        foreach(var clip in sounds)
        {
            AudioSource source = sourceRoot.AddComponent<AudioSource>();
            source.clip = clip;
            sourceList.Add(source);
        }

        CuteAudioSource cute = new CuteAudioSource
        {
            tag = ((PartForm)part).ToString(),
            enable = false,
            volume = 1,
            pan = 0,
            sourceList = sourceList,
            awb = soundInfo.awb
        };

        return cute;
    }

    //(Live) Margen que se resta al final de cada clip al posicionarlo. Unity rechaza un seek
    //exactamente en `clip.length` ("An invalid seek position was passed to this function") y la
    //pista acaba sonando desde el principio.
    const float ClipEndGuard = 0.05f;

    //(Live) `time` < 0 = reproducir desde donde este cada pista (inicio del show). Con `time` >= 0
    //las pistas que ya han terminado en ese instante NO se reproducen: no se pueden posicionar y
    //volverian a sonar desde el principio.
    public static void Play(CuteAudioSource sourceList, float time = -1f)
    {
        if (sourceList == null || sourceList.sourceList == null) return;
        foreach(var source in sourceList.sourceList)
        {
            if (source == null || source.clip == null) continue;
            if (time >= 0f && time >= source.clip.length) continue;
            source.Play();
        }
    }

    public static void Stop(CuteAudioSource sourceList)
    {
        if (sourceList == null || sourceList.sourceList == null) return;
        foreach (var source in sourceList.sourceList)
        {
            if (source == null) continue;
            source.Stop();
        }
    }

    public static void SetTime(CuteAudioSource sourceList, float time)
    {
        if (sourceList == null || sourceList.sourceList == null) return;
        foreach (var source in sourceList.sourceList)
        {
            if (source == null || source.clip == null) continue;
            //Cada onda del AWB tiene su propia duracion y al final del show el timeline sigue mas
            //alla de algunas de ellas: el tiempo se acota al clip de cada pista.
            float max = Mathf.Max(0f, source.clip.length - ClipEndGuard);
            source.time = Mathf.Clamp(time, 0f, max);
        }
    }

    /// <summary>
    /// (VIDEO SPEED) Cambia la velocidad de reproduccion de una fuente bajando/subiendo su pitch:
    /// Unity no escala el audio con `Time.timeScale`, asi que sin esto el show se desincroniza.
    /// </summary>
    public static void SetPlaybackSpeed(CuteAudioSource sourceList, float speed)
    {
        if (sourceList == null || sourceList.sourceList == null) return;

        float pitch = Mathf.Clamp(speed, 0.1f, 3f);
        for (int i = 0; i < sourceList.sourceList.Count; i++)
        {
            var source = sourceList.sourceList[i];
            if (source != null) source.pitch = pitch;
        }
    }

    public enum PartForm
    {
        center = 0,
        left = 1,
        right = 2,
        left2 = 3,
        right2 = 4,
        left3 = 5,
        right3 = 6,
    }

    // Original .cctor 0x1b8ea00, GetVolume 0x1b8d670 and GetPartData 0x1b8d290.
    internal static readonly float[] DefaultVolumes = { 0f, .79f, .89f, 1f, 1.12f, 1.26f };
    internal static readonly float[] DefaultVolumeRates = { 0f, .79f, .79f, .56f, .53f, .47f, .42f, .37f };
    internal static readonly int[][] DefaultVolumeIndices = {
        new[] { 0 }, new[] { 5 }, new[] { 2, 2 }, new[] { 3, 5, 3 },
        new[] { 3, 3, 3, 3 }, new[] { 1, 2, 4, 2, 1 },
        new[] { 3, 3, 3, 3, 3, 3 }, new[] { 1, 1, 2, 4, 2, 1, 1 }
    };
    internal static readonly float[][] DefaultPans = {
        new[] { 0f }, new[] { 0f }, new[] { -.15f, .15f }, new[] { -.3f, 0f, .3f },
        new[] { -.3f, -.1f, .1f, .3f }, new[] { -.3f, -.15f, 0f, .15f, .3f },
        new[] { -.3f, -.2f, -.1f, .1f, .2f, .3f }, new[] { -.3f, -.2f, -.1f, 0f, .1f, .2f, .3f }
    };
    internal static readonly string[] OrderedParts = { "left3", "left2", "left", "center", "right", "right2", "right3" };

    //(Live) Nombre de columna del `_part` → slot (PartForm). El slot es el orden con el que se crean
    //las pistas: center, left, right, left2, right2, left3, right3.
    internal static readonly Dictionary<string, int> PartSlot = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        { "center", (int)PartForm.center }, { "left", (int)PartForm.left }, { "right", (int)PartForm.right },
        { "left2", (int)PartForm.left2 }, { "right2", (int)PartForm.right2 },
        { "left3", (int)PartForm.left3 }, { "right3", (int)PartForm.right3 },
    };

    //(Live) Tope por defecto de pistas con la MISMA voz sonando A LA VEZ. En Mix voices se usa este
    //valor (3). En Solo concert el tope es configurable desde Config.json
    //(MaxSimultaneousVoiceOnSoloConcert, 2 por defecto): si suenan más pistas activas que ese número,
    //el resto se silencia en orden de slot.
    public const int MaxSimultaneousSameVoice = 3;
    static readonly bool[] SilencedSlots = new bool[7];

    static public void AlterUpdate(float _liveCurrentTime, PartEntry partInfo, List<CuteAudioSource> liveVocal,
                                   bool forceUpdate = false, bool capSameVoice = false,
                                   int maxSameVoice = MaxSimultaneousSameVoice)
    {
        if (partInfo == null || liveVocal == null ||
            !partInfo.PartSettings.TryGetValue("time", out var times)) return;
        int targetIndex = times.Count - 1;
        while (targetIndex >= 0 && _liveCurrentTime < times[targetIndex] / 1000.0) targetIndex--;
        if (targetIndex < 0 || (!forceUpdate && LastAudioPartIndex == targetIndex)) return;
        LastAudioPartIndex = targetIndex;

        //(Live) Con el flag activo sólo pueden sonar las primeras `maxSameVoice` pistas (en orden de
        //slot) que pida la tabla en este momento; el resto quedan mudas.
        ComputeSilencedSlots(capSameVoice, liveVocal, partInfo, targetIndex, SilencedSlots, maxSameVoice);

        int activeCount = 0;
        foreach (string part in OrderedParts)
        {
            var vocal = FindVocal(liveVocal, part);
            if (vocal == null || SilencedSlots[PartSlot[part]]) continue;
            if (PartValue(partInfo, part, targetIndex, 0f) > 0f) activeCount++;
        }
        // Original AlterUpdate 0x1b8c31d and 0x1b8c562: >=900 means default,
        // not zero. An explicit zero remains authored silence.
        float rate = PartValue(partInfo, "volume_rate", targetIndex, 901f);
        if (rate >= 900f) rate = activeCount < DefaultVolumeRates.Length ? DefaultVolumeRates[activeCount] : 1f;
        int rank = 0;
        foreach (string part in OrderedParts)
        {
            var vocal = FindVocal(liveVocal, part);
            if (vocal == null) continue;
            int index = (int)PartValue(partInfo, part, targetIndex, 0f);
            if (SilencedSlots[PartSlot[part]]) index = 0;   //(Live) tope de la misma voz: pista muda
            vocal.SwitchActiveSource(index, forceUpdate);
            bool active = index > 0;
            int row = activeCount < DefaultVolumeIndices.Length ? activeCount : 0;
            int column = active && row > 0 ? rank++ : 0;
            if (!active) row = 0;
            float volume = PartValue(partInfo, part + "_vol", targetIndex, 901f);
            float pan = PartValue(partInfo, part + "_pan", targetIndex, 901f);
            if (volume >= 900f) volume = DefaultVolumes[DefaultVolumeIndices[row][column]];
            if (pan >= 900f) pan = DefaultPans[row][column];
            vocal.SetVolume(volume * rate);
            vocal.SetPanStereo(pan);
        }
    }

    internal static CuteAudioSource FindVocal(List<CuteAudioSource> vocals, string part)
    {
        for (int i = 0; i < vocals.Count; i++)
            if (vocals[i].tag == part) return vocals[i];
        return null;
    }

    //(Live) Decide qué slots quedan mudos cuando la MISMA voz ocupa muchas pistas (tope
    //`maxVoices`). La usan la mezcla en vivo (AlterUpdate) y la mezcla offline de la grabación, para
    //que ambas coincidan. En Solo concert el tope viene de Config.json; en Mix voices es el 3 fijo.
    internal static void ComputeSilencedSlots(bool capSameVoice, List<CuteAudioSource> liveVocal,
                                              PartEntry partInfo, int targetIndex, bool[] silenced,
                                              int maxVoices = MaxSimultaneousSameVoice)
    {
        for (int i = 0; i < silenced.Length; i++) silenced[i] = false;
        if (!capSameVoice || partInfo == null || liveVocal == null) return;
        maxVoices = Math.Max(1, maxVoices);
        if (liveVocal.Count <= maxVoices) return;

        int allowed = 0;
        //Se recorren los 7 slots (no liveVocal.Count): si un AWB falló, la lista puede tener huecos.
        for (int slot = 0; slot < silenced.Length; slot++)
        {
            string partName = ((PartForm)slot).ToString();
            if (FindVocal(liveVocal, partName) == null) continue;
            if (PartValue(partInfo, partName, targetIndex, 0f) <= 0f) continue;   // no canta ahora
            if (allowed >= maxVoices) silenced[slot] = true;
            else allowed++;
        }
    }

    internal static float PartValue(PartEntry data, string column, int row, float fallback)
    {
        return data.PartSettings.TryGetValue(column, out var values) && row < values.Count
            ? values[row] : fallback;
    }
}
