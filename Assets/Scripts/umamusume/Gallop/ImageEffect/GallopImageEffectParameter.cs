using System;
using Gallop.ImageEffect;
using UnityEngine;

namespace Gallop
{
    /// <summary>
    /// 当前仅启用 DofDiffusionBloomOverlay 所需参数。
    ///
    /// 其他官方参数字段和处理逻辑暂时保留为注释，
    /// 等对应参数类及 Pass 完成后再恢复。
    /// </summary>
    [Serializable]
    public class GallopImageEffectParameter : ScriptableObject
    {
        /*
        // 官方字段：当前未启用。
        public GlobalFogParam GlobalFogParam;
        public SunShaftsParam SunShaftsParam;
        public TiltShiftParam TiltShiftParam;
        public IndirectLightShaftsParam IndirectLightShaftsParam;
        public RadialBlurParam RadialBlurParam;
        */

        // 当前 PostImageEffectFeature 实际使用的参数。
        public DofDiffusionBloomOverlayParam
            DofDiffusionBloomOverlayParam;

        /*
        // 官方字段：当前未启用。
        public LensDistortionParam LensDistortionParam;
        public ColorCorrectionParam ColorCorrectionParam;
        public FluctuationParam FluctuationParam;

        public ChromaticAberrationParameter
            ChromaticAberrationParam;

        public ToneCurveParam ToneCurveParam;
        public ExposureParam ExposureParam;
        public HatchingParam HatchingParam;
        public LetterBoxParam LetterBoxParam;
        public RainSplashParam RainSplashParam;
        */

        /// <summary>
        /// 从另一个参数资源复制后的扩展处理。
        /// 官方基类为空实现。
        /// </summary>
        protected virtual void OnCopyFrom(
            GallopImageEffectParameter src)
        {
        }

        /// <summary>
        /// 从 GallopImageEffect 读取后的扩展处理。
        /// 官方基类为空实现。
        /// </summary>
        protected virtual void OnCopyFrom(
            GallopImageEffect src)
        {
        }

        /// <summary>
        /// 写入 GallopImageEffect 后的扩展处理。
        /// 官方基类为空实现。
        /// </summary>
        protected virtual void OnCopyTo(
            GallopImageEffect target)
        {
        }

        /// <summary>
        /// 插值完成后的扩展处理。
        /// 官方基类为空实现。
        /// </summary>
        protected virtual void OnLerp(
            GallopImageEffectParameter src1,
            GallopImageEffectParameter src2,
            float t)
        {
        }

        /// <summary>
        /// 从运行时 GallopImageEffect 读取参数。
        /// 当前只读取 DofDiffusionBloomOverlay。
        /// </summary>
        public void CopyFrom(GallopImageEffect src)
        {
            DofDiffusionBloomOverlayParam.Setup(
                src.DofDiffusionBloomOverlayParam);

            /*
            // 官方完整逻辑，当前暂不启用。
            GlobalFogParam.Setup(src.GlobalFogParam);
            SunShaftsParam.Setup(src.SunShaftsParam);
            TiltShiftParam.Setup(src.TiltShiftParam);

            IndirectLightShaftsParam.Setup(
                src.IndirectLightShaftsParam);

            RadialBlurParam.Setup(src.RadialBlurParam);

            LensDistortionParam.Setup(
                src.LensDistortionParam);

            FluctuationParam.Setup(src.FluctuationParam);

            ColorCorrectionParam.Setup(
                src.ColorCorrectionParam);

            ChromaticAberrationParam.Setup(
                src.ChromaticAberration);

            ToneCurveParam.Setup(src.ToneCurveParam);
            ExposureParam.Setup(src.ExposureParam);
            HatchingParam.Setup(src.HatchingParam);
            RainSplashParam.Setup(src.RainSplash);
            */

            OnCopyFrom(src);
        }

        /// <summary>
        /// 从另一个参数资源复制。
        /// 当前只复制 DofDiffusionBloomOverlay。
        /// </summary>
        public void CopyFrom(GallopImageEffectParameter src)
        {
            DofDiffusionBloomOverlayParam.Setup(
                src.DofDiffusionBloomOverlayParam);

            /*
            // 官方完整逻辑，当前暂不启用。
            GlobalFogParam.Setup(src.GlobalFogParam);
            SunShaftsParam.Setup(src.SunShaftsParam);
            TiltShiftParam.Setup(src.TiltShiftParam);

            IndirectLightShaftsParam.Setup(
                src.IndirectLightShaftsParam);

            RadialBlurParam.Setup(src.RadialBlurParam);

            LensDistortionParam.Setup(
                src.LensDistortionParam);

            ColorCorrectionParam.Setup(
                src.ColorCorrectionParam);

            ChromaticAberrationParam.Setup(
                src.ChromaticAberrationParam);

            ToneCurveParam.Setup(src.ToneCurveParam);
            ExposureParam.Setup(src.ExposureParam);
            HatchingParam.Setup(src.HatchingParam);
            RainSplashParam.Setup(src.RainSplashParam);

            // 官方本来就不会复制：
            // FluctuationParam
            // LetterBoxParam
            */

            OnCopyFrom(src);
        }

