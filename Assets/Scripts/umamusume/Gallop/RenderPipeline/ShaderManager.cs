using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop
{
    /// <summary>
    /// Gallop ShaderManager 的 Dof/Bloom 兼容性实现。
    ///
    /// 此版本直接与项目的 UmaAssetManager 集成：
    /// - UmaViewerMain 通过 UmaAssetManager 加载 AbList["shader"]
    /// - UmaAssetManager 将 shader.a 保留在其 NeverUnload 缓存中。
    /// - ShaderManager 直接从该 AssetBundle 解析官方的 Gallop 路径
    ///
    /// 不使用 Resources.Load 和额外的 AssetBundle 注册
/// </summary>
    public class ShaderManager
    {
        private const ShaderKinds ShaderPassTypeNormalGroupBegin = ShaderKinds.SoftShadowEffect;
        private const ShaderKinds ShaderPassTypeNormalGroupEnd = ShaderKinds.VertexColor;
        private const ShaderKinds ShaderPassTypeUIGroupBegin = ShaderKinds.UICircle;
        private const ShaderKinds ShaderPassTypeUIGroupEnd = ShaderKinds.UIAnimationTransparentUI;

        private const string SHADER_KEYWORD_ALL = "_";
        private const string ResourceFolderPath = "Resources/";

        public const string CHARACTER_MASK_COLOR_KEYWORD = "USE_MASK_COLOR";
        public const string CHARACTER_HOLOGRAM_KEYWORD = "USE_HOLOGRAM";
        public const string SPRITE_UV_ADJUST_TONE = "USE_ADJUST_TONE";

        public const int DEFAULT_CHARA_STENCIL_MASK = 100;
        public const int DRAWIN_MASK_CHARA_STENCIL_MASK = 200;
        public const int DEFAULT_MASK_STENCIL_MASK = 95;

        public const CompareFunction DEFAULT_CAHRA_STENCIL_COMPARE = CompareFunction.Equal;
        public const CompareFunction DEFAULT_PROP_STENCIL_COMPARE = CompareFunction.Always;
        public const StencilOp DEFAULT_PROP_STENCIL_OP = StencilOp.Replace;

        public const ShaderKinds CHARACTER_SHADER_BEGIN = ShaderKinds.CharacterUnlit;
        public const ShaderKinds CHARACTER_SHADER_END = ShaderKinds.CharacterUnlitZIgnoreTransparent;

        public const string UVSCROLL_SURPPORTER_SHADER_NAME = "DistortionPaletteUVScrollCD";
        public const string TRAIL_SURPPORTER_SHADER_NAME = "DistortionPaletteCDTrail";

        private static readonly string[] _shaderFileNames =
        {
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorFogProjection", //   0 BgLightmapColorFogProjection
        "Assets/_Gallop/Resources/Shader/3D/BG/BgRedAlphaGreenColorShadowFogUVScroll", //   1 BgRedAlphaGreenColorShadowFogUVScroll
        "Assets/_Gallop/Resources/Shader/3D/BG/BgRedAlphaGreenColorShadowFog", //   2 BgRedAlphaGreenColorShadowFog
        "Assets/_Gallop/Resources/Shader/3D/BG/BgShadowOnly", //   3 BgShadowOnly
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapFog", //   4 BgLightmapFog
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorFog", //   5 BgLightmapColorFog
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorFogFur", //   6 BgLightmapColorFogFur
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorFogShadow", //   7 BgLightmapColorFogShadow
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorCutoffFog", //   8 BgLightmapColorCutoffFog
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorCutoffFogDepth", //   9 BgLightmapColorCutoffFogDepth
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorCutoffFogGrass", //  10 BgLightmapColorCutoffFogGrass
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorCutoffFogGrassDepth", //  11 BgLightmapColorCutoffFogGrassDepth
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorCutoffFogZTestDepth", //  12 BgLightmapColorCutoffFogZTestDepth
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorNoSub", //  13 BgLightmapColorNoSub
        "Assets/_Gallop/Resources/Shader/3D/BG/BgMirrorBall", //  14 BgMirrorBall
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightAlphaAddScroll", //  15 BgLightAlphaAddScroll
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorAlphaFog", //  16 BgLightmapColorAlphaFog
        "Assets/_Gallop/Resources/Shader/3D/BG/BgUnlitAlphaFog", //  17 BgUnlitAlphaFog
        "Assets/_Gallop/Resources/Shader/3D/BG/BgUnlitNoFog", //  18 BgUnlitNoFog
        "Assets/_Gallop/Resources/Shader/3D/BG/BgUnlitMirrorNoFog", //  19 BgUnlitMirrorNoFog
        "Assets/_Gallop/Resources/Shader/3D/BG/BgMultiplicativeShadow", //  20 BgMultiplicativeShadow
        "Assets/_Gallop/Resources/Shader/3D/BG/BgAudienceImpostors", //  21 BgAudienceImpostors
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLightmapColorAlphaFogIgnoreZ", //  22 BgLightmapColorAlphaFogIgnoreZ
        "Assets/_Gallop/Resources/Shader/3D/BG/BgUVAnimationLightmapColorCutoffFog", //  23 BgUvAnimationLightmapColorCutoffFog
        "Assets/_Gallop/Resources/Shader/3D/BG/BgUVAnimationLightmapColorCutoffFogFill", //  24 BgUvAnimationLightmapColorCutoffFogFill
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterUnlit", //  25 CharacterUnlit
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterUnlitMultiply", //  26 CharacterUnlitMultiply
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonTSER", //  27 CharacterToonTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSER", //  28 CharacterNolineToonTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSERCf", //  29 CharacterNolineToonTSERCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherToonTSER", //  30 CharacterDitherToonTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherNolineToonTSER", //  31 CharacterDitherNolineToonTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonTSERA", //  32 CharacterToonTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSERA", //  33 CharacterNolineToonTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSERACf", //  34 CharacterNolineToonTSERACf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonTSER", //  35 CharacterAlphaNolineToonTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonTSERCf", //  36 CharacterAlphaNolineToonTSERCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaDitherNolineToonTSER", //  37 CharacterAlphaDitherNolineToonTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonTSERA", //  38 CharacterAlphaNolineToonTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonTSERACf", //  39 CharacterAlphaNolineToonTSERACf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonBehindTSER", //  40 CharacterAlphaNolineToonBehindTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSERB", //  41 CharacterNolineToonTSERB
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSERAB", //  42 CharacterNolineToonTSERAB
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherNolineToonTSERB", //  43 CharacterDitherNolineToonTSERB
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonTSEREm", //  44 CharacterToonTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSEREm", //  45 CharacterNolineToonTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSEREmCf", //  46 CharacterNolineToonTSEREmCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherToonTSEREm", //  47 CharacterDitherToonTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherNolineToonTSEREm", //  48 CharacterDitherNolineToonTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonTSERAEm", //  49 CharacterToonTSERAEm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSERAEm", //  50 CharacterNolineToonTSERAEm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSERAEmCf", //  51 CharacterNolineToonTSERAEmCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonTSERRfl", //  52 CharacterToonTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSERRfl", //  53 CharacterNolineToonTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonTSERRfl", //  54 CharacterAlphaNolineToonTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonTSERARfl", //  55 CharacterToonTSERARfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonTSERARfl", //  56 CharacterNolineToonTSERARfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonTSERARfl", //  57 CharacterAlphaNolineToonTSERARfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonBehindTSERRfl", //  58 CharacterAlphaNolineToonBehindTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonMayu", //  59 CharacterToonMayu
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonMayuCf", //  60 CharacterToonMayuCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherToonMayu", //  61 CharacterDitherToonMayu
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonMayuA", //  62 CharacterToonMayuA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonMayuACf", //  63 CharacterToonMayuACf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonEyeT", //  64 CharacterToonEyeT
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonEyeTRef", //  65 CharacterToonEyeTRef
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonEyeTRefCf", //  66 CharacterToonEyeTRefCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherToonEyeT", //  67 CharacterDitherToonEyeT
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonEyeTA", //  68 CharacterToonEyeTA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonEyeTACf", //  69 CharacterToonEyeTACf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonFaceTSER", //  70 CharacterToonFaceTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonFaceTSEREm", //  71 CharacterToonFaceTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonFaceTSER", //  72 CharacterNolineToonFaceTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonFaceTSERCf", //  73 CharacterNolineToonFaceTSERCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonFaceTSEREm", //  74 CharacterNolineToonFaceTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonFaceTSEREmCf", //  75 CharacterNolineToonFaceTSEREmCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonFaceTSERA", //  76 CharacterToonFaceTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonFaceTSERAEm", //  77 CharacterToonFaceTSERAEm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonFaceTSERA", //  78 CharacterNolineToonFaceTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonFaceTSERACf", //  79 CharacterNolineToonFaceTSERACf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonFaceTSERAEm", //  80 CharacterNolineToonFaceTSERAEm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonFaceTSERAEmCf", //  81 CharacterNolineToonFaceTSERAEmCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherToonFaceTSER", //  82 CharacterDitherToonFaceTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherToonFaceTSEREm", //  83 CharacterDitherToonFaceTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonHairTSER", //  84 CharacterToonHairTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonHairTSEREm", //  85 CharacterToonHairTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonHairTSERRfl", //  86 CharacterToonHairTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSER", //  87 CharacterNolineToonHairTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSERCf", //  88 CharacterNolineToonHairTSERCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSEREm", //  89 CharacterNolineToonHairTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSEREmCf", //  90 CharacterNolineToonHairTSEREmCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSERRfl", //  91 CharacterNolineToonHairTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonHairTSERA", //  92 CharacterToonHairTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonHairTSERAEm", //  93 CharacterToonHairTSERAEm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonHairTSERARfl", //  94 CharacterToonHairTSERARfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSERA", //  95 CharacterNolineToonHairTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSERACf", //  96 CharacterNolineToonHairTSERACf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSERAEm", //  97 CharacterNolineToonHairTSERAEm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSERAEmCf", //  98 CharacterNolineToonHairTSERAEmCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonHairTSERARfl", //  99 CharacterNolineToonHairTSERARfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherToonHairTSER", // 100 CharacterDitherToonHairTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherToonHairTSEREm", // 101 CharacterDitherToonHairTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonHairTSER", // 102 CharacterAlphaNolineToonHairTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonHairTSERCf", // 103 CharacterAlphaNolineToonHairTSERCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonHairTSERRfl", // 104 CharacterAlphaNolineToonHairTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaDitherNolineToonHairTSER", // 105 CharacterAlphaDitherNolineToonHairTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonHairTSERA", // 106 CharacterAlphaNolineToonHairTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonHairTSERACf", // 107 CharacterAlphaNolineToonHairTSERACf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonHairTSERARfl", // 108 CharacterAlphaNolineToonHairTSERARfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonBehindHairTSER", // 109 CharacterAlphaNolineToonBehindHairTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAlphaNolineToonBehindHairTSERRfl", // 110 CharacterAlphaNolineToonBehindHairTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonColorTSER", // 111 CharacterToonColorTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonColorTSERA", // 112 CharacterToonColorTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonColorTSER", // 113 CharacterNolineToonColorTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterNolineToonColorTSERCf", // 114 CharacterNolineToonColorTSERCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherToonColorTSER", // 115 CharacterDitherToonColorTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonHairTSERNoOutlineCoord", // 116 CharacterToonHairTSERNoOutlineCoord
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonColorTSERNoOutlineCoord", // 117 CharacterToonColorTSERNoOutlineCoord
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonFaceTSERNoOutlineCoord", // 118 CharacterToonFaceTSERNoOutlineCoord
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterToonTSERNoOutlineCoord", // 119 CharacterToonTSERNoOutlineCoord
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterMultiplyCheek", // 120 CharacterMultiplyCheek
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterMultiplyCheekCf", // 121 CharacterMultiplyCheekCf
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterDitherMultiplyCheek", // 122 CharacterDitherMultiplyCheek
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterUnlitTear", // 123 CharacterUnlitTear
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterUnlitTransparent", // 124 CharacterUnlitTransparent
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterUnlitZIgnoreTransparent", // 125 CharacterUnlitZIgnoreTransparent
        "Assets/_Gallop/Resources/Shader/3D/Character/MiniCharaBody", // 126 MiniCharaBody
        "Assets/_Gallop/Resources/Shader/3D/Character/MiniCharaShadow", // 127 MiniCharaShadow
        "Assets/_Gallop/Resources/Shader/3D/Character/MiniCharaEye", // 128 MiniCharaEye
        "Assets/_Gallop/Resources/Shader/3D/Character/ParticleAttachableEffectFresnel", // 129 CharacterSweat
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterShadow", // 130 CharacterShadow
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSER", // 131 PropAlphaNolineToonTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSERA", // 132 PropAlphaNolineToonTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSERACf", // 133 PropAlphaNolineToonTSERACf
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSERAEm", // 134 PropAlphaNolineToonTSERAEm
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSERAEmCf", // 135 PropAlphaNolineToonTSERAEmCf
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSERARfl", // 136 PropAlphaNolineToonTSERARfl
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSERARflCf", // 137 PropAlphaNolineToonTSERARflCf
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSERCf", // 138 PropAlphaNolineToonTSERCf
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSEREm", // 139 PropAlphaNolineToonTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSEREmCf", // 140 PropAlphaNolineToonTSEREmCf
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSERRfl", // 141 PropAlphaNolineToonTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/PropAlphaNolineToonTSERRflCf", // 142 PropAlphaNolineToonTSERRflCf
        "Assets/_Gallop/Resources/Shader/3D/Character/PropToonR", // 143 PropToonR
        "Assets/_Gallop/Resources/Shader/3D/Character/PropToonTSER", // 144 PropToonTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/PropToonTSERA", // 145 PropToonTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/PropToonTSERAEm", // 146 PropToonTSERAEm
        "Assets/_Gallop/Resources/Shader/3D/Character/PropToonTSERARfl", // 147 PropToonTSERARfl
        "Assets/_Gallop/Resources/Shader/3D/Character/PropToonTSEREm", // 148 PropToonTSEREm
        "Assets/_Gallop/Resources/Shader/3D/Character/PropToonTSERRfl", // 149 PropToonTSERRfl
        "Assets/_Gallop/Resources/Shader/3D/Character/PropTransparentToonR", // 150 PropTransparentToonR
        "Assets/_Gallop/Resources/Shader/3D/Character/PropTransparentToonOutline", // 151 PropTransparentToonOutline
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterAura", // 152 CharacterAura
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/SoftShadowEffect", // 153 SoftShadowEffect
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/CastShadow", // 154 CastShadow
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/ReceiveMirror", // 155 ReceiveMirror
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/ProjectorShadow", // 156 ProjectorShadow
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/SimplePaint", // 157 SimplePaint
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/ColorCorrectionCurves", // 158 ColorCorrectionCurves
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/ColorCorrectionCurvesSimple", // 159 ColorCorrectionCurvesSimple
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/ColorCorrectionSelective", // 160 ColorCorrectionSelective
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/DepthOfField34", // 161 DepthOfField34
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/SeparableWeightedBlurDof34", // 162 SeparableWeightedBlurDof34
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/FastBloom", // 163 FastBloom
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/WeightedBlur", // 164 WeightedBlur
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/RadialBlur", // 165 RadialBlur
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/TiltShiftHdrLensBlur", // 166 TiltShiftHdrLensBlur
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/FlipScreen", // 167 FlipScreen
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/LensDistortion", // 168 LensDistortion
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Fluctuation", // 169 Fluctuation
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/ChromaticAberration", // 170 ChromaticAberration
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/ColorGrading", // 171 ColorGrading
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/AlphaMask", // 172 AlphaMask
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/ToneCurve", // 173 ToneCurve
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Exposure", // 174 Exposure
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Vortex", // 175 Vortex
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Aura", // 176 Aura
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/FilmRoll", // 177 FilmRoll
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Hatching", // 178 Hatching
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/BoldOutlineSDF/BoldOutlineSDF", // 179 BoldOutlineSDF
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/LetterBox", // 180 LetterBox
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/SimpleClear", // 181 SimpleClear
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/SunShaftsComposite", // 182 SunShaftsComposite
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/PostDofBloom_Rich", // 183 PostDofBloom_Rich
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/PostDiffusionBloom_Rich", // 184 PostDiffusionBloom_Rich
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/PostDiffusionDofBloom_Rich", // 185 PostDiffusionDofBloom_Rich
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/PostBloom_Rich", // 186 PostBloom_Rich
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/PostBlit_Rich", // 187 PostBlit_Rich
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/IndirectLightShafts", // 188 IndirectLightShafts
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/GlobalFog", // 189 GlobalFog
        "Assets/_Gallop/Resources/Shader/3D/ImageEffect/Rich/RainSplash", // 190 RainSplash
        "Assets/_Gallop/Resources/Shader/3D/MultiCamera/MultiCameraComposite", // 191 MultiCameraComposite
        "Assets/_Gallop/Resources/Shader/3D/MultiCamera/MultiCameraFinalComposite", // 192 MultiCameraFinalComposite
        "Assets/_Gallop/Resources/Shader/3D/MultiCamera/MultiCameraOneShotFade", // 193 MultiCameraOneShotFade
        "Assets/_Gallop/Resources/Shader/2D/MobileBlurCustom", // 194 MobileBlurCustom
        "Assets/_Gallop/Resources/Shader/2D/ZekkenFontDraw", // 195 ZekkenFontDraw
        "Assets/_Gallop/Resources/Shader/2D/SpriteUV", // 196 SpriteUV
        "Assets/_Gallop/Resources/Shader/2D/SpriteCutoff", // 197 SpriteCutoff
        "Assets/_Gallop/Resources/Shader/2D/BlendAlpha", // 198 BlendAlpha
        "Assets/_Gallop/Resources/Shader/2D/BakeIcon", // 199 BakeIcon
        "Assets/_Gallop/Resources/Shader/2D/Background3D", // 200 Background3D
        "Assets/_Gallop/Resources/Shader/2D/StoryStill/StoryStill-CharacterAdd", // 201 StoryStillCharacterAdd
        "Assets/_Gallop/Resources/Shader/2D/StoryStill/StoryStill-Fill", // 202 StoryStillFill
        "Assets/_Gallop/Resources/Shader/2D/TextureClipping", // 203 TextureClipping
        "Assets/_Gallop/Resources/Shader/2D/LayeredBackground", // 204 LayererdBackground
        "Assets/_Gallop/Resources/Shader/2D/UnlitGradientColor", // 205 UnlitGradientColor
        "Assets/_Gallop/Resources/Shader/3D/Pool/ReplacedWaterChara", // 206 ReplacedWaterChara
        "Assets/_Gallop/Resources/Shader/3D/Pool/BgSpecialWater", // 207 BgSpecialWater
        "Assets/_Gallop/Resources/Shader/3D/Pool/BgSpecialCaustics", // 208 BgSpecialCaustics
        "Assets/_Gallop/Resources/Shader/3D/Live/Chara/CharaProjectorShadow", // 209 CharaProjectorShadow
        "Assets/_Gallop/Resources/Shader/3D/Live/Chara/CharaShadow", // 210 CharaShadow
        "Assets/_Gallop/Resources/Shader/3D/Live/Cyalume/CyalumeDefault", // 211 CyalumeDefault
        "Assets/_Gallop/Resources/Shader/3D/Live/Cyalume/CyalumeDefault_hq", // 212 CyalumeDefault_hq
        "Assets/_Gallop/Resources/Shader/3D/Live/Cyalume/MobShadow", // 213 MobShadow
        "Assets/_Gallop/Resources/Shader/3D/Live/Particle/ParticleFlicker", // 214 ParticleFlicker
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageAlpha", // 215 StageAlpha
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageAlphaMask", // 216 StageAlphaMask
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageBeamLight", // 217 StageBeamLight
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageBeamLightCutoff", // 218 StageBeamLightCutoff
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageBeamLightFadeout", // 219 StageBeamLightFadeout
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageCutoffFog", // 220 StageCutoffFog
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefault", // 221 StageDefault
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultCutoff", // 222 StageDefaultCutoff
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultEnvMap", // 223 StageDefaultEnvMap
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultNoAmbient", // 224 StageDefaultNoAmbient
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultNoAmbientAlpha", // 225 StageDefaultNoAmbientAlpha
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultShadow", // 226 StageDefaultShadow
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultShadowNoAmbient", // 227 StageDefaultShadowNoAmbient
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultShadowPanel", // 228 StageDefaultShadowPanel
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultStencil", // 229 StageDefaultStencil
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultTransparent", // 230 StageDefaultTransparent
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultTransparentNoAmbient", // 231 StageDefaultTransparentNoAmbient
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultUVAmbient", // 232 StageDefaultUVAmbient
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultWave", // 233 StageDefaultWave
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDefaultWaveNoAmbient", // 234 StageDefaultWaveNoAmbient
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageDepthOnly", // 235 StageDepthOnly
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageGrass", // 236 StageGrass
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLaser", // 237 StageLaser
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightAdd1", // 238 StageLightAdd1
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightAdd1_UV", // 239 StageLightAdd1_UV
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightAdd1_UVAlphaMask", // 240 StageLightAdd1_UVAlphaMask
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightAdd1_UVAlphaMask_TransmittedLightMask", // 241 StageLightAdd1_UVAlphaMask_TransmittedLightMask
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightBlend", // 242 StageLightBlend
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightBlink", // 243 StageLightBlink
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightBlinkBlend", // 244 StageLightBlinkBlend
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightBlinkCutoff", // 245 StageLightBlinkCutoff
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightBlinkFadeout", // 246 StageLightBlinkFadeout
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageLightBlinkSimple", // 247 StageLightBlinkSimple
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageMonitor", // 248 StageMonitor
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageMonitorAdditive", // 249 StageMonitorAdditive
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageMonitorAdditiveProjector", // 250 StageMonitorAdditiveProjector
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageMonitorBlendProjectorTransparent", // 251 StageMonitorBlendProjectorTransparent
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageMonitorOverlayGlayAlpha", // 252 StageMonitorOverlayGlayAlpha
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageMultiUvsetsBlend", // 253 StageMultiUvsetsBlend
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageProjector", // 254 StageProjector
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageProjectorAnimVertexAlpha", // 255 StageProjectorAnimVertexAlpha
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageProjectorBlend", // 256 StageProjectorBlend
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageProjectorBlendAnimVertexAlpha", // 257 StageProjectorBlendAnimVertexAlpha
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageProjectorBlendVertexAlpha", // 258 StageProjectorBlendVertexAlpha
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageProjectorVertexAlpha", // 259 StageProjectorVertexAlpha
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageShadow", // 260 StageShadow
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageSubTexDistortionTransparentWave", // 261 StageSubTexDistortionTransparentWave
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageSubTexDistortionWave", // 262 StageSubTexDistortionWave
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageSunCatcher", // 263 StageSunCatcher
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageTraceparentAlphaAdditiveWave", // 264 StageTraceparentAlphaAdditiveWave
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageTraceparentAlphaWave", // 265 StageTraceparentAlphaWave
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageTransmittedLightMask", // 266 StageTransmittedLightMask
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageUnlitVcolorNoFog", // 267 StageUnlitVcolorNoFog
        "Assets/_Gallop/Resources/Shader/3D/Live/Stage/StageVertexAlphaAdditiveUVNoAmbient", // 268 StageVertexAlphaAdditiveUVNoAmbient
        "Assets/_Gallop/Resources/Shader/3D/MultiCamera/RaceMultiCameraMask", // 269 RaceMultiCameraMask
        "Assets/_Gallop/Resources/Shader/3D/MultiCamera/RaceMultiCameraColorMask", // 270 RaceMultiCameraColorMask
        "Assets/_Gallop/Resources/Shader/3D/MultiCamera/RaceMultiCameraColorMaskComposite", // 271 RaceMultiCameraColorMaskComposite
        "Assets/_Gallop/Resources/Shader/3D/MultiCamera/RacePostCameraMask", // 272 PostMarkerCamera
        "Assets/_Gallop/Resources/Shader/UI/UICircle", // 273 UICircle
        "Assets/_Gallop/Resources/Shader/UI/UI-Default-ScrollAlphaTargetVertical", // 274 UIDefaultScrollAlphaTargetVertical
        "Assets/_Gallop/Resources/Shader/UI/UI-SmoothMask", // 275 UISmoothMask
        "Assets/_Gallop/Resources/Shader/UI/UI-Blur", // 276 UIBlur
        "Assets/_Gallop/Resources/Shader/UI/UI-TextureBlur", // 277 UITextureBlur
        "Assets/_Gallop/Resources/Shader/UI/UISimpleCopy", // 278 UISimpleCopy
        "Assets/_Gallop/Resources/Shader/UI/UI-Bloom", // 279 UIBloom
        "Assets/_Gallop/Resources/Shader/UI/UIImageEffect", // 280 UIImageEffect
        "Assets/_Gallop/Resources/Shader/UI/UI-Default-SpriteAlphaMask", // 281 UIDefaultSpriteAlphaMask
        "Assets/_Gallop/Resources/Shader/UI/UI-Default-BlendMulti", // 282 UIDefaultBlendMulti
        "Assets/_Gallop/Resources/Shader/UI/UI-Default-FillAddColor", // 283 UIDefaultFillAddColor
        "Assets/_Gallop/Resources/Shader/UI/UI-Default-CaptureDisplay", // 284 UIDefaultCaptureDisplay
        "Assets/_Gallop/Resources/Shader/UI/UI-ColorAdd", // 285 UIColorAdd
        "Assets/_Gallop/Resources/Shader/UI/UI-Default-Monochrome", // 286 UIDefaultMonochrome
        "Assets/_Gallop/Resources/Shader/UI/UIColorAddtive", // 287 UIColorAddtive
        "Assets/_Gallop/Resources/Shader/UI/UI-Default-Additive", // 288 UIDefaultAdditive
        "Assets/_Gallop/Resources/Shader/UI/UI-IgnoreAlpha", // 289 UIIgnoreAlpha
        "Assets/_Gallop/Resources/Shader/UIAnimation/Additive", // 290 UIAnimationAdditive
        "Assets/_Gallop/Resources/Shader/UIAnimation/Transparent", // 291 UIAnimationTransparent
        "Assets/_Gallop/Resources/Shader/UIAnimation/TransparentUI", // 292 UIAnimationTransparentUI
        "Assets/_Gallop/Resources/Shader/Effect/Additive", // 293 EffAdditive
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveAlphaMask", // 294 EffAdditiveAlphaMask
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveAlphaMaskMulti", // 295 EffAdditiveAlphaMaskMulti
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveCutoffCD", // 296 EffAdditiveCutoffCD
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveCutoffCDGroundFade", // 297 EffAdditiveCutoffCDGroundFade
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveDistortionCD", // 298 EffAdditiveDistortionCD
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveDistortionCDStencil", // 299 EffAdditiveDistortionCDStencil
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveDistortionPaletteCD", // 300 EffAdditiveDistortionPaletteCD
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveDistortionPaletteCDGroundFade", // 301 EffAdditiveDistortionPaletteCDGroundFade
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveDistortionPaletteMaskCD", // 302 EffAdditiveDistortionPaletteMaskCD
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveFresnel", // 303 EffAdditiveFresnel
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveGroundFade", // 304 EffAdditiveGroundFade
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveMultiply", // 305 EffAdditiveMultiply
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveMultiplyDistortion", // 306 EffAdditiveMultiplyDistortion
        "Assets/_Gallop/Resources/Shader/Effect/AdditivePaletteCD", // 307 EffAdditivePaletteCD
        "Assets/_Gallop/Resources/Shader/Effect/AdditivePaletteCDGroundFade", // 308 EffAdditivePaletteCDGroundFade
        "Assets/_Gallop/Resources/Shader/Effect/AdditivePaletteCDStencil", // 309 EffAdditivePaletteCDStencil
        "Assets/_Gallop/Resources/Shader/Effect/AdditivePolarCoordinate", // 310 EffAdditivePolarCoordinate
        "Assets/_Gallop/Resources/Shader/Effect/AdditivePolarCoordinateCD", // 311 EffAdditivePolarCoordinateCD
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveStencil", // 312 EffAdditiveStencil
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveUVScrollCD", // 313 EffAdditiveUVScrollCD
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveUVScrollCDStencil", // 314 EffAdditiveUVScrollCDStencil
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveUVScrollCutoffCD", // 315 EffAdditiveUVScrollCutoffCD
        "Assets/_Gallop/Resources/Shader/Effect/AdditiveZIgnore", // 316 EffAdditiveZIgnore
        "Assets/_Gallop/Resources/Shader/Effect/AlphatestCutoff", // 317 EffAlphatestCutoff
        "Assets/_Gallop/Resources/Shader/Effect/Multiply", // 318 EffMultiply
        "Assets/_Gallop/Resources/Shader/Effect/RaceMarker", // 319 EffRaceMarker
        "Assets/_Gallop/Resources/Shader/Effect/Transparent", // 320 EffTransparent
        "Assets/_Gallop/Resources/Shader/Effect/TransparentAlphaMask", // 321 EffTransparentAlphaMask
        "Assets/_Gallop/Resources/Shader/Effect/TransparentAlphaMaskMulti", // 322 EffTransparentAlphaMaskMulti
        "Assets/_Gallop/Resources/Shader/Effect/TransparentColorMaskRGBA", // 323 EffTransparentColorMaskRGBA
        "Assets/_Gallop/Resources/Shader/Effect/TransparentCutoffCD", // 324 EffTransparentCutoffCD
        "Assets/_Gallop/Resources/Shader/Effect/TransparentCutoffCDGroundFade", // 325 EffTransparentCutoffCDGroundFade
        "Assets/_Gallop/Resources/Shader/Effect/TransparentDistortionCD", // 326 EffTransparentDistortionCD
        "Assets/_Gallop/Resources/Shader/Effect/TransparentDistortionCDStencil", // 327 EffTransparentDistortionCDStencil
        "Assets/_Gallop/Resources/Shader/Effect/TransparentDistortionPaletteCD", // 328 EffTransparentDistortionPaletteCD
        "Assets/_Gallop/Resources/Shader/Effect/TransparentDistortionPaletteCDGroundFade", // 329 EffTransparentDistortionPaletteCDGroundFade
        "Assets/_Gallop/Resources/Shader/Effect/TransparentDistortionPaletteMaskCD", // 330 EffTransparentDistortionPaletteMaskCD
        "Assets/_Gallop/Resources/Shader/Effect/TransparentEnvMapNoColor", // 331 EffTransparentEnvMapNoColor
        "Assets/_Gallop/Resources/Shader/Effect/TransparentFresnel", // 332 EffTransparentFresnel
        "Assets/_Gallop/Resources/Shader/Effect/TransparentGallopFog", // 333 EffTransparentGallopFog
        "Assets/_Gallop/Resources/Shader/Effect/TransparentGroundFade", // 334 EffTransparentGroundFade
        "Assets/_Gallop/Resources/Shader/Effect/TransparentGroundFadeGallopFog", // 335 EffTransparentGroundFadeGallopFog
        "Assets/_Gallop/Resources/Shader/Effect/TransparentMizutamaFade", // 336 EffTransparentMizutamaFade
        "Assets/_Gallop/Resources/Shader/Effect/TransparentPaletteCD", // 337 EffTransparentPaletteCD
        "Assets/_Gallop/Resources/Shader/Effect/TransparentPaletteCDGroundFade", // 338 EffTransparentPaletteCDGroundFade
        "Assets/_Gallop/Resources/Shader/Effect/TransparentPaletteCDStencil", // 339 EffTransparentPaletteCDStencil
        "Assets/_Gallop/Resources/Shader/Effect/TransparentPolarCoordinate", // 340 EffTransparentPolarCoordinate
        "Assets/_Gallop/Resources/Shader/Effect/TransparentPolarCoordinateCD", // 341 EffTransparentPolarCoordinateCD
        "Assets/_Gallop/Resources/Shader/Effect/TransparentStencil", // 342 EffTransparentStencil
        "Assets/_Gallop/Resources/Shader/Effect/TransparentUVScrollCD", // 343 EffTransparentUVScrollCD
        "Assets/_Gallop/Resources/Shader/Effect/TransparentUVScrollCDStencil", // 344 EffTransparentUVScrollCDStencil
        "Assets/_Gallop/Resources/Shader/Effect/TransparentUVScrollCustom", // 345 EffTransparentUVScrollCustom
        "Assets/_Gallop/Resources/Shader/Effect/TransparentUVScrollCutoffCD", // 346 EffTransparentUVScrollCutoffCD
        "Assets/_Gallop/Resources/Shader/Effect/TransparentZIgnore", // 347 EffTransparentZIgnore
        "Assets/_Gallop/Resources/Shader/Effect/StencilMask", // 348 EffStencilMask
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/BlendProjector", // 349 BlendProjector
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/AddProjector", // 350 AddProjector
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/MulProjector", // 351 MulProjector
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/CutoffBlendProjector", // 352 CutoffBlendProjector
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/CutoffAddProjector", // 353 CutoffAddProjector
        "Assets/_Gallop/Resources/Shader/3D/MirrorAndShadow/CutoffMulProjector", // 354 CutoffMulProjector
        "Assets/_Gallop/Resources/Shader/3D/BG/BgLensFlare", // 355 BgLensFlare
        "Assets/_Gallop/Resources/Shader/Utils/TextureComposite", // 356 TextureComposite
        "Assets/_Gallop/Resources/Shader/Utils/VertexColor", // 357 VertexColor
        "Assets/_Gallop/Resources/Shader/Utils/DrawBorder", // 358 DrawBorder
        "Assets/_Gallop/Resources/Shader/Utils/DepthCopy", // 359 DepthCopy
        "Assets/_Gallop/Resources/Shader/Utils/ColoredFrame", // 360 ColoredFrame
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterHologramToonTSERA", // 361 CharacterHologramToonTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterHologramToonEyeTA", // 362 CharacterHologramToonEyeTA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterHologramToonFaceTSERA", // 363 CharacterHologramToonFaceTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterHologramToonHairTSERA", // 364 CharacterHologramToonHairTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterHologramToonMayuA", // 365 CharacterHologramToonMayuA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterHologramToonColorTSERA", // 366 CharacterHologramToonColorTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/HologramToonOnly", // 367 HologramToonOnly
        "Assets/_Gallop/Resources/Shader/3D/Character/PropHologramToonTSERA", // 368 PropHologramToonTSERA
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterScreenMappingToonTSER", // 369 CharacterScreenMappingToonTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterScreenMappingToonColorTSER", // 370 CharacterScreenMappingToonColorTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterScreenMappingToonEyeT", // 371 CharacterScreenMappingToonEyeT
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterScreenMappingToonMayu", // 372 CharacterScreenMappingToonMayu
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterScreenMappingToonFaceTSER", // 373 CharacterScreenMappingToonFaceTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/CharacterScreenMappingToonHairTSER", // 374 CharacterScreenMappingToonHairTSER
        "Assets/_Gallop/Resources/Shader/3D/Character/EyeHighlightOnly", // 375 EyeHiglightOnly
        "Assets/_Gallop/Resources/Shader/Utils/FixColor", // 376 DefaultShader
        };
        
        private static readonly string[] _computeShaderFileNames =
        {
        "Assets/_Gallop/Resources/Shader/Compute/DrawBorderCS", // 0 DrawBorder
        };

        // The original build initializes this from an 18-int metadata blob.
        // It is not needed by this Dof/Bloom-only usable implementation because
        // GetShader loads on demand instead of blocking on the official warmup phases.
        private static readonly ShaderKinds[] AFTERDOWNLOAD_WARMUPSHADER_ARRAY =
            Array.Empty<ShaderKinds>();

        private static readonly ShaderKinds[] DofBloomShaderKinds =
        {
            ShaderKinds.DepthOfField34,
            ShaderKinds.SeparableWeightedBlurDof34,
            ShaderKinds.FastBloom,
            ShaderKinds.WeightedBlur,
            ShaderKinds.SimpleClear,
            ShaderKinds.PostDofBloom_Rich,
            ShaderKinds.PostDiffusionBloom_Rich,
            ShaderKinds.PostDiffusionDofBloom_Rich,
            ShaderKinds.PostBloom_Rich,
            ShaderKinds.PostBlit_Rich,
            // REPORT §10.4-C3：预热集补 RadialBlur / ColorCorrection 系（原生
            // SetupShaderVariant RVA 0x19ce4c0 走 DofDiffusionBloomOverlayPass.
            // AddShaderVariant 覆盖整个后处理链）。此前缺失导致首次进 Live 时
            // Radial/Color pass 需等 GetShader 惰性加载，可能首帧 setup failed。
            ShaderKinds.RadialBlur,
            ShaderKinds.ColorCorrectionCurves,
            ShaderKinds.ColorCorrectionCurvesSimple,
            ShaderKinds.ColorCorrectionSelective,
            ShaderKinds.ToneCurve,
            ShaderKinds.Exposure,
            ShaderKinds.SunShaftsComposite,
            ShaderKinds.IndirectLightShafts,
            ShaderKinds.MultiCameraComposite,
            ShaderKinds.MultiCameraFinalComposite,
            ShaderKinds.MultiCameraOneShotFade,
        };

        private static bool _isInit;
        private static bool _isWarmUpShader;
        private static bool _isWarmUpShaderAfterDL;
        private static bool _isWarmupRequest;
        private static bool _loggedBundleNotReady;
        private static bool _loggedBundleReady;

        private static readonly string[] _propertyIdNames;
        private static readonly int[] _propertyIds;
        private static readonly Shader[] _shaders;
        private static readonly ComputeShader[] _computeShaders;
        private static ShaderVariantCollection _shaderVariantCollection;

        private const string GameShaderBundleEntryName = "shader";

        static ShaderManager()
        {
            _propertyIdNames = new string[(int)PropertyId.Max];
            _propertyIds = new int[(int)PropertyId.Max];
            _shaders = new Shader[(int)ShaderKinds.Max];
            _computeShaders =
                new ComputeShader[(int)ComputeShaderKind.Max];

            for (int i = 0; i < _propertyIdNames.Length; i++)
            {
                _propertyIdNames[i] =
                    ((PropertyId)i).ToString();
            }
        }

        public ShaderManager()
        {
        }

        public static bool IsWarmupShader => _isWarmUpShader;
        public static bool IsWarmupShaderAfterDL => _isWarmUpShaderAfterDL;
        public static string[] ShaderFileNames => _shaderFileNames;

        public static void InitManager()
        {
            if (_isInit)
            {
                return;
            }

            SetShaderPropertyId();
            _isInit = true;
        }

        private static void SetShaderPropertyId()
        {
            for (int i = 0; i < _propertyIdNames.Length; i++)
            {
                _propertyIds[i] = Shader.PropertyToID(_propertyIdNames[i]);
            }
        }

        public static int GetPropertyId(PropertyId id)
        {
            InitManager();
            return _propertyIds[(int)id];
        }

        public static string GetPropertyName(PropertyId id)
        {
            InitManager();
            return _propertyIdNames[(int)id];
        }

        private static void SetupShaderVariant()
        {
            // Dof/Bloom-only project integration.
            // Official ShaderManager also adds RadialBlurPass and
            // ColorCorrectionPass here.
            RenderPipeline.DofDiffusionBloomOverlayPass .AddShaderVariant(_shaderVariantCollection);
        }

        private static bool IsShaderVariant(ShaderKinds kind)
        {
            return RenderPipeline
                .DofDiffusionBloomOverlayPass
                .IsVaritantShader(kind);
        }

        private static void AfterWarmupAllShader()
        {
            _shaderVariantCollection =
                new ShaderVariantCollection();

            //SetupShaderVariant();

            _shaderVariantCollection.WarmUp();

            // Official build calls:
            // GraphicSettings.InitializeAfterShaderLoad();
            // It is omitted because this project does not currently provide
            // the official GraphicSettings implementation.
            _isWarmUpShader = true;
        }
        public static void WarmupDofBloomShader()
        {
            if (_isWarmUpShader)
            {
                return;
            }

            _isWarmupRequest = true;

            if (GetGameShaderBundle() == null)
            {
                Debug.LogError(
                    "[ShaderManager] shader AB 尚未加载。");
                return;
            }

            // Preload the current Dof/Bloom subset from shader.a.
            for (int i = 0; i < DofBloomShaderKinds.Length; i++)
            {
                LoadShader(DofBloomShaderKinds[i]);
            }

            // Compatibility behavior for the Dof/Bloom-only build:
            // all relevant shaders come from the same already-loaded shader.a.
            _isWarmUpShaderAfterDL = true;

            AfterWarmupAllShader();
        }

        private static void WarmupAdd(ShaderVariantCollection shaderVariant,ShaderKinds kind,string[][] keywordArray,string[][] uiKeywordArray,string[] defaultKeywordArray)
        {
            Shader shader = LoadShader(kind);

            // 这是项目适配保护。官方没有此判断，
            // 但 AB 缺失时避免直接崩溃。
            if (shaderVariant == null || shader == null)
            {
                return;
            }

            string[] allKeywordArray =
            {
                SHADER_KEYWORD_ALL
            };

            int kindValue = (int)kind;

            // 官方范围：153～357。
            if ((uint)(kindValue - 153) <= 0xCC)
            {
                shaderVariant.Add(new ShaderVariantCollection.ShaderVariant(shader,(PassType)0,allKeywordArray));

                return;
            }

            if (keywordArray != null)
            {
                for (int i = 0; i < keywordArray.Length; i++)
                {
                    string[] keywords = keywordArray[i];

                    shaderVariant.Add( new ShaderVariantCollection.ShaderVariant(shader,(PassType)13, keywords));

                    shaderVariant.Add( new ShaderVariantCollection.ShaderVariant( shader,(PassType)0, keywords));
                }
            }

            shaderVariant.Add( new ShaderVariantCollection.ShaderVariant(shader, (PassType)8, allKeywordArray));
        }

        private static string[][] MakeShaderKeywordPattern()
        {
            // The official array contains one additional global keyword.
            // Dof/Bloom variant shaders are excluded from the generic warmup
            // path and are added by DofDiffusionBloomOverlayPass instead.
            return new string[][]
            {
                new string[]
                {
                    SHADER_KEYWORD_ALL
                }
            };
        }

        private static string[][] MakeShaderKeywordPatternAfter()
        {
            return new string[][]
            {
                new string[]
                {
                    SHADER_KEYWORD_ALL
                }
            };
        }

        private static string[][] MakeUIShaderKeywordPattern()
        {
            return new string[][]
            {
                new string[]
                {
                    SHADER_KEYWORD_ALL
                }
            };
        }

        public static IEnumerator SliceWarmupAllShaderAfterDL()
        {
            if (_isWarmUpShaderAfterDL)
            {
                yield break;
            }

            _isWarmupRequest = true;

            if (GetGameShaderBundle() == null)
            {
                Debug.LogError(
                    "[ShaderManager] shader AB 尚未加载。");
                yield break;
            }

            // 项目兼容的 Dof/Bloom 子集,完整的官方实现使用了未知的 18 条目 AFTERDOWNLOAD_WARMUPSHADER_ARRAY,按三个一组排列
            for (int i = 0; i < DofBloomShaderKinds.Length; i++)
            {
                LoadShader(DofBloomShaderKinds[i]);
                yield return null;
            }

            _isWarmUpShaderAfterDL = true;
        }

        private static bool IsExistAfterDownloadShader(ShaderKinds kind)
        {
            for (int i = 0; i < AFTERDOWNLOAD_WARMUPSHADER_ARRAY.Length; i++)
            {
                if (AFTERDOWNLOAD_WARMUPSHADER_ARRAY[i] == kind)
                {
                    return true;
                }
            }

            return false;
        }

        public static IEnumerator SliceWarmupAllShader()
        {
            if (_isWarmUpShader)
            {
                yield break;
            }

            WarmupDofBloomShader();
            yield break;
        }

        private static void WarmupAllShader()
        {
            WarmupDofBloomShader();
        }

        public static bool IsShaderBundleReady
        {
            get
            {
                return GetGameShaderBundle() != null;
            }
        }

        private static AssetBundle GetGameShaderBundle()
        {
            // 尽可能使用实际的 UmaDatabaseEntry，因为UmaAssetManager按 entry.Name 而非 AbList键进行缓存。
            global::UmaViewerMain main = global::UmaViewerMain.Instance;

            // Renderer features may be created before UmaAssetManager's
            // singleton during pipeline deserialization. Treat that state as
            // "not ready" instead of dereferencing a missing manager.
            if (global::UmaAssetManager.instance == null)
                return null;

            if (main != null && main.AbList != null)
            {
                global::UmaDatabaseEntry shaderEntry;

                if (main.AbList.TryGetValue( GameShaderBundleEntryName, out shaderEntry) && shaderEntry != null)
                {
                    AssetBundle bundle = null;
                    if (global::UmaAssetManager.Exist(shaderEntry))
                        bundle = global::UmaAssetManager.Get(shaderEntry);

                    if (bundle != null)
                    {
                        return bundle;
                    }
                }
            }

            // Fallback for projects where the manifest entry name is "shader".
            return global::UmaAssetManager.Exist(GameShaderBundleEntryName)
                ? global::UmaAssetManager.Get(GameShaderBundleEntryName)
                : null;
        }

        /// <summary>
        /// Call this after:
        /// UmaAssetManager.LoadAssetBundle(AbList["shader"], true)
        /// </summary>
        public static void LoadShaderBundle()
        {
            if (GetGameShaderBundle() != null)
            {
                LogBundleReadyOnce();
                return;
            }

            UmaViewerMain main = UmaViewerMain.Instance;

            if (main == null || main.AbList == null)
            {
                Debug.LogError("[ShaderManager] UmaViewerMain 未初始化。");
                return;
            }

            UmaDatabaseEntry shaderEntry;

            if (!main.AbList.TryGetValue(GameShaderBundleEntryName, out shaderEntry) || shaderEntry == null)
            {
                Debug.LogError("[ShaderManager] AbList 中没有 shader。");
                return;
            }

            AssetBundle bundle = UmaAssetManager.LoadAssetBundle(shaderEntry, true);
            if (bundle != null)
            {
                _loggedBundleNotReady = false;
                LogBundleReadyOnce();
            }
            else if (!_loggedBundleNotReady)
            {
                _loggedBundleNotReady = true;
                Debug.LogWarning("[ShaderManager] shader bundle request did not produce a loaded bundle; will retry.");
            }
        }

        private static void LogBundleReadyOnce()
        {
            if (_loggedBundleReady)
                return;

            _loggedBundleReady = true;
            Debug.Log("[ShaderManager] shader bundle is available to recovered post effects.");
        }

        public static string GetShaderFileName(ShaderKinds kinds)
        {
            return _shaderFileNames[(int)kinds];
        }

        private static string GetShaderFileNameWithoutRootPath(
            ShaderKinds kinds)
        {
            return RemoveResourcesRoot(GetShaderFileName(kinds));
        }

        public static Shader GetShader(ShaderKinds kinds)
        {
            InitManager();

            int index = (int)kinds;
            if ((uint)index >= (uint)_shaders.Length)
            {
                return null;
            }

            Shader shader = _shaders[index];
            if (shader == null)
            {
                shader = LoadShader(kinds);
            }

            return shader;
        }

        private static Shader LoadShader(ShaderKinds kinds)
        {
            int index = (int)kinds;
            if ((uint)index >= (uint)_shaders.Length)
            {
                return null;
            }

            AssetBundle bundle = GetGameShaderBundle();
            if (bundle == null)
            {
                Debug.LogError(
                    "[ShaderManager] Cannot load " + kinds +
                    ": UmaAssetManager has not loaded the shader AB.");
                return null;
            }

            string officialPath = GetShaderFileName(kinds);
            Shader shader = LoadFromGameShaderBundle<Shader>( bundle, officialPath, ".shader");

            _shaders[index] = shader;

            if (shader == null)
            {
                Debug.LogError(
                    "[ShaderManager] Shader not found in " +
                    bundle.name + ": " + officialPath +
                    " (" + kinds + ", index=" + index + ")");
            }

            return shader;
        }

        public static string GetShaderFileName(
            ComputeShaderKind kind)
        {
            return _computeShaderFileNames[(int)kind];
        }

        private static string GetShaderFileNameWithoutRootPath(
            ComputeShaderKind kind)
        {
            return RemoveResourcesRoot(GetShaderFileName(kind));
        }

        public static ComputeShader LoadComputeShader(
            ComputeShaderKind kind)
        {
            int index = (int)kind;
            if ((uint)index >= (uint)_computeShaders.Length)
            {
                return null;
            }

            AssetBundle bundle = GetGameShaderBundle();
            if (bundle == null)
            {
                Debug.LogError(
                    "[ShaderManager] Cannot load ComputeShader " +
                    kind + ": shader AB is not loaded.");
                return null;
            }

            string officialPath = GetShaderFileName(kind);
            ComputeShader shader =
                LoadFromGameShaderBundle<ComputeShader>(
                    bundle,
                    officialPath,
                    ".compute");

            _computeShaders[index] = shader;

            if (shader == null)
            {
                Debug.LogError(
                    "[ShaderManager] ComputeShader not found in " +
                    bundle.name + ": " + officialPath);
            }

            return shader;
        }

        public static ComputeShader GetShader(
            ComputeShaderKind kind)
        {
            int index = (int)kind;
            if ((uint)index >= (uint)_computeShaders.Length)
            {
                return null;
            }

            ComputeShader shader = _computeShaders[index];

            return shader != null
                ? shader
                : LoadComputeShader(kind);
        }

        private static T LoadFromGameShaderBundle<T>(AssetBundle bundle, string officialPath, string extension)
            where T : UnityEngine.Object
        {
            if (bundle == null ||
                string.IsNullOrEmpty(officialPath))
            {
                return null;
            }

            string assetName = MakeBundleAssetName(
                officialPath,
                extension);

            T asset = bundle.LoadAsset<T>(assetName);
            if (asset != null)
            {
                return asset;
            }

            // Fallback for bundles whose stored extension or casing differs.
            // This path only runs when the official lowercase path failed.
            string normalizedOfficial =
                NormalizeBundleAssetName(officialPath);

            string[] allAssetNames = bundle.GetAllAssetNames();

            for (int i = 0; i < allAssetNames.Length; i++)
            {
                string candidate = allAssetNames[i];

                if (NormalizeBundleAssetName(candidate) !=
                    normalizedOfficial)
                {
                    continue;
                }

                asset = bundle.LoadAsset<T>(candidate);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private static string MakeBundleAssetName(string officialPath, string extension)
        {
            string path = officialPath
                .Replace('\\', '/')
                .Trim()
                .TrimStart('/')
                .ToLowerInvariant();

            if (!string.IsNullOrEmpty(extension) &&
                !path.EndsWith(
                    extension,
                    StringComparison.OrdinalIgnoreCase))
            {
                path += extension;
            }

            return path;
        }

        private static string NormalizeBundleAssetName(
            string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string normalized = path
                .Replace('\\', '/')
                .Trim()
                .TrimStart('/')
                .ToLowerInvariant();

            string[] extensions =
            {
                ".shader",
                ".compute",
                ".asset",
            };

            for (int i = 0; i < extensions.Length; i++)
            {
                if (normalized.EndsWith(
                    extensions[i],
                    StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized.Substring(
                        0,
                        normalized.Length -
                        extensions[i].Length);

                    break;
                }
            }

            return normalized;
        }

        public static void Release()
        {
            Array.Clear(_shaders, 0, _shaders.Length);
            Array.Clear( _computeShaders, 0, _computeShaders.Length);

            if (_shaderVariantCollection != null)
            {
                _shaderVariantCollection.Clear();
                _shaderVariantCollection = null;
            }

            // shader.a is owned by UmaAssetManager and remains loaded.
            // Reset warmup state so the variants can be rebuilt later.
            _isWarmupRequest = false;
            _isWarmUpShaderAfterDL = false;
            _isWarmUpShader = false;
        }

        public static void AddShaderVariant(ShaderVariantCollection collection, ShaderKinds shaderKind, string[] keywordArray)
        {
            if (collection == null)
            {
                return;
            }

            Shader shader = GetShader(shaderKind);
            if (shader == null)
            {
                return;
            }

            collection.Add( new ShaderVariantCollection.ShaderVariant( shader, PassType.Normal, keywordArray ?? Array.Empty<string>()));
        }

        private static string RemoveResourcesRoot(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return fullPath;
            }

            int index = fullPath.IndexOf(ResourceFolderPath, StringComparison.Ordinal);

            if (index < 0)
            {
                return fullPath;
            }

            return fullPath.Remove(0, index + ResourceFolderPath.Length);
        }
        

        public enum ShaderKinds
        {
            BgLightmapColorFogProjection,
            BgRedAlphaGreenColorShadowFogUVScroll,
            BgRedAlphaGreenColorShadowFog,
            BgShadowOnly,
            BgLightmapFog,
            BgLightmapColorFog,
            BgLightmapColorFogFur,
            BgLightmapColorFogShadow,
            BgLightmapColorCutoffFog,
            BgLightmapColorCutoffFogDepth,
            BgLightmapColorCutoffFogGrass,
            BgLightmapColorCutoffFogGrassDepth,
            BgLightmapColorCutoffFogZTestDepth,
            BgLightmapColorNoSub,
            BgMirrorBall,
            BgLightAlphaAddScroll,
            BgLightmapColorAlphaFog,
            BgUnlitAlphaFog,
            BgUnlitNoFog,
            BgUnlitMirrorNoFog,
            BgMultiplicativeShadow,
            BgAudienceImpostors,
            BgLightmapColorAlphaFogIgnoreZ,
            BgUvAnimationLightmapColorCutoffFog,
            BgUvAnimationLightmapColorCutoffFogFill,
            CharacterUnlit,
            CharacterUnlitMultiply,
            CharacterToonTSER,
            CharacterNolineToonTSER,
            CharacterNolineToonTSERCf,
            CharacterDitherToonTSER,
            CharacterDitherNolineToonTSER,
            CharacterToonTSERA,
            CharacterNolineToonTSERA,
            CharacterNolineToonTSERACf,
            CharacterAlphaNolineToonTSER,
            CharacterAlphaNolineToonTSERCf,
            CharacterAlphaDitherNolineToonTSER,
            CharacterAlphaNolineToonTSERA,
            CharacterAlphaNolineToonTSERACf,
            CharacterAlphaNolineToonBehindTSER,
            CharacterNolineToonTSERB,
            CharacterNolineToonTSERAB,
            CharacterDitherNolineToonTSERB,
            CharacterToonTSEREm,
            CharacterNolineToonTSEREm,
            CharacterNolineToonTSEREmCf,
            CharacterDitherToonTSEREm,
            CharacterDitherNolineToonTSEREm,
            CharacterToonTSERAEm,
            CharacterNolineToonTSERAEm,
            CharacterNolineToonTSERAEmCf,
            CharacterToonTSERRfl,
            CharacterNolineToonTSERRfl,
            CharacterAlphaNolineToonTSERRfl,
            CharacterToonTSERARfl,
            CharacterNolineToonTSERARfl,
            CharacterAlphaNolineToonTSERARfl,
            CharacterAlphaNolineToonBehindTSERRfl,
            CharacterToonMayu,
            CharacterToonMayuCf,
            CharacterDitherToonMayu,
            CharacterToonMayuA,
            CharacterToonMayuACf,
            CharacterToonEyeT,
            CharacterToonEyeTRef,
            CharacterToonEyeTRefCf,
            CharacterDitherToonEyeT,
            CharacterToonEyeTA,
            CharacterToonEyeTACf,
            CharacterToonFaceTSER,
            CharacterToonFaceTSEREm,
            CharacterNolineToonFaceTSER,
            CharacterNolineToonFaceTSERCf,
            CharacterNolineToonFaceTSEREm,
            CharacterNolineToonFaceTSEREmCf,
            CharacterToonFaceTSERA,
            CharacterToonFaceTSERAEm,
            CharacterNolineToonFaceTSERA,
            CharacterNolineToonFaceTSERACf,
            CharacterNolineToonFaceTSERAEm,
            CharacterNolineToonFaceTSERAEmCf,
            CharacterDitherToonFaceTSER,
            CharacterDitherToonFaceTSEREm,
            CharacterToonHairTSER,
            CharacterToonHairTSEREm,
            CharacterToonHairTSERRfl,
            CharacterNolineToonHairTSER,
            CharacterNolineToonHairTSERCf,
            CharacterNolineToonHairTSEREm,
            CharacterNolineToonHairTSEREmCf,
            CharacterNolineToonHairTSERRfl,
            CharacterToonHairTSERA,
            CharacterToonHairTSERAEm,
            CharacterToonHairTSERARfl,
            CharacterNolineToonHairTSERA,
            CharacterNolineToonHairTSERACf,
            CharacterNolineToonHairTSERAEm,
            CharacterNolineToonHairTSERAEmCf,
            CharacterNolineToonHairTSERARfl,
            CharacterDitherToonHairTSER,
            CharacterDitherToonHairTSEREm,
            CharacterAlphaNolineToonHairTSER,
            CharacterAlphaNolineToonHairTSERCf,
            CharacterAlphaNolineToonHairTSERRfl,
            CharacterAlphaDitherNolineToonHairTSER,
            CharacterAlphaNolineToonHairTSERA,
            CharacterAlphaNolineToonHairTSERACf,
            CharacterAlphaNolineToonHairTSERARfl,
            CharacterAlphaNolineToonBehindHairTSER,
            CharacterAlphaNolineToonBehindHairTSERRfl,
            CharacterToonColorTSER,
            CharacterToonColorTSERA,
            CharacterNolineToonColorTSER,
            CharacterNolineToonColorTSERCf,
            CharacterDitherToonColorTSER,
            CharacterToonHairTSERNoOutlineCoord,
            CharacterToonColorTSERNoOutlineCoord,
            CharacterToonFaceTSERNoOutlineCoord,
            CharacterToonTSERNoOutlineCoord,
            CharacterMultiplyCheek,
            CharacterMultiplyCheekCf,
            CharacterDitherMultiplyCheek,
            CharacterUnlitTear,
            CharacterUnlitTransparent,
            CharacterUnlitZIgnoreTransparent,
            MiniCharaBody,
            MiniCharaShadow,
            MiniCharaEye,
            CharacterSweat,
            CharacterShadow,
            PropAlphaNolineToonTSER,
            PropAlphaNolineToonTSERA,
            PropAlphaNolineToonTSERACf,
            PropAlphaNolineToonTSERAEm,
            PropAlphaNolineToonTSERAEmCf,
            PropAlphaNolineToonTSERARfl,
            PropAlphaNolineToonTSERARflCf,
            PropAlphaNolineToonTSERCf,
            PropAlphaNolineToonTSEREm,
            PropAlphaNolineToonTSEREmCf,
            PropAlphaNolineToonTSERRfl,
            PropAlphaNolineToonTSERRflCf,
            PropToonR,
            PropToonTSER,
            PropToonTSERA,
            PropToonTSERAEm,
            PropToonTSERARfl,
            PropToonTSEREm,
            PropToonTSERRfl,
            PropTransparentToonR,
            PropTransparentToonOutline,
            CharacterAura,
            SoftShadowEffect,
            CastShadow,
            ReceiveMirror,
            ProjectorShadow,
            SimplePaint,
            ColorCorrectionCurves,
            ColorCorrectionCurvesSimple,
            ColorCorrectionSelective,
            DepthOfField34,
            SeparableWeightedBlurDof34,
            FastBloom,
            WeightedBlur,
            RadialBlur,
            TiltShiftHdrLensBlur,
            FlipScreen,
            LensDistortion,
            Fluctuation,
            ChromaticAberration,
            ColorGrading,
            AlphaMask,
            ToneCurve,
            Exposure,
            Vortex,
            Aura,
            FilmRoll,
            Hatching,
            BoldOutlineSDF,
            LetterBox,
            SimpleClear,
            SunShaftsComposite,
            PostDofBloom_Rich,
            PostDiffusionBloom_Rich,
            PostDiffusionDofBloom_Rich,
            PostBloom_Rich,
            PostBlit_Rich,
            IndirectLightShafts,
            GlobalFog,
            RainSplash,
            MultiCameraComposite,
            MultiCameraFinalComposite,
            MultiCameraOneShotFade,
            MobileBlurCustom,
            ZekkenFontDraw,
            SpriteUV,
            SpriteCutoff,
            BlendAlpha,
            BakeIcon,
            Background3D,
            StoryStillCharacterAdd,
            StoryStillFill,
            TextureClipping,
            LayererdBackground,
            UnlitGradientColor,
            ReplacedWaterChara,
            BgSpecialWater,
            BgSpecialCaustics,
            CharaProjectorShadow,
            CharaShadow,
            CyalumeDefault,
            CyalumeDefault_hq,
            MobShadow,
            ParticleFlicker,
            StageAlpha,
            StageAlphaMask,
            StageBeamLight,
            StageBeamLightCutoff,
            StageBeamLightFadeout,
            StageCutoffFog,
            StageDefault,
            StageDefaultCutoff,
            StageDefaultEnvMap,
            StageDefaultNoAmbient,
            StageDefaultNoAmbientAlpha,
            StageDefaultShadow,
            StageDefaultShadowNoAmbient,
            StageDefaultShadowPanel,
            StageDefaultStencil,
            StageDefaultTransparent,
            StageDefaultTransparentNoAmbient,
            StageDefaultUVAmbient,
            StageDefaultWave,
            StageDefaultWaveNoAmbient,
            StageDepthOnly,
            StageGrass,
            StageLaser,
            StageLightAdd1,
            StageLightAdd1_UV,
            StageLightAdd1_UVAlphaMask,
            StageLightAdd1_UVAlphaMask_TransmittedLightMask,
            StageLightBlend,
            StageLightBlink,
            StageLightBlinkBlend,
            StageLightBlinkCutoff,
            StageLightBlinkFadeout,
            StageLightBlinkSimple,
            StageMonitor,
            StageMonitorAdditive,
            StageMonitorAdditiveProjector,
            StageMonitorBlendProjectorTransparent,
            StageMonitorOverlayGlayAlpha,
            StageMultiUvsetsBlend,
            StageProjector,
            StageProjectorAnimVertexAlpha,
            StageProjectorBlend,
            StageProjectorBlendAnimVertexAlpha,
            StageProjectorBlendVertexAlpha,
            StageProjectorVertexAlpha,
            StageShadow,
            StageSubTexDistortionTransparentWave,
            StageSubTexDistortionWave,
            StageSunCatcher,
            StageTraceparentAlphaAdditiveWave,
            StageTraceparentAlphaWave,
            StageTransmittedLightMask,
            StageUnlitVcolorNoFog,
            StageVertexAlphaAdditiveUVNoAmbient,
            RaceMultiCameraMask,
            RaceMultiCameraColorMask,
            RaceMultiCameraColorMaskComposite,
            PostMarkerCamera,
            UICircle,
            UIDefaultScrollAlphaTargetVertical,
            UISmoothMask,
            UIBlur,
            UITextureBlur,
            UISimpleCopy,
            UIBloom,
            UIImageEffect,
            UIDefaultSpriteAlphaMask,
            UIDefaultBlendMulti,
            UIDefaultFillAddColor,
            UIDefaultCaptureDisplay,
            UIColorAdd,
            UIDefaultMonochrome,
            UIColorAddtive,
            UIDefaultAdditive,
            UIIgnoreAlpha,
            UIAnimationAdditive,
            UIAnimationTransparent,
            UIAnimationTransparentUI,
            EffAdditive,
            EffAdditiveAlphaMask,
            EffAdditiveAlphaMaskMulti,
            EffAdditiveCutoffCD,
            EffAdditiveCutoffCDGroundFade,
            EffAdditiveDistortionCD,
            EffAdditiveDistortionCDStencil,
            EffAdditiveDistortionPaletteCD,
            EffAdditiveDistortionPaletteCDGroundFade,
            EffAdditiveDistortionPaletteMaskCD,
            EffAdditiveFresnel,
            EffAdditiveGroundFade,
            EffAdditiveMultiply,
            EffAdditiveMultiplyDistortion,
            EffAdditivePaletteCD,
            EffAdditivePaletteCDGroundFade,
            EffAdditivePaletteCDStencil,
            EffAdditivePolarCoordinate,
            EffAdditivePolarCoordinateCD,
            EffAdditiveStencil,
            EffAdditiveUVScrollCD,
            EffAdditiveUVScrollCDStencil,
            EffAdditiveUVScrollCutoffCD,
            EffAdditiveZIgnore,
            EffAlphatestCutoff,
            EffMultiply,
            EffRaceMarker,
            EffTransparent,
            EffTransparentAlphaMask,
            EffTransparentAlphaMaskMulti,
            EffTransparentColorMaskRGBA,
            EffTransparentCutoffCD,
            EffTransparentCutoffCDGroundFade,
            EffTransparentDistortionCD,
            EffTransparentDistortionCDStencil,
            EffTransparentDistortionPaletteCD,
            EffTransparentDistortionPaletteCDGroundFade,
            EffTransparentDistortionPaletteMaskCD,
            EffTransparentEnvMapNoColor,
            EffTransparentFresnel,
            EffTransparentGallopFog,
            EffTransparentGroundFade,
            EffTransparentGroundFadeGallopFog,
            EffTransparentMizutamaFade,
            EffTransparentPaletteCD,
            EffTransparentPaletteCDGroundFade,
            EffTransparentPaletteCDStencil,
            EffTransparentPolarCoordinate,
            EffTransparentPolarCoordinateCD,
            EffTransparentStencil,
            EffTransparentUVScrollCD,
            EffTransparentUVScrollCDStencil,
            EffTransparentUVScrollCustom,
            EffTransparentUVScrollCutoffCD,
            EffTransparentZIgnore,
            EffStencilMask,
            BlendProjector,
            AddProjector,
            MulProjector,
            CutoffBlendProjector,
            CutoffAddProjector,
            CutoffMulProjector,
            BgLensFlare,
            TextureComposite,
            VertexColor,
            DrawBorder,
            DepthCopy,
            ColoredFrame,
            CharacterHologramToonTSERA,
            CharacterHologramToonEyeTA,
            CharacterHologramToonFaceTSERA,
            CharacterHologramToonHairTSERA,
            CharacterHologramToonMayuA,
            CharacterHologramToonColorTSERA,
            HologramToonOnly,
            PropHologramToonTSERA,
            CharacterScreenMappingToonTSER,
            CharacterScreenMappingToonColorTSER,
            CharacterScreenMappingToonEyeT,
            CharacterScreenMappingToonMayu,
            CharacterScreenMappingToonFaceTSER,
            CharacterScreenMappingToonHairTSER,
            EyeHiglightOnly,
            DefaultShader,
            ApplicationMax,
            Max = 377,
            Invalid = -1
        }

        public enum ComputeShaderKind
        {
            DrawBorder,
            Max
        }

        public enum PropertyId
        {
            _Global_FogColor,
            _Global_FogMinDistance,
            _Global_FogLength,
            _Global_MaxDensity,
            _Global_MaxHeight,
            _Global_FogWorld_Origin,
            _GlobalTimeScale,
            _Global_LightmapColor,
            _Global_LightmapShadowColor,
            _Global_LightmapDensityAddColor,
            _Global_LightmapModulateColor,
            _GlobalDirtColor,
            _GlobalDirtRimSpecularColor,
            _GlobalDirtToonColor,
            _GlobalScreenUVScrollParam,
            _GlobalInvertCulling,
            _ProjectionViewProj,
            _CharaColor,
            _ToonDarkColor,
            _ToonBrightColor,
            _VertexColorToonPower,
            _RimColor,
            _RimStep,
            _RimFeather,
            _RimSpecRate,
            _RimShadow,
            _RimShadowRate,
            _RimShadowRate2,
            _EmissiveColor,
            _EmissiveIntensity,
            _EmissiveRimIntensity,
            _EmissiveRimPower,
            _IsEmissiveRimCenter,
            _RimHorizonOffset,
            _RimVerticalOffset,
            _RimColor2,
            _RimStep2,
            _RimFeather2,
            _RimSpecRate2,
            _RimHorizonOffset2,
            _RimVerticalOffset2,
            _LightProbeColor,
            _MainTex,
            _MainTex_ST,
            _TripleMaskMap,
            _OptionMaskMap,
            _UseOptionMaskMap,
            _SpecularColor,
            _SpecularPower,
            _ToonMap,
            _ToonColor,
            _ToonStep,
            _ToonFeather,
            _OutlineWidth,
            _OutlineColor,
            _EmissiveTex,
            _ZekkenNumberTex,
            _ZekkenNameTex,
            _ZekkenFontColor,
            _DirtTex,
            _DirtRate,
            _DitherTex,
            _UVEmissiveScroll,
            _UVEmissivePower,
            _EyePupliScale,
            _ClipColor,
            _CheekRate,
            _PreviousCheekTex,
            _AppTime,
            _BlurStrength,
            _BlurWidth,
            _CenterX,
            _CenterY,
            _ProjectionShadowMap,
            _CompTex,
            _Color,
            _AddColor,
            _FrontOffset,
            _BackOffset,
            _Offset,
            _StencilMask,
            _StencilComp,
            _StencilOp,
            _Stencil,
            _Cull,
            _DistortionScrollU,
            _DistortionScrollV,
            _AlphaParam,
            _ShadowColor,
            _EnableScrollAppTime,
            _ReflectionRate,
            _ReflectionTex,
            _ReflectionMask,
            _ReflectionOffsetScaleUV,
            _Speed,
            _Distortion,
            _UCross,
            _VCross,
            _U,
            _V,
            _NormalPower,
            _WaveDistortionPower,
            _WaveClearly,
            _WaveDiffusion,
            _WaveDecline,
            _IsWorldWave,
            _WaveDir,
            _WaveFreq,
            _WaveSpeed,
            _WaveSize,
            _PaintUV,
            _Brush,
            _BrushScale,
            _ControlColor,
            _InputTex,
            _PrevTex,
            _Prev2Tex,
            _FootTex,
            _VOffset,
            _DistanceAlphaFadeNear,
            _DistanceAlphaFadeFar,
            _DistanceAlphaFadeAlpha,
            _ColorFade,
            _BlinkLightColor,
            _CutoffHeight,
            _FadeoutHeightStart,
            _FadeoutHeightEnd,
            _FadeoutHeightLength,
            _HighParam1,
            _HighParam2,
            _MainParam,
            _TexScrollParam,
            _FixProjection,
            _CameraLength,
            _BgClipTexture,
            _BgClipUVOffset,
            _BgClipUVScale,
            _BgClipRGBThreshold,
            _UseOriginalDirectionalLight,
            _OriginalDirectionalLightDir,
            _FaceCenterPos,
            _FaceUp,
            _FaceForward,
            _CheekPretenseThreshold,
            _NosePretenseThreshold,
            _NoseVisibility,
            _CylinderBlend,
            _HairNormalBlend,
            _MulColor0,
            _MulColor1,
            _MulColor2,
            _ColorPower,
            _ColorClampPower,
            _AmbientColor,
            _BrightColor,
            _BaseColor,
            _BorderLightDir,
            _LocalTimer,
            _RandomValue,
            _BlurRadius4,
            _SunPosition,
            _Power,
            _SunColor,
            _ColorBuffer,
            _CenterMultiplex,
            _CenterBrightness,
            _BlackLevel,
            _KomorebiRate,
            _BlurSize,
            _BlurArea,
            _Params,
            _Blurred,
            _RgbTex,
            _ZCurve,
            _RgbDepthTex,
            _Saturation,
            selColor,
            targetColor,
            offsets,
            _bloomDofWeight,
            _CurveParams,
            _InvRenderTargetSize,
            _TapLowBackground,
            _Bloom,
            _Parameter,
            _PixelSize,
            _ColorParam,
            _DepthPower,
            _DepthPowerFront,
            _DepthPowerBack,
            _DepthCancelRect,
            _DepthCancelBlendLength,
            _PostFilmPower,
            _PostFilmOffsetParam,
            _PostFilmOptionParam,
            _PostFilmColor0,
            _PostFilmColor1,
            _PostFilmColor2,
            _PostFilmColor3,
            _TapHigh,
            _TapLowForeground,
            _ForegroundBlurExtrude,
            _TapMedium,
            _TapLow,
            _dofForegroundSize,
            _texMovie,
            _texMovieMask,
            _movieScale,
            _movieOffset,
            _colorBlendFactor,
            _BloomHighRange,
            _bloomHighRangeIntensity,
            _BlurParam,
            _BlurParamEx,
            _BlurTex,
            _BlurStartArea,
            _BlurEndArea,
            _BlurPower,
            _Gain,
            _CameraDepthTexture,
            _PostFilmRollParameter,
            _PostFilmScaleParameter,
            _PostFilmIsUVMovieNoScale,
            _PostFilmIsInverseVignette,
            _PostFilmIsAlphaMasking,
            _BloomIsScreenBlend,
            _PostFilmIsWithoutDepth,
            _SecondTex,
            _ThirdTex,
            _FourthTex,
            _FifthTex,
            _Angle,
            _Scale,
            _Alpha,
            _Alpha2,
            _MaskAlpha,
            _ShaftsTex1,
            _ShaftsTex2,
            _ShaftsTex3,
            _ShaftsTex4,
            _ShaftsTex5,
            _MaskTex,
            _FrustumCornersWS,
            _CameraWS,
            _HeightParams,
            _DistanceParams,
            _FogColor,
            _SceneFogParams,
            _SceneFogMode,
            _Height,
            _faceShadowColor,
            _faceShadowAlpha,
            _faceShadowEndY,
            _faceShadowLength,
            _faceShadowHeadMat,
            _blendRate,
            _blendScale,
            _UVScrollX,
            _UVScrollY,
            _UVScrollOffsetX,
            _UVScrollOffsetY,
            _LensDistortion_CenterScale,
            _LensDistortion_Amount,
            _DepthClip,
            _DistortionArea,
            _RotVolume,
            _HightLightParam,
            _HightLightColor,
            _OffsetColorX,
            _OffsetColorY,
            _Lut2D_Params,
            _Lut2D_Rate,
            _Lut2D,
            _MaxCoC,
            _Aspect,
            _DrawRect,
            _EdgeWidth,
            _lowResFixScaleX,
            _lowResFixScaleY,
            _lowResOffsetX,
            _lowResOffsetY,
            _fixScale,
            _FlipMaskAlpha,
            _FadeValue,
            _LineThickness,
            _MobMaskPosArray,
            _CyalumeMaskPosArray,
            _MobGroupMatrix,
            _CyalumeGroupMatrix,
            _ShadowFadeCenter,
            _ShadowFadeFront,
            _ShadowFadeParam,
            _ShadowStartColor,
            _ShadowEndColor,
            _IsBinarization,
            _DirShadowColor,
            _DirShadowIntensity,
            _ShadowFadeStart,
            _ShadowFadeLength,
            _AdditionalLightShadowRenderType,
            _ProjectorMulColor0,
            _ProjectorColorPower,
            _ProjectorColor,
            _ProjectorTex,
            _MaskColorTex,
            _MaskColorR1,
            _MaskColorR2,
            _MaskColorG1,
            _MaskColorG2,
            _MaskColorB1,
            _MaskColorB2,
            _MaskToonColorR1,
            _MaskToonColorR2,
            _MaskToonColorG1,
            _MaskToonColorG2,
            _MaskToonColorB1,
            _MaskToonColorB2,
            _BlendFactor,
            _UVAdjust,
            _MonitorWidth,
            _MonitorHeight,
            _Cutoff,
            _High0Tex,
            _High1Tex,
            _High2Tex,
            _Timer,
            _VertexLerpColor,
            _ColorLerpRate,
            _FlickerLightRate,
            _FlickerDarkRate,
            _CullMode,
            _ZTestMode,
            _ScrollX,
            _ScrollOffsetX,
            _TimeOffset,
            _VertexScale,
            _Contrast,
            _Gamma,
            _ZBufferParams,
            _AlphaArray,
            _SplitScreenVerticalNum,
            _SplitScreenHorizontalNum,
            _UVOffset,
            _Transform,
            _LineColor,
            _LineAntialiasing,
            _ScreenScale,
            _WindScale,
            _WindParam,
            _WindDirTex,
            _FadeColor,
            _FadeRate,
            _ZWrite,
            _ZTest,
            _CurveTex,
            _MinLevel,
            _MaxLevel,
            _MaskMinLevel,
            _MaskMaxLevel,
            _DepthMask,
            _NoiseTex,
            _NoiseTex_ST,
            _ShellAmountMax,
            _ShellAmount,
            _ShellStep,
            _ShellStepScale,
            _AlphaCutout,
            _Occlusion,
            _IsEnableGrassMove,
            _BaseMove,
            _WindFreq,
            _WindMove,
            _Brightness,
            _GrassFadeBeginDist,
            _GrassFadeEndDist,
            _GrassFadeOffsetPower,
            _SubTex,
            _LightTex,
            _MultimapRate,
            _Fresnel,
            _ViewDir,
            _AxisFramesX,
            _AxisFramesY,
            _UpperAngle,
            _LowerAngle,
            _LightmapColorRate,
            _VertexInput,
            unity_Projector,
            unity_ProjectorClip,
            _Temp,
            _UScrollSurpport,
            _VScrollSurpport,
            _TrailCustom1_b,
            _TrailCustom1_a,
            _TrailCustom2_r,
            _TrailCustom2_g,
            _TrailCustom2_b,
            _TrailCustom2_a,
            _UV,
            _Size,
            _ScreenColorPower,
            _EffectColorPower,
            _SrcBlend,
            _DstBlend,
            _ColorMask,
            _Silhouette,
            _TransmittedLightMaskScale,
            _ColorFlickerSpeed,
            _Direction,
            _ScanTiling,
            _ScanSpeed,
            _Grain,
            _RimPowerHolo,
            _RimHoloIntensity,
            _GlowTiling,
            _GlowSpeed,
            _GlitchSpeed,
            _GlitchIntensity,
            _IsZeroOneGlitch,
            _HologramEmission,
            _HologramAlpha,
            _HologramBaseColor,
            _HologramRimColor,
            _HologramGlowIntensity,
            _HologramNoiseTex,
            _HologramVoxel,
            _HologramVoxelSmoothness,
            _HologramVoxelIntensity,
            _GlitchNoiseIntensity,
            _ScreenMappingColor,
            _ScreenMappingColorPower,
            _ScreenMappingBlendMode,
            _ScreenMappingPatternType,
            _ScreenMappingPatternReverse,
            _ScreenMappingPatternInterval,
            _ScreenMappingPatternLength,
            _ScreenMappingAppliesNoseShadow,
            _ScreenMappingAppliesMouthShadow,
            _EnvRate,
            _EnvBias,
            _WaveAmplitude,
            _WaveLength,
            _MonitorColorIntensity,
            _FilmGrainTex,
            _SecondColor,
            _CutoffTex,
            _CutoffType,
            _NormalizeNormal,
            _DistMap_ST,
            _DistPower,
            _AuraWidth,
            _CopyTexClipInfo,
            _ScreenDrawArea,
            _ScreenDrawAreaMargin,
            _ScreenDrawAreaMarginColor,
            ScreenUVScrollParam,
            RollMaskColor,
            RollMaskFadeSize,
            NoisePower,
            IsNoiseRandom,
            NoiseColor,
            NoiseRange,
            ScanLineSpeed,
            ScanLineSize,
            ScanLinePower,
            ScanlineBlurSize,
            _VerticalPadding,
            _PaddingColor,
            _FovFactor,
            _MaskedTex,
            _CutOffAlpha,
            _BlendMode,
            _BlendAlpha,
            _NoiseMatrix,
            _StrokeTiling,
            _StrokeOffset,
            _CircleMaskOffset,
            _CircleMaskSize,
            _CircleMaskStretchRatio,
            _CircleMaskMatrix,
            _CircleMaskInnerRatio,
            _CircleMaskInnerEffect,
            _BoldOutlineWidth,
            _BorderValue,
            _BorderRange,
            _LetterBoxHeightRatio,
            _LetterBoxColor,
            _MirrorBallPosWS,
            _MirrorBallRotateAxis,
            _MirrorBallRotateValue,
            _MirrorBallScale,
            _MirrorBallProjectionRadius,
            _MirrorBallFallOffPower,
            _MirrorBallIsLoopRotation,
            _MirrorBallLoopRotationSpeed,
            _MirrorBallUVOffset,
            _MirrorBallUVScale,
            _MirrorBallUseCubeMap,
            _ShadowOffset,
            _AnimationTex,
            _FilterTex,
            _OffsetDirectionExpand,
            _OffsetScaleExpand,
            _OffsetDirectionBase,
            _OffsetScaleBase,
            _MinDepthValue,
            _MaxDepthValue,
            _MistNoiseTex,
            _MistColor,
            _MistIntensity,
            _MistDensity,
            _MistScrollSpeed,
            _MistBlurSize,
            _MistPower,
            _SplashNoiseTex,
            _SplashScrollSpeed,
            _SplashColor,
            _SplashThreshold,
            _SplashBaseRatio,
            _SplashIntensity,
            _SplashDensity,
            _SplashPower,
            _IsSplashOuter,
            _IsSplashInner,
            _AspectComp,
            _UnderlayColor,
            _UnderlayOffsetX,
            _UnderlayOffsetY,
            _UnderlayDilate,
            _UnderlaySoftness,
            _GradientScale,
            _TopColor,
            _BottomColor,
            _BillboardScale,
            Max
        }

        [Flags]
        public enum ColorChannel
        {
            R = 8,
            G = 4,
            B = 2,
            A = 1,
            RGBA = 15
        }
    }
}
