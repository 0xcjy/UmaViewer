using System;
using Gallop.RenderPipeline;
using UnityEngine;

namespace Gallop.ImageEffect
{
    [Serializable]
    public class DofDiffusionBloomOverlayParam
    {
        public enum DofDiffusionBloomType
        {
            None = 0,
            DofBloom = 1,
            DiffusionDofBloom = 2,
            Bloom = 3,
            DiffusionBloom = 4,
            Dof = 5,
            OldDof = 6,
            OldDofFastBloom = 7,
            OverlayOnly = 8
        }

        public enum BloomScreenBlendMode
        {
            Screen = 0,
            Add = 1
        }

        [SerializeField]
        private DofDiffusionBloomType _useDofDiffusionBloomType;

        [SerializeField]
        private bool _isEnableOldDof;

        [SerializeField]
        private bool _isEnableDof;

        [SerializeField]
        private bool _isEnableDiffusion;

        [SerializeField]
        private bool _isEnableBloom;

        public bool IsEnableDofAutoDisable { get; set; }

        [Header("Rich DOF")]
        public DepthBlurAndBloom.DofQuality DofQualityType;

        public DepthBlurAndBloom.DofFocalType DofFocalType;

        [SerializeField]
        private Transform _dofFocalTransfrom;

        [SerializeField]
        private Vector3 _dofFocalPosition;

        [SerializeField]
        private float _dofFocalPoint;

        public float DofSmoothness;
        public float DofFocalSize;
        public float DofMaxFocalSize;
        public float DofMaxBlurSpread;

        [SerializeField]
        public float DofForegroundSize;

        [SerializeField]
        public DepthBlurAndBloom.DofBlur DofBlurType;

        [Header("Rich Bloom")]
        [Range(0f, 1f)]
        public float BloomDofWeight;

        [Range(0f, 1.5f)]
        public float BloomThreshold;

        [Range(0f, 15f)]
        public float BloomIntensity;

        [Range(0f, 10f)]
        public float BloomBlurSize;

        public BloomScreenBlendMode BloomBlendMode;

        [Header("Diffusion")]
        [Range(0.1f, 10f)]
        public float DiffusionBlurSize;

        [Range(0f, 2f)]
        public float DiffusionBright;

        [Range(0f, 1f)]
        public float DiffusionThreshold;

        [SerializeField]
        [Range(0f, 2f)]
        public float DiffusionSaturation;

        [SerializeField]
        [Range(0f, 2f)]
        public float DiffusionContrast;

        [NonSerialized]
        public Camera TargetCamera;

        // 官方字段名本身就拼成 Poewr。
        private float _ballBlurPoewrFactor;
        private float _ballBlurBrightnessThreshhold;
        private float _ballBlurBrightnessIntensity;
        private float _ballBlurSpread;
        private bool _isPointBallBlur;

        [Header("ScreenOverlay")]
        [SerializeField]
        private ScreenOverlay _screenOverlay;

        public bool IsEnable
        {
            get
            {
                return _useDofDiffusionBloomType !=
                       DofDiffusionBloomType.None;
            }
        }

        public DofDiffusionBloomType UseDofDiffusionBloomType
        {
            get
            {
                return _useDofDiffusionBloomType;
            }
        }

        public bool IsEnableOldDof
        {
            get
            {
                return _isEnableOldDof;
            }
            set
            {
                _isEnableOldDof = value;
            }
        }

        public bool IsEnableDof
        {
            get
            {
                return _isEnableDof;
            }
            set
            {
                _isEnableDof = value;
            }
        }

        public bool IsEnableDiffusion
        {
            get
            {
                return _isEnableDiffusion;
            }
            set
            {
                _isEnableDiffusion = value;
            }
        }

        public bool IsEnableBloom
        {
            get
            {
                return _isEnableBloom;
            }
            set
            {
                _isEnableBloom = value;
            }
        }

        public bool IsEnableOverlay
        {
            get
            {
                // 官方代码依次短路调用三个 Overlay.IsValid()。
                // ScreenOverlay 构造函数保证三个对象均已创建。
                return _screenOverlay.Overlay1.IsValid() ||
                       _screenOverlay.Overlay2.IsValid() ||
                       _screenOverlay.Overlay3.IsValid();
            }
        }

