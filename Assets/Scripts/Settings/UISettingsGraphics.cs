using System;
using System.Collections.Generic;
using UnityEngine;

public static class UISettingsGraphics
{
    private static readonly int[] Rates = { 60, 30, 90, 120, 144, 165, 240, 0 };

    // Los helpers de frame rate reciben el valor "actual" por parámetro: hay DOS ajustes
    // independientes y cada panel edita el suyo. El viewer (Version2) usa `Config.UIFrameRate` y el
    // live usa `Config.TargetFrameRate`.
    public static List<string> FrameRateLabels(int current)
    {
        var labels = new List<string>();
        foreach (int rate in Rates) labels.Add(rate == 0 ? "Unlimited" : rate + " FPS");
        if (FrameRateIndexOf(current) == Rates.Length) labels.Add(current + " FPS");
        return labels;
    }

    public static int FrameRateIndexOf(int current)
    {
        int rate = Math.Max(0, current);
        int index = Array.IndexOf(Rates, rate);
        return index < 0 ? Rates.Length : index;
    }

    public static int FrameRateAt(int index, int fallback)
    {
        return index >= 0 && index < Rates.Length ? Rates[index] : fallback;
    }

    //(Los controles del panel de ajustes del live viven en la escena `LiveScene` (YAML) y los cablea
    //LiveGraphicsSettingsUI. Aqui solo queda lo que aplica el cambio en `Config` y en el runtime.)
    public static void SaveGraphics()
    {
        Config.Instance.UpdateConfig(false);
        RuntimeGraphicsSettings.ApplyLiveRenderScale();
        if (UmaViewerUI.Instance != null && UmaViewerUI.Instance.CameraSettings != null)
            UmaViewerUI.Instance.CameraSettings.AAModeDropdown.SetValueWithoutNotify(Config.Instance.AntiAliasing);
    }
}
