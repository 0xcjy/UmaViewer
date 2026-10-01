using System;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    // Native SunShaftsPass.Execute 0x1a4fe30; Setup 0x1a50a00.
    public sealed class SunShaftsPass : ScriptableRenderPass, IDisposable
    {
        [Serializable]
        public struct Parameter
        {
            public bool IsEnable, IsEnabledBorderClear, IsEnabledBlurAlpha;
            public Camera TargetCamera;
            public Transform SunTransform;
            public Vector3 SunPosition;
            public Color SunColor;
            public int Resolution, ScreenBlendMode, BlurIterations;
            public float BlurRadius, SunPower, CenterBrightness, CenterMultiplex, KomorebiRate;
            public float ColorLerpRate, ScreenColorPower, EffectColorPower, Intensity, BlackLevel;
            public bool IsValid => IsEnable;
            public static Parameter Default() => new Parameter
            {
                Resolution = 1, ScreenBlendMode = 0, BlurRadius = 2.5f, SunColor = Color.white, SunPower = -1f,
                CenterBrightness = 1.5f, CenterMultiplex = 1f, ScreenColorPower = 1f, EffectColorPower = 1f,
                Intensity = 1.15f, BlackLevel = 0.1f, BlurIterations = 2
            };
        }

        private Material _material, _borderMaterial;
        private Mesh _border;
        private readonly Vector3[] _borderVertices = new Vector3[16];
        private int _borderWidth, _borderHeight;
        private Parameter _parameter;
        private PostImageEffectFeature _feature;
        private static readonly int Low0 = Shader.PropertyToID("_RecoveredSunShaftLow0");
        private static readonly int Low1 = Shader.PropertyToID("_RecoveredSunShaftLow1");
        private static readonly int Output = Shader.PropertyToID("_RecoveredSunShaftOutput");
        private static readonly int ColorBuffer = Shader.PropertyToID("_ColorBuffer");
        private static readonly int BlurRadius = Shader.PropertyToID("_BlurRadius4");

        public SunShaftsPass(RenderPassEvent evt) { renderPassEvent = evt; }
        public bool Setup(Parameter parameter, PostImageEffectFeature feature)
        {
            _parameter = parameter;
            _feature = feature;
            if (!parameter.IsValid) return false;
            Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.SunShaftsComposite);
            Shader clear = ShaderManager.GetShader(ShaderManager.ShaderKinds.SimpleClear);
            if (shader == null || !shader.isSupported || (parameter.IsEnabledBorderClear &&
                (clear == null || !clear.isSupported))) return false;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            else _material.shader = shader;
            if (clear != null)
            {
                if (_borderMaterial == null) _borderMaterial = CoreUtils.CreateEngineMaterial(clear);
                else _borderMaterial.shader = clear;
            }
            return _material.passCount >= 5;
        }

        private void UpdateBorder(int width, int height)
        {
            if (_border != null && width == _borderWidth && height == _borderHeight) return;
            _borderWidth = width; _borderHeight = height;
            float x = 1f / width, y = 1f / height;
            // Native CreateMesh 0x1a4f4e0 has four quads at z=0.1. Its DrawBorder
            // vertex keyword is stripped from the Windows shader bundle. Expand the
            // same one-pixel edge quads on CPU, then use native SimpleClear's black PS.
            SetQuad(0, 0f, 0f, x, 1f);
            SetQuad(4, 1f - x, 0f, 1f, 1f);
            SetQuad(8, 0f, 0f, 1f, y);
            SetQuad(12, 0f, 1f - y, 1f, 1f);
            if (_border == null)
            {
                _border = new Mesh { name = "RecoveredSunShaftBorder", hideFlags = HideFlags.HideAndDontSave };
                _border.vertices = _borderVertices;
                _border.triangles = new[] { 0,1,2,0,2,3,4,5,6,4,6,7,8,9,10,8,10,11,12,13,14,12,14,15 };
            }
            else _border.vertices = _borderVertices;
        }
        private void SetQuad(int start, float x0, float y0, float x1, float y1)
        {
            _borderVertices[start] = new Vector3(x0, y0, 0.1f);
            _borderVertices[start + 1] = new Vector3(x1, y0, 0.1f);
            _borderVertices[start + 2] = new Vector3(x1, y1, 0.1f);
            _borderVertices[start + 3] = new Vector3(x0, y1, 0.1f);
        }

        private void DrawBorder(CommandBuffer cmd, UnityEngine.Rendering.Universal.CameraData cameraData, int width, int height)
        {
            UpdateBorder(width, height);
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0f, 1f, 0f, 1f, -1f, 1f),
                cameraData.IsCameraProjectionMatrixFlipped());
            RenderingUtils.SetViewAndProjectionMatrices(cmd, Matrix4x4.identity, projection, false);
            for (int i = 0; i < _borderMaterial.passCount; i++)
                cmd.DrawMesh(_border, Matrix4x4.identity, _borderMaterial, 0, i);
            RenderingUtils.SetViewAndProjectionMatrices(cmd, cameraData.GetViewMatrix(),
                cameraData.GetGPUProjectionMatrix(), false);
        }

        public static Vector3 ResolveViewport(Parameter parameter, Camera camera)
        {
            if (parameter.SunTransform != null)
                return camera.WorldToViewportPoint(parameter.SunTransform.position);
            return camera.WorldToViewportPoint(parameter.SunPosition);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_feature == null || _material == null || !_parameter.IsValid) return;
            var cmd = CommandBufferPool.Get("Recovered.SunShafts");
            try
            {
                if ((int)renderPassEvent > (int)RenderPassEvent.BeforeRenderingPostProcessing)
                    _feature.ResetSourceRT(cmd, ref renderingData);
                var input = _feature.SourceRT;
                var full = renderingData.cameraData.cameraTargetDescriptor;
                full.depthBufferBits = 0; full.msaaSamples = 1; full.bindMS = false;
                full.useMipMap = full.autoGenerateMips = false;
                var low = full;
                int divisor = _parameter.Resolution == 2 ? 1 : _parameter.Resolution == 1 ? 2 : 4;
                low.width = Mathf.Max(1, full.width / divisor);
                low.height = Mathf.Max(1, full.height / divisor);
                Camera camera = _parameter.TargetCamera != null ? _parameter.TargetCamera : renderingData.cameraData.camera;
                Vector3 viewport = ResolveViewport(_parameter, camera);
                int iterations = Mathf.Clamp(_parameter.BlurIterations, 1, 4);
                if (_parameter.IsEnabledBorderClear && (Mathf.Abs(viewport.x - 0.5f) > 1f || Mathf.Abs(viewport.y - 0.5f) > 1f))
                {
                    if (iterations == 1) return;
                    iterations /= 2;
                }
                cmd.GetTemporaryRT(Low0, low, FilterMode.Bilinear);
                cmd.GetTemporaryRT(Low1, low, FilterMode.Bilinear);
                cmd.GetTemporaryRT(Output, full, FilterMode.Bilinear);
                cmd.SetGlobalVector("_SunPosition", new Vector4(viewport.x, viewport.y, viewport.z, 0f));
                cmd.SetGlobalFloat("_CenterBrightness", _parameter.CenterBrightness);
                cmd.SetGlobalFloat("_CenterMultiplex", _parameter.CenterMultiplex);
                cmd.SetGlobalFloat("_Power", _parameter.SunPower);
                cmd.SetGlobalFloat("_KomorebiRate", _parameter.KomorebiRate);
                cmd.SetGlobalFloat("_BlackLevel", _parameter.BlackLevel);
                cmd.SetGlobalFloat("_ColorLerpRate", _parameter.ColorLerpRate);
                cmd.SetGlobalFloat("_ScreenColorPower", _parameter.ScreenColorPower);
                cmd.SetGlobalFloat("_EffectColorPower", _parameter.EffectColorPower);
                cmd.SetGlobalVector(BlurRadius, new Vector4(_parameter.BlurRadius, _parameter.BlurRadius, 0f, 0f));
                cmd.Blit(input.RtId, Low0, _material, 0);
                if (_parameter.IsEnabledBorderClear && _borderMaterial != null)
                {
                    cmd.SetRenderTarget(Low0);
                    DrawBorder(cmd, renderingData.cameraData, low.width, low.height);
                }
                int blurPass = _parameter.IsEnabledBlurAlpha ? 4 : 1;
                float radius = _parameter.BlurRadius / 768f;
                cmd.SetGlobalVector(BlurRadius, new Vector4(radius, radius, 0f, 0f));
                for (int i = 0; i < iterations; i++)
                {
                    cmd.Blit(Low0, Low1, _material, blurPass);
                    radius = _parameter.BlurRadius * (i * 2f + 1f) * 6f / 768f;
                    cmd.SetGlobalVector(BlurRadius, new Vector4(radius, radius, 0f, 0f));
                    cmd.Blit(Low1, Low0, _material, blurPass);
                    radius = _parameter.BlurRadius * (i * 2f + 2f) * 6f / 768f;
                    cmd.SetGlobalVector(BlurRadius, new Vector4(radius, radius, 0f, 0f));
                }
                cmd.SetGlobalVector("_SunColor", (Vector4)(_parameter.SunColor * _parameter.Intensity));
                cmd.SetGlobalTexture(ColorBuffer, Low0);
                cmd.Blit(input.RtId, Output, _material, _parameter.ScreenBlendMode == 0 ? 2 : 3);
                cmd.SetGlobalTexture(ColorBuffer, Texture2D.blackTexture);
                cmd.ReleaseTemporaryRT(Low0); cmd.ReleaseTemporaryRT(Low1);
                _feature.SetSourceRT(RenderTextureHandle.Make(Output, full.width, full.height), cmd, ref renderingData);
                context.ExecuteCommandBuffer(cmd);
                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    "SunShafts", renderPassEvent, input, _feature.SourceRT);
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material); CoreUtils.Destroy(_borderMaterial); CoreUtils.Destroy(_border);
            _material = null; _borderMaterial = null; _border = null;
        }
    }
}
