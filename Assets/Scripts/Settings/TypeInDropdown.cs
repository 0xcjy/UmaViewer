using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Desplegable con lista de presets que ADEMAS se puede escribir: lo tecleado se adopta como valor
// (con un rango permitido y una etiqueta opcionales). Lo comparten el panel de ajustes del viewer
// (TMP_Dropdown) y el del live (Dropdown); la logica vive aqui y cada tipo de desplegable la
// implementa en su subclase.
public abstract class TypeInDropdown
{
    private const float TypingIdleSeconds = 0.7f;   // Se adopta lo escrito cuando se deja de teclear.
    private const int MaxTypedDigits = 5;

    private readonly List<int> values = new List<int>();
    private int min;
    private int max;
    private string suffix;
    private Func<int, string> label;
    private Action<int> apply;

    private bool typing;
    private string typed = "";
    private float idle;

    // Rect completo del desplegable (rotulo + flecha), para saber si el clic cae dentro.
    public RectTransform Rect { get; private set; }
    public bool Typing { get { return typing; } }

    // Se llama desde el constructor de cada subclase, DESPUES de asignar su desplegable.
    protected void Initialize(Component dropdown, int[] presets, int minValue, int maxValue, string suffixText, Func<int, string> labelText, Action<int> applyValue)
    {
        Rect = dropdown.transform as RectTransform;
        min = minValue;
        max = maxValue;
        suffix = suffixText;
        label = labelText;
        apply = applyValue;

        ResetValueListeners();
        ClearOptions();
        foreach (int v in presets)
        {
            values.Add(v);
            AddOption(Format(v));
        }
        AddValueListener(OnOptionChanged);
    }

    // Muestra `value` en el desplegable; si no esta en la lista, lo anade.
    public void SetValue(int value)
    {
        if (max > min) value = Mathf.Clamp(value, min, max);

        int idx = values.IndexOf(value);
        if (idx < 0)
        {
            values.Add(value);
            AddOption(Format(value));
            idx = values.Count - 1;
        }
        SetSelection(idx);
    }

    public void BeginTyping()
    {
        typing = true;
        typed = "";
        idle = 0f;
    }

    public void Update()
    {
        if (!typing) return;
        if (!IsUsable())
        {
            Commit();
            return;
        }

        string entrada = Input.inputString;
        if (!string.IsNullOrEmpty(entrada))
        {
            foreach (char c in entrada)
            {
                if (c == '\b')
                {
                    if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1);
                }
                else if (c >= '0' && c <= '9' && typed.Length < MaxTypedDigits)
                {
                    typed += c;
                }
            }
            idle = 0f;
        }

        // Mientras se teclea, el rotulo ensena lo escrito.
        RefreshCaption();

        if (typed.Length > 0)
        {
            idle += Time.unscaledDeltaTime;
            if (idle >= TypingIdleSeconds) Commit();
        }
    }

    // Al dejar de teclear se adopta el numero (dentro del rango, si lo hay). Si no habia nada, se
    // restaura el rotulo con la opcion elegida.
    public void Commit()
    {
        bool applied = false;
        int value;
        if (typing && typed.Length > 0 && int.TryParse(typed, out value))
        {
            if (max > min) value = Mathf.Clamp(value, min, max);
            apply(value);
            applied = true;
        }
        typing = false;
        typed = "";
        if (!applied) RefreshCaption();
    }

    private string Format(int value)
    {
        return label != null ? label(value) : value + suffix;
    }

    private void OnOptionChanged(int index)
    {
        if (index < 0 || index >= values.Count) return;
        apply(values[index]);
    }

    private void RefreshCaption()
    {
        string texto = typed ?? "";
        if (texto.Length == 0) texto = SelectedOptionText();
        SetCaption(texto);
    }

    protected abstract void ClearOptions();
    protected abstract void AddOption(string text);
    protected abstract string SelectedOptionText();
    protected abstract void SetSelection(int index);
    protected abstract void SetCaption(string text);
    protected abstract void ResetValueListeners();
    protected abstract void AddValueListener(UnityAction<int> action);
    protected abstract bool IsUsable();
}

// Version para los desplegables del panel del viewer (TextMeshPro).
public class TmpTypeInDropdown : TypeInDropdown
{
    private readonly TMP_Dropdown dd;

    public TmpTypeInDropdown(TMP_Dropdown dropdown, int[] presets, int minValue, int maxValue, string suffixText, Action<int> applyValue)
        : this(dropdown, presets, minValue, maxValue, suffixText, null, applyValue)
    {
    }

    public TmpTypeInDropdown(TMP_Dropdown dropdown, int[] presets, int minValue, int maxValue, string suffixText, Func<int, string> labelText, Action<int> applyValue)
    {
        dd = dropdown;
        Initialize(dropdown, presets, minValue, maxValue, suffixText, labelText, applyValue);
    }

    protected override void ClearOptions() { dd.ClearOptions(); }

    protected override void AddOption(string text) { dd.options.Add(new TMP_Dropdown.OptionData(text)); }

    protected override string SelectedOptionText()
    {
        return dd.value >= 0 && dd.value < dd.options.Count ? dd.options[dd.value].text : "";
    }

    protected override void SetSelection(int index)
    {
        dd.SetValueWithoutNotify(index);
        dd.RefreshShownValue();
    }

    protected override void SetCaption(string text)
    {
        if (dd.captionText == null) return;
        if (!string.Equals(dd.captionText.text, text, StringComparison.Ordinal)) dd.captionText.text = text;
    }

    protected override void ResetValueListeners() { dd.onValueChanged = new TMP_Dropdown.DropdownEvent(); }

    protected override void AddValueListener(UnityAction<int> action) { dd.onValueChanged.AddListener(action); }

    protected override bool IsUsable() { return dd.gameObject.activeInHierarchy && dd.IsInteractable(); }
}

// Version para los desplegables del panel del live (UI clasica).
public class LegacyTypeInDropdown : TypeInDropdown
{
    private readonly Dropdown dd;

    public LegacyTypeInDropdown(Dropdown dropdown, int[] presets, int minValue, int maxValue, string suffixText, Action<int> applyValue)
        : this(dropdown, presets, minValue, maxValue, suffixText, null, applyValue)
    {
    }

    public LegacyTypeInDropdown(Dropdown dropdown, int[] presets, int minValue, int maxValue, string suffixText, Func<int, string> labelText, Action<int> applyValue)
    {
        dd = dropdown;
        Initialize(dropdown, presets, minValue, maxValue, suffixText, labelText, applyValue);
    }

    protected override void ClearOptions() { dd.ClearOptions(); }

    protected override void AddOption(string text) { dd.options.Add(new Dropdown.OptionData(text)); }

    protected override string SelectedOptionText()
    {
        return dd.value >= 0 && dd.value < dd.options.Count ? dd.options[dd.value].text : "";
    }

    protected override void SetSelection(int index)
    {
        dd.SetValueWithoutNotify(index);
        dd.RefreshShownValue();
    }

    protected override void SetCaption(string text)
    {
        if (dd.captionText == null) return;
        if (!string.Equals(dd.captionText.text, text, StringComparison.Ordinal)) dd.captionText.text = text;
    }

    protected override void ResetValueListeners() { dd.onValueChanged = new Dropdown.DropdownEvent(); }

    protected override void AddValueListener(UnityAction<int> action) { dd.onValueChanged.AddListener(action); }

    protected override bool IsUsable() { return dd.gameObject.activeInHierarchy && dd.IsInteractable(); }
}
