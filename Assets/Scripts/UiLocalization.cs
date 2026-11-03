using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

/// <summary>
/// Runtime UI translation.
///
/// The scenes keep their original English strings; when the selected language is Chinese each
/// TMP label is swapped for the matching entry in Resources/UI/zh-CN.json (keyed by the exact
/// original text). Selecting any other language restores the original strings, so the English
/// UI stays untouched.
/// </summary>
public static class UiLocalization
{
    private const string ResourcePath = "UI/zh-CN";

    private static Dictionary<string, string> zhMap;
    private static readonly Dictionary<TMP_Text, string> originals = new Dictionary<TMP_Text, string>();
    private static readonly Dictionary<Text, string> legacyOriginals = new Dictionary<Text, string>();
    private static TMP_FontAsset cjkFont;
    private static readonly Dictionary<TMP_Dropdown, List<string>> originalOptions =
        new Dictionary<TMP_Dropdown, List<string>>();
    private static bool loadFailed;

    public static void Apply(Language language)
    {
        Prune();

        if (language != Language.Cn)
        {
            Restore();
            return;
        }

        if (!Load()) return;

        foreach (TMP_Text text in Object.FindObjectsOfType<TMP_Text>(true))
        {
            if (text == null) continue;

            ApplyCjkFallback(text);

            if (!originals.TryGetValue(text, out string original))
            {
                original = text.text;
                originals[text] = original;
            }

            if (string.IsNullOrEmpty(original)) continue;

            if (zhMap.TryGetValue(original, out string translated) && text.text != translated)
            {
                text.text = translated;
                text.ForceMeshUpdate();
            }
        }

        // Legacy UnityEngine.UI.Text labels (the whole Other/Settings button column is still
        // built from those, not TMP) — without this they stayed English even though the table
        // had an entry.
        foreach (Text text in Object.FindObjectsOfType<Text>(true))
        {
            if (text == null) continue;

            if (!legacyOriginals.TryGetValue(text, out string original))
            {
                original = text.text;
                legacyOriginals[text] = original;
            }

            if (string.IsNullOrEmpty(original)) continue;

            if (zhMap.TryGetValue(original, out string translated) && text.text != translated)
            {
                text.text = translated;
            }
        }

        // Dropdown captions and their expanded list both come from options[].text, so the
        // entries have to be translated too — otherwise "Japanese" stayed "Japanese" in the
        // list while the label above it was already Chinese.
        foreach (TMP_Dropdown dropdown in Object.FindObjectsOfType<TMP_Dropdown>(true))
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0) continue;

            if (!originalOptions.TryGetValue(dropdown, out List<string> source))
            {
                source = new List<string>(dropdown.options.Count);
                foreach (TMP_Dropdown.OptionData option in dropdown.options)
                {
                    source.Add(option.text);
                }
                originalOptions[dropdown] = source;
            }

            bool changed = false;

            for (int i = 0; i < dropdown.options.Count && i < source.Count; i++)
            {
                string text = source[i];

                if (string.IsNullOrEmpty(text)) continue;

                string target = zhMap.TryGetValue(text, out string zh) ? zh : text;

                if (dropdown.options[i].text != target)
                {
                    dropdown.options[i].text = target;
                    changed = true;
                }
            }

