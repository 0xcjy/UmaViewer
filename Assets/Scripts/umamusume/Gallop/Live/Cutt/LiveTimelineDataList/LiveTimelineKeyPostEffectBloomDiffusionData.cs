using System;
using Gallop.ImageEffect;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyPostEffectBloomDiffusionData
        : LiveTimelineKeyWithInterpolate
    {
        private const int ATTR_ENABLE_DIFFUSION = 65536;
        private const int ATTR_ENABLE_BLOOM = 131072;

        public override LiveTimelineKeyDataType dataType
        {
            get
            {
                return LiveTimelineKeyDataType.PostEffectBloomDiffusion;
            }
        }

        public bool IsEnabledDiffusion
        {
            get
            {
                return ((int)attribute & ATTR_ENABLE_DIFFUSION) != 0;
            }
            set
            {
                int attr = (int)attribute;

                if (value)
                    attr |= ATTR_ENABLE_DIFFUSION;
                else
                    attr &= ~ATTR_ENABLE_DIFFUSION;

                attribute = (LiveTimelineKeyAttribute)attr;
            }
        }

        public bool IsEnabledBloom
        {
            get
            {
                return ((int)attribute & ATTR_ENABLE_BLOOM) != 0;
            }
            set
            {
                int attr = (int)attribute;

                if (value)
                    attr |= ATTR_ENABLE_BLOOM;
                else
                    attr &= ~ATTR_ENABLE_BLOOM;

                attribute = (LiveTimelineKeyAttribute)attr;
            }
        }

        public float bloomDofWeight;
        public float threshold;
        public float intensity;
        public float BloomBlurSize;

        public DofDiffusionBloomOverlayParam.BloomScreenBlendMode
            BloomBlendMode;

        public float diffusionBlurSize;
        public float diffusionBright;
        public float diffusionThreshold;
        public float diffusionSaturation;
        public float diffusionContrast;

        public LiveTimelineKeyPostEffectBloomDiffusionData()
        {
        }
    }
}