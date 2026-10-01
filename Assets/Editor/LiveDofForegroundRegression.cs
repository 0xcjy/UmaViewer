using System;
using System.Reflection;
using Gallop;
using Gallop.ImageEffect;
using Gallop.RenderPipeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// GPU regression for the recovered CoC bridge. No song/game assets required.
public static class LiveDofForegroundRegression
{
    [MenuItem("UmaViewer/Rendering/Validate DOF foreground (GPU)")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            throw new InvalidOperationException("GPU test requires graphics; do not pass -nographics.");
        var root = new GameObject("DofForegroundRegression");
        var camera = root.AddComponent<Camera>();
        camera.enabled = false;
        camera.nearClipPlane = 0.8f;
        camera.farClipPlane = 1000f;
        var pass = new DofDiffusionBloomOverlayPass(RenderPassEvent.BeforeRenderingPostProcessing);
        var shader = Resources.Load<Shader>("RenderPipeline/LiveDofCoC");
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("LiveDofCoC shader unavailable or failed compilation.");
        float[] depths = { 5, 15, 25, 29.23675f, 31.04587f, 34.40935f, 40.81587f, 55.9f, 65, 90 };
        var depth = new Texture2D(depths.Length, 1, TextureFormat.RFloat, false, true) { filterMode = FilterMode.Point };
        var source = new Texture2D(depths.Length, 1, TextureFormat.RGBAFloat, false, true) { filterMode = FilterMode.Point };
        var result = new RenderTexture(depths.Length, 1, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(depths.Length, 1, TextureFormat.RGBAFloat, false, true);
        var previousRT = RenderTexture.active;
        var oldDepth = Shader.GetGlobalTexture("_CameraDepthTexture");
        string[] vectors = { "_ZBufferParams", "_CurveParams", "_GlobalScreenUVScrollParam", "_InvRenderTargetSize" };
        var oldVectors = Array.ConvertAll(vectors, name => Shader.GetGlobalVector(name));
        float oldStart = Shader.GetGlobalFloat("_GallopDofFocalStart");
        float oldSize = Shader.GetGlobalFloat("_dofForegroundSize");
        float oldWeight = Shader.GetGlobalFloat("_bloomDofWeight");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            result.Create();
            // Synthetic raw depth uses the same ZBufferParams as the shader, both depth conventions.
            foreach (bool reversed in new[] { false, true })
            foreach (float size in new[] { 30f, 0f })
            foreach (float foreground in new[] { 1f, 0f })
            {
                float ratio = camera.farClipPlane / camera.nearClipPlane;
                float x = reversed ? ratio - 1 : 1 - ratio;
                float y = reversed ? 1 : ratio;
                for (int i = 0; i < depths.Length; i++)
                {
                    depth.SetPixel(i, 0, new Color((camera.farClipPlane / depths[i] - y) / x, 0, 0, 0));
                    source.SetPixel(i, 0, new Color(0.2f, 0.4f, 0.6f, 0.7f));
                }
                depth.Apply(); source.Apply();
                var parameter = new DofDiffusionBloomOverlayPass.Parameter
                {
                    TargetCamera = camera, DofFocalType = DepthBlurAndBloom.DofFocalType.Position,
                    DofFocalPosition = new Vector3(0, 0, 40.81587f), DofFocalSize = size,
                    DofSmoothness = 0.5f, DofForegroundSize = foreground,
                    DofQualityType = (DepthBlurAndBloom.DofQuality)5
                };
                var src = RenderTextureHandle.Make(new RenderTargetIdentifier(source));
                src.Width = depths.Length; src.Height = 1;
                var dst = RenderTextureHandle.Make(new RenderTargetIdentifier(result));
                using (var cmd = new CommandBuffer())
                {
                    typeof(DofDiffusionBloomOverlayPass).GetMethod("PrepareDofParam", flags).Invoke(pass,
                        new object[] { default(ScriptableRenderContext), cmd, parameter, src });
                    cmd.SetGlobalVector("_ZBufferParams", new Vector4(x, y, x / 1000, y / 1000));
                    cmd.SetGlobalVector("_GlobalScreenUVScrollParam", Vector4.zero);
                    cmd.SetGlobalTexture("_CameraDepthTexture", depth);
                    cmd.SetGlobalFloat("_dofForegroundSize", foreground);
                    typeof(DofDiffusionBloomOverlayPass).GetMethod("BlitDofCoc", flags).Invoke(pass,
                        new object[] { cmd, parameter, src, dst, null, 0 });
                    Graphics.ExecuteCommandBuffer(cmd);
                }
                RenderTexture.active = result;
                readback.ReadPixels(new Rect(0, 0, depths.Length, 1), 0, 0); readback.Apply();
                float range = camera.farClipPlane - camera.nearClipPlane;
                float focus = 40.81587f / range, half = size * 0.5f / range;
                for (int i = 0; i < depths.Length; i++)
                {
                    float z = depths[i] / 1000;
                    float bg = Mathf.Clamp01((z - focus - half) / (focus * 0.5f));
                    float fg = Mathf.Clamp01((Mathf.Max(0, focus - half) - z) / (focus * 0.5f)) * foreground;
                    var actual = readback.GetPixel(i, 0);
                    if (Mathf.Abs(actual.a - Mathf.Max(bg, fg)) > 0.002f ||
                        Mathf.Abs(actual.r - 0.2f) > 0.002f || Mathf.Abs(actual.g - 0.4f) > 0.002f || Mathf.Abs(actual.b - 0.6f) > 0.002f)
                        throw new InvalidOperationException($"CoC mismatch depth={depths[i]} size={size} reversed={reversed}: {actual}");
                    if (size == 30 && foreground == 1 && !reversed)
                        Debug.Log($"[LiveDofForegroundRegression] depth={depths[i]:F2}m alpha={actual.a:F4}");
                }
            }
            Debug.Log("[LiveDofForegroundRegression] PASS: 80 GPU samples, frame3000 focus, near/far CoC, zero-width band, foreground off, RGB preserved, normal/reversed Z.");
        }
        finally
        {
            RenderTexture.active = previousRT;
            Shader.SetGlobalTexture("_CameraDepthTexture", oldDepth);
            for (int i = 0; i < vectors.Length; i++) Shader.SetGlobalVector(vectors[i], oldVectors[i]);
            Shader.SetGlobalFloat("_GallopDofFocalStart", oldStart);
            Shader.SetGlobalFloat("_dofForegroundSize", oldSize);
            Shader.SetGlobalFloat("_bloomDofWeight", oldWeight);
            pass.Dispose(); result.Release();
            foreach (UnityEngine.Object obj in new UnityEngine.Object[] { root, depth, source, result, readback })
                UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
