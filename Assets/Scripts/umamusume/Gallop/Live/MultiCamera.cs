using Gallop.RenderPipeline;
using UnityEngine;

namespace Gallop.Live
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera), typeof(MultiCameraComposite))]
    public class MultiCamera : MonoBehaviour
    {
        private Camera _camera;
        private RenderTexture _captureTexture;
        private RenderTexture _originalTarget;
        private bool _ownsTarget;

        public MultiCameraComposite Composite { get; private set; }
        public MultiCameraFinalComposite FinalComposite { get; private set; }
        public RenderTexture CaptureTexture => _captureTexture;
        public int TimelineIndex { get; private set; } = -1;
        public PostImageEffectFeature.Parameter PostEffectParameter { get; } = new PostImageEffectFeature.Parameter();

        public Camera GetCamera()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            return _camera;
        }

        public void Initialize(MultiCameraFinalComposite owner, int timelineIndex)
        {
            FinalComposite = owner;
            TimelineIndex = timelineIndex;
            Composite = GetComponent<MultiCameraComposite>();
            Composite.Initialize(owner, timelineIndex);
            PostEffectParameter.TargetCamera = GetCamera();
        }

        // Native MakeRenderTexture (0x1a7c710) uses virtual-resolution ARGB32,
        // no MSAA/mips. URP scene captures additionally need their own depth attachment.
        public void MakeRenderTexture(int width, int height)
        {
            Camera camera = GetCamera();
            if (_captureTexture == null || _captureTexture.width != width ||
                _captureTexture.height != height || !_captureTexture.IsCreated())
            {
                ResetRenderTexture();
                if (_captureTexture != null)
                {
                    _captureTexture.Release();
                    Destroy(_captureTexture);
                }
                _captureTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "MultiCameraCapture_" + TimelineIndex,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    antiAliasing = 1,
                    useMipMap = false,
                    autoGenerateMips = false,
                    hideFlags = HideFlags.DontSave
                };
                _captureTexture.Create();
            }
            if (!_ownsTarget)
            {
                _originalTarget = camera.targetTexture;
                _ownsTarget = true;
            }
            camera.targetTexture = _captureTexture;
        }

        public void ResetRenderTexture()
        {
            Camera camera = GetCamera();
            if (_ownsTarget && camera.targetTexture == _captureTexture)
                camera.targetTexture = _originalTarget;
            _originalTarget = null;
            _ownsTarget = false;
        }

        public void DisableCameraTimeline()
        {
            if (Composite != null) Composite.FadeValue = 0f;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            ResetRenderTexture();
        }

        private void OnDestroy()
        {
            ResetRenderTexture();
            if (_captureTexture != null)
            {
                _captureTexture.Release();
                Destroy(_captureTexture);
                _captureTexture = null;
            }
        }
    }
}
