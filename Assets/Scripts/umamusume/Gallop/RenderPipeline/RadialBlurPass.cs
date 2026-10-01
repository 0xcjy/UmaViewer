using System;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // Native OnRadialBlur RVA 0x1a3d340: low-resolution iterations followed
    // by a full-resolution, depth/area-masked composite using the original source.
    public sealed class RadialBlurPass : ScriptableRenderPass, IDisposable
    {
        [Serializable]
        public struct Parameter
        {
            public int Type, Downsample, Iteration;
            public Vector2 Offset, EllipseDir;
            public float StartArea, EndArea, Power, RollEulerAngles;
            public bool IsEnabledDepth, IsEnabledDepthCancelRect, IsExpandDepthCancelRect;
            public float DepthPowerFront, DepthPowerBack, DepthCancelBlendLength;
            public Vector4 DepthCancelRect;
            public bool IsValid => Type >= 1 && Type <= 5 && Finite(Power) && Finite(StartArea) &&
                Finite(EndArea) && Finite(Offset.x) && Finite(Offset.y) &&
                Finite(EllipseDir.x) && Finite(EllipseDir.y) && Finite(RollEulerAngles) &&
                (!IsEnabledDepth || (Finite(DepthPowerFront) && Finite(DepthPowerBack) &&
                Finite(DepthCancelBlendLength) && Finite(DepthCancelRect.x) && Finite(DepthCancelRect.y) &&
                Finite(DepthCancelRect.z) && Finite(DepthCancelRect.w)));
            private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
            public static Parameter Default() => new Parameter { Downsample = 1, Iteration = 1,
                EndArea = 1f, Power = 1f, EllipseDir = Vector2.one };
        }

        private Material _material;
        private Parameter _parameter;
        private PostImageEffectFeature _feature;
        private static readonly int Low0 = Shader.PropertyToID("_RecoveredRadialLow0");
        private static readonly int Low1 = Shader.PropertyToID("_RecoveredRadialLow1");
        private static readonly int Output = Shader.PropertyToID("_RecoveredRadialOutput");
        private static readonly int BlurTex = Shader.PropertyToID("_BlurTex");
        public RadialBlurPass(RenderPassEvent evt) { renderPassEvent = evt; }
        public bool Setup(Parameter parameter, PostImageEffectFeature feature)
        {
            _parameter = parameter;
            _feature = feature;
            if (!parameter.IsValid) return false;
            Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.RadialBlur);
            if (shader == null || !shader.isSupported) return false;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            else if (_material.shader != shader) _material.shader = shader;
            return _material.passCount >= 10;
        }
        public static int BlurPassIndex(int type) => Mathf.Clamp(type - 1, 0, 4) * 2;
        public static float AreaDifference(float start, float end)
        {
            float d = end - start;
            return Mathf.Abs(d) >= 0.0001f ? d : d < 0 ? -0.0001f : 0.0001f;
        }
        public static Vector4 CancelBounds(Parameter p)
        {
            Vector4 r = p.DepthCancelRect;
            r.z += r.x; r.w += r.y;
            if (p.IsExpandDepthCancelRect)
                r += new Vector4(-1f, -1f, 1f, 1f) * p.DepthCancelBlendLength;
            return r;
        }
        public static void ApplyMaterial(Material material, Parameter p, Camera camera)
        {
            material.SetVector("_BlurParam", new Vector4(p.Offset.x, p.Offset.y, p.EllipseDir.x, p.EllipseDir.y));
            float angle = -p.RollEulerAngles * Mathf.Deg2Rad;
            material.SetVector("_BlurParamEx", p.Type == 5
                ? new Vector4(-Mathf.Sin(angle), Mathf.Cos(angle), 0f, 0f) : Vector4.zero);
            material.SetFloat("_BlurStartArea", p.StartArea);
            material.SetFloat("_BlurEndArea", AreaDifference(p.StartArea, p.EndArea));
            material.SetFloat("_BlurPower", p.Power);
            bool depth = p.IsEnabledDepth && camera != null;
            CoreUtils.SetKeyword(material, "ENABLE_DEPTH", depth);
            CoreUtils.SetKeyword(material, "ENABLE_DEPTH_CANCEL_RECT", depth && p.IsEnabledDepthCancelRect);
            float near = camera != null ? camera.nearClipPlane : 0f;
            float range = camera != null ? Mathf.Max(1f, camera.farClipPlane - near) : 1f;
            material.SetFloat("_DepthPowerFront", depth ? Mathf.Clamp01((p.DepthPowerFront - near) / range) : 0f);
            material.SetFloat("_DepthPowerBack", depth ? Mathf.Clamp01((p.DepthPowerBack - near) / range) : 0f);
            material.SetVector("_DepthCancelRect", depth ? CancelBounds(p) : Vector4.zero);
            material.SetFloat("_DepthCancelBlendLength", depth ? p.DepthCancelBlendLength : 0f);
        }
        // Also used by the isolated GPU regression, so tests execute production blits.
        public static void Render(CommandBuffer cmd, Material material, Parameter p, Camera camera,
            RenderTargetIdentifier source, RenderTargetIdentifier destination, RenderTextureDescriptor descriptor)
        {
            ApplyMaterial(material, p, camera);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = descriptor.autoGenerateMips = false;
            // Native divisor, not a power-of-two shift. Guard malformed serialized keys.
            int divisor = Mathf.Max(1, p.Downsample);
            descriptor.width = Mathf.Max(1, descriptor.width / divisor);
            descriptor.height = Mathf.Max(1, descriptor.height / divisor);
            cmd.GetTemporaryRT(Low0, descriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(Low1, descriptor, FilterMode.Bilinear);
            int pass = BlurPassIndex(p.Type), read = Low0, write = Low1;
            cmd.Blit(source, read, material, pass);
            for (int i = 1; i < Mathf.Clamp(p.Iteration, 1, 32); i++)
            {
                cmd.Blit(read, write, material, pass);
                int swap = read; read = write; write = swap;
            }
            cmd.SetGlobalTexture(BlurTex, read);
            cmd.Blit(source, destination, material, pass | 1);
            cmd.SetGlobalTexture(BlurTex, Texture2D.blackTexture);
            cmd.ReleaseTemporaryRT(Low0);
            cmd.ReleaseTemporaryRT(Low1);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_feature == null || _material == null || !_parameter.IsValid) return;
            var cmd = CommandBufferPool.Get("Recovered.RadialBlur");
            try
            {
                // After 550 the preceding DOF pass writes camera color directly.
                if ((int)renderPassEvent > (int)RenderPassEvent.BeforeRenderingPostProcessing)
                    _feature.ResetSourceRT(cmd, ref renderingData);
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1; descriptor.bindMS = false;
                descriptor.useMipMap = descriptor.autoGenerateMips = false;
                cmd.GetTemporaryRT(Output, descriptor, FilterMode.Bilinear);
                var inputHandle = _feature.SourceRT;
                var inputRT = inputHandle.RtId;
                Render(cmd, _material, _parameter, renderingData.cameraData.camera, inputRT, Output, descriptor);
                _feature.SetSourceRT(RenderTextureHandle.Make(Output, descriptor.width, descriptor.height), cmd, ref renderingData);
                context.ExecuteCommandBuffer(cmd);
                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    "RadialBlur", renderPassEvent,
                    inputHandle, _feature.SourceRT);
            }
            finally { CommandBufferPool.Release(cmd); }
        }
        public void Dispose() { CoreUtils.Destroy(_material); _material = null; }
    }
}
