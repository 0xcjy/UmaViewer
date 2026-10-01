using System;
using System.Collections.Generic;
using System.Globalization;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEngine;

namespace Gallop.Live
{
    // Native StageController.InitializeSunShaftsControl 0x1a863b0,
    // UpdateVolumeLight 0x1a95860 and Director.OnUpdateLightShafts 0x1a636c0.
    public sealed class LiveVolumeLightController
    {
        private SunShaftsPass.Parameter _sun = SunShaftsPass.Parameter.Default();
        private IndirectLightShaftsPass.Parameter _shafts;
        private float _sunPower, _sunIntensity, _fadeStart, _fadeMix;

        public SunShaftsPass.Parameter SunShafts => _sun;
        public IndirectLightShaftsPass.Parameter LightShafts => _shafts;

        public void Initialize(Transform stageRoot, string sunObjectName,
            LiveTimelineSunShaftsSettings settings, Camera camera,
            Func<string, GameObject> stageObjectLookup = null)
        {
            _sun = SunShaftsPass.Parameter.Default();
            _sun.SunTransform = ResolveSunTransform(stageRoot, sunObjectName, stageObjectLookup);
            _sun.TargetCamera = camera;
            _sun.Resolution = settings != null ? settings.resolution : 1;
            _sun.ScreenBlendMode = settings != null ? settings.screenBlendMode : 1;
            _sun.SunColor = settings != null ? settings.sunColor : Color.white;
            _sunPower = settings != null ? settings.sunPower : 0f;
            _sun.SunPower = _sunPower;
            _sun.CenterBrightness = settings != null ? settings.sunCenterBrightness : 1.5f;
            _sun.CenterMultiplex = settings != null ? settings.sunCenterMultiplex : 1f;
            _sunIntensity = settings != null ? settings.intensity : 0.5f;
            _fadeStart = settings != null ? settings.fadeStart : 40f;
            _sun.Intensity = _sunIntensity;
            _fadeMix = settings != null ? settings.fadeMix : 18f;
            _sun.BlackLevel = settings != null ? settings.blackLevel : 0.1f;
            _sun.KomorebiRate = settings != null ? settings.komorebiRate : 0f;
            _sun.BlurRadius = settings != null ? settings.blurRadius : 5f;
            _sun.BlurIterations = settings != null ? settings.blurIterations : 2;
            _sun.IsEnabledBorderClear = settings != null && settings.isEnabledBorderClear;
            _shafts = default;
        }

        public void SetLightShaftsTextures(LiveTimelineIndirectLightShaftsSettings settings, Texture[] textures)
        {
            _shafts.ShaftType = settings != null ? (int)settings.shaftType : 0;
            _shafts.Textures = textures;
            _shafts.MaskTexture = textures != null && textures.Length > 2 ? textures[2] : Texture2D.blackTexture;
        }

        public void UpdateVolume(LiveVolumeLightTimeline.VolumeUpdateInfo info)
        {
            _sun.IsEnable = info.Enable;
            _sunPower = info.Power;
            if (_sun.SunTransform != null) _sun.SunTransform.localPosition = info.SunPosition;
            _sun.SunColor = info.Color;
            _sun.BlurRadius = info.BlurRadius;
            _sun.KomorebiRate = info.Komorebi;
            _sun.ColorLerpRate = info.ColorRate;
            _sun.ScreenColorPower = info.ScreenColorPower;
            _sun.EffectColorPower = info.EffectColorPower;
            _sun.IsEnabledBorderClear = info.BorderClear;
            _sun.IsEnabledBlurAlpha = info.BlurAlpha;
        }

        public void UpdateLightShafts(LiveVolumeLightTimeline.ShaftsUpdateInfo info)
        {
            _shafts.IsEnable = info.Enable;
            // Native writes timeline offset directly to shader _Offset and converts
            // angle degrees to turns for shader _Angle.
            _shafts.Speed = info.Speed;
            _shafts.Offset = info.Offset;
            _shafts.Angle = info.Angle / 360f;
            _shafts.Scale = info.Scale;
            _shafts.Alpha = info.Alpha;
            _shafts.Alpha2 = info.Alpha2;
            _shafts.MaskAlpha = info.MaskAlpha;
        }

