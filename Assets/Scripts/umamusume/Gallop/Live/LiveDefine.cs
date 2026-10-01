using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop
{
    public static class LiveDefine
    {
        public const int LIVE_CHARACTER_MAX = 20;
        public const int LIVE_THEATER_CHARACTER_MAX = 18;
        public const int BACK_DANCER_RANK = 4;

        public const int LIVE_CHARACTER_DIMENSION = 0;
        public const int LIVE_DRESS_DIMENSION = 1;

        public const int LIVE_VARIATION_ID_INVALID = -1;
        public const int LIVE_VARIATION_ID_DEFAULT = 0;

        public const int BG_VARIATION_ID_INVALID = -1;
        public const int BG_VARIATION_ID_DEFAULT = 0;
        public const int BG_VARIATION_ID_REDUCED_AUDIENCE = 9;

        public const int OKE_VARIATION_ID_INVALID = 0;
        public const int OKE_VARIATION_ID_CALL_ON = 1;
        public const int OKE_VARIATION_ID_CALL_OFF = 2;

        public const int SONG_VARIATION_ID_DEFAULT = 1;
        public const float CAMERA_ORTHOGRAPHIC_SIZE_DEFAULT = 5f;
        public const int SHEET_VARIATION_ID_DEFAULT = 0;
        public const int SCREEN_MODE_DEFAULT = 0;
        public const int SCREEN_MODE_FULL_PORTRAIT = 1;
        public const int RANDOM_SEED = 12345;

        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");

        public enum TrainerCameraOperation
        {
            Swipe,
            Gyro
        }

        public enum TrainerCameraRotateDirection
        {
            Normal,
            Reverse
        }

        public enum TrainerCameraOperationSensitivity
        {
            Min,
            Max = 5,
            Default = 3
        }

        public enum LightBlendMode
        {
            Addition,     // 0 -> One / One
            Multiply,     // 1 -> DstColor / One
            SoftAddition, // 2 -> OneMinusDstColor / One
            AlphaBlend,   // 3 -> SrcAlpha / OneMinusSrcAlpha
            Multiply0,    // 4 -> DstColor / Zero
            Multiply2x    // 5 -> DstColor / SrcColor
        }

        // 官方拼写就是 HightLightBlendMode
        public enum HightLightBlendMode
        {
            Addition,
            Multiply,
            Normal
        }

        public enum Easing
        {
            Linear,
            EaseIn,
            EaseOut,
            EaseInOut
        }

        public enum ConstraintObjectType
        {
            Props,
            MiniChara
        }

        public static bool IsValidLiveVariationId(int liveVariationId)
        {
            return liveVariationId >= LIVE_VARIATION_ID_DEFAULT;
        }

        public static bool IsValidBgVariationId(int bgVariationId)
        {
            return bgVariationId >= BG_VARIATION_ID_DEFAULT;
        }

        public static bool IsValidOkeVariationId(int okeVariationId)
        {
            return okeVariationId >= OKE_VARIATION_ID_INVALID;
        }

        public static bool TrySetMaterialBlendMode(Material material, BlendMode srcBlend, BlendMode dstBlend)
        {
            if (material == null)
                return false;

            //官方会先HasProperty,不是随便写
            if (!material.HasProperty(SrcBlendId))
                return false;

            if (!material.HasProperty(DstBlendId))
                return false;

            material.SetFloat(SrcBlendId, (float)srcBlend);
            material.SetFloat(DstBlendId, (float)dstBlend);

            return true;
        }

        public static bool TrySetLightBlendModeMaterialProperty(LightBlendMode mode, Material material)
        {
            switch (mode)
            {
                case LightBlendMode.Addition:
                    // 官方 case 0: 1, 1
                    return TrySetMaterialBlendMode(material,BlendMode.One,BlendMode.One);

                case LightBlendMode.Multiply:
                    // case 1: 2, 1这个就是LightBlinkBlend正常要用的 DstColor/One
                    return TrySetMaterialBlendMode(material,BlendMode.DstColor,BlendMode.One);

                case LightBlendMode.SoftAddition:
                    // case 2: 4, 1
                    return TrySetMaterialBlendMode(material,BlendMode.OneMinusDstColor,BlendMode.One);

                case LightBlendMode.AlphaBlend:
                    // 官方 case 3: 5, 10
                    return TrySetMaterialBlendMode(material,BlendMode.SrcAlpha,BlendMode.OneMinusSrcAlpha);

                case LightBlendMode.Multiply0:
                    // 官方 case 4: 2, 0
                    return TrySetMaterialBlendMode(material,BlendMode.DstColor,BlendMode.Zero);

                case LightBlendMode.Multiply2x:
                    // 官方 case 5: 2, 3
                    return TrySetMaterialBlendMode(material,BlendMode.DstColor,BlendMode.SrcColor);

                default:
                    //官方default会设置One/One,但返回 false
                    TrySetMaterialBlendMode(material,BlendMode.One,BlendMode.One);
                    return false;
            }
        }
    }
}