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
    private static readonly int[] UIFpsOptions = { 30, 60, 90, 120, 180, 240 };
    private static readonly int[] UIRenderScaleOptions = { 50, 75, 100, 150, 200 };
    private const float TypingIdleSeconds = 0.7f;   //Se adopta lo escrito cuando se deja de teclear.
    private const int MinUIRenderScale = 50;        //El render scale del viewer se limita a 50-200 %.
    private const int MaxUIRenderScale = 200;

    private TypeInDropdown _uiFps;
    private TypeInDropdown _uiRenderScale;

    public void ApplySettings()
    {
        EnsureLanguageOptions();
        EnsureUIFps();
        EnsureUIRenderScale();
        WorkModeDropdown.SetValueWithoutNotify((int)Config.Instance.WorkMode);
        RegionDropdown.SetValueWithoutNotify((int)Config.Instance.Region);
        LanguageDropdown.SetValueWithoutNotify(LanguageToDropdownValue(Config.Instance.Language));
        UmaViewerMain.ApplyFrameRateLimit();
        UpdateDBButton.interactable = (Config.Instance.WorkMode == WorkMode.Standalone);
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
        if (ContainsPointer(field.Dropdown)) field.BeginTyping();
        else if (field.Typing) field.Commit();
    }

    private void EnsureUIFps()
    {
        if (_uiFps != null) return;

        var dd = FindDropdown("UIfps");
        if (dd == null) return;
        _uiFps = new TypeInDropdown(dd, UIFpsOptions, 0, 0, "", ApplyUIFrameRate);
        _uiFps.SetValue(Config.Instance.UIFrameRate);
    }

    private void EnsureUIRenderScale()
    {
        if (_uiRenderScale != null) return;

        var dd = FindDropdown("UIrenderScale");
        if (dd == null) return;
        _uiRenderScale = new TypeInDropdown(dd, UIRenderScaleOptions, MinUIRenderScale, MaxUIRenderScale, "%", ApplyUIRenderScale);
        _uiRenderScale.SetValue(Config.Instance.UIRenderScalePercent);
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
    private static bool ContainsPointer(TMP_Dropdown dd)
    {
        if (dd == null) return false;
        var canvas = dd.GetComponentInParent<Canvas>();
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(dd.transform as RectTransform, Input.mousePosition, cam);
    }

    //--------------------------------------------------------------------------
    // Desplegable con lista de presets que además se puede escribir: lo tecleado
    // se ADOPTA como valor (con un rango permitido y un sufijo opcional, p. ej. "%").
    // Lo escrito se pinta en el rótulo mientras se teclea, como en los buscadores.
    //--------------------------------------------------------------------------
    private class TypeInDropdown
    {
        private readonly TMP_Dropdown dd;
        private readonly List<int> values = new List<int>();
        private readonly int min;
        private readonly int max;
        private readonly string suffix;
        private readonly System.Action<int> apply;

        private bool typing;
        private string typed = "";
        private float idle;

        public TMP_Dropdown Dropdown { get { return dd; } }
        public bool Typing { get { return typing; } }

        public TypeInDropdown(TMP_Dropdown dropdown, int[] presets, int minValue, int maxValue, string suffixText, System.Action<int> applyValue)
        {
            dd = dropdown;
            min = minValue;
            max = maxValue;
            suffix = suffixText;
            apply = applyValue;

            dd.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (int v in presets)
            {
                values.Add(v);
                options.Add(new TMP_Dropdown.OptionData(v + suffix));
            }
            dd.AddOptions(options);
            dd.onValueChanged = new TMP_Dropdown.DropdownEvent();
            dd.onValueChanged.AddListener(OnOptionChanged);
        }

        //Muestra `value` en el desplegable; si no está en la lista, lo añade.
        public void SetValue(int value)
        {
            if (dd == null) return;
            if (max > min) value = Mathf.Clamp(value, min, max);

            int idx = values.IndexOf(value);
            if (idx < 0)
            {
                values.Add(value);
                dd.options.Add(new TMP_Dropdown.OptionData(value + suffix));
                idx = values.Count - 1;
            }
            dd.SetValueWithoutNotify(idx);
            dd.RefreshShownValue();
        }

        private void OnOptionChanged(int index)
        {
            if (index < 0 || index >= values.Count) return;
            apply(values[index]);
        }

        public void BeginTyping()
        {
            typing = true;
            typed = "";
            idle = 0f;
        }

        public void Update()
        {
            if (dd == null || !typing) return;
            if (!dd.gameObject.activeInHierarchy || !dd.IsInteractable())
            {
                Commit();
                return;
            }

            string tecleado = Input.inputString;
            if (!string.IsNullOrEmpty(tecleado))
            {
                foreach (char c in tecleado)
                {
                    if (c == '\b')
                    {
                        if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
                    }
                    else if (c >= '0' && c <= '9' && typed.Length < 5)
                    {
                        typed += c;
                    }
                }
                idle = 0f;
            }

            //Mientras se teclea, el rótulo enseña lo escrito (igual que en los buscadores).
            RefreshCaption();

            if (typed.Length > 0)
            {
                idle += Time.unscaledDeltaTime;
                if (idle >= TypingIdleSeconds) Commit();
            }
        }

        //Al dejar de teclear se adopta el número (dentro del rango, si lo hay). Si no había nada, se
        //restaura el rótulo con la opción elegida.
        public void Commit()
        {
            bool applied = false;
            if (typing && typed.Length > 0 && int.TryParse(typed, out int value))
            {
                if (max > min) value = Mathf.Clamp(value, min, max);
                apply(value);
                applied = true;
            }
            typing = false;
            typed = "";
            if (!applied) RefreshCaption();
        }

        //Rótulo: lo tecleado mientras se escribe; si no hay nada escrito, la opción elegida.
        private void RefreshCaption()
        {
            if (dd == null || dd.captionText == null) return;
            string texto = typed ?? "";
            if (texto.Length == 0 && dd.value >= 0 && dd.value < dd.options.Count)
                texto = dd.options[dd.value].text;
            if (!string.Equals(dd.captionText.text, texto, System.StringComparison.Ordinal))
                dd.captionText.text = texto;
        }
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

    public void UpdateGameDB()
    {
        if (_updateResVerCoroutine != null && Config.Instance.WorkMode != WorkMode.Standalone) return;
        Popup.Create($"Automatic database update is no longer supported until all issues with new files are resolved. Please run the game to obtain required files.", -1, 200,
            "Ok", null, "Ok");
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
