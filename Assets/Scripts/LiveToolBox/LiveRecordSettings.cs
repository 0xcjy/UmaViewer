using System;
using System.IO;
using UnityEngine;

//(Grabación del live) Ajustes de render que el usuario elige en el menú de grabación. Es un objeto
//sencillo (sin MonoBehaviour) que viaja desde el panel del viewer (escena Version2) hasta la escena
//del live, que es donde se graba de verdad.
//
//La grabación NO es una captura de ventana: el motor del live se avanza frame a frame con el
//"delta" fijo de la cadencia elegida y cada frame se renderiza a un RenderTexture del tamaño pedido.
//Así la cadencia y la resolución quedan garantizadas aunque la máquina no dé ese FPS en tiempo real.
[Serializable]
public class LiveRecordSettings
{
    //Cadencia de muestreo de la timeline (= frames por segundo del vídeo). La timeline del live está
    //escrita a 60 fps, pero acepta tiempos fraccionarios, así que se puede muestrear a 30 o a 120.
    public int fps = 60;
    //Resolución de SALIDA del vídeo.
    public int width = 1920;
    public int height = 1080;
    //Supermuestreo: se renderiza por encima del tamaño de salida (100..200 %) y FFmpeg lo reduce.
    //Es el equivalente a un SSAA: bordes más limpios a costa de tiempo de render.
    public int superSamplePercent = 100;
    //MSAA del RenderTexture de captura (1 = off, 2, 4 u 8).
    public int msaa = 1;
    //Postefectos del live (bloom, difusión, DOF, desenfoque radial, corrección de color...).
    public bool postProcess = true;
    //Quemar la letra del show en el vídeo.
    public bool lyrics = true;
    //Tramo a grabar, en segundos. 'endTime <= 0' significa "todo el show".
    public float startTime = 0f;
    public float endTime = 0f;
    //Destino del vídeo. Si falta la carpeta, se usa la de por defecto.
    public string outputFolder = "";
    public string fileName = "";

    //Lo deja el panel de grabación y lo consume la escena del live al cargarse. Se limpia al
    //arrancar la grabación para que no se dispare una segunda vez.
    public static LiveRecordSettings Pending;

    public const int MinFps = 1;
    public const int MaxFps = 480;
    public const int MinSize = 16;
    public const int MaxSize = 7680;

    //Tamaño real del RenderTexture de captura (salida * supermuestreo).
    public int RenderWidth => Mathf.Clamp(Mathf.RoundToInt(width * (superSamplePercent / 100f)), MinSize, 16384);
    public int RenderHeight => Mathf.Clamp(Mathf.RoundToInt(height * (superSamplePercent / 100f)), MinSize, 16384);

    public float Step => 1f / Mathf.Clamp(fps, MinFps, MaxFps);

    //Carpeta de salida por defecto del usuario (visible, junto al ejecutable / al proyecto).
    public static string DefaultFolder
        => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings"));

    public string ResolvedFolder
        => string.IsNullOrWhiteSpace(outputFolder) ? DefaultFolder : outputFolder.Trim();

    public string ResolvedName
        => string.IsNullOrWhiteSpace(fileName) ? "Live" : SanitizeFileName(fileName.Trim());

    public string VideoPath => Path.Combine(ResolvedFolder, ResolvedName + ".mp4");
    public string AudioPath => Path.Combine(ResolvedFolder, ResolvedName + ".wav");

    //Comprueba los valores y los deja dentro de rango. Lanza si algo es irrecuperable (así el panel
    //puede avisar en vez de grabar basura).
    public void Validate()
    {
        if (fps < MinFps || fps > MaxFps)
            throw new InvalidOperationException($"FPS out of range ({MinFps}-{MaxFps}).");
        if (width < MinSize || width > MaxSize || height < MinSize || height > MaxSize)
            throw new InvalidOperationException($"Resolution out of range ({MinSize}-{MaxSize}).");
        //H.264 4:2:0 exige lados pares.
        width -= width % 2;
        height -= height % 2;
        superSamplePercent = Mathf.Clamp(superSamplePercent, 50, 200);
        msaa = msaa <= 1 ? 1 : (msaa <= 2 ? 2 : (msaa <= 4 ? 4 : 8));
        if (startTime < 0f) startTime = 0f;
        if (endTime > 0f && endTime <= startTime)
            throw new InvalidOperationException("End time must be greater than the start time.");
    }

    public LiveRecordSettings Clone()
    {
        return (LiveRecordSettings)MemberwiseClone();
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
