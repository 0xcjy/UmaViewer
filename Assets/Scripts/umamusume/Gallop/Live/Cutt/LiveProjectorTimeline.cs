using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Native AlterUpdate_Projector: selects keys on integer frames, like the other live light tracks.
    // The evaluated sample is what StageController.UpdateProjector forwards to each mslight fixture.
    public static class LiveProjectorTimeline
    {
        public struct UpdateInfo
        {
            public LiveTimelineProjectorData group;
            public LiveTimelineKeyProjectorData current;
            public LiveTimelineKeyProjectorData next;
            public float progress;
            // Elapsed seconds since the current key; the clip scrub uses `time * speed`.
            public float time;
            public float power;
            public Color color;
            public Vector3 position;
            public float rotate;
            public Vector2 size;
            public int materialID;
            public int motionID;
            public float speed;
            public int lightBlendMode;
        }

        public static bool TryEvaluate(LiveTimelineProjectorData group, float frame,
            TimelinePlayerMode mode, out UpdateInfo info)
        {
            info = default;
            info.group = group;
            var keys = group != null ? group.keys : null;
            int nativeFrame = Mathf.FloorToInt(frame);
            if (keys == null || keys.Count == 0 || keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable) ||
                !keys.EnablePlayModeTimeline(mode) || keys[0] == null || nativeFrame < keys[0].frame)
                return false;

            LiveTimelineControl.FindTimelineKey(out var current, out var next, keys, nativeFrame);
            var a = current as LiveTimelineKeyProjectorData;
            var b = next as LiveTimelineKeyProjectorData;
            if (a == null) return false;

            float t = b != null && b.IsInterpolateKey()
                ? LiveTimelineControl.CalculateInterpolationValue(a, b, nativeFrame) : 0f;
            bool lerp = b != null && b.IsInterpolateKey();

            info.current = a;
            info.next = b;
            info.progress = t;
            info.time = (nativeFrame - a.frame) / 60f;
            info.power = lerp ? Mathf.LerpUnclamped(a.power, b.power, t) : a.power;
            info.color = lerp ? Color.LerpUnclamped(a.color1, b.color1, t) : a.color1;
            info.position = lerp ? Vector3.LerpUnclamped(a.position, b.position, t) : a.position;
            info.rotate = lerp ? Mathf.LerpUnclamped(a.rotate, b.rotate, t) : a.rotate;
            info.size = lerp ? Vector2.LerpUnclamped(a.size, b.size, t) : a.size;
            info.speed = a.speed;
            info.materialID = a.materialID;
            info.motionID = a.motionID;
            info.lightBlendMode = a.LightBlendMode;
            return true;
        }
    }

    public delegate void ProjectorUpdateInfoDelegate(ref LiveProjectorTimeline.UpdateInfo info);
}
