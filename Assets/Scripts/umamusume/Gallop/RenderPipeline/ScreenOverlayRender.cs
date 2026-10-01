using System;
using Gallop.ImageEffect;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Official ScreenOverlayRender flow reconstructed from the IL2CPP implementation.
    ///
    /// The render algorithm, pass selection, keyword order, shader parameters,
    /// temporary-RT ping-pong, and Parameter validity rules follow the official code.
    ///
    /// Three external static values have not yet been tied back to their original
    /// owner types in this game build, so they are exposed as adapters below:
    /// EnablePostFilm, EnableUVMovie, and DefaultPostFilmColor.
    /// </summary>
    public class ScreenOverlayRender
    {
        private static readonly int TEMP_RT0_NAME =
            Shader.PropertyToID("ScreenOverlayRender_RT0");

        private static readonly int TEMP_RT1_NAME =
            Shader.PropertyToID("ScreenOverlayRender_RT1");

        /// <summary>
        /// Adapter for the official global post-film capability byte checked by
        /// PostFilmBlit. Keep true until its original owner is wired in.
        /// </summary>
        public static bool EnablePostFilm = true;

        /// <summary>
        /// Adapter for the official global UV-movie capability byte checked by Apply.
        /// Keep true until its original owner is wired in.
        /// </summary>
        public static bool EnableUVMovie = true;

        /// <summary>
        /// Adapter for the official static Color copied by Parameter.Default().
        /// The remaining Default() values and initialization order are official.
        /// </summary>
        public static Color DefaultPostFilmColor = Color.black;

        private readonly Render _renderer;

        public ScreenOverlayRender()
        {
            _renderer = new Render();
        }

        public void PostFilmBlit(
            ScriptableRenderContext context,
            CommandBuffer cmd,
            RenderTextureHandle source,
            RenderTextureHandle destination,
            Material material,
            ref Parameter overlay1,
            ref Parameter overlay2,
            ref Parameter overlay3,
            int defaultPass,
            int filmPass1st,
            int filmPass2nd)
        {
            // Official early fallback: no context submission or cmd.Clear here.
            if (material == null || !EnablePostFilm)
            {
                Blit(cmd, source, destination, null, -1);
                return;
            }

            bool isValidity1 = overlay1.IsValidity;
            bool isValidity2 = overlay2.IsValidity;
            bool isValidity3 = overlay3.IsValidity;

            RenderTextureHandle currentSource = source;
            RenderTextureHandle currentDestination = destination;

            // A later overlay requires the first result to be preserved in RT0.
            if (isValidity2 || isValidity3)
            {
                currentDestination = RenderTextureHandle.Make(
                    TEMP_RT0_NAME,
                    source.Width,
                    source.Height);

                currentDestination.GetTemporaryRT(
                    cmd,
                    FilterMode.Bilinear);
            }

            if (isValidity1)
            {
                _renderer.Blit(
                    ref overlay1,
                    cmd,
                    currentSource,
                    currentDestination,
                    material,
                    filmPass1st);
            }
            else if (defaultPass >= 0)
            {
                Blit(
                    cmd,
                    currentSource,
                    currentDestination,
                    material,
                    defaultPass);
            }
            else
            {
                Blit(cmd, currentSource, currentDestination, null, -1);
            }

            currentSource = currentDestination;
            currentDestination = destination;

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            if (isValidity2)
            {
                // Overlay3 needs the Overlay2 result to be preserved in RT1.
                if (isValidity3)
                {
                    currentDestination = RenderTextureHandle.Make(
                        TEMP_RT1_NAME,
                        source.Width,
                        source.Height);

                    currentDestination.GetTemporaryRT(
                        cmd,
                        FilterMode.Bilinear);
                }

                _renderer.Blit(
                    ref overlay2,
                    cmd,
                    currentSource,
                    currentDestination,
                    material,
                    filmPass2nd);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                if (currentSource != source)
                    currentSource.ReleaseTemporaryRT(cmd);

                currentSource = currentDestination;
                currentDestination = destination;
            }

            if (isValidity3)
            {
                _renderer.Blit(
                    ref overlay3,
                    cmd,
                    currentSource,
                    currentDestination,
                    material,
                    filmPass2nd);

                if (currentSource != source)
                    currentSource.ReleaseTemporaryRT(cmd);
            }

            // Official behavior: the final overlay command is not submitted here.
            // The outer render pass submits the remaining CommandBuffer commands.
        }

        [Serializable]
        public struct Parameter
        {
            public ScreenOverlay.Overlay.PostFilmMode PostFilmMode;
            public float PostFilmPower;
            public float DepthPower;
            public float DepthClip;
            public Vector2 PostFilmOffsetParam;
            public Vector4 PostFilmOptionParam;
            public Color PostFilmColor0;
            public Color PostFilmColor1;
            public Color PostFilmColor2;
            public Color PostFilmColor3;
            public bool InverseVignette;
            public ScreenOverlay.Overlay.LayerMode LayerMode;
            public ScreenOverlay.Overlay.ColorBlend ColorBlend;
            public int MovieResId;

            public bool IsExistMovieMask { get; private set; }
            public Texture MovieTexture { get; private set; }
            public Texture MovieMaskTexture { get; private set; }
            public Vector2 MovieTextureScale { get; private set; }
            public Vector2 MovieTextureOffset { get; private set; }

            public float ColorBlendFactor;
            public Vector4 RollParameter;
            public Vector4 ScaleParameter;

            // Present in the official Parameter layout. Render.Apply keeps its own
            // runtime copy, exactly as the official implementation does.
            private bool _isUVMovieNoScale;

            public bool IsEnableDepth;

            public void SetMovieInfo(
                Texture texMovie,
                Texture texMask,
                Vector2 scale,
                Vector2 offset)
            {
                MovieTexture = texMovie;
                MovieMaskTexture = texMask;
                MovieTextureScale = scale;
                MovieTextureOffset = offset;
                IsExistMovieMask = texMask != null;
            }

            public void SetRollAngle(float angle)
            {
                RollParameter.x = Gallop.Math.Sinf(angle);
                RollParameter.y = Gallop.Math.Cosf(angle);
            }

            public void SetScale(Vector2 scale)
            {
                // Official behavior intentionally performs no zero protection.
                ScaleParameter.x = 1f / scale.x;
                ScaleParameter.y = 1f / scale.y;
            }

            public void Setup(ScreenOverlay.Overlay src)
            {
                PostFilmMode = src.postFilmMode;
                PostFilmPower = src.postFilmPower;
                DepthPower = src.depthPower;
                DepthClip = src.DepthClip;
                PostFilmOffsetParam = src.postFilmOffsetParam;
                PostFilmOptionParam = src.postFilmOptionParam;
                PostFilmColor0 = src.postFilmColor0;
                PostFilmColor1 = src.postFilmColor1;
                PostFilmColor2 = src.postFilmColor2;
                PostFilmColor3 = src.postFilmColor3;
                InverseVignette = src.inverseVignette;
                LayerMode = src.layerMode;
                ColorBlend = src.colorBlend;
                MovieResId = src.movieResId;
                RollParameter = src.RollParameter;
                ScaleParameter = src.ScaleParameter;
                IsEnableDepth = src.IsEnableDepth;
                // REPORT §10.3-B2a（工作流复核发现）：时间轴端 LivePostFilmTimeline.Evaluate
                // / Copy 均已携带 colorBlendFactor，但本 Setup 此前漏拷，导致
                // ApplyGlobalShaderProperty（PostFilmBlit 内 ShaderManager.PropertyId.
                // _colorBlendFactor）整链恒 0，colorBlend 合成层权重失效。
                ColorBlendFactor = src.colorBlendFactor;
            }

            public static Parameter Default()
            {
                Parameter result = default;

                result.PostFilmMode = (ScreenOverlay.Overlay.PostFilmMode)0;
                result.PostFilmPower = 0f;
                result.DepthPower = 0f;
                result.DepthClip = 2f;
                result.PostFilmOffsetParam = Vector2.zero;
                result.PostFilmOptionParam = Vector4.zero;

                result.PostFilmColor0 = DefaultPostFilmColor;
                result.PostFilmColor1 = DefaultPostFilmColor;
                result.PostFilmColor2 = DefaultPostFilmColor;
                result.PostFilmColor3 = DefaultPostFilmColor;

                result.InverseVignette = false;
                result.LayerMode = (ScreenOverlay.Overlay.LayerMode)0;
                result.ColorBlend = (ScreenOverlay.Overlay.ColorBlend)0;
                result.MovieResId = 0;

                result.IsExistMovieMask = false;
                result.MovieTexture = null;
                result.MovieMaskTexture = null;
                result.MovieTextureScale = Vector2.one;
                result.MovieTextureOffset = Vector2.zero;
                result.ColorBlendFactor = 0f;

                result.RollParameter = Vector4.zero;
                result.ScaleParameter = Vector4.one;
                result._isUVMovieNoScale = false;
                result.IsEnableDepth = true;

                result.SetRollAngle(0f);
                return result;
            }

            public bool IsDepthValid
            {
                get { return IsEnableDepth && IsValidity; }
            }

            public bool IsValidity
            {
                get
                {
                    switch ((int)PostFilmMode)
                    {
                        case 0:
                            return false;

                        case 3:
                        case 4:
                        case 6:
                            return true;

                        case 7:
                            return PostFilmColor0.a > 0f;

                        default:
                            return PostFilmPower > 0f;
                    }
                }
            }
        }

        public class Render
        {
            // Official strings and official array order recovered from metadata.
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

            private bool _isUVMovieNoScale;

            private void SetShaderKeyword(
                CommandBuffer cmd,
                int id,
                string[] keywordArray)
            {
                for (int i = 0; i < keywordArray.Length; i++)
                {
                    if (i != id)
                        cmd.DisableShaderKeyword(keywordArray[i]);
                }

                // Index zero is the official "no enabled keyword" slot.
                if (id > 0 && id < keywordArray.Length)
                    cmd.EnableShaderKeyword(keywordArray[id]);
            }

            private void Apply(
                CommandBuffer cmd,
                ref Parameter param,
                int rtWidth,
                int rtHeight)
            {
                int layerMode = (int)param.LayerMode;

                // Official early return only applies to nonzero LayerMode.
                if (layerMode != 0 && !EnableUVMovie)
                    return;

                SetShaderKeyword(
                    cmd,
                    (int)param.PostFilmMode,
                    SHADER_KEYWORD_MODE);

                if (layerMode == 0)
                {
                    SetShaderKeyword(cmd, 0, SHADER_KEYWORD_BLEND);
                }
                else if (layerMode == 1 || layerMode == 2)
                {
                    int keywordId = layerMode + (int)param.ColorBlend;
                    SetShaderKeyword(cmd, keywordId, SHADER_KEYWORD_BLEND);

                    if (param.MovieTexture != null)
                    {
                        cmd.SetGlobalTexture(
                            ShaderManager.GetPropertyId(
                                ShaderManager.PropertyId._texMovie),
                            param.MovieTexture);

                        int gcd = GreatestCommonDivisor(rtWidth, rtHeight);
                        float reducedWidth = rtWidth / (float)gcd;
                        float reducedHeight = rtHeight / (float)gcd;

                        float baseWidth = 7f;
                        float baseHeight = 3f;

                        if (reducedWidth / reducedHeight < 7f / 3f)
                        {
                            baseWidth = Mathf.Ceil(reducedWidth / 7f) * 7f;
                            baseHeight = Mathf.Ceil(reducedHeight / 3f) * 3f;
                        }

                        param.ScaleParameter.z = reducedWidth / baseWidth;
                        param.ScaleParameter.w = reducedHeight / baseHeight;
                    }
                }

                // LayerMode values outside 0/1/2 intentionally leave the blend
                // keyword state unchanged, matching the official switch flow.
                _isUVMovieNoScale = layerMode == 2;

                if (param.IsExistMovieMask && param.MovieMaskTexture != null)
                {
                    cmd.SetGlobalTexture(
                        ShaderManager.GetPropertyId(
                            ShaderManager.PropertyId._texMovieMask),
                        param.MovieMaskTexture);
                }

                cmd.SetGlobalVector(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._movieScale),
                    new Vector4(
                        param.MovieTextureScale.x,
                        param.MovieTextureScale.y,
                        0f,
                        0f));

                cmd.SetGlobalVector(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._movieOffset),
                    new Vector4(
                        param.MovieTextureOffset.x,
                        param.MovieTextureOffset.y,
                        0f,
                        0f));

                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._colorBlendFactor),
                    param.ColorBlendFactor);
            }

            public void Blit(
                ref Parameter param,
                CommandBuffer cmd,
                RenderTextureHandle sourceRT,
                RenderTextureHandle destinationRT,
                Material material,
                int pass)
            {
                int actualPass = param.InverseVignette
                    ? pass + 1
                    : pass;

                int width = sourceRT.Width;
                int height = sourceRT.Height;

                Apply(cmd, ref param, width, height);

                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmPower),
                    param.PostFilmPower);

                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._DepthPower),
                    param.DepthPower);

                float depthClip = param.DepthClip > 1f
                    ? 0f
                    : 1f - param.DepthClip;

                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._DepthClip),
                    depthClip);

                cmd.SetGlobalVector(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmOffsetParam),
                    new Vector4(
                        param.PostFilmOffsetParam.x,
                        param.PostFilmOffsetParam.y,
                        0f,
                        0f));

                cmd.SetGlobalVector(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmOptionParam),
                    param.PostFilmOptionParam);

                cmd.SetGlobalColor(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmColor0),
                    param.PostFilmColor0);

                cmd.SetGlobalColor(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmColor1),
                    param.PostFilmColor1);

                cmd.SetGlobalColor(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmColor2),
                    param.PostFilmColor2);

                cmd.SetGlobalColor(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmColor3),
                    param.PostFilmColor3);

                // Official code mutates z on the Parameter before submitting it.
                param.RollParameter.z = width / (float)height;

                cmd.SetGlobalVector(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmRollParameter),
                    param.RollParameter);

                cmd.SetGlobalVector(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmScaleParameter),
                    param.ScaleParameter);

                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmIsUVMovieNoScale),
                    _isUVMovieNoScale ? 1f : 0f);

                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmIsInverseVignette),
                    param.InverseVignette ? 1f : 0f);

                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmIsAlphaMasking),
                    param.IsExistMovieMask ? 1f : 0f);

                cmd.SetGlobalFloat(
                    ShaderManager.GetPropertyId(
                        ShaderManager.PropertyId._PostFilmIsWithoutDepth),
                    param.IsEnableDepth ? 0f : 1f);

                ScreenOverlayRender.Blit(
                    cmd,
                    sourceRT,
                    destinationRT,
                    material,
                    actualPass);
            }
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

            return left;
        }

        private static void Blit(
            CommandBuffer cmd,
            RenderTextureHandle source,
            RenderTextureHandle destination,
            Material material,
            int pass)
        {
            if (material == null || pass < 0)
            {
                cmd.Blit(source.RtId, destination.RtId);
            }
            else
            {
                cmd.Blit(
                    source.RtId,
                    destination.RtId,
                    material,
                    pass);
            }
        }
    }
}