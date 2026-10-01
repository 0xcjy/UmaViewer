using System;
using System.IO;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Overlay = Gallop.ImageEffect.ScreenOverlay.Overlay;

public static class LivePostFilmRegression
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception("PostFilm: " + message); }
    static void Near(float a, float b, string message) { Check(Mathf.Abs(a-b)<0.002f, message + ": " + a + " vs " + b); }
    [MenuItem("UmaViewer/Rendering/Validate PostFilm timeline")]
    public static void Run()
    {
        var keys = new LiveTimelineKeyPostFilmDataList();
        var a = new LiveTimelineKeyPostFilmData { frame=10, filmMode=PostFilmMode.Add, filmPower=2,
            attribute=(LiveTimelineKeyAttribute)0x100000, color0=Color.red, color1=Color.green,
            color2=Color.blue, color3=Color.white, depthPower=-0.75f, DepthClip=2, FilmScale=Vector2.one };
        var b = new LiveTimelineKeyPostFilmData { frame=30, filmMode=PostFilmMode.Mul, filmPower=4,
            interpolateType=LiveCameraInterpolateType.Linear, color0=Color.red, color1=Color.green,
            color2=Color.blue, color3=Color.white, depthPower=-0.25f, DepthClip=2, FilmScale=Vector2.one };
        keys.thisList.Add(a); keys.thisList.Add(b);
        var output = new Overlay();
        foreach (PostColorType type in Enum.GetValues(typeof(PostColorType)))
        {
            a.colorType=type;
            LivePostFilmTimeline.Evaluate(keys,20,TimelinePlayerMode.Default,output);
            Near(output.postFilmPower,3,"raw unclamped power"); Near(output.depthPower,-0.5f,"signed depth");
            Check(output.inverseVignette && output.IsEnableDepth,"inverse is not depth disable");
            Check(output.postFilmMode==Overlay.PostFilmMode.Add,"mode must not switch before cut");
            Check(output.postFilmColor0==Color.red,"corner0");
            Check(output.postFilmColor1==(type==PostColorType.ColorAll || type==PostColorType.Color2TopBottom ? Color.red:Color.green),"corner1");
            Check(output.postFilmColor2==(type==PostColorType.ColorAll || type==PostColorType.Color2LeftRight ? Color.red:type==PostColorType.Color2TopBottom ? Color.green:Color.blue),"corner2");
            Check(output.postFilmColor3==(type==PostColorType.ColorAll ? Color.red:type==PostColorType.Color4 ? Color.white:Color.green),"corner3");
        }
        LivePostFilmTimeline.Evaluate(keys,30,TimelinePlayerMode.Default,output);
        Check(output.postFilmMode==Overlay.PostFilmMode.Mul && !output.inverseVignette,"exact cut");
        LivePostFilmTimeline.Evaluate(keys,20,TimelinePlayerMode.Default,output); Near(output.postFilmPower,3,"seek backward");
        LivePostFilmTimeline.Evaluate(keys,0,TimelinePlayerMode.Default,output); Check(!output.IsValid(),"before first key");
        keys.attribute=LiveTimelineKeyDataListAttr.Disable;
        LivePostFilmTimeline.Evaluate(keys,20,TimelinePlayerMode.Default,output); Check(!output.IsValid(),"disabled track");
        keys.attribute=0; a.layerMode=LiveTimelineKeyPostFilmData.LayerMode.UVMovie;
        LivePostFilmTimeline.Evaluate(keys,20,TimelinePlayerMode.Default,output); Check(!output.IsValid(),"unsupported movie bypass");
        a.layerMode=LiveTimelineKeyPostFilmData.LayerMode.Color; a.FilmScale=Vector2.zero; b.FilmScale=Vector2.zero;
        LivePostFilmTimeline.Evaluate(keys,20,TimelinePlayerMode.Default,output); Near(output.ScaleParameter.x,1,"safe scale");
        LivePostFilmTimeline.Evaluate(null,20,TimelinePlayerMode.Default,output); Check(!output.IsValid(),"missing track reset");
        var sheet=ScriptableObject.CreateInstance<LiveTimelineWorkSheet>();
        try {
            JsonUtility.FromJsonOverwrite("{\"postFilmKeys\":{\"thisList\":[{\"frame\":0,\"filmMode\":5,\"attribute\":1048576,\"filmPower\":0.1}]},\"postFilm2Keys\":{\"thisList\":[]},\"postFilm3Keys\":{\"thisList\":[]}}",sheet);
            Check(sheet.postFilmKeys.Count==1 && sheet.postFilmKeys.thisList[0].filmMode==PostFilmMode.VignetteAdd,"worksheet deserialization");
        } finally { UnityEngine.Object.DestroyImmediate(sheet); }
        ValidateBlinkSync();
        Debug.Log("LivePostFilmRegression timeline PASS");
    }

    private static void ValidateBlinkSync()
    {
        var keys=new LiveTimelineKeyPostFilmDataList();
        var a=new LiveTimelineKeyPostFilmData { frame=10, filmMode=PostFilmMode.Add,
            layerMode=LiveTimelineKeyPostFilmData.LayerMode.Color, colorType=PostColorType.Color4,
            attribute=(LiveTimelineKeyAttribute)0x1e00000,
            BlinkLightName="stage_beam", BlinkLightContainerIndex=2,
            BlinkLightBrightnessPower=1f, IsAdjustedBlinkLightColor=false,
            color0=new Color(.1f,.2f,.3f,.11f), color1=new Color(.1f,.2f,.3f,.22f),
            color2=new Color(.1f,.2f,.3f,.33f), color3=new Color(.1f,.2f,.3f,.44f), FilmScale=Vector2.one };
        var b=new LiveTimelineKeyPostFilmData { frame=30, filmMode=PostFilmMode.Add,
            interpolateType=LiveCameraInterpolateType.Linear, colorType=PostColorType.Color4,
            color0=Color.white, color1=Color.white, color2=Color.white, color3=Color.white,
            FilmScale=Vector2.one };
        keys.thisList.Add(a); keys.thisList.Add(b);
        var output=new Overlay();
        int lookups=0;
        LivePostFilmTimeline.BlinkColorResolver resolve=delegate(string name,int hash,int index,out Color raw) {
            Check(name=="stage_beam" && index==2,"lookup identity");
            lookups++; raw=new Color(.2f,.4f,.6f,.9f); return true;
        };
        LivePostFilmTimeline.Evaluate(keys,20,TimelinePlayerMode.Default,output,resolve);
        Check(lookups==1,"one lookup shared by all four corners");
        foreach(Color c in new[]{output.postFilmColor0,output.postFilmColor1,output.postFilmColor2,output.postFilmColor3})
            Near(c.r,.6f,"synced R interpolates to next unsynced color");
        Near(output.postFilmColor0.a,.555f,"synced corner0 preserves authored alpha");
        Near(output.postFilmColor1.a,.61f,"synced corner1 preserves authored alpha");
        Near(output.postFilmColor2.a,.665f,"synced corner2 preserves authored alpha");
        Near(output.postFilmColor3.a,.72f,"synced corner3 preserves authored alpha");
        b.attribute=(LiveTimelineKeyAttribute)0x1e00000;
        LivePostFilmTimeline.Evaluate(keys,20,TimelinePlayerMode.Default,output,resolve);
        foreach(Color c in new[]{output.postFilmColor0,output.postFilmColor1,output.postFilmColor2,output.postFilmColor3})
            Near(c.r,.2f,"next sync holds current resolved RGB");
        Near(output.postFilmColor3.a,.44f,"next sync holds current authored alpha");
        a.attribute=0;
        LivePostFilmTimeline.Evaluate(keys,20,TimelinePlayerMode.Default,output,resolve);
        Near(output.postFilmColor0.r,.1f,"next sync holds authored current without lookup");
        a.attribute=(LiveTimelineKeyAttribute)0x200000;
        b.attribute=0;
        a.BlinkLightBrightnessPower=0f; a.IsAdjustedBlinkLightColor=true;
        LivePostFilmTimeline.Evaluate(keys,10,TimelinePlayerMode.Default,output,resolve);
        Near(output.postFilmColor0.r,1f,"key brightness zero plus adjusted color");
        Near(output.postFilmColor1.r,.1f,"only selected corner synced");
        Near(output.postFilmColor0.a,.11f,"raw stage alpha not copied");
        LivePostFilmTimeline.BlinkColorResolver missing=delegate(string name,int hash,int index,out Color raw) {
            raw=Color.black; return false;
        };
        LivePostFilmTimeline.Evaluate(keys,10,TimelinePlayerMode.Default,output,missing);
        Near(output.postFilmColor0.r,.1f,"failed lookup retains authored color");
        a.BlinkLightBrightnessPower=3f; a.IsAdjustedBlinkLightColor=false;
        LivePostFilmTimeline.Evaluate(keys,10,TimelinePlayerMode.Default,output,resolve);
        Check(output.postFilmColor0.b>1f,"key brightness can produce HDR color");
        Debug.Log("LivePostFilmRegression blink sync PASS");
    }

    // Only for an isolated batch-mode project: this test changes global shader state.
    public static void RunGpu()
    {
        Check(Application.isBatchMode,"GPU regression must run in isolated batch mode");
        Run();
        string bundlePath=Environment.GetEnvironmentVariable("UMA_POSTFILM_TEST_BUNDLE");
        Check(File.Exists(bundlePath),"set UMA_POSTFILM_TEST_BUNDLE to the locally extracted shader bundle");
        var bundle=AssetBundle.LoadFromFile(bundlePath);
        Material material=null;
        var source=new RenderTexture(32,32,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        var target=new RenderTexture(32,32,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        var read=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);
        var previous=RenderTexture.active;
        try {
            Shader shader=null;
            foreach(var candidate in bundle.LoadAllAssets<Shader>()) if(candidate.name=="Gallop/ImageEffects/Rich/PostBlit_Rich") shader=candidate;
            Check(shader!=null && shader.isSupported,"official shader supported");
            material=new Material(shader);
            source.Create(); target.Create();
            RenderTexture.active=source; GL.Clear(false,true,new Color(0.2f,0.3f,0.4f,1));
            var overlay=new Overlay(); LivePostFilmTimeline.Reset(overlay);
            overlay.postFilmMode=Overlay.PostFilmMode.Add; overlay.postFilmPower=0.5f;
            overlay.postFilmColor0=overlay.postFilmColor1=overlay.postFilmColor2=overlay.postFilmColor3=new Color(0.4f,0.2f,0.1f,1);
            overlay.IsEnableDepth=true;
            var renderer=new ScreenOverlayRender.Render();
            var param=new ScreenOverlayRender.Parameter(); param.Setup(overlay);
            foreach(bool explicitColor in new[]{false,true}) {
                using(var cmd=new CommandBuffer()) {
                    if(explicitColor) cmd.EnableShaderKeyword("COLOR_ONLY"); else cmd.DisableShaderKeyword("COLOR_ONLY");
                    renderer.Blit(ref param,cmd,RenderTextureHandle.Make(source),RenderTextureHandle.Make(target),material,3);
                    Graphics.ExecuteCommandBuffer(cmd);
                }
                RenderTexture.active=target; read.ReadPixels(new Rect(0,0,32,32),0,0); read.Apply();
                var pixel=read.GetPixel(16,16);
                Debug.Log("PostFilm GPU COLOR_ONLY="+explicitColor+" pixel="+pixel.ToString("F5"));
                Near(pixel.r,0.4f,"Add red"); Near(pixel.g,0.4f,"Add green"); Near(pixel.b,0.45f,"Add blue");
            }
            // Both first/later passes must execute the requested inverse-vignette variant.
            overlay.postFilmMode=Overlay.PostFilmMode.VignetteAdd;
            overlay.postFilmOptionParam=new Vector4(0.3f,0.8f,0,0);
            overlay.postFilmOffsetParam=new Vector2(0,0.58f);
            Color[] normal=null;
            foreach(int pass in new[]{1,3})
            foreach(bool inverse in new[]{false,true}) {
                overlay.inverseVignette=inverse; param.Setup(overlay);
                using(var cmd=new CommandBuffer()) {
                    cmd.DisableShaderKeyword("COLOR_ONLY");
                    renderer.Blit(ref param,cmd,RenderTextureHandle.Make(source),RenderTextureHandle.Make(target),material,pass);
                    Graphics.ExecuteCommandBuffer(cmd);
                }
                RenderTexture.active=target; read.ReadPixels(new Rect(0,0,32,32),0,0); read.Apply();
                var pixels=read.GetPixels(); float maxDifference=0;
                for(int i=0;i<pixels.Length;i++) {
                    Check(!float.IsNaN(pixels[i].r) && !float.IsInfinity(pixels[i].r),"finite vignette output");
                    if(inverse) maxDifference=Mathf.Max(maxDifference,Mathf.Abs(pixels[i].r-normal[i].r));
                }
                if(!inverse) normal=pixels;
                else Check(maxDifference>0.01f,"inverse pass must change the spatial mask");
                Debug.Log("PostFilm vignette pass="+pass+" inverse="+inverse+" difference="+maxDifference);
            }
            // Sequential layers use the previous layer's pixels, not the original source.
            overlay.inverseVignette=false; overlay.postFilmMode=Overlay.PostFilmMode.Add; param.Setup(overlay);
            using(var cmd=new CommandBuffer()) {
                renderer.Blit(ref param,cmd,RenderTextureHandle.Make(source),RenderTextureHandle.Make(target),material,3);
                overlay.postFilmMode=Overlay.PostFilmMode.Lerp; overlay.postFilmPower=0.5f;
                overlay.postFilmColor0=overlay.postFilmColor1=overlay.postFilmColor2=overlay.postFilmColor3=Color.black;
                param.Setup(overlay);
                renderer.Blit(ref param,cmd,RenderTextureHandle.Make(target),RenderTextureHandle.Make(source),material,3);
                Graphics.ExecuteCommandBuffer(cmd);
            }
            RenderTexture.active=source; read.ReadPixels(new Rect(0,0,32,32),0,0); read.Apply();
            var stacked=read.GetPixel(16,16);
            Near(stacked.r,0.2f,"ordered Add then Lerp red"); Near(stacked.g,0.2f,"ordered Add then Lerp green"); Near(stacked.b,0.225f,"ordered Add then Lerp blue");
            Debug.Log("LivePostFilmRegression GPU PASS");
        } finally {
            RenderTexture.active=previous;
            if(material!=null) UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(read);
            if(bundle!=null) bundle.Unload(true);
        }
    }
}


