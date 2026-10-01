using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop.ImageEffect
{
    [Serializable]
    public class ScreenOverlay
    {
        // Official keyword order used by both the legacy image-effect path and
        // Gallop.RenderPipeline.ScreenOverlayRender.
        private static readonly string[] SHADER_KEYWORD_MODE =
        {
            "MODE_NONE",
            "MODE_LERP",
            "MODE_ADD",
            "MODE_MUL",
            "MODE_VIGNETTE_LERP",
            "MODE_VIGNETTE_ADD",
            "MODE_VIGNETTE_MUL",
            "MODE_MONOCHROME",
            "MODE_SCREENBLEND",
            "MODE_VIGNETT_SCREENBLEND"
        };

        private static readonly string[] SHADER_KEYWORD_BLEND =
        {
            "MASK_VIGNETTE",
            "BLEND_NONE",
            "BLEND_LERP",
            "BLEND_ADD",
            "BLEND_MUL"
        };

        // Adapter for the official runtime capability flags. These remain true
        // until their original owner types are connected in the project.
        public static bool EnablePostFilm = true;
        public static bool EnableUVMovie = true;
        public static Color DefaultPostFilmColor = Color.black;

        [Header("Screen Overlay - First layer")]
        [SerializeField]
        private Overlay _overlay1;

        [Header("Screen Overlay - Second layer")]
        [SerializeField]
        private Overlay _overlay2;

        [Header("Screen Overlay - Third layer")]
        [SerializeField]
        private Overlay _overlay3;

        public Overlay Overlay1 => _overlay1;
        public Overlay Overlay2 => _overlay2;
        public Overlay Overlay3 => _overlay3;

        public bool IsEnable
        {
            get
            {
                return (_overlay1 != null && _overlay1.IsValid()) ||
                       (_overlay2 != null && _overlay2.IsValid()) ||
                       (_overlay3 != null && _overlay3.IsValid());
            }
        }

        public bool IsUseDepthTexture
        {
            get
            {
                return (_overlay1 != null && _overlay1.IsDepthValid()) ||
                       (_overlay2 != null && _overlay2.IsDepthValid()) ||
                       (_overlay3 != null && _overlay3.IsDepthValid());
            }
        }

        public ScreenOverlay()
        {
            _overlay1 = new Overlay();
            _overlay2 = new Overlay();
            _overlay3 = new Overlay();
        }

        public void PostFilmBlit(
            RenderTexture source,
            RenderTexture destination,
            Material material,
            int defaultPass,
            int filmPass1st,
            int filmPass2nd)
        {
            if (material == null || !EnablePostFilm)
            {
                Graphics.Blit(source, destination);
                return;
            }

            bool valid1 = _overlay1 != null && _overlay1.IsValid();
            bool valid2 = _overlay2 != null && _overlay2.IsValid();
            bool valid3 = _overlay3 != null && _overlay3.IsValid();

            RenderTexture currentSource = source;
            RenderTexture currentDestination = destination;

            if (valid2 || valid3)
                currentDestination = GetTemporary(source);

            if (valid1)
            {
                _overlay1.Blit(
                    currentSource,
                    currentDestination,
                    material,
                    filmPass1st);
            }
            else if (defaultPass >= 0)
            {
                Graphics.Blit(
                    currentSource,
                    currentDestination,
                    material,
                    defaultPass);
            }
            else
            {
                Graphics.Blit(currentSource, currentDestination);
            }

            currentSource = currentDestination;
            currentDestination = destination;

            if (valid2)
            {
                if (valid3)
                    currentDestination = GetTemporary(source);

                _overlay2.Blit(
                    currentSource,
                    currentDestination,
                    material,
                    filmPass2nd);

                if (currentSource != source)
                    RenderTexture.ReleaseTemporary(currentSource);

                currentSource = currentDestination;
                currentDestination = destination;
            }

            if (valid3)
            {
                _overlay3.Blit(
                    currentSource,
                    currentDestination,
                    material,
                    filmPass2nd);

                if (currentSource != source)
                    RenderTexture.ReleaseTemporary(currentSource);
            }
        }

        public static void SetShaderVariantKeyword(List<string> keywordList)
        {
            if (keywordList == null)
                return;

            // This helper is only used by ShaderManager's warm-up registration.
            // The runtime keyword selection itself is performed by Overlay.Update.
            keywordList.Add(SHADER_KEYWORD_MODE[(int)Overlay.PostFilmMode.Lerp]);
        }

        private static RenderTexture GetTemporary(RenderTexture source)
        {
            if (source == null)
                return RenderTexture.GetTemporary(1, 1, 0);

            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                source.format);

            temporary.filterMode = FilterMode.Bilinear;
            return temporary;
        }

        [Serializable]
        public class Overlay
        {
            public const float DEFAULT_DEPTH_CLIP = 2f;

            public PostFilmMode postFilmMode;
            public float postFilmPower;
            public float depthPower;
            public float DepthClip;
            public Vector2 postFilmOffsetParam;
            public Vector4 postFilmOptionParam;
            public Color postFilmColor0;
            public Color postFilmColor1;
            public Color postFilmColor2;
            public Color postFilmColor3;
            public bool inverseVignette;
            public LayerMode layerMode;
            public ColorBlend colorBlend;
            public int movieResId;

            [SerializeField]
            private bool _isExistMovieMask;

            [SerializeField]
            private Texture _movieTexture;

            [SerializeField]
            private Texture _movieMaskTexture;

            [SerializeField]
            private Vector2 _movieTextureScale;

            [SerializeField]
            private Vector2 _movieTextureOffset;

            public float colorBlendFactor;
            public Vector4 RollParameter;
            public Vector4 ScaleParameter;

            [SerializeField]
            private bool _isUVMovieNoScale;

            public bool IsEnableDepth;

            public bool IsExistMovieMask => _isExistMovieMask;
            public Texture MovieTexture => _movieTexture;
            public Texture MovieMaskTexture => _movieMaskTexture;
            public Vector2 MovieTextureScale => _movieTextureScale;
            public Vector2 MovieTextureOffset => _movieTextureOffset;

            public Overlay()
            {
                postFilmMode = PostFilmMode.None;
                postFilmPower = 0f;
                depthPower = 0f;
                DepthClip = DEFAULT_DEPTH_CLIP;
                postFilmOffsetParam = Vector2.zero;
                postFilmOptionParam = Vector4.zero;

                postFilmColor0 = DefaultPostFilmColor;
                postFilmColor1 = DefaultPostFilmColor;
                postFilmColor2 = DefaultPostFilmColor;
                postFilmColor3 = DefaultPostFilmColor;

                inverseVignette = false;
                layerMode = LayerMode.Color;
                colorBlend = ColorBlend.None;
                movieResId = 0;

                _isExistMovieMask = false;
                _movieTexture = null;
                _movieMaskTexture = null;
                _movieTextureScale = Vector2.one;
                _movieTextureOffset = Vector2.zero;

                colorBlendFactor = 0f;
                RollParameter = Vector4.zero;
                ScaleParameter = Vector4.one;
                _isUVMovieNoScale = false;
                IsEnableDepth = true;

                SetRollAngle(0f);
            }

            public void SetMovieInfo(
                Texture texMovie,
                Texture texMask,
                Vector2 scale,
                Vector2 offset)
            {
                _movieTexture = texMovie;
                _movieMaskTexture = texMask;
                _movieTextureScale = scale;
                _movieTextureOffset = offset;
                _isExistMovieMask = texMask != null;
            }

            public void SetScale(Vector2 scale)
            {
                ScaleParameter.x = 1f / scale.x;
                ScaleParameter.y = 1f / scale.y;
            }

            public void SetRollAngle(float angle)
            {
                RollParameter.x = Mathf.Sin(angle);
                RollParameter.y = Mathf.Cos(angle);
            }

            private static void SetShaderKeyword(
                int id,
                string[] keywordArray,
                Material material)
            {
                if (material == null || keywordArray == null)
                    return;

                for (int i = 0; i < keywordArray.Length; i++)
                {
                    if (i != id)
                        material.DisableKeyword(keywordArray[i]);
                }

                if (id > 0 && id < keywordArray.Length)
                    material.EnableKeyword(keywordArray[id]);
            }

            public void Update(Material material, RenderTexture mainTexture)
            {
                if (material == null)
                    return;

                int layerModeValue = (int)layerMode;

                // This is the same split as ScreenOverlayRender.Render.Apply:
                // the movie block can be skipped while common post-film values
                // are still written below.
                if (layerModeValue == 0 || EnableUVMovie)
                {
                    SetShaderKeyword(
                        (int)postFilmMode,
                        SHADER_KEYWORD_MODE,
                        material);

                    if (layerModeValue == 0)
                    {
                        SetShaderKeyword(0, SHADER_KEYWORD_BLEND, material);
                    }
                    else if (layerModeValue == 1 || layerModeValue == 2)
                    {
                        int keywordId = layerModeValue + (int)colorBlend;
                        SetShaderKeyword(
                            keywordId,
                            SHADER_KEYWORD_BLEND,
                            material);

                        if (_movieTexture != null)
                        {
                            material.SetTexture(
                                ShaderIds.TexMovie,
                                _movieTexture);

                            if (mainTexture != null &&
                                mainTexture.width > 0 &&
                                mainTexture.height > 0)
                            {
                                int gcd = GreatestCommonDivisor(
                                    mainTexture.width,
                                    mainTexture.height);

                                float reducedWidth =
                                    mainTexture.width / (float)gcd;
                                float reducedHeight =
                                    mainTexture.height / (float)gcd;

                                float baseWidth = 7f;
                                float baseHeight = 3f;

                                if (reducedWidth / reducedHeight < 7f / 3f)
                                {
                                    baseWidth =
                                        Mathf.Ceil(reducedWidth / 7f) * 7f;
                                    baseHeight =
                                        Mathf.Ceil(reducedHeight / 3f) * 3f;
                                }

                                ScaleParameter.z = reducedWidth / baseWidth;
                                ScaleParameter.w = reducedHeight / baseHeight;
                            }
                        }
                    }

                    _isUVMovieNoScale = layerModeValue == 2;

                    if (_isExistMovieMask && _movieMaskTexture != null)
                    {
                        material.SetTexture(
                            ShaderIds.TexMovieMask,
                            _movieMaskTexture);
                    }

                    material.SetVector(
                        ShaderIds.MovieScale,
                        new Vector4(
                            _movieTextureScale.x,
                            _movieTextureScale.y,
                            0f,
                            0f));

                    material.SetVector(
                        ShaderIds.MovieOffset,
                        new Vector4(
                            _movieTextureOffset.x,
                            _movieTextureOffset.y,
                            0f,
                            0f));

                    material.SetFloat(
                        ShaderIds.ColorBlendFactor,
                        colorBlendFactor);
                }

                material.SetFloat(ShaderIds.PostFilmPower, postFilmPower);
                material.SetFloat(ShaderIds.DepthPower, depthPower);

                float depthClip = DepthClip > 1f
                    ? 0f
                    : 1f - DepthClip;

                material.SetFloat(ShaderIds.DepthClip, depthClip);

                material.SetVector(
                    ShaderIds.PostFilmOffsetParam,
                    new Vector4(
                        postFilmOffsetParam.x,
                        postFilmOffsetParam.y,
                        0f,
                        0f));

                material.SetVector(
                    ShaderIds.PostFilmOptionParam,
                    postFilmOptionParam);

                material.SetColor(ShaderIds.PostFilmColor0, postFilmColor0);
                material.SetColor(ShaderIds.PostFilmColor1, postFilmColor1);
                material.SetColor(ShaderIds.PostFilmColor2, postFilmColor2);
                material.SetColor(ShaderIds.PostFilmColor3, postFilmColor3);

                if (mainTexture != null && mainTexture.height != 0)
                {
                    RollParameter.z =
                        mainTexture.width / (float)mainTexture.height;
                }

                material.SetVector(
                    ShaderIds.PostFilmRollParameter,
                    RollParameter);

                material.SetVector(
                    ShaderIds.PostFilmScaleParameter,
                    ScaleParameter);

                material.SetFloat(
                    ShaderIds.PostFilmIsUVMovieNoScale,
                    _isUVMovieNoScale ? 1f : 0f);

                material.SetFloat(
                    ShaderIds.PostFilmIsInverseVignette,
                    inverseVignette ? 1f : 0f);

                material.SetFloat(
                    ShaderIds.PostFilmIsAlphaMasking,
                    _isExistMovieMask ? 1f : 0f);

                material.SetFloat(
                    ShaderIds.PostFilmIsWithoutDepth,
                    IsEnableDepth ? 0f : 1f);
            }

            public bool IsDepthValid()
            {
                return IsEnableDepth && IsValid();
            }

            public bool IsValid()
            {
                switch (postFilmMode)
                {
                    case PostFilmMode.None:
                        return false;

                    case PostFilmMode.Mul:
                    case PostFilmMode.VignetteLerp:
                    case PostFilmMode.VignetteMul:
                        return true;

                    case PostFilmMode.Monochrome:
                        return postFilmColor0.a > 0f;

                    default:
                        return postFilmPower > 0f;
                }
            }

            public void Blit(
                RenderTexture source,
                RenderTexture destination,
                Material material,
                int pass)
            {
                if (material == null || pass < 0)
                {
                    Graphics.Blit(source, destination);
                    return;
                }

                int actualPass = inverseVignette
                    ? pass + 1
                    : pass;

                Update(material, source);
                Graphics.Blit(source, destination, material, actualPass);
            }

            private static int GreatestCommonDivisor(int left, int right)
            {
                left = System.Math.Abs(left);
                right = System.Math.Abs(right);

                while (right != 0)
                {
                    int remainder = left % right;
                    left = right;
                    right = remainder;
                }

                return left == 0 ? 1 : left;
            }

            public enum PostFilmMode
            {
                None,
                Lerp,
                Add,
                Mul,
                VignetteLerp,
                VignetteAdd,
                VignetteMul,
                Monochrome,
                ScreenBlend,
                VignetteScreenBlend
            }

            public enum LayerMode
            {
                Color,
                UVMovie,
                UVMovieNoScale
            }

            public enum ColorBlend
            {
                None,
                Lerp,
                Additive,
                Multiply
            }

            private static class ShaderIds
            {
                public static readonly int PostFilmPower =
                    Shader.PropertyToID("_PostFilmPower");
                public static readonly int DepthPower =
                    Shader.PropertyToID("_DepthPower");
                public static readonly int DepthClip =
                    Shader.PropertyToID("_DepthClip");
                public static readonly int PostFilmOffsetParam =
                    Shader.PropertyToID("_PostFilmOffsetParam");
                public static readonly int PostFilmOptionParam =
                    Shader.PropertyToID("_PostFilmOptionParam");
                public static readonly int PostFilmColor0 =
                    Shader.PropertyToID("_PostFilmColor0");
                public static readonly int PostFilmColor1 =
                    Shader.PropertyToID("_PostFilmColor1");
                public static readonly int PostFilmColor2 =
                    Shader.PropertyToID("_PostFilmColor2");
                public static readonly int PostFilmColor3 =
                    Shader.PropertyToID("_PostFilmColor3");
                public static readonly int TexMovie =
                    Shader.PropertyToID("_texMovie");
                public static readonly int TexMovieMask =
                    Shader.PropertyToID("_texMovieMask");
                public static readonly int MovieScale =
                    Shader.PropertyToID("_movieScale");
                public static readonly int MovieOffset =
                    Shader.PropertyToID("_movieOffset");
                public static readonly int ColorBlendFactor =
                    Shader.PropertyToID("_colorBlendFactor");
                public static readonly int PostFilmRollParameter =
                    Shader.PropertyToID("_PostFilmRollParameter");
                public static readonly int PostFilmScaleParameter =
                    Shader.PropertyToID("_PostFilmScaleParameter");
                public static readonly int PostFilmIsUVMovieNoScale =
                    Shader.PropertyToID("_PostFilmIsUVMovieNoScale");
                public static readonly int PostFilmIsInverseVignette =
                    Shader.PropertyToID("_PostFilmIsInverseVignette");
                public static readonly int PostFilmIsAlphaMasking =
                    Shader.PropertyToID("_PostFilmIsAlphaMasking");
                public static readonly int PostFilmIsWithoutDepth =
                    Shader.PropertyToID("_PostFilmIsWithoutDepth");
            }
        }
    }
}