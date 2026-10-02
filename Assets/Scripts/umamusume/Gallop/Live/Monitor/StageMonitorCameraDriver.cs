using System;
using System.Collections.Generic;
using Gallop.Live.Cutt;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gallop.Live
{
    /// <summary>Captures the authored MonitorCamera tracks for stage monitor materials.</summary>
    [DisallowMultipleComponent]
    public sealed class StageMonitorCameraDriver : MonoBehaviour
    {
        private static Func<LiveTimelineKeyCameraPositionData, LiveTimelineControl,
            LiveTimelineControl.FindTimelineConfig, Vector3> _getPosition = GetPosition;
        private readonly BezierCalcWork _lookAtBezier = new BezierCalcWork();
        private readonly List<Renderer> _screens = new List<Renderer>();
        private readonly List<bool> _screenVisibility = new List<bool>();
        private Camera _captureCamera;
        private CacheCamera _cachedCamera;
        private RenderTexture _texture;
        private bool _screensHidden;
        private bool _hasTimelineCullingMask;
        private int _timelineCullingMask;
        private bool _hasTimelineBackground;
        private Color _timelineBackground;

        public Texture Texture => IsActive ? _texture : null;
        public bool IsActive => isActiveAndEnabled && _captureCamera != null &&
            _captureCamera.enabled && _texture != null && _texture.IsCreated();

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            ReleaseCapture();
        }

        private void OnDestroy()
        {
            ReleaseCapture();
        }

        public void SetScreenRenderers(IList<Renderer> screens)
        {
            RestoreScreens();
            _screens.Clear();
            _screenVisibility.Clear();
            if (screens == null) return;
            for (int i = 0; i < screens.Count; i++)
            {
                Renderer screen = screens[i];
                if (screen == null || _screens.Contains(screen)) continue;
                _screens.Add(screen);
                _screenVisibility.Add(false);
            }
        }

        // No camera render is requested from render callbacks: monitor/reflection
        // cameras cannot recursively trigger another monitor capture. Suppress the
        // screen geometry only while this camera renders to avoid RT feedback.
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != _captureCamera || !IsActive || _screensHidden) return;
            _screensHidden = true;
            for (int i = 0; i < _screens.Count; i++)
            {
                Renderer screen = _screens[i];
                if (screen == null) continue;
                _screenVisibility[i] = screen.forceRenderingOff;
                screen.forceRenderingOff = true;
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == _captureCamera) RestoreScreens();
        }

        private void RestoreScreens()
        {
            if (!_screensHidden) return;
            for (int i = 0; i < _screens.Count; i++)
                if (_screens[i] != null) _screens[i].forceRenderingOff = _screenVisibility[i];
            _screensHidden = false;
        }

        public void ReleaseCapture()
        {
            RestoreScreens();
            _hasTimelineCullingMask = false;
            _hasTimelineBackground = false;
            if (_captureCamera != null)
            {
                _captureCamera.enabled = false;
                _captureCamera.targetTexture = null;
                Destroy(_captureCamera.gameObject);
                _captureCamera = null;
                _cachedCamera = null;
            }
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
                _texture = null;
            }
        }

        public void UpdateCapture(LiveTimelineControl timeline, float currentFrame, LiveTimelineData data)
        {
            RestoreScreens();
            if (_captureCamera != null) _captureCamera.enabled = false;
            if (!isActiveAndEnabled || timeline == null || data == null || data.worksheetList == null) return;

            LiveTimelineWorkSheet positionSheet = null;
            LiveTimelineKeyMonitorCameraPositionData positionKey = null;
            LiveTimelineKeyMonitorCameraPositionData nextPositionKey = null;
            LiveTimelineKeyMonitorCameraLookAtData lookAtKey = null;
            LiveTimelineKeyMonitorCameraLookAtData nextLookAtKey = null;
            LiveTimelineKeyMonitorCameraLayerData layerKey = null;
            LiveTimelineKeyMonitorCameraLayerData nextLayerKey = null;
            ILiveTimelineKeyDataList positionKeys = null;
            for (int i = 0; i < data.worksheetList.Count; i++)
            {
                LiveTimelineWorkSheet sheet = data.worksheetList[i];
                if (sheet == null) continue;
                if (positionKey == null && sheet.monitorCameraPosKeys != null && sheet.monitorCameraPosKeys.Count > 0)
                {
                    var keys = sheet.monitorCameraPosKeys[0]?.keys;
                    if (FindKeys(keys, timeline, currentFrame, out positionKey, out nextPositionKey))
                    {
                        positionSheet = sheet;
                        positionKeys = keys;
                    }
                }
                if (lookAtKey == null && sheet.monitorCameraLookAtKeys != null && sheet.monitorCameraLookAtKeys.Count > 0)
                    FindKeys(sheet.monitorCameraLookAtKeys[0]?.keys, timeline, currentFrame, out lookAtKey, out nextLookAtKey);
                if (layerKey == null && sheet.monitorCameraLayerKeys != null && sheet.monitorCameraLayerKeys.Count > 0)
                    FindKeys(sheet.monitorCameraLayerKeys[0]?.keys, timeline, currentFrame, out layerKey, out nextLayerKey);
            }
            if (positionKey == null || !positionKey.enable || lookAtKey == null) return;

            Director director = Director.instance;
            Camera template = director != null ? director.MainRenderCamera : null;
            if (template == null || template == _captureCamera || template.cameraType != CameraType.Game ||
                !director.IsPresentationCamera(template)) return;
            if (!EnsureCaptureCamera()) return;

            var config = new LiveTimelineControl.FindTimelineConfig
            {
                keyType = LiveTimelineControl.FindTimelineConfig.KeyType.KeyDirect,
                curKey = positionKey,
                nextKey = nextPositionKey,
                posKeys = positionKeys
            };
            if (!timeline.CalculateCameraPos(out Vector3 position, positionSheet, currentFrame,
                _cachedCamera, ref config, ref _getPosition)) return;
            Vector3 lookAt = EvaluateLookAt(timeline, position, lookAtKey, nextLookAtKey, currentFrame);

            _captureCamera.CopyFrom(template);
            // Native AlterUpdate_MonitorCameraPosition (0x1ad5a88..0x1ad5ad1)
            // updates these only on flagged keys; CopyFrom must not erase their state.
            if (((int)positionKey.attribute & 0x20000) != 0)
            {
                _timelineCullingMask = positionKey.GetCullingMask();
                _hasTimelineCullingMask = true;
            }
            if (((int)positionKey.attribute & 0x40000) != 0)
            {
                _timelineBackground = positionKey.BgColor;
                _hasTimelineBackground = true;
            }
            if (_hasTimelineCullingMask) _captureCamera.cullingMask = _timelineCullingMask;
            if (_hasTimelineBackground) _captureCamera.backgroundColor = _timelineBackground;
            _captureCamera.targetTexture = _texture;
            _captureCamera.depth = template.depth - 100f;
            _captureCamera.rect = new Rect(0f, 0f, 1f, 1f);
            _captureCamera.aspect = (float)_texture.width / _texture.height;
            _captureCamera.allowMSAA = false;
            _captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
            _captureCamera.ResetWorldToCameraMatrix();
            _captureCamera.ResetProjectionMatrix();
            _captureCamera.ResetCullingMatrix();

            Quaternion rotation = (lookAt - position).sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookAt - position, Vector3.up) : template.transform.rotation;
            if (layerKey != null)
            {
                float layerRatio = GetRatio(layerKey, nextLayerKey, currentFrame);
                Vector3 min = nextLayerKey != null
                    ? Vector3.LerpUnclamped(layerKey.offsetMinPosition, nextLayerKey.offsetMinPosition, layerRatio)
                    : layerKey.offsetMinPosition;
                Vector3 max = nextLayerKey != null
                    ? Vector3.LerpUnclamped(layerKey.offsetMaxPosition, nextLayerKey.offsetMaxPosition, layerRatio)
                    : layerKey.offsetMaxPosition;
                position += rotation * LayerOffset(min, max, positionKey.charaRelativeBase, timeline.liveCharactorLocators);
                lookAt += rotation * LayerOffset(min, max, lookAtKey.lookAtCharaPos, timeline.liveCharactorLocators);
                if ((lookAt - position).sqrMagnitude > 0.0001f)
                    rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            }
            float ratio = GetRatio(positionKey, nextPositionKey, currentFrame);
            float fov = nextPositionKey != null ? Mathf.LerpUnclamped(positionKey.fov, nextPositionKey.fov, ratio) : positionKey.fov;
            float roll = nextPositionKey != null ? Mathf.LerpUnclamped(positionKey.roll, nextPositionKey.roll, ratio) : positionKey.roll;
            _captureCamera.transform.SetPositionAndRotation(position, rotation * Quaternion.AngleAxis(roll, Vector3.forward));
            _captureCamera.fieldOfView = fov > 0f ? fov : 30f;
            _captureCamera.nearClipPlane = positionKey.nearClip;
            _captureCamera.farClipPlane = positionKey.farClip;
            _captureCamera.enabled = true;
        }

        private bool EnsureCaptureCamera()
        {
            if (_captureCamera == null)
            {
                GameObject go = new GameObject("MonitorCameraCapture");
                go.transform.SetParent(transform, false);
                _captureCamera = go.AddComponent<Camera>();
                _captureCamera.enabled = false;
                _cachedCamera = new CacheCamera(_captureCamera);
                var cameraData = go.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderType = CameraRenderType.Base;
                cameraData.renderPostProcessing = false;
            }
            if (_texture == null)
            {
                _texture = new RenderTexture(1024, 576, 24, RenderTextureFormat.ARGB32)
                {
                    name = "LiveMonitorCameraRT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
            }
            return _texture.IsCreated() || _texture.Create();
        }


        private static bool FindKeys<T>(ILiveTimelineKeyDataList keys, LiveTimelineControl timeline, float frame,
            out T current, out T next) where T : LiveTimelineKey
        {
            current = null;
            next = null;
            if (keys == null || keys.Count == 0 || keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(timeline.PlayMode) || frame < keys.At(0).frame) return false;
            LiveTimelineControl.FindTimelineKey(out var a, out var b, keys, frame);
            current = a as T;
            next = b as T;
            return current != null;
        }

        private static float GetRatio(LiveTimelineKey current, LiveTimelineKeyWithInterpolate next, float frame)
        {
            return next != null && next.IsInterpolateKey()
                ? LiveTimelineControl.CalculateInterpolationValue(current, next, frame) : 0f;
        }

        private static Vector3 GetPosition(LiveTimelineKeyCameraPositionData key, LiveTimelineControl timeline,
            LiveTimelineControl.FindTimelineConfig config) => key.GetValue(timeline);

        private Vector3 EvaluateLookAt(LiveTimelineControl timeline, Vector3 position,
            LiveTimelineKeyMonitorCameraLookAtData current, LiveTimelineKeyMonitorCameraLookAtData next, float frame)
        {
            Vector3 start = current.GetValue(timeline, position);
            if (next == null || !next.IsInterpolateKey()) return start;
            float ratio = GetRatio(current, next, frame);
            int count = next.GetBezierPointCount();
            if (count == 0) return Vector3.LerpUnclamped(start, next.GetValue(timeline, position), ratio);
            Vector3 end = next.GetValue(timeline, Vector3.zero);
            if (next.necessaryToUseNewBezierCalcMethod)
            {
                _lookAtBezier.Set(start, end, count);
                _lookAtBezier.UpdatePoints(next, timeline, Vector3.zero);
                _lookAtBezier.Calc(count, ratio, out Vector3 result);
                return result;
            }
            Vector3 cp1 = next.GetBezierPoint(0, timeline, Vector3.zero);
            Vector3 cp2 = next.GetBezierPoint(1, timeline, Vector3.zero);
            Vector3 cp3 = next.GetBezierPoint(2, timeline, Vector3.zero);
            Vector3 target;
            switch (count)
            {
                case 1: BezierUtil.Calc(ref start, ref end, ref cp1, ratio, out target); return target;
                case 2: BezierUtil.Calc(ref start, ref end, ref cp1, ref cp2, ratio, out target); return target;
                case 3: BezierUtil.Calc(ref start, ref end, ref cp1, ref cp2, ref cp3, ratio, out target); return target;
                default: return Vector3.LerpUnclamped(start, next.GetValue(timeline, position), ratio);
            }
        }

        private static Vector3 LayerOffset(Vector3 min, Vector3 max, LiveCharaPositionFlag flags,
            ILiveTimelineCharactorLocator[] locators)
        {
            // Same height mapping as MultiCameraComposite/GetCameraLayerOffset 0x1ae0ea0.
            float height = 0f;
            int count = 0;
            if ((int)flags <= 0) height = 1f;
            else if (locators != null)
                for (int i = 0; i < locators.Length && i < 20; i++)
                    if (flags.hasFlag((LiveCharaPosition)i) && locators[i] != null)
                    {
                        height += locators[i].liveCharaHeightValue;
                        count++;
                    }
            if (count > 1) height /= count;
            return height > 0f ? min + (max - min) * ((height - 130f) / 60f) : Vector3.zero;
        }
    }
}
