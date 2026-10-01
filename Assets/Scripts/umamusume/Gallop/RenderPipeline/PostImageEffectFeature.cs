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
    /// Recovered SourceSetup -> Dof/Diffusion/Bloom/Overlay -> RadialBlur -> SimpleColorCorrection -> FinalBlit chain.
    /// Other reference passes are not implemented by this feature yet.
    /// </summary>
    public class PostImageEffectFeature : ScriptableRendererFeature
    {
        private const int INVALID_NAME_ID = -2;
        private const int REGISTER_PASS_SIZE = 12;
        private const string SOURCE_SETUP_POOL_NAME =
            "PostImageEffectFeature.SourceSetup";
        private const string FINAL_BLIT_POOL_NAME =
            "PostImageEffectFeature.FinalBlit";

        // 只保留 DofDiffusionBloomOverlayPass 实际需要的 Pass。
        private SourceSetupPass _sourceSetupPass;
        private DofDiffusionBloomOverlayPass
            _dofDiffusionBloomOverlayPass;
        private FinalBlitPass _finalBlitPass;
        private RadialBlurPass _radialBlurPass;
        private SimpleColorCorrectionPass _colorCorrectionPass;
        private FinalBlitPass _colorEarlyResolvePass;
        private ExposureToneCurvePass _exposurePass, _toneCurvePass;
        private SunShaftsPass _sunShaftsPass;
        private IndirectLightShaftsPass _indirectLightShaftsPass;
        private MultiCameraCompositePass _multiCameraCompositePass;
        private MultiCameraFinalCompositePass _multiCameraFinalCompositePass;
        private ProjectorRenderPass _projectorPass;

        private RenderTextureHandle _sourceRT;
        private List<ScriptableRenderPass> _registerPass;
        private bool _loggedEnqueue;
        private bool _loggedParameterMissing;
        private bool _loggedParameterDisabled;
        private bool _loggedSetupFailed;
        private bool _loggedBypass;
        private bool _shaderLoadRequested;
        private float _nextShaderLoadAttempt;

        // Missing shader resources cause Setup to fail without replacing camera color.
        // Disable this switch for an unprocessed A/B baseline.
        [SerializeField]
        private bool _enableRendering = true;

        // Unlike SetActive, this leaves the mandatory multi-camera composition passes scheduled.
        public bool EnableRendering
        {
            get => _enableRendering;
            set => _enableRendering = value;
        }

        // Shared live state. Director updates this object from the timeline;
        // each camera's CameraData points at the same state before Setup runs.
        private static Parameter _runtimeParameter = new Parameter();

        public static Parameter RuntimeParameter
        {
            get
            {
                if (_runtimeParameter == null)
                    _runtimeParameter = new Parameter();
                return _runtimeParameter;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetRuntimeParameter()
        {
            _runtimeParameter = new Parameter();
        }

        public RenderTextureHandle SourceRT
        {
            get { return _sourceRT; }
        }

        // Pure scheduling policy, shared with regression tests. URP performs the stable
        // event sort; this does not change the existing DOF pass or its late camera writes.
        public struct ColorChainSchedule
        {
            public int FirstEvent, LastEvent;
            public bool EarlyResolve, FinalResolve;
        }
        public static ColorChainSchedule GetColorChainSchedule(bool dof, bool radial, bool color, RenderPassEvent effectEvent)
        {
            bool existing = dof || radial;
            int first = existing ? (int)effectEvent : 550;
            int last = first;
            if (color) { first = System.Math.Min(first, 550); last = System.Math.Max(last, 550); }
            return new ColorChainSchedule {
                FirstEvent = first, LastEvent = last,
                EarlyResolve = color && existing && (int)effectEvent > 550,
                FinalResolve = radial || (color && (!existing || (int)effectEvent <= 550)) ||
                    (existing && (int)effectEvent <= 550)
            };
        }

        public override void Create()
        {
            Dispose();
            _loggedEnqueue = false;
            _loggedParameterMissing = false;
            _loggedParameterDisabled = false;
            _loggedSetupFailed = false;
            _loggedBypass = false;
            _shaderLoadRequested = false;
            _nextShaderLoadAttempt = 0f;

            RenderPassEvent passEvent =
                RenderPassEvent.BeforeRenderingPostProcessing;

            _sourceSetupPass = new SourceSetupPass(this, passEvent);
            _dofDiffusionBloomOverlayPass = new DofDiffusionBloomOverlayPass(passEvent);
            _finalBlitPass = new FinalBlitPass(this, passEvent);
            _radialBlurPass = new RadialBlurPass(passEvent);
            _colorCorrectionPass = new SimpleColorCorrectionPass();
            _colorEarlyResolvePass = new FinalBlitPass(this, passEvent, "ColorEarlyResolve");
            _exposurePass = new ExposureToneCurvePass(false);
            _toneCurvePass = new ExposureToneCurvePass(true);
            _sunShaftsPass = new SunShaftsPass(passEvent);
            _indirectLightShaftsPass = new IndirectLightShaftsPass(passEvent);
            _multiCameraCompositePass = new MultiCameraCompositePass();
            _multiCameraFinalCompositePass = new MultiCameraFinalCompositePass();
            _projectorPass = new ProjectorRenderPass();

            _registerPass = new List<ScriptableRenderPass>(REGISTER_PASS_SIZE);

            _sourceRT = RenderTextureHandle.Make(new RenderTargetIdentifier(INVALID_NAME_ID), 0, 0);
            Debug.Log($"[RecoveredPostEffect] Create enabled={_enableRendering} " +
                      $"shaderReady={ShaderManager.IsShaderBundleReady}");

            // 让 Dof Pass 直接使用同一套 CameraData 参数解析逻辑。
            DofDiffusionBloomOverlayPass.FeatureParameterResolver = ResolveFeatureParameter;
        }

        public override void AddRenderPasses( ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera == null)
                return;

            var director = Gallop.Live.Director.instance;
            // Native ProjectorFeature draws stage projectors after opaque geometry,
            // independently of presentation-camera post-processing (including reflection/monitor views).
            if (Application.isPlaying && camera.cameraType == CameraType.Game && director != null &&
                _projectorPass != null && _projectorPass.Setup(director.LightProjectors, director.MirrorBallProjectors))
            {
                renderer.EnqueuePass(_projectorPass);
            }

            if (string.Equals(camera.name, "MonitorCameraCapture", StringComparison.Ordinal))
                return;

            // The timeline parameter is authored for the Live presentation
            // cameras. Reflection/monitor cameras use different render targets
            // and depth ranges; running the same multi-RT DOF/Bloom chain on
            // them corrupts the reflection texture and can leak black holes or
            // a white veil into the main frame.
            // [REPORT §10.3-B1/§4.2] 原生按相机组逐台注册后处理（RegisterPass 走
            // camera stack，覆盖演示相机组），master7 原以 camera != MainRenderCamera
            // 的引用相等做门控：CameraSwitcher 切换瞬间旧相机当帧不注册、新相机
            // 重走 Setup/SourceRT 重建链，切换帧丢失一次后处理输出。改为对
            // Director 的演示相机组做成员判定（IsPresentationCamera），同帧内
            // 组内每台相机都注册整链，消除切换窗口。非 Game 类型和
            // MonitorCameraCapture 的跳过保持不变（与原生名字跳过一致）。
            if (!Application.isPlaying || camera.cameraType != CameraType.Game || director == null)
                return;
            camera.TryGetComponent<MultiCamera>(out var multiCamera);
            if (multiCamera == null && !director.IsPresentationCamera(camera)) return;

            // Native composition runs even when the optional image-effect chain is disabled.
            if (multiCamera != null)
            {
                if (_multiCameraCompositePass != null && _multiCameraCompositePass.Setup(multiCamera))
                    renderer.EnqueuePass(_multiCameraCompositePass);
            }
            else if (camera == director.MainRenderCamera && _multiCameraFinalCompositePass != null &&
                _multiCameraFinalCompositePass.Setup(director.MultiCameraFinalComposite))
                renderer.EnqueuePass(_multiCameraFinalCompositePass);

            bool renderEnabled = _enableRendering;
            if (!renderEnabled)
            {
                if (!_loggedBypass)
                {
                    _loggedBypass = true;
                    Debug.Log("[RecoveredPostEffect] bypass enabled: camera color is preserved");
                }
                return;
            }

            // Renderer features are constructed while the pipeline is being
            // deserialized, before UmaViewerMain.Start() has loaded shader.a.
            // Request the bundle here as soon as a Live camera actually needs
            // the recovered chain. Setup below is retried on subsequent frames
            // and will create the materials after the synchronous load lands.
            if (!ShaderManager.IsShaderBundleReady &&
                Time.realtimeSinceStartup >= _nextShaderLoadAttempt)
            {
                _nextShaderLoadAttempt = Time.realtimeSinceStartup + 1f;
                ShaderManager.LoadShaderBundle();
                if (!_shaderLoadRequested)
                {
                    _shaderLoadRequested = true;
                    Debug.Log("[RecoveredPostEffect] requested shader bundle load from render pass");
                }
            }

            CameraData cameraData;
            if (!camera.TryGetComponent(out cameraData))
                cameraData = camera.gameObject.AddComponent<CameraData>();

            // The recovered feature expects CameraData.Parameter. Keep it
            // bound to the timeline-owned object and refresh the active camera
            // every frame because CameraSwitcher can replace it.
            var cameraParameter = multiCamera != null ? multiCamera.PostEffectParameter : RuntimeParameter;
            cameraData.Parameter = cameraParameter;
            cameraParameter.TargetCamera = camera;

            Parameter parameter = ResolveFeatureParameter(cameraData);
            if (parameter == null)
            {
                if (!_loggedParameterMissing)
                {
                    _loggedParameterMissing = true;
                    Debug.LogWarning($"[RecoveredPostEffect] skip camera={camera.name}: CameraData.Parameter missing");
                }
                return;
            }

            if (!parameter.IsEnable)
            {
                if (!_loggedParameterDisabled)
                {
                    _loggedParameterDisabled = true;
                    Debug.Log($"[RecoveredPostEffect] skip camera={camera.name}: feature parameter disabled");
                }
                return;
            }

            if (_registerPass == null)
            {
                _registerPass =  new List<ScriptableRenderPass>(REGISTER_PASS_SIZE);
            }

            _registerPass.Clear();

            if (_sourceSetupPass == null || _dofDiffusionBloomOverlayPass == null || _finalBlitPass == null)
            {
                return;
            }

            bool dofReady = _dofDiffusionBloomOverlayPass.Setup(cameraData, this);
            bool radialReady = _radialBlurPass != null && _radialBlurPass.Setup(parameter.RadialBlur, this);
            bool colorReady = _colorCorrectionPass != null && _colorCorrectionPass.Setup(parameter.ColorCorrection, this);
            bool exposureReady = _exposurePass != null && _exposurePass.Setup(parameter.ExposureParam, parameter.ToneCurveParam, this);
            bool toneReady = _toneCurvePass != null && _toneCurvePass.Setup(parameter.ExposureParam, parameter.ToneCurveParam, this);
            bool sunReady = _sunShaftsPass != null && _sunShaftsPass.Setup(parameter.SunShafts, this);
            bool shaftsReady = _indirectLightShaftsPass != null && _indirectLightShaftsPass.Setup(parameter.LightShafts, this);
            bool correctionReady = colorReady || exposureReady || toneReady;
            if (!dofReady && !radialReady && !correctionReady && !sunReady && !shaftsReady)
            {
                if (!_loggedSetupFailed)
                {
                    _loggedSetupFailed = true;
                    Debug.LogWarning($"[RecoveredPostEffect] setup failed camera={camera.name} " +
                                     $"type={parameter.DofDiffuionBloomOverlay.UseDofDiffusionBloomType} " +
                                     $"shaderReady={ShaderManager.IsShaderBundleReady}");
                }
                return;
            }

            if (!_loggedEnqueue)
            {
                _loggedEnqueue = true;
                Debug.Log($"[RecoveredPostEffect] enqueue camera={camera.name} " +
                          $"type={RuntimeParameter.DofDiffuionBloomOverlay.UseDofDiffusionBloomType} " +
                          $"shaderReady={ShaderManager.IsShaderBundleReady}");
            }

            // Explicitly request URP depth for CoC; do not rely on another feature
            // incidentally producing _CameraDepthTexture. Reset when leaving DOF.
            _dofDiffusionBloomOverlayPass.ConfigureInput(parameter.IsUseDepthTexture
                ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
            RenderPassEvent effectEvent = parameter.DofDiffuionBloomOverlay.RenderPassEvent;

            // Color keeps its native default event550, independent of DOF's event.
            var schedule = GetColorChainSchedule(dofReady, radialReady,
                correctionReady || sunReady || shaftsReady, effectEvent);
            _sourceSetupPass.renderPassEvent = (RenderPassEvent)schedule.FirstEvent;
            _finalBlitPass.renderPassEvent = (RenderPassEvent)schedule.LastEvent;

            // 同一 RenderPassEvent 下按 Enqueue 顺序执行。
            _registerPass.Add(_sourceSetupPass);
            if (sunReady) _registerPass.Add(_sunShaftsPass);
            if (shaftsReady) _registerPass.Add(_indirectLightShaftsPass);
            if (dofReady) _registerPass.Add(_dofDiffusionBloomOverlayPass);
            if (radialReady)
            {
                _radialBlurPass.renderPassEvent = effectEvent;
                _radialBlurPass.ConfigureInput(parameter.RadialBlur.IsEnabledDepth
                    ? ScriptableRenderPassInput.Depth : ScriptableRenderPassInput.None);
                _registerPass.Add(_radialBlurPass);
            }

            if (toneReady) _registerPass.Add(_toneCurvePass);
            if (exposureReady) _registerPass.Add(_exposurePass);
            if (colorReady)
            {
                _registerPass.Add(_colorCorrectionPass);
            }
            // All native event550 corrections must resolve before a late DOF draw.
            if (schedule.EarlyResolve)
            {
                _registerPass.Add(_colorEarlyResolvePass);
            }

            // Radial always hands its temporary result back to this feature.
            // DOF alone writes directly to camera color for events after 550.
            if (schedule.FinalResolve)
            {
                _registerPass.Add(_finalBlitPass);
            }

            for (int i = 0; i < _registerPass.Count; i++)
                renderer.EnqueuePass(_registerPass[i]);

            // Read-only runtime evidence: this records the actual URP descriptor
            // and scheduled pass count without changing the render chain.
            LiveRuntimeDiagnostics.RecordRenderPass(
                camera, renderingData.cameraData.cameraTargetDescriptor, parameter,
                _registerPass.Count, ShaderManager.IsShaderBundleReady,
                renderingData.cameraData.postProcessEnabled);
        }

        /// <summary>
        /// 将当前源设置为相机颜色目标
        /// 这替代原版SetSourceRTPass对 Dof-only 链路的作用
        /// </summary>
        public void ResetSourceRT( CommandBuffer cmd, ref RenderingData renderingData)
        {
            ScriptableRenderer renderer = renderingData.cameraData.renderer;

            if (renderer == null)
                return;

            RTHandle cameraColorHandle = renderer.cameraColorTargetHandle;
            if (cameraColorHandle == null)
                return;

            RenderTargetIdentifier cameraColorTarget = cameraColorHandle.nameID;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;

            if (cmd != null && !_sourceRT.RtId.Equals(cameraColorTarget) && _sourceRT.NameId != INVALID_NAME_ID)
            {
                cmd.ReleaseTemporaryRT(_sourceRT.NameId);
            }

            _sourceRT = RenderTextureHandle.Make( cameraColorTarget, descriptor.width, descriptor.height);
        }

        /// <summary>
        /// DofDiffusionBloomOverlayPass.Execute 在完成后调用此函数，
        /// 把输出临时 RT 交回 Feature。
        /// </summary>
        public void SetSourceRT(RenderTextureHandle sourceHandle, CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (_sourceRT.NameId == sourceHandle.NameId)
                return;

            ReleaseCurrentTemporaryRT(cmd, ref renderingData);

            _sourceRT = sourceHandle;
        }

        private void ReleaseCurrentTemporaryRT(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ScriptableRenderer renderer = renderingData.cameraData.renderer;

            if (renderer == null)
                return;

            RTHandle cameraColorHandle = renderer.cameraColorTargetHandle;
            if (cameraColorHandle == null)
                return;

            RenderTargetIdentifier cameraColorTarget = cameraColorHandle.nameID;

            if (!_sourceRT.RtId.Equals(cameraColorTarget) && _sourceRT.NameId != INVALID_NAME_ID && cmd != null)
            {
                cmd.ReleaseTemporaryRT(_sourceRT.NameId);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _registerPass = null;
            _radialBlurPass?.Dispose();
            _radialBlurPass = null;
            _colorCorrectionPass?.Dispose();
            _colorCorrectionPass = null;
            _colorEarlyResolvePass = null;
            _exposurePass?.Dispose();
            _toneCurvePass?.Dispose();
            _exposurePass = _toneCurvePass = null;

            _sunShaftsPass?.Dispose();
            _indirectLightShaftsPass?.Dispose();
            _sunShaftsPass = null;
            _indirectLightShaftsPass = null;
            _multiCameraCompositePass = null;
            _multiCameraFinalCompositePass = null;
            if (_dofDiffusionBloomOverlayPass != null)
            {
                _dofDiffusionBloomOverlayPass.Dispose();
                _dofDiffusionBloomOverlayPass = null;
            }

            _sourceSetupPass = null;
            _finalBlitPass = null;

            base.Dispose(disposing);
        }

        /// <summary>
        /// CameraData 原生偏移 +0x150 对应的 Parameter 解析。
        /// 字段真实名称确定后，可以直接替换为明确字段访问。
        /// </summary>
        private static Parameter ResolveFeatureParameter(CameraData cameraData)
        {
            if (cameraData == null)
                return null;

            object instance = cameraData;
            Type type = instance.GetType();
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            while (type != null)
            {
                FieldInfo[] fields = type.GetFields(flags);
                for (int i = 0; i < fields.Length; i++)
                {
                    if (typeof(Parameter)
                        .IsAssignableFrom(fields[i].FieldType))
                    {
                        return fields[i].GetValue(instance)
                            as Parameter;
                    }
                }

                PropertyInfo[] properties = type.GetProperties(flags);

                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    if (!typeof(Parameter)
                        .IsAssignableFrom(property.PropertyType))
                    {
                        continue;
                    }

                    try
                    {
                        return property.GetValue(instance, null)
                            as Parameter;
                    }
                    catch
                    {
                        // 继续搜索基类中的参数字段。
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        /// 在 Dof Pass 前，把 Feature.SourceRT 指向 Camera Color。
        /// </summary>
        private sealed class SourceSetupPass : ScriptableRenderPass
        {
            private readonly PostImageEffectFeature _owner;

            public SourceSetupPass( PostImageEffectFeature owner, RenderPassEvent passEvent)
            {
                _owner = owner;
                renderPassEvent = passEvent;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_owner == null)
                    return;

                CommandBuffer cmd =
                    CommandBufferPool.Get(SOURCE_SETUP_POOL_NAME);

                try
                {
                    _owner.ResetSourceRT(cmd, ref renderingData);

                    context.ExecuteCommandBuffer(cmd);
                    LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                        "SourceSetup", renderPassEvent, default, _owner.SourceRT);
                }
                finally
                {
                    CommandBufferPool.Release(cmd);
                }
            }
        }

        /// <summary>
        /// Dof Pass 在 550 或更早执行时，只更新 Feature.SourceRT。
        /// 此 Pass 将结果写回 Camera Color，并释放 Dof 临时 RT。
        /// </summary>
        private sealed class FinalBlitPass : ScriptableRenderPass
        {
            private readonly PostImageEffectFeature _owner;
            private readonly string _traceStep;

            public FinalBlitPass(PostImageEffectFeature owner, RenderPassEvent passEvent, string traceStep = "FinalBlit")
            {
                _owner = owner;
                _traceStep = traceStep;
                renderPassEvent = passEvent;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_owner == null)
                    return;

                RenderTextureHandle source = _owner.SourceRT;
                if (source.NameId == INVALID_NAME_ID)
                    return;

                ScriptableRenderer renderer = renderingData.cameraData.renderer;

                if (renderer == null)
                    return;

                RTHandle cameraColorHandle = renderer.cameraColorTargetHandle;
                if (cameraColorHandle == null)
                    return;

                RenderTargetIdentifier cameraColorTarget = cameraColorHandle.nameID;

                CommandBuffer cmd =
                    CommandBufferPool.Get(FINAL_BLIT_POOL_NAME);

                try
                {
                    if (!source.RtId.Equals(cameraColorTarget))
                    {
                        cmd.Blit(
                            source.RtId,
                            cameraColorTarget);
                    }

                    // Blit 后释放 Dof 输出临时 RT,并把 SourceRT 恢复为 Camera Color
                    _owner.ResetSourceRT(cmd, ref renderingData);

                    context.ExecuteCommandBuffer(cmd);
                    LiveRuntimeDiagnostics.RecordRenderExecution(renderingData.cameraData.camera,
                        _traceStep, renderPassEvent, source, _owner.SourceRT);
                }
                finally
                {
                    CommandBufferPool.Release(cmd);
                }
            }
        }

        public class Parameter
        {
            private Camera _targetCamera;
            private CameraData _targetCameraData;

            public bool IsEnable;

            // DofDiffusionBloomOverlayPass 实际使用的参数。
            public DofDiffusionBloomOverlayPass.Parameter
                DofDiffuionBloomOverlay;

            public RadialBlurPass.Parameter RadialBlur = RadialBlurPass.Parameter.Default();
            public SimpleColorCorrectionPass.Parameter ColorCorrection;
            public SunShaftsPass.Parameter SunShafts = SunShaftsPass.Parameter.Default();
            public IndirectLightShaftsPass.Parameter LightShafts;

            // Native ToneCurve/Exposure parameter blocks consumed at event550.
            public Gallop.ImageEffect.ExposureParam ExposureParam =
                new Gallop.ImageEffect.ExposureParam();
            public Gallop.ImageEffect.ToneCurveParam ToneCurveParam =
                new Gallop.ImageEffect.ToneCurveParam();

            public bool IsRequestDepthPass;

            public Camera TargetCamera
            {
                get { return _targetCamera; }
                set
                {
                    _targetCamera = value;
                    DofDiffuionBloomOverlay.TargetCamera = value;

                    if (_targetCamera == null)
                        return;

                    if (!_targetCamera.TryGetComponent(
                            out _targetCameraData))
                    {
                        _targetCameraData = _targetCamera.gameObject.AddComponent<CameraData>();

                        InvokeCameraDataInitialize( _targetCameraData);
                    }
                }
            }

            public bool IsUseDepthTexture
            {
                get
                {
                    // The recovered IsDepthTexture property represents the original
                    // overlay/depth-request contract, not URP's complete DOF input needs.
                    var mode = DofDiffuionBloomOverlay.UseDofDiffusionBloomType;
                    return (RadialBlur.IsValid && RadialBlur.IsEnabledDepth) ||
                           mode == DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DofBloom ||
                           mode == DofDiffusionBloomOverlayParam.DofDiffusionBloomType.DiffusionDofBloom ||
                           mode == DofDiffusionBloomOverlayParam.DofDiffusionBloomType.Dof ||
                           mode == DofDiffusionBloomOverlayParam.DofDiffusionBloomType.OldDof ||
                           mode == DofDiffusionBloomOverlayParam.DofDiffusionBloomType.OldDofFastBloom ||
                           DofDiffuionBloomOverlay.IsDepthTexture;
                }
            }

            public Parameter()
            {
                IsEnable = true;
                DofDiffuionBloomOverlay = DofDiffusionBloomOverlayPass.Parameter.Default();
                IsRequestDepthPass = true;
            }

            public void ShallowCopyFrom(Parameter src)
            {
                if (src == null)
                    throw new ArgumentNullException(nameof(src));

                ShallowCopyParameters(src);
                IsRequestDepthPass = src.IsRequestDepthPass;
            }

            public void ShallowCopyParameters(Parameter src)
            {
                if (src == null)
                    throw new ArgumentNullException(nameof(src));

                DofDiffuionBloomOverlay =
                    src.DofDiffuionBloomOverlay;
                RadialBlur = src.RadialBlur;
                ColorCorrection = src.ColorCorrection;
                SunShafts = src.SunShafts;
                LightShafts = src.LightShafts;
            }

            public void CopyFromGallopImageEffect(
                GallopImageEffectParameter imageEffectParam)
            {
                if (imageEffectParam == null)
                {
                    throw new ArgumentNullException( nameof(imageEffectParam));
                }

                DofDiffuionBloomOverlay.Setup(imageEffectParam.DofDiffusionBloomOverlayParam);
            }

            private static void InvokeCameraDataInitialize(
                CameraData cameraData)
            {
                if (cameraData == null)
                    return;

                MethodInfo[] methods =
                    cameraData.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (method.Name != "Initialize")
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length != 1)
                        continue;

                    Type parameterType = parameters[0].ParameterType;

                    if (parameterType == typeof(bool))
                    {
                        method.Invoke(cameraData, new object[] { false });
                        return;
                    }

                    if (parameterType == typeof(int))
                    {
                        method.Invoke(cameraData, new object[] { 0 });
                        return;
                    }
                }
            }
        }
    }
}

