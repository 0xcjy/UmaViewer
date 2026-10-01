using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEngine;
using UnityEngine.Rendering;
using Overlay = Gallop.ImageEffect.ScreenOverlay.Overlay;

// Film-only attribution on already captured real pixels. This is NOT a full Live replay:
// input is before DOF/Bloom/diffusion; actual depth is absent. Synced BlinkLight RGB
// is available only when explicitly recovered from same-frame runtime metadata.
// Raw-depth sweeps are explicitly counterfactual, not estimated scene depth.
public static class LiveCapturedFilmAttribution
{
    [Serializable] public class CurveKey { public float time, value, inSlope, outSlope, inWeight, outWeight; public int weightedMode; }
    [Serializable] public class Row { public LiveTimelineKeyPostFilmData key; public CurveKey[] curveKeys; public int preWrap, postWrap; }
    [Serializable] public class Track { public string name; public int attribute, playMode; public Row[] rows; }
    [Serializable] public class Frame { public int frame; public string imagePath, sha256; public float[] expectedPowers; public Color rawBlinkRgb; public Color[] expectedColor0; public string blinkLookup; }
    [Serializable] public class Input { public Track[] tracks; public Frame[] frames; }
    [Serializable] public class Stats {
        public string image, stage; public int frame, layer, omittedLayer; public float rawDepth;
        public float[] mean, maximum; public float anyChannelOverOne, allChannelsNearWhite;
        public float shaderParityMaxError;
    }
    [Serializable] public class ParameterRecord {
        public int frame, layer; public string mode; public float power, depthPower, depthClip;
        public bool valid, inverse; public Vector2 offset; public Vector4 options; public Color[] colors;
    }
    [Serializable] public class Report {
        public string limitation, colorSpace, inputPath; public List<ParameterRecord> parameters = new List<ParameterRecord>();
        public List<Stats> stages = new List<Stats>();
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception("CapturedFilmAttribution: " + message); }
    static RenderTexture RT(int w, int h, RenderTextureFormat format) {
        var rt = new RenderTexture(w, h, 0, format, RenderTextureReadWrite.Default) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        rt.Create(); return rt;
    }
    static Color[] Read(RenderTexture rt) {
        var old = RenderTexture.active;
        var read = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
        try { RenderTexture.active = rt; read.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); read.Apply(); return read.GetPixels(); }
        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(read); }
    }
    static void Save(RenderTexture rt, string path) {
        var old = RenderTexture.active;
        var read = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
        try { RenderTexture.active = rt; read.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); read.Apply(); File.WriteAllBytes(path, read.EncodeToPNG()); }
        finally { RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(read); }
    }
    static Stats Measure(Color[] pixels, int frame, float depth, int layer, string stage, int omitted = 0) {
        var sums = new double[3]; var maximum = new float[3]; int over = 0, white = 0;
        foreach (var p in pixels) {
            bool any = false, all = true;
            for (int c = 0; c < 3; ++c) {
                Check(!float.IsNaN(p[c]) && !float.IsInfinity(p[c]), "non-finite output");
                sums[c] += p[c]; maximum[c] = Mathf.Max(maximum[c], p[c]);
                any |= p[c] > 1.0001f; all &= p[c] >= 250f / 255f;
            }
            if (any) ++over; if (all) ++white;
        }
        return new Stats { frame=frame, rawDepth=depth, layer=layer, stage=stage, omittedLayer=omitted,
            mean=Array.ConvertAll(sums, x => (float)(x / pixels.Length)), maximum=maximum,
            anyChannelOverOne=over/(float)pixels.Length, allChannelsNearWhite=white/(float)pixels.Length };
    }
    static void Draw(ScreenOverlayRender.Render renderer, ref ScreenOverlayRender.Parameter param,
        Texture source, RenderTexture target, Material material, int pass) {
        using (var cmd = new CommandBuffer()) {
            if (source is RenderTexture rt)
                renderer.Blit(ref param, cmd, RenderTextureHandle.Make(rt), RenderTextureHandle.Make(target), material, pass);
            else throw new Exception("Source must have an explicit RT descriptor");
            Graphics.ExecuteCommandBuffer(cmd);
        }
    }
    public static void Run() {
        Check(Application.isBatchMode, "isolated batch-mode only; do not use user's Editor");
        Check(QualitySettings.activeColorSpace == ColorSpace.Gamma, "captured Gamma project required");
        string inputPath = Environment.GetEnvironmentVariable("UMA_FILM_ATTRIBUTION_INPUT");
        string output = Environment.GetEnvironmentVariable("UMA_FILM_ATTRIBUTION_OUTPUT");
        string bundlePath = Environment.GetEnvironmentVariable("UMA_POSTFILM_TEST_BUNDLE");
        Check(File.Exists(inputPath) && File.Exists(bundlePath) && !string.IsNullOrEmpty(output), "missing input environment");
        Directory.CreateDirectory(output);
        var input = JsonUtility.FromJson<Input>(File.ReadAllText(inputPath));
        Check(input.tracks.Length == 3 && input.frames.Length == 3, "three authored tracks and three real frames");
        var tracks = new LiveTimelineKeyPostFilmDataList[3];
        for (int i = 0; i < 3; ++i) {
            var t = input.tracks[i];
            tracks[i] = new LiveTimelineKeyPostFilmDataList { attribute=(LiveTimelineKeyDataListAttr)t.attribute, playMode=(TimelineKeyPlayMode)t.playMode };
            foreach (var row in t.rows) {
                var keys = Array.ConvertAll(row.curveKeys, x => new Keyframe(x.time,x.value,x.inSlope,x.outSlope,x.inWeight,x.outWeight) { weightedMode=(WeightedMode)x.weightedMode });
                row.key.curve = new AnimationCurve(keys) { preWrapMode=(WrapMode)row.preWrap, postWrapMode=(WrapMode)row.postWrap };
                tracks[i].thisList.Add(row.key);
            }
        }
        var report = new Report { inputPath=inputPath, colorSpace=QualitySettings.activeColorSpace.ToString(),
            limitation="Film-only, before-DOF/Bloom real captured input. Raw hardware-depth sweep is counterfactual; no captured depth, captured BlinkLight RGB when present, no color correction or game-reference parity. Float probes measure shader outputs before ARGB32 clamping; next layer consumes ARGB32 just as native film ping-pong." };
        var bundle = AssetBundle.LoadFromFile(bundlePath);
        Check(bundle != null, "load official shader bundle");
        Material film=null, combined=null;
        var oldDepth=Shader.GetGlobalTexture("_CameraDepthTexture"); var oldScroll=Shader.GetGlobalVector("_GlobalScreenUVScrollParam");
        var oldInverseSize=Shader.GetGlobalVector("_InvRenderTargetSize");
        var depth = new Texture2D(1,1,TextureFormat.RFloat,false,true) { filterMode=FilterMode.Point };
        try {
            foreach (var shader in bundle.LoadAllAssets<Shader>()) {
                if (shader.name == "Gallop/ImageEffects/Rich/PostBlit_Rich") film = new Material(shader);
                if (shader.name == "Gallop/ImageEffects/Rich/PostDiffusionDofBloom_Rich") combined = new Material(shader);
            }
            Check(film != null && film.shader.isSupported && combined != null && combined.shader.isSupported, "shipped shaders supported");
            film.SetColor("_DimmerColor",Color.white); combined.SetColor("_DimmerColor",Color.white);
            Shader.DisableKeyword("COLOR_ONLY"); // Match production's color-layer Apply (no enabled blend keyword).
            Shader.SetGlobalVector("_GlobalScreenUVScrollParam",Vector4.zero);
            var renderer = new ScreenOverlayRender.Render();
            foreach (var f in input.frames) {
                using (var hash = SHA256.Create()) {
                    string actual = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(f.imagePath))).Replace("-","").ToLowerInvariant();
                    Check(actual==f.sha256,"input hash " + f.frame);
                }
                var image = new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                Check(image.LoadImage(File.ReadAllBytes(f.imagePath),false),"load real pixels");
                var source = RT(image.width,image.height,RenderTextureFormat.ARGB32);
                var a = RT(image.width,image.height,RenderTextureFormat.ARGB32);
                var b = RT(image.width,image.height,RenderTextureFormat.ARGB32);
                var probe = RT(image.width,image.height,RenderTextureFormat.ARGBFloat);
                var alternate = RT(image.width,image.height,RenderTextureFormat.ARGBFloat);
                try {
                    Graphics.Blit(image,source);
                    Shader.SetGlobalVector("_InvRenderTargetSize",new Vector4(1f/image.width,1f/image.height,image.width,image.height));
                    var param = new ScreenOverlayRender.Parameter[3];
                    for (int i=0;i<3;++i) {
                        var overlay = new Overlay();
                        if (f.expectedColor0 != null && f.expectedColor0.Length == 3) {
                            // Captured current-stage raw RGB, recovered only from a unit-brightness
                            // sync layer. Native key brightness is applied inside Evaluate.
                            LivePostFilmTimeline.BlinkColorResolver resolve = delegate(string name,int hash,int index,out Color raw) {
                                raw = f.rawBlinkRgb; return true;
                            };
                            LivePostFilmTimeline.Evaluate(tracks[i],f.frame,TimelinePlayerMode.Default,overlay,resolve);
                            var expected=f.expectedColor0[i]; var actual=overlay.postFilmColor0;
                            Check(Mathf.Abs(actual.r-expected.r)<0.0002f && Mathf.Abs(actual.g-expected.g)<0.0002f &&
                                  Mathf.Abs(actual.b-expected.b)<0.0002f && Mathf.Abs(actual.a-expected.a)<0.0002f,
                                  "captured synced color0 frame="+f.frame+" layer="+(i+1)+" actual="+actual+" expected="+expected);
                        } else LivePostFilmTimeline.Evaluate(tracks[i],f.frame,TimelinePlayerMode.Default,overlay);
                        param[i]=ScreenOverlayRender.Parameter.Default(); param[i].Setup(overlay);
                        Check(Mathf.Abs(param[i].PostFilmPower-f.expectedPowers[i])<0.0002f,"evaluated power vs captured runtime frame="+f.frame+" layer="+(i+1)+" actual="+param[i].PostFilmPower+" expected="+f.expectedPowers[i]);
                        report.parameters.Add(new ParameterRecord { frame=f.frame, layer=i+1, mode=param[i].PostFilmMode.ToString(),power=param[i].PostFilmPower,depthPower=param[i].DepthPower,depthClip=param[i].DepthClip,valid=param[i].IsValidity,inverse=param[i].InverseVignette,offset=param[i].PostFilmOffsetParam,options=param[i].PostFilmOptionParam,colors=new[]{param[i].PostFilmColor0,param[i].PostFilmColor1,param[i].PostFilmColor2,param[i].PostFilmColor3} });
                    }
                    report.stages.Add(Measure(Read(source),f.frame,-1,0,"input"));
                    foreach (float raw in new[]{0f,0.02f,0.1f,1f}) {
                        depth.SetPixel(0,0,new Color(raw,0,0,1)); depth.Apply(); Shader.SetGlobalTexture("_CameraDepthTexture",depth);
                        RenderTexture current=source;
                        for (int i=0;i<3;++i) {
                            RenderTexture next = current==a ? b : a;
                            if (param[i].IsValidity) {
                                Draw(renderer,ref param[i],current,probe,film,i==0?1:3);
                                // Production later-pass arithmetic should match plain PostBlit on identical inputs.
                                Draw(renderer,ref param[i],current,alternate,combined,13);
                                var values=Read(probe); var other=Read(alternate); float maxError=0;
                                for (int n=0;n<values.Length;++n) for (int c=0;c<3;++c) maxError=Mathf.Max(maxError,Mathf.Abs(values[n][c]-other[n][c]));
                                Check(maxError<0.002f,"film variant parity " + maxError);
                                var measured=Measure(values,f.frame,raw,i+1,"before-output-clamp"); measured.shaderParityMaxError=maxError; report.stages.Add(measured);
                                Draw(renderer,ref param[i],current,next,film,i==0?1:3);
                            } else Graphics.Blit(current,next);
                            string name="frame"+f.frame.ToString("D6")+"_rawdepth"+raw.ToString("0.00",CultureInfo.InvariantCulture)+"_layer"+(i+1)+".png";
                            Save(next,Path.Combine(output,name)); var s=Measure(Read(next),f.frame,raw,i+1,"ARGB32"); s.image=name; report.stages.Add(s); current=next;
                        }
                        // Leave-one-layer-out experiments retain actual authored powers/order of all other layers.
                        for (int omitted=1;omitted<=3;++omitted) {
                            current=source;
                            for (int i=0;i<3;++i) {
                                RenderTexture next=current==a ? b : a;
                                if (i+1!=omitted && param[i].IsValidity) Draw(renderer,ref param[i],current,next,film,i==0?1:3);
                                else Graphics.Blit(current,next);
                                current=next;
                            }
                            string name="frame"+f.frame.ToString("D6")+"_rawdepth"+raw.ToString("0.00",CultureInfo.InvariantCulture)+"_omit"+omitted+".png";
                            Save(current,Path.Combine(output,name)); var s=Measure(Read(current),f.frame,raw,3,"counterfactual-omit",omitted); s.image=name; report.stages.Add(s);
                        }
                    }
                } finally {
                    foreach (var rt in new[]{source,a,b,probe,alternate}) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
            File.WriteAllText(Path.Combine(output,"report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("LiveCapturedFilmAttribution PASS: real PNG hashes, nine exact runtime powers, captured sync RGB when provided, authored curves, film variant parity, counterfactual depth sweeps and omit-one layers. NOT full Live parity. Output="+output);
        } finally {
            Shader.SetGlobalTexture("_CameraDepthTexture",oldDepth); Shader.SetGlobalVector("_GlobalScreenUVScrollParam",oldScroll); Shader.SetGlobalVector("_InvRenderTargetSize",oldInverseSize);
            UnityEngine.Object.DestroyImmediate(depth); if(film!=null)UnityEngine.Object.DestroyImmediate(film); if(combined!=null)UnityEngine.Object.DestroyImmediate(combined); bundle.Unload(true);
        }
    }
}
