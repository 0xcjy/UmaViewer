using System;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [Serializable]
    public class LiveTimelineKeyMultiCameraLayerData : LiveTimelineKeyWithInterpolate
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraLayer;
        public Vector3 offsetMaxPosition;
        public Vector3 offsetMinPosition;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraLayerDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraLayerData> { }

    [Serializable]
    public class LiveTimelineMultiCameraLayerData : ILiveTimelineGroupDataWithName
    {
        public LiveTimelineKeyMultiCameraLayerDataList keys = new LiveTimelineKeyMultiCameraLayerDataList();
        public int MultiCameraNo;
        public LiveTimelineMultiCameraLayerData() : base("MultiCameraLayer") { }
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }

    [Serializable]
    public class LiveTimelineMultiCameraPostEffect : ILiveTimelineGroupDataWithName
    {
        public int MultiCameraNo;
        public LiveTimelineMultiCameraPostEffect(string defaultName) : base(defaultName) { }
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraPostFilmData : LiveTimelineKeyPostFilmData
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraPostFilm;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraPostFilmDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraPostFilmData> { }

    [Serializable]
    public class LiveTimelineMultiCameraPostFilmData : LiveTimelineMultiCameraPostEffect
    {
        public LiveTimelineKeyMultiCameraPostFilmDataList keys = new LiveTimelineKeyMultiCameraPostFilmDataList();
        public LiveTimelineMultiCameraPostFilmData() : base("Overlay") { }
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraPostEffectBloomDiffusionData : LiveTimelineKeyPostEffectBloomDiffusionData
    {
        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraPostEffectBloomDiffusion;
    }

    [Serializable]
    public class LiveTimelineKeyMultiCameraPostEffectBloomDiffusionDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraPostEffectBloomDiffusionData> { }

    [Serializable]
    public class LiveTimelineMultiCameraPostEffectBloomDiffusionData : LiveTimelineMultiCameraPostEffect
    {
        public LiveTimelineKeyMultiCameraPostEffectBloomDiffusionDataList keys = new LiveTimelineKeyMultiCameraPostEffectBloomDiffusionDataList();
        public LiveTimelineMultiCameraPostEffectBloomDiffusionData() : base("Bloom/Diffusion") { }
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }
}
