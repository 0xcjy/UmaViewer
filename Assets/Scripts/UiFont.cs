using UnityEngine;

/// <summary>
/// 统一的中文字体来源，给遗留的 UnityEngine.UI.Text 用。
///
/// 这些控件原先用的是 Resources.GetBuiltinResource&lt;Font&gt;("LegacyRuntime.ttf")，
/// 那是 Arial 的替代品，**一个汉字都没有**；旧版 Text 找不到字形时会交给操作系统逐字挑字体，
/// 于是同一行里「錦」和「贊頌」会和其它字用上不同的字体。
///
/// CreateDynamicFontFromOSFont 直接把整段文字交给同一个系统字体渲染，字形自然统一。
/// </summary>
public static class UiFont
{
    private static Font cached;

    /// <summary>按优先级挑一个带完整汉字的系统字体名。</summary>
    private static readonly string[] Candidates =
    {
        // Windows
        "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "SimSun",
        // macOS
        "PingFang SC", "Hiragino Sans GB", "Heiti SC",
        // Linux / Android
        "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "WenQuanYi Micro Hei",
        // 最后兜底：Unity 内置（无汉字，仅保证不返回 null）
        "Arial",
    };

    /// <summary>TMP 这边统一用 Noto Sans CJK，legacy Text 也取同一份字体文件。</summary>
    private const string BundledFontPath = "Fonts/NotoSansCJKsc-Bold";

    public static Font Get(int size = 24)
    {
        if (cached != null)
            return cached;

        // 优先用工程自带的那份，保证和 TMP 的字形完全一致
        Font bundled = Resources.Load<Font>(BundledFontPath);

        if (bundled != null)
        {
            cached = bundled;
            Debug.Log("[UiFont] legacy Text 使用内置字体: " + BundledFontPath);
            return cached;
        }

        // 逐个试，CreateDynamicFontFromOSFont 找不到字体会返回 null
        foreach (string family in Candidates)
        {
            Font f = Font.CreateDynamicFontFromOSFont(family, size);

            if (f != null)
            {
                cached = f;
                Debug.Log($"[UiFont] legacy Text 使用系统字体: {family}");
                return cached;
            }
        }

        cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Debug.LogWarning("[UiFont] 没有可用的系统中文字体，回退到内置 Arial（中文可能逐字换字体）");
        return cached;
    }
}
