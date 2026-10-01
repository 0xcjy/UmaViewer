using System.Collections;
using UnityEngine;

namespace Gallop
{
    public sealed class HomeJukeboxController : MonoBehaviour
    {
        private static HomeJukeboxController _instance;

        public static HomeJukeboxController Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<HomeJukeboxController>();
                if (_instance != null)
                    return _instance;

                var root = new GameObject(nameof(HomeJukeboxController));
                DontDestroyOnLoad(root);
                _instance = root.AddComponent<HomeJukeboxController>();
                return _instance;
            }
        }

        public int Music => _musicId;
        public bool IsLoadingMusic => _isLoadingMusic;
        public bool IsPlayingMusic => _isPlayingMusic;
        public bool IsPartsLoading => false;

        private int _musicId;
        private bool _isLoadingMusic;
        private Coroutine _loadingCoroutine;
        private bool _isPlayingMusic;
        private bool _playWhenReady;
        private bool _useGameSizeVersion;
        private bool _visible = true;

        private JukeboxUtil.LampType _lampType;
        private JukeboxUtil.LampAnimationType _lampAnimation;
        private int _nameTextureWidth = 8;
        private bool _isPowerOn;

        private readonly JukeboxAudioPlayer _audioPlayer = new JukeboxAudioPlayer();

        public bool GetVisible()
        {
            return _visible;
        }

        public void SetVisible(bool visible, bool isForce = false)
        {
            _visible = visible;
            if (!visible)
                StopMusic();
        }

        public void SetupMusic(int musicId)
        {
            JukeboxUtil.LampType lampType = JukeboxUtil.LampType.None;
            JukeboxUtil.LampAnimationType lampAnimation = JukeboxUtil.LampAnimationType.None;
            int nameTextureWidth = 8;
            bool isPowerOn = false;

            if (musicId != JukeboxUtil.NONE_MUSIC_ID)
            {
                MasterJukeboxMusicData master = UmaDatabaseController.Instance.JukeboxMusicData;
                MasterJukeboxMusicData.JukeboxMusicData data = master?.Get(musicId);
                if (data == null)
                {
                    Debug.LogError($"[HomeJukeboxController] Master data not found: musicId={musicId}");
                    SetupMusic(
                        JukeboxUtil.NONE_MUSIC_ID,
                        JukeboxUtil.LampType.None,
                        JukeboxUtil.LampAnimationType.None,
                        8,
                        false);
                    return;
                }

                lampType = (JukeboxUtil.LampType)data.LampColor;
                lampAnimation = (JukeboxUtil.LampAnimationType)data.LampAnimation;
                nameTextureWidth = data.NameTextureLength;
                isPowerOn = true;
            }

            SetupMusic(musicId, lampType, lampAnimation, nameTextureWidth, isPowerOn);
        }

        public void SetupMusic(
            int id,
            JukeboxUtil.LampType lampType,
            JukeboxUtil.LampAnimationType lampAnimation,
            int nameTextureWidth,
            bool isPowerOn)
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (_loadingCoroutine != null)
            {
                StopCoroutine(_loadingCoroutine);
                _loadingCoroutine = null;
            }

            _loadingCoroutine = StartCoroutine(
                CoroutineLoadingAndSettingMusic(
                    id,
                    lampType,
                    lampAnimation,
                    nameTextureWidth,
                    isPowerOn));
        }

        public void SetupMusicAndPlay(int musicId)
        {
            SetupMusicAndPlay(musicId, false);
        }

        public void SetupMusicAndPlay(int musicId, bool useGameSizeVersion)
        {
            // SetupMusic starts a coroutine, so PlayMusic() called immediately
            // would still see the previous _musicId before the first MoveNext.
            // Arm autoplay first and let the loading coroutine start playback
            // only after the requested cue has been prepared.
            _useGameSizeVersion = useGameSizeVersion;
            _playWhenReady = true;
            SetupMusic(musicId);
        }

        private IEnumerator CoroutineLoadingAndSettingMusic(
            int id,
            JukeboxUtil.LampType lampType,
            JukeboxUtil.LampAnimationType lampAnimation,
            int nameTextureWidth,
            bool isPowerOn)
        {
            _isLoadingMusic = true;
            _isPlayingMusic = false;

            _musicId = id;
            _lampType = lampType;
            _lampAnimation = lampAnimation;
            _nameTextureWidth = nameTextureWidth;
            _isPowerOn = isPowerOn;

            _audioPlayer.Clear();
            yield return null;

            yield return LoadingAndSettingMusic(isPowerOn);

            _isLoadingMusic = false;
            _loadingCoroutine = null;

            if (_playWhenReady && _musicId != JukeboxUtil.NONE_MUSIC_ID && _audioPlayer.IsPrepared)
            {
                _audioPlayer.Play();
                _isPlayingMusic = true;
            }
        }

        private IEnumerator LoadingAndSettingMusic(bool isPowerOn)
        {
            if (!isPowerOn || _musicId == JukeboxUtil.NONE_MUSIC_ID)
            {
                _audioPlayer.Clear();
                yield break;
            }

            MasterJukeboxMusicData master = UmaDatabaseController.Instance.JukeboxMusicData;
            MasterJukeboxMusicData.JukeboxMusicData data = master?.Get(_musicId);
            if (data == null)
            {
                Debug.LogError($"[HomeJukeboxController] Cannot load missing musicId={_musicId}");
                yield break;
            }

            // Allow the click frame to finish before file decoding starts.
            yield return null;

            if (!_audioPlayer.Prepare(data, _useGameSizeVersion, false, out string error))
            {
                Debug.LogError($"[HomeJukeboxController] {error}");
                if (UmaViewerUI.Instance != null)
                    UmaViewerUI.Instance.ShowMessage(error, UIMessageType.Error);
            }
        }

        public void PlayMusic()
        {
            if (_musicId == JukeboxUtil.NONE_MUSIC_ID)
            {
                StopMusic();
                return;
            }

            _playWhenReady = true;
            if (_isLoadingMusic)
                return;

            if (_audioPlayer.IsPrepared)
            {
                _audioPlayer.Play();
                _isPlayingMusic = true;
            }
        }

        public void StopMusic()
        {
            _playWhenReady = false;
            _isPlayingMusic = false;
            _audioPlayer.Stop();
        }

        public void Delete()
        {
            if (_loadingCoroutine != null)
            {
                StopCoroutine(_loadingCoroutine);
                _loadingCoroutine = null;
            }

            _audioPlayer.Dispose();
            _musicId = JukeboxUtil.NONE_MUSIC_ID;
            _isLoadingMusic = false;
            _isPlayingMusic = false;
            _playWhenReady = false;
        }

        public void EnableTouchCollider()
        {
        }

        public void DisableTouchCollider()
        {
        }

        public bool IsSameCollider(Collider collider)
        {
            return false;
        }

        private void OnDestroy()
        {
            Delete();
            if (_instance == this)
                _instance = null;
        }
    }
}