        public Transform DofFocalTransfrom
        {
            get
            {
                return _dofFocalTransfrom;
            }
            set
            {
                _dofFocalTransfrom = value;
                DofFocalType = (DepthBlurAndBloom.DofFocalType)0;
            }
        }

        public Vector3 DofFocalPosition
        {
            get
            {
                return _dofFocalPosition;
            }
            set
            {
                _dofFocalPosition = value;
                DofFocalType = (DepthBlurAndBloom.DofFocalType)1;
            }
        }

        public float DofFocalPoint
        {
            get
            {
                return _dofFocalPoint;
            }
            set
            {
                _dofFocalPoint = value;
                DofFocalType = (DepthBlurAndBloom.DofFocalType)2;
            }
        }

        public float BallBlurPowerFactor
        {
            get
            {
                return _ballBlurPoewrFactor;
            }
            set
            {
                _ballBlurPoewrFactor = value;
            }
        }

        public float BallBlurBrightnessThreshhold
        {
            get
            {
                return _ballBlurBrightnessThreshhold;
            }
            set
            {
                _ballBlurBrightnessThreshhold = value;
            }
        }

        public float BallBlurBrightnessIntensity
        {
            get
            {
                return _ballBlurBrightnessIntensity;
            }
            set
            {
                _ballBlurBrightnessIntensity = value;
            }
        }

        public float BallBlurSpread
        {
            get
            {
                return _ballBlurSpread;
            }
            set
            {
                _ballBlurSpread = value;
            }
        }

        public bool IsPointBallBlur
        {
            get
            {
                return _isPointBallBlur;
            }
            set
            {
                _isPointBallBlur = value;
            }
        }

        public ScreenOverlay ScreenOverlay
        {
            get
            {
                return _screenOverlay;
            }
        }

        public void DecideDrawType(
            bool isDof,
            bool isDiffusion,
            bool isBloom,
            bool isOverlay)
        {
            if (isDof && GraphicSettings.IsDOF)
            {
                if (_isEnableOldDof)
                {
                    if (isBloom && GraphicSettings.IsBloom)
                    {
                        _useDofDiffusionBloomType =
                            DofDiffusionBloomType.OldDofFastBloom;
                    }
                    else
                    {
                        _useDofDiffusionBloomType =
                            DofDiffusionBloomType.OldDof;
                    }

                    return;
                }

                if (isBloom && GraphicSettings.IsBloom)
                {
                    if (isDiffusion && GraphicSettings.IsDiffution)
                    {
                        _useDofDiffusionBloomType =
                            DofDiffusionBloomType.DiffusionDofBloom;
                    }
                    else
                    {
                        _useDofDiffusionBloomType =
                            DofDiffusionBloomType.DofBloom;
                    }

                    return;
                }

                if (isDiffusion && GraphicSettings.IsDiffution)
                {
                    // 官方即使 Bloom=false，也仍使用该枚举值。
                    _useDofDiffusionBloomType =
                        DofDiffusionBloomType.DiffusionDofBloom;
                }
                else
                {
                    _useDofDiffusionBloomType =
                        DofDiffusionBloomType.Dof;
                }

                return;
            }

            if (isBloom && GraphicSettings.IsBloom)
            {
                if (isDiffusion && GraphicSettings.IsDiffution)
                {
                    _useDofDiffusionBloomType =
                        DofDiffusionBloomType.DiffusionBloom;
                }
                else
                {
                    _useDofDiffusionBloomType =
                        DofDiffusionBloomType.Bloom;
                }

                return;
            }

            if (isOverlay && GraphicSettings.IsScreenOverlay)
            {
                _useDofDiffusionBloomType =
                    DofDiffusionBloomType.OverlayOnly;
            }
            else
            {
                _useDofDiffusionBloomType =
                    DofDiffusionBloomType.None;
            }
        }