        /// <summary>
        /// 将参数写入运行时 GallopImageEffect。
        /// 当前只写入 DofDiffusionBloomOverlay。
        /// </summary>
        public void CopyTo(GallopImageEffect target)
        {
            if (!target.IsInitialized)
                return;

            target.DofDiffusionBloomOverlayParam.Setup(
                DofDiffusionBloomOverlayParam);

            /*
            // 官方完整逻辑，当前暂不启用。
            target.GlobalFogParam.Setup(GlobalFogParam);
            target.SunShaftsParam.Setup(SunShaftsParam);
            target.TiltShiftParam.Setup(TiltShiftParam);

            target.IndirectLightShaftsParam.Setup(
                IndirectLightShaftsParam);

            target.RadialBlurParam.Setup(RadialBlurParam);

            target.LensDistortionParam.Setup(
                LensDistortionParam);

            target.FluctuationParam.Setup(
                FluctuationParam);

            target.ColorCorrectionParam.Setup(
                ColorCorrectionParam);

            target.ChromaticAberration.Setup(
                ChromaticAberrationParam);

            target.ToneCurveParam.Setup(ToneCurveParam);
            target.ExposureParam.Setup(ExposureParam);
            target.RainSplash.Setup(RainSplashParam);

            // 官方本来就不会写入：
            // HatchingParam
            // LetterBoxParam
            */

            OnCopyTo(target);
        }

        /// <summary>
        /// 在两个参数资源之间插值。
        /// 当前只插值 DofDiffusionBloomOverlay。
        /// </summary>
        public void Lerp(
            GallopImageEffectParameter src1,
            GallopImageEffectParameter src2,
            float t)
        {
            DofDiffusionBloomOverlayParam.Lerp(
                src1.DofDiffusionBloomOverlayParam,
                src2.DofDiffusionBloomOverlayParam,
                t);

            /*
            // 官方完整逻辑，当前暂不启用。
            GlobalFogParam.Lerp(
                src1.GlobalFogParam,
                src2.GlobalFogParam,
                t);

            SunShaftsParam.Lerp(
                src1.SunShaftsParam,
                src2.SunShaftsParam,
                t);

            TiltShiftParam.Lerp(
                src1.TiltShiftParam,
                src2.TiltShiftParam,
                t);

            IndirectLightShaftsParam.Lerp(
                src1.IndirectLightShaftsParam,
                src2.IndirectLightShaftsParam,
                t);

            RadialBlurParam.Lerp(
                src1.RadialBlurParam,
                src2.RadialBlurParam,
                t);

            LensDistortionParam.Lerp(
                src1.LensDistortionParam,
                src2.LensDistortionParam,
                t);

            ColorCorrectionParam.Lerp(
                src1.ColorCorrectionParam,
                src2.ColorCorrectionParam,
                t);

            ChromaticAberrationParam.Lerp(
                src1.ChromaticAberrationParam,
                src2.ChromaticAberrationParam,
                t);

            ToneCurveParam.Lerp(
                src1.ToneCurveParam,
                src2.ToneCurveParam,
                t);

            ExposureParam.Lerp(
                src1.ExposureParam,
                src2.ExposureParam,
                t);

            HatchingParam.Lerp(
                src1.HatchingParam,
                src2.HatchingParam,
                t);

            RainSplashParam.Lerp(
                src1.RainSplashParam,
                src2.RainSplashParam,
                t);

            // 官方本来就不会插值：
            // FluctuationParam
            // LetterBoxParam
            */

            OnLerp(src1, src2, t);
        }

        public GallopImageEffectParameter()
        {
            DofDiffusionBloomOverlayParam =
                new DofDiffusionBloomOverlayParam();

            /*
            // 官方完整构造逻辑，当前暂不启用。
            GlobalFogParam = new GlobalFogParam();
            SunShaftsParam = new SunShaftsParam();
            TiltShiftParam = new TiltShiftParam();

            IndirectLightShaftsParam =
                new IndirectLightShaftsParam();

            RadialBlurParam = new RadialBlurParam();

            LensDistortionParam =
                new LensDistortionParam();

            ColorCorrectionParam =
                new ColorCorrectionParam();

            FluctuationParam =
                new FluctuationParam();

            ChromaticAberrationParam =
                new ChromaticAberrationParameter();

            ToneCurveParam = new ToneCurveParam();
            ExposureParam = new ExposureParam();
            HatchingParam = new HatchingParam();
            LetterBoxParam = new LetterBoxParam();
            RainSplashParam = new RainSplashParam();
            */
        }
    }
}