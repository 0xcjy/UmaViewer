using System;
using Gallop.ImageEffect;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // Native ExposurePass.Execute 0x1a22560, ToneCurvePass.Execute 0x1a51df0.
    public sealed class ExposureToneCurvePass : ScriptableRenderPass, IDisposable
    {
        readonly bool _tone;
        readonly int _output;
        readonly Color32[] _pixels;
        Material _material;
        Texture2D _lut;
        PostImageEffectFeature _feature;
        ExposureParam _exposure;
        ToneCurveParam _curve;

        public ExposureToneCurvePass(bool tone)
        {
            _tone = tone;
            _output = Shader.PropertyToID(tone ? "_RecoveredToneCurveOutput" : "_RecoveredExposureOutput");
            _pixels = tone ? new Color32[512] : null;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public bool Setup(ExposureParam exposure, ToneCurveParam curve, PostImageEffectFeature feature)
        {
            _exposure = exposure;
            _curve = curve;
            _feature = feature;
            if (_tone ? curve == null || !curve.IsEnable || !curve.IsValidity : exposure == null || !exposure.IsEnable)
                return false;
            var shader = ShaderManager.GetShader(_tone ? ShaderManager.ShaderKinds.ToneCurve : ShaderManager.ShaderKinds.Exposure);
            if (shader == null || !shader.isSupported) return false;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            else if (_material.shader != shader) _material.shader = shader;
            ConfigureInput(ScriptableRenderPassInput.Depth);
            return _material.passCount > 0;
        }

        public static void RenderExposure(CommandBuffer cmd, Material material, ExposureParam parameter,
            RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            material.SetVector("_Parameter", new Vector4(1f + parameter.Gain, parameter.Lift,
                1f + parameter.MaskGain, parameter.MaskLift));
            material.SetFloat("_DepthMask", parameter.DepthMask);
            cmd.Blit(source, destination, material, 0);
        }

        public Texture2D UpdateToneCurveTexture(ToneCurveParam parameter)
        {
            // Native 0x1a52bc0: 256 samples per row, clamp then truncate to byte.
            if (_lut == null)
                _lut = new Texture2D(256, 2, TextureFormat.ARGB32, false, true)
                { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 256; i++)
            {
                byte value = (byte)(Mathf.Clamp01(parameter.ToneCurve.Evaluate(i / 255f)) * 255f);
                byte mask = (byte)(Mathf.Clamp01(parameter.MaskToneCurve.Evaluate(i / 255f)) * 255f);
                _pixels[i] = new Color32(value, value, value, 255);
                _pixels[i + 256] = new Color32(mask, mask, mask, 255);
            }
            _lut.SetPixels32(_pixels);
            _lut.Apply(false, false);
            return _lut;
        }

        public static void RenderToneCurve(CommandBuffer cmd, Material material, Texture lut, ToneCurveParam parameter,
            RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            cmd.SetGlobalTexture("_CurveTex", lut);
            cmd.SetGlobalVector("_MinLevel", parameter.MinCorrectionLevel);
            cmd.SetGlobalVector("_MaxLevel", parameter.MaxCorrectionLevel);
            cmd.SetGlobalVector("_MaskMinLevel", parameter.MaskMinCorrectionLevel);
            cmd.SetGlobalVector("_MaskMaxLevel", parameter.MaskMaxCorrectionLevel);
            cmd.SetGlobalFloat("_DepthMask", parameter.DepthMask);
            cmd.Blit(source, destination, material, 0);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_feature == null || _material == null) return;
            var cmd = CommandBufferPool.Get(_tone ? "Recovered.ToneCurve" : "Recovered.Exposure");
            try
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.useMipMap = descriptor.autoGenerateMips = false;
                cmd.GetTemporaryRT(_output, descriptor, FilterMode.Bilinear);
                var input = _feature.SourceRT;
                if (_tone) RenderToneCurve(cmd, _material, UpdateToneCurveTexture(_curve), _curve, input.RtId, _output);
                else RenderExposure(cmd, _material, _exposure, input.RtId, _output);
                _feature.SetSourceRT(RenderTextureHandle.Make(_output, descriptor.width, descriptor.height), cmd, ref renderingData);
                context.ExecuteCommandBuffer(cmd);
                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    _tone ? "ToneCurve" : "Exposure", renderPassEvent, input, _feature.SourceRT);
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
            CoreUtils.Destroy(_lut);
            _material = null;
            _lut = null;
            _feature = null;
            _exposure = null;
            _curve = null;
        }
    }
}
