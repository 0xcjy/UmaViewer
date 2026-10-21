using UnityEngine;
using UnityEngine.UI;

public class LiveCharacterSelect : MonoBehaviour
{
    public CharaEntry CharaEntry;
    public string CostumeId;
    public string HeadCostumeId;
    // (Color) Color del traje comun elegido para este slot (0 = el de siempre). Los trajes comunes
    // con varios colores (p.ej. la camisa negra del 3.5周年) se cargan con este indice.
    public int CostumeColorIndex;
    public Image CharaImage;
    public Image CostumeImage;
    public Text IndexText;

    public void SelectChara(UmaViewerUI ui) 
    {
        if (!ui) return;
        ui.CurrentSeletChara = this;
        if (!ui.SelectCharacterPannel.activeInHierarchy)
            ui.ToggleUIPanel(ui.SelectCharacterPannel);
    }

    public void SetValue(CharaEntry charaentry, string costumeId, Sprite costumeSprite, string headCostumeId, int costumeColorIndex = 0)
    {
        CharaEntry = charaentry;
        CostumeId = costumeId;
        HeadCostumeId = headCostumeId;
        CostumeColorIndex = costumeColorIndex < 0 ? 0 : costumeColorIndex;
        CharaImage.enabled = true;
        CharaImage.sprite = charaentry.Icon;
        CostumeImage.sprite = costumeSprite;
    }
}

