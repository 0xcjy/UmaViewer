using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UI;

public class LiveViewerUI : MonoBehaviour
{
    public static LiveViewerUI Instance;

    public UnityEngine.UI.Slider ProgressBar;

    //(Tiempo del live) Etiquetas que sustituyen al antiguo texto "ProgressSeconds" creado por codigo:
    //muestran el tiempo actual de la cancion y su duracion total. Son OPCIONALES: si el YAML no las
    //deja cableadas, se descubren en Awake por nombre dentro de Canvas/Trigger/Bottom/Slider.
    public TMP_Text CurrentTimeLabel;
    public TMP_Text TotalTimeLabel;

    public RectTransform BottonUITransform;

    //(Live: los controles de ajustes graficos viven en la escena `LiveScene`; este componente los
    //rellena y cablea. Ver LiveGraphicsSettingsUI.cs.)
    public LiveGraphicsSettingsUI GraphicsSettings;

    public GameObject RecordingUI;

    public Text RecordingText;

    public Text LyricsText;

    public List<UmaLyricsData> CurrentLyrics = new List<UmaLyricsData>();

    private int _displayedCurrentSecond = -1;
    private int _displayedTotalSecond = -1;

    float targetHeight = 0;
    float height;
    private void Awake()
    {
        if (Config.Instance == null)
        {
            new Config();
        }
        //(Los controles de ajustes graficos ya existen en la escena: aqui solo se rellenan.)
        if (GraphicsSettings != null) GraphicsSettings.Apply();
        RuntimeGraphicsSettings.ApplyLiveRenderScale();
        UmaViewerMain.ApplyFrameRateLimitLive();

        height = BottonUITransform.rect.height;
        targetHeight = 0;
        Invoke(nameof(HideSlider), 1.5f);
        Instance = this;

        // Se descubren las etiquetas de tiempo de la barra si el YAML no las deja cableadas.
        if (CurrentTimeLabel == null) CurrentTimeLabel = FindTimeLabel("CurrentTime");
        if (TotalTimeLabel == null) TotalTimeLabel = FindTimeLabel("TotalTime");

        // Cablea el boton de salir del live que haya en la escena (el onClick trae un binding roto:
        //se sustituye entero por la salida real). Si no se encuentra, avisa: el Esc sigue saliendo.
        WireCancelButton();

        // Overlay de depuración con el frame actual del live, que se enciende y apaga con
        // Ctrl+Shift+F9. Se monta por código (no en el YAML) y vive en esta escena, que es la que
        // tiene el Director. Ver LiveFrameDebugOverlay.cs.
        LiveFrameDebugOverlay.Ensure();

        // Overlay de depuración con los slots de voz del live (qué PJ canta en cada uno y si su
        // pista está sonando), que se enciende y apaga con Ctrl+F8. Ver LiveVocalDebugOverlay.cs.
        LiveVocalDebugOverlay.Ensure();
        //TrueProgressBar = (UnityEngine.UIElements.Slider)ProgressBar;
    }

