using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//(Grabación del live) Campo de texto que coloca el cursor donde se hace clic.
//
//POR QUÉ
//-------
//El InputField de uGUI selecciona TODO el texto al recibir el foco (InputField.OnFocus -> SelectAll).
//Con eso, un clic en medio de "Hola buenos dias" seleccionaba la frase entera y, al escribir, la borraba.
//Aquí el primer clic coloca el cursor en el punto pulsado, como en cualquier campo de texto normal.
//
//CÓMO Y POR QUÉ ASÍ
//------------------
//La activación del campo (y su SelectAll) ocurre en el LateUpdate del propio InputField, es decir,
//DESPUÉS de cualquier Update/corrutina. Por eso el cursor no se coloca en OnPointerClick ni en una
//corrutina (el SelectAll lo pisaría): se hace en 'Canvas.willRenderCanvases', que se dispara al final
//del frame, después del LateUpdate y antes de dibujar. Así el usuario no llega a ver la selección.
//En los clics posteriores uGUI ya coloca el cursor por su cuenta ('OnPointerClick' con
//'hadFocusBefore'), así que solo se interviene en el primero.
public class LiveRecordTextField : InputField
{
    //Posición de dibujado del texto (si el contenido va desplazado dentro del campo). Es privada en
    //uGUI, así que se lee por reflexión para sumarla al índice del carácter.
    private static readonly FieldInfo DrawStartField =
        typeof(InputField).GetField("m_DrawStart", BindingFlags.Instance | BindingFlags.NonPublic);

    private bool _pendingCaret;
    private Vector2 _clickPosition;
    private Camera _clickCamera;

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        bool hadFocus = isFocused;
        base.OnPointerClick(eventData);
        if (hadFocus) return;

        _pendingCaret = true;
        _clickPosition = eventData.position;
        _clickCamera = eventData.pressEventCamera;

        Canvas.willRenderCanvases -= ApplyPendingCaret;
        Canvas.willRenderCanvases += ApplyPendingCaret;
    }

    private void ApplyPendingCaret()
    {
        if (!_pendingCaret) return;
        if (!isFocused) return;   //aún no está activo: se reintenta en el siguiente frame

        _pendingCaret = false;
        Canvas.willRenderCanvases -= ApplyPendingCaret;

        if (textComponent == null) return;

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                textComponent.rectTransform, _clickPosition, _clickCamera, out local))
            return;

        int index = GetCharacterIndexFromPosition(local);
        if (DrawStartField != null)
        {
            object value = DrawStartField.GetValue(this);
            if (value is int) index += (int)value;
        }
        index = Mathf.Clamp(index, 0, text.Length);

        //Pone ancla y foco en el mismo punto: sin selección, cursor donde se ha pulsado.
        caretPosition = index;
        ForceLabelUpdate();
    }

    private void OnDisable()
    {
        _pendingCaret = false;
        Canvas.willRenderCanvases -= ApplyPendingCaret;
    }
}
