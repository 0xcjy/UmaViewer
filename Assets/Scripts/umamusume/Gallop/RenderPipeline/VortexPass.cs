using System;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Recovered Vortex effect.
    ///
    /// Native references:
    ///  - Vortex.SetupMaterialParameter (0x7ffffc98a1f0): _DistortionArea = (Area.x, Area.y,
    ///    max(Area.z, MIN_AREA_SIZE), max(Area.w, MIN_AREA_SIZE)) with MIN_AREA_SIZE = 0.001,
    ///    _Angle = RotVolume, _DepthClip = DepthClip;
    ///  - Vortex.OnRenderImage (0x7ffffc98a610): full-resolution temporary (format 7) and
    ///    Graphics.Blit(source, temp, material, 0). The temporary is what the caller continues with;
    ///  - VortexParam.get_IsValidity (0x7ffff8b11560) is just IsEnable.
    ///
    /// Native hook: LiveImageEffect keeps Vortex in `_effectCommandBufferList` and draws it from
    /// `EntryAfterLensflareCommandBuffer` (the camera command buffer that runs after lens flare,
    /// i.e. after the whole image-effect chain). Here it is therefore registered as the LAST pass of
    /// the recovered chain.
    /// </summary>
    public sealed class VortexPass : ScriptableRenderPass, IDisposable
    {
        private const float kMinAreaSize = 0.001f;                 // VortexParam.MIN_AREA_SIZE
        // Native format 7 (ARGB4444 / B4G4R4A4) is not renderable on every device
        // (RenderTexture.Create fails with "color format unsupported"), so the temporary uses the
        // camera target format instead.

        [Serializable]
        public struct Parameter
        {
            public bool IsEnable;
            public Vector4 Area;
            public float RotVolume, DepthClip;

            public bool IsValid => IsEnable && Finite(Area) && Finite(RotVolume) && Finite(DepthClip);

            private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
            private static bool Finite(Vector4 v) =>
                Finite(v.x) && Finite(v.y) && Finite(v.z) && Finite(v.w);
        }

        private Material _material;
        private Parameter _parameter;
        private PostImageEffectFeature _feature;
        private static readonly int Temp = Shader.PropertyToID("_RecoveredVortex");

        public VortexPass(RenderPassEvent evt) { renderPassEvent = evt; }

        public bool Setup(Parameter parameter, PostImageEffectFeature feature)
        {
            _parameter = parameter;
            _feature = feature;
            if (!parameter.IsValid) return false;
            Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.Vortex);
            if (shader == null || !shader.isSupported) return false;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            else if (_material.shader != shader) _material.shader = shader;
            return _material.passCount > 0;
        }

        public static void ApplyMaterial(Material material, Parameter p)
        {
            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._DistortionArea),
                new Vector4(p.Area.x, p.Area.y,
                    Mathf.Max(p.Area.z, kMinAreaSize), Mathf.Max(p.Area.w, kMinAreaSize)));
            material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._Angle), p.RotVolume);
            material.SetFloat(ShaderManager.GetPropertyId(ShaderManager.PropertyId._DepthClip), p.DepthClip);
        }

        public static void Render(CommandBuffer cmd, Material material, Parameter p,
            RenderTargetIdentifier source, RenderTargetIdentifier destination)
        {
            ApplyMaterial(material, p);
            cmd.Blit(source, destination, material, 0);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_feature == null || _material == null || !_parameter.IsValid) return;
            var cmd = CommandBufferPool.Get("Recovered.Vortex");
            try
            {
                if ((int)renderPassEvent > (int)RenderPassEvent.BeforeRenderingPostProcessing)
                    _feature.ResetSourceRT(cmd, ref renderingData);

                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                cmd.GetTemporaryRT(Temp, descriptor.width, descriptor.height, 0,
                    FilterMode.Bilinear, descriptor.colorFormat);

                var inputHandle = _feature.SourceRT;
                Render(cmd, _material, _parameter, inputHandle.RtId, Temp);
                _feature.SetSourceRT(RenderTextureHandle.Make(Temp, descriptor.width, descriptor.height),
                    cmd, ref renderingData);

                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    "Vortex", renderPassEvent, inputHandle, _feature.SourceRT);

                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose() { CoreUtils.Destroy(_material); _material = null; }
    }
}
