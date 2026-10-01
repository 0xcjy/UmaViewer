using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    // Field layout and evaluation: native 0x1acf110 / 0x1acedd0.
    public static class LiveEffectTimeline
    {
        public struct EffectUpdateInfo
        {
            public float ProgressTime;
            public bool isEnable, isPlay, isLoop, isClear, IsUpdateClear;
            public int owner, occurrenceSpot, ParentStageObjectNameHash;
            public string ParentStageObjectName;
            public bool IsAttachProps;
            public int PropsIndex;
            public Vector3 offset;
            public bool isLinkOwnerPositionX, isLinkOwnerPositionY, isLinkOwnerPositionZ, isLinkOwnerRotate;
            public bool isTransparentFX, IsColorPowerRGB, IsParticleParam;
            public Color color;
            public float colorPower;
            public int ColorProperty;
            public Vector3 offsetAngle, OffsetScale;
            public LiveTimelineKeyEffectData.ParticleParam[] ParticleParamArray;
            public int timelineIndex;
            public float Speed;
            public bool ExcludeColorCorrection;
        }

        public struct EffectScaleUpdateInfo
        {
            public bool isEnable, isLinkOwnerScaleX, isLinkOwnerScaleY, isLinkOwnerScaleZ;
            public int timelineIndex;
        }

        public static bool Evaluate(LiveTimelineEffectData group, int index, float frame, float time,
            float timeScale, TimelinePlayerMode playMode, Func<int, bool> ownerEnabled,
            out EffectUpdateInfo info)
        {
            info = default;
            if (!Active(group?.keys, frame, playMode)) return false;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, group.keys, frame);
            var a = current as LiveTimelineKeyEffectData;
            if (a == null) return false;
            var b = next as LiveTimelineKeyEffectData;
            // Native tests the NEXT key's IsInterpolateKey, not the current key.
            float t = b != null && b.IsInterpolateKey()
                ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;
            float speed = a.IsTimescaleDisabled ? 1f : timeScale;
            info = new EffectUpdateInfo
            {
                ProgressTime = a.IsSimulate ? Mathf.Max(0f, (time - a.frame / 60f) * speed) : 0f,
                isEnable = ownerEnabled(a.owner), isPlay = a.IsPlay, isLoop = a.IsLoop,
                isClear = a.IsClear, IsUpdateClear = group.updatedKeyFrame != a.frame,
                owner = a.owner, occurrenceSpot = a.occurrenceSpot,
                ParentStageObjectName = a.ParentStageObjectName,
                ParentStageObjectNameHash = a.ParentStageObjectNameHash,
                IsAttachProps = a.IsAttachProps, PropsIndex = a.PropsIndex,
                offset = Vector3.LerpUnclamped(a.offset, b.offset, t),
                isLinkOwnerPositionX = a.IsLinkOwnerPositionX,
                isLinkOwnerPositionY = a.IsLinkOwnerPositionY,
                isLinkOwnerPositionZ = a.IsLinkOwnerPositionZ,
                isLinkOwnerRotate = a.IsLinkOwnerRotate,
                isTransparentFX = a.IsTransparentFX, IsColorPowerRGB = a.IsColorPowerRGB,
                IsParticleParam = a.IsParticleParam,
                color = Color.LerpUnclamped(a.color, b.color, t),
                colorPower = Mathf.LerpUnclamped(a.colorPower, b.colorPower, t),
                ColorProperty = a.ColorProperty,
                offsetAngle = Vector3.LerpUnclamped(a.offsetAngle, b.offsetAngle, t),
                OffsetScale = Vector3.LerpUnclamped(a.offsetScale, b.offsetScale, t),
                ParticleParamArray = a.ParticleParamArray, timelineIndex = index, Speed = speed,
                ExcludeColorCorrection = a.IsIgnoreColorCorrection
            };
            group.updatedKeyFrame = a.frame;
            return true;
        }

        public static bool EvaluateScale(LiveTimelineEffectData group, int index, float frame,
            TimelinePlayerMode playMode, Func<int, bool> ownerEnabled, out EffectScaleUpdateInfo info)
        {
            info = default;
            if (!Active(group?.keys, frame, playMode)) return false;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, group.keys, frame);
            var key = current as LiveTimelineKeyEffectData;
            if (key == null) return false;
            info = new EffectScaleUpdateInfo
            {
                isEnable = ownerEnabled(key.owner), timelineIndex = index,
                isLinkOwnerScaleX = !key.HasAttribute(LiveTimelineKeyEffectData.FLAG_CANCEL_OWNER_SCALE_X),
                isLinkOwnerScaleY = !key.HasAttribute(LiveTimelineKeyEffectData.FLAG_CANCEL_OWNER_SCALE_Y),
                isLinkOwnerScaleZ = !key.HasAttribute(LiveTimelineKeyEffectData.FLAG_CANCEL_OWNER_SCALE_Z)
            };
            return true;
        }

        public static bool EvaluateParticle(LiveTimelineParticleData group, float frame,
            TimelinePlayerMode playMode, out float rate)
        {
            rate = 0f;
            if (!Active(group?.keys, frame, playMode)) return false;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, group.keys, frame);
            var a = current as LiveTimelineKeyParticleData;
            if (a == null) return false;
            var b = next as LiveTimelineKeyParticleData;
            rate = b != null ? Mathf.LerpUnclamped(a.emissionRate, b.emissionRate,
                LiveTimelineControl.CalculateInterpolationValue(a, b, frame)) : a.emissionRate;
            return true;
        }

        public static bool EvaluateParticleGroup(LiveTimelineParticleGroupData group, float frame,
            TimelinePlayerMode playMode, out Vector2 rate)
        {
            rate = default;
            if (!Active(group?.keys, frame, playMode)) return false;
            LiveTimelineControl.FindTimelineKey(out var current, out var next, group.keys, frame);
            var a = current as LiveTimelineKeyParticleGroupData;
            if (a == null) return false;
            var b = next as LiveTimelineKeyParticleGroupData;
            float t = b != null ? LiveTimelineControl.CalculateInterpolationValue(a, b, frame) : 0f;
            b = b ?? a;
            rate = new Vector2(Mathf.LerpUnclamped(a.FlickerDarkRate, b.FlickerDarkRate, t),
                Mathf.LerpUnclamped(a.FlickerLightRate, b.FlickerLightRate, t));
            return true;
        }

        private static bool Active(ILiveTimelineKeyDataList keys, float frame, TimelinePlayerMode mode)
        {
            return keys != null && keys.Count > 0 && !keys.HasAttribute(LiveTimelineKeyDataListAttr.Disable)
                && keys.EnablePlayModeTimeline(mode) && keys[0] != null && frame >= keys[0].frame;
        }
    }
}
