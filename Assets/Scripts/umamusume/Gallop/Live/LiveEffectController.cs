using System;
using System.Collections.Generic;
using System.IO;
using Gallop.Live.Cutt;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gallop.Live
{
    // Director.LoadParticle 0x1a61710; LiveEffectController 0x1a9d2d0..0x1a9d921.
    public sealed class LiveEffectController : IDisposable
    {
        private readonly Dictionary<int, Effect> effects = new Dictionary<int, Effect>();
        private readonly List<UmaDatabaseEntry> resources = new List<UmaDatabaseEntry>();
        private readonly Transform defaultParent, worldOwner;
        private readonly Func<int, Transform> characterTransform;
        private readonly Func<string, int, Transform> stageTransform;
        private readonly int normalLayer, excludedColorCorrectionLayer, transparentLayer;

        public LiveEffectController(Transform defaultParent, Transform worldOwner,
            Func<int, Transform> characterTransform, Func<string, int, Transform> stageTransform,
            int normalLayer, int excludedColorCorrectionLayer, int transparentLayer)
        {
            this.defaultParent = defaultParent;
            this.worldOwner = worldOwner;
            this.characterTransform = characterTransform;
            this.stageTransform = stageTransform;
            this.normalLayer = normalLayer;
            this.excludedColorCorrectionLayer = excludedColorCorrectionLayer;
            this.transparentLayer = transparentLayer;
        }

        private static bool Selected(LiveTimelineEffectData group, TimelinePlayerMode mode,
            Func<int, bool> variationEnabled)
        {
            return group?.keys != null && !group.keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable)
                && group.keys.EnablePlayModeTimeline(mode)
                && (!group.applyVariation || (variationEnabled != null && variationEnabled(group.variationId)));
        }

        public static string GetResourcePath(LiveTimelineEffectData group)
        {
            return string.IsNullOrEmpty(group.folder) ? "3d/Effect/Live/pfb_" + group.name
                : "3d/Effect/Live/" + group.folder + "/pfb_" + group.name;
        }

        // Pass GetWorkSheetBySheetIndex(1), NOT every worksheet. Same predicate as Load.
        public static void CollectResourcePaths(LiveTimelineWorkSheet sheetIndex1, TimelinePlayerMode mode,
            Func<int, bool> variationEnabled, ICollection<string> paths)
        {
            if (sheetIndex1?.effectList == null) return;
            foreach (var group in sheetIndex1.effectList)
                if (Selected(group, mode, variationEnabled)) paths.Add(GetResourcePath(group));
        }

        public void Load(LiveTimelineWorkSheet sheetIndex1, TimelinePlayerMode mode,
            Func<int, bool> variationEnabled)
        {
            Dispose();
            if (sheetIndex1?.effectList == null) return;
            for (int i = 0; i < sheetIndex1.effectList.Count; i++)
            {
                var group = sheetIndex1.effectList[i];
                if (!Selected(group, mode, variationEnabled)) continue;
                string path = GetResourcePath(group);
                GameObject prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    UmaDatabaseEntry entry = FindResourceEntry(path);
                    if (entry == null)
                    {
                        Debug.LogWarning("[LiveEffectController] Missing effect entry: " + path);
                        continue;
                    }
                    AssetBundle bundle = UmaAssetManager.LoadAssetBundle(entry, neverUnload: false, isRecursive: true);
                    // LoadAssetBundle acquires the dependency tree even if the root fails.
                    resources.Add(entry);
                    if (bundle != null) prefab = LoadPrefab(bundle, path);
                }
                if (prefab == null)
                {
                    Debug.LogWarning("[LiveEffectController] Missing effect prefab: " + path);
                    continue;
                }
                var instance = Object.Instantiate(prefab);
                if (instance.GetComponentInChildren<ParticleSystem>(true) == null)
                {
                    Debug.LogWarning("[LiveEffectController] Effect has no particle system: " + path);
                    Object.Destroy(instance);
                    continue;
                }
                effects.Add(i, new Effect(instance, defaultParent, normalLayer, excludedColorCorrectionLayer, transparentLayer));
            }
        }

        public static UmaDatabaseEntry FindResourceEntry(string path)
        {
            var entries = UmaViewerMain.Instance?.AbList;
            if (entries == null) return null;
            if (entries.TryGetValue(path, out var entry)) return entry;
            foreach (var pair in entries)
                if (string.Equals(pair.Key, path, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(pair.Value?.Name, path, StringComparison.OrdinalIgnoreCase)) return pair.Value;
            return null;
        }

        private static GameObject LoadPrefab(AssetBundle bundle, string path)
        {
            var prefab = bundle.LoadAsset<GameObject>(path);
            if (prefab != null) return prefab;
            string file = Path.GetFileName(path);
            foreach (string asset in bundle.GetAllAssetNames())
                if (string.Equals(Path.GetFileNameWithoutExtension(asset), file, StringComparison.OrdinalIgnoreCase))
                    return bundle.LoadAsset<GameObject>(asset);
            return null;
        }

        public static int CharacterIndex(int owner)
        {
            if (owner >= 1 && owner <= 17) return owner - 1;
            if (owner >= 23 && owner <= 24) return owner - 2;
            return -1;
        }

        public static bool IsOwnerEnabled(int owner, Func<int, bool> characterEnabled)
        {
            if (owner == 18 || owner == 19 || owner == 22) return true;
            int index = CharacterIndex(owner);
            return index >= 0 && characterEnabled != null && characterEnabled(index);
        }

        private Transform ResolveOwner(LiveEffectTimeline.EffectUpdateInfo info)
        {
            if (info.owner == 18) return null;
            if (info.owner == 19) return worldOwner;
            if (info.owner == 22) return stageTransform?.Invoke(info.ParentStageObjectName, info.ParentStageObjectNameHash);
            int index = CharacterIndex(info.owner);
            return index >= 0 ? characterTransform?.Invoke(index) : null;
        }

        public void Update(LiveEffectTimeline.EffectUpdateInfo info)
        {
            if (effects.TryGetValue(info.timelineIndex, out var effect)) effect.Update(info, ResolveOwner(info));
        }

        // Run AFTER character transform/scaling and Effect.Update (0x1a6a1d0).
        public void UpdateScale(LiveEffectTimeline.EffectScaleUpdateInfo info)
        {
            if (effects.TryGetValue(info.timelineIndex, out var effect)) effect.UpdateScale(info);
        }

        public void Pause(bool paused)
        {
            foreach (var effect in effects.Values) effect.Pause(paused);
        }

        public void ResetForSeek()
        {
            foreach (var effect in effects.Values) effect.Stop(true);
        }

        public void Dispose()
        {
            foreach (var effect in effects.Values) effect.Dispose();
            effects.Clear();
            for (int i = resources.Count - 1; i >= 0; i--) UmaAssetManager.UnloadAssetBundle(resources[i], false);
            resources.Clear();
        }

        private sealed class Effect : IDisposable
        {
            private static readonly int ColorId = Shader.PropertyToID("_Color");
            private static readonly int MulColorId = Shader.PropertyToID("_MulColor0");
            private static readonly int ColorPowerId = Shader.PropertyToID("_ColorPower");
            private readonly GameObject instance;
            private readonly Transform transform, fallbackParent;
            private readonly Vector3 initialPosition, initialAngle;
            private readonly ParticleSystem root;
            private readonly ParticleSystem[] particles;
            private readonly Renderer[] renderers;
            private readonly Animator[] animators;
            private readonly Dictionary<int, ParticleSystem> namedParticles = new Dictionary<int, ParticleSystem>();
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private Transform owner;
            private bool playing, paused;
            private float speed = 1f;
            private readonly int normalLayer, excludedColorCorrectionLayer, transparentLayer;

            public Effect(GameObject instance, Transform fallbackParent,
                int normalLayer, int excludedColorCorrectionLayer, int transparentLayer)
            {
                this.instance = instance;
                this.fallbackParent = fallbackParent;
                this.normalLayer = normalLayer;
                this.excludedColorCorrectionLayer = excludedColorCorrectionLayer;
                this.transparentLayer = transparentLayer;
                transform = instance.transform;
                initialPosition = transform.localPosition;
                initialAngle = transform.localEulerAngles;
                root = instance.GetComponentInChildren<ParticleSystem>(true);
                particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                renderers = instance.GetComponentsInChildren<Renderer>(true);
                animators = instance.GetComponentsInChildren<Animator>(true);
                foreach (var particle in particles)
                    namedParticles[FNVHash.Generate(particle.gameObject.name)] = particle;
                transform.SetParent(fallbackParent, false);
                SetLayer(normalLayer);
                Stop(true);
            }

            public void Update(LiveEffectTimeline.EffectUpdateInfo info, Transform resolvedOwner)
            {
                if (info.isClear && info.IsUpdateClear) Stop(true);
                foreach (var renderer in renderers) if (renderer != null) renderer.enabled = info.isEnable;
                owner = resolvedOwner;
                Transform parent = owner != null ? owner : fallbackParent;
                if (transform.parent != parent) transform.SetParent(parent, false);
                Vector3 position = info.offset + (info.owner == 18 ? initialPosition : Vector3.zero);
                Vector3 angle = info.offsetAngle + (info.owner == 18 ? initialAngle : Vector3.zero);
                transform.localPosition = position;
                Vector3 linked = transform.position;
                transform.position = new Vector3(info.isLinkOwnerPositionX ? linked.x : position.x,
                    info.isLinkOwnerPositionY ? linked.y : position.y, info.isLinkOwnerPositionZ ? linked.z : position.z);
                if (info.isLinkOwnerRotate) transform.localRotation = Quaternion.Euler(angle);
                else transform.rotation = Quaternion.Euler(angle);
                transform.localScale = info.OffsetScale;
                SetLayer(info.isTransparentFX ? transparentLayer
                    : info.ExcludeColorCorrection ? excludedColorCorrectionLayer : normalLayer);
                speed = info.Speed;
                foreach (var particle in particles)
                {
                    if (particle == null) continue;
                    var main = particle.main;
                    main.simulationSpeed = speed;
                }
                foreach (var animator in animators) if (animator != null) animator.speed = paused ? 0f : speed;
                if (info.IsParticleParam) UpdateParticleParams(info.ParticleParamArray);
                if (playing != info.isPlay)
                {
                    if (!info.isPlay) Stop(false);
                    else
                    {
                        root.Simulate(info.ProgressTime, true, true, false);
                        root.Play(true);
                        foreach (var animator in animators)
                            if (animator != null) { animator.enabled = true; animator.Update(info.ProgressTime); }
                        playing = true;
                    }
                }
                var rootMain = root.main;
                if (rootMain.loop != info.isLoop) rootMain.loop = info.isLoop;
                Color color = info.color;
                if (info.ColorProperty == 1)
                {
                    properties.SetColor(MulColorId, color);
                    properties.SetFloat(ColorPowerId, info.colorPower);
                }
                else
                {
                    color.r *= info.colorPower; color.g *= info.colorPower; color.b *= info.colorPower;
                    if (!info.IsColorPowerRGB) color.a *= info.colorPower;
                    properties.SetColor(ColorId, color);
                }
                foreach (var renderer in renderers) if (renderer != null) renderer.SetPropertyBlock(properties);
                if (paused) Pause(true);
            }

            public void UpdateScale(LiveEffectTimeline.EffectScaleUpdateInfo info)
            {
                if (!info.isEnable || owner == null) return;
                Vector3 scale = transform.localScale, parent = owner.localScale;
                if (!info.isLinkOwnerScaleX) scale.x = SafeDivide(scale.x, parent.x);
                if (!info.isLinkOwnerScaleY) scale.y = SafeDivide(scale.y, parent.y);
                if (!info.isLinkOwnerScaleZ) scale.z = SafeDivide(scale.z, parent.z);
                transform.localScale = scale;
            }

            private static float SafeDivide(float value, float scale) => Mathf.Abs(scale) < 0.000001f ? 0f : value / scale;

            private void UpdateParticleParams(LiveTimelineKeyEffectData.ParticleParam[] parameters)
            {
                if (parameters == null) return;
                foreach (var parameter in parameters)
                {
                    if (parameter == null || !namedParticles.TryGetValue(parameter.ObjectNameHash, out var particle)
                        || particle == null) continue;
                    particle.gameObject.SetActive(parameter.IsEnabled);
                    var main = particle.main;
                    if (!particle.isPlaying && !Mathf.Approximately(main.duration, parameter.MainDuration)) main.duration = parameter.MainDuration;
                    main.loop = parameter.MainLooping;
                    if (parameter.IsUpdateMainSimulationSpace)
                    {
                        ParticleSystemSimulationSpace space = parameter.MainSimulationSpace == 1
                            ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
                        if (parameter.IsSetAllMainSimulationSpace)
                            foreach (var item in particles) { if (item == null) continue; var module = item.main; module.simulationSpace = space; }
                        else main.simulationSpace = space;
                    }
                    var color = main.startColor;
                    if (color.mode == ParticleSystemGradientMode.TwoColors)
                    {
                        color.colorMin = parameter.MainStartColorMin;
                        color.colorMax = parameter.MainStartColorMax;
                        main.startColor = color;
                    }
                    else if (color.mode == ParticleSystemGradientMode.Color)
                    {
                        color.color = parameter.MainStartColorMax;
                        main.startColor = color;
                    }
                    var emission = particle.emission;
                    emission.rateOverTimeMultiplier = parameter.EmissionRateOverTimeMultiplier;
                    var shape = particle.shape;
                    shape.scale = parameter.ShapeScale;
                    if (parameter.IsUpdateRandomSeed)
                    {
                        if (parameter.IsSetAllRandomSeed) foreach (var item in particles) SetRandomSeed(item, parameter.RandomSeed);
                        else SetRandomSeed(particle, parameter.RandomSeed);
                    }
                }
            }

            private static void SetRandomSeed(ParticleSystem particle, int seed)
            {
                if (particle == null || particle.isPlaying) return;
                particle.useAutoRandomSeed = seed == 0;
                if (seed != 0) particle.randomSeed = unchecked((uint)seed);
            }

            private void SetLayer(int layer)
            {
                // A missing configured layer cannot be replaced by an unrelated one.
                if (layer < 0) return;
                SetLayer(instance.transform, layer);
            }

            private static void SetLayer(Transform node, int layer)
            {
                node.gameObject.layer = layer;
                for (int i = 0; i < node.childCount; i++) SetLayer(node.GetChild(i), layer);
            }

            public void Stop(bool clear)
            {
                root.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
                foreach (var animator in animators) if (animator != null) animator.enabled = false;
                playing = false;
            }

            public void Pause(bool value)
            {
                bool wasPaused = paused;
                paused = value;
                if (value) root.Pause(true);
                else if (wasPaused && playing) root.Play(true);
                foreach (var animator in animators) if (animator != null) animator.speed = value ? 0f : speed;
            }

            public void Dispose()
            {
                if (instance == null) return;
                Stop(true);
                Object.Destroy(instance);
            }
        }
    }
}
