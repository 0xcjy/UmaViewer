using System;
using UnityEngine;

//(Grabación de pantalla del live) Sonda TEMPORAL de audio para LiveClipRecorder.
//
// Se añade al GameObject del `AudioListener` justo al empezar la grabación y se destruye al
// pararla. `OnAudioFilterRead` recibe la MEZCLA FINAL que sale por los altavoces (música, voces y
// efectos, con el volumen y el mute actuales) y la copia a un búfer en memoria. El grabador vuelca
// ese búfer a un WAV al terminar y lo muxa con el vídeo.
//
// No modifica el audio: sólo copia (`data` se deja intacto). El callback corre en el hilo de audio,
// así que aquí no se toca ninguna API de Unity: sólo se copia el array bajo un lock.
public class LiveClipAudioTap : MonoBehaviour
{
    private readonly object _lock = new object();

    private float[] _buffer;
    private int _samples;
    private int _channels = 2;
    private int _rate = 48000;
    private bool _capturing;

    //Reserva el búfer para `maxSeconds` de audio (más un segundo de margen). Se llama desde el
    //hilo principal antes de empezar a capturar.
    public void Begin(float maxSeconds)
    {
        _rate = Mathf.Max(8000, AudioSettings.outputSampleRate);
        _channels = GuessChannels();

        int capacity = Mathf.CeilToInt(maxSeconds * _rate) * _channels;
        _buffer = new float[Mathf.Max(capacity, _rate * _channels)];
        _samples = 0;

        lock (_lock) { _capturing = true; }
    }

    public void StopCapture()
    {
        lock (_lock) { _capturing = false; }
    }

    //Devuelve una copia de lo capturado y deja de grabar (idempotente).
    public float[] Snapshot(out int channels, out int rate)
    {
        lock (_lock)
        {
            _capturing = false;
            channels = _channels;
            rate = _rate;

            if (_samples <= 0) return Array.Empty<float>();

            var copy = new float[_samples];
            Array.Copy(_buffer, 0, copy, 0, _samples);
            return copy;
        }
    }

    //Hilo de audio: sólo copia. Si el búfer se llena (grabación más larga de lo previsto) se
    //descarta el resto, que es preferible a crecer en el hilo de audio.
    private void OnAudioFilterRead(float[] data, int channels)
    {
        lock (_lock)
        {
            if (!_capturing || _buffer == null) return;

            _channels = channels;
            int space = _buffer.Length - _samples;
            if (space <= 0) return;

            int count = Math.Min(space, data.Length);
            Array.Copy(data, 0, _buffer, _samples, count);
            _samples += count;
        }
    }

    //Canales del dispositivo de salida, para dimensionar el búfer. Lo normal es 2 (estéreo).
    private static int GuessChannels()
    {
        switch (AudioSettings.speakerMode)
        {
            case AudioSpeakerMode.Mono: return 1;
            case AudioSpeakerMode.Quad: return 4;
            case AudioSpeakerMode.Surround: return 5;
            case AudioSpeakerMode.Mode5point1: return 6;
            case AudioSpeakerMode.Mode7point1: return 8;
            default: return 2;
        }
    }
}
