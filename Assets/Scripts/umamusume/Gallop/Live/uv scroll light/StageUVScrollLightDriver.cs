using System;
using System.Collections.Generic;
using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    public class StageUVScrollLightDriver : MonoBehaviour
    {
        [Header("日志与缓存")]
        [InspectorName("输出详细日志")]
        public bool verboseLog = false;

        [InspectorName("包含未激活的渲染器")]
        public bool includeInactiveRenderers = true;

        [InspectorName("启用时重建缓存")]
        public bool rebuildCacheOnEnable = true;

        [InspectorName("目标缺失时重建缓存")]
        public bool rebuildCacheWhenTargetMissing = true;

        [Header("Shader 基础属性名称")]
        [InspectorName("主纹理属性")]
        public string mainTexProperty = "_MainTex";

        [InspectorName("乘算颜色 0 属性")]
        public string mulColor0Property = "_MulColor0";

        [InspectorName("乘算颜色 1 属性")]
        public string mulColor1Property = "_MulColor1";

        [InspectorName("颜色强度属性")]
        public string colorPowerProperty = "_ColorPower";

        [Header("Shader UV 滚动属性名称")]
        [InspectorName("水平滚动偏移属性")]
        public string scrollOffsetXProperty = "_ScrollOffsetX";

        [InspectorName("垂直滚动偏移属性")]
        public string scrollOffsetYProperty = "_ScrollOffsetY";

        [InspectorName("水平滚动速度属性")]
        public string scrollSpeedXProperty = "_ScrollSpeedX";

        [InspectorName("垂直滚动速度属性")]
        public string scrollSpeedYProperty = "_ScrollSpeedY";

        [InspectorName("经过时间属性")]
        public string elapsedTimeProperty = "_ElapsedTime";

        private LiveTimelineControl _ctl;
        private StageController _stage;

        private readonly Dictionary<string, UVScrollLightController> _controllerMap =
            new Dictionary<string, UVScrollLightController>(64);

        private readonly HashSet<string> _missingLogged =
            new HashSet<string>();

        private void OnEnable()
        {
            BindIfPossible();

            if (rebuildCacheOnEnable)
                RebuildCache();
        }

        private void OnDisable()
        {
            Unbind();
            ReleaseControllers();
        }

        private void LateUpdate()
        {
            if (_ctl == null || _stage == null)
                BindIfPossible();
        }

        private void BindIfPossible()
        {
            Director dir = Director.instance;
            if (!dir)
                return;

            LiveTimelineControl newCtl = dir._liveTimelineControl;
            StageController newStage = dir._stageController;
            if (newCtl == null || newStage == null)
                return;

            if (_ctl == newCtl && _stage == newStage)
                return;

            Unbind();

            _ctl = newCtl;
            _stage = newStage;
            _ctl.OnUpdateUVScrollLight += OnUVScrollLight;

            if (verboseLog)
                Debug.Log("[StageUVScrollLightDriver] bound");
        }

        private void Unbind()
        {
            if (_ctl != null)
                _ctl.OnUpdateUVScrollLight -= OnUVScrollLight;

            _ctl = null;
            _stage = null;
            _missingLogged.Clear();
        }

        private void ReleaseControllers()
        {
            foreach (KeyValuePair<string, UVScrollLightController> kv in _controllerMap)
            {
                kv.Value?.Release();
            }

            _controllerMap.Clear();
        }

        private void OnUVScrollLight(ref UVScrollLightUpdateInfo info)
        {
            if (info.data == null)
                return;

            string materialName = NormalizeMaterialName(info.data.name);
            if (string.IsNullOrEmpty(materialName))
                return;

            if (!_controllerMap.TryGetValue(
                    materialName,
                    out UVScrollLightController controller) ||
                controller == null)
            {
                if (rebuildCacheWhenTargetMissing)
                    RebuildCache();

                _controllerMap.TryGetValue(materialName, out controller);
            }

            if (controller == null)
            {
                if (verboseLog && _missingLogged.Add(materialName))
                {
                    Debug.LogWarning(
                        $"[StageUVScrollLightDriver] controller not found for material: {materialName}");
                }

                return;
            }

            controller.UpdateInfo(ref info);
            controller.Update();
        }

        public void RebuildCache()
        {
            ReleaseControllers();
            _missingLogged.Clear();

            if (_stage == null)
                return;

            Renderer[] renderers =
                _stage.GetComponentsInChildren<Renderer>(includeInactiveRenderers);

            Dictionary<string, List<Material>> materialMap =
                new Dictionary<string, List<Material>>(64);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                Material[] materials;

                try
                {
                    materials = renderer.materials;
                }
                catch (Exception e)
                {
                    if (verboseLog)
                    {
                        Debug.LogWarning(
                            $"[StageUVScrollLightDriver] failed to read materials from " +
                            $"{renderer.name}: {e.Message}");
                    }

                    continue;
                }

                if (materials == null || materials.Length == 0)
                    continue;

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                        continue;

                    string key = NormalizeMaterialName(material.name);
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (!materialMap.TryGetValue(key, out List<Material> list))
                    {
                        list = new List<Material>(4);
                        materialMap.Add(key, list);
                    }

                    list.Add(material);
                }
            }

            foreach (KeyValuePair<string, List<Material>> kv in materialMap)
            {
                if (kv.Value == null || kv.Value.Count == 0)
                    continue;

                _controllerMap[kv.Key] = new UVScrollLightController(
                    kv.Value.ToArray(),
                    false,
                    mainTexProperty,
                    mulColor0Property,
                    mulColor1Property,
                    colorPowerProperty,
                    scrollOffsetXProperty,
                    scrollOffsetYProperty,
                    scrollSpeedXProperty,
                    scrollSpeedYProperty,
                    elapsedTimeProperty);
            }

            if (verboseLog)
            {
                Debug.Log(
                    $"[StageUVScrollLightDriver] cache rebuilt: " +
                    $"controllers={_controllerMap.Count}");
            }
        }

        private static string NormalizeMaterialName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = value.Replace("(Instance)", string.Empty);
            value = value.Replace("(Clone)", string.Empty);

            return value.Trim().ToLowerInvariant();
        }
    }
}