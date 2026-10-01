using Gallop.ImageEffect;
using Gallop.RenderPipeline;
using UnityEngine;

namespace Gallop
{
    /// <summary>Timeline adapter. Rendering is owned by the Gallop renderer feature,
    /// not by an additional URP Bloom volume (which would apply bloom twice).</summary>
    [DisallowMultipleComponent]
    public class GallopImageEffect : MonoBehaviour
    {
        [SerializeField] private DofDiffusionBloomOverlayParam _dofDiffusionBloomOverlayParam =
            new DofDiffusionBloomOverlayParam();

        public DofDiffusionBloomOverlayParam DofDiffusionBloomOverlayParam => _dofDiffusionBloomOverlayParam;
        public bool IsInitialized { get; private set; }

        // 子系统 A7b：原生 GallopImageEffect.get_ExposureParam(RVA=0x19a97b0) /
        // get_ToneCurveParam(RVA=0x19a9930) 的 master7 等价属性。原生 getter 返回
        // CameraData 内偏移 +5B0h 的参数块；master7 渲染链用独立 Param 对象，
        // Director.OnUpdateExposure/OnUpdateToneCurve 写入这里，再由
        // ApplyBloomParameter 发布到 PostImageEffectFeature.RuntimeParameter。
        public ExposureParam ExposureParam { get; } = new ExposureParam();
        public ToneCurveParam ToneCurveParam { get; } = new ToneCurveParam();

        // User A/B gate, independent of authored timeline data and other effects.
        // Enabled after fixing the source focus-mode setter ordering regression.
        public static bool UserDofEnabled { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetUserDofEnabled() { UserDofEnabled = true; }

        public static void SetUserDofEnabled(bool enabled)
        {
            UserDofEnabled = enabled;
            var director = Live.Director.instance;
            if (director == null || director.MainRenderCamera == null) return;
            var adapter = director.MainRenderCamera.GetComponent<GallopImageEffect>();
            if (adapter != null) adapter.ApplyBloomParameter();
        }

        private void Awake() { InitializeVolume(); }

        // Retained for existing callers; no spatial Volume is created.
        public void InitializeVolume() { IsInitialized = true; }

        public void ApplyBloomParameter()
        {
            var director = Live.Director.instance;
            if (director == null || director.MainRenderCamera == null ||
                director.MainRenderCamera.gameObject != gameObject) return;

            var parameter = PostImageEffectFeature.RuntimeParameter;
            parameter.DofDiffuionBloomOverlay.Setup(_dofDiffusionBloomOverlayParam);
            if (!UserDofEnabled)
            {
                parameter.DofDiffuionBloomOverlay.IsEnableDof = false;
                parameter.DofDiffuionBloomOverlay.IsEnableOldDof = false;
            }
            parameter.DofDiffuionBloomOverlay.DecideDrawType(
                UserDofEnabled && _dofDiffusionBloomOverlayParam.IsEnableDof);
            parameter.TargetCamera = director.MainRenderCamera;

            // 子系统 A7b：原生 CameraData.UpdateImageEffectParameter(RVA=0x1a0da30)
            // 末尾 jmp Parameter.DecideDrawType，把 GallopImageEffect 的参数块
            //（DOF + Exposure + ToneCurve…）统一交给渲染侧。master7 的
            // PostImageEffectFeature.Parameter 还没有 Exposure/ToneCurve 的
            // pass 消费字段，因此先挂在运行时参数对象上供后续
            // ExposurePass/ToneCurvePass（原生 RVA=0x1a22e10/0x1a527d0）接入，
            // 数据已随时间轴逐帧刷新，不会比原生缺帧。
            parameter.ExposureParam.Setup(ExposureParam);
            parameter.ToneCurveParam.Setup(ToneCurveParam);
        }
    }
}
