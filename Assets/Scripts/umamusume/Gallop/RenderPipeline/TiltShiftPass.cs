using System;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Recovered TiltShift effect.
    ///
    /// Native reference: Gallop.ImageEffect.TiltShift.OnRenderImage (0x7ffffc987e20).
    ///  1. full-resolution temporary (native format 7 / ARGB4444, replaced by the camera target
    ///     format because not every device can render B4G4R4A4);
    ///     source.filterMode is forced to Bilinear;
    ///  2. _BlurSize = max(0, MaxBlurSize), _BlurArea = BlurArea,
    ///     _Params = (Offset.x, Offset.y, sin(Roll°), cos(Roll°));
    ///  3. when Downsample > 0 a second temporary at (w >> Downsample, h >> Downsample) in the
    ///     *source* format (RenderTexture.get_format(source));
    ///  4. Graphics.Blit(source, target, material, pass) with
    ///     pass = Quality * 2 + (Mode == TiltShiftMode ? 0 : 1);
    ///  5. when Downsample > 0, _Blurred = that temporary and
    ///     Graphics.Blit(source, fullResolution, material, 6);
    ///  6. the full-resolution temporary is what the caller chain continues with.
    ///
    /// TiltShiftParam.get_IsValidity is `Mode != None`, so Mode 0 disables the effect.
    /// Native chain position: after DofDiffusionBloomOverlay and before RadialBlur.
    /// </summary>
    public sealed class TiltShiftPass : ScriptableRenderPass, IDisposable
    {
        // TiltShiftParam.TiltShiftMode
        public const int ModeNone = 0;
        public const int ModeTiltShift = 1;
        public const int ModeIris = 2;

        [Serializable]
        public struct Parameter
        {
            public int Mode, Quality, Downsample;
            public float BlurArea, MaxBlurSize, Roll;
            public Vector2 Offset;

            // TiltShiftParam.get_IsValidity: Mode != None.
            public bool IsEnable => Mode != ModeNone;

            public bool IsValid => IsEnable && Downsample >= 0 && Downsample < 16 &&
                Finite(BlurArea) && Finite(MaxBlurSize) && Finite(Roll) &&
                Finite(Offset.x) && Finite(Offset.y);

            private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private const int kCompositePass = 6;
        private const float kDeg2Rad = 0.017453292f;
        // Native format 7 (ARGB4444 / B4G4R4A4) is not renderable on every device
        // (RenderTexture.Create fails with "color format unsupported"), so the temporaries use the
        // camera target format instead.

        private Material _material;
        private Parameter _parameter;
        private PostImageEffectFeature _feature;
        private static readonly int Full = Shader.PropertyToID("_RecoveredTiltShiftFull");

        public TiltShiftPass(RenderPassEvent evt) { renderPassEvent = evt; }

        public bool Setup(Parameter parameter, PostImageEffectFeature feature)
        {
            _parameter = parameter;
            _feature = feature;
            if (!parameter.IsValid) return false;
            Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.TiltShiftHdrLensBlur);
            if (shader == null || !shader.isSupported) return false;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            else if (_material.shader != shader) _material.shader = shader;
            // Pass 6 is the full-resolution composite; without it the effect cannot finish.
            return _material.passCount > kCompositePass;
        }

        public static int ShaderPass(int mode, int quality)
        {
            // Quality * 2 + (Mode == TiltShiftMode ? 0 : 1)
            return Mathf.Clamp(quality, 0, 2) * 2 + (mode == ModeTiltShift ? 0 : 1);
        }

        public static void ApplyMaterial(Material material, Parameter p)
        {
            material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._BlurSize),
                p.MaxBlurSize >= 0f ? p.MaxBlurSize : 0f);
            material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._BlurArea), p.BlurArea);
            float roll = p.Roll * kDeg2Rad;
            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._Params),
                new Vector4(p.Offset.x, p.Offset.y, Mathf.Sin(roll), Mathf.Cos(roll)));
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_feature == null || _material == null || !_parameter.IsValid) return;
            var cmd = CommandBufferPool.Get("Recovered.TiltShift");
            bool downsample = _parameter.Downsample > 0;
            RenderTexture low = null;
            try
            {
                if ((int)renderPassEvent > (int)RenderPassEvent.BeforeRenderingPostProcessing)
                    _feature.ResetSourceRT(cmd, ref renderingData);

                int width = renderingData.cameraData.cameraTargetDescriptor.width;
                int height = renderingData.cameraData.cameraTargetDescriptor.height;
                if (width <= 0 || height <= 0) return;

                ApplyMaterial(_material, _parameter);
                int pass = Mathf.Clamp(ShaderPass(_parameter.Mode, _parameter.Quality), 0,
                    _material.passCount - 1);

                var inputHandle = _feature.SourceRT;
                cmd.GetTemporaryRT(Full, width, height, 0, FilterMode.Bilinear,
                    renderingData.cameraData.cameraTargetDescriptor.colorFormat);

                if (downsample)
                {
                    low = RenderTexture.GetTemporary(
                        Mathf.Max(1, width >> _parameter.Downsample),
                        Mathf.Max(1, height >> _parameter.Downsample), 0,
                        renderingData.cameraData.cameraTargetDescriptor.colorFormat,
                        RenderTextureReadWrite.Default);
                    low.filterMode = FilterMode.Bilinear;
                    cmd.Blit(inputHandle.RtId, low, _material, pass);
                    _material.SetTexture(ShaderManager.GetPropertyId(ShaderManager.PropertyId._Blurred), low);
                    cmd.Blit(inputHandle.RtId, Full, _material, kCompositePass);
                }
                else
                {
                    cmd.Blit(inputHandle.RtId, Full, _material, pass);
                }

                _feature.SetSourceRT(RenderTextureHandle.Make(Full, width, height), cmd, ref renderingData);

                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    "TiltShift", renderPassEvent, inputHandle, _feature.SourceRT);

                context.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                CommandBufferPool.Release(cmd);
                // The material must not keep pointing at a released buffer on the next frame.
                if (low != null)
                {
                    _material.SetTexture(ShaderManager.GetPropertyId(ShaderManager.PropertyId._Blurred), null);
                    RenderTexture.ReleaseTemporary(low);
                }
            }
        }

        public void Dispose() { CoreUtils.Destroy(_material); _material = null; }
    }
}
