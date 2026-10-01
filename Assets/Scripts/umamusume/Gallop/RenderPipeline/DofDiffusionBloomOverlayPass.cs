using System;
using System.Collections.Generic;
using System.Reflection;
using Gallop.ImageEffect;
using Gallop.Live;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Gallop 的 Dof / Diffusion / Bloom / Overlay 渲染通道。
    ///
    /// 根据此版本官方 IL2CPP 确认的内容：
    /// - 设置 / 材质映射
    /// - DecideDrawType
    /// - PrepareDofParam / FocalDistance01
    /// - CreateBloomTexture
    /// - 新的 DOF / Bloom / Diffusion 渲染路径
    /// - 加权 / 混合 / 圆盘 / 球形模糊路径
    /// - 旧的 DOF 渲染链
    /// - 执行外部分发和源渲染纹理的交接
    ///
    /// 仍作为临时/待定部分隔离：
    /// - 旧 DOF 的近裁剪阈值常量
    /// - 官方静态构造函数中用于临时渲染纹理名称的字符串
    /// </summary>
    public class DofDiffusionBloomOverlayPass : ScriptableRenderPass
    {
        private const string POOL_NAME = "DofDiffusionBloomOverlayPass";
        private static readonly int FocalStartId = Shader.PropertyToID("_GallopDofFocalStart");
        private Material _foregroundCocMaterial;

        private const int INVALID_DOWNSAMPLE_TEXTURE_NAMEID = -2;
        private const int BLOOM_DIVIDER = 4;
        private const float DOFONEOVERBASESIZE = 0.001953125f; // 1 / 512
        private const float DOF_HEIGHT_BASE_SIZE_HORIZONTAL = 0.0034722222f;
        private const float DOF_HEIGHT_BASE_SIZE_VERTICAL = 0.0010986328f;

        private const int PASS_WEIGHTED_BLUR = 0;
        private const int PASS_WEIGHTED_BLUR2 = 1;
        private const int PASS_DISC_BLUR = 2;
        private const int PASS_BALL_BLUR_EXTRACTION = 3;
        private const int PASS_BALL_BLUR = 4;
        private const int PASS_BALL_BLUR_COMBINE = 5;

        private const float MAX_COC_LIMIT = 0.05f;
        private const float MAX_COC_DIVISOR = 50f;
        private const float MAX_COC_MULTIPLIER = 4f;
        private const float MAX_COC_BIAS = 6f;

        private const int PASS_FASTBLOOM_DOWNSAMPLE = 1;
        private const int PASS_FASTBLOOM_VERTICALBLUR = 2;
        private const int PASS_FASTBLOOM_HORIZONTALBLUR = 3;

        private const int PASS_POSTBLIT_DIMMERONLY = 0;
        private const int PASS_POSTBLIT_OVERLAY1 = 1;
        private const int PASS_POSTBLIT_OVERLAY2 = 3;

        private const int PASS_POSTBLOOM_BLOOM = 0;
        private const int PASS_POSTBLOOM_OVERLAY1 = 1;
        private const int PASS_POSTBLOOM_OVERLAY2 = 3;

        private const int PASS_POSTDOFBLOOM_DOWNSAMPLE = 3;
        private const int PASS_POSTDOFBLOOM_DOFBLOOM = 4;
        private const int PASS_POSTDOFBLOOM_COCBG_RICH = 7;
        private const int PASS_POSTDOFBLOOM_COCBGFG = 8;
        private const int PASS_POSTDOFBLOOM_OVERLAY1 = 9;
        private const int PASS_POSTDOFBLOOM_OVERLAY2 = 11;

        // The official cctor names have not been dumped yet. The IDs only need to
        // remain unique and stable inside this pass.
        private static readonly int TEMP_RT0_NAME = Shader.PropertyToID("_GallopDofDiffusionBloomTempRT0");
        private static readonly int TEMP_RT1_NAME = Shader.PropertyToID("_GallopDofDiffusionBloomTempRT1");
        private static readonly int TEMP_RT2_NAME = Shader.PropertyToID("_GallopDofDiffusionBloomTempRT2");
        private static readonly int FOREGROUND_RT_NAME = Shader.PropertyToID("_GallopDofForegroundRT");
        private static readonly int MEDIUMREZWORK_RT_NAME = Shader.PropertyToID("_GallopDofMediumRezWorkRT");
        private static readonly int FINALDEFOCUS_RT_NAME = Shader.PropertyToID("_GallopDofFinalDefocusRT");
        private static readonly int LOWREZOWORK_RT0_NAME = Shader.PropertyToID("_GallopDofLowRezWorkRT0");
        private static readonly int LOWREZOWORK_RT1_NAME = Shader.PropertyToID("_GallopDofLowRezWorkRT1");
        private static readonly int TEMP_OLDBLUR_FG_RT_NAME = Shader.PropertyToID("_GallopOldDofBlurFgRT");
        private static readonly int TEMP_DOWNSAMPLE_RT0_NAME = Shader.PropertyToID("_GallopDofDownSampleRT0");
        private static readonly int TEMP_DOWNSAMPLE_RT1_NAME = Shader.PropertyToID("_GallopDofDownSampleRT1");
        private static readonly int TEMP_DOWNSAMPLE_RT2_NAME = Shader.PropertyToID("_GallopDofDownSampleRT2");
        private static readonly int TEMP_DOFBLOOM_RT_NAME = Shader.PropertyToID("_GallopDofBloomRT");
        private static readonly int TEMP_BLUR_RT0_NAME = Shader.PropertyToID("_GallopDofBlurRT0");
        private static readonly int TEMP_BALLBLUR_RT0_NAME = Shader.PropertyToID("_GallopBallBlurRT0");
        private static readonly int TEMP_BALLBLUR_RT1_NAME = Shader.PropertyToID("_GallopBallBlurRT1");
        private static readonly int TEMP_BALLBLUR_RT2_NAME = Shader.PropertyToID("_GallopBallBlurRT2");
        private static readonly int TEMP_DOF_RT_NAME = Shader.PropertyToID("_GallopDofRT");
        private static readonly int TEMP_DIFFUSIONDOFBLOOM_RT_NAME = Shader.PropertyToID("_GallopDiffusionDofBloomRT");
        private static readonly int TEMP_DIFFUSIONFILTER_RT_NAME = Shader.PropertyToID("_GallopDiffusionFilterRT");

        /// <summary>
        /// Adapter for the CameraData member at native offset +0x150.
        /// Assign this once from your CameraData implementation when its field name is known.
        /// A reflection fallback is used when this remains null.
        /// </summary>
        public static Func<CameraData, PostImageEffectFeature.Parameter> FeatureParameterResolver;

        /// <summary>
        /// Official quality singleton condition: instance + 0x2C == 3.
        /// Bind this to the matching quality manager when that type is identified.
        /// </summary>
        public static Func<bool> SpecialQualityMode3Resolver;

        /// <summary>
        /// Official OldDof foreground condition adds a static float to nearClipPlane.
        /// The owner of that static field has not yet been identified.
        /// Bind this resolver when the value is recovered; zero keeps the unresolved
        /// dependency isolated instead of hard-coding a guessed constant.
        /// </summary>
        public static Func<float> OldDofNearClipOffsetResolver;

        /// <summary>
        /// The static Vector4 at the official constants class static-fields + 0x14.
        /// z/w are used by DOF; x/y are used by FastBloom.
        /// Replace this value after the static field is identified.
        /// </summary>
        public static Vector4 DefaultShaderParameter = new Vector4(0f, 0f, 0f, 1f);

        /// <summary>
        /// Official constants static-fields + 0x08 (12-byte Vector3), copied by Parameter.Default.
        /// Replace after dumping the three floats from that static field.
        /// </summary>
        public static Vector3 DefaultDofFocalPosition = Vector3.zero;

        // Official capability singleton qword_7FFDAE10B848 static-field bytes:
        // +0x04 Bloom, +0x05 DOF, +0x06 Overlay, +0x07 Diffusion.
        // The owning managed type is still unidentified.
        public static bool EnableBloomCapability = true;
        public static bool EnableDofCapability = true;
        public static bool EnableOverlayCapability = true;
        public static bool EnableDiffusionCapability = true;

        private CameraData _cameraData;
        private PostImageEffectFeature _feature;
        private PostImageEffectFeature.Parameter _featureParameter;

        private Material _oldDofMaterial;
        private Material _oldDofBlurMaterial;
        private Material _dofBloomMaterial;
        private Material _diffusionBloomMaterial;
        private Material _diffusionDofBloomMaterial;
        private Material _weightedBlurMaterial;
        private Material _bloomMaterial;
        private Material _blitMaterial;
        private Material _fastBloomMaterial;

        private float _dofWidthOverHeight;
        private float _dofHeightBaseSize;
        private int _rezworkWidth;
        private int _rezworkHeight;

        private ScreenOverlayRender _overlayRender;
        private bool _loggedExecute;
        private bool _loggedMaterialDiagnostics;
        private float _nextDofParameterLogTime;

        // Snapshot of the values actually sent to the shader during Execute.
        // LiveFrameCapture reads this after WaitForEndOfFrame so diagnostics can
        // distinguish render-time camera state from the later presentation state.
        public static bool LastDofDiagnosticsValid { get; private set; }
        public static int LastDofUnityFrame { get; private set; } = -1;
        public static int LastDofCameraId { get; private set; }
        public static float LastDofFocalDistance01 { get; private set; }
        public static float LastDofFocalEnd { get; private set; }
        public static float LastDofDepthRange { get; private set; }
        public static float LastDofFocalDepth { get; private set; }
        public static Vector3 LastDofFocalPosition { get; private set; }
        public static string LastDofFocusCalculation { get; private set; }

        // A bad/stripped official shader pass must never replace the camera
        // image with an empty temporary target. Keep the warning one-shot per
        // material/pass pair so a malformed bundle remains diagnosable without
        // flooding the Editor log every frame.
        private static readonly HashSet<string> _invalidPassWarnings =
            new HashSet<string>();

        public DofDiffusionBloomOverlayPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            _overlayRender = CreateOverlayRender();
        }

        private static ScreenOverlayRender CreateOverlayRender()
        {
            try
            {
                return (ScreenOverlayRender)Activator.CreateInstance(typeof(ScreenOverlayRender), true);
            }
            catch (Exception ex)
            {
                Debug.LogError("[DofDiffusionBloomOverlayPass] Failed to create ScreenOverlayRender: " + ex);
                return null;
            }
        }

        private bool CheckAndCreateMaterial(ref Material material, ShaderManager.ShaderKinds shaderKind)
        {
            if (material != null)
                return true;

            Shader shader = ShaderManager.GetShader(shaderKind);
            if (shader == null)
                return false;

            material = new Material(shader);
            return true;
        }

        public bool Setup(CameraData cameraData, PostImageEffectFeature feature)
        {
            _cameraData = null;
            _feature = null;
            _featureParameter = null;

            if (cameraData == null || feature == null)
                return false;

            PostImageEffectFeature.Parameter featureParameter = ResolveFeatureParameter(cameraData);
            if (featureParameter == null)
                return false;

            ref Parameter param = ref featureParameter.DofDiffuionBloomOverlay;
            if (!param.IsValidity)
                return false;

            if (!CheckAndCreateMaterial(ref _oldDofMaterial, ShaderManager.ShaderKinds.DepthOfField34) ||
                !CheckAndCreateMaterial(ref _oldDofBlurMaterial, ShaderManager.ShaderKinds.SeparableWeightedBlurDof34) ||
                !CheckAndCreateMaterial(ref _fastBloomMaterial, ShaderManager.ShaderKinds.FastBloom) ||
                !CheckAndCreateMaterial(ref _dofBloomMaterial, ShaderManager.ShaderKinds.PostDofBloom_Rich) ||
                !CheckAndCreateMaterial(ref _diffusionBloomMaterial, ShaderManager.ShaderKinds.PostDiffusionBloom_Rich) ||
                !CheckAndCreateMaterial(ref _diffusionDofBloomMaterial, ShaderManager.ShaderKinds.PostDiffusionDofBloom_Rich) ||
                !CheckAndCreateMaterial(ref _weightedBlurMaterial, ShaderManager.ShaderKinds.WeightedBlur) ||
                !CheckAndCreateMaterial(ref _bloomMaterial, ShaderManager.ShaderKinds.PostBloom_Rich) ||
                !CheckAndCreateMaterial(ref _blitMaterial, ShaderManager.ShaderKinds.PostBlit_Rich))
            {
                return false;
            }

            if (!_loggedMaterialDiagnostics)
            {
                _loggedMaterialDiagnostics = true;
                LogMaterialDiagnostics("DepthOfField34", _oldDofMaterial);
                LogMaterialDiagnostics("SeparableWeightedBlurDof34", _oldDofBlurMaterial);
                LogMaterialDiagnostics("FastBloom", _fastBloomMaterial);
                LogMaterialDiagnostics("PostDofBloom_Rich", _dofBloomMaterial);
                LogMaterialDiagnostics("PostDiffusionBloom_Rich", _diffusionBloomMaterial);
                LogMaterialDiagnostics("PostDiffusionDofBloom_Rich", _diffusionDofBloomMaterial);
                LogMaterialDiagnostics("WeightedBlur", _weightedBlurMaterial);
                LogMaterialDiagnostics("PostBloom_Rich", _bloomMaterial);
                LogMaterialDiagnostics("PostBlit_Rich", _blitMaterial);
            }

            renderPassEvent = param.RenderPassEvent;
            _cameraData = cameraData;
            _feature = feature;
            _featureParameter = featureParameter;

            if (_overlayRender == null)
                _overlayRender = CreateOverlayRender();

            return true;
        }

        private static void LogMaterialDiagnostics(string label, Material material)
        {
            if (material == null)
            {
                Debug.LogWarning("[RecoveredPostEffect] material " + label + " is null");
                return;
            }

            Shader shader = material.shader;
            Debug.Log("[RecoveredPostEffect] material " + label +
                      " shader=" + (shader != null ? shader.name : "<null>") +
                      " passes=" + material.passCount +
                      " mainTex=" + material.HasProperty("_MainTex") +
                      " blitTexture=" + material.HasProperty("_BlitTexture") +
                      " parameter=" + material.HasProperty("_Parameter") +
                      " invTarget=" + material.HasProperty("_InvRenderTargetSize"));
        }

        /// <summary>
        /// Direct overload for projects that already have the feature parameter object at RegisterPass time.
        /// </summary>
        public bool Setup(
            CameraData cameraData,
            PostImageEffectFeature feature,
            PostImageEffectFeature.Parameter featureParameter)
        {
            FeatureParameterResolver = _ => featureParameter;
            return Setup(cameraData, feature);
        }

        private static PostImageEffectFeature.Parameter ResolveFeatureParameter(CameraData cameraData)
        {
            if (FeatureParameterResolver != null)
            {
                PostImageEffectFeature.Parameter resolved = FeatureParameterResolver(cameraData);
                if (resolved != null)
                    return resolved;
            }

            // Native Setup reads CameraData + 0x150. Until the managed member name is known,
            // search direct fields/properties for the matching parameter class.
            object boxed = cameraData;
            Type type = boxed.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (type != null)
            {
                FieldInfo[] fields = type.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (typeof(PostImageEffectFeature.Parameter).IsAssignableFrom(fields[i].FieldType))
                        return fields[i].GetValue(boxed) as PostImageEffectFeature.Parameter;
                }

                PropertyInfo[] properties = type.GetProperties(flags);
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                        continue;
                    if (!typeof(PostImageEffectFeature.Parameter).IsAssignableFrom(property.PropertyType))
                        continue;

                    try
                    {
                        return property.GetValue(boxed, null) as PostImageEffectFeature.Parameter;
                    }
                    catch
                    {
                        // Keep scanning.
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        public void Dispose()
        {
            DestroyMaterial(ref _foregroundCocMaterial);
            DestroyMaterial(ref _oldDofMaterial);
            DestroyMaterial(ref _oldDofBlurMaterial);
            DestroyMaterial(ref _dofBloomMaterial);
            DestroyMaterial(ref _diffusionBloomMaterial);
            DestroyMaterial(ref _diffusionDofBloomMaterial);
            DestroyMaterial(ref _weightedBlurMaterial);
            DestroyMaterial(ref _bloomMaterial);
            DestroyMaterial(ref _blitMaterial);
            DestroyMaterial(ref _fastBloomMaterial);

            _cameraData = default;
            _feature = null;
            _featureParameter = null;
            _overlayRender = null;
            _loggedExecute = false;
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(material);
            else
                UnityEngine.Object.DestroyImmediate(material);

            material = null;
        }

        private static void RestoreShader(Material material, ShaderManager.ShaderKinds kind)
        {
            if (material == null)
                return;

            Shader expected = ShaderManager.GetShader(kind);
            if (expected != null && material.shader != expected)
                material.shader = expected;
        }

        private void PrepareDofParam(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source)
        {
            Camera camera = param.TargetCamera;
            if (camera == null)
                throw new InvalidOperationException("DofDiffusionBloomOverlayPass.Parameter.TargetCamera is null.");

            float near = camera.nearClipPlane;
            float depthRange = camera.farClipPlane - near;
            float focalDistance01 = 0.1f;

            // Native PrepareDofParam (RVA 0x1a20030) divides the raw viewport z by
            // far-near and only clamps negatives: 0x181a201c8 reads Vector3.z,
            // 0x181a201cd divides, 0x181a201d2..0x181a201d8 clamp the low side.
            // There is no near-plane subtraction and no upper Clamp01 here; the
            // shared `focalDistance01 < 0` guard below is the native clamp.
            switch ((int)param.DofFocalType)
            {
                case 0:
                    if (param.DofFocalTransfrom != null)
                    {
                        float focalDepth = camera.WorldToViewportPoint(param.DofFocalTransfrom.position).z;
                        focalDistance01 = focalDepth / depthRange;
                    }
                    else
                    {
                        focalDistance01 = FocalDistance01(ref param, param.DofFocalPoint);
                    }
                    break;

                case 1:
                    float positionDepth = camera.WorldToViewportPoint(param.DofFocalPosition).z;
                    focalDistance01 = positionDepth / depthRange;
                    break;

                case 2:
                    focalDistance01 = FocalDistance01(ref param, param.DofFocalPoint);
                    break;
            }

            if (focalDistance01 < 0f)
                focalDistance01 = 0f;

            if (param.DofSmoothness < 0.1f)
                param.DofSmoothness = 0.1f;

            int width = GetHandleWidth(source);
            int height = GetHandleHeight(source);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Invalid source RenderTextureHandle size.");

            int invTargetSizeId = ShaderManager.GetPropertyId(ShaderManager.PropertyId._InvRenderTargetSize);
            cmd.SetGlobalVector(
                invTargetSizeId,
                new Vector4(
                    1f / width,
                    1f / height,
                    DefaultShaderParameter.z,
                    DefaultShaderParameter.w));

            _dofWidthOverHeight = (float)width / height;
            _dofHeightBaseSize = width > height
                ? DOF_HEIGHT_BASE_SIZE_HORIZONTAL
                : DOF_HEIGHT_BASE_SIZE_VERTICAL;

            float inverseFocalSmoothness = SafeDivide(1f, focalDistance01 * param.DofSmoothness);
            float focalEnd = focalDistance01 + (param.DofFocalSize / depthRange) * 0.5f;
            // The bundled COCBGFG shader reuses focalEnd for BOTH sides. With
            // a wide focal band (live1001 frame2963: 30m), that blurs performers
            // inside the intended sharp interval. Supply the near edge separately;
            // leave the recovered background curve and blur/composite untouched.
            cmd.SetGlobalFloat(FocalStartId,
                Mathf.Max(0f, focalDistance01 - (param.DofFocalSize / depthRange) * 0.5f));
            LastDofDiagnosticsValid = true;
            LastDofUnityFrame = Time.frameCount;
            LastDofCameraId = camera.GetInstanceID();
            LastDofFocalDistance01 = focalDistance01;
            LastDofFocalEnd = focalEnd;
            LastDofDepthRange = depthRange;
            LastDofFocalDepth = focalDistance01 * depthRange;
            LastDofFocalPosition = param.DofFocalPosition;
            LastDofFocusCalculation = param.DofFocalTransfrom != null
                ? "transform"
                : param.DofFocalType == DepthBlurAndBloom.DofFocalType.Position
                    ? "position" : "point";
            if (Time.realtimeSinceStartup >= _nextDofParameterLogTime)
            {
                _nextDofParameterLogTime = Time.realtimeSinceStartup +
                    5f;
                // The normalised values alone are unreadable, so also report the
                // distances they stand for under this pass's own far-near
                // normalisation: where the frame is still sharp and where the
                // background CoC saturates. Comparing those two numbers with the
                // camera-to-performer distance is what tells us whether the focus
                // target is right, independent of the official shader internals.
                float focusMeters = focalDistance01 * depthRange;
                float sharpToMeters = focalEnd * depthRange;
                float rampMeters = focalDistance01 * param.DofSmoothness * depthRange;
                Debug.Log($"[RecoveredPostEffect] dof-param camera={camera.name} " +
                          $"target={(param.DofFocalTransfrom != null ? param.DofFocalTransfrom.name : param.DofFocalType == DepthBlurAndBloom.DofFocalType.Position ? "position" : "point") } " +
                          $"depth01={focalDistance01:0.######} focalEnd={focalEnd:0.######} " +
                          $"focus={focusMeters:0.##}m sharpTo={sharpToMeters:0.##}m " +
                          $"blurAt={(sharpToMeters + rampMeters):0.##}m " +
                          $"near={near:0.###} far={camera.farClipPlane:0.#} " +
                          $"size={param.DofFocalSize:0.###} spread={param.DofMaxBlurSpread:0.###} " +
                          $"smooth={param.DofSmoothness:0.###} quality={param.DofQualityType} " +
                          $"blur={param.DofBlurType}");
            }

            int curveParamsId = ShaderManager.GetPropertyId(ShaderManager.PropertyId._CurveParams);
            cmd.SetGlobalVector(
                curveParamsId,
                new Vector4(
                    inverseFocalSmoothness,
                    inverseFocalSmoothness,
                    focalEnd,
                    DefaultShaderParameter.w));

            int bloomDofWeightId = ShaderManager.GetPropertyId(ShaderManager.PropertyId._bloomDofWeight);
            cmd.SetGlobalFloat(bloomDofWeightId, param.BloomDofWeight);

            _rezworkWidth = (int)(width * 0.5f);
            _rezworkHeight = (int)(height * 0.5f);

            if (IsNeedChangeBlurResolutionValue())
            {
                bool isVertical = UnityEngine.Screen.height > UnityEngine.Screen.width;
                if (isVertical)
                {
                    _rezworkWidth = 360;
                    _rezworkHeight = 640;
                }
                else
                {
                    _rezworkWidth = 640;
                    _rezworkHeight = 360;
                }
            }
        }

        private bool IsNeedChangeBlurResolutionValue()
        {
            return SpecialQualityMode3Resolver != null && SpecialQualityMode3Resolver();
        }

        private float FocalDistance01(ref Parameter param, float worldDist)
        {
            Camera camera = param.TargetCamera;
            if (camera == null || camera.transform == null)
                throw new InvalidOperationException("DOF target camera or its Transform is null.");

            float distanceFromCamera = worldDist - camera.nearClipPlane;
            Vector3 worldPosition = camera.transform.position + camera.transform.forward * distanceFromCamera;
            float viewportZ = camera.WorldToViewportPoint(worldPosition).z;
            return viewportZ / (camera.farClipPlane - camera.nearClipPlane);
        }

        private float GetLowResolutionDividerBasedOnQuality(float baseDivider)
        {
            return IsNeedChangeBlurResolutionValue() ? baseDivider * 2f : baseDivider;
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) >= 0.000001f ? value / divisor : 0f;
        }

        private void OnRenderImageOldDof(
            ref Parameter param,
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            RenderTextureHandle source,
            RenderTextureHandle destination)
        {
            Camera camera = param.TargetCamera;
            if (camera == null)
                throw new InvalidOperationException("DofDiffusionBloomOverlayPass.Parameter.TargetCamera is null.");

            float smoothness = Mathf.Max(param.DofSmoothness, 0.1f);
            float focalStartCurve = param.OldDofFocalZStartCurve;
            float focalEndCurve = param.OldDofFocalZEndCurve;
            float focal01;

            if (param.DofFocalTransfrom != null)
            {
                focal01 =
                    camera.WorldToViewportPoint(param.DofFocalTransfrom.position).z /
                    camera.farClipPlane;
            }
            else if (param.IsSimpleTweakMode)
            {
                focal01 = FocalDistance01(ref param, param.DofFocalPoint);
            }
            else
            {
                focal01 = FocalDistance01(ref param, param.OldDofFocalZDistance);
            }

            if (param.IsSimpleTweakMode)
            {
                focalStartCurve = focal01 * smoothness;
                focalEndCurve = focal01 * smoothness;
            }

            float nearClipOffset =
                OldDofNearClipOffsetResolver != null
                    ? OldDofNearClipOffsetResolver()
                    : 0f;

            bool foregroundBlur =
                (int)param.DofQualityType > 1 &&
                param.DofFocalPoint > camera.nearClipPlane + nearClipOffset;

            int sourceWidth = GetHandleWidth(source);
            int sourceHeight = GetHandleHeight(source);
            if (sourceWidth <= 0 || sourceHeight <= 0)
                throw new InvalidOperationException("Invalid OldDof source size.");

            _dofWidthOverHeight = (float)sourceWidth / sourceHeight;
            _dofHeightBaseSize =
                sourceWidth <= sourceHeight
                    ? DOF_HEIGHT_BASE_SIZE_VERTICAL
                    : DOF_HEIGHT_BASE_SIZE_HORIZONTAL;

            cmd.SetGlobalFloat(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._ForegroundBlurExtrude),
                param.OldDofForegroundBlurExtrude);

            if (param.IsSimpleTweakMode)
            {
                focalStartCurve = 1f / focalStartCurve;
                focalEndCurve = 1f / focalEndCurve;
            }

            float normalizedFocalSize =
                param.DofFocalSize /
                (camera.farClipPlane - camera.nearClipPlane);

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._CurveParams),
                new Vector4(
                    focalStartCurve,
                    focalEndCurve,
                    normalizedFocalSize * 0.5f,
                    focal01));

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._InvRenderTargetSize),
                new Vector4(
                    1f / sourceWidth,
                    1f / sourceHeight,
                    0f,
                    0f));

            int halfWidth = (int)(sourceWidth * 0.5f);
            int halfHeight = (int)(sourceHeight * 0.5f);

            RenderTextureHandle foreground =
                RenderTextureHandle.Make(
                    FOREGROUND_RT_NAME,
                    sourceWidth,
                    sourceHeight);

            RenderTextureHandle mediumRezWork =
                RenderTextureHandle.Make(
                    MEDIUMREZWORK_RT_NAME,
                    halfWidth,
                    halfHeight);

            RenderTextureHandle finalDefocus =
                RenderTextureHandle.Make(
                    FINALDEFOCUS_RT_NAME,
                    halfWidth,
                    halfHeight);

            RenderTextureHandle lowRezWork =
                RenderTextureHandle.Make(
                    LOWREZOWORK_RT0_NAME,
                    halfWidth,
                    halfHeight);

            if (foregroundBlur)
                Allocate(cmd, foreground, sourceWidth, sourceHeight, FilterMode.Bilinear);

            Allocate(cmd, mediumRezWork, halfWidth, halfHeight, FilterMode.Bilinear);
            Allocate(cmd, finalDefocus, halfWidth, halfHeight, FilterMode.Bilinear);
            Allocate(cmd, lowRezWork, halfWidth, halfHeight, FilterMode.Bilinear);

            Blit(cmd, source, source, _oldDofMaterial, 3);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            OldDofDownsample(ref context, cmd, source, mediumRezWork);
            OldDofBlurFg(
                ref context,
                cmd,
                mediumRezWork,
                mediumRezWork,
                4,
                param.DofMaxBlurSpread);

            OldDofDownsample(ref context, cmd, mediumRezWork, lowRezWork);
            OldDofBlurFg(
                ref context,
                cmd,
                lowRezWork,
                lowRezWork,
                0,
                param.DofMaxBlurSpread);

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._TapLow),
                GetHandleIdentifier(lowRezWork));

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._TapMedium),
                GetHandleIdentifier(mediumRezWork));

            Blit(cmd, source, finalDefocus, _oldDofBlurMaterial, 3);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._TapLowBackground),
                GetHandleIdentifier(finalDefocus));

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._TapMedium),
                GetHandleIdentifier(mediumRezWork));

            int finalPass = 0;

            if (foregroundBlur)
            {
                Blit(cmd, source, foreground, _oldDofMaterial, 0);
                Blit(cmd, foreground, source, _oldDofMaterial, 5);

                OldDofDownsample(ref context, cmd, source, mediumRezWork);

                OldDofBlurFg(
                    ref context,
                    cmd,
                    mediumRezWork,
                    mediumRezWork,
                    2,
                    param.DofMaxBlurSpread);

                OldDofBlurFg(
                    ref context,
                    cmd,
                    mediumRezWork,
                    lowRezWork,
                    1,
                    param.DofMaxBlurSpread);

                Blit(cmd, lowRezWork, finalDefocus, null, -1);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                cmd.SetGlobalTexture(
                    ShaderManager.GetPropertyId(ShaderManager.PropertyId._TapLowBackground),
                    GetHandleIdentifier(finalDefocus));

                finalPass = 4;
            }

            Blit(cmd, source, destination, _oldDofMaterial, finalPass);

            Release(cmd, lowRezWork);
            Release(cmd, finalDefocus);
            Release(cmd, mediumRezWork);

            if (foregroundBlur)
                Release(cmd, foreground);
        }

        private void OldDofDownsample(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            RenderTextureHandle from,
            RenderTextureHandle to)
        {
            int width = GetHandleWidth(to);
            int height = GetHandleHeight(to);

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._InvRenderTargetSize),
                new Vector4(
                    1f / width,
                    1f / height,
                    0f,
                    0f));

            Blit(cmd, from, to, _oldDofMaterial, 6);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void OldDofBlurFg(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            RenderTextureHandle from,
            RenderTextureHandle to,
            int blurPass,
            float spread)
        {
            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._TapHigh),
                GetHandleIdentifier(from));

            int width = GetHandleWidth(to);
            int height = GetHandleHeight(to);

            RenderTextureHandle temp =
                RenderTextureHandle.Make(
                    TEMP_OLDBLUR_FG_RT_NAME,
                    width,
                    height);

            Allocate(cmd, temp, width, height, FilterMode.Bilinear);

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId.offsets),
                new Vector4(
                    0f,
                    spread * DOFONEOVERBASESIZE,
                    0f,
                    0f));

            Blit(cmd, from, temp, _oldDofBlurMaterial, blurPass);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId.offsets),
                new Vector4(
                    spread / _dofWidthOverHeight * DOFONEOVERBASESIZE,
                    0f,
                    0f,
                    0f));

            // Official 0x7FFDABBAC3CA-0x7FFDABBAC413 behavior:
            // the second pass again blits from -> TEMP_OLDBLUR_FG_RT.
            // The 'to' handle is used only to determine the temporary RT size.
            Blit(cmd, from, temp, _oldDofBlurMaterial, blurPass);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            Release(cmd, temp);
        }

        public void OnRenderImageOverlay(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source,
            RenderTextureHandle destination)
        {
            if (!InvokePostFilmBlit(
                    context,
                    cmd,
                    source,
                    destination,
                    _blitMaterial,
                    ref param.Overlay1,
                    ref param.Overlay2,
                    ref param.Overlay3,
                    PASS_POSTBLIT_DIMMERONLY,
                    PASS_POSTBLIT_OVERLAY1,
                    PASS_POSTBLIT_OVERLAY2))
            {
                Blit(cmd, source, destination, _blitMaterial, PASS_POSTBLIT_DIMMERONLY);
            }
        }

        private RenderTextureHandle CreateBloomTexture(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source)
        {
            RenderTextureHandle invalid = RenderTextureHandle.Make(
                INVALID_DOWNSAMPLE_TEXTURE_NAMEID,
                0,
                0);

            return CreateBloomTexture(ref context, cmd, ref param, source, invalid);
        }

        private RenderTextureHandle CreateBloomTexture(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source,
            RenderTextureHandle downSample)
        {
            int sourceWidth = GetHandleWidth(source);
            int sourceHeight = GetHandleHeight(source);
            int width = sourceWidth / BLOOM_DIVIDER;
            int height = sourceHeight / BLOOM_DIVIDER;

            if (IsNeedChangeBlurResolutionValue())
            {
                int downWidth = GetHandleWidth(downSample);
                int downHeight = GetHandleHeight(downSample);
                if (downWidth > 0 && downHeight > 0)
                {
                    width = downWidth / 2;
                    height = downHeight / 2;
                }
                else
                {
                    width /= 2;
                    height /= 2;
                }
            }

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            int parameterId = ShaderManager.GetPropertyId(ShaderManager.PropertyId._Parameter);

            if (param.BloomBlurSize <= 0f)
            {
                cmd.SetGlobalVector(
                    parameterId,
                    new Vector4(
                        DefaultShaderParameter.x,
                        DefaultShaderParameter.y,
                        param.BloomThreshold,
                        param.BloomIntensity));

                RenderTextureHandle result = RenderTextureHandle.Make(
                    TEMP_DOWNSAMPLE_RT2_NAME,
                    width,
                    height);

                Allocate(cmd, result, width, height);
                Blit(cmd, source, result, _fastBloomMaterial, PASS_FASTBLOOM_DOWNSAMPLE);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                return result;
            }

            bool createdDownSample = false;
            if (GetHandleNameId(downSample) == INVALID_DOWNSAMPLE_TEXTURE_NAMEID)
            {
                cmd.SetGlobalVector(
                    parameterId,
                    new Vector4(
                        DefaultShaderParameter.x,
                        DefaultShaderParameter.y,
                        0f,
                        1f));

                downSample = RenderTextureHandle.Make(
                    TEMP_DOWNSAMPLE_RT0_NAME,
                    width,
                    height);

                Allocate(cmd, downSample, width, height);
                Blit(cmd, source, downSample, _fastBloomMaterial, PASS_FASTBLOOM_DOWNSAMPLE);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                createdDownSample = true;
            }

            float aspect = sourceHeight > 0 ? (float)sourceWidth / sourceHeight : 1f;
            float verticalSpread = param.BloomBlurSize * DOFONEOVERBASESIZE;
            float horizontalSpread = SafeDivide(param.BloomBlurSize, aspect) * DOFONEOVERBASESIZE;

            cmd.SetGlobalVector(
                parameterId,
                new Vector4(
                    horizontalSpread,
                    verticalSpread,
                    param.BloomThreshold,
                    param.BloomIntensity));

            RenderTextureHandle verticalBlur = RenderTextureHandle.Make(
                TEMP_DOWNSAMPLE_RT1_NAME,
                width,
                height);
            Allocate(cmd, verticalBlur, width, height);
            Blit(cmd, downSample, verticalBlur, _fastBloomMaterial, PASS_FASTBLOOM_VERTICALBLUR);

            RenderTextureHandle finalBloom = RenderTextureHandle.Make(
                TEMP_DOWNSAMPLE_RT2_NAME,
                width,
                height);
            Allocate(cmd, finalBloom, width, height);
            Blit(cmd, verticalBlur, finalBloom, _fastBloomMaterial, PASS_FASTBLOOM_HORIZONTALBLUR);

            Release(cmd, verticalBlur);
            if (createdDownSample)
                Release(cmd, downSample);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            return finalBloom;
        }

        private void OnRenderImageOldFastBloom(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source,
            RenderTextureHandle destination)
        {
            cmd.SetGlobalFloat(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._bloomDofWeight),
                param.BloomDofWeight);

            RenderTextureHandle bloomTexture = CreateBloomTexture(
                ref context,
                cmd,
                ref param,
                source);

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Bloom),
                GetHandleIdentifier(bloomTexture));

            SetBloomScreenBlend(cmd, param.BloomBlendMode);

            // Official OldFastBloom path does not call ScreenOverlayRender.PostFilmBlit.
            Blit(
                cmd,
                source,
                destination,
                _bloomMaterial,
                PASS_POSTBLOOM_BLOOM);

            Release(cmd, bloomTexture);
        }

        private void OnRenderImageFastBloom(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source,
            RenderTextureHandle destination)
        {
            int bloomDofWeightId = ShaderManager.GetPropertyId(ShaderManager.PropertyId._bloomDofWeight);
            cmd.SetGlobalFloat(bloomDofWeightId, param.BloomDofWeight);

            RenderTextureHandle bloom = CreateBloomTexture(ref context, cmd, ref param, source);
            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Bloom),
                GetHandleIdentifier(bloom));
            SetBloomScreenBlend(cmd, param.BloomBlendMode);

            if (!InvokePostFilmBlit(
                    context,
                    cmd,
                    source,
                    destination,
                    _bloomMaterial,
                    ref param.Overlay1,
                    ref param.Overlay2,
                    ref param.Overlay3,
                    PASS_POSTBLOOM_BLOOM,
                    PASS_POSTBLOOM_OVERLAY1,
                    PASS_POSTBLOOM_OVERLAY2))
            {
                Blit(cmd, source, destination, _bloomMaterial, PASS_POSTBLOOM_BLOOM);
            }

            Release(cmd, bloom);
        }

        private void BlurBltHorizon(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            RenderTextureHandle from,
            RenderTextureHandle to,
            float spread)
        {
            Vector4 offsets = DefaultShaderParameter;
            offsets.x =
                spread /
                _dofWidthOverHeight *
                DOFONEOVERBASESIZE;

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId.offsets),
                offsets);

            Blit(cmd, from, to, _weightedBlurMaterial, PASS_WEIGHTED_BLUR);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void BlurBltMixed(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            RenderTextureHandle from,
            RenderTextureHandle to,
            float spread)
        {
            int width = GetHandleWidth(to);
            int height = GetHandleHeight(to);

            RenderTextureHandle temp =
                RenderTextureHandle.Make(
                    TEMP_BLUR_RT0_NAME,
                    width,
                    height);

            Allocate(cmd, temp, width, height, FilterMode.Bilinear);

            Vector4 offsets = DefaultShaderParameter;
            offsets.x =
                spread /
                _dofWidthOverHeight *
                DOFONEOVERBASESIZE;
            offsets.y = 0f;

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId.offsets),
                offsets);

            Blit(cmd, from, temp, _weightedBlurMaterial, PASS_WEIGHTED_BLUR2);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            offsets = DefaultShaderParameter;
            offsets.x = 0f;
            offsets.y = spread * _dofHeightBaseSize;

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId.offsets),
                offsets);

            Blit(cmd, temp, to, _weightedBlurMaterial, PASS_WEIGHTED_BLUR);

            Release(cmd, temp);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private float CalculateMaxCoc(float spread, float screenHeight)
        {
            return Mathf.Min(
                MAX_COC_LIMIT,
                ((spread / MAX_COC_DIVISOR) *
                 MAX_COC_MULTIPLIER +
                 MAX_COC_BIAS) /
                screenHeight);
        }

        private void BlurBltDisc(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            RenderTextureHandle from,
            RenderTextureHandle to,
            float spread,
            int shift = 1)
        {
            int targetWidth = GetHandleWidth(to);
            int targetHeight = GetHandleHeight(to);

            float aspect =
                (float)targetWidth /
                targetHeight;

            int tempWidth = targetWidth >> shift;
            int tempHeight = targetHeight >> shift;

            RenderTextureHandle temp =
                RenderTextureHandle.Make(
                    TEMP_BLUR_RT0_NAME,
                    tempWidth,
                    tempHeight);

            Allocate(cmd, temp, tempWidth, tempHeight, FilterMode.Bilinear);

            cmd.SetGlobalFloat(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._MaxCoC),
                CalculateMaxCoc(spread, targetHeight));

            cmd.SetGlobalFloat(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Aspect),
                1f / aspect);

            Blit(cmd, from, temp, _weightedBlurMaterial, PASS_DISC_BLUR);
            Blit(cmd, temp, to, _weightedBlurMaterial, PASS_DISC_BLUR);

            Release(cmd, temp);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void BlurBltBallBlur(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle from,
            RenderTextureHandle to,
            float spread)
        {
            if (param.BallBlurBrightnessIntensity < 0f)
            {
                BlurBltDisc(
                    ref context,
                    cmd,
                    from,
                    to,
                    spread,
                    1);
                return;
            }

            Vector4 parameter = DefaultShaderParameter;
            parameter.x = param.BallBlurPowerFactor;
            parameter.y = param.BallBlurBrightnessThreshhold;
            parameter.z = param.BallBlurBrightnessIntensity;

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Parameter),
                parameter);

            int targetWidth = GetHandleWidth(to);
            int targetHeight = GetHandleHeight(to);

            RenderTextureHandle ballBlur0 =
                RenderTextureHandle.Make(
                    TEMP_BALLBLUR_RT0_NAME,
                    targetWidth >> 1,
                    targetHeight >> 1);

            Allocate(
                cmd,
                ballBlur0,
                targetWidth >> 1,
                targetHeight >> 1,
                param.IsPointBallBlur
                    ? FilterMode.Point
                    : FilterMode.Bilinear);

            Blit(
                cmd,
                from,
                ballBlur0,
                _weightedBlurMaterial,
                PASS_BALL_BLUR_EXTRACTION);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            RenderTextureHandle ballBlur1 =
                RenderTextureHandle.Make(
                    TEMP_BALLBLUR_RT1_NAME,
                    targetWidth,
                    targetHeight);

            Allocate(
                cmd,
                ballBlur1,
                targetWidth,
                targetHeight,
                FilterMode.Bilinear);

            float aspect =
                (float)targetWidth /
                targetHeight;

            cmd.SetGlobalFloat(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._MaxCoC),
                CalculateMaxCoc(
                    param.BallBlurSpread,
                    targetHeight));

            cmd.SetGlobalFloat(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Aspect),
                1f / aspect);

            Blit(
                cmd,
                ballBlur0,
                ballBlur1,
                _weightedBlurMaterial,
                PASS_BALL_BLUR);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            RenderTextureHandle ballBlur2 =
                RenderTextureHandle.Make(
                    TEMP_BALLBLUR_RT2_NAME,
                    targetWidth,
                    targetHeight);

            Allocate(
                cmd,
                ballBlur2,
                targetWidth,
                targetHeight,
                FilterMode.Bilinear);

            BlurBltDisc(
                ref context,
                cmd,
                from,
                ballBlur2,
                spread,
                1);

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._MaskTex),
                GetHandleIdentifier(from));

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._EmissiveTex),
                GetHandleIdentifier(ballBlur1));

            Blit(
                cmd,
                ballBlur2,
                to,
                _weightedBlurMaterial,
                PASS_BALL_BLUR_COMBINE);

            Release(cmd, ballBlur0);
            Release(cmd, ballBlur1);
            Release(cmd, ballBlur2);
        }

        private void BlurBlt(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle from,
            RenderTextureHandle to,
            float spread)
        {
            switch ((int)param.DofBlurType)
            {
                case 0:
                    BlurBltHorizon(
                        ref context,
                        cmd,
                        from,
                        to,
                        spread);
                    break;

                case 1:
                    BlurBltMixed(
                        ref context,
                        cmd,
                        from,
                        to,
                        spread);
                    break;

                case 2:
                    BlurBltDisc(
                        ref context,
                        cmd,
                        from,
                        to,
                        spread,
                        1);
                    break;

                case 3:
                    BlurBltBallBlur(
                        ref context,
                        cmd,
                        ref param,
                        from,
                        to,
                        spread);
                    break;
            }

            // Official BlurBlt performs one final unconditional submission.
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void SetBlurParameter(CommandBuffer cmd, float spread, float axisX, float axisY)
        {
            float x = spread * _dofHeightBaseSize * axisX;
            float y = spread * _dofHeightBaseSize * axisY;
            if (_dofWidthOverHeight > 0.000001f)
                x /= _dofWidthOverHeight;

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Parameter),
                new Vector4(x, y, 0f, 0f));
        }

        private void OnRenderImageDofBloom(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source,
            RenderTextureHandle destination)
        {
            PrepareDofParam(ref context, cmd, ref param, source);

            int sourceWidth = GetHandleWidth(source);
            int sourceHeight = GetHandleHeight(source);

            RenderTextureHandle dofBloomTexture = RenderTextureHandle.Make(
                TEMP_DOFBLOOM_RT_NAME,
                sourceWidth,
                sourceHeight);

            RenderTextureHandle downSampleTexture = RenderTextureHandle.Make(
                TEMP_DOWNSAMPLE_RT0_NAME,
                _rezworkWidth,
                _rezworkHeight);

            RenderTextureHandle lowRezWorkTexture = RenderTextureHandle.Make(
                LOWREZOWORK_RT0_NAME,
                _rezworkWidth,
                _rezworkHeight);

            Allocate(cmd, dofBloomTexture, sourceWidth, sourceHeight);
            Allocate(cmd, downSampleTexture, _rezworkWidth, _rezworkHeight);
            Allocate(cmd, lowRezWorkTexture, _rezworkWidth, _rezworkHeight);

            int cocPass = PASS_POSTDOFBLOOM_COCBG_RICH;
            if ((int)param.DofQualityType == 5)
            {
                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(ShaderManager.PropertyId._dofForegroundSize),
                    param.DofForegroundSize);
                cocPass = PASS_POSTDOFBLOOM_COCBGFG;
            }

            BlitDofCoc(cmd, ref param, source, dofBloomTexture, _dofBloomMaterial, cocPass);
            Blit(cmd, dofBloomTexture, downSampleTexture, _dofBloomMaterial, PASS_POSTDOFBLOOM_DOWNSAMPLE);

            BlurBlt(
                ref context,
                cmd,
                ref param,
                downSampleTexture,
                lowRezWorkTexture,
                param.DofMaxBlurSpread);

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._TapLowBackground),
                GetHandleIdentifier(lowRezWorkTexture));

            RenderTextureHandle bloomTexture = CreateBloomTexture(
                ref context,
                cmd,
                ref param,
                dofBloomTexture,
                downSampleTexture);

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Bloom),
                GetHandleIdentifier(bloomTexture));
            SetBloomScreenBlend(cmd, param.BloomBlendMode);

            if (!InvokePostFilmBlit(
                    context,
                    cmd,
                    dofBloomTexture,
                    destination,
                    _dofBloomMaterial,
                    ref param.Overlay1,
                    ref param.Overlay2,
                    ref param.Overlay3,
                    PASS_POSTDOFBLOOM_DOFBLOOM,
                    PASS_POSTDOFBLOOM_OVERLAY1,
                    PASS_POSTDOFBLOOM_OVERLAY2))
            {
                Blit(cmd, dofBloomTexture, destination, _dofBloomMaterial, PASS_POSTDOFBLOOM_DOFBLOOM);
            }

            Release(cmd, bloomTexture);
            if (!HandlesEqual(dofBloomTexture, source))
                Release(cmd, dofBloomTexture);
            Release(cmd, lowRezWorkTexture);
            Release(cmd, downSampleTexture);
        }

        private void OnRenderImageDof(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source,
            RenderTextureHandle destination)
        {
            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Bloom),
                Texture2D.blackTexture);

            PrepareDofParam(
                ref context,
                cmd,
                ref param,
                source);

            int sourceWidth = GetHandleWidth(source);
            int sourceHeight = GetHandleHeight(source);

            RenderTextureHandle dofTexture =
                RenderTextureHandle.Make(
                    TEMP_DOF_RT_NAME,
                    sourceWidth,
                    sourceHeight);

            RenderTextureHandle lowRezWork =
                RenderTextureHandle.Make(
                    LOWREZOWORK_RT0_NAME,
                    _rezworkWidth,
                    _rezworkHeight);

            Allocate(
                cmd,
                dofTexture,
                sourceWidth,
                sourceHeight,
                FilterMode.Bilinear);

            Allocate(
                cmd,
                lowRezWork,
                _rezworkWidth,
                _rezworkHeight,
                FilterMode.Bilinear);

            int cocPass = PASS_POSTDOFBLOOM_COCBG_RICH;

            if ((int)param.DofQualityType == 5)
            {
                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(ShaderManager.PropertyId._dofForegroundSize),
                    param.DofForegroundSize);

                cocPass = PASS_POSTDOFBLOOM_COCBGFG;
            }

            BlitDofCoc(cmd, ref param, source, dofTexture, _dofBloomMaterial, cocPass);

            RenderTextureHandle downSample =
                RenderTextureHandle.Make(
                    TEMP_DOWNSAMPLE_RT0_NAME,
                    _rezworkWidth,
                    _rezworkHeight);

            Allocate(
                cmd,
                downSample,
                _rezworkWidth,
                _rezworkHeight,
                FilterMode.Bilinear);

            Blit(
                cmd,
                dofTexture,
                downSample,
                _dofBloomMaterial,
                PASS_POSTDOFBLOOM_DOWNSAMPLE);

            BlurBlt(
                ref context,
                cmd,
                ref param,
                downSample,
                lowRezWork,
                param.DofMaxBlurSpread);

            Release(cmd, downSample);

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._TapLowBackground),
                GetHandleIdentifier(lowRezWork));

            SetBloomScreenBlend(cmd, param.BloomBlendMode);

            if (!InvokePostFilmBlit(
                    context,
                    cmd,
                    dofTexture,
                    destination,
                    _dofBloomMaterial,
                    ref param.Overlay1,
                    ref param.Overlay2,
                    ref param.Overlay3,
                    0,
                    PASS_POSTDOFBLOOM_OVERLAY1,
                    PASS_POSTDOFBLOOM_OVERLAY2))
            {
                Blit(cmd, dofTexture, destination, _dofBloomMaterial, 0);
            }

            if (!HandlesEqual(dofTexture, source))
                Release(cmd, dofTexture);

            Release(cmd, lowRezWork);
        }

        private void OnRenderImageDiffusionFastBloom(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source,
            RenderTextureHandle destination)
        {
            cmd.SetGlobalFloat(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._bloomDofWeight),
                param.BloomDofWeight);

            Vector4 defaultParameter = DefaultShaderParameter;

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Parameter),
                new Vector4(
                    param.BloomBlurSize * 0.5f,
                    defaultParameter.y,
                    param.BloomThreshold,
                    param.BloomIntensity));

            int sourceWidth = GetHandleWidth(source);
            int sourceHeight = GetHandleHeight(source);
            int quarterWidth = sourceWidth / BLOOM_DIVIDER;
            int quarterHeight = sourceHeight / BLOOM_DIVIDER;

            RenderTextureHandle lowRezWork0 =
                RenderTextureHandle.Make(
                    LOWREZOWORK_RT0_NAME,
                    quarterWidth,
                    quarterHeight);

            RenderTextureHandle lowRezWork1 =
                RenderTextureHandle.Make(
                    LOWREZOWORK_RT1_NAME,
                    quarterWidth,
                    quarterHeight);

            Allocate(
                cmd,
                lowRezWork0,
                quarterWidth,
                quarterHeight,
                FilterMode.Bilinear);

            Allocate(
                cmd,
                lowRezWork1,
                quarterWidth,
                quarterHeight,
                FilterMode.Bilinear);

            int halfWidth = (int)(sourceWidth * 0.5f);
            int halfHeight = (int)(sourceHeight * 0.5f);

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._PixelSize),
                new Vector4(
                    param.DiffusionBlurSize * 0.5f / halfWidth,
                    param.DiffusionBlurSize * 0.5f / halfHeight,
                    defaultParameter.z,
                    defaultParameter.w));

            Blit(
                cmd,
                source,
                lowRezWork0,
                _diffusionBloomMaterial,
                0);

            Blit(
                cmd,
                lowRezWork0,
                lowRezWork1,
                _diffusionBloomMaterial,
                1);

            RenderTextureHandle bloomTexture =
                CreateBloomTexture(
                    ref context,
                    cmd,
                    ref param,
                    source);

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._ColorParam),
                new Vector4(
                    param.DiffusionBright,
                    param.DiffusionSaturation,
                    param.DiffusionContrast,
                    param.DiffusionThreshold));

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._Bloom),
                GetHandleIdentifier(bloomTexture));

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._RgbTex),
                GetHandleIdentifier(lowRezWork1));

            SetBloomScreenBlend(cmd, param.BloomBlendMode);

            if (!InvokePostFilmBlit(
                    context,
                    cmd,
                    source,
                    destination,
                    _diffusionBloomMaterial,
                    ref param.Overlay1,
                    ref param.Overlay2,
                    ref param.Overlay3,
                    2,
                    3,
                    5))
            {
                Blit(cmd, source, destination, _diffusionBloomMaterial, 2);
            }

            Release(cmd, lowRezWork1);
            Release(cmd, lowRezWork0);
            Release(cmd, bloomTexture);
        }

        private void DiffusionFilterProcess(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source,
            RenderTextureHandle destination)
        {
            RenderTextureHandle diffusionFilter =
                RenderTextureHandle.Make(
                    TEMP_DIFFUSIONFILTER_RT_NAME,
                    _rezworkWidth,
                    _rezworkHeight);

            Allocate(
                cmd,
                diffusionFilter,
                _rezworkWidth,
                _rezworkHeight,
                FilterMode.Bilinear);

            Vector4 defaultParameter = DefaultShaderParameter;

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._PixelSize),
                new Vector4(
                    param.DiffusionBlurSize * 0.5f / _rezworkWidth,
                    param.DiffusionBlurSize * 0.5f / _rezworkHeight,
                    defaultParameter.z,
                    defaultParameter.w));

            cmd.SetGlobalVector(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._ColorParam),
                new Vector4(
                    param.DiffusionBright,
                    param.DiffusionSaturation,
                    param.DiffusionContrast,
                    param.DiffusionThreshold));

            Blit(
                cmd,
                source,
                diffusionFilter,
                _diffusionDofBloomMaterial,
                6);

            Blit(
                cmd,
                diffusionFilter,
                destination,
                _diffusionDofBloomMaterial,
                7);

            Release(cmd, diffusionFilter);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void OnRenderImageDiffusionDofBloom(
            ref ScriptableRenderContext context,
            CommandBuffer cmd,
            ref Parameter param,
            RenderTextureHandle source,
            RenderTextureHandle destination)
        {
            PrepareDofParam(
                ref context,
                cmd,
                ref param,
                source);

            int sourceWidth = GetHandleWidth(source);
            int sourceHeight = GetHandleHeight(source);

            RenderTextureHandle diffusionDofBloom =
                RenderTextureHandle.Make(
                    TEMP_DIFFUSIONDOFBLOOM_RT_NAME,
                    sourceWidth,
                    sourceHeight);

            RenderTextureHandle lowRezWork0 =
                RenderTextureHandle.Make(
                    LOWREZOWORK_RT0_NAME,
                    _rezworkWidth,
                    _rezworkHeight);

            RenderTextureHandle lowRezWork1 =
                RenderTextureHandle.Make(
                    LOWREZOWORK_RT1_NAME,
                    _rezworkWidth,
                    _rezworkHeight);

            RenderTextureHandle downSample =
                RenderTextureHandle.Make(
                    TEMP_DOWNSAMPLE_RT0_NAME,
                    _rezworkWidth,
                    _rezworkHeight);

            Allocate(
                cmd,
                diffusionDofBloom,
                sourceWidth,
                sourceHeight,
                FilterMode.Bilinear);

            Allocate(
                cmd,
                lowRezWork0,
                _rezworkWidth,
                _rezworkHeight,
                FilterMode.Bilinear);

            Allocate(
                cmd,
                lowRezWork1,
                _rezworkWidth,
                _rezworkHeight,
                FilterMode.Bilinear);

            Allocate(
                cmd,
                downSample,
                _rezworkWidth,
                _rezworkHeight,
                FilterMode.Bilinear);

            int cocPass = 9;

            if ((int)param.DofQualityType == 5)
            {
                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(ShaderManager.PropertyId._dofForegroundSize),
                    param.DofForegroundSize);

                cocPass = 10;
            }

            BlitDofCoc(cmd, ref param, source, diffusionDofBloom, _diffusionDofBloomMaterial, cocPass);

            Blit(
                cmd,
                diffusionDofBloom,
                downSample,
                _diffusionDofBloomMaterial,
                3);

            BlurBlt(
                ref context,
                cmd,
                ref param,
                downSample,
                lowRezWork0,
                param.DofMaxBlurSpread);

            DiffusionFilterProcess(
                ref context,
                cmd,
                ref param,
                lowRezWork0,
                lowRezWork1);

            cmd.SetGlobalTexture(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._TapLowBackground),
                GetHandleIdentifier(lowRezWork1));

            RenderTextureHandle bloomTexture =
                RenderTextureHandle.Make(
                    INVALID_DOWNSAMPLE_TEXTURE_NAMEID,
                    0,
                    0);

            int basePass;

            if (!param.IsEnableBloom)
            {
                SetBloomScreenBlend(cmd, param.BloomBlendMode);
                basePass = -1;
            }
            else
            {
                bloomTexture =
                    CreateBloomTexture(
                        ref context,
                        cmd,
                        ref param,
                        diffusionDofBloom,
                        downSample);

                cmd.SetGlobalTexture(
                    ShaderManager.GetPropertyId(ShaderManager.PropertyId._Bloom),
                    GetHandleIdentifier(bloomTexture));

                SetBloomScreenBlend(cmd, param.BloomBlendMode);
                basePass = 8;
            }

            if (!InvokePostFilmBlit(
                    context,
                    cmd,
                    diffusionDofBloom,
                    destination,
                    _diffusionDofBloomMaterial,
                    ref param.Overlay1,
                    ref param.Overlay2,
                    ref param.Overlay3,
                    basePass,
                    11,
                    13))
            {
                Blit(
                    cmd,
                    diffusionDofBloom,
                    destination,
                    _diffusionDofBloomMaterial,
                    basePass);
            }

            if (GetHandleNameId(bloomTexture) != INVALID_DOWNSAMPLE_TEXTURE_NAMEID)
                Release(cmd, bloomTexture);

            Release(cmd, diffusionDofBloom);
            Release(cmd, lowRezWork0);
            Release(cmd, lowRezWork1);
            Release(cmd, downSample);
        }

        private void SetBloomScreenBlend(
            CommandBuffer cmd,
            DofDiffusionBloomOverlayParam.BloomScreenBlendMode mode)
        {
            cmd.SetGlobalFloat(
                ShaderManager.GetPropertyId(ShaderManager.PropertyId._BloomIsScreenBlend),
                (int)mode == 0 ? 1f : 0f);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RestoreShader(_oldDofMaterial, ShaderManager.ShaderKinds.DepthOfField34);
            RestoreShader(_oldDofBlurMaterial, ShaderManager.ShaderKinds.SeparableWeightedBlurDof34);
            RestoreShader(_fastBloomMaterial, ShaderManager.ShaderKinds.FastBloom);
            RestoreShader(_dofBloomMaterial, ShaderManager.ShaderKinds.PostDofBloom_Rich);
            RestoreShader(_diffusionBloomMaterial, ShaderManager.ShaderKinds.PostDiffusionBloom_Rich);
            RestoreShader(_diffusionDofBloomMaterial, ShaderManager.ShaderKinds.PostDiffusionDofBloom_Rich);
            RestoreShader(_weightedBlurMaterial, ShaderManager.ShaderKinds.WeightedBlur);
            RestoreShader(_bloomMaterial, ShaderManager.ShaderKinds.PostBloom_Rich);
            RestoreShader(_blitMaterial, ShaderManager.ShaderKinds.PostBlit_Rich);

            if (_cameraData == null || _feature == null || _featureParameter == null)
                return;

            ref Parameter param = ref _featureParameter.DofDiffuionBloomOverlay;
            RenderTextureHandle source = _feature.SourceRT;
            int width = GetHandleWidth(source);
            int height = GetHandleHeight(source);
            if (width <= 0 || height <= 0)
                return;

            if (!_loggedExecute)
            {
                _loggedExecute = true;
#pragma warning disable 618
                RenderTargetIdentifier cameraColorTarget =
                    renderingData.cameraData.renderer != null &&
                    renderingData.cameraData.renderer.cameraColorTargetHandle != null
                        ? renderingData.cameraData.renderer.cameraColorTargetHandle.nameID
                        : default;
#pragma warning restore 618
                Debug.Log($"[RecoveredPostEffect] execute camera={renderingData.cameraData.camera?.name} " +
                          $"type={param.UseDofDiffusionBloomType} size={width}x{height} " +
                          $"bloom={param.IsEnableBloom} dof={param.IsEnableDof} diffusion={param.IsEnableDiffusion} " +
                          $"source={source.RtId} sourceNameId={source.NameId} cameraTarget={cameraColorTarget}");
            }

            CommandBuffer cmd = CommandBufferPool.Get(POOL_NAME);
            try
            {
                RenderTextureHandle destination = RenderTextureHandle.Make(
                    TEMP_RT0_NAME,
                    width,
                    height);
                Allocate(cmd, destination, width, height);

                cmd.SetGlobalTexture(
                    ShaderManager.GetPropertyId(ShaderManager.PropertyId._Bloom),
                    Texture2D.blackTexture);

                switch (param.UseDofDiffusionBloomType)
                {
                    case DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DofBloom:
                        OnRenderImageDofBloom(ref context, cmd, ref param, source, destination);
                        break;

                    case DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DiffusionDofBloom:
                        OnRenderImageDiffusionDofBloom(ref context, cmd, ref param, source, destination);
                        break;

                    case DofDiffusionBloomOverlayParam.DofDiffusionBloomType.Bloom:
                        OnRenderImageFastBloom(ref context, cmd, ref param, source, destination);
                        break;

                    case DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DiffusionBloom:
                        OnRenderImageDiffusionFastBloom(ref context, cmd, ref param, source, destination);
                        break;

                    case DofDiffusionBloomOverlayParam.DofDiffusionBloomType.Dof:
                        OnRenderImageDof(ref context, cmd, ref param, source, destination);
                        break;

                    case DofDiffusionBloomOverlayParam.DofDiffusionBloomType.OldDof:
                        OnRenderImageOldDof(ref param, ref context, cmd, source, destination);
                        break;

                    case DofDiffusionBloomOverlayParam.DofDiffusionBloomType.OldDofFastBloom:
                        OnRenderImageOldFastBloom(ref context, cmd, ref param, source, destination);
                        break;

                    case DofDiffusionBloomOverlayParam.DofDiffusionBloomType.OverlayOnly:
                        OnRenderImageOverlay(ref context, cmd, ref param, source, destination);
                        break;

                    default:
                        Blit(cmd, source, destination, null, -1);
                        break;
                }

                // Official behavior: passes at or before 550 hand the temporary RT to
                // PostImageEffectFeature; later passes write directly to camera color.
                if ((int)renderPassEvent <= 550)
                {
                    _feature.SetSourceRT(destination, cmd, ref renderingData);
                }
                else
                {
                    RTHandle cameraColorHandle =
                        renderingData.cameraData.renderer.cameraColorTargetHandle;
                    if (cameraColorHandle == null)
                    {
                        Release(cmd, destination);
                        return;
                    }

                    RenderTargetIdentifier cameraColorTarget = cameraColorHandle.nameID;
                    cmd.Blit(GetHandleIdentifier(destination), cameraColorTarget);
                    Release(cmd, destination);
                }

                context.ExecuteCommandBuffer(cmd);
                LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                    "DofDiffusionBloomOverlay", renderPassEvent, source,
                    (int)renderPassEvent <= 550 ? destination :
                        RenderTextureHandle.Make(renderingData.cameraData.renderer.cameraColorTargetHandle.nameID, width, height));
            }
            finally
            {
                CommandBufferPool.Release(cmd);
            }
        }

        private bool InvokePostFilmBlit(
            ScriptableRenderContext context,
            CommandBuffer cmd,
            RenderTextureHandle source,
            RenderTextureHandle destination,
            Material material,
            ref ScreenOverlayRender.Parameter overlay1,
            ref ScreenOverlayRender.Parameter overlay2,
            ref ScreenOverlayRender.Parameter overlay3,
            int basePass,
            int overlay1Pass,
            int overlay2Pass)
        {
            if (_overlayRender == null)
                return false;

            // The concrete renderer signature is available: preserve ref updates without
            // reflection, boxed parameters, or runtime overload discovery.
            try
            {
                _overlayRender.PostFilmBlit(context, cmd, source, destination, material,
                    ref overlay1, ref overlay2, ref overlay3, basePass, overlay1Pass, overlay2Pass);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[DofDiffusionBloomOverlayPass] PostFilmBlit failed: " + ex);
                return false;
            }
        }

        private static void Allocate(
            CommandBuffer cmd,
            RenderTextureHandle handle,
            int width,
            int height)
        {
            Allocate(
                cmd,
                handle,
                width,
                height,
                FilterMode.Bilinear);
        }

        private static void Allocate(
            CommandBuffer cmd,
            RenderTextureHandle handle,
            int width,
            int height,
            FilterMode filterMode)
        {
            int nameId = GetHandleNameId(handle);

            cmd.GetTemporaryRT(
                nameId,
                Mathf.Max(1, width),
                Mathf.Max(1, height),
                0,
                filterMode,
                RenderTextureFormat.ARGBHalf);
        }

        private static void Release(CommandBuffer cmd, RenderTextureHandle handle)
        {
            int nameId = GetHandleNameId(handle);
            if (nameId != INVALID_DOWNSAMPLE_TEXTURE_NAMEID)
                cmd.ReleaseTemporaryRT(nameId);
        }

        private void BlitDofCoc(CommandBuffer cmd, ref Parameter param,
            RenderTextureHandle source, RenderTextureHandle destination,
            Material originalMaterial, int originalPass)
        {
            if ((int)param.DofQualityType == 5)
            {
                if (_foregroundCocMaterial == null)
                {
                    var shader = Resources.Load<Shader>("RenderPipeline/LiveDofCoC");
                    if (shader != null && shader.isSupported)
                        _foregroundCocMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                if (_foregroundCocMaterial != null)
                {
                    Blit(cmd, source, destination, _foregroundCocMaterial, 0);
                    return;
                }
            }
            // Background-only mode retains the exact official pass.
            Blit(cmd, source, destination, originalMaterial, originalPass);
        }

        private static void Blit(
            CommandBuffer cmd,
            RenderTextureHandle source,
            RenderTextureHandle destination,
            Material material,
            int pass)
        {
            RenderTargetIdentifier src = GetHandleIdentifier(source);
            RenderTargetIdentifier dst = GetHandleIdentifier(destination);
            if (material == null || pass < 0)
            {
                cmd.Blit(src, dst);
                return;
            }

            // Unity accepts an out-of-range pass index but leaves the
            // destination undefined on some backends. That manifests as the
            // solid-color frame reported in Live. Preserve the source instead.
            if (pass >= material.passCount)
            {
                string shaderName = material.shader != null ? material.shader.name : "<null>";
                string warningKey = shaderName + "#" + pass;
                if (_invalidPassWarnings.Add(warningKey))
                {
                    Debug.LogError("[RecoveredPostEffect] invalid shader pass " + pass +
                                    " for " + shaderName + " (passCount=" +
                                    material.passCount + "); using passthrough");
                }
                cmd.Blit(src, dst);
                return;
            }

            cmd.Blit(src, dst, material, pass);
        }

        private static bool HandlesEqual(RenderTextureHandle left, RenderTextureHandle right)
        {
            return GetHandleNameId(left) == GetHandleNameId(right) &&
                   GetHandleIdentifier(left).Equals(GetHandleIdentifier(right));
        }

        private static int GetHandleNameId(RenderTextureHandle handle)
        {
            return ReadIntMember(handle, new[]
            {
                "NameID", "NameId", "nameID", "nameId", "_nameID", "_nameId",
                "RenderTextureNameID", "renderTextureNameID"
            }, INVALID_DOWNSAMPLE_TEXTURE_NAMEID);
        }

        private static int GetHandleWidth(RenderTextureHandle handle)
        {
            return ReadIntMember(handle, new[]
            {
                "Width", "width", "_width"
            }, 0);
        }

        private static int GetHandleHeight(RenderTextureHandle handle)
        {
            return ReadIntMember(handle, new[]
            {
                "Height", "height", "_height"
            }, 0);
        }

        private static RenderTargetIdentifier GetHandleIdentifier(RenderTextureHandle handle)
        {
            // RenderTextureHandle is the project-owned concrete type.  Use its
            // actual target directly instead of reflecting over guessed member
            // names: the camera source intentionally has NameId == -2, while
            // RtId still points at the real camera color target.
            return handle.RtId;
        }

        private static int ReadIntMember(object obj, string[] names, int fallback)
        {
            object value = ReadMember(obj, names);
            if (value == null)
                return fallback;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static object ReadMember(object obj, string[] names)
        {
            if (obj == null)
                return null;

            Type type = obj.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (type != null)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    FieldInfo field = type.GetField(names[i], flags);
                    if (field != null)
                        return field.GetValue(obj);

                    PropertyInfo property = type.GetProperty(names[i], flags);
                    if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                    {
                        try
                        {
                            return property.GetValue(obj, null);
                        }
                        catch
                        {
                            // Try next candidate.
                        }
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        private static T ReadValue<T>(object obj, string[] names, T fallback)
        {
            object value = ReadMember(obj, names);
            if (value == null)
                return fallback;

            if (value is T typed)
                return typed;

            try
            {
                if (typeof(T).IsEnum)
                    return (T)Enum.ToObject(typeof(T), Convert.ToInt32(value));
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return fallback;
            }
        }

        public static void AddShaderVariant(ShaderVariantCollection collection)
        {
            if (collection == null)
                return;

            AddVariant(collection, ShaderManager.ShaderKinds.DepthOfField34);
            AddVariant(collection, ShaderManager.ShaderKinds.SeparableWeightedBlurDof34);
            AddVariant(collection, ShaderManager.ShaderKinds.FastBloom);
            AddVariant(collection, ShaderManager.ShaderKinds.WeightedBlur);
            AddVariant(collection, ShaderManager.ShaderKinds.PostDofBloom_Rich);
            AddVariant(collection, ShaderManager.ShaderKinds.PostDiffusionBloom_Rich);
            AddVariant(collection, ShaderManager.ShaderKinds.PostDiffusionDofBloom_Rich);
            AddVariant(collection, ShaderManager.ShaderKinds.PostBloom_Rich);
            AddVariant(collection, ShaderManager.ShaderKinds.PostBlit_Rich);
        }

        private static void AddVariant(ShaderVariantCollection collection, ShaderManager.ShaderKinds kind)
        {
            Shader shader = ShaderManager.GetShader(kind);
            if (shader != null)
                collection.Add(new ShaderVariantCollection.ShaderVariant(shader, PassType.Normal));
        }

        public static bool IsVaritantShader(ShaderManager.ShaderKinds kind)
        {
            return kind == ShaderManager.ShaderKinds.DepthOfField34 ||
                   kind == ShaderManager.ShaderKinds.SeparableWeightedBlurDof34 ||
                   kind == ShaderManager.ShaderKinds.FastBloom ||
                   kind == ShaderManager.ShaderKinds.WeightedBlur ||
                   kind == ShaderManager.ShaderKinds.PostDofBloom_Rich ||
                   kind == ShaderManager.ShaderKinds.PostDiffusionBloom_Rich ||
                   kind == ShaderManager.ShaderKinds.PostDiffusionDofBloom_Rich ||
                   kind == ShaderManager.ShaderKinds.PostBloom_Rich ||
                   kind == ShaderManager.ShaderKinds.PostBlit_Rich;
        }

        [Serializable]
        public struct Parameter
        {
            private DofDiffusionBloomOverlayParam.DofDiffusionBloomType _useDofDiffusionBloomType;

            public bool IsDisableDofTemporary;
            public bool IsEnableOldDof;
            public bool IsSimpleTweakMode;

            public float OldDofForegroundBlurExtrude;
            public float OldDofFocalZDistance;
            public float OldDofFocalZStartCurve;
            public float OldDofFocalZEndCurve;

            public bool IsEnableDof;
            public bool IsEnableDiffusion;
            public bool IsEnableBloom;
            public bool IsEnableDofAutoDisable;

            public DepthBlurAndBloom.DofQuality DofQualityType;
            public DepthBlurAndBloom.DofFocalType DofFocalType;

            private Transform _dofFocalTransfrom;
            private Vector3 _dofFocalPosition;
            private float _dofFocalPoint;

            public float DofSmoothness;
            public float DofFocalSize;
            public float DofMaxFocalSize;
            public float DofMaxBlurSpread;
            public float DofForegroundSize;
            public DepthBlurAndBloom.DofBlur DofBlurType;

            public float BloomDofWeight;
            public float BloomThreshold;
            public float BloomIntensity;
            public float BloomBlurSize;
            public DofDiffusionBloomOverlayParam.BloomScreenBlendMode BloomBlendMode;

            public float DiffusionBlurSize;
            public float DiffusionBright;
            public float DiffusionThreshold;
            public float DiffusionSaturation;
            public float DiffusionContrast;

            public Camera TargetCamera;

            public float BallBlurPowerFactor;
            public float BallBlurBrightnessThreshhold;
            public float BallBlurBrightnessIntensity;
            public float BallBlurSpread;
            public bool IsPointBallBlur;

            public ScreenOverlayRender.Parameter Overlay1;
            public ScreenOverlayRender.Parameter Overlay2;
            public ScreenOverlayRender.Parameter Overlay3;

            public RenderPassEvent RenderPassEvent;

            public bool IsValidity => IsEnableMode;

            public DofDiffusionBloomOverlayParam.DofDiffusionBloomType UseDofDiffusionBloomType =>
                _useDofDiffusionBloomType;

            public bool IsEnableOverlay =>
                Overlay1.IsValidity || Overlay2.IsValidity || Overlay3.IsValidity;

            public Transform DofFocalTransfrom
            {
                get => _dofFocalTransfrom;
                set => _dofFocalTransfrom = value;
            }

            public Vector3 DofFocalPosition
            {
                get => _dofFocalPosition;
                set => _dofFocalPosition = value;
            }

            public float DofFocalPoint
            {
                get => _dofFocalPoint;
                set => _dofFocalPoint = value;
            }

            public bool IsDepthTexture
            {
                get
                {
                    // Official get_IsDepthTexture:
                    // mode 0 never requests depth. For every enabled mode, the
                    // temporary-disable byte itself forces depth, otherwise the
                    // requirement comes only from the three overlays.
                    if (_useDofDiffusionBloomType ==
                        DofDiffusionBloomOverlayParam.DofDiffusionBloomType.None)
                    {
                        return false;
                    }

                    if (IsDisableDofTemporary)
                        return true;

                    return Overlay1.IsDepthValid |
                           Overlay2.IsDepthValid |
                           Overlay3.IsDepthValid;
                }
            }

            private bool IsEnableMode =>
                _useDofDiffusionBloomType != DofDiffusionBloomOverlayParam.DofDiffusionBloomType.None;

            public void DecideDrawType(bool isDof)
            {
                bool useBloom =
                    IsEnableBloom &&
                    EnableBloomCapability;      // static-fields +0x04

                bool useDiffusion =
                    IsEnableDiffusion &&
                    EnableDiffusionCapability;  // static-fields +0x07

                bool useOverlay =
                    IsEnableOverlay &&
                    EnableOverlayCapability;    // static-fields +0x06

                bool useDof =
                    isDof &&
                    EnableDofCapability;        // static-fields +0x05

                if (!useDof)
                {
                    if (!useBloom)
                    {
                        _useDofDiffusionBloomType =
                            useOverlay
                                ? DofDiffusionBloomOverlayParam.DofDiffusionBloomType.OverlayOnly
                                : DofDiffusionBloomOverlayParam.DofDiffusionBloomType.None;
                        return;
                    }

                    _useDofDiffusionBloomType =
                        useDiffusion
                            ? DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DiffusionBloom
                            : DofDiffusionBloomOverlayParam.DofDiffusionBloomType.Bloom;
                    return;
                }

                if (IsEnableOldDof)
                {
                    _useDofDiffusionBloomType =
                        useBloom
                            ? DofDiffusionBloomOverlayParam.DofDiffusionBloomType.OldDofFastBloom
                            : DofDiffusionBloomOverlayParam.DofDiffusionBloomType.OldDof;
                    return;
                }

                if (useDiffusion)
                {
                    _useDofDiffusionBloomType =
                        DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DiffusionDofBloom;
                    return;
                }

                _useDofDiffusionBloomType =
                    useBloom
                        ? DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DofBloom
                        : DofDiffusionBloomOverlayParam.DofDiffusionBloomType.Dof;
            }

            public void Setup(DofDiffusionBloomOverlayParam src)
            {
                if (src == null)
                    throw new NullReferenceException(nameof(src));

                IsEnableOldDof = src.IsEnableOldDof;
                IsEnableDof = src.IsEnableDof;
                DofQualityType = src.DofQualityType;

                // Exact native write order from VA 0x7FFDABBB49F0.
                DofFocalType = (DepthBlurAndBloom.DofFocalType)1;
                _dofFocalPosition = src.DofFocalPosition;

                DofFocalType = (DepthBlurAndBloom.DofFocalType)2;
                _dofFocalPoint = src.DofFocalPoint;

                DofFocalType = src.DofFocalType;

                DofSmoothness = src.DofSmoothness;
                DofFocalSize = src.DofFocalSize;
                DofMaxFocalSize = src.DofMaxFocalSize;
                DofMaxBlurSpread = src.DofMaxBlurSpread;
                DofForegroundSize = src.DofForegroundSize;
                DofBlurType = src.DofBlurType;

                SetupBloom(src);
                SetupDiffusion(src);

                ScreenOverlay screenOverlay = src.ScreenOverlay;
                if (screenOverlay == null)
                {
                    throw new NullReferenceException(
                        "DofDiffusionBloomOverlayParam.ScreenOverlay is null.");
                }

                Overlay1.Setup(screenOverlay.Overlay1);
                Overlay2.Setup(screenOverlay.Overlay2);
                Overlay3.Setup(screenOverlay.Overlay3);

                // Official Setup deliberately leaves these untouched:
                // IsSimpleTweakMode, old-DOF values, IsEnableDofAutoDisable,
                // DofFocalTransfrom, TargetCamera, BallBlur values,
                // RenderPassEvent and draw-type state.
            }

            public void SetupBloom(DofDiffusionBloomOverlayParam src)
            {
                if (src == null)
                    throw new NullReferenceException(nameof(src));

                IsEnableBloom = src.IsEnableBloom;
                BloomDofWeight = src.BloomDofWeight;
                BloomThreshold = src.BloomThreshold;
                BloomIntensity = src.BloomIntensity;
                BloomBlurSize = src.BloomBlurSize;
                BloomBlendMode = src.BloomBlendMode;
            }

            public void SetupDiffusion(DofDiffusionBloomOverlayParam src)
            {
                if (src == null)
                    throw new NullReferenceException(nameof(src));

                IsEnableDiffusion = src.IsEnableDiffusion;
                DiffusionBlurSize = src.DiffusionBlurSize;
                DiffusionBright = src.DiffusionBright;
                DiffusionThreshold = src.DiffusionThreshold;
                DiffusionSaturation = src.DiffusionSaturation;
                DiffusionContrast = src.DiffusionContrast;
            }

            public static Parameter Default()
            {
                Parameter result = default;

                result._useDofDiffusionBloomType =
                    DofDiffusionBloomOverlayParam.DofDiffusionBloomType.None;

                result.IsDisableDofTemporary = false;
                result.IsEnableOldDof = false;
                result.IsSimpleTweakMode = true;

                result.OldDofForegroundBlurExtrude = 1.15f;
                result.OldDofFocalZDistance = 0f;
                result.OldDofFocalZStartCurve = 1f;
                result.OldDofFocalZEndCurve = 1f;

                result.IsEnableDof = false;
                result.IsEnableDiffusion = false;
                result.IsEnableBloom = false;
                result.IsEnableDofAutoDisable = false;

                result.DofQualityType = (DepthBlurAndBloom.DofQuality)1;
                result.DofFocalType = (DepthBlurAndBloom.DofFocalType)1;
                result._dofFocalTransfrom = null;
                result._dofFocalPosition = DefaultDofFocalPosition;
                result._dofFocalPoint = 1f;

                result.DofSmoothness = 0.5f;
                result.DofFocalSize = 0f;
                result.DofMaxFocalSize = 0f;
                result.DofMaxBlurSpread = 1.75f;
                result.DofForegroundSize = 0f;
                result.DofBlurType = (DepthBlurAndBloom.DofBlur)0;

                result.BloomDofWeight = 0f;
                result.BloomThreshold = 0.25f;
                result.BloomIntensity = 0.75f;
                result.BloomBlurSize = 1f;
                result.BloomBlendMode =
                    DofDiffusionBloomOverlayParam.BloomScreenBlendMode.Add;

                result.DiffusionBlurSize = 1f;
                result.DiffusionBright = 1f;
                result.DiffusionThreshold = 0f;
                result.DiffusionSaturation = 1f;
                result.DiffusionContrast = 1f;

                result.TargetCamera = null;

                result.BallBlurPowerFactor = 0f;
                result.BallBlurBrightnessThreshhold = 1f;
                result.BallBlurBrightnessIntensity = -1f;
                result.BallBlurSpread = 1f;
                result.IsPointBallBlur = false;

                result.Overlay1 = ScreenOverlayRender.Parameter.Default();
                result.Overlay2 = ScreenOverlayRender.Parameter.Default();
                result.Overlay3 = ScreenOverlayRender.Parameter.Default();

                result.RenderPassEvent = (RenderPassEvent)550;
                return result;
            }

            public void CopyFrom_Bloom(in Parameter src)
            {
                // Official VA 0x7FFDABBB3350 copies exactly 21 bytes/fields:
                // IsEnableBloom and the five Bloom values. Overlay state is untouched.
                IsEnableBloom = src.IsEnableBloom;
                BloomDofWeight = src.BloomDofWeight;
                BloomThreshold = src.BloomThreshold;
                BloomIntensity = src.BloomIntensity;
                BloomBlurSize = src.BloomBlurSize;
                BloomBlendMode = src.BloomBlendMode;
            }
        }
    }
}
