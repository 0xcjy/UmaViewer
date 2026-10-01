using UnityEngine;

namespace Gallop.Live
{
    public class LiveFlashLegendController : LiveFlashController
    {
        private const string NAME_FONT_NAME = "FOT-TsukuMin Pro E";
        private const string NAME_FONT_PATH = "Font/Mincho/FOT-TsukuMinPro-E";

        private const string NAME_ROOT0_NAME = "OBJ_mc_legend_live_name00";
        private const string NAME_ROOT1_NAME = "OBJ_mc_legend_live_name01";
        private const string NAME_TEXT_NAME = "TXT_txt_legend_live_name00";

        private const string CATCH_COPY_ROOT0_NAME = "OBJ_mc_legend_live_catchcopy00";
        private const string CATCH_COPY_ROOT1_NAME = "OBJ_mc_legend_live_catchcopy01";
        private const string CATCH_COPY_PLANE_NAME = "PLN_dum_legend_live_catchcopy00";

        [SerializeField] private Font _nameFont;
        [SerializeField] private Texture2D _catchCopyTexture;

        public override void Initialize(string flashResourcePath)
        {
            SetupNameFont();
            base.Initialize(flashResourcePath);
        }

        protected override void DestroySub()
        {
            DestroyNameFont();
            _catchCopyTexture = null;
            base.DestroySub();
        }

        private void SetupNameFont()
        {
            _nameFont = LoadOnView<Font>(NAME_FONT_PATH);

            if (_nameFont != null)
                ReplaceFlashFont(NAME_FONT_NAME, _nameFont);
            else
                SetCurrentFlashFont(null);
        }

        private void DestroyNameFont()
        {
            if (_nameFont == null)
                return;

            ReplaceFlashFont(NAME_FONT_NAME, null);
            _nameFont = null;
            SetCurrentFlashFont(null);
        }

        public void Setup(string charaName, string catchCopyImagePath)
        {
            if (!IsInitialized)
                return;

            SetCharacterName(charaName);

            _catchCopyTexture = LoadOnView<Texture2D>(catchCopyImagePath);
            if (_catchCopyTexture != null)
                SetCatchCopyTexture();
        }

        private void SetCharacterName(string charaName)
        {
            SetCharacterNameCore(NAME_ROOT0_NAME, charaName);
            SetCharacterNameCore(NAME_ROOT1_NAME, charaName);
        }

        private void SetCharacterNameCore(string rootName, string charaName)
        {
            if (string.IsNullOrEmpty(rootName))
                return;

            string rootObjectName = GetFlashRootObjectName(rootName);
            if (string.IsNullOrEmpty(rootObjectName))
                return;

            SetFlashText(charaName, NAME_TEXT_NAME, true, rootObjectName);
        }

        private void SetCatchCopyTexture()
        {
            SetCatchCopyTextureCore(CATCH_COPY_ROOT0_NAME);
            SetCatchCopyTextureCore(CATCH_COPY_ROOT1_NAME);
        }

        private void SetCatchCopyTextureCore(string rootName)
        {
            if (string.IsNullOrEmpty(rootName) || _catchCopyTexture == null)
                return;

            string rootObjectName = GetFlashRootObjectName(rootName);
            if (string.IsNullOrEmpty(rootObjectName))
                return;

            SetFlashTexture(CATCH_COPY_PLANE_NAME, _catchCopyTexture, 0, true, false, rootObjectName);
        }
    }
}