        private static void Copy(
            ref ScreenOverlayRender.Parameter src,
            ScreenOverlay.Overlay dst)
        {
            // 官方只复制这些字段。
            // MovieTexture/Mask、MovieScale/Offset、ColorBlendFactor 和
            // _isUVMovieNoScale 均不在该函数的复制范围内。
            dst.postFilmMode = src.PostFilmMode;
            dst.postFilmPower = src.PostFilmPower;
            dst.depthPower = src.DepthPower;
            dst.DepthClip = src.DepthClip;
            dst.postFilmOffsetParam = src.PostFilmOffsetParam;
            dst.postFilmOptionParam = src.PostFilmOptionParam;
            dst.postFilmColor0 = src.PostFilmColor0;
            dst.postFilmColor1 = src.PostFilmColor1;
            dst.postFilmColor2 = src.PostFilmColor2;
            dst.postFilmColor3 = src.PostFilmColor3;
            dst.inverseVignette = src.InverseVignette;
            dst.IsEnableDepth = src.IsEnableDepth;
            dst.layerMode = src.LayerMode;
            dst.colorBlend = src.ColorBlend;
            dst.movieResId = src.MovieResId;
            dst.RollParameter = src.RollParameter;
            dst.ScaleParameter = src.ScaleParameter;
        }

        private static void Copy(
            ScreenOverlay.Overlay src,
            ScreenOverlay.Overlay dst)
        {
            // 与上面的 Parameter -> Overlay 版本完全相同的字段集合。
            dst.postFilmMode = src.postFilmMode;
            dst.postFilmPower = src.postFilmPower;
            dst.depthPower = src.depthPower;
            dst.DepthClip = src.DepthClip;
            dst.postFilmOffsetParam = src.postFilmOffsetParam;
            dst.postFilmOptionParam = src.postFilmOptionParam;
            dst.postFilmColor0 = src.postFilmColor0;
            dst.postFilmColor1 = src.postFilmColor1;
            dst.postFilmColor2 = src.postFilmColor2;
            dst.postFilmColor3 = src.postFilmColor3;
            dst.inverseVignette = src.inverseVignette;
            dst.IsEnableDepth = src.IsEnableDepth;
            dst.layerMode = src.layerMode;
            dst.colorBlend = src.colorBlend;
            dst.movieResId = src.movieResId;
            dst.RollParameter = src.RollParameter;
            dst.ScaleParameter = src.ScaleParameter;
        }

        public void Setup(DofDiffusionBloomOverlayPass.Parameter src)
        {
            _isEnableOldDof = src.IsEnableOldDof;
            _isEnableDof = src.IsEnableDof;
            DofQualityType = src.DofQualityType;

            // 官方通过属性 setter 写入，因此会依次把 FocalType 设为 1、2，
            // 最后再恢复为源参数中的值。
            DofFocalPosition = src.DofFocalPosition;
            DofFocalPoint = src.DofFocalPoint;
            DofFocalType = src.DofFocalType;

            DofSmoothness = src.DofSmoothness;
            DofFocalSize = src.DofFocalSize;
            DofMaxFocalSize = src.DofMaxFocalSize;
            DofMaxBlurSpread = src.DofMaxBlurSpread;
            DofForegroundSize = src.DofForegroundSize;
            DofBlurType = src.DofBlurType;

            _isEnableBloom = src.IsEnableBloom;
            BloomDofWeight = src.BloomDofWeight;
            BloomThreshold = src.BloomThreshold;
            BloomIntensity = src.BloomIntensity;
            BloomBlurSize = src.BloomBlurSize;
            BloomBlendMode = src.BloomBlendMode;

            _isEnableDiffusion = src.IsEnableDiffusion;
            DiffusionBlurSize = src.DiffusionBlurSize;
            DiffusionBright = src.DiffusionBright;
            DiffusionThreshold = src.DiffusionThreshold;
            DiffusionSaturation = src.DiffusionSaturation;
            DiffusionContrast = src.DiffusionContrast;

            Copy(ref src.Overlay1, _screenOverlay.Overlay1);
            Copy(ref src.Overlay2, _screenOverlay.Overlay2);
            Copy(ref src.Overlay3, _screenOverlay.Overlay3);

            // 官方故意不复制：
            // _useDofDiffusionBloomType
            // IsEnableDofAutoDisable
            // _dofFocalTransfrom
            // TargetCamera
            // 全部 BallBlur 字段
        }

