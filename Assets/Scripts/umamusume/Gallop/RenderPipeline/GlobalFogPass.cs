using System;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Recovered GlobalFog effect (Unity Standard Assets GlobalFog, which is what the game ships).
    ///
    /// Native reference: Gallop.ImageEffect.GlobalFog.OnRenderImage (0x7ffffc97fa80).
    /// Constants verified against the binary: 1.201122 (x) and 1.442695 (y) scale the scene fog
    /// density, the frustum corners are normalised and scaled by |topLeft| * far / near, and the
    /// draw is a full-screen quad whose vertex Z is the frustum-corner row index (the classic
    /// PostEffectsBase.CustomGraphicsBlit).
    ///
    /// Shader properties (native ids): _FrustumCornersWS 227, _CameraWS 228, _HeightParams 229,
    /// _DistanceParams 230, _FogColor 231, _SceneFogParams 232, _SceneFogMode 233.
    /// Pass: 0 = height + distance, 1 = height only, 2 = distance only.
    /// Native chain position: first image effect (before SunShafts).
    /// </summary>
    public sealed class GlobalFogPass : ScriptableRenderPass, IDisposable
    {
        [Serializable]
        public struct Parameter
        {
            public bool DistanceFog, UseRadialDistance, HeightFog;
            public float FogHeight, FogHeightDensity, StartDistance;
            public Color FogColor;
            public int SceneFogMode;                 // 1 Linear, 2 Exponential, 3 ExponentialSquared
            public float SceneFogDensity, SceneFogStart, SceneFogEnd;

            public bool IsEnable => DistanceFog || HeightFog;

            public bool IsValid => (DistanceFog || HeightFog) &&
                Finite(FogHeight) && Finite(FogHeightDensity) && Finite(StartDistance) &&
                Finite(SceneFogDensity) && Finite(SceneFogStart) && Finite(SceneFogEnd);

            private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        }

        private const float kLog2e = 1.442695f;     // native 0x3FB8AA3B
        private const float kFog2 = 1.201122f;      // native 0x3F99BE61
        // Native format 7 (ARGB4444 / B4G4R4A4) is not renderable on every device
        // (RenderTexture.Create fails with "color format unsupported"), so the temporary uses the
        // camera target format instead.

        private Material _material;
        private Parameter _parameter;
        private PostImageEffectFeature _feature;
        private Mesh _quad;
        private static readonly int Temp = Shader.PropertyToID("_RecoveredGlobalFog");
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private RenderTexture _sourceCopy;

        public GlobalFogPass(RenderPassEvent evt) { renderPassEvent = evt; }

        public bool Setup(Parameter parameter, PostImageEffectFeature feature)
        {
            _parameter = parameter;
            _feature = feature;
            if (!parameter.IsValid) return false;
            Shader shader = ShaderManager.GetShader(ShaderManager.ShaderKinds.GlobalFog);
            if (shader == null || !shader.isSupported) return false;
            if (_material == null) _material = CoreUtils.CreateEngineMaterial(shader);
            else if (_material.shader != shader) _material.shader = shader;
            if (_material.passCount < 3) return false;
            if (_quad == null) _quad = BuildQuad();
            return true;
        }

        // PostEffectsBase.CustomGraphicsBlit draws this quad under GL.LoadOrtho(): x,y cover the
        // screen and vertex Z is the _FrustumCornersWS row index (the shader reads it and zeroes it
        // before projecting). In a CommandBuffer there is no LoadOrtho, so the quad is emitted
        // directly in clip space (-1..1) with an identity view/projection.
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "RecoveredGlobalFogQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(-1f, -1f, 3f),
                new Vector3(1f, -1f, 2f),
                new Vector3(1f, 1f, 1f),
                new Vector3(-1f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.UploadMeshData(true);
            return mesh;
        }

        public static int ShaderPass(Parameter p)
        {
            // Native: 2 - (distance ? 1 : 0) when either type is off, otherwise 0.
            if (p.DistanceFog && p.HeightFog) return 0;
            return p.HeightFog ? 1 : 2;
        }

        public static void ApplyMaterial(Material material, Parameter p, Camera camera)
        {
            Transform tr = camera.transform;
            float near = camera.nearClipPlane, far = camera.farClipPlane;
            float halfFov = camera.fieldOfView * 0.5f;
            float tan = Mathf.Tan(halfFov * Mathf.Deg2Rad);

            Vector3 toRight = tr.right * (near * tan * camera.aspect);
            Vector3 toTop = tr.up * (near * tan);
            Vector3 forward = tr.forward * near;

            Vector3 topLeft = forward - toRight + toTop;
            float camScale = topLeft.magnitude * far / near;
            topLeft.Normalize(); topLeft *= camScale;

            Vector3 topRight = forward + toRight + toTop;
            topRight.Normalize(); topRight *= camScale;

            Vector3 bottomRight = forward + toRight - toTop;
            bottomRight.Normalize(); bottomRight *= camScale;

            Vector3 bottomLeft = forward - toRight - toTop;
            bottomLeft.Normalize(); bottomLeft *= camScale;

            var corners = Matrix4x4.identity;
            corners.SetRow(0, bottomLeft);
            corners.SetRow(1, bottomRight);
            corners.SetRow(2, topRight);
            corners.SetRow(3, topLeft);

            material.SetMatrix(ShaderManager.GetPropertyId(ShaderManager.PropertyId._FrustumCornersWS), corners);
            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._CameraWS), tr.position);

            Vector3 camPos = tr.position;
            float above = camPos.y - p.FogHeight;
            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._HeightParams),
                new Vector4(p.FogHeight, above, above > 0f ? 0f : 1f, p.FogHeightDensity * 0.5f));

            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._DistanceParams),
                new Vector4(p.StartDistance > 0f ? -p.StartDistance : 0f, 0f, 0f, 0f));

            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._FogColor), p.FogColor);

            var sceneFogParams = Vector4.zero;
            var sceneFogMode = Vector4.zero;
            if (p.SceneFogMode == 1) // Linear
            {
                float range = p.SceneFogEnd - p.SceneFogStart;
                if (Mathf.Abs(range) > 0.0001f)
                    sceneFogParams = new Vector4(0f, 0f, 1f / range, p.SceneFogEnd / range);
            }
            sceneFogParams.x = p.SceneFogDensity * kFog2;
            sceneFogParams.y = p.SceneFogDensity * kLog2e;
            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._SceneFogParams), sceneFogParams);

            sceneFogMode.x = p.SceneFogMode;
            sceneFogMode.y = p.UseRadialDistance ? 1f : 0f;
            material.SetVector(ShaderManager.GetPropertyId(ShaderManager.PropertyId._SceneFogMode), sceneFogMode);

        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_feature == null || _material == null || !_parameter.IsEnable) return;
            var cmd = CommandBufferPool.Get("Recovered.GlobalFog");
            try
            {
                if ((int)renderPassEvent > (int)RenderPassEvent.BeforeRenderingPostProcessing)
                    _feature.ResetSourceRT(cmd, ref renderingData);

                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.useMipMap = descriptor.autoGenerateMips = false;
                cmd.GetTemporaryRT(Temp, descriptor.width, descriptor.height, 0, FilterMode.Bilinear,
                    descriptor.colorFormat);

                ApplyMaterial(_material, _parameter, renderingData.cameraData.camera);

                var inputHandle = _feature.SourceRT;
                // The quad needs the source as a real texture (DrawMesh cannot bind a render-target
                // identifier), so copy it into a cached buffer first.
                int w = descriptor.width, h = descriptor.height;
                if (_sourceCopy == null || _sourceCopy.width != w || _sourceCopy.height != h)
                {
                    if (_sourceCopy != null) { _sourceCopy.Release(); CoreUtils.Destroy(_sourceCopy); }
                    _sourceCopy = new RenderTexture(w, h, 0, renderingData.cameraData.cameraTargetDescriptor.colorFormat,
                        RenderTextureReadWrite.Default) { hideFlags = HideFlags.HideAndDontSave };
                    _sourceCopy.Create();
                }
                cmd.Blit(inputHandle.RtId, _sourceCopy);
                _material.SetTexture(MainTex, _sourceCopy);

                cmd.SetRenderTarget(Temp);
                // Clip-space quad: identity view/projection, restored right after the draw so the
                // rest of the chain keeps the camera matrices.
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmd.DrawMesh(_quad, Matrix4x4.identity, _material, 0, ShaderPass(_parameter));
                var camera = renderingData.cameraData.camera;
                cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                _feature.SetSourceRT(RenderTextureHandle.Make(Temp, descriptor.width, descriptor.height),
                    cmd, ref renderingData);

                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    "GlobalFog", renderPassEvent, inputHandle, _feature.SourceRT);

                context.ExecuteCommandBuffer(cmd);
            }
            finally { CommandBufferPool.Release(cmd); }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
            _material = null;
            if (_quad != null) CoreUtils.Destroy(_quad);
            _quad = null;
            if (_sourceCopy != null) { _sourceCopy.Release(); CoreUtils.Destroy(_sourceCopy); _sourceCopy = null; }
        }
    }
}
