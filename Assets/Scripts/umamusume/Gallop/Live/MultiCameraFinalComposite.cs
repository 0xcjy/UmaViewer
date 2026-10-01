using Gallop.RenderPipeline;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.Live
{
    [DisallowMultipleComponent]
    public class MultiCameraFinalComposite : MonoBehaviour
    {
        private static readonly int FadeId = Shader.PropertyToID("_FadeValue");
        private MultiCamera[] _cameras;
        private Camera _renderCamera;
        private Material _material;
        private Material _oneShotMaterial;
        private RenderTexture _framebufferCompositeTexture;
        private RenderTexture _compositeTexture;
        private float _fadeValue = 1f;
        private int _oneShotFrame = -1;
        private bool _oneShotTimeline;
        private bool _compositionEnabled;
        private int _preparedFrame = -1;

        public RenderTexture FramebufferCompositeTexture => _framebufferCompositeTexture;
        public RenderTexture CompositeTexture => _compositeTexture;
        public Camera RenderCamera => _renderCamera;
        public RenderTexture monitorTexture { get; set; }
        // Native: 0 = camera composition, 1 = frozen one-shot, 2 = capture transition.
        public int fadeType { get; set; }
        public float fadeValue
        {
            get => _fadeValue;
            set { if (value >= 0f && value <= 1f) _fadeValue = value; }
        }

        public void Initialize(MultiCamera[] cameras, Camera renderCamera)
        {
            _cameras = cameras;
            _renderCamera = renderCamera;
            MultiCameraComposite.InitializeRenderOrder(cameras == null ? 0 : cameras.Length);
            EnsureMaterials();
            PrepareTextures();
        }

        public void SetRenderCamera(Camera camera)
        {
            _renderCamera = camera;
            // Defer resize until beginFrameRendering, after this frame's timeline
            // has selected live composition or a retained Single-key image.
        }

        private bool EnsureMaterials()
        {
            if (_material == null)
            {
                Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.MultiCameraFinalComposite);
                if (shader != null) _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            // MakeRenderTexture 0x1a7c95a explicitly loads kind 193 as the third
            // material; using 192 for this path loses the native RGB fade semantics.
            if (_oneShotMaterial == null)
            {
                Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.MultiCameraOneShotFade);
                if (shader != null) _oneShotMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            return _material != null;
        }

        public void BeginTimelineFrame()
        {
            _oneShotTimeline = false;
        }

        public void BeginOneShotFade(int keyFrame)
        {
            _oneShotTimeline = true;
            _compositionEnabled = true;
            if (_oneShotFrame != keyFrame)
            {
                _oneShotFrame = keyFrame;
                fadeType = 2;
            }
            else if (fadeType != 2) fadeType = 1;
        }

        public void EndTimelineFrame()
        {
            if (_oneShotTimeline)
            {
                // Native disables the entire camera group after evaluating every
                // position key; a later key must not reactivate another camera.
                if (_cameras != null)
                    for (int i = 0; i < _cameras.Length; i++)
                        if (_cameras[i] != null) _cameras[i].gameObject.SetActive(false);
                return;
            }
            fadeType = 0;
            fadeValue = 1f;
            _oneShotFrame = -1;
            _compositionEnabled = false;
            if (_cameras == null) return;
            for (int i = 0; i < _cameras.Length; i++)
                if (_cameras[i] != null && _cameras[i].isActiveAndEnabled &&
                    _cameras[i].Composite.FadeValue > 0f) _compositionEnabled = true;
        }

        private void PrepareTextures()
        {
            if (_renderCamera == null) return;
            int width = Mathf.Max(1, _renderCamera.pixelWidth);
            int height = Mathf.Max(1, _renderCamera.pixelHeight);
            EnsureTexture(ref _framebufferCompositeTexture, width, height, "MultiCameraFramebufferComposite");
            // Keep the populated Single image at its previous dimensions until live
            // composition resumes; sampling already scales it to the destination.
            if (fadeType == 0 || _compositeTexture == null || !_compositeTexture.IsCreated())
                EnsureTexture(ref _compositeTexture, width, height, "MultiCameraOneShotComposite");
            if (_cameras == null) return;
            for (int i = 0; i < _cameras.Length; i++)
                if (_cameras[i] != null && _cameras[i].isActiveAndEnabled)
                    _cameras[i].MakeRenderTexture(width, height);
        }

        private static void EnsureTexture(ref RenderTexture texture, int width, int height, string name)
        {
            if (texture != null && texture.width == width && texture.height == height && texture.IsCreated()) return;
            ReleaseTexture(ref texture);
            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.DontSave
            };
            texture.Create();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
        }

        private void OnEnable()
        {
            _preparedFrame = -1;
            RenderPipelineManager.beginFrameRendering += OnBeginFrameCallback;
            RenderPipelineManager.endFrameRendering += OnEndFrameCallback;
        }

        // Native 0x1a7bdb0 clears only the framebuffer accumulation, never the
        // retained one-shot image. Resize target attachments before URP builds camera data.
        private void OnBeginFrameCallback(ScriptableRenderContext context, Camera[] cameras)
        {
            // URP may submit one context per camera, including reflections.
            // Clear once per Unity frame, not again before presentation.
            if (_preparedFrame == Time.frameCount) return;
            _preparedFrame = Time.frameCount;
            PrepareTextures();
            if (_cameras != null)
                for (int i = 0; i < _cameras.Length; i++)
                    if (_cameras[i] != null && _cameras[i].Composite != null)
                        _cameras[i].Composite.UpdateCameraDepthRenderOrder(1);
            MultiCameraComposite.ResetRenderOrder();
            if (_framebufferCompositeTexture == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("MultiCameraClear");
            cmd.SetRenderTarget(_framebufferCompositeTexture);
            cmd.ClearRenderTarget(false, true, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void OnEndFrameCallback(ScriptableRenderContext context, Camera[] cameras)
        {
            if (_cameras == null) return;
            for (int i = 0; i < _cameras.Length; i++)
                if (_cameras[i] != null && _cameras[i].Composite != null)
                    _cameras[i].Composite.RestoreCameraDepth();
        }

        // Called on the postprocessed presentation target, after the camera's final
        // image copy. Both native shaders BLEND onto existing color; do not blit into
        // an undefined temporary destination or sample/draw the same color target.
        public void AfterRenderingCallback(CommandBuffer cmd, RenderTargetIdentifier destination, bool projectionFlipped)
        {
            if (!isActiveAndEnabled || _compositeTexture == null || !EnsureMaterials()) return;
            if (fadeType == 2)
            {
                fadeType = 1;
                return;
            }
            if (_compositionEnabled && fadeValue > 0f)
            {
                Material material = fadeType == 0 ? _material : _oneShotMaterial;
                RenderTexture source = fadeType == 0 ? _framebufferCompositeTexture : _compositeTexture;
                if (material == null || source == null) return;
                // Auxiliary accumulation is a regular texture; the URP camera
                // attachment follows the GPU projection's vertical orientation.
                bool flip = fadeType == 0 && projectionFlipped;
                material.SetTextureScale("_MainTex", new Vector2(1f, flip ? -1f : 1f));
                material.SetTextureOffset("_MainTex", new Vector2(0f, flip ? 1f : 0f));
                material.SetFloat(FadeId, fadeValue);
                cmd.SetGlobalFloat(FadeId, fadeValue);
                if (monitorTexture != null && monitorTexture != source)
                    cmd.Blit(source, monitorTexture, material, 0);
                cmd.Blit(source, destination, material, 0);
            }
            if (fadeType == 0) cmd.Blit(destination, _compositeTexture);
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameCallback;
            RenderPipelineManager.endFrameRendering -= OnEndFrameCallback;
            if (_cameras != null)
                for (int i = 0; i < _cameras.Length; i++)
                    if (_cameras[i] != null) _cameras[i].ResetRenderTexture();
        }

        private void OnDestroy()
        {
            ReleaseTexture(ref _framebufferCompositeTexture);
            ReleaseTexture(ref _compositeTexture);
            if (_material != null) Destroy(_material);
            if (_oneShotMaterial != null) Destroy(_oneShotMaterial);
        }
    }
}
