using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public class UmaUIContainer : MonoBehaviour
{
    private const string UmaFontResourcePath = "Fonts & Materials/dynamic01 SDF 1";
    private const string ChineseFontSourceResourcePath = "Fonts/NotoSansCJKsc-Bold";
    private const float ChineseFontSize = 20f;
    private static TMP_FontAsset _runtimeChineseFont;
    private static bool _chineseFontMissingLogged;

    public enum TextType
    {
        Text = 0,
        TextMesh = 1
    }

    [FormerlySerializedAs("TextComponentType")]
    [SerializeField]
    private TextType textComponentType = TextType.TextMesh;

    public TextMeshProUGUI TextMesh;
    public Text Text;

    public Button Button;
    public Slider Slider;
    public Toggle Toggle;
    public Image Image;
    public Image ToggleImage;
    public string Id;

    public TextType TextComponentType
    {
        get => textComponentType;
        set
        {
            textComponentType = value;
            if (textComponentType == TextType.TextMesh)
                ApplyLanguageFont();
            RefreshTextComponent();
        }
    }

    public string Name
    {
        get
        {
            if (UseTextMesh)
                return TextMesh.text;

            if (Text != null)
                return Text.text;

            return string.Empty;
        }

        set
        {
            if (UseTextMesh)
            {
                TextMesh.text = value ?? string.Empty;
                return;
            }

            if (Text != null)
                Text.text = value ?? string.Empty;
        }
    }

    public float FontSize
    {
        get
        {
            if (UseTextMesh)
                return TextMesh.fontSize;

            if (Text != null)
                return Text.fontSize;

            return 0f;
        }

        set
        {
            if (UseTextMesh)
            {
                TextMesh.fontSize = value;
                return;
            }

            if (Text != null)
                Text.fontSize = Mathf.RoundToInt(value);
        }
    }

    private bool UseTextMesh => textComponentType == TextType.TextMesh && TextMesh != null;
    private void Awake()
    {
        if (textComponentType == TextType.TextMesh)
            ApplyLanguageFont();
        RefreshTextComponent();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshTextComponent();
    }
#endif

    public void UseTextMeshPro()
    {
        textComponentType = TextType.TextMesh;
        ApplyLanguageFont();
        RefreshTextComponent();
    }

    public void UseLocalizedText()
    {
        if (Config.Instance != null && Config.Instance.Language == Language.Cn)
            UseTextMeshPro();
        else
            UseLegacyText();
    }

    public void UseLegacyText()
    {
        textComponentType = TextType.Text;
        RefreshTextComponent();
    }

    private void RefreshTextComponent()
    {
        bool useTmp =
            textComponentType == TextType.TextMesh &&
            TextMesh != null;

        if (TextMesh != null)
            TextMesh.gameObject.SetActive(useTmp);

        if (Text != null)
            Text.gameObject.SetActive(!useTmp);
    }

    private void ApplyLanguageFont()
    {
        if (TextMesh == null)
            return;

        //英文、日文继续使用项目原来的赛马娘字体,中文使用中文字体
        //中文字体的 fallback 会处理尚未翻译仍然返回日文名的角色
        bool useChineseFont = Config.Instance != null && Config.Instance.Language == Language.Cn;

        TMP_FontAsset font = useChineseFont ? GetRuntimeCjkFont() : Resources.Load<TMP_FontAsset>(UmaFontResourcePath);

        if (font != null)
        {
            TextMesh.font = font;

            if (useChineseFont)
            {
                TextMesh.fontSize = ChineseFontSize;
                // 字体文件本身就是 Bold,不再叠加仿粗,避免边缘发糊
                TextMesh.fontStyle = FontStyles.Normal;
            }
        }
    }

    public static TMP_FontAsset GetRuntimeCjkFont()
    {
        if (_runtimeChineseFont != null)
            return _runtimeChineseFont;

        Font sourceFont = Resources.Load<Font>(ChineseFontSourceResourcePath);
        if (sourceFont == null)
        {
            if (!_chineseFontMissingLogged)
            {
                Debug.LogError($"Chinese font not found at Resources/{ChineseFontSourceResourcePath}.");
                _chineseFontMissingLogged = true;
            }
            return null;
        }

        _chineseFontMissingLogged = false;

        _runtimeChineseFont = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);

        if (_runtimeChineseFont != null)
            _runtimeChineseFont.name = "Noto Sans CJK SC Bold Dynamic";

        return _runtimeChineseFont;
    }
}
