using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [System.Serializable]
    public class LiveTimelineKeyMultiCameraLookAtData : LiveTimelineKeyCameraLookAtData
    {

        public override LiveTimelineKeyDataType dataType => LiveTimelineKeyDataType.MultiCameraLookAt;
    }

    [System.Serializable]
    public class LiveTimelineKeyMultiCameraLookAtDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyMultiCameraLookAtData>
    {

    }

    [System.Serializable]
    public class LiveTimelineMultiCameraLookAtData : ILiveTimelineGroupDataWithName
    {
        private const string default_name = "MultiCameraLookAt";
        public int MultiCameraNo;
        public LiveTimelineKeyMultiCameraLookAtDataList keys;
        public override ILiveTimelineKeyDataList GetKeyList() => keys;
    }
}
