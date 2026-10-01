using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Gallop.RenderPipeline;

// Isolated GPU contract tests for the shipped shaders, not a whole-Live parity test.
// Expected equations come from local DXBC (documented in LIVE_RENDER_RESTORATION.md).
public static class LiveBloomDiffusionRegression
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("LiveBloomDiffusionRegression: " + message);
    }

    static void Near(Color actual, Color expected, string label)
    {
        for (int c = 0; c < 3; ++c)
            Check(!float.IsNaN(actual[c]) && Math.Abs(actual[c] - expected[c]) < 0.00015f,
                label + " channel " + c + " actual=" + actual[c] + " expected=" + expected[c]);
    }

    static RenderTexture Texture(int width, int height)
    {
        var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.Create();
        return rt;
    }

    static void Fill(RenderTexture rt, Color color)
    {
        RenderTexture.active = rt;
        GL.Clear(false, true, color);
    }

    static Color Read(RenderTexture rt, Texture2D read)
    {
        RenderTexture.active = rt;
        read.ReadPixels(new Rect(rt.width / 2, rt.height / 2, 1, 1), 0, 0);
        read.Apply();
        return read.GetPixel(0, 0);
    }

    static Color Composite(Color source, Color bloom, float weight, bool screen)
    {
        Color result = source;
        for (int c = 0; c < 3; ++c)
            result[c] = screen ? 1 - (1 - source[c]) * (1 - bloom[c] * weight) : source[c] + bloom[c] * weight;
        return result;
    }

    static Color Diffusion(Color composite, Color blur, Vector4 parameters)
    {
        Color result = composite;
        for (int c = 0; c < 3; ++c)
        {
            float square = composite[c] * composite[c];
            result[c] = Mathf.Max(composite[c], Mathf.Max(0, square + blur[c] - blur[c] * square - parameters.w)) * parameters.x;
        }
        float luminance = result.r * 0.2125f + result.g * 0.7154f + result.b * 0.0721f;
        for (int c = 0; c < 3; ++c)
            result[c] = (luminance + parameters.y * (result[c] - luminance) - 0.5f) * parameters.z + 0.5f;
        return result;
    }

    public static void RunGpu()
    {
        Check(Application.isBatchMode, "run only in an isolated batch-mode project");
        string path = Environment.GetEnvironmentVariable("UMA_POSTFILM_TEST_BUNDLE");
        Check(File.Exists(path), "missing local shader bundle");
        var bundle = AssetBundle.LoadFromFile(path);
        Check(bundle != null, "bundle load");
        var previous = RenderTexture.active;
        var source = Texture(64, 32);
        var low = Texture(16, 8);
        var vertical = Texture(16, 8);
        var bloom = Texture(16, 8);
        var target = Texture(64, 32);
        var read = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        Material fast = null, post = null, diffusion = null, diffusionDof = null;
        string[] floats = { "_bloomDofWeight", "_BloomIsScreenBlend" };
        float[] oldFloats = Array.ConvertAll(floats, Shader.GetGlobalFloat);
        string[] vectors = { "_Parameter", "_ColorParam", "_PixelSize" };
        Vector4[] oldVectors = Array.ConvertAll(vectors, Shader.GetGlobalVector);
        var oldBloom = Shader.GetGlobalTexture("_Bloom");
        var oldRgb = Shader.GetGlobalTexture("_RgbTex");
        var oldLowBackground = Shader.GetGlobalTexture("_TapLowBackground");
        try
        {
            foreach (var shader in bundle.LoadAllAssets<Shader>())
            {
                if (shader.name == "Gallop/ImageEffects/FastBloom") fast = new Material(shader);
                if (shader.name == "Gallop/ImageEffects/Rich/PostBloom_Rich") post = new Material(shader);
                if (shader.name == "Gallop/ImageEffects/Rich/PostDiffusionBloom_Rich") diffusion = new Material(shader);
                if (shader.name == "Gallop/ImageEffects/Rich/PostDiffusionDofBloom_Rich") diffusionDof = new Material(shader);
            }
            Check(fast && post && diffusion && diffusionDof, "four shipped shaders present");
            Check(fast.shader.isSupported && post.shader.isSupported && diffusion.shader.isSupported &&
                  diffusionDof.shader.isSupported, "shaders supported");
            post.SetColor("_DimmerColor", Color.white);
            Color input = new Color(0.05f, 0.4f, 1.4f, 1);
            Fill(source, input);
            foreach (float threshold in new[] { 0f, 0.1f, 0.23f })
            foreach (float intensity in new[] { 0f, 0.5f, 0.9f, 2f })
            {
                Color expected = input;
                for (int c = 0; c < 3; ++c) expected[c] = Mathf.Max(0, input[c] - threshold) * intensity;
                // Zero-radius native path: threshold/intensity both in downsample.
                Shader.SetGlobalVector("_Parameter", new Vector4(0, 0, threshold, intensity));
                Graphics.Blit(source, bloom, fast, 1);
                Near(Read(bloom, read), expected, "zero-radius extraction");
                // Positive radius path: neutral reduction, threshold only in vertical,
                // intensity only in horizontal. Both Gaussian kernels sum to one.
                Shader.SetGlobalVector("_Parameter", new Vector4(0, 0, 0, 1));
                Graphics.Blit(source, low, fast, 1);
                Shader.SetGlobalVector("_Parameter", new Vector4(1f / 1024, 1f / 512, threshold, intensity));
                Graphics.Blit(low, vertical, fast, 2);
                Graphics.Blit(vertical, bloom, fast, 3);
                Near(Read(bloom, read), expected, "two-axis extraction (no double threshold/intensity)");
            }
            var bloomColor = new Color(0.1f, 0.3f, 0.8f, 1);
            var blurColor = new Color(0.2f, 0.6f, 0.9f, 1);
            Fill(bloom, bloomColor);
            Fill(low, blurColor);
            Shader.SetGlobalTexture("_Bloom", bloom);
            Shader.SetGlobalTexture("_RgbTex", low);
            foreach (bool screen in new[] { false, true, false })
            foreach (float weight in new[] { 0f, 0.5f, 1f })
            {
                Shader.SetGlobalFloat("_bloomDofWeight", weight);
                Shader.SetGlobalFloat("_BloomIsScreenBlend", screen ? 1 : 0);
                var composite = Composite(input, bloomColor, weight, screen);
                Graphics.Blit(source, target, post, 0);
                Near(Read(target, read), composite, "Bloom Add/Screen HDR");
                foreach (var parameters in new[] { new Vector4(1, 1, 1, 0), new Vector4(0.8f, 0.5f, 1.1f, 0.2f), new Vector4(1.2f, 0, 0.9f, 0.1f) })
                {
                    Shader.SetGlobalVector("_ColorParam", parameters);
                    Graphics.Blit(source, target, diffusion, 2);
                    Near(Read(target, read), Diffusion(composite, blurColor, parameters), "Diffusion composite and color order");
                }
            }
            // Pass 1 combines Bloom and the first film layer. Pass 3 consumes
            // the previous output without sampling Bloom again.
            var film = new Gallop.RenderPipeline.ScreenOverlayRender.Render();
            var first = Gallop.RenderPipeline.ScreenOverlayRender.Parameter.Default();
            first.PostFilmMode = Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode.Add;
            first.PostFilmPower = 0.5f;
            first.PostFilmColor0 = first.PostFilmColor1 = first.PostFilmColor2 = first.PostFilmColor3 =
                new Color(0.4f, 0.2f, 0.1f, 1f);
            var later = Gallop.RenderPipeline.ScreenOverlayRender.Parameter.Default();
            later.PostFilmMode = Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode.Lerp;
            later.PostFilmPower = 0.5f;
            later.PostFilmColor0 = later.PostFilmColor1 = later.PostFilmColor2 = later.PostFilmColor3 = Color.black;
            Fill(source, new Color(0.2f, 0.3f, 0.4f, 1f));
            Fill(bloom, new Color(0.1f, 0.2f, 0.3f, 1f));
            Shader.SetGlobalTexture("_Bloom", bloom);
            Shader.SetGlobalFloat("_bloomDofWeight", 0.5f);
            Shader.SetGlobalFloat("_BloomIsScreenBlend", 0f);
            post.SetColor("_DimmerColor", Color.white);
            using (var cmd = new CommandBuffer())
            {
                film.Blit(ref first, cmd, RenderTextureHandle.Make(source), RenderTextureHandle.Make(target), post, 1);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                Near(Read(target, read), new Color(0.45f, 0.5f, 0.6f, 1f), "Bloom plus first film layer");
                film.Blit(ref later, cmd, RenderTextureHandle.Make(target), RenderTextureHandle.Make(source), post, 3);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                Near(Read(source, read), new Color(0.225f, 0.25f, 0.3f, 1f), "second film layer without repeated Bloom");
                film.Blit(ref later, cmd, RenderTextureHandle.Make(source), RenderTextureHandle.Make(target), post, 3);
                Graphics.ExecuteCommandBuffer(cmd);
                Near(Read(target, read), new Color(0.1125f, 0.125f, 0.15f, 1f), "third film layer without repeated Bloom");
            }
            // The DiffusionDofBloom mode can remain selected when its Bloom flag
            // is off. Execute binds black before the conditional extraction; verify
            // that its first film pass does not reuse the previous frame's bloom.
            var noBloomFilm = Gallop.RenderPipeline.ScreenOverlayRender.Parameter.Default();
            noBloomFilm.PostFilmMode = Gallop.ImageEffect.ScreenOverlay.Overlay.PostFilmMode.Lerp;
            noBloomFilm.PostFilmPower = 0.5f;
            noBloomFilm.PostFilmColor0 = noBloomFilm.PostFilmColor1 =
                noBloomFilm.PostFilmColor2 = noBloomFilm.PostFilmColor3 = Color.black;
            diffusionDof.SetColor("_DimmerColor", Color.white);
            Fill(source, new Color(0.2f, 0.3f, 0.4f, 1f));
            Fill(low, new Color(0.2f, 0.3f, 0.4f, 1f));
            Fill(bloom, new Color(0.9f, 0.9f, 0.9f, 1f));
            // Suppress diffusion's extra lift so this fixture isolates stale Bloom.
            Shader.SetGlobalVector("_ColorParam", new Vector4(1f, 1f, 1f, 1f));
            Shader.SetGlobalTexture("_TapLowBackground", low);
            Shader.SetGlobalTexture("_Bloom", bloom);
            Shader.SetGlobalTexture("_Bloom", Texture2D.blackTexture);
            using (var cmd = new CommandBuffer())
            {
                film.Blit(ref noBloomFilm, cmd, RenderTextureHandle.Make(source),
                    RenderTextureHandle.Make(target), diffusionDof, 11);
                Graphics.ExecuteCommandBuffer(cmd);
            }
            Near(Read(target, read), new Color(0.1f, 0.15f, 0.2f, 1f),
                "DiffusionDofBloom first film pass with Bloom disabled");
            Debug.Log("LiveBloomDiffusionRegression GPU PASS: 24 extraction, 9 Bloom and 27 diffusion equations; HDR/Add/Screen, three-layer Bloom-once PostFilm, and Bloom-disabled DiffusionDofBloom first film pass");
        }
        finally
        {
            for (int i = 0; i < floats.Length; ++i) Shader.SetGlobalFloat(floats[i], oldFloats[i]);
            for (int i = 0; i < vectors.Length; ++i) Shader.SetGlobalVector(vectors[i], oldVectors[i]);
            Shader.SetGlobalTexture("_Bloom", oldBloom);
            Shader.SetGlobalTexture("_RgbTex", oldRgb);
            Shader.SetGlobalTexture("_TapLowBackground", oldLowBackground);
            RenderTexture.active = previous;
            foreach (var rt in new[] { source, low, vertical, bloom, target }) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            UnityEngine.Object.DestroyImmediate(read);
            if (fast) UnityEngine.Object.DestroyImmediate(fast);
            if (post) UnityEngine.Object.DestroyImmediate(post);
            if (diffusion) UnityEngine.Object.DestroyImmediate(diffusion);
            if (diffusionDof) UnityEngine.Object.DestroyImmediate(diffusionDof);
            bundle.Unload(true);
        }
    }
}
