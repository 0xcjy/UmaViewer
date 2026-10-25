using System;
using Gallop.Live;
using Gallop.Live.Cutt;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Recovered ChromaticAberration effect.
    ///
    /// Native references:
    ///  - Gallop.ImageEffect.ChromaticAberration.OnRenderImage (0x7ffffc96eee0): allocates one
    ///    full-resolution temporary, writes three material vectors (_OffsetColorX, _OffsetColorY
    ///    and _Parameter) and blits source -> temporary with `effectType` as the shader pass.
    ///    The returned temporary is what the caller chain continues with; the source is not
    ///    modified in place.
    ///  - Gallop.Live.Director.OnUpdateChromaticAberration (0x7ffffc7990c0): copies the authored
    ///    per-channel offsets scaled by 0.01 into _OffsetColorX/_OffsetColorY (x=red, y=green,
    ///    z=blue, w left untouched) and packs _Parameter as (0, 0, power, clip).
    /// </summary>
    public sealed class ChromaticAberrationPass : ScriptableRenderPass, IDisposable
    {
        [Serializable]
        public struct Parameter
        {
            public bool IsEnable;
            public Vector4 OffsetColorX;
            public Vector4 OffsetColorY;
            public Vector4 ParameterVector;
            public int Type;

            public bool IsValid => IsEnable && Type >= 0 &&
                Finite(OffsetColorX) && Finite(OffsetColorY) && Finite(ParameterVector);

            private static bool Finite(Vector4 v) =>
                !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) && !float.IsNaN(v.w) &&
                !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z) && !float.IsInfinity(v.w);
        }

        // Director.OnUpdateChromaticAberration multiplies the authored offsets by 0.01.
        private const float kAuthoredOffsetScale = 0.01f;

        private Material _material;
        private Parameter _parameter;
        private PostImageEffectFeature _feature;
        private static readonly int Output = Shader.PropertyToID("_RecoveredChromaticAberration");

        public ChromaticAberrationPass(RenderPassEvent evt) { renderPassEvent = evt; }

        public static Parameter FromUpdateInfo(ChromaticAberrationUpdateInfo info)
        {
            return new Parameter
            {
                IsEnable = info.isEnable,
                Type = info.effectType,
                OffsetColorX = new Vector4(info.redOffset.x, info.greenOffset.x, info.blueOffset.x, 0f) *
                               kAuthoredOffsetScale,
                OffsetColorY = new Vector4(info.redOffset.y, info.greenOffset.y, info.blueOffset.y, 0f) *
                               kAuthoredOffsetScale,
                ParameterVector = new Vector4(0f, 0f, info.power, info.clip)
            };
        }

        public bool Setup(Parameter parameter, PostImageEffectFeature feature)
        {
            _parameter = parameter;
            _feature = feature;
            if (!parameter.IsValid) return false;
            Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.ChromaticAberration);
            if (shader == null || !shader.isSupported) return false;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            else if (_material.shader != shader) _material.shader = shader;
            return _material.passCount > 0;
        }

        public static void ApplyMaterial(Material material, Parameter p)
        {
            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._OffsetColorX), p.OffsetColorX);
            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._OffsetColorY), p.OffsetColorY);
            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._Parameter), p.ParameterVector);
        }

        // Also used by isolated proof passes, so tests execute the production blit.
        // The native effect returns the temporary it wrote, so the chain continues from it.
        public static void Render(CommandBuffer cmd, Material material, Parameter p, Camera camera,
            RenderTargetIdentifier source, RenderTargetIdentifier destination,
            RenderTextureDescriptor descriptor)
        {
            ApplyMaterial(material, p);
            // The authored type is the shader pass; clamp so a malformed key cannot throw.
            int pass = Mathf.Clamp(p.Type, 0, Mathf.Max(0, material.passCount - 1));
            cmd.Blit(source, destination, material, pass);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_feature == null || _material == null || !_parameter.IsValid) return;
            var cmd = CommandBufferPool.Get("Recovered.ChromaticAberration");
            try
            {
                if ((int)renderPassEvent > (int)RenderPassEvent.BeforeRenderingPostProcessing)
                    _feature.ResetSourceRT(cmd, ref renderingData);

                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.useMipMap = descriptor.autoGenerateMips = false;

                cmd.GetTemporaryRT(Output, descriptor, FilterMode.Bilinear);

                var inputHandle = _feature.SourceRT;
                Render(cmd, _material, _parameter, renderingData.cameraData.camera,
                    inputHandle.RtId, Output, descriptor);
                _feature.SetSourceRT(RenderTextureHandle.Make(Output, descriptor.width, descriptor.height),
                    cmd, ref renderingData);

                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    "ChromaticAberration", renderPassEvent, inputHandle, _feature.SourceRT);

                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose() { CoreUtils.Destroy(_material); _material = null; }
    }
}
