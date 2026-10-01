using System;
using System.IO;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEngine;
using UnityEngine.Rendering;

public static class LiveColorCorrectionRegression
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("LiveColorCorrectionRegression: " + message);
    }
    static void Draw(Material material, Texture lut, float saturation, Texture source, RenderTexture output)
    {
        using (var cmd = new CommandBuffer())
        {
            // Deliberately poison uniforms: production must establish all draw state.
            Shader.SetGlobalFloat("_PostFilmPower", 1);
            Shader.SetGlobalFloat("_Saturation", -8);
            SimpleColorCorrectionPass.Render(cmd, material, lut, saturation,
                new RenderTargetIdentifier(source), new RenderTargetIdentifier(output));
            Graphics.ExecuteCommandBuffer(cmd);
            Check(Shader.GetGlobalTexture("_RgbTex") == Texture2D.blackTexture, "release LUT global binding");
        }
    }
    static void RunSchedule()
    {
        int checks = 0;
        foreach (int evt in new[] { 500, 550, 600 })
        for (int mask = 1; mask < 8; ++mask)
        {
            bool dof = (mask & 1) != 0, radial = (mask & 2) != 0, color = (mask & 4) != 0;
            var schedule = PostImageEffectFeature.GetColorChainSchedule(dof, radial, color,
                (UnityEngine.Rendering.Universal.RenderPassEvent)evt);
            // Model source ownership in actual event order; a camera resolve consumes temps.
            var steps = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, string>>();
            Action<int, string> add = (e, name) => steps.Add(new System.Collections.Generic.KeyValuePair<int, string>(e, name));
            add(schedule.FirstEvent, "source");
            if (dof) add(evt, "dof");
            if (radial) add(evt, "radial");
            if (color) add(550, "color");
            if (schedule.EarlyResolve) add(550, "resolve");
            if (schedule.FinalResolve) add(schedule.LastEvent, "resolve");
            // LINQ OrderBy is stable, like URP's pass event sort.
            bool sourceIsCamera = true, initialized = false;
            foreach (var step in System.Linq.Enumerable.OrderBy(steps, x => x.Key))
            {
                if (step.Value == "source") { initialized = true; continue; }
                Check(initialized, "source setup must run first");
                if ((step.Value == "dof" || step.Value == "radial") && step.Key > 550)
                    Check(sourceIsCamera, "late effects must not discard an unresolved color temp");
                if (step.Value == "color" || step.Value == "radial" || (step.Value == "dof" && step.Key <= 550))
                    sourceIsCamera = false;
                if (step.Value == "resolve") sourceIsCamera = true;
            }
            Check(sourceIsCamera, "final result must resolve to camera");
            checks++;
        }
        Debug.Log("LiveColorCorrection schedule PASS: " + checks + " pass combinations/event boundaries (ownership model, not full URP execution)");
    }
    static void RunTimeline(LiveTimelineWorkSheet sheet)
    {
        RunSchedule();
        var groups = sheet.colorCorrectionDataLists;
        var keys = groups[0].keys.thisList;
        Func<float, SimpleColorCorrectionPass.Parameter> sample = frame =>
            LiveColorCorrectionTimeline.Evaluate(groups, frame, TimelinePlayerMode.Default);
        Check(!sample(-1).Enabled && !sample(float.NaN).Enabled && !sample(float.PositiveInfinity).Enabled,
            "pre-first and invalid time bypass");
        Check(!LiveColorCorrectionTimeline.Evaluate(null, 0, TimelinePlayerMode.Default).Enabled, "absent worksheet clears");
        using (var pass = new SimpleColorCorrectionPass())
        {
            Check((int)pass.renderPassEvent == 550, "native color event");
            foreach (int i in new[] { 0, 1, 2, 3, 4, 5, 3, 0, 5 })
            {
                var value = sample(keys[i].frame);
                Check(value.IsValid && value.Red == keys[i].redCurve, "exact cut/seek " + i);
                if (i > 0) Check(sample(keys[i].frame - .25f).Red == keys[i-1].redCurve, "hold until cut " + i);
                int before = pass.LutUploadCount;
                Check(pass.Prepare(value), "prepare valid");
                Check(pass.LutUploadCount == before + 1, "cut/seek uploads once");
                var texture = pass.LutTexture;
                for (int j = 0; j < 20; ++j) Check(pass.Prepare(value), "repeat valid");
                value.Saturation = .7f; pass.Prepare(value);
                Check(pass.LutUploadCount == before + 1 && texture == pass.LutTexture, "stable/saturation avoids upload");
            }
            Check(!pass.Prepare(default), "disable");
            int uploads = pass.LutUploadCount;
            pass.Prepare(sample(keys[5].frame));
            Check(pass.LutUploadCount == uploads + 1, "reenable refresh");
            pass.Dispose(); Check(pass.LutTexture == null, "pass LUT dispose");
            pass.Prepare(sample(0)); Check(pass.LutTexture != null, "pass recreate");
        }
        var list = groups[0].keys;
        var oldAttribute = list.attribute;
        list.attribute = LiveTimelineKeyDataListAttr.Disable;
        Check(!sample(0).Enabled, "disabled list"); list.attribute = oldAttribute;
        var key = keys[0];
        key.enable = 0; Check(!sample(0).Enabled, "disabled key"); key.enable = 1;
        key.mode = 1; Check(!sample(0).Enabled, "advanced bypass"); key.mode = 0;
        key.selective = 1; Check(!sample(0).Enabled, "selective bypass"); key.selective = 0;
        groups.Add(groups[0]); Check(!sample(0).Enabled, "ambiguous groups bypass"); groups.RemoveAt(1);
        PostImageEffectFeature.RuntimeParameter.ColorCorrection = sample(0);
        var copy = new PostImageEffectFeature.Parameter();
        copy.ShallowCopyFrom(PostImageEffectFeature.RuntimeParameter);
        Check(copy.ColorCorrection.IsValid && copy.ColorCorrection.Red == keys[0].redCurve, "parameter copy preserves color");
        PostImageEffectFeature.ResetRuntimeParameter();
        Check(!PostImageEffectFeature.RuntimeParameter.ColorCorrection.Enabled, "live teardown reset");
        Debug.Log("LiveColorCorrection timeline/cache PASS: six real cuts, reverse seek, unsupported modes, reset, LUT lifecycle");
    }
    // D3D normalized sampling: pixel centers at (i+.5)/width, exact RGB row centers.
    // Do not use Texture2D.GetPixelBilinear as a GPU oracle (CPU coordinate convention).
    static float SampleRow(Texture2D lut, float u, int row)
    {
        float x = Mathf.Clamp(u * 256f - 0.5f, 0, 255);
        int a = Mathf.FloorToInt(x), b = Mathf.Min(a + 1, 255);
        return Mathf.Lerp(lut.GetPixel(a, row).r, lut.GetPixel(b, row).r, x - a);
    }
    static Color Expected(Texture2D lut, Color source, float saturation)
    {
        var color = new Color(SampleRow(lut, source.r, 0),
            SampleRow(lut, source.g, 1), SampleRow(lut, source.b, 2), source.a);
        float luminance = color.r * 0.2126729f + color.g * 0.7151522f + color.b * 0.072175f;
        for (int c = 0; c < 3; ++c) color[c] = luminance + saturation * (color[c] - luminance);
        return color;
    }
    // Spatial fixture: all 256 LUT intervals, distinct RGB/alpha ramps, HDR edges.
    // Also checks the observed native ordering (Bloom -> curves), not only isolated color swatches.
    static void RunSpatial(AssetBundle bundle, Material material,
        System.Collections.Generic.List<LiveTimelineKeyColorCorrectionData> keys, ColorCorrectionCurveLut lut)
    {
        const int width = 257, height = 9;
        var pixels = new Color[width * height];
        for (int y = 0; y < height; ++y)
        for (int x = 0; x < width; ++x)
        {
            float r = x / 256f;
            float g = ((x * 73 + y * 29) % 257) / 256f;
            float b = ((x * 31 + y * 67) % 257) / 256f;
            if (y == 0) { r -= .2f; g += .4f; }
            pixels[y * width + x] = new Color(r, g, b, (y * width + x) / (float)(pixels.Length - 1));
        }
        var source = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
        var read = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
        var bloom = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        var intermediate = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var output = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;
        var oldAniso = QualitySettings.anisotropicFiltering;
        var oldBloom = Shader.GetGlobalTexture("_Bloom");
        float oldWeight = Shader.GetGlobalFloat("_bloomDofWeight"), oldScreen = Shader.GetGlobalFloat("_BloomIsScreenBlend");
        Material post = null;
        int checks = 0, orderWitnesses = 0;
        Color bloomColor = new Color(.16f, .08f, .24f, 1);
        try
        {
            foreach (var shader in bundle.LoadAllAssets<Shader>())
                if (shader.name == "Gallop/ImageEffects/Rich/PostBloom_Rich") post = new Material(shader);
            Check(post && post.shader.isSupported, "spatial Bloom shader");
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            Check(lut.Texture.anisoLevel == 0, "data LUT must opt out of forced anisotropic filtering");
            post.SetColor("_DimmerColor", Color.white);
            source.filterMode = FilterMode.Point; source.wrapMode = TextureWrapMode.Clamp;
            source.SetPixels(pixels); source.Apply(false, false);
            bloom.SetPixel(0, 0, bloomColor); bloom.Apply(false, false);
            intermediate.Create(); output.Create();
            Shader.SetGlobalTexture("_Bloom", bloom);
            Shader.SetGlobalFloat("_bloomDofWeight", .7f);
            foreach (var key in keys)
            {
                lut.Update(key.redCurve, key.greenCurve, key.blueCurve);
                Shader.SetGlobalTexture("_RgbTex", lut.Texture);
                // -1 = curves alone, 0 = additive Bloom then curves, 1 = screen Bloom then curves.
                foreach (int blend in new[] { -1, 0, 1 })
                foreach (float saturation in new[] { 1f, 0f, 1.4f })
                {
                    Shader.SetGlobalFloat("_Saturation", saturation);
                    if (blend >= 0)
                    {
                        Shader.SetGlobalFloat("_BloomIsScreenBlend", blend);
                        Graphics.Blit(source, intermediate, post, 0);
                        Draw(material, lut.Texture, saturation, intermediate, output);
                    }
                    else Draw(material, lut.Texture, saturation, source, output);
                    RenderTexture.active = output;
                    read.ReadPixels(new Rect(0, 0, width, height), 0, 0); read.Apply(false, false);
                    var actual = read.GetPixels();
                    for (int i = 0; i < pixels.Length; ++i)
                    {
                        Color composed = pixels[i];
                        if (blend >= 0)
                        {
                            for (int c = 0; c < 4; ++c)
                                composed[c] = blend == 0 ? composed[c] + bloomColor[c] * .7f :
                                    1 - (1 - composed[c]) * (1 - bloomColor[c] * .7f);
                            // Native PostBloom_Rich applies Add/Screen to all four channels.
                        }
                        Color expected = Expected(lut.Texture, composed, saturation);
                        for (int c = 0; c < 4; ++c)
                            if (float.IsNaN(actual[i][c]) || Mathf.Abs(actual[i][c] - expected[c]) >= .0015f)
                                throw new Exception("LiveColorCorrectionRegression spatial frame=" + key.frame +
                                    " blend=" + blend + " saturation=" + saturation + " pixel=" + i +
                                    " channel=" + c + " got=" + actual[i][c] + " expected=" + expected[c] + " source=" + pixels[i].ToString("F7") + " rgba=" + actual[i].ToString("F7") + " expectedRGBA=" + expected.ToString("F7"));
                        if (blend == 0 && saturation == 1)
                        {
                            Color reversed = Expected(lut.Texture, pixels[i], saturation);
                            if (Mathf.Abs(expected.r - (reversed.r + bloomColor.r * .7f)) > .02f) orderWitnesses++;
                        }
                        checks++;
                    }
                }
            }
            Check(orderWitnesses > 100, "fixture must detect accidentally moving curves before Bloom");
            Debug.Log("LiveColorCorrectionRegression spatial PASS: " + checks +
                " RGBA pixels; LUT ramps/HDR/alpha and Bloom-before-curves Add/Screen; order witnesses=" + orderWitnesses);
        }
        finally
        {
            QualitySettings.anisotropicFiltering = oldAniso;
            Shader.SetGlobalTexture("_Bloom", oldBloom);
            Shader.SetGlobalFloat("_bloomDofWeight", oldWeight); Shader.SetGlobalFloat("_BloomIsScreenBlend", oldScreen);
            RenderTexture.active = previous;
            intermediate.Release(); output.Release();
            UnityEngine.Object.DestroyImmediate(intermediate); UnityEngine.Object.DestroyImmediate(output);
            UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(read); UnityEngine.Object.DestroyImmediate(bloom);
            if (post) UnityEngine.Object.DestroyImmediate(post);
        }
    }

    public static void RunGpu()
    {
        Check(Application.isBatchMode, "isolated batch mode only");
        string path = Environment.GetEnvironmentVariable("UMA_COLOR_TEST_KEYS");
        Check(File.Exists(path), "UMA_COLOR_TEST_KEYS must contain the local Live1176 worksheet fixture");
        var sheet = ScriptableObject.CreateInstance<LiveTimelineWorkSheet>();
        AssetBundle bundle = null;
        Material material = null;
        var source = new RenderTexture(16, 16, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var output = new RenderTexture(16, 16, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var read = new Texture2D(16, 16, TextureFormat.RGBAFloat, false, true);
        var previous = RenderTexture.active;
        var oldRgb = Shader.GetGlobalTexture("_RgbTex");
        float oldSaturation = Shader.GetGlobalFloat("_Saturation"), oldPower = Shader.GetGlobalFloat("_PostFilmPower");
        using (var lut = new ColorCorrectionCurveLut())
        try
        {
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), sheet);
            Check(sheet.colorCorrectionDataLists.Count == 1, "real group deserialization");
            var keys = sheet.colorCorrectionDataLists[0].keys.thisList;
            Check(keys.Count == 6 && keys[0].enable == 1 && keys[5].frame == 7066, "six actual enabled cuts");
            RunTimeline(sheet);
            Check(!keys[0].IsInterpolateKey(), "shipped keys do not have interpolation fields");
            Check(keys[0].redCurve.length == 2 && Math.Abs(keys[0].redCurve.keys[0].outTangent - 0.7630331f) < 0.00001f,
                "native AnimationCurve tangents survive deserialization");
            Check(ColorCorrectionCurveLut.Quantize(-1) == 0 && ColorCorrectionCurveLut.Quantize(2) == 255 &&
                ColorCorrectionCurveLut.Quantize(0.5f) == 127, "truncating native byte conversion");
            var identity = AnimationCurve.Linear(0, 0, 1, 1);
            var zero = AnimationCurve.Constant(0, 1, 0);
            var one = AnimationCurve.Constant(0, 1, 1);
            var pixels = new Color32[1024];
            ColorCorrectionCurveLut.Fill(pixels, zero, one, identity, one, zero, identity, 0.5f);
            Check(pixels[0].r == 127 && pixels[256].g == 127 && pixels[767].b == 255 && pixels[768].a == 0,
                "row layout, endpoint, pre-quantization curve blend, unused row");
            bundle = AssetBundle.LoadFromFile(Environment.GetEnvironmentVariable("UMA_POSTFILM_TEST_BUNDLE"));
            foreach (var shader in bundle.LoadAllAssets<Shader>())
                if (shader.name == "Gallop/ImageEffects/ColorCorrectionCurvesSimple") material = new Material(shader);
            Check(material && material.shader.isSupported, "shipped simple shader supported");
            // Mask power=0 means full correction in DXBC (not correction disabled).
            Shader.SetGlobalFloat("_PostFilmPower", 0);
            source.Create(); output.Create();
            int checks = 0;
            Texture2D firstTexture = null;
            foreach (var key in keys)
            {
                lut.Update(key.redCurve, key.greenCurve, key.blueCurve);
                if (firstTexture == null) firstTexture = lut.Texture;
                Check(firstTexture == lut.Texture && lut.Texture.mipmapCount == 1, "reuse LUT without mips");
                foreach (float saturation in new[] { 0f, 1f, 1.4f })
                foreach (Color color in new[] { new Color(0.2f, 0.4f, 0.8f, 0.35f), new Color(0, 0.5f, 1, 0.8f), new Color(-0.2f, 1.4f, 0.7f, 1) })
                {
                    Shader.SetGlobalTexture("_RgbTex", lut.Texture);
                    Shader.SetGlobalFloat("_Saturation", saturation);
                    RenderTexture.active = source; GL.Clear(false, true, color);
                    Draw(material, lut.Texture, saturation, source, output);
                    RenderTexture.active = output;
                    read.ReadPixels(new Rect(0, 0, 16, 16), 0, 0); read.Apply();
                    Color actual = read.GetPixel(8, 8), expected = Expected(lut.Texture, color, saturation);
                    for (int c = 0; c < 4; ++c)
                        Check(!float.IsNaN(actual[c]) && Mathf.Abs(actual[c] - expected[c]) < 0.0015f,
                            "GPU curve row/saturation/alpha input=" + color + " saturation=" + saturation + " frame=" + key.frame + " channel=" + c + " got=" + actual[c] + " expected=" + expected[c]);
                    checks++;
                }
            }
            Debug.Log("Color LUT sampling diagnostics aniso=" + lut.Texture.anisoLevel + " filter=" + lut.Texture.filterMode + " qualityAniso=" + QualitySettings.anisotropicFiltering);
            RunSpatial(bundle, material, keys, lut);
            lut.Dispose(); Check(lut.Texture == null, "LUT released");
            lut.Update(identity, identity, identity); Check(lut.Texture != null, "LUT recreated after dispose");
            Debug.Log("LiveColorCorrectionRegression PASS: actual Live1176 six curve keys, byte packing and " + checks + " GPU color/alpha checks; production command-buffer draw verified; full Live render-context parity not tested");
        }
        finally
        {
            Shader.SetGlobalTexture("_RgbTex", oldRgb);
            Shader.SetGlobalFloat("_Saturation", oldSaturation); Shader.SetGlobalFloat("_PostFilmPower", oldPower);
            RenderTexture.active = previous;
            source.Release(); output.Release();
            UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(output);
            UnityEngine.Object.DestroyImmediate(read); UnityEngine.Object.DestroyImmediate(sheet);
            if (material) UnityEngine.Object.DestroyImmediate(material);
            if (bundle) bundle.Unload(true);
        }
    }
}
