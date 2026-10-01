using System.Collections.Generic;
using Gallop.ImageEffect;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    public static class LiveMultiCameraTimeline
    {
        private static bool Active(ILiveTimelineKeyDataList keys, float frame, TimelinePlayerMode mode)
        {
            return keys != null && keys.Count != 0 &&
                !keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) &&
                keys.EnablePlayModeTimeline(mode) && frame >= keys.At(0).frame;
        }

        private static MultiCamera CameraAt(MultiCamera[] cameras, int index)
        {
            return cameras != null && index >= 0 && index < cameras.Length ? cameras[index] : null;
        }

        public static void EvaluateLayers(IList<LiveTimelineMultiCameraLayerData> groups,
            MultiCamera[] cameras, float frame, TimelinePlayerMode mode)
        {
            if (cameras == null) return;
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null || cameras[i].Composite == null) continue;
                cameras[i].Composite.LayerOffsetMin = Vector3.zero;
                cameras[i].Composite.LayerOffsetRange = Vector3.zero;
            }
            if (groups == null) return;
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var camera = group == null ? null : CameraAt(cameras, group.MultiCameraNo);
                if (camera == null || !Active(group.keys, frame, mode)) continue;
                LiveTimelineControl.FindTimelineKey(out var current, out var next, group.keys, frame);
                var a = current as LiveTimelineKeyMultiCameraLayerData;
                var b = next as LiveTimelineKeyMultiCameraLayerData;
                if (a == null) continue;
                float t = b != null && b.IsInterpolateKey()
                    ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
                b = b ?? a;
                Vector3 min = Vector3.LerpUnclamped(a.offsetMinPosition, b.offsetMinPosition, t);
                Vector3 max = Vector3.LerpUnclamped(a.offsetMaxPosition, b.offsetMaxPosition, t);
                camera.Composite.LayerOffsetMin = min;
                camera.Composite.LayerOffsetRange = max - min;
            }
        }

        public static void EvaluatePostEffects(
            IList<LiveTimelineMultiCameraPostFilmData> film1,
            IList<LiveTimelineMultiCameraPostFilmData> film2,
            IList<LiveTimelineMultiCameraPostFilmData> film3,
            IList<LiveTimelineMultiCameraPostEffectBloomDiffusionData> bloomDiffusion,
            MultiCamera[] cameras, float frame, TimelinePlayerMode mode,
            LivePostFilmTimeline.BlinkColorResolver blinkColor = null)
        {
            if (cameras == null) return;
            // Reset all cameras, including currently disabled ones. Timeline seeks and
            // runtime switchers must not inherit a previous key/sheet's film or bloom.
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null || cameras[i].Composite == null) continue;
                var p = cameras[i].Composite.ImageEffect.DofDiffusionBloomOverlayParam;
                LivePostFilmTimeline.Reset(p.ScreenOverlay.Overlay1);
                LivePostFilmTimeline.Reset(p.ScreenOverlay.Overlay2);
                LivePostFilmTimeline.Reset(p.ScreenOverlay.Overlay3);
                p.IsEnableBloom = false;
                p.IsEnableDiffusion = false;
                p.IsEnableDof = false;
                p.IsEnableOldDof = false;
            }
            EvaluateFilms(film1, cameras, frame, mode, 0, blinkColor);
            EvaluateFilms(film2, cameras, frame, mode, 1, blinkColor);
            EvaluateFilms(film3, cameras, frame, mode, 2, blinkColor);
            if (bloomDiffusion != null)
                for (int i = 0; i < bloomDiffusion.Count; i++)
                {
                    var group = bloomDiffusion[i];
                    var camera = group == null ? null : CameraAt(cameras, group.MultiCameraNo);
                    if (camera == null || !Active(group.keys, frame, mode)) continue;
                    LiveTimelineControl.FindTimelineKey(out var current, out var next, group.keys, frame);
                    var a = current as LiveTimelineKeyPostEffectBloomDiffusionData;
                    var b = next as LiveTimelineKeyPostEffectBloomDiffusionData;
                    if (a == null) continue;
                    float t = b != null && b.IsInterpolateKey()
                        ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
                    b = b ?? a;
                    var p = camera.Composite.ImageEffect.DofDiffusionBloomOverlayParam;
                    p.IsEnableBloom = a.IsEnabledBloom;
                    p.IsEnableDiffusion = a.IsEnabledDiffusion;
                    p.BloomDofWeight = Mathf.LerpUnclamped(a.bloomDofWeight, b.bloomDofWeight, t);
                    p.BloomThreshold = Mathf.LerpUnclamped(a.threshold, b.threshold, t);
                    p.BloomIntensity = Mathf.LerpUnclamped(a.intensity, b.intensity, t);
                    p.BloomBlurSize = Mathf.LerpUnclamped(a.BloomBlurSize, b.BloomBlurSize, t);
                    p.BloomBlendMode = a.BloomBlendMode;
                    p.DiffusionBlurSize = Mathf.LerpUnclamped(a.diffusionBlurSize, b.diffusionBlurSize, t);
                    p.DiffusionBright = Mathf.LerpUnclamped(a.diffusionBright, b.diffusionBright, t);
                    p.DiffusionThreshold = Mathf.LerpUnclamped(a.diffusionThreshold, b.diffusionThreshold, t);
                    p.DiffusionSaturation = Mathf.LerpUnclamped(a.diffusionSaturation, b.diffusionSaturation, t);
                    p.DiffusionContrast = Mathf.LerpUnclamped(a.diffusionContrast, b.diffusionContrast, t);
                }
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null || cameras[i].Composite == null) continue;
                var p = cameras[i].Composite.ImageEffect.DofDiffusionBloomOverlayParam;
                p.TargetCamera = cameras[i].GetCamera();
                p.DecideDrawType(false, p.IsEnableDiffusion, p.IsEnableBloom, p.IsEnableOverlay);
                cameras[i].PostEffectParameter.DofDiffuionBloomOverlay.Setup(p);
                cameras[i].PostEffectParameter.DofDiffuionBloomOverlay.DecideDrawType(false);
                cameras[i].PostEffectParameter.TargetCamera = cameras[i].GetCamera();
            }
        }

        private static void EvaluateFilms(IList<LiveTimelineMultiCameraPostFilmData> groups,
            MultiCamera[] cameras, float frame, TimelinePlayerMode mode, int layer,
            LivePostFilmTimeline.BlinkColorResolver blinkColor)
        {
            if (groups == null) return;
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var camera = group == null ? null : CameraAt(cameras, group.MultiCameraNo);
                if (camera == null) continue;
                var overlay = camera.Composite.ImageEffect.DofDiffusionBloomOverlayParam.ScreenOverlay;
                LivePostFilmTimeline.Evaluate(group.keys, frame, mode,
                    layer == 0 ? overlay.Overlay1 : layer == 1 ? overlay.Overlay2 : overlay.Overlay3,
                    blinkColor);
            }
        }
    }
}
