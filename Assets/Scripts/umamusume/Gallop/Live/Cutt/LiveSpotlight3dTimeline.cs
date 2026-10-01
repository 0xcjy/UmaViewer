using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    public static class LiveSpotlight3dTimeline
    {
        // Native AlterUpdate_Spotlight3d 0x1ada690. Camera type 1 selects an
        // indexed multicamera when available; every other type uses the main camera.
        public static bool TryEvaluate(LiveTimelineSpotlight3dData group, int index,
            float frame, TimelinePlayerMode playMode, Transform mainCamera,
            Func<int, Transform> multiCamera, out Spotlight3dUpdateInfo info)
        {
            info = default(Spotlight3dUpdateInfo);
            var keys = group?.keys;
            if (group == null || group.CharacterIndex < 0 || keys == null || keys.Count == 0 ||
                keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(playMode) || keys[0] == null || frame < keys[0].frame)
                return false;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, frame);
            var a = current as LiveTimelineKeySpotlight3dData;
            var b = next as LiveTimelineKeySpotlight3dData;
            if (a == null) return false;
            // Native tests the destination key, not the current key.
            float t = b != null && b.IsInterpolateKey()
                ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;
            info.timelineIndex = index;
            info.characterFlag = 1 << group.CharacterIndex;
            info.isActive = a.isActive;
            info.IsEnabledBillboard = a.IsEnabledBillboard;
            info.color = Color.LerpUnclamped(a.color, b.color, t);
            info.colorPower = Mathf.LerpUnclamped(a.colorPower, b.colorPower, t);
            info.localHeight = Mathf.LerpUnclamped(a.localHeight, b.localHeight, t);
            info.position = Vector3.LerpUnclamped(a.position, b.position, t) +
                Vector3.LerpUnclamped(a.characterPosition, b.characterPosition, t);
            info.rotation = Vector3.LerpUnclamped(a.rotation, b.rotation, t);
            info.scale = Vector3.LerpUnclamped(a.scale, b.scale, t);
            info.TargetCameraTransform = mainCamera;
            if (a.targetCameraType == 1 && multiCamera != null)
            {
                Transform camera = multiCamera(a.targetCameraIndex);
                if (camera != null) info.TargetCameraTransform = camera;
            }
            return true;
        }
    }
}
