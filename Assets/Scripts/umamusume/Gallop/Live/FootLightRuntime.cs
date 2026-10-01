using System;
using System.Collections.Generic;
using Gallop.Live.Cutt;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gallop.Live
{
    // Spotlight3d. Resource presence is independent of illumination: FollowSpot BgColor1
    // tracks select characters and drive circle power; defaults start dark.
    public sealed class FootLightRuntime : IDisposable
    {
        public const string DefaultSpotlightPath = "3d/env/live/common/spotlight/pfb_env_live_cmn_spotlight000";
        public const string DefaultShadowPath = "3d/env/live/common/shadow/pfb_env_live_cmn_shadow000";
        public const int Layer = 13;

        private sealed class Binding
        {
            public int characterIndex;
            public Transform character;
            public FollowSpotLightController spotlight;
            public FakeShadowController leftShadow;
            public FakeShadowController rightShadow;
        }

        private readonly List<Binding> _bindings = new List<Binding>();
        private Func<int, Vector3> _groundPositionResolver;

        // Optional path resolvers allow an explicitly known per-character resource selection.
        // Null/empty selections always use the default; do not guess how a root names array
        // maps to characters. Resolve/load bundles through the existing Live resource owner.
        public void Initialize(Transform stageRoot, IList<Transform> characterRoots,
            Func<int, Vector3> groundPositionResolver, Func<string, GameObject> prefabResolver,
            Func<int, bool, Transform> footResolver,
            Func<int, string> spotlightPathResolver = null, Func<int, string> shadowPathResolver = null)
        {
            Dispose();
            if (stageRoot == null) throw new ArgumentNullException(nameof(stageRoot));
            if (characterRoots == null) throw new ArgumentNullException(nameof(characterRoots));
            if (groundPositionResolver == null) throw new ArgumentNullException(nameof(groundPositionResolver));
            if (prefabResolver == null) throw new ArgumentNullException(nameof(prefabResolver));
            if (footResolver == null) throw new ArgumentNullException(nameof(footResolver));
            _groundPositionResolver = groundPositionResolver;
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            try
            {
                for (int i = 0; i < characterRoots.Count; i++)
                {
                    Transform character = characterRoots[i];
                    if (character == null) continue;
                    var binding = new Binding { characterIndex = i, character = character };
                    _bindings.Add(binding);
                    GameObject spotlight = InstantiateResource(spotlightPathResolver?.Invoke(i),
                        DefaultSpotlightPath, stageRoot, prefabResolver, prefabs);
                    if (spotlight != null)
                    {
                        binding.spotlight = spotlight.GetComponent<FollowSpotLightController>();
                        if (binding.spotlight == null)
                        {
                            DestroyInstance(spotlight);
                            Debug.LogError("[FootLight] Authored spotlight prefab has no FollowSpotLightController.");
                        }
                        else binding.spotlight.Bind(character, character);
                    }
                    binding.leftShadow = CreateFootShadow(i, true, footResolver(i, true), character,
                        shadowPathResolver?.Invoke(i), stageRoot, prefabResolver, prefabs);
                    binding.rightShadow = CreateFootShadow(i, false, footResolver(i, false), character,
                        shadowPathResolver?.Invoke(i), stageRoot, prefabResolver, prefabs);
                }
                AlterLateUpdate();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static FakeShadowController CreateFootShadow(int index, bool left, Transform foot,
            Transform character, string path, Transform stageRoot, Func<string, GameObject> resolver,
            Dictionary<string, GameObject> prefabs)
        {
            if (foot == null)
            {
                Debug.LogError("[FootLight] Missing " + (left ? "left" : "right") + " ankle for character " + index);
                return null;
            }
            var instance = InstantiateResource(path, DefaultShadowPath, stageRoot, resolver, prefabs);
            if (instance == null) return null;
            var controller = instance.GetComponent<FakeShadowController>();
            if (controller == null)
            {
                DestroyInstance(instance);
                Debug.LogError("[FootLight] Authored shadow prefab has no FakeShadowController.");
                return null;
            }
            instance.name += left ? "_LeftFoot" : "_RightFoot";
            controller.Bind(character, foot);
            return controller;
        }

        private static GameObject InstantiateResource(string path, string defaultPath, Transform parent,
            Func<string, GameObject> resolver, Dictionary<string, GameObject> prefabs)
        {
            if (string.IsNullOrEmpty(path)) path = defaultPath;
            if (!prefabs.TryGetValue(path, out GameObject prefab))
            {
                prefab = resolver(path);
                prefabs.Add(path, prefab);
                if (prefab == null) Debug.LogError("[FootLight] Missing authored prefab " + path);
            }
            if (prefab == null) return null;
            GameObject instance = Object.Instantiate(prefab, parent, false);
            SetLayer(instance.transform);
            return instance;
        }

        public void ApplySpotlight(ref BgColor1UpdateInfo info)
        {
            LiveCharaPositionFlag group;
            switch (info.TimelineName)
            {
                case "FollowSpotCenter": group = LiveCharaPositionFlag.Center; break;
                case "FollowSpotLeft": group = LiveCharaPositionFlag.Left; break;
                case "FollowSpotRight": group = LiveCharaPositionFlag.Right; break;
                case "FollowSpotBack": group = LiveCharaPositionFlag.Back; break;
                default: return;
            }
            foreach (var binding in _bindings)
            {
                int mask = info.flags != 0 ? info.flags : (int)group;
                bool selected = (mask & (1 << binding.characterIndex)) != 0;
                if (selected && binding.spotlight != null)
                    binding.spotlight.ApplyLight(info.color, info.colorPower, info.scale);
            }
        }

        // Call after timeline formation placement, before rendering any presentation,
        // auxiliary, monitor or reflection camera. No autonomous Unity LateUpdate ordering.
        public void AlterLateUpdate()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                Binding binding = _bindings[i];
                bool active = binding.character != null && binding.character.gameObject.activeInHierarchy;
                if (binding.spotlight != null && binding.spotlight.gameObject.activeSelf != active)
                    binding.spotlight.gameObject.SetActive(active);
                if (binding.leftShadow != null && binding.leftShadow.gameObject.activeSelf != active)
                    binding.leftShadow.gameObject.SetActive(active);
                if (binding.rightShadow != null && binding.rightShadow.gameObject.activeSelf != active)
                    binding.rightShadow.gameObject.SetActive(active);
                if (!active) continue;
                Vector3 position = _groundPositionResolver(binding.characterIndex);
                if (binding.spotlight != null) binding.spotlight.AlterLateUpdate(position);
                if (binding.spotlight != null) binding.spotlight.SetCharacterVisible(active);
                if (binding.leftShadow != null) binding.leftShadow.AlterLateUpdate(position);
                if (binding.rightShadow != null) binding.rightShadow.AlterLateUpdate(position);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                Binding binding = _bindings[i];
                if (binding.spotlight != null) DestroyInstance(binding.spotlight.gameObject);
                if (binding.leftShadow != null) DestroyInstance(binding.leftShadow.gameObject);
                if (binding.rightShadow != null) DestroyInstance(binding.rightShadow.gameObject);
            }
            _bindings.Clear();
            _groundPositionResolver = null;
        }

        private static void DestroyInstance(GameObject instance)
        {
            instance.SetActive(false);
            Object.Destroy(instance);
        }

        private static void SetLayer(Transform root)
        {
            root.gameObject.layer = Layer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i));
        }
    }
}
