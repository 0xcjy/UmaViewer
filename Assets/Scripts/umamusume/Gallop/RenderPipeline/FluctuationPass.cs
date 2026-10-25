using System;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Recovered Fluctuation effect (track 71).
    ///
    /// Native references:
    ///  - Fluctuation.CreateMaterial (0x7ffffc97ee70): loads the noise texture with
    ///    Resources.Load("ImageEffect/Fluctuation/noise") into _SecondTex (212);
    ///  - Fluctuation.OnRenderImage (0x7ffffc97f060): accumulates MovePosition with
    ///    `MovePosition += MovePower * MoveDirection` and Mathf.Repeat(value, 1) on both axes, then
    ///    sets _CameraDepthTexture (204), _UVScrollX (242) = MovePosition.x, _UVScrollY (243) = y,
    ///    _Power (146) and _DepthClip (248), blits into a full-resolution temporary (format 7) and
    ///    returns it;
    ///  - FluctuationParam.IsValidity: IsEnable &amp;&amp; (DisableGraphicSettings || GraphicSettings.IsFluctuation).
    ///
    /// The game's noise lives in its Resources (not in the shader bundle) but the same asset is
    /// catalogued as "imageeffect/fluctuation/noise" in the viewer database, so it is loaded from
    /// there — no substitute texture is generated.
    /// Native chain position: after RadialBlur and before LensDistortion/ChromaticAberration.
    /// </summary>
    public sealed class FluctuationPass : ScriptableRenderPass, IDisposable
    {
        private const string kNoiseAssetName = "imageeffect/fluctuation/noise";
        // Native format 7 (ARGB4444 / B4G4R4A4) is not renderable on every device
        // (RenderTexture.Create fails with "color format unsupported"), so the temporary uses the
        // camera target format instead.

        [Serializable]
        public struct Parameter
        {
            public bool IsEnable;
            public bool DisableGraphicSettings;
            public Vector2 MoveDirection;
            public float MovePower, Power, DepthClip;

            public bool IsValid => IsEnable &&
                (DisableGraphicSettings || GraphicSettings.IsFluctuation) &&
                Finite(MoveDirection.x) && Finite(MoveDirection.y) &&
                Finite(MovePower) && Finite(Power) && Finite(DepthClip);

            private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private static readonly int SecondTex = Shader.PropertyToID("_SecondTex");
        private static readonly int UvScrollX = Shader.PropertyToID("_UVScrollX");
        private static readonly int UvScrollY = Shader.PropertyToID("_UVScrollY");
        private static readonly int PowerId = Shader.PropertyToID("_Power");
        private static readonly int DepthClipId = Shader.PropertyToID("_DepthClip");
        private static readonly int Temp = Shader.PropertyToID("_RecoveredFluctuation");

        private Material _material;
        private Parameter _parameter;
        private PostImageEffectFeature _feature;
        private Vector2 _movePosition;
        private static Texture2D _noise;

        public FluctuationPass(RenderPassEvent evt) { renderPassEvent = evt; }

        private static Texture2D LoadNoise()
        {
            if (_noise != null) return _noise;
            var main = UmaViewerMain.Instance;
            if (main == null || main.AbList == null) return null;
            if (!main.AbList.TryGetValue(kNoiseAssetName, out var entry) || entry == null) return null;
            _noise = entry.Get<Texture2D>();
            return _noise;
        }

        public bool Setup(Parameter parameter, PostImageEffectFeature feature)
        {
            _parameter = parameter;
            _feature = feature;
            if (!parameter.IsValid) return false;
            Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.Fluctuation);
            if (shader == null || !shader.isSupported) return false;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            else if (_material.shader != shader) _material.shader = shader;

            Texture2D noise = LoadNoise();
            if (noise == null)
            {
                // Without the authored noise the shader would sample nothing; skip instead of guessing.
                return false;
            }
            _material.SetTexture(SecondTex, noise);
            return _material.passCount > 0;
        }

        public static void ApplyMaterial(Material material, Parameter p, Vector2 movePosition)
        {
            material.SetFloat(UvScrollX, movePosition.x);
            material.SetFloat(UvScrollY, movePosition.y);
            material.SetFloat(PowerId, p.Power);
            material.SetFloat(DepthClipId, p.DepthClip);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_feature == null || _material == null || !_parameter.IsValid) return;
            var cmd = CommandBufferPool.Get("Recovered.Fluctuation");
            try
            {
                if ((int)renderPassEvent > (int)RenderPassEvent.BeforeRenderingPostProcessing)
                    _feature.ResetSourceRT(cmd, ref renderingData);

                // Native accumulation happens per OnRenderImage call, before the material write.
                _movePosition.x = Mathf.Repeat(_movePosition.x + _parameter.MovePower * _parameter.MoveDirection.x, 1f);
                _movePosition.y = Mathf.Repeat(_movePosition.y + _parameter.MovePower * _parameter.MoveDirection.y, 1f);

                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                cmd.GetTemporaryRT(Temp, descriptor.width, descriptor.height, 0, FilterMode.Bilinear,
                    descriptor.colorFormat);

                ApplyMaterial(_material, _parameter, _movePosition);

                var inputHandle = _feature.SourceRT;
                cmd.Blit(inputHandle.RtId, Temp, _material, 0);
                _feature.SetSourceRT(RenderTextureHandle.Make(Temp, descriptor.width, descriptor.height),
                    cmd, ref renderingData);

                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    "Fluctuation", renderPassEvent, inputHandle, _feature.SourceRT);

                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose() { CoreUtils.Destroy(_material); _material = null; }
    }
}
