using Gallop;
using UnityEngine;
[DisallowMultipleComponent]
public sealed class JukeboxMusicSelect : MonoBehaviour
{
    public MasterJukeboxMusicData.JukeboxMusicData Music { get; private set; }
    public Sprite Jacket { get; private set; }

    private UmaViewerUI _owner;
    private UmaUIContainer _view;
    private JukeboxSelectionPulse _selectionPulse;

    public void Initialize(UmaViewerUI owner,UmaUIContainer view,MasterJukeboxMusicData.JukeboxMusicData music,Sprite jacket,string title)
    {
        _owner = owner;
        _view = view;
        Music = music;
        Jacket = jacket;

        name = $"Jukebox Music {music.MusicId}";
        _view.UseLegacyText();
        _view.Name = title;
        _view.Image.sprite = jacket;
        _view.Image.enabled = jacket != null;

        if (_view.ToggleImage != null)
        {
            _view.ToggleImage.enabled = false;
            _selectionPulse = _view.ToggleImage.GetComponent<JukeboxSelectionPulse>();
            if (_selectionPulse != null)
                _selectionPulse.enabled = false;
        }

        _view.Button.onClick.RemoveAllListeners();
        _view.Button.onClick.AddListener(SelectMusic);
    }

    public void SelectMusic()
    {
        if (_owner != null)
            _owner.SelectJukeboxMusic(this);
    }

    public void SetSelected(bool selected)
    {
        if (_view != null && _view.ToggleImage != null)
        {
            _view.ToggleImage.enabled = selected;
            if (_selectionPulse != null)
                _selectionPulse.enabled = selected;
        }
    }
}
