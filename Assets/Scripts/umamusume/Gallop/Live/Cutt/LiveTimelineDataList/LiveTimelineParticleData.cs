using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyParticleData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.Particle;
        public float emissionRate;
    }

    [Serializable]
    public class LiveTimelineKeyParticleDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyParticleData> { }

    [Serializable]
    public class LiveTimelineParticleData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyParticleDataList keys = new LiveTimelineKeyParticleDataList();
        public LiveTimelineParticleData() : base("Particle") { }
    }

    [Serializable]
    public class LiveTimelineKeyParticleGroupData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.ParticleGroup;
        public float FlickerLightRate;
        public float FlickerDarkRate;
    }

    [Serializable]
    public class LiveTimelineKeyParticleGroupDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyParticleGroupData> { }

    [Serializable]
    public class LiveTimelineParticleGroupData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyParticleGroupDataList keys = new LiveTimelineKeyParticleGroupDataList();
        public LiveTimelineParticleGroupData() : base("ParticleGroup") { }
    }
}
