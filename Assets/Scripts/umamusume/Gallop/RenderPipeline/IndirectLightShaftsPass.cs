using System;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // IndirectLightShaftsPass.Execute 0x1a29f80; Setup 0x1a2a880.
    public sealed class IndirectLightShaftsPass : ScriptableRenderPass, IDisposable
    {
        [Serializable]
        public struct Parameter
        {
            public bool IsEnable;
            public int ShaftType;
            public Texture[] Textures;
            public Texture MaskTexture;
            public Vector4 Speed, Angle, Offset, Alpha, Alpha2, MaskAlpha;
            public float Scale;
            public bool IsValid => IsEnable && Textures != null && Textures.Length >= (ShaftType == 1 ? 6 : 2);
        }
        private Parameter _parameter;
        private Material _material;
        private PostImageEffectFeature _feature;
        private static readonly int Output = Shader.PropertyToID("_RecoveredIndirectLightShafts");
        private static readonly int MaskTexture = Shader.PropertyToID("_MaskTex");
        private static readonly int[] ShaftTextures = { Shader.PropertyToID("_ShaftsTex1"),
            Shader.PropertyToID("_ShaftsTex2"), Shader.PropertyToID("_ShaftsTex3"), Shader.PropertyToID("_ShaftsTex4"),
            Shader.PropertyToID("_ShaftsTex5") };
        public IndirectLightShaftsPass(RenderPassEvent evt) { renderPassEvent = evt; }
        public bool Setup(Parameter parameter, PostImageEffectFeature feature)
        {
            _parameter = parameter; _feature = feature;
            if (!parameter.IsValid) return false;
            Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.IndirectLightShafts);
            if (shader == null || !shader.isSupported) return false;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            else _material.shader = shader;
            return _material.passCount >= 2;
        }
        public static void Render(CommandBuffer cmd, Material material, Parameter parameter,
            RenderTargetIdentifier source, RenderTargetIdentifier destination, float screenAspect)
        {
            cmd.SetGlobalVector("_Offset", parameter.Offset);
            cmd.SetGlobalVector("_Speed", parameter.Speed);
            cmd.SetGlobalVector("_Angle", parameter.Angle);
            cmd.SetGlobalVector("_Scale", new Vector4(parameter.Scale / screenAspect,
                parameter.Scale, parameter.Scale, parameter.Scale));
            cmd.SetGlobalVector("_Alpha", parameter.Alpha);
            cmd.SetGlobalVector("_Alpha2", parameter.Alpha2);
            cmd.SetGlobalVector("_MaskAlpha", parameter.MaskAlpha);
            int count = parameter.ShaftType == 1 ? 5 : 2;
            for (int i = 0; i < count; i++) cmd.SetGlobalTexture(ShaftTextures[i], parameter.Textures[i]);
            Texture mask = parameter.ShaftType == 1 ? parameter.Textures[5] : parameter.MaskTexture;
            cmd.SetGlobalTexture(MaskTexture, mask != null ? mask : Texture2D.blackTexture);
            cmd.Blit(source, destination, material, parameter.ShaftType == 1 ? 1 : 0);
            cmd.SetGlobalTexture(MaskTexture, Texture2D.blackTexture);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_feature == null || _material == null || !_parameter.IsValid) return;
            var cmd = CommandBufferPool.Get("Recovered.IndirectLightShafts");
            try
            {
                if ((int)renderPassEvent > (int)RenderPassEvent.BeforeRenderingPostProcessing)
                    _feature.ResetSourceRT(cmd, ref renderingData);
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1; descriptor.bindMS = false;
                descriptor.useMipMap = descriptor.autoGenerateMips = false;
                cmd.GetTemporaryRT(Output, descriptor, FilterMode.Bilinear);
                var input = _feature.SourceRT;
                Render(cmd, _material, _parameter, input.RtId, Output, (float)Screen.width / Screen.height);
                _feature.SetSourceRT(RenderTextureHandle.Make(Output, descriptor.width, descriptor.height), cmd, ref renderingData);
                context.ExecuteCommandBuffer(cmd);
                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    "IndirectLightShafts", renderPassEvent, input, _feature.SourceRT);
            }
            finally { CommandBufferPool.Release(cmd); }
        }
        public void Dispose() { CoreUtils.Destroy(_material); _material = null; }
    }
}