            if (changed)
            {
                dropdown.RefreshShownValue();
            }
        }
    }

    public static void Restore()
    {
        foreach (KeyValuePair<TMP_Text, string> pair in originals)
        {
            if (pair.Key != null)
            {
                pair.Key.text = pair.Value;
            }
        }

        foreach (KeyValuePair<Text, string> pair in legacyOriginals)
        {
            if (pair.Key != null)
            {
                pair.Key.text = pair.Value;
            }
        }

        foreach (KeyValuePair<TMP_Dropdown, List<string>> pair in originalOptions)
        {
            TMP_Dropdown dropdown = pair.Key;

            if (dropdown == null || dropdown.options == null) continue;

            for (int i = 0; i < dropdown.options.Count && i < pair.Value.Count; i++)
            {
                dropdown.options[i].text = pair.Value[i];
            }

            dropdown.RefreshShownValue();
        }
    }

    private static void Prune()
    {
        if (originals.Count == 0) return;

        List<TMP_Text> dead = null;

        foreach (TMP_Text key in originals.Keys)
        {
            if (key == null)
            {
                if (dead == null) dead = new List<TMP_Text>();
                dead.Add(key);
            }
        }

        if (dead != null)
        {
            foreach (TMP_Text key in dead)
            {
                originals.Remove(key);
            }
        }

        if (legacyOriginals.Count > 0)
        {
            List<Text> deadLegacy = null;

            foreach (Text key in legacyOriginals.Keys)
            {
                if (key == null)
                {
                    if (deadLegacy == null) deadLegacy = new List<Text>();
                    deadLegacy.Add(key);
                }
            }

            if (deadLegacy != null)
            {
                foreach (Text key in deadLegacy) legacyOriginals.Remove(key);
            }
        }

        if (originalOptions.Count == 0) return;

        List<TMP_Dropdown> deadDropdowns = null;

        foreach (TMP_Dropdown key in originalOptions.Keys)
        {
            if (key == null)
            {
                if (deadDropdowns == null) deadDropdowns = new List<TMP_Dropdown>();
                deadDropdowns.Add(key);
            }
        }

        if (deadDropdowns == null) return;

        foreach (TMP_Dropdown key in deadDropdowns)
        {
            originalOptions.Remove(key);
        }
    }

    private static bool Load()
    {
        if (zhMap != null) return zhMap.Count > 0;
        if (loadFailed) return false;

        zhMap = new Dictionary<string, string>();

        // Bundled table first, then the on-disk override: wording can be corrected by editing
        // {exe}/translations/ui-zh-CN.json without rebuilding the player.
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);

        if (asset != null)
        {
            Merge(asset.text, "Resources/" + ResourcePath);
        }
        else
        {
            Debug.LogWarning(
                "[UiLocalization] Resources/" + ResourcePath + ".json is missing; using the on-disk override only.");
        }

        string overridePath = LocalOverridePath;

        if (!string.IsNullOrEmpty(overridePath) && System.IO.File.Exists(overridePath))
        {
            try
            {
                Merge(System.IO.File.ReadAllText(overridePath), overridePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[UiLocalization] could not read " + overridePath + ": " + ex.Message);
            }
        }

        if (zhMap.Count == 0)
        {
            loadFailed = true;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Translates a label that is concatenated at runtime (e.g. "Members Count: " + n).
    /// Returns the input unchanged when the table has no entry or the UI is not Chinese.
    /// </summary>
    public static string T(string english)
    {
        if (string.IsNullOrEmpty(english)) return english;

        if (Config.Instance == null || Config.Instance.Language != Language.Cn) return english;

        if (!Load()) return english;

        return zhMap.TryGetValue(english, out string translated) && !string.IsNullOrEmpty(translated)
            ? translated
            : english;
    }

    /// <summary>{exe}/translations/ui-zh-CN.json — wording fixes that survive without a rebuild.</summary>
    private static string LocalOverridePath
    {
        get
        {
            string root = System.IO.Directory.GetParent(Application.dataPath)?.FullName;

            return string.IsNullOrEmpty(root)
                ? null
                : System.IO.Path.Combine(root, "translations", "ui-zh-CN.json");
        }
    }

    /// <summary>
    /// Every TMP label needs a CJK-capable fallback, not just the Other panel: the camera and
    /// model panels render 中心环绕 / 躯干 etc. and showed tofu boxes without it.
    /// </summary>
    private static void ApplyCjkFallback(TMP_Text text)
    {
        if (cjkFont == null)
        {
            Font source = Resources.Load<Font>("Fonts/NotoSansCJKsc-Bold");

            if (source == null) return;

            cjkFont = TMP_FontAsset.CreateFontAsset(
                source, 90, 7, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic);

            if (cjkFont == null) return;

            cjkFont.name = "NotoSansCJK (dynamic)";
        }

        TMP_FontAsset font = text.font;

        if (font == null || font == cjkFont) return;

        if (font.fallbackFontAssetTable == null)
        {
            font.fallbackFontAssetTable = new List<TMP_FontAsset>(1);
        }

        if (!font.fallbackFontAssetTable.Contains(cjkFont))
        {
            font.fallbackFontAssetTable.Add(cjkFont);
        }
    }

    private static void Merge(string json, string source)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            Dictionary<string, string> table =
                JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

            if (table == null) return;

            foreach (KeyValuePair<string, string> pair in table)
            {
                if (!string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value))
                {
                    zhMap[pair.Key] = pair.Value;
                }
            }

            Debug.Log("[UiLocalization] loaded " + table.Count + " entries from " + source);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[UiLocalization] could not parse " + source + ": " + ex.Message);
        }
    }
}
