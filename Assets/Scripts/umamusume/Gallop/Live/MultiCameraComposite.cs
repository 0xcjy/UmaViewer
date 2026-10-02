using Gallop.ImageEffect;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.Live
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class MultiCameraComposite : MonoBehaviour
    {
        public enum DivideLineType { Fade = 0, Color = 1 }

        private static readonly int FadeId = Shader.PropertyToID("_FadeValue");
        private static readonly int TransformId = Shader.PropertyToID("_Transform");
        private static readonly int ThicknessId = Shader.PropertyToID("_LineThickness");
        private static readonly int ColorId = Shader.PropertyToID("_LineColor");
        private static readonly int AntialiasingId = Shader.PropertyToID("_LineAntialiasing");
        private static int[] _renderOrder;
        private static int _renderCount;
        private static bool _fixedRenderOrder;
        private static bool _useRenderOrder;

        private Material _material;
        private float _fadeValue;
        private float _depthBackup;
        private bool _isChangedDepth;
        private int _timelineIndex = -1;

        public GallopImageEffect ImageEffect { get; private set; }
        public Camera RenderCamera { get; private set; }
        public MultiCameraFinalComposite FinalComposite { get; private set; }
        public DivideLineType LineType { get; set; }
        public float LineThickness { get; set; } = 0.015f;
        public Color LineColor { get; set; } = Color.white;
        public float LineAntialiasing { get; set; } = 0.015f;
        public Vector4 TransformParameter { get; set; }
        public bool IsScreenDivide { get; set; } = true;
        public float Roll { get; private set; }
        public bool UpdateMainCamera { get; private set; }
        public Vector3 LayerOffsetMin { get; set; }
        public Vector3 LayerOffsetRange { get; set; }
        public Vector3 LayerOffset { get; private set; }

        // GetCameraLayerOffset 0x1ae0ea0 maps the selected characters' mean height
        // from 130..190cm onto the authored min/max range; it is not random jitter.
        public void UpdateLayerOffset(LiveCharaPositionFlag flags, ILiveTimelineCharactorLocator[] locators)
        {
            TryGetLayerOffset(flags, locators, out var offset);
            LayerOffset = offset;
        }

        private bool TryGetLayerOffset(LiveCharaPositionFlag flags,
            ILiveTimelineCharactorLocator[] locators, out Vector3 offset)
        {
            float height = 0f;
            int count = 0;
            if ((int)flags <= 0) height = 1f;
            else if (locators != null)
            {
                for (int i = 0; i < locators.Length && i < 20; i++)
                    if (flags.hasFlag((LiveCharaPosition)i) && locators[i] != null)
                    {
                        height += locators[i].liveCharaHeightValue;
                        count++;
                    }
                if (count > 1) height /= count;
            }
            offset = height > 0f ? LayerOffsetMin + LayerOffsetRange * ((height - 130f) / 60f) : Vector3.zero;
            return height > 0f;
        }

        // 0x1ad70b3..0x1ad7259: orient first, rotate the position and look-at
        // layer offsets into camera space, then apply authored local-axis roll.
        public Vector3 ApplyLookAt(Vector3 target, LiveCharaPositionFlag flags,
            ILiveTimelineCharactorLocator[] locators)
        {
            if (RenderCamera == null) return target;
            Transform cameraTransform = RenderCamera.transform;
            cameraTransform.LookAt(target);
            if (TryGetLayerOffset(flags, locators, out var lookAtOffset))
            {
                Quaternion rotation = cameraTransform.rotation;
                cameraTransform.position += rotation * LayerOffset;
                target += rotation * lookAtOffset;
                cameraTransform.LookAt(target);
            }
            cameraTransform.Rotate(0f, 0f, Roll);
            return target;
        }
        public float FadeValue
        {
            get => _fadeValue;
            set { if (value >= 0f && value <= 1f) _fadeValue = value; }
        }

        public void Initialize(MultiCameraFinalComposite owner, int timelineIndex)
        {
            FinalComposite = owner;
            _timelineIndex = timelineIndex;
            RenderCamera = GetComponent<Camera>();
            ImageEffect = GetComponent<GallopImageEffect>();
            if (ImageEffect == null) ImageEffect = gameObject.AddComponent<GallopImageEffect>();
            EnsureMaterial();
        }

        private bool EnsureMaterial()
        {
            if (_material != null) return true;
            Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.MultiCameraComposite);
            if (shader == null || shader.passCount < 2) return false;
            _material = new Material(shader) { hideFlags = HideFlags.DontSave };
            return true;
        }

        public static void InitializeRenderOrder(int cameraNum)
        {
            _renderOrder = new int[Mathf.Max(0, cameraNum)];
            _useRenderOrder = cameraNum > 0;
            _fixedRenderOrder = false;
            ResetRenderOrder();
        }

        public static void ResetRenderOrder()
        {
            _renderCount = 0;
            if (!_useRenderOrder || _fixedRenderOrder) return;
            for (int i = 0; i < _renderOrder.Length; i++) _renderOrder[i] = -1;
        }

        public static void FixRenderOrder()
        {
            _fixedRenderOrder = true;
        }

        // 0x1a7b3e0 sets baseDepth + learned ordinal, not currentDepth + ordinal.
        public void UpdateCameraDepthRenderOrder(int baseDepth)
        {
            if (!_useRenderOrder || RenderCamera == null || _timelineIndex < 0 ||
                _timelineIndex >= _renderOrder.Length || _renderOrder[_timelineIndex] < 0) return;
            RestoreCameraDepth();
            _depthBackup = RenderCamera.depth;
            RenderCamera.depth = baseDepth + _renderOrder[_timelineIndex];
            _isChangedDepth = true;
        }

        public void RestoreCameraDepth()
        {
            if (_isChangedDepth && RenderCamera != null) RenderCamera.depth = _depthBackup;
            _isChangedDepth = false;
        }

        public void ResetRenderTexture()
        {
            MultiCamera camera = GetComponent<MultiCamera>();
            if (camera != null) camera.ResetRenderTexture();
            else if (RenderCamera != null) RenderCamera.targetTexture = null;
        }

        // 0x1ad7370 / 0x1ad8710: switch fade is in seconds rounded to 60-Hz frames;
        // geometric/color interpolation belongs to the next key.
        public void ApplyTimeline(LiveTimelineKeyMultiCameraPositionData current,
            LiveTimelineKeyMultiCameraPositionData next, float frame)
        {
            if (current == null) return;
            float t = next != null && next.IsInterpolateKey()
                ? LiveTimelineControl.CalculateInterpolationValue(current, next, frame) : 0f;
            if (next == null) next = current;
            float duration = Mathf.Round(current.fadeTime * 60f);
            bool transitioning = duration > 0f && frame < current.frame + duration;
            float progress = transitioning ? Mathf.Clamp01((frame - current.frame) / duration) : 1f;
            bool divided = current.maskType != LiveTimelineKeyMultiCameraPositionData.MaskType.Single;
            bool visible = current.enableMultiCamera && divided;
            FadeValue = visible ? progress : 1f - progress;
            IsScreenDivide = current.maskType != LiveTimelineKeyMultiCameraPositionData.MaskType.All;
            LineType = current.LineType;
            LineThickness = Mathf.LerpUnclamped(current.lineThickness, next.lineThickness, t);
            LineColor = Color.LerpUnclamped(current.LineColor, next.LineColor, t);
            LineAntialiasing = Mathf.LerpUnclamped(current.LineAntialiasing, next.LineAntialiasing, t);
            float maskRoll = Mathf.LerpUnclamped(current.maskRoll, next.maskRoll, t);
            Vector2 offset = Vector2.LerpUnclamped(current.maskOffset, next.maskOffset, t);
            float centralAngle = Mathf.LerpUnclamped(current.MaskCentralAngle, next.MaskCentralAngle, t);
            TransformParameter = new Vector4(offset.x, offset.y,
                (GetMaskRoll(current.maskType) + maskRoll) / 360f,
                current.maskType == LiveTimelineKeyMultiCameraPositionData.MaskType.Fan
                    ? (90f - centralAngle * 0.5f) / 360f : 0f);
            Roll = Mathf.LerpUnclamped(current.roll, next.roll, t);
            UpdateMainCamera = current.updateMainCamera;
            if (RenderCamera != null)
            {
                RenderCamera.nearClipPlane = current.nearClip;
                RenderCamera.farClipPlane = current.farClip;
                RenderCamera.fieldOfView = Mathf.LerpUnclamped(current.fov, next.fov, t);
                if (((int)current.attribute & 0x40000) != 0)
                    RenderCamera.backgroundColor = current.BgColor;
            }
            if (current.maskType == LiveTimelineKeyMultiCameraPositionData.MaskType.Single && transitioning)
            {
                FinalComposite.BeginOneShotFade(current.frame);
                FinalComposite.fadeValue = FadeValue;
                gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(visible || transitioning);
            }
        }

        public static float GetMaskRoll(LiveTimelineKeyMultiCameraPositionData.MaskType mask)
        {
            switch (mask)
            {
                case LiveTimelineKeyMultiCameraPositionData.MaskType.Down: return 180f;
                case LiveTimelineKeyMultiCameraPositionData.MaskType.Left: return -90f;
                case LiveTimelineKeyMultiCameraPositionData.MaskType.Right: return 90f;
                case LiveTimelineKeyMultiCameraPositionData.MaskType.LeftUp: return -45f;
                case LiveTimelineKeyMultiCameraPositionData.MaskType.RightUp: return 45f;
                case LiveTimelineKeyMultiCameraPositionData.MaskType.LeftDown: return -135f;
                case LiveTimelineKeyMultiCameraPositionData.MaskType.RightDown: return 135f;
                default: return 0f;
            }
        }

        public bool AfterRenderingCallback(CommandBuffer cmd, RenderTargetIdentifier source)
        {
            if (!isActiveAndEnabled || FinalComposite == null || !FinalComposite.isActiveAndEnabled) return false;
            bool rendered = false;
            if (FadeValue > 0f && FinalComposite != null &&
                FinalComposite.FramebufferCompositeTexture != null && EnsureMaterial())
            {
                _material.SetFloat(FadeId, FadeValue);
                _material.SetVector(TransformId, TransformParameter);
                _material.SetFloat(ThicknessId, IsScreenDivide ? Mathf.Max(0.001f, LineThickness) : -1f);
                _material.SetColor(ColorId, LineColor);
                _material.SetFloat(AntialiasingId, LineAntialiasing);
                cmd.Blit(source, FinalComposite.FramebufferCompositeTexture, _material, (int)LineType);
                rendered = true;
            }
            if (_useRenderOrder && !_fixedRenderOrder && _timelineIndex >= 0 &&
                _timelineIndex < _renderOrder.Length) _renderOrder[_timelineIndex] = _renderCount++;
            RestoreCameraDepth();
            return rendered;
        }

        private void OnDisable() { RestoreCameraDepth(); }
        private void OnDestroy()
        {
            RestoreCameraDepth();
            if (_material != null) Destroy(_material);
        }
    }
}
