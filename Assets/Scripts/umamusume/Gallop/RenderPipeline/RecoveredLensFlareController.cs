using System;
using System.Collections.Generic;
using Gallop.Live.Cutt;
using UnityEngine;

namespace Gallop.RenderPipeline
{
    // Native StageController.AddLensFlareControllerArray 0x1a7f8b0 / UpdateLensFlare 0x1a91360.
    // This adapter consumes authored CustomLensFlare sources, never creates substitute light effects.
    public sealed class RecoveredLensFlareController
    {
        private sealed class Target
        {
            public CustomLensFlare Flare;
            public Transform Transform;
            public Vector3 InitialPosition;
            public int NameHash;
            public Material Material;
            public bool HasColorPower, HasMulColor0;
            public Color ColorFactor = Color.white;
            public float BrightnessFactor = 1f;
            public bool AutoBrightness = true;
            public bool ScreenFit;
        }

        private readonly Dictionary<int, List<Target>> _groups = new Dictionary<int, List<Target>>();
        private readonly List<Target> _targets = new List<Target>();
        private readonly Plane[] _frustumPlanes = new Plane[6];
        private readonly float _standard;
        private readonly float _under;
        private readonly int _colorPowerId;
        private readonly int _mulColor0Id;

        public RecoveredLensFlareController(Transform root, float standard, float under,
            string nameSuffixToken = "")
        {
            _standard = standard;
            _under = under;
            _colorPowerId = ShaderManager.GetPropertyId(ShaderManager.PropertyId._ColorPower);
            _mulColor0Id = ShaderManager.GetPropertyId(ShaderManager.PropertyId._MulColor0);
            if (root == null) return;
            foreach (var flare in root.GetComponentsInChildren<CustomLensFlare>(true))
            {
                if (flare == null) continue;
                var sourceTransform = flare.transform;
                var renderer = flare.GetComponent<MeshRenderer>();
                Material material = RenderUtils.GetMaterial(renderer);
                var target = new Target
                {
                    Flare = flare,
                    Transform = sourceTransform,
                    InitialPosition = sourceTransform.localPosition,
                    NameHash = FNVHash.Generate(flare.name + nameSuffixToken),
                    Material = material,
                    HasColorPower = material != null && material.HasProperty(_colorPowerId),
                    HasMulColor0 = material != null && material.HasProperty(_mulColor0Id)
                };
                _targets.Add(target);
                if (sourceTransform.parent == null) continue;
                // Native strips every literal Clone token from the parent only.
                int parentHash = FNVHash.Generate(sourceTransform.parent.name.Replace("(Clone)", string.Empty) + nameSuffixToken);
                if (!_groups.TryGetValue(parentHash, out var group))
                {
                    group = new List<Target>();
                    _groups.Add(parentHash, group);
                }
                group.Add(target);
            }
        }

        public void Apply(ref LensFlareUpdateInfo info)
        {
            if (_groups.TryGetValue(info.TimelineNameHash, out var group))
            {
                foreach (var target in group) Apply(target, ref info);
                return;
            }
            foreach (var target in _targets)
            {
                if (target.Flare == null || target.NameHash != info.TimelineNameHash) continue;
                Apply(target, ref info);
                return; // Native individual fallback stops at the first matching controller.
            }
        }

        // UnityLensFlareController.UpdateInfo 0x1aaaf20.
        private static void Apply(Target target, ref LensFlareUpdateInfo info)
        {
            if (target.Flare == null) return;
            if (info.enableParameter)
            {
                target.Transform.localPosition = info.IsOverridePosition
                    ? info.offset : target.InitialPosition + info.offset;
                target.Flare.FadeSpeed = info.fadeSpeed;
                target.ColorFactor = info.color;
                target.BrightnessFactor = info.brightness;
                target.ScreenFit = info.IsScreenFit;
            }
            target.AutoBrightness = info.IsAutoBrightness;
            if (target.Flare.gameObject.activeSelf != info.enable)
                target.Flare.gameObject.SetActive(info.enable);
        }

        // Native AlterUpdate 0x1aaa410. Call after timeline/material/light updates, once per Live frame.
        public void AlterUpdate(Camera camera)
        {
            foreach (var target in _targets)
            {
                if (target.Flare == null || !target.Flare.gameObject.activeInHierarchy) continue;
                Color color = target.ColorFactor;
                if (target.Material != null)
                {
                    Color materialColor = target.HasMulColor0 ? target.Material.GetColor(_mulColor0Id) : Color.white;
                    float colorPower = target.HasColorPower ? target.Material.GetFloat(_colorPowerId) : 1f;
                    color *= materialColor * (colorPower * 2f);
                }
                bool visibleColor = color.r > 0.000001f || color.g > 0.000001f || color.b > 0.000001f;
                float brightness = 0f;
                if (visibleColor && camera != null)
                {
                    brightness = target.BrightnessFactor;
                    if (target.AutoBrightness)
                    {
                        float anglePower = Mathf.Clamp01(-Vector3.Dot(camera.transform.forward, target.Transform.up));
                        if (_standard > brightness && _standard > _under)
                            brightness = Mathf.LerpUnclamped(0f, _standard,
                                Mathf.Clamp01((brightness - _under) / (_standard - _under)));
                        brightness *= anglePower;
                    }
                    else if (target.ScreenFit)
                    {
                        FitOnScreen(target.Transform, camera);
                    }
                }
                // Source positioning is on the authored host above, not on an arbitrary child offset.
                target.Flare.ApplyTimeline(brightness > 0f, false, Vector3.zero,
                    color, brightness, target.Flare.FadeSpeed);
            }
        }

        // FitOnScreen 0x1aaa870 uses localPosition directly against world frustum planes.
        // Retain that native coordinate convention rather than substituting viewport clamping.
        private void FitOnScreen(Transform source, Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, _frustumPlanes);
            Vector3 position = source.localPosition;
            if (_frustumPlanes[4].GetDistanceToPoint(position) < 0f) return;
            Vector3 correction = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                float distance = _frustumPlanes[i].GetDistanceToPoint(position);
                if (distance < 0f)
                    correction -= _frustumPlanes[i].normal * (distance - 0.01f);
            }
            source.localPosition = position + correction;
        }
    }
}
