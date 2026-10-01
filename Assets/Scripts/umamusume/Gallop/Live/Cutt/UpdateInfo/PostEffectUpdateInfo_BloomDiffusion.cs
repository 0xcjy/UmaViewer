using Gallop.ImageEffect;

namespace Gallop.Live.Cutt
{
    public struct PostEffectUpdateInfo_BloomDiffusion
    {
        public bool IsEnabledBloom;

        public float bloomDofWeight;
        public float threshold;
        public float intensity;
        public float BloomBlurSize;
        public DofDiffusionBloomOverlayParam.BloomScreenBlendMode BloomBlendMode;

        public bool IsEnabledDiffusion;

        public float diffusionBlurSize;
        public float diffusionBright;
        public float diffusionThreshold;
        public float diffusionSaturation;
        public float diffusionContrast;
    }
}