        // Bind the currently active timeline camera after camera/transform evaluation, before publishing.
        // Native LateUpdateSunShaftsControl 0x1a87500 and CalcSunShaftsRate 0x1b0d450.
        public void LateUpdateSunShafts(Camera camera)
        {
            if (camera != null) _sun.TargetCamera = camera;
            if (!_sun.IsEnable || _sun.SunTransform == null || _sun.TargetCamera == null) return;
            Transform view = _sun.TargetCamera.transform;
            _sun.SunPower = _sunPower * _sunIntensity * CalculateSunShaftsRate(
                view.forward, _sun.SunTransform.position, view.position, _fadeStart, _fadeMix);
        }

        public static float CalculateSunShaftsRate(Vector3 forward, Vector3 sunPosition,
            Vector3 cameraPosition, float fadeStart, float fadeMix)
        {
            forward.Normalize();
            Vector3 toSun = (sunPosition - cameraPosition).normalized;
            float angle = Mathf.Acos(Vector3.Dot(forward, toSun)) * Mathf.Rad2Deg;
            float downDot = Vector3.Dot(forward, Vector3.down);
            if (downDot >= 0f) angle += (90f - Mathf.Acos(downDot) * Mathf.Rad2Deg) * 0.5f;
            float fade = (angle - fadeStart) / fadeMix;
            // Ordered native comparisons preserve NaN rather than inventing a zero-denominator fallback.
            if (fade < 0f) fade = 0f;
            else if (fade > 1f) fade = 1f;
            return 1f - fade;
        }

        // Native ResolveSunShaftsTransform 0x1a8b170: path, stage lookup, recursive child name.
        public static Transform ResolveSunTransform(Transform root, string name,
            Func<string, GameObject> stageObjectLookup = null)
        {
            if (root == null) return null;
            if (string.IsNullOrEmpty(name)) name = "Sunshafts";
            Transform sun = root.Find(name);
            if (sun == null && stageObjectLookup != null)
            {
                GameObject stageObject = stageObjectLookup(name);
                if (stageObject != null) sun = stageObject.transform;
            }
            return sun != null ? sun : FindChildRecursive(root, name);
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name) return child;
                Transform found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }

    public static class LiveLightShaftsResources
    {
        private static readonly string[] DefaultNames =
        {
            "tex_indirectLightShafts_def_A_hq", "tex_indirectLightShafts_def_B_hq",
            "tex_indirectLightShafts_sun_hq"
        };

        // Native Director.GetIndirectLightShaftsTexturePath 0x1a5baa0.
        public static string[] GetTexturePaths(LiveTimelineIndirectLightShaftsSettings settings)
        {
            if (settings == null) return Array.Empty<string>();
            string[] names = settings.shaftType == LiveTimelineIndirectLightShaftsSettings.ShaftType.Vr03
                ? settings.shaftTextureNames : DefaultNames;
            if (names == null || names.Length == 0) return Array.Empty<string>();
            var paths = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
                if (!string.IsNullOrEmpty(names[i]))
                    paths[i] = string.Format(CultureInfo.InvariantCulture,
                        "3d/IndirectLightShafts/lightshafts_{0:0000}/{1}", settings.id, names[i]);
            return paths;
        }

        // Native Director.LoadIndirectLightShaftsTextures 0x1a60bc0 substitutes black per missing asset.
        public static Texture[] LoadTextures(LiveTimelineIndirectLightShaftsSettings settings)
        {
            string[] paths = GetTexturePaths(settings);
            if (paths.Length == 0) return null;
            var textures = new Texture[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                Texture2D texture = string.IsNullOrEmpty(paths[i]) ? null
                    : LiveFlashResourceUtility.LoadOnView<Texture2D>(paths[i]);
                textures[i] = texture != null ? texture : Texture2D.blackTexture;
            }
            return textures;
        }
    }
}
