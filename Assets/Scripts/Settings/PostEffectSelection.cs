using System;
using System.Collections.Generic;

namespace Gallop.RenderPipeline
{
    /// <summary>
    /// Which individual post effects of the live presentation chain are allowed to draw.
    ///
    /// The global switch is <see cref="Config.PostEffects"/> (the "Post effects" checkbox): when it is
    /// off the whole chain is bypassed, exactly as before. These per-effect switches are a second
    /// gate on top of it — an effect draws only when the global switch AND its own tick are on.
    ///
    /// Storage is the list of *disabled* ids in Config, so an effect added later defaults to on.
    /// </summary>
    public enum PostEffectId
    {
        Dof = 0,
        Diffusion,
        Bloom,
        PostFilm,
        RadialBlur,
        TiltShift,
        ChromaticAberration,
        GlobalFog,
        Vortex,
        Fluctuation,
        ColorCorrection,
        Exposure,
        ToneCurve,
        SunShafts,
        LightShafts,
        LensFlare
    }

    public static class PostEffectSelection
    {
        private static readonly HashSet<string> Disabled = new HashSet<string>(StringComparer.Ordinal);
        private static bool _loaded;

        // Panel order and labels.
        public static readonly PostEffectId[] All =
        {
            PostEffectId.Dof,
            PostEffectId.Diffusion,
            PostEffectId.Bloom,
            PostEffectId.PostFilm,
            PostEffectId.RadialBlur,
            PostEffectId.TiltShift,
            PostEffectId.ChromaticAberration,
            PostEffectId.GlobalFog,
            PostEffectId.Vortex,
            PostEffectId.Fluctuation,
            PostEffectId.ColorCorrection,
            PostEffectId.Exposure,
            PostEffectId.ToneCurve,
            PostEffectId.SunShafts,
            PostEffectId.LightShafts,
            PostEffectId.LensFlare
        };

        public static string Label(PostEffectId id)
        {
            switch (id)
            {
                case PostEffectId.Dof: return "Depth of field";
                case PostEffectId.Diffusion: return "Diffusion";
                case PostEffectId.Bloom: return "Bloom";
                case PostEffectId.PostFilm: return "Post film";
                case PostEffectId.RadialBlur: return "Radial blur";
                case PostEffectId.TiltShift: return "Tilt shift";
                case PostEffectId.ChromaticAberration: return "Chromatic aberration";
                case PostEffectId.GlobalFog: return "Global fog";
                case PostEffectId.Vortex: return "Vortex";
                case PostEffectId.Fluctuation: return "Fluctuation";
                case PostEffectId.ColorCorrection: return "Color correction";
                case PostEffectId.Exposure: return "Exposure";
                case PostEffectId.ToneCurve: return "Tone curve";
                case PostEffectId.SunShafts: return "Sun shafts";
                case PostEffectId.LightShafts: return "Light shafts";
                case PostEffectId.LensFlare: return "Lens flare";
                default: return id.ToString();
            }
        }

        /// <summary> Reads the disabled list from Config once (safe to call repeatedly). </summary>
        public static void LoadFromConfig()
        {
            if (_loaded) return;
            _loaded = true;
            Disabled.Clear();
            var config = Config.Instance;
            if (config == null || config.PostEffectsDisabled == null) return;
            for (int i = 0; i < config.PostEffectsDisabled.Count; i++)
            {
                string id = config.PostEffectsDisabled[i];
                if (!string.IsNullOrEmpty(id)) Disabled.Add(id);
            }
        }

        public static bool IsEnabled(PostEffectId id)
        {
            LoadFromConfig();
            return !Disabled.Contains(id.ToString());
        }

        /// <summary> Stores the switch in Config and refreshes the runtime chain. </summary>
        public static void SetEnabled(PostEffectId id, bool enabled)
        {
            LoadFromConfig();
            string key = id.ToString();
            bool changed = enabled ? Disabled.Remove(key) : Disabled.Add(key);
            if (!changed) return;

            var config = Config.Instance;
            if (config != null)
            {
                if (config.PostEffectsDisabled == null)
                    config.PostEffectsDisabled = new List<string>();
                config.PostEffectsDisabled.Clear();
                foreach (string entry in Disabled) config.PostEffectsDisabled.Add(entry);
                config.PostEffectsDisabled.Sort(StringComparer.Ordinal);
                config.UpdateConfig(false);
            }

            UISettingsGraphics.ApplyLivePostEffects();
        }
    }
}
