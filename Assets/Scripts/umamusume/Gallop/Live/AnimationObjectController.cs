using System.Collections.Generic;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.Live
{
    /// <summary>
    /// Drives the legacy <see cref="Animation"/> that some stage prefabs ship with (the
    /// `blinklight_wash_*` families). Those prefabs have `m_Animation = 0` (no default clip), so
    /// `playAutomatically` plays nothing and the object stays static.
    ///
    /// The clip comes from the `animationList` timeline track
    /// (<see cref="LiveTimelineControl.OnUpdateAnimation"/>): `animationId` indexes the states of
    /// the <see cref="Animation"/> component and `offsetTime` / `speed` place the playhead.
    ///
    /// Song 1001 data (`pfb_env_live10102_blinklight_wash_ground_a_side`): states `..._loop_000`
    /// (5.9667 s) and `..._open` (7.3 s); track `AnimeObjA` keys frame 0 -> id 1 (Clamp, speed
    /// 1.2), 286 -> id 0 (Loop, speed 1), 4196 -> id 1 (Loop, speed 4.68, offset 1) and
    /// 4231 / 4645 / 4685 -> id 0 (Loop, offsets -0.09 / 0.36 / 0). The last keys re-seek the very
    /// same clip with a different offset, so the playhead is written every frame instead of only
    /// on a clip change; writing it also keeps a paused live and slider seeks in sync.
    /// </summary>
    [AddComponentMenu("")]
    public class AnimationObjectController : MonoBehaviour
    {
        private Animation _animation;
        private string[] _clipNames = new string[0];
        private int _currentClip = -1;
        private bool _initialized;
        private bool _driven;

        public bool IsInitialized { get { return _initialized; } }

        public bool HasClips { get { return _clipNames != null && _clipNames.Length > 0; } }

        private void OnEnable()
        {
            Initialize();
        }

        /// <summary>
        /// Collects the clip names of the object's <see cref="Animation"/> (their order is the
        /// `animationId` index) and starts the clip the prefab already had, if any. A prefab with
        /// no default clip stays stopped until the timeline sends a key.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
                return;
            _initialized = true;

            _animation = GetComponent<Animation>();
            if (_animation == null)
                _animation = GetComponentInChildren<Animation>(true);

            _currentClip = -1;
            _clipNames = new string[0];

            if (_animation == null)
                return;

            var names = new List<string>();
            foreach (AnimationState state in _animation)
                names.Add(state != null && state.clip != null ? state.clip.name : null);
            _clipNames = names.ToArray();

            if (_animation.clip != null)
            {
                // Prefab with a default clip (e.g. `Take 001`): Unity plays it on its own.
                _animation.Play();
                _currentClip = IndexOfClip(_animation.clip.name);
            }
            else
            {
                // No default clip: nothing runs until the timeline picks one.
                _animation.Stop();
            }
        }

        /// <summary>
        /// Applies one `animationList` key: selects the clip for `animationId` and places the
        /// playhead at `offsetTime + progressTime * speed`, wrapped or clamped by `wrapMode`.
        /// </summary>
        public void UpdateStatus(ref AnimationUpdateInfo info)
        {
            _driven = true;
            if (!_initialized)
                Initialize();

            if (_animation == null || _clipNames.Length == 0)
                return;

            int index = info.animationId;
            if (index < 0 || index >= _clipNames.Length)
                return;

            string clipName = _clipNames[index];
            if (string.IsNullOrEmpty(clipName))
                return;

            if (_currentClip != index)
            {
                _currentClip = index;
                _animation.wrapMode = info.wrapMode;
                _animation.Play(clipName);
            }

            AnimationState state = _animation[clipName];
            if (state == null)
                return;

            // The playhead is written below, so the state itself must not advance: `speed` is
            // applied when computing the time instead (same result, but seek/pause exact).
            state.speed = 0f;
            _animation.wrapMode = info.wrapMode;

            float length = state.clip != null ? state.clip.length : 0f;
            if (length <= 0f)
            {
                state.time = 0f;
                return;
            }

            float time = info.offsetTime + info.progressTime * info.speed;
            if (info.wrapMode == WrapMode.Loop)
                time = Mathf.Repeat(time, length);
            else
                time = Mathf.Clamp(time, 0f, length);
            state.time = time;
        }

        // Objetos que NO reciben ninguna clave de animationList (los haces del escenario:
        // `anm_<stage>_blinkbeamlight_..._loop_000`). Su clip nunca se reproduce porque el prefab no
        // lo tiene asignado como clip por defecto, asi que se quedaban en la pose de reposo
        // (apuntando arriba). Aqui se reproduce su propio clip en bucle. Si la pista los controla
        // (_driven), no se toca nada.
        private void LateUpdate()
        {
            if (!_initialized || _driven || _animation == null) return;
            if (_clipNames.Length != 1 || string.IsNullOrEmpty(_clipNames[0])) return;
            AnimationState state = _animation[_clipNames[0]];
            if (state == null || state.clip == null) return;
            state.wrapMode = WrapMode.Loop;
            state.speed = 1f;
            _animation.wrapMode = WrapMode.Loop;
            if (!_animation.isPlaying) _animation.Play(_clipNames[0]);
        }

        private int IndexOfClip(string clipName)
        {
            for (int i = 0; i < _clipNames.Length; i++)
            {
                if (_clipNames[i] == clipName)
                    return i;
            }
            return -1;
        }
    }
}
