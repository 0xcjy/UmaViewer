using UnityEngine;
using Gallop.Live.Cutt;

namespace Gallop.Live
{
    /// <summary>Renders the dedicated MonitorCamera position/look-at tracks into a screen texture.</summary>
    public sealed class StageMonitorCameraDriver : MonoBehaviour
    {
        private Camera _captureCamera;
        private RenderTexture _texture;
        private int _lastPositionKeyFrame = int.MinValue;

        public Texture Texture => _texture;
        public bool IsActive => _captureCamera != null && _captureCamera.enabled;

        private void OnDestroy()
        {
            if (_captureCamera != null)
                Destroy(_captureCamera.gameObject);
            if (_texture != null)
                Destroy(_texture);
        }

        public void UpdateCapture(LiveTimelineControl timeline, float currentFrame, LiveTimelineData data)
        {
            if (timeline == null || data == null || data.worksheetList == null)
                return;

            LiveTimelineKeyMonitorCameraPositionData positionKey = null;
            LiveTimelineKeyMonitorCameraPositionData nextPositionKey = null;
            LiveTimelineKeyMonitorCameraLookAtData lookAtKey = null;
            LiveTimelineKeyMonitorCameraLookAtData nextLookAtKey = null;
            for (int i = 0; i < data.worksheetList.Count; i++)
            {
                LiveTimelineWorkSheet sheet = data.worksheetList[i];
                if (sheet == null)
                    continue;
                if (positionKey == null && sheet.monitorCameraPosKeys != null && sheet.monitorCameraPosKeys.Count > 0)
                    FindKeys(sheet.monitorCameraPosKeys[0].keys, currentFrame, out positionKey, out nextPositionKey);
                if (lookAtKey == null && sheet.monitorCameraLookAtKeys != null && sheet.monitorCameraLookAtKeys.Count > 0)
                    FindKeys(sheet.monitorCameraLookAtKeys[0].keys, currentFrame, out lookAtKey, out nextLookAtKey);
                if (positionKey != null && lookAtKey != null)
                    break;
            }

            if (positionKey == null)
                return;

            if (!positionKey.enable)
            {
                if (_captureCamera != null)
                    _captureCamera.enabled = false;
                return;
            }

            float positionRatio = GetRatio(positionKey, nextPositionKey, currentFrame);
            Vector3 position = positionKey.GetValue(timeline);
            if (nextPositionKey != null)
                position = Vector3.Lerp(position, nextPositionKey.GetValue(timeline), positionRatio);

            Vector3 lookAt = position + Vector3.forward;
            if (lookAtKey != null)
            {
                lookAt = lookAtKey.GetValue(timeline, position);
                if (nextLookAtKey != null)
                    lookAt = Vector3.Lerp(lookAt, nextLookAtKey.GetValue(timeline, position),
                        GetRatio(lookAtKey, nextLookAtKey, currentFrame));
            }

            EnsureCaptureCamera(positionKey);
            if (_captureCamera == null)
                return;

            Camera renderTemplate = Camera.main;
            if (renderTemplate != null && renderTemplate != _captureCamera)
            {
                _captureCamera.CopyFrom(renderTemplate);
                _captureCamera.targetTexture = _texture;
                _captureCamera.enabled = true;
            }

            _captureCamera.transform.position = position;
            if ((lookAt - position).sqrMagnitude > 0.0001f)
                _captureCamera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            float fov = positionKey.fov;
            if (nextPositionKey != null)
                fov = Mathf.Lerp(fov, nextPositionKey.fov, positionRatio);
            float roll = positionKey.roll;
            if (nextPositionKey != null)
                roll = Mathf.Lerp(roll, nextPositionKey.roll, positionRatio);
            _captureCamera.transform.rotation *= Quaternion.AngleAxis(roll, Vector3.forward);
            _captureCamera.fieldOfView = fov > 0f ? fov : 30f;
            _captureCamera.nearClipPlane = positionKey.nearClip;
            _captureCamera.farClipPlane = positionKey.farClip;

            if (_lastPositionKeyFrame != positionKey.frame)
            {
                _lastPositionKeyFrame = positionKey.frame;
                Debug.Log($"[MonitorCamera] key={positionKey.frame}, pos={position}, lookAt={lookAt}, " +
                    $"mask=0x{_captureCamera.cullingMask:X8}, template='{renderTemplate?.name}'");
            }
        }

        private void EnsureCaptureCamera(LiveTimelineKeyCameraPositionData positionKey)
        {
            if (_captureCamera == null)
            {
                GameObject go = new GameObject("MonitorCameraCapture");
                go.transform.SetParent(transform, false);
                _captureCamera = go.AddComponent<Camera>();
                _captureCamera.enabled = true;
                _captureCamera.depth = -100f;
                _captureCamera.clearFlags = CameraClearFlags.Skybox;
                _captureCamera.fieldOfView = 30f;
                _captureCamera.nearClipPlane = positionKey.nearClip;
                _captureCamera.farClipPlane = positionKey.farClip;
            }

            if (_texture == null || !_texture.IsCreated())
            {
                if (_texture != null)
                    Destroy(_texture);
                _texture = new RenderTexture(1024, 576, 24, RenderTextureFormat.ARGB32)
                {
                    name = "LiveMonitorCameraRT",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _texture.Create();
            }

            _captureCamera.targetTexture = _texture;
            _captureCamera.enabled = true;
        }

        private static void FindKeys(LiveTimelineKeyMonitorCameraPositionDataList keys,
            float frame, out LiveTimelineKeyMonitorCameraPositionData current, out LiveTimelineKeyMonitorCameraPositionData next)
        {
            current = null;
            next = null;
            if (keys == null || keys.Count == 0)
                return;
            LiveTimelineKeyIndex index = keys.FindCurrentKey(frame / LiveTimelineControl.kTargetFpsF);
            current = index?.key as LiveTimelineKeyMonitorCameraPositionData;
            next = index?.nextKey as LiveTimelineKeyMonitorCameraPositionData;
        }

        private static void FindKeys(LiveTimelineKeyMonitorCameraLookAtDataList keys,
            float frame, out LiveTimelineKeyMonitorCameraLookAtData current, out LiveTimelineKeyMonitorCameraLookAtData next)
        {
            current = null;
            next = null;
            if (keys == null || keys.Count == 0)
                return;
            LiveTimelineKeyIndex index = keys.FindCurrentKey(frame / LiveTimelineControl.kTargetFpsF);
            current = index?.key as LiveTimelineKeyMonitorCameraLookAtData;
            next = index?.nextKey as LiveTimelineKeyMonitorCameraLookAtData;
        }

        private static float GetRatio(LiveTimelineKeyWithInterpolate current, LiveTimelineKeyWithInterpolate next, float frame)
        {
            return next != null ? LiveTimelineControl.CalculateInterpolationValue(current, next, frame) : 0f;
        }
    }
}