        public void Setup(DofDiffusionBloomOverlayParam src)
        {
            _isEnableOldDof = src._isEnableOldDof;
            _isEnableDof = src._isEnableDof;
            DofQualityType = src.DofQualityType;

            DofFocalPosition = src.DofFocalPosition;
            DofFocalPoint = src.DofFocalPoint;
            DofFocalType = src.DofFocalType;

            DofSmoothness = src.DofSmoothness;
            DofFocalSize = src.DofFocalSize;
            DofMaxFocalSize = src.DofMaxFocalSize;
            DofMaxBlurSpread = src.DofMaxBlurSpread;
            DofForegroundSize = src.DofForegroundSize;
            DofBlurType = src.DofBlurType;

            _isEnableBloom = src._isEnableBloom;
            BloomDofWeight = src.BloomDofWeight;
            BloomThreshold = src.BloomThreshold;
            BloomIntensity = src.BloomIntensity;
            BloomBlurSize = src.BloomBlurSize;
            BloomBlendMode = src.BloomBlendMode;

            _isEnableDiffusion = src._isEnableDiffusion;
            DiffusionBlurSize = src.DiffusionBlurSize;
            DiffusionBright = src.DiffusionBright;
            DiffusionThreshold = src.DiffusionThreshold;
            DiffusionSaturation = src.DiffusionSaturation;
            DiffusionContrast = src.DiffusionContrast;

            Copy(src._screenOverlay.Overlay1, _screenOverlay.Overlay1);
            Copy(src._screenOverlay.Overlay2, _screenOverlay.Overlay2);
            Copy(src._screenOverlay.Overlay3, _screenOverlay.Overlay3);

            // 与 Pass.Parameter 重载相同，官方不复制运行时状态、
            // Transform、Camera、AutoDisable 和 BallBlur 字段。
        }

        public void Lerp(
            DofDiffusionBloomOverlayParam src1,
            DofDiffusionBloomOverlayParam src2,
            float t)
        {
            // Vector3.Lerp / Mathf.Lerp / Color.Lerp / Vector4.Lerp
            // 都会把 t 限制在 0..1，和官方调用一致。
            DofFocalPosition = Vector3.Lerp(
                src1.DofFocalPosition,
                src2.DofFocalPosition,
                t);

            DofFocalPoint = Mathf.Lerp(
                src1.DofFocalPoint,
                src2.DofFocalPoint,
                t);

            DofSmoothness = Mathf.Lerp(
                src1.DofSmoothness,
                src2.DofSmoothness,
                t);

            DofFocalSize = Mathf.Lerp(
                src1.DofFocalSize,
                src2.DofFocalSize,
                t);

            DofMaxFocalSize = Mathf.Lerp(
                src1.DofMaxFocalSize,
                src2.DofMaxFocalSize,
                t);

            DofMaxBlurSpread = Mathf.Lerp(
                src1.DofMaxBlurSpread,
                src2.DofMaxBlurSpread,
                t);

            DofForegroundSize = Mathf.Lerp(
                src1.DofForegroundSize,
                src2.DofForegroundSize,
                t);

            BloomDofWeight = Mathf.Lerp(
                src1.BloomDofWeight,
                src2.BloomDofWeight,
                t);

            BloomThreshold = Mathf.Lerp(
                src1.BloomThreshold,
                src2.BloomThreshold,
                t);

            BloomIntensity = Mathf.Lerp(
                src1.BloomIntensity,
                src2.BloomIntensity,
                t);

            BloomBlurSize = Mathf.Lerp(
                src1.BloomBlurSize,
                src2.BloomBlurSize,
                t);

            DiffusionBlurSize = Mathf.Lerp(
                src1.DiffusionBlurSize,
                src2.DiffusionBlurSize,
                t);

            DiffusionBright = Mathf.Lerp(
                src1.DiffusionBright,
                src2.DiffusionBright,
                t);

            DiffusionThreshold = Mathf.Lerp(
                src1.DiffusionThreshold,
                src2.DiffusionThreshold,
                t);

            DiffusionSaturation = Mathf.Lerp(
                src1.DiffusionSaturation,
                src2.DiffusionSaturation,
                t);

            DiffusionContrast = Mathf.Lerp(
                src1.DiffusionContrast,
                src2.DiffusionContrast,
                t);

            Lerp(
                _screenOverlay.Overlay1,
                src1._screenOverlay.Overlay1,
                src2._screenOverlay.Overlay1,
                t);

            Lerp(
                _screenOverlay.Overlay2,
                src1._screenOverlay.Overlay2,
                src2._screenOverlay.Overlay2,
                t);

            Lerp(
                _screenOverlay.Overlay3,
                src1._screenOverlay.Overlay3,
                src2._screenOverlay.Overlay3,
                t);

            // 官方不会在这里插值或选择以下内容：
            // enable bool、DrawType、Quality、BlurType、BlendMode、
            // Transform、Camera、Movie 信息、ColorBlendFactor、BallBlur。
            //
            // 最后一次 DofFocalPoint setter 会使 DofFocalType 保持为枚举值 2，
            // 这是官方行为，不要在这里恢复 src 的 FocalType。
        }

