using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Popup : MonoBehaviour
{
    [SerializeField] private TMP_Text Text;
    [SerializeField] private Button AcceptButton;
    [SerializeField] private Button RejectButton;
    [SerializeField] private RectTransform PopupBody;

    private System.Action _onAccept;
    private System.Action _onReject;

    public static Popup Create(string text, float width = -1, float height = -1, string acceptText = "Ok", System.Action onAccept = null, string rejectText = "", System.Action onReject = null)
    {
        var popup = Instantiate(Resources.Load<Popup>("Prefabs/FullScreenPopup"), UmaViewerUI.Instance.canvasScaler.transform);

        popup.Text.text = text;

        if (!string.IsNullOrEmpty(acceptText))
        {
            popup._onAccept = onAccept;
            popup.AcceptButton.GetComponentInChildren<TMP_Text>().text = acceptText;
        }
        if (!string.IsNullOrEmpty(rejectText))
        {
            popup._onReject = onReject;
            popup.RejectButton.GetComponentInChildren<TMP_Text>().text = rejectText;
        }

        var sizeDelta = popup.PopupBody.sizeDelta;
        if (width != -1)
        {
            sizeDelta.x = width;
        }
        if(height != -1)
        {
            sizeDelta.y = height;
        }
        popup.PopupBody.sizeDelta = sizeDelta;

        return popup;
    }

    // Popup SIN botones y NO cerrable (no se puede cerrar con clic). Pensado para procesos en
    // curso: se muestra mientras dura el trabajo y se cierra destruyendo su GameObject.
    public static Popup CreateProgress(string text)
    {
        var popup = Create(text, -1, 200);
        popup.HideButtons();
        return popup;
    }

    private void HideButtons()
    {
        // El prefab agrupa los dos botones bajo el nodo "Buttons"; ocultarlo los quita todos y
        // deja el popup sin ninguna opcion de cierre.
        Transform buttonsRow = null;
        if (AcceptButton != null) buttonsRow = AcceptButton.transform.parent;
        else if (RejectButton != null) buttonsRow = RejectButton.transform.parent;

        if (buttonsRow != null)
        {
            buttonsRow.gameObject.SetActive(false);
        }
        else
        {
            if (AcceptButton != null) AcceptButton.gameObject.SetActive(false);
            if (RejectButton != null) RejectButton.gameObject.SetActive(false);
        }
    }

    public void OnAcceptButtonClick()
    {
        _onAccept?.Invoke();
        Destroy(this.gameObject);
    }

    public void OnRejectButtonClick()
    {
        _onReject?.Invoke();
        Destroy(this.gameObject);
    }
}
