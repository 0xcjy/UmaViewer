using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;

public static class LiveRadialBlurRegression
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception("RadialBlur: " + message); }
    static void Near(float a, float b, string message) { Check(Mathf.Abs(a-b)<0.002f,message+" actual="+a+" expected="+b); }
    public static void Run()
    {
        var sheet=ScriptableObject.CreateInstance<LiveTimelineWorkSheet>();
        try {
            JsonUtility.FromJsonOverwrite("{\"radialBlurKeys\":{\"thisList\":[{\"frame\":10,\"moveBlurType\":2,\"attribute\":65536,\"radialBlurDownsample\":3,\"radialBlurIteration\":2,\"radialBlurPower\":0.5,\"depthPowerBack\":38},{\"frame\":20,\"moveBlurType\":0}]}}",sheet);
            var keys=sheet.radialBlurKeys;
            Check(keys.Count==2,"worksheet track deserialization");
            var p=LiveRadialBlurTimeline.Evaluate(keys,10,TimelinePlayerMode.Default);
            Check(p.Type==2 && p.IsEnabledDepth && p.Downsample==3 && p.Iteration==2,"discrete key values");
            Near(p.Power,0.5f,"raw strength"); Near(p.DepthPowerBack,38,"authored distance");
            Check(!LiveRadialBlurTimeline.Evaluate(keys,20,TimelinePlayerMode.Default).IsValid,"off key");
            Check(LiveRadialBlurTimeline.Evaluate(keys,10,TimelinePlayerMode.Default).IsValid,"reverse seek");
            Check(!LiveRadialBlurTimeline.Evaluate(keys,0,TimelinePlayerMode.Default).IsValid,"pre first");
            keys.attribute=LiveTimelineKeyDataListAttr.Disable;
            Check(!LiveRadialBlurTimeline.Evaluate(keys,10,TimelinePlayerMode.Default).IsValid,"disabled");
            Check(!LiveRadialBlurTimeline.Evaluate(null,10,TimelinePlayerMode.Default).IsValid,"absent track reset");
            for(int mode=1;mode<=5;mode++) Check(RadialBlurPass.BlurPassIndex(mode)==2*(mode-1),"native pass mapping");
            Check(RadialBlurPass.AreaDifference(0,0)==0.0001f,"zero interval guard");
            Check(RadialBlurPass.AreaDifference(1,0.99999f)<0,"signed interval guard");
            p.DepthCancelRect=new Vector4(0.2f,0.3f,0.4f,0.5f); p.IsExpandDepthCancelRect=true; p.DepthCancelBlendLength=0.1f;
            var bounds=RadialBlurPass.CancelBounds(p); Near(bounds.x,0.1f,"expanded min"); Near(bounds.w,0.9f,"expanded max");
            var feature=new PostImageEffectFeature.Parameter(); feature.RadialBlur=p;
            Check(feature.IsUseDepthTexture,"radial requests depth independently");
            var copy=new PostImageEffectFeature.Parameter(); copy.ShallowCopyParameters(feature);
            Check(copy.RadialBlur.Type==2,"parameter copy");
        } finally { UnityEngine.Object.DestroyImmediate(sheet); }
        string fixture=Environment.GetEnvironmentVariable("UMA_RADIAL_TEST_KEYS");
        if(!string.IsNullOrEmpty(fixture)) {
            var realKeys=JsonUtility.FromJson<LiveTimelineKeyRadialBlurDataList>(File.ReadAllText(fixture));
            Check(realKeys.Count==28,"Live1001 actual key count");
            Check(LiveRadialBlurTimeline.Evaluate(realKeys,728,TimelinePlayerMode.Default).Type==0,"real before onset");
            var onset=LiveRadialBlurTimeline.Evaluate(realKeys,729,TimelinePlayerMode.Default);
            Check(onset.Type==2&&onset.IsEnabledDepth,"real onset");Near(onset.DepthPowerBack,38,"real mask distance");
            Check(LiveRadialBlurTimeline.Evaluate(realKeys,988,TimelinePlayerMode.Default).Type==0,"real off cut");
            Check(LiveRadialBlurTimeline.Evaluate(realKeys,3965,TimelinePlayerMode.Default).Type==1,"real radial impact");
            for(int f=0;f<=7900;f++) {
                var sample=LiveRadialBlurTimeline.Evaluate(realKeys,f,TimelinePlayerMode.Default);
                Check(sample.Type==0||sample.IsValid,"real full track finite frame="+f);
            }
            Debug.Log("LiveRadialBlurRegression real Live1001 7901 timeline samples PASS");
        }
        Debug.Log("LiveRadialBlurRegression timeline PASS");
    }
    static float Difference(Color[] a, Color[] b)
    {
        float sum=0;
        for(int i=0;i<a.Length;i++) {
            Check(!float.IsNaN(a[i].r)&&!float.IsInfinity(a[i].r)&&!float.IsNaN(a[i].g)&&!float.IsInfinity(a[i].g),"finite pixels");
            sum+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);
        }
        return sum/(a.Length*3);
    }
    public static void RunGpu()
    {
        Check(Application.isBatchMode,"isolated batch mode required"); Run();
        var bundle=AssetBundle.LoadFromFile(Environment.GetEnvironmentVariable("UMA_POSTFILM_TEST_BUNDLE"));
        Material material=null;
        var source=new RenderTexture(64,64,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        var target=new RenderTexture(64,64,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        var texture=new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);
        var read=new Texture2D(64,64,TextureFormat.RGBAFloat,false,true);
        var cameraObject=new GameObject("RadialRegressionCamera"); var camera=cameraObject.AddComponent<Camera>();
        camera.nearClipPlane=0.3f;camera.farClipPlane=100f;
        var previous=RenderTexture.active;
        var depthTexture=new Texture2D(64,64,TextureFormat.RFloat,false,true) { filterMode=FilterMode.Point };
        var previousDepth=Shader.GetGlobalTexture("_CameraDepthTexture");
        var previousZ=Shader.GetGlobalVector("_ZBufferParams");
        var previousScroll=Shader.GetGlobalVector("_GlobalScreenUVScrollParam");
        try {
            Shader shader=null; foreach(var candidate in bundle.LoadAllAssets<Shader>()) if(candidate.name=="Cygames/ImageEffects/RadialBlur") shader=candidate;
            Check(shader!=null&&shader.isSupported,"official shader available"); material=new Material(shader);
            source.Create();target.Create();
            var pixels=new Color[64*64];
            for(int y=0;y<64;y++) for(int x=0;x<64;x++) pixels[y*64+x]=new Color((x%8<4)?0.8f:0.1f,(y%8<4)?0.7f:0.2f,0.3f,1);
            texture.SetPixels(pixels);texture.Apply();Graphics.Blit(texture,source);
            Func<RadialBlurPass.Parameter,Color[]> render=p=>{
                using(var cmd=new CommandBuffer()) {
                    RadialBlurPass.Render(cmd,material,p,camera,source,target,source.descriptor);
                    Graphics.ExecuteCommandBuffer(cmd);
                }
                RenderTexture.active=target;read.ReadPixels(new Rect(0,0,64,64),0,0);read.Apply();return read.GetPixels();
            };
            var p=RadialBlurPass.Parameter.Default();p.Type=1;p.Power=0;
            var baseline=render(p);Near(Difference(baseline,pixels),0,"zero power identity");
            for(int mode=1;mode<=5;mode++) {
                p.Type=mode;p.Power=2;p.StartArea=0;p.EndArea=0;p.Downsample=2;p.Iteration=2;p.RollEulerAngles=35;
                var result=render(p);float difference=Difference(result,baseline);
                Check(difference>0.005f,"mode "+mode+" actually changes source");
                Debug.Log("Radial GPU mode="+mode+" meanDifference="+difference);
            }
            // Real shader's depth gate: front < z <= back preserves source, farther pixels blur.
            p.Type=2;p.Downsample=1;p.Iteration=1;p.Power=2;p.IsEnabledDepth=true;
            p.DepthPowerFront=0;p.DepthPowerBack=38;
            foreach(bool reversed in new[]{false,true}) {
                float ratio=camera.farClipPlane/camera.nearClipPlane;
                float zx=reversed?ratio-1:1-ratio, zy=reversed?1:ratio;
                Shader.SetGlobalVector("_ZBufferParams",new Vector4(zx,zy,zx/100,zy/100));
                Shader.SetGlobalVector("_GlobalScreenUVScrollParam",Vector4.zero);
                for(int y=0;y<64;y++) for(int x=0;x<64;x++) {
                    float distance=x<32?10:60;
                    depthTexture.SetPixel(x,y,new Color((100/distance-zy)/zx,0,0,0));
                }
                depthTexture.Apply();Shader.SetGlobalTexture("_CameraDepthTexture",depthTexture);
                var gated=render(p);float protectedError=0,farDifference=0;
                for(int y=4;y<60;y++) for(int x=4;x<60;x++) {
                    float d=Mathf.Abs(gated[y*64+x].r-baseline[y*64+x].r);
                    if(x<28)protectedError=Mathf.Max(protectedError,d);
                    if(x>36)farDifference+=d;
                }
                Near(protectedError,0,"near object depth protection reversed="+reversed);
                Check(farDifference>1,"far background blur reversed="+reversed);
                p.IsEnabledDepthCancelRect=true;p.DepthCancelRect=new Vector4(0.1f,0.1f,0.3f,0.8f);
                p.DepthCancelBlendLength=0.02f;
                var cancelled=render(p);float restoredBlur=0;
                for(int y=12;y<52;y++)for(int x=10;x<22;x++)restoredBlur+=Mathf.Abs(cancelled[y*64+x].r-gated[y*64+x].r);
                Check(restoredBlur>1,"cancel rectangle overrides depth protection");
                p.IsEnabledDepthCancelRect=false;
                Debug.Log("Radial depth GPU reversed="+reversed+" protectedError="+protectedError+" farDifference="+farDifference+" rectDifference="+restoredBlur);
            }
            p.IsEnabledDepth=true;p.IsEnabledDepthCancelRect=true;p.DepthPowerFront=4;p.DepthPowerBack=38;
            p.DepthCancelRect=new Vector4(0.2f,0.3f,0.4f,0.5f);p.DepthCancelBlendLength=0.1f;
            RadialBlurPass.ApplyMaterial(material,p,camera);
            Near(material.GetFloat("_DepthPowerFront"),(4-0.3f)/99.7f,"normalized front");
            Near(material.GetFloat("_DepthPowerBack"),(38-0.3f)/99.7f,"normalized back");
            Check(material.IsKeywordEnabled("ENABLE_DEPTH")&&material.IsKeywordEnabled("ENABLE_DEPTH_CANCEL_RECT"),"depth keywords");
            p.IsEnabledDepth=false;RadialBlurPass.ApplyMaterial(material,p,camera);
            Check(!material.IsKeywordEnabled("ENABLE_DEPTH")&&!material.IsKeywordEnabled("ENABLE_DEPTH_CANCEL_RECT"),"stale keyword reset");
            Near(material.GetFloat("_DepthPowerBack"),0,"stale distance reset");
            p.Downsample=0;p.Iteration=0;Difference(render(p),baseline);
            Debug.Log("LiveRadialBlurRegression GPU PASS");
        } finally {
            RenderTexture.active=previous;
            Shader.SetGlobalTexture("_CameraDepthTexture",previousDepth);
            Shader.SetGlobalVector("_ZBufferParams",previousZ);
            Shader.SetGlobalVector("_GlobalScreenUVScrollParam",previousScroll);
            UnityEngine.Object.DestroyImmediate(depthTexture);
            UnityEngine.Object.DestroyImmediate(material);UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(read);UnityEngine.Object.DestroyImmediate(cameraObject);
            if(bundle!=null) bundle.Unload(true);
        }
    }
}