        private static void Lerp(
            ScreenOverlay.Overlay dst,
            ScreenOverlay.Overlay src1,
            ScreenOverlay.Overlay src2,
            float t)
        {
            // 官方 Overlay Lerp 只处理以下连续数值字段。
            dst.postFilmPower = Mathf.Lerp(
                src1.postFilmPower,
                src2.postFilmPower,
                t);

            dst.depthPower = Mathf.Lerp(
                src1.depthPower,
                src2.depthPower,
                t);

            dst.DepthClip = Mathf.Lerp(
                src1.DepthClip,
                src2.DepthClip,
                t);

            dst.postFilmOffsetParam = Vector2.Lerp(
                src1.postFilmOffsetParam,
                src2.postFilmOffsetParam,
                t);

            dst.postFilmOptionParam = Vector4.Lerp(
                src1.postFilmOptionParam,
                src2.postFilmOptionParam,
                t);

            dst.postFilmColor0 = Color.Lerp(
                src1.postFilmColor0,
                src2.postFilmColor0,
                t);

            dst.postFilmColor1 = Color.Lerp(
                src1.postFilmColor1,
                src2.postFilmColor1,
                t);

            dst.postFilmColor2 = Color.Lerp(
                src1.postFilmColor2,
                src2.postFilmColor2,
                t);

            dst.postFilmColor3 = Color.Lerp(
                src1.postFilmColor3,
                src2.postFilmColor3,
                t);

            dst.RollParameter = Vector4.Lerp(
                src1.RollParameter,
                src2.RollParameter,
                t);

            dst.ScaleParameter = Vector4.Lerp(
                src1.ScaleParameter,
                src2.ScaleParameter,
                t);

            // 官方不处理：
            // postFilmMode、inverseVignette、layerMode、colorBlend、
            // movieResId、MovieTexture/Mask、MovieScale/Offset、
            // colorBlendFactor、_isUVMovieNoScale、IsEnableDepth。
        }

        public DofDiffusionBloomOverlayParam()
        {
            _useDofDiffusionBloomType =
                DofDiffusionBloomType.None;

            _isEnableOldDof = false;
            _isEnableDof = false;
            _isEnableDiffusion = false;
            _isEnableBloom = false;

            IsEnableDofAutoDisable = true;

            DofQualityType =
                (DepthBlurAndBloom.DofQuality)1;

            DofFocalType =
                (DepthBlurAndBloom.DofFocalType)1;

            _dofFocalTransfrom = null;
            _dofFocalPosition = Vector3.zero;
            _dofFocalPoint = 1f;

            DofSmoothness = 0.5f;
            DofFocalSize = 0f;
            DofMaxFocalSize = 0f;
            DofMaxBlurSpread = 1.75f;
            DofForegroundSize = 0f;
            DofBlurType = (DepthBlurAndBloom.DofBlur)0;

            BloomDofWeight = 0f;
            BloomThreshold = 0.25f;
            BloomIntensity = 0.75f;
            BloomBlurSize = 1f;
            BloomBlendMode = BloomScreenBlendMode.Add;

            DiffusionBlurSize = 1f;
            DiffusionBright = 1f;
            DiffusionThreshold = 0f;
            DiffusionSaturation = 1f;
            DiffusionContrast = 1f;

            TargetCamera = null;

            _ballBlurPoewrFactor = 0f;
            _ballBlurBrightnessThreshhold = 1f;
            _ballBlurBrightnessIntensity = -1f;
            _ballBlurSpread = 1f;
            _isPointBallBlur = false;

            _screenOverlay = new ScreenOverlay();
        }
    }
}