#if UNITY_STANDALONE || UNITY_EDITOR
using SFB;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UISettingsOther : MonoBehaviour
{
    public Button UpdateDBButton;
    public TMP_Dropdown RegionDropdown;
    public TMP_Dropdown WorkModeDropdown;
    public TMP_Dropdown LanguageDropdown;
    private IEnumerator _updateResVerCoroutine;

    //--- Elementos `Other/UIfps` y `Other/UIrenderScale/Dropdown` (viven en Version2.unity; aquí SÓLO
    //se resuelven por nombre y se cablea el comportamiento). Ver EnsureUIFps/EnsureUIRenderScale.
    //El desplegable escribible que los cablea es la clase compartida `TypeInDropdown`.
    private static readonly int[] UIFpsOptions = { 30, 60, 90, 120, 180, 240 };
    private static readonly int[] UIRenderScaleOptions = { 50, 75, 100, 150, 200 };
    private const int MinUIRenderScale = 50;        //El render scale del viewer se limita a 50-200 %.
    private const int MaxUIRenderScale = 200;

    private TypeInDropdown _uiFps;
    private TypeInDropdown _uiRenderScale;

    public void ApplySettings()
    {
        EnsureLanguageOptions();
        EnsureUIFps();
        EnsureUIRenderScale();
        EnsureUpdateNonLoadingModelsButton();
        WorkModeDropdown.SetValueWithoutNotify((int)Config.Instance.WorkMode);
        RegionDropdown.SetValueWithoutNotify((int)Config.Instance.Region);
        LanguageDropdown.SetValueWithoutNotify(LanguageToDropdownValue(Config.Instance.Language));
        UmaViewerMain.ApplyFrameRateLimit();
        // El boton "UpdateDatabase" genera las listas EN (nombres + lyrics) desde el cliente
        // Global. Se deja SIEMPRE clicable: si Region no es JP, UpdateGameDB avisa y no hace nada.
        UpdateDBButton.interactable = true;
    }

    private void EnsureLanguageOptions()
    {
        if (LanguageDropdown == null)
            return;

        var options = new List<string>
        {
            "简体中文",
            "English",
            "日本語"
        };

        bool optionsMatch = LanguageDropdown.options.Count == options.Count;
        if (optionsMatch)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (LanguageDropdown.options[i].text != options[i])
                {
                    optionsMatch = false;
                    break;
                }
            }
        }

        if (!optionsMatch)
        {
            LanguageDropdown.ClearOptions();
            LanguageDropdown.AddOptions(options);
        }
    }

    private int LanguageToDropdownValue(Language language)
    {
        switch (language)
        {
            case Language.Cn:
                return 0;
            case Language.En:
                return 1;
            case Language.Jp:
            default:
                return 2;
        }
    }

    private Language DropdownValueToLanguage(int value)
    {
        switch (value)
        {
            case 0:
                return Language.Cn;
            case 1:
                return Language.En;
            case 2:
            default:
                return Language.Jp;
        }
    }

    //==========================================================================
    // UIfps y UIrenderScale/Dropdown: desplegables con lista propia que ADEMÁS se
    // pueden escribir. Lo tecleado se ADOPTA como valor (no filtra la lista, a
    // diferencia de los buscadores). El diseño vive en Version2.unity.
    //==========================================================================

    private void Update()
    {
        //Un clic dentro de uno de estos desplegables empieza a escuchar el teclado; un clic fuera
        //cierra la captura del que estuviera escuchando.
        if (Input.GetMouseButtonDown(0))
        {
            HandleTypingClick(_uiFps);
            HandleTypingClick(_uiRenderScale);
        }

        if (_uiFps != null) _uiFps.Update();
        if (_uiRenderScale != null) _uiRenderScale.Update();
    }

    private static void HandleTypingClick(TypeInDropdown field)
    {
        if (field == null) return;
        if (ContainsPointer(field.Rect)) field.BeginTyping();
        else if (field.Typing) field.Commit();
    }

    private void EnsureUIFps()
    {
        if (_uiFps != null) return;

        var dd = FindDropdown("UIfps");
        if (dd == null) return;
        _uiFps = new TmpTypeInDropdown(dd, UIFpsOptions, 0, 0, "", ApplyUIFrameRate);
        _uiFps.SetValue(Config.Instance.UIFrameRate);
    }

    private void EnsureUIRenderScale()
    {
        if (_uiRenderScale != null) return;

        var dd = FindDropdown("UIrenderScale");
        if (dd == null) return;
        _uiRenderScale = new TmpTypeInDropdown(dd, UIRenderScaleOptions, MinUIRenderScale, MaxUIRenderScale, "%", ApplyUIRenderScale);
        _uiRenderScale.SetValue(Config.Instance.UIRenderScalePercent);
    }

    //Cablea por nombre el boton `Other/UpdateNonLoadingModels` (genera la blacklist de trajes sin
    //cuerpo). El diseno vive en la escena; aqui solo se resuelve y se le pone el listener.
    private void EnsureUpdateNonLoadingModelsButton()
    {
        var row = transform.Find("UpdateNonLoadingModels");
        if (row == null)
        {
            UnityEngine.Debug.LogWarning("[UISettingsOther] No se encuentra 'Other/UpdateNonLoadingModels' en la escena.");
            return;
        }
        var button = row.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            UnityEngine.Debug.LogWarning("[UISettingsOther] 'Other/UpdateNonLoadingModels' no tiene Button.");
            return;
        }
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(UpdateNonLoadingModels);
    }

    //Busca el TMP_Dropdown de una fila de `Other` por nombre; avisa si falta.
    private TMP_Dropdown FindDropdown(string rowName)
    {
        var row = transform.Find(rowName);
        if (row == null)
        {
            UnityEngine.Debug.LogWarning("[UISettingsOther] No se encuentra el elemento 'Other/" + rowName + "' en la escena.");
            return null;
        }
        var dd = row.GetComponentInChildren<TMP_Dropdown>(true);
        if (dd == null)
            UnityEngine.Debug.LogWarning("[UISettingsOther] 'Other/" + rowName + "' no tiene TMP_Dropdown.");
        return dd;
    }

    private void ApplyUIFrameRate(int fps)
    {
        if (Config.Instance.UIFrameRate != fps)
        {
            Config.Instance.UIFrameRate = fps;
            Config.Instance.UpdateConfig(false);
        }
        UmaViewerMain.ApplyFrameRateLimit();
        if (_uiFps != null) _uiFps.SetValue(fps);
    }

    private void ApplyUIRenderScale(int percent)
    {
        Config.Instance.UIRenderScalePercent = Mathf.Clamp(percent, MinUIRenderScale, MaxUIRenderScale);
        Config.Instance.UpdateConfig(false);
        RuntimeGraphicsSettings.Apply();
        if (_uiRenderScale != null) _uiRenderScale.SetValue(Config.Instance.UIRenderScalePercent);
    }

    //Rect COMPLETO del desplegable (rótulo + flecha), igual que en los buscadores.
    private static bool ContainsPointer(RectTransform rect)
    {
        if (rect == null) return false;
        var canvas = rect.GetComponentInParent<Canvas>();
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
    }

    public void ChangeLanguage(int lang)
    {
        Language language = DropdownValueToLanguage(lang);
        if (Config.Instance.Language != language)
        {
            Config.Instance.Language = language;
            Config.Instance.UpdateConfig(false);

            // Rebuild all localized lists and reload English translations when
            // necessary without requiring the application process to restart.
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }
    }

    public void ChangeRegion(int region)
    {
        if ((int)Config.Instance.Region != region)
        {
            Config.Instance.Region = (Region)region;
            Config.Instance.UpdateConfig(false);
            StartCoroutine(UmaViewerUI.Instance.ApplyGraphicsSettings());
        }
    }

    public void ChangeWorkMode(int mode)
    {
        if ((int)Config.Instance.WorkMode != mode)
        {
            Config.Instance.WorkMode = (WorkMode)mode;
            Config.Instance.UpdateConfig(true);
        }
    }

    //Genera la blacklist de trajes sin cuerpo (`blacklist_trajes.json`) desde los datos cargados y
    //la aplica al listado. Mismo escaneo que la carga normal (LoadNormalUma). Muestra un popup
    //sin opciones mientras se construye (repartido entre frames) y lo cierra al terminar.
    public void UpdateNonLoadingModels()
    {
        var popup = Popup.CreateProgress(
            "Updating list of costumes that trigger error on some characters.\nThose costumes will be disable for selection");
        StartCoroutine(UpdateNonLoadingModelsRoutine(popup));
    }

    private IEnumerator UpdateNonLoadingModelsRoutine(Popup popup)
    {
        // Deja que el popup se pinte antes del primer frame de trabajo.
        yield return null;

        bool completed = false;
        int count = 0;
        string error = null;

        yield return CostumeBlacklist.BuildAndSaveRoutine((ok, c, e) =>
        {
            completed = ok;
            count = c;
            error = e;
        });

        if (popup != null) Destroy(popup.gameObject);

        if (completed)
        {
            UmaViewerUI.Instance.ShowMessage(
                $"Non-loading costume blacklist updated ({count} entries).",
                UIMessageType.Success);
        }
        else
        {
            UmaViewerUI.Instance.ShowMessage(
                "Could not update the blacklist: " + error,
                UIMessageType.Error);
        }
    }

    public void UpdateGameDB()
    {
        if (Config.Instance.Region != Region.Jp)
        {
            UmaViewerUI.Instance.ShowMessage(
                "Not needed with Region = Global: that database already has English names.",
                UIMessageType.Warning);
            return;
        }

        // Si ya hay una carpeta Global guardada y valida, se actualiza directamente (sin aviso);
        // solo se pregunta la primera vez (o si la ruta guardada dejo de ser valida).
        string saved = Config.Instance.GlobalNamesPath;
        if (!string.IsNullOrEmpty(saved) && GlobalNames.IsValidFolder(saved))
        {
            RunUpdate(saved);
            return;
        }

        Popup.Create(
            "Update English name for JP database. Please select the global folder in the next step",
            -1, 200,
            "Ok", OnUpdateDatabaseAccept,
            "Cancel", null);
    }

    // Callback del "Ok" del aviso: abre el explorador y, con la carpeta elegida, intenta
    // construir la lista de nombres. Si algo falla NO se toca la lista anterior.
    private void OnUpdateDatabaseAccept()
    {
        string folder = SelectGlobalFolder();
        if (string.IsNullOrEmpty(folder)) return;   // Cancelado: no se hace nada.

        RunUpdate(folder);
    }

    // Construye las listas EN (nombres + lyrics) desde la carpeta del cliente Global. Guarda la
    // carpeta en Config para que las siguientes pulsaciones de "Update Database" sean automaticas.
    private void RunUpdate(string folder)
    {
        if (GlobalNames.BuildFromGlobalFolder(folder, out string error))
        {
            // Recuerda la carpeta elegida para futuros refrescos y como inicio del dialogo.
            Config.Instance.GlobalNamesPath = folder;
            Config.Instance.UpdateConfig(false);

            // Genera tambien las lyrics EN desde el cliente Global (solo en el Editor: leer y
            // desencriptar los bundles del global no se hace en un build).
            string lyricsError;
            if (!GlobalLyrics.BuildFromGlobalFolder(folder, out lyricsError) &&
                !string.IsNullOrEmpty(lyricsError))
            {
                UnityEngine.Debug.LogWarning("[GlobalLyrics] " + lyricsError);
            }

            UmaViewerUI.Instance.ShowMessage("English names updated.", UIMessageType.Success);
        }
        else
        {
            UmaViewerUI.Instance.ShowMessage("Could not update English names: " + error, UIMessageType.Error);
        }
    }

    private string SelectGlobalFolder()
    {
#if UNITY_EDITOR
        return EditorUtility.OpenFolderPanel(
            "Select the GLOBAL game folder (.../UmamusumePrettyDerby_Data/Persistent)",
            Config.Instance.GlobalNamesPath, "");
#elif UNITY_STANDALONE
        string[] paths = StandaloneFileBrowser.OpenFolderPanel(
            "Select the GLOBAL game folder", Config.Instance.GlobalNamesPath, false);
        return (paths != null && paths.Length > 0) ? paths[0] : null;
#else
        UmaViewerUI.Instance.ShowMessage("Not supported on this platform", UIMessageType.Warning);
        return null;
#endif
    }

    public void ChangeDataPath()
    {
#if UNITY_EDITOR
        // Editor 里用 Unity 自带的选文件夹（不会走 SFB 的原生 .bundle，因此不会 DllNotFoundException）
        var p = EditorUtility.OpenFolderPanel("Select Folder", Config.Instance.MainPath, "");
        if (!string.IsNullOrEmpty(p) && p != Config.Instance.MainPath)
        {
            Config.Instance.MainPath = p;
            Config.Instance.UpdateConfig(true);
        }

#elif UNITY_STANDALONE
        // Standalone 运行时仍走 SFB（如果以后要打包到 mac App，这里建议再换成更现代的方案）
        var path = StandaloneFileBrowser.OpenFolderPanel("Select Folder", Config.Instance.MainPath, false);
        if (path != null && path.Length > 0 && !string.IsNullOrEmpty(path[0]) && path[0] != Config.Instance.MainPath)
        {
            Config.Instance.MainPath = path[0];
            Config.Instance.UpdateConfig(true);
        }
#else
        UmaViewerUI.Instance.ShowMessage("Not supported on this platform", UIMessageType.Warning);
#endif
    }

    public void OpenConfig()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        if (File.Exists(Config.configPath))
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = Config.configPath,
                UseShellExecute = true
            });
        }
#else
        UmaViewerUI.Instance.ShowMessage("Not supported on this platform", UIMessageType.Warning);
#endif
    }

    public void UnloadAllBundle() => UmaAssetManager.UnloadAllBundle(true);
}
