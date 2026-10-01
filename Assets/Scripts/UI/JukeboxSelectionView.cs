using UnityEngine;
using UnityEngine.UI;
public sealed class JukeboxSelectionView : MonoBehaviour
{
    [Header("图片窗口")]
    public Button CloseButton;

    [Header("歌曲选择")]
    public Image SelectedJacket;
    public GameObject SelectedJacketPlaceholder;
    public Text SelectedInfo;

    [Header("BGM version")]
    public Toggle ShortVersionToggle;
    public Toggle GameSizeVersionToggle;

    [Header("Song grid")]
    public ScrollRect SongList;
    public UmaUIContainer SongItemPrefab;
}