    //Busca la etiqueta de tiempo (TMP) dentro del objeto de escena con el nombre dado
    //("CurrentTime" / "TotalTime", hijos de Canvas/Trigger/Bottom/Slider). Primero por ruta exacta y,
    //si no, por nombre en toda la escena. Descarta prefabs/assets: solo cuenta lo que esta en una
    //escena cargada.
    private TMP_Text FindTimeLabel(string goName)
    {
        var go = GameObject.Find("Canvas/Trigger/Bottom/Slider/" + goName);
        if (go != null)
        {
            var t = go.GetComponent<TMP_Text>();
            if (t == null) t = go.GetComponentInChildren<TMP_Text>(true);
            if (t != null) return t;
        }

        foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (t == null) continue;
            if (!t.gameObject.scene.IsValid()) continue;   //descarta prefabs/assets
            if (t.gameObject.name == goName) return t;
            if (t.transform.parent != null && t.transform.parent.name == goName) return t;
        }
        return null;
    }

    //Pinta el tiempo actual y la duracion total de la cancion. La llama Director.Update cada frame
    //(barato: solo escribe si el segundo mostrado cambia).
    public void UpdateTimeLabels(float current, float total)
    {
        if (CurrentTimeLabel != null)
        {
            int s = ToWholeSeconds(current);
            if (s != _displayedCurrentSecond)
            {
                _displayedCurrentSecond = s;
                CurrentTimeLabel.text = FormatTime(s);
            }
        }
        if (TotalTimeLabel != null)
        {
            int s = ToWholeSeconds(total);
            if (s != _displayedTotalSecond)
            {
                _displayedTotalSecond = s;
                TotalTimeLabel.text = FormatTime(s);
            }
        }
    }

    private static int ToWholeSeconds(float seconds)
    {
        if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return 0;
        return Mathf.FloorToInt(seconds);
    }

    //mm:ss (los minutos no se acotan a 60: un show de 90 min da "90:00").
    private static string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, secs);
    }

    //Cablea el boton de salir del live. Se busca por RUTA exacta y, si no, por nombre en toda la
    //escena, para no depender de como este escrito. Si no se encuentra, solo avisa: el Esc sigue
    //saliendo del live.
    private void WireCancelButton()
    {
        var bn = FindCancelButton();
        if (bn == null)
        {
            Debug.LogWarning("[LiveViewerUI] No se encuentra el boton de salir del live: se sale solo con Esc.");
            return;
        }
        //El onClick puede traer llamadas persistentes con target vacio (binding roto). Se sustituye
        //entero por la salida real del live.
        bn.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        bn.onClick.AddListener(OnExitLiveButtonClick);
        Debug.Log($"[LiveViewerUI] Boton de salir del live cableado: '{bn.gameObject.name}'.");
    }

    //Busca el Button de salir del live. Primero por ruta exacta (su sitio en LiveScene.unity) y, si
    //no, por nombre. Descarta prefabs/assets: solo cuenta lo que esta en una escena cargada.
    private UnityEngine.UI.Button FindCancelButton()
    {
        string[] names = { "CancelButtton", "CancelButton", "ExitButtton", "ExitButton" };
        string[] parents = { "Canvas/Trigger/Bottom/Slider/", "Canvas/Trigger/Bottom/" };

        //1) Ruta exacta.
        foreach (var parentPath in parents)
        {
            foreach (var n in names)
            {
                var go = GameObject.Find(parentPath + n);
                if (go == null) continue;
                var b = go.GetComponent<UnityEngine.UI.Button>();
                if (b != null) return b;
            }
        }

        //2) Por nombre, en cualquier parte de las escenas cargadas (incluye inactivos).
        foreach (var b in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>())
        {
            if (b == null) continue;
            if (!b.gameObject.scene.IsValid()) continue;   //descarta prefabs/assets
            var goName = b.gameObject.name;
            foreach (var n in names)
            {
                if (goName == n) return b;
            }
        }
        return null;
    }

    //Sale del live igual que Esc: pide la salida al Director, que es quien hace el cambio de escena
    //(Director.ExitLive es privado; RequestExit es el punto de entrada publico).
    public void OnExitLiveButtonClick()
    {
        var director = Gallop.Live.Director.instance;
        if (director == null)
        {
            //Respaldo: por si el singleton no estuviera asignado, se busca el componente en escena.
            director = UnityEngine.Object.FindObjectOfType<Gallop.Live.Director>();
        }
        if (director != null)
        {
            director.RequestExit();
        }
        else
        {
            Debug.LogWarning("[LiveViewerUI] No hay Director en escena: no se puede salir del live con el boton.");
        }
    }

    public void OnMouse(bool isEnter)
    {
        if (isEnter)
        {
            targetHeight = 0;
            CancelInvoke();
        }
        else
        {
            Invoke(nameof(HideSlider), 2.5f);
        }
    }

    private void FixedUpdate()
    {
        BottonUITransform.anchoredPosition = Vector2.Lerp(BottonUITransform.anchoredPosition, new Vector2(0, targetHeight), Time.fixedDeltaTime * 5);
    }

    private void HideSlider()
    {
        targetHeight = -height;
    }

    public void UpdateLyrics(float time)
    {
        var text = UmaUtility.GetCurrentLyrics(time, CurrentLyrics);
        LyricsText.text = text;
    }
}
