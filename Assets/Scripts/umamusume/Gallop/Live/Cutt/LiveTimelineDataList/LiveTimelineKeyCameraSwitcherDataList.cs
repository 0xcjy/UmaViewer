using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop.Live.Cutt
{
    [System.Serializable]
    public class LiveTimelineKeyCameraSwitcherData : LiveTimelineKey
    {
        public override LiveTimelineKeyDataType dataType
        {
            get{return LiveTimelineKeyDataType.CameraSwitcher;}
        }
        public int cameraIndex;
    }

    [System.Serializable]
    public class LiveTimelineKeyCameraSwitcherDataList : LiveTimelineKeyDataListTemplate<LiveTimelineKeyCameraSwitcherData>
    {

    }
}
