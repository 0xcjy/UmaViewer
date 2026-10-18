using System;
using System.Collections.Generic;
using Gallop.Live.Cutt;
using Gallop.RenderPipeline;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gallop.Live
{
    // StageController.SetupMirrorScanLight + ProjectorController.UpdateStatus/UpdateTransform/
    // UpdateMaterial/UpdateAnimation/UpdateEnable (mirror-scan light, the authored `projecterList`
    // track). Each track group names one fixture (mslight000..003) which the controller prefab
    // exposes through its AssetHolder; the keys drive transform, material, colour and clip scrub.
    public sealed class MirrorScanLightRuntime : IDisposable
    {
        public const string ControllerPath =
            "3d/env/live/common/mirrorscanlight/pfb_env_live_cmn_mslight_controller000";

        private const string MaterialPathFormat =
            "3d/env/live/common/mirrorscanlight/materials/mtl_env_live_cmn_mslight{0:000}";

        // The track's materialID indexes the authored material array (1 = stars, 3 = soft glow).
        private const int MaterialCount = 5;

        private sealed class Binding
        {
            public int hash;
            public GameObject instance;
            public Transform transform;
            public Renderer[] renderers;
            public Animation animation;
            public readonly List<AnimationClip> clips = new List<AnimationClip>();
            public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            public readonly HashSet<int> errors = new HashSet<int>();
        }

        private readonly List<Binding> _bindings = new List<Binding>();
        private readonly Dictionary<int, Binding> _byHash = new Dictionary<int, Binding>();
        private readonly Material[] _materials = new Material[MaterialCount];
        private readonly HashSet<int> _missingGroups = new HashSet<int>();
        private static readonly int MulColor0Id =
            ShaderManager.GetPropertyId(ShaderManager.PropertyId._MulColor0);
        private static readonly int ColorPowerId =
            ShaderManager.GetPropertyId(ShaderManager.PropertyId._ColorPower);
        private GameObject _root;

        public static string GetMaterialPath(int materialId) => string.Format(
            System.Globalization.CultureInfo.InvariantCulture, MaterialPathFormat, materialId);

        public void Initialize(Transform stageRoot, IList<LiveTimelineProjectorData> groups,
            Func<string, GameObject> prefabResolver, Func<int, Material> materialResolver)
        {
            Dispose();
            if (stageRoot == null) throw new ArgumentNullException(nameof(stageRoot));
            GameObject controller = prefabResolver != null ? prefabResolver(ControllerPath) : null;
            if (controller == null)
            {
                Debug.LogError("[MirrorScanLight] Authored controller prefab is unavailable: " + ControllerPath);
                return;
            }
            _root = Object.Instantiate(controller, stageRoot, false);
            _root.name = "mslight_runtime";
            AssetHolder holder = _root.GetComponentInChildren<AssetHolder>(true);
            if (holder == null)
            {
                Debug.LogError("[MirrorScanLight] Controller prefab has no AssetHolder.", _root);
                return;
            }
            for (int i = 0; i < MaterialCount; i++)
                _materials[i] = materialResolver != null ? materialResolver(i) : null;
            if (groups == null) return;
            for (int g = 0; g < groups.Count; g++)
            {
                LiveTimelineProjectorData group = groups[g];
                if (group == null || string.IsNullOrEmpty(group.name)) continue;
                GameObject fixtureAsset = holder.Get<GameObject>(group.name);
                if (fixtureAsset == null)
                {
                    Debug.LogError("[MirrorScanLight] AssetHolder has no fixture for track '" + group.name + "'.");
                    continue;
                }
                // The AssetHolder points at prefab assets inside the bundle, not at scene objects:
                // the controller prefab instantiates empty, so each fixture is spawned here.
                GameObject fixture = Object.Instantiate(fixtureAsset, _root.transform, false);
                fixture.name = "mslight_" + group.name;
                var binding = new Binding
                {
                    hash = group.nameHash,
                    instance = fixture,
                    transform = fixture.transform,
                    renderers = fixture.GetComponentsInChildren<Renderer>(true),
                    animation = fixture.GetComponent<Animation>()
                };
                if (binding.animation != null)
                {
                    binding.animation.playAutomatically = false;
                    foreach (AnimationState state in binding.animation)
                        if (state != null && state.clip != null) binding.clips.Add(state.clip);
                }
                fixture.SetActive(false);
                _bindings.Add(binding);
                if (!_byHash.ContainsKey(binding.hash)) _byHash.Add(binding.hash, binding);
            }
        }

        public void Apply(ref LiveProjectorTimeline.UpdateInfo info)
        {
            if (info.group == null) return;
            if (!_byHash.TryGetValue(info.group.nameHash, out Binding binding))
            {
                if (_missingGroups.Add(info.group.nameHash))
                    Debug.LogError("[MirrorScanLight] Track '" + info.group.name + "' has no authored mslight fixture.");
                return;
            }
            if (info.current == null)
            {
                SetVisible(binding, false);
                return;
            }
            binding.transform.localPosition = info.position;
            binding.transform.localEulerAngles = new Vector3(0f, info.rotate, 0f);
            binding.transform.localScale = new Vector3(info.size.x, 1f, info.size.y);
            // The authored keys switch the fixture off with power = 0; the visible intensity is
            // `_ColorPower`, so keep the renderers hidden while it is zero.
            bool visible = info.power > 0f;
            SetVisible(binding, visible);
            if (!visible) return;
            ApplyMaterial(binding, ref info);
            ApplyAnimation(binding, ref info);
        }

        private void ApplyMaterial(Binding binding, ref LiveProjectorTimeline.UpdateInfo info)
        {
            int index = info.materialID;
            Material material = index >= 0 && index < _materials.Length ? _materials[index] : null;
            if (material == null)
            {
                if (binding.errors.Add(index))
                    Debug.LogError("[MirrorScanLight] Authored material " + index +
                        " is unavailable: " + GetMaterialPath(index));
                return;
            }
            binding.block.Clear();
            if (material.HasProperty(MulColor0Id)) binding.block.SetColor(MulColor0Id, info.color);
            if (material.HasProperty(ColorPowerId)) binding.block.SetFloat(ColorPowerId, info.power);
            binding.block.SetFloat("_ColorPowerMultiply", 1f);
            for (int i = 0; i < binding.renderers.Length; i++)
            {
                Renderer renderer = binding.renderers[i];
                if (renderer == null) continue;
                renderer.sharedMaterial = material;
                renderer.SetPropertyBlock(binding.block);
            }
        }

        // Native UpdateAnimation scrubs the authored clip by `elapsed * speed` instead of letting it
        // free-run, so seeks and pauses stay deterministic.
        private void ApplyAnimation(Binding binding, ref LiveProjectorTimeline.UpdateInfo info)
        {
            if (binding.animation == null || binding.clips.Count == 0) return;
            int index = Mathf.Clamp(info.motionID, 0, binding.clips.Count - 1);
            AnimationClip clip = binding.clips[index];
            if (clip == null) return;
            AnimationState state = null;
            foreach (AnimationState candidate in binding.animation)
                if (candidate != null && candidate.clip == clip) { state = candidate; break; }
            if (state == null) return;
            if (!binding.animation.IsPlaying(state.name))
            {
                binding.animation.Stop();
                binding.animation.Play(state.name);
            }
            float time = info.time * info.speed;
            if (clip.length > 0f) time = Mathf.Repeat(time, clip.length);
            state.time = time;
            state.enabled = true;
            binding.animation.Sample();
            state.enabled = false;
        }

        private static void SetVisible(Binding binding, bool visible)
        {
            if (binding.instance != null && binding.instance.activeSelf != visible)
                binding.instance.SetActive(visible);
        }

        public void Dispose()
        {
            _bindings.Clear();
            _byHash.Clear();
            _missingGroups.Clear();
            Array.Clear(_materials, 0, _materials.Length);
            if (_root == null) return;
            _root.SetActive(false);
            Object.Destroy(_root);
            _root = null;
        }
    }
}
