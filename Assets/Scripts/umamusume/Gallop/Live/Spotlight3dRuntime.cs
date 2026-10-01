using System;
using System.Collections.Generic;
using Gallop.Live.Cutt;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gallop.Live
{
    // CharacterObject's spotlight-only lifecycle, kept outside character/physics code.
    public sealed class Spotlight3dRuntime : IDisposable
    {
        private readonly IList<Transform> _characterRoots;
        private readonly Transform _mainCamera;
        private readonly int _layer;
        private readonly List<Spotlight3dController>[] _controllers;

        public Spotlight3dRuntime(IList<Transform> characterRoots, Transform mainCamera, int layer)
        {
            _characterRoots = characterRoots ?? throw new ArgumentNullException(nameof(characterRoots));
            _mainCamera = mainCamera;
            _layer = layer;
            _controllers = new List<Spotlight3dController>[System.Math.Min(characterRoots.Count, 20)];
            for (int i = 0; i < _controllers.Length; i++)
                _controllers[i] = new List<Spotlight3dController>();
        }

        // Native Director 0x1a5ae30 and CharacterObject 0x1a57fa0:
        // one instance per eligible group, parented to its bound character object.
        // The caller supplies the StageController AssetHolder lookup, not a stage mesh.
        public void CreateControllers(IList<LiveTimelineSpotlight3dData> groups,
            TimelinePlayerMode playMode, Func<int, GameObject> resolvePrefab)
        {
            ClearControllers();
            if (groups == null) return;
            if (resolvePrefab == null) throw new ArgumentNullException(nameof(resolvePrefab));
            for (int index = 0; index < groups.Count; index++)
            {
                var group = groups[index];
                if (group != null) group.UpdateStatus();
                if (group == null || group.CharacterIndex < 0 || group.AssetId < 0 || group.CharacterIndex >= _controllers.Length ||
                    group.keys == null || group.keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                    !group.keys.EnablePlayModeTimeline(playMode)) continue;
                Transform parent = _characterRoots[group.CharacterIndex];
                if (parent == null) continue;
                GameObject prefab = resolvePrefab(group.AssetId);
                if (prefab == null)
                {
                    Debug.LogError("[Spotlight3d] Missing authored prefab " +
                        LiveTimelineSpotlight3dData.GetAssetName(group.AssetId));
                    continue;
                }
                GameObject instance = Object.Instantiate(prefab, parent);
                SetLayerRecursively(instance.transform, _layer);
                Spotlight3dController controller = instance.AddComponent<Spotlight3dController>();
                controller.Initialize(index, _mainCamera);
                _controllers[group.CharacterIndex].Add(controller);
            }
        }

        // Native OnUpdateSpotlight3d 0x1a63e50 / GetSpotlight3dController 0x1a58300.
        public void Update(ref Spotlight3dUpdateInfo info)
        {
            for (int character = 0; character < _controllers.Length; character++)
            {
                if ((info.characterFlag & (1 << character)) == 0) continue;
                var controllers = _controllers[character];
                for (int i = 0; i < controllers.Count; i++)
                {
                    var controller = controllers[i];
                    if (controller != null && controller.TimelineIndex == info.timelineIndex)
                    {
                        controller.SetUpdateInfo(ref info);
                        break;
                    }
                }
            }
        }

        // Invoke after timeline and character placement, before any camera renders.
        public void AlterLateUpdate()
        {
            for (int character = 0; character < _controllers.Length; character++)
            {
                var controllers = _controllers[character];
                for (int i = 0; i < controllers.Count; i++)
                    if (controllers[i] != null) controllers[i].AlterLateUpdate();
            }
        }

        // Native 0x1a57cb0 destroys owned instances; controller destroys cloned materials.
        public void ClearControllers()
        {
            for (int character = 0; character < _controllers.Length; character++)
            {
                var controllers = _controllers[character];
                for (int i = 0; i < controllers.Count; i++)
                {
                    var controller = controllers[i];
                    if (controller == null) continue;
                    controller.gameObject.SetActive(false);
                    Object.Destroy(controller.gameObject);
                }
                controllers.Clear();
            }
        }

        public void Dispose() => ClearControllers();

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